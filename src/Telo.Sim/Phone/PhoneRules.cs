using Telo.Sim.Content;
using Telo.Sim.Town;
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
        Add(s, new TextMessage { Day = s.Day, Body = body });
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
