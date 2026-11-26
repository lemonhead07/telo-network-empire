using Telo.Sim.Build;
using Telo.Sim.Content;
using Telo.Sim.Ledger;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Phone;

/// <summary>One text on PIP's phone. Saved (v9).</summary>
public sealed class TextMessage
{
    public int Day { get; set; }
    public string From { get; set; } = "PIP";
    public string Body { get; set; } = "";
    /// <summary>Sent by the player (the night text), rather than received.</summary>
    public bool Out { get; set; }
}

/// <summary>
/// The phone is PIP (D34, N4): one text in each morning, one out each night. Sending the night text ends the day.
/// The morning text opens a new chapter when one has started; otherwise it is yesterday in one line.
/// It is for reading only: every decision is still made by moving a thing.
/// </summary>
public static class PhoneRules
{
    public const int Keep = 40;

    public static void Morning(GameState s, ContentDb c)
    {
        if (s.Texts.Any(t => t.Day == s.Day && !t.Out)) return;   // one a day
        string body;
        if (c.Plot.Count > 0 && Plot.Current(s, c) is { } ch && s.ChapterTexted != ch.Id && ch.Text.Length > 0)
        {
            body = ch.Text;
            s.ChapterTexted = ch.Id;
        }
        else body = Yesterday(s);
        if (Today(s, c) is { } today) body += " " + today;
        Add(s, new TextMessage { Day = s.Day, Body = body });
    }

    /// <summary>
    /// The day ahead, when there's more than a day in it (owner, 2026-11-26): with the driving between them, a day holds
    /// about two jobs, so a pole line or a project goes up over several days around them. Null on an ordinary day,
    /// and in the first hour (one thing at a time).
    /// </summary>
    public static string? Today(GameState s, ContentDb c)
    {
        if (!Onboarding.PhoneRings(s, c)) return null;
        // the player's own jobs: bookings, orders and faults nobody has been handed (odd jobs on offer are optional)
        int jobs = StaffRules.Tickets(s, c).Count(t => t.AssignedTo is null && t.Kind != TicketKind.OddJob);
        var building = new List<string>();
        foreach (var (id, line) in s.Lines)
            if (line.Planted > 0 && c.Premises.TryGetValue(id, out var p) && p.PoleLine is not null && LedgerRules.StatusOf(s, id) != PromiseStatus.Served)
                building.Add($"the pole line to {p.Name} ({line.Planted} pole{(line.Planted == 1 ? "" : "s")} up)");
        foreach (var (id, prog) in s.Projects)
            if (!ProjectRules.Done(s, id) && c.Project(id) is { } pd)
                building.Add($"{pd.Name} (step {Math.Min(prog.Step + 1, pd.Steps.Count)} of {pd.Steps.Count})");
        string rest = s.StaffMembers.Count > 0 ? "hand some over, or the rest keeps till tomorrow" : "the rest keeps till tomorrow";
        if (jobs >= 2 && building.Count > 0)
            return $"Today: {jobs} jobs waiting, and {string.Join(" and ", building)}. Two jobs and the driving is a full day: " +
                   $"{(building.Count == 1 ? "that goes" : "those go")} up over a few days between jobs, and nothing you've put up comes down.";
        if (jobs >= 3) return $"Today: {jobs} jobs waiting. Two and the driving is a full day: {rest}.";
        return null;
    }

    /// <summary>The night text: the player signs the day off. Ends the day (the caller saves).</summary>
    public static void SendNight(Simulation sim, string body = "Done for today.")
    {
        Add(sim.State, new TextMessage { Day = sim.State.Day, Body = body, Out = true, From = "you" });
        sim.Sleep();
    }

    public static IEnumerable<TextMessage> Recent(GameState s, int n) => s.Texts.TakeLast(n);

    private static string Yesterday(GameState s)
    {
        var bits = new List<string> { $"${s.Cash} in the tin", $"{s.LiveSubscribers} on the network" };
        if (s.LastEvening is { } eve) bits.Add(eve.Engaged > 0 ? $"{eve.Engaged} engaged tones last night" : "nobody got the engaged tone");
        if (s.Faults.Count > 0) bits.Add($"{s.Faults.Count} fault{(s.Faults.Count == 1 ? "" : "s")} waiting");
        return string.Join(". ", bits) + ".";
    }

    private static void Add(GameState s, TextMessage t)
    {
        s.Texts.Add(t);
        if (s.Texts.Count > Keep) s.Texts.RemoveRange(0, s.Texts.Count - Keep);
    }
}
