using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.World;

namespace Telo.Sim.Ledger;

/// <summary>The Last Mile is people, not a score. The ledger remembers who you promised and whether you came.</summary>
public static class LedgerRules
{
    public const long LifelineFee = 5;

    public static PromiseStatus StatusOf(GameState s, string premiseId) =>
        s.Ledger.TryGetValue(premiseId, out var p) ? p.Status : PromiseStatus.Unserved;

    public static bool Promise(GameState s, ContentDb c, string premiseId, int dueDay)
    {
        if (!c.Premises.ContainsKey(premiseId) || dueDay <= s.Day) return false;
        if (StatusOf(s, premiseId) is PromiseStatus.Served) return false;
        s.Ledger[premiseId] = new PromiseRecord { Status = PromiseStatus.Promised, DueDay = dueDay };
        s.Emit(EventKind.Ledger, $"You promised {c.Premises[premiseId].Name} a line by {new GameDate(dueDay)}.", premiseId);
        return true;
    }

    /// <param name="alreadyBuilt">The line was built by hand (see Build/LineRules), so there is no fee to pay.</param>
    public static bool Serve(GameState s, ContentDb c, string premiseId, bool alreadyBuilt = false)
    {
        if (!c.Premises.TryGetValue(premiseId, out var p) || StatusOf(s, premiseId) == PromiseStatus.Served) return false;
        if (!alreadyBuilt && (s.Cash < p.CostToServe || p.PoleLine is not null)) return false; // a pole line can't be bought, only built
        bool late = StatusOf(s, premiseId) == PromiseStatus.Broken;
        if (!alreadyBuilt) s.Cash -= p.CostToServe;
        s.Ledger[premiseId] = new PromiseRecord { Status = PromiseStatus.Served, DueDay = s.Day };
        s.Customers.Add(new Customer { Id = s.NewId("cust"), Name = p.Name, Archetype = "lastmile", MonthlyFee = LifelineFee, JoinedDay = s.Day, PremiseId = p.Id });
        s.Emit(EventKind.Ledger, late ? $"{p.Name} is connected. Late, but connected." : $"{p.Name} is connected.", premiseId);
        return true;
    }

    public static void Tick(GameState s, ContentDb c)
    {
        foreach (var (id, rec) in s.Ledger)
            if (rec.Status == PromiseStatus.Promised && s.Day > rec.DueDay)
            {
                rec.Status = PromiseStatus.Broken;
                s.Emit(EventKind.Ledger, $"The date you gave {c.Premises[id].Name} has passed. The ledger remembers.", id);
            }
    }
}
