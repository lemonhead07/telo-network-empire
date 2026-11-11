using Telo.Sim.Content;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>
/// "What should I do next?" — derived from state every time, never scripted, so it can't fall out of step with the rules.
/// Returns the text and (when there is one) the lot the player should go to.
/// </summary>
public static class Guidance
{
    public static (string Text, string? LotId) Next(GameState s, ContentDb c)
    {
        var needSocket = s.Customers.FirstOrDefault(x => x.Live && !x.Installed && !x.SocketFitted);
        if (needSocket is not null)
            return s.Modems > 0
                ? ($"Fit the wall socket at {needSocket.Name}'s house.", needSocket.LotId)
                : s.Cash >= TownRules.ModemPrice
                    ? ($"Buy a modem at the electronics shop (${TownRules.ModemPrice}).", c.Lots.Values.FirstOrDefault(l => l.Kind == LotKind.Shop)?.Id)
                    : BestJob(s, c) is { } pj ? ($"You can't afford a modem. Earn cash: {pj.Title.ToLowerInvariant()} at {Who(c, pj)}'s (${pj.Pay}).", pj.LotId)
                    : ("You can't afford a modem. Sleep and check for odd jobs tomorrow.", null);

        if (s.ReadyForFrame > 0) return ($"Wire {s.ReadyForFrame} new line(s) at the frame in your room.", null);
        if (s.MessagesWaiting > 0) return ($"Play the answering machine in your room ({s.MessagesWaiting} waiting).", null);
        if (BestJob(s, c) is { } job && (s.Cash < 150 || !TownRules.Prospects(s, c).Any(l => s.Day >= TownRules.HouseholdOf(s, l.Id).RetryDay)))
            return ($"Odd job: {job.Title.ToLowerInvariant()} at {Who(c, job)}'s (${job.Pay}).", job.LotId);
        var prospect = TownRules.Prospects(s, c).FirstOrDefault(l => s.Day >= TownRules.HouseholdOf(s, l.Id).RetryDay);
        if (prospect is not null) return ($"Knock on doors to find customers. Try {prospect.Resident}'s.", prospect.Id);
        if (BestJob(s, c) is { } any) return ($"Odd job: {any.Title.ToLowerInvariant()} at {Who(c, any)}'s (${any.Pay}).", any.LotId);
        return ("Quiet day. Sleep until morning, or read PIP's Almanac.", null);
    }

    private static OddJob? BestJob(GameState s, ContentDb c) => s.Jobs.OrderByDescending(j => j.Pay).FirstOrDefault();
    private static string Who(ContentDb c, OddJob j) => c.Lots.TryGetValue(j.LotId, out var l) ? l.Resident : "a neighbour";
}
