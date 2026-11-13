using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.World;

namespace Telo.Sim.Town;

public enum KnockResult { NobodyHome, NotInterested, SignedUp, AlreadySigned, TooSoon, LastMile, NoSuchLot }

/// <summary>
/// Street-level work: knocking on doors, odd jobs for cash and goodwill, and fitting sockets in customers' houses.
/// This is the Act 0–1 game: everything is done in person, at a real door, on the planet.
/// </summary>
public static class TownRules
{
    public const long ModemPrice = 35;
    public const int SocketHours = 1, KnockMinutes = 10, RepairMinutes = 45;
    public const Tools RepairNeeds = Tools.Toolbox;

    private static readonly Dictionary<string, int> Interest = new()
    {
        ["curious"] = 600, ["student"] = 500, ["family"] = 450, ["gamer"] = 550, ["small-business"] = 520,
        ["price-sensitive"] = 300, ["reliability-sensitive"] = 360, ["tech-inexperienced"] = 260,
    };

    private static readonly Dictionary<string, string> Rejection = new()
    {
        ["curious"] = "\"Fascinating. Come back when it does something.\"",
        ["student"] = "\"I can barely afford the phone I've got.\"",
        ["family"] = "\"We've a telephone already, thank you.\"",
        ["gamer"] = "\"Does it play games? No? Then no.\"",
        ["small-business"] = "\"Leave a leaflet. I'm with a customer.\"",
        ["price-sensitive"] = "\"How much? A month? Every month?\"",
        ["reliability-sensitive"] = "\"And when it breaks, who do I ring? You? On what?\"",
        ["tech-inexperienced"] = "\"Oh, I wouldn't know what to do with one of those.\"",
    };

    private static readonly (string Title, long Pay, int Hours, LotKind? Only)[] JobKinds =
    {
        ("Rewire a phone extension", 22, 1, null), ("Fix a crackling line", 18, 1, null), ("Tune the TV aerial", 15, 1, null),
        ("Fit an answering machine", 28, 1, null), ("Settle a party-line dispute", 12, 1, null), ("Mend a radio set", 20, 2, null),
        ("Repair the shop's telex machine", 45, 2, LotKind.Shop), ("Restring the line to the barn", 40, 3, LotKind.Farm),
    };

    public static Household HouseholdOf(GameState s, string lotId)
    {
        if (!s.Households.TryGetValue(lotId, out var h)) s.Households[lotId] = h = new Household();
        return h;
    }

    public static Customer? CustomerAt(GameState s, string lotId) => s.Customers.FirstOrDefault(c => c.LotId == lotId && c.Live);
    public static OddJob? JobAt(GameState s, string lotId) => s.Jobs.FirstOrDefault(j => j.LotId == lotId);

    // ---- selling -------------------------------------------------------------------------------
    public static KnockResult Knock(GameState s, ContentDb c, DetRng rng, string lotId)
    {
        if (!c.Lots.TryGetValue(lotId, out var lot)) return KnockResult.NoSuchLot;
        if (lot.Premise is not null) return KnockResult.LastMile;
        if (!Onboarding.CanKnock(s, c) || lot.Kind == LotKind.Depot) return KnockResult.TooSoon;
        var h = HouseholdOf(s, lotId);
        if (h.Status == HouseholdStatus.Signed) return KnockResult.AlreadySigned;
        if (s.Day < h.RetryDay) return KnockResult.TooSoon;

        // nobody answers when nobody is in: people keep hours (see HomeRules). No dice.
        if (!HomeRules.IsHome(s, c, lot)) return KnockResult.NobodyHome;
        int happyNeighbours = s.Customers.Count(x => x.Live && x.Installed && x.Satisfaction >= 60);
        int chance = Interest.GetValueOrDefault(lot.Archetype, 400) + h.Goodwill + Math.Min(250, happyNeighbours * 50);
        if (!rng.ChancePermille(Math.Min(950, chance)))
        {
            h.Status = HouseholdStatus.Declined;
            h.RetryDay = s.Day + 14;
            s.Emit(EventKind.Info, $"{lot.Resident}: {Rejection.GetValueOrDefault(lot.Archetype, "\"Not today.\"")}", lotId);
            return KnockResult.NotInterested;
        }
        SignUp(s, lot);
        return KnockResult.SignedUp;
    }

    public static Customer SignUp(GameState s, LotDef lot)
    {
        HouseholdOf(s, lot.Id).Status = HouseholdStatus.Signed;
        var cust = new Customer
        {
            Id = s.NewId("cust"), Name = lot.Resident, Archetype = lot.Archetype, MonthlyFee = lot.Fee, JoinedDay = s.Day,
            Installed = false, SocketFitted = false, LotId = lot.Id,
        };
        s.Customers.Add(cust);
        s.Emit(EventKind.Info, $"{lot.Resident} signs up for a line at ${lot.Fee}/month. Next: do the install at their house.", lot.Id);
        return cust;
    }

    /// <summary>Lots that could still become customers by ordinary means.</summary>
    public static IEnumerable<LotDef> Prospects(GameState s, ContentDb c) =>
        c.Lots.Values.Where(l => l.Kind is LotKind.House or LotKind.Shop && l.Premise is null && HouseholdOf(s, l.Id).Status != HouseholdStatus.Signed).OrderBy(l => l.Id);

    // ---- installing ------------------------------------------------------------------------------
    public static bool BuyModem(GameState s)
    {
        if (s.Cash < ModemPrice) return false;
        s.Cash -= ModemPrice; s.Modems++;
        s.Emit(EventKind.Money, $"Bought a modem for ${ModemPrice}. In stock: {s.Modems}.");
        return true;
    }

    public static bool FitSocket(GameState s, string customerId)
    {
        var cust = s.Customers.FirstOrDefault(x => x.Id == customerId);
        if (cust is null || cust.SocketFitted || s.Modems <= 0) return false;
        s.Modems--; cust.SocketFitted = true;
        s.Installs.Remove(cust.Id);
        s.Emit(EventKind.Work, $"Socket and modem fitted at {cust.Name}'s. Now wire their pair at the frame.", cust.Id);
        return true;
    }

    // ---- fault repair (manual: drive out and fix it with your hands) ----------------------------
    public static Fault? FaultAt(GameState s, string lotId) =>
        CustomerAt(s, lotId) is { } c ? s.Faults.FirstOrDefault(f => f.CustomerId == c.Id) : null;

    /// <summary>Fix a surfaced fault at the customer's house. Needs the toolbox with you.</summary>
    public static bool Repair(GameState s, string faultId, Tools onSite)
    {
        var fault = s.Faults.FirstOrDefault(f => f.Id == faultId);
        if (fault is null || (onSite & RepairNeeds) != RepairNeeds) return false;
        s.Faults.Remove(fault);
        s.FaultsFixed++;
        if (s.Customers.FirstOrDefault(c => c.Id == fault.CustomerId) is { } cust) cust.Satisfaction = Math.Min(100, cust.Satisfaction + 10);
        return true;
    }

    // ---- odd jobs --------------------------------------------------------------------------------
    public static OddJob? OfferJob(GameState s, ContentDb c, DetRng rng)
    {
        var free = c.Lots.Values.Where(l => l.Kind != LotKind.Depot && JobAt(s, l.Id) is null && (l.Kind != LotKind.Farm || Onboarding.RoadOpen(s, c))).OrderBy(l => l.Id).ToList();
        if (free.Count == 0) return null;
        var lot = free[rng.Next(free.Count)];
        var kinds = JobKinds.Where(k => k.Only is null || k.Only == lot.Kind).ToList();
        var kind = kinds[rng.Next(kinds.Count)];
        long pay = kind.Pay + rng.Next(9);
        if (lot.Kind == LotKind.Farm) pay = pay * 2; // worth the drive
        var job = new OddJob { Id = s.NewId("job"), LotId = lot.Id, Title = kind.Title, Pay = pay, Hours = kind.Hours, ExpiresDay = s.Day + 4 };
        s.Jobs.Add(job);
        return job;
    }

    /// <param name="bodged">It was thumped, not fixed. They pay now and think less of you later.</param>
    public static OddJob? CompleteJob(GameState s, ContentDb c, string jobId, bool bodged = false)
    {
        var job = s.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (job is null) return null;
        s.Jobs.Remove(job);
        s.Cash += job.Pay; s.JobsDone++;
        var home = HouseholdOf(s, job.LotId);
        home.Goodwill = bodged ? Math.Max(-300, home.Goodwill - 150) : Math.Min(300, home.Goodwill + 150);
        string who = c.Lots.TryGetValue(job.LotId, out var lot) ? lot.Resident : "the customer";
        s.Emit(EventKind.Money, bodged ? $"{job.Title}: \"done\". {who} pays ${job.Pay}. It rattles." : $"{job.Title}: done. {who} pays ${job.Pay} and puts the kettle on.", job.LotId);
        return job;
    }

    public static void Tick(GameState s, ContentDb c, DetRng rng)
    {
        foreach (var j in s.Jobs.Where(j => j.ExpiresDay < s.Day).ToList())
        {
            s.Jobs.Remove(j);
            if (c.Lots.TryGetValue(j.LotId, out var lot)) s.Emit(EventKind.Info, $"{lot.Resident} got someone else in for \"{j.Title}\".", j.LotId);
        }
        if (c.Lots.Count > 0 && s.Jobs.Count < Onboarding.JobLimit(s, c) && rng.ChancePermille(600)) OfferJob(s, c, rng);
    }
}
