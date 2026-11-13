using Telo.Sim.Content;
using Telo.Sim.Net;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>
/// "What should I do next?" — derived from state every time, never scripted, so it can't fall out of step with the rules.
/// Returns the text and (when there is one) the lot the player should go to.
/// </summary>
public static class Guidance
{
    public const string QuietText = "Quiet day. Sleep until something needs you, or read PIP's Almanac.";
    /// <summary>Nothing needs the player right now.</summary>
    public static bool IsQuiet(GameState s, ContentDb c) => Next(s, c).Text == QuietText;

    public static (string Text, string? LotId) Next(GameState s, ContentDb c)
    {
        // The first hour: one new thing at a time, each explained once in the line the player is already reading.
        switch (Onboarding.Current(s, c))
        {
            case OnboardingStep.Enquiry when s.MessagesWaiting > 0:
                return ("Play the answering machine in your room.", null);
            case OnboardingStep.Install when s.Modems > 0 && s.Customers.FirstOrDefault(x => x.Live && !x.SocketFitted) is { } firstCust:
                return ($"Install the line at {firstCust.Name}'s house.", firstCust.LotId);
            case OnboardingStep.Frame:
                return ("Wire the new line at the frame in your room.", null);
            case OnboardingStep.OddJob:
                return BestJob(s, c) is { } firstJob
                    ? ($"Odd job: {firstJob.Title.ToLowerInvariant()} at {Who(c, firstJob)}'s (${firstJob.Pay}).", firstJob.LotId)
                    : ("Sleep until morning: someone will ring with an odd job.", null);
            case OnboardingStep.Knock when TownRules.Prospects(s, c).FirstOrDefault(l => s.Day >= TownRules.HouseholdOf(s, l.Id).RetryDay) is { } firstDoor:
                return ($"Knock on a door to find a customer. Try {firstDoor.Resident}'s.", firstDoor.Id);
        }

        if (s.Faults.FirstOrDefault() is { } fault && s.Customers.FirstOrDefault(x => x.Id == fault.CustomerId) is { } faulty)
            return ($"Fault on {faulty.Name}'s line. Drive out and fix it (take the toolbox).", faulty.LotId);

        // someone is waiting for something they ordered
        if (s.Orders.FirstOrDefault() is { } order && s.Customers.FirstOrDefault(x => x.Id == order.CustomerId) is { } buyer && c.Products.TryGetValue(order.ProductId, out var wanted))
            return ($"Order: fit {wanted.Name.ToLowerInvariant()} at {buyer.Name}'s (${wanted.Price}).", buyer.LotId);

        // the sysop's terminal: callers to let in, uploads to look through, tonight's mail
        if (BoardService.TerminalOpen(s) && !BoardService.Delegated(s, c))
        {
            if (BoardService.Waiting(s) is { Count: > 0 } waiting) return ($"Let {waiting.Count} new caller(s) in at the Board's terminal.", null);
            if (BoardService.MailOpen(s) && !BoardService.MailRunToday(s)) return ("Run tonight's mail call at the Board's terminal.", null);
            if (BoardService.NewFiles(s).Count >= 3) return ($"The Board has {BoardService.NewFiles(s).Count} new uploads to look through.", null);
        }

        // the Board is full of an evening: more lines in
        if (s.LineCards > 0) return ("Fit the line card beside the frame in your room.", null);
        if (s.LastEvening is { GaveUp.Count: >= 2 } eve)
        {
            if (BoardRules.CanAddLine(s))
                return s.Cash >= BoardRules.CardPrice
                    ? ($"The Board was engaged last night ({eve.GaveUp.Count} gave up). Buy a line card at Sol's (${BoardRules.CardPrice}).", c.Lots.Values.FirstOrDefault(l => l.Kind == LotKind.Shop)?.Id)
                    : ($"The Board was engaged last night. A line card is ${BoardRules.CardPrice}; you have ${s.Cash}.", BestJob(s, c)?.LotId);
            if (s.Entries.GetValueOrDefault(BoardRules.HuntGroupEntry) == EntryStatus.Readable)
                return ("The Board can't take a fifth line. PIP's Almanac has a page on hunt groups.", null);
        }

        bool tech = c.Work.ContainsKey("install") && WorkRules.Current(s, c, "install").Mode != WorkMode.Manual;
        if (!tech && c.Work.ContainsKey("install") && WorkRules.CanUpgrade(s, c, "install") == UpgradeResult.Ok)
            return ($"You can afford a technician (${WorkRules.Next(s, c, "install")!.Cost}). Hire them in the company notebook in your room.", null);
        // paper and people: a technician only works on tickets they have been handed (until there is a depot)
        if (!s.HasDepot && Clock.IsWorkingDay(s.Day) && Clock.Hour(s) < Clock.WorkEndsHour
            && s.StaffMembers.FirstOrDefault(m => StaffRules.Tickets(s, c).Any(t => StaffRules.CanTake(s, c, m, t))) is { } idle)
            return ($"Take a job ticket from the rail in your room and hand it to {idle.Name.Split(' ')[0]}.", null);
        if (tech && s.PendingInstalls > 0 && s.Modems == 0)
            return s.Cash >= TownRules.ModemPrice
                ? ("Your technician is out of modems. Buy some at the electronics shop.", c.Lots.Values.FirstOrDefault(l => l.Kind == LotKind.Shop)?.Id)
                : ("Your technician is out of modems and you can't afford one. Earn some cash.", BestJob(s, c)?.LotId);

        var needSocket = tech ? null : s.Customers.FirstOrDefault(x => x.Live && !x.Installed && !x.SocketFitted);
        if (needSocket is not null)
            return s.Modems > 0
                ? ($"Install the line at {needSocket.Name}'s house.", needSocket.LotId)
                : s.Cash >= TownRules.ModemPrice
                    ? ($"Buy a modem at the electronics shop (${TownRules.ModemPrice}).", c.Lots.Values.FirstOrDefault(l => l.Kind == LotKind.Shop)?.Id)
                    : BestJob(s, c) is { } pj ? ($"You can't afford a modem. Earn cash: {pj.Title.ToLowerInvariant()} at {Who(c, pj)}'s (${pj.Pay}).", pj.LotId)
                    : ("You can't afford a modem. Sleep and check for odd jobs tomorrow.", null);

        if (!tech && s.ReadyForFrame > 0) return ($"Wire {s.ReadyForFrame} new line(s) at the frame in your room.", null);
        if (s.MessagesWaiting > 0) return ($"Play the answering machine in your room ({s.MessagesWaiting} waiting).", null);
        if (BestJob(s, c) is { } job && (s.Cash < 150 || !TownRules.Prospects(s, c).Any(l => s.Day >= TownRules.HouseholdOf(s, l.Id).RetryDay)))
            return ($"Odd job: {job.Title.ToLowerInvariant()} at {Who(c, job)}'s (${job.Pay}).", job.LotId);
        // hands free: time to build something
        if (tech && c.Premises.Values.FirstOrDefault(p => p.PoleLine is not null && p.Act <= s.ActIndex + 1 && Ledger.LedgerRules.StatusOf(s, p.Id) != PromiseStatus.Served) is { } far)
        {
            var line = Build.LineRules.ProgressOf(s, far.Id);
            return (line.Strung < line.Planted - 1
                ? $"Pole line to {far.Name}: string the next span (cable drum and ladder)."
                : $"Pole line to {far.Name}: plant pole {line.Planted + 1} along the farm road (poles are on the van, ${far.PoleLine!.PoleCost} each).", null);
        }
        // this chapter's work is done and the world hasn't caught up: remember the next one early
        if (ActRules.EarlyStartEntry(s, c) is { } page && s.Archive >= page.ArchiveCost)
            return ($"Get ahead: recall \"{page.Title}\" in PIP's Almanac ({page.ArchiveCost} Archive).", null);
        var prospect = TownRules.Prospects(s, c).FirstOrDefault(l => s.Day >= TownRules.HouseholdOf(s, l.Id).RetryDay);
        if (prospect is not null) return ($"Knock on doors to find customers. Try {prospect.Resident}'s.", prospect.Id);
        if (BestJob(s, c) is { } any) return ($"Odd job: {any.Title.ToLowerInvariant()} at {Who(c, any)}'s (${any.Pay}).", any.LotId);
        return (QuietText, null);
    }

    private static OddJob? BestJob(GameState s, ContentDb c) => s.Jobs.OrderByDescending(j => j.Pay).FirstOrDefault();
    private static string Who(ContentDb c, OddJob j) => c.Lots.TryGetValue(j.LotId, out var l) ? l.Resident : "a neighbour";
}
