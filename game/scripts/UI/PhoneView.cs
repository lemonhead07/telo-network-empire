using Godot;
using Telo.Sim.Almanac;
using Telo.Sim.Phone;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Game.UI;

/// <summary>
/// PIP in your hand (spine step 4): the clock, the money, today's texts and the "next" line, on a phone held at the edge of view.
/// It replaces the status strip and the speech bubbles. It is for reading only: nothing on it is pressed.
/// </summary>
public partial class PhoneView : ModalView
{
    public Simulation Sim { get; set; } = null!;
    private string _next = "", _where = "";
    private float _hour;
    private readonly Queue<string> _live = new();
    private string? _saying;
    private float _sayLeft;

    /// <summary>PIP says something now (a tip, a pager beep). Shown on the phone for its reading time.</summary>
    public void Say(string text) => _live.Enqueue(text);

    public void Tick(float delta, float hour, string next, string where)
    {
        _hour = hour; _next = next; _where = where;
        _sayLeft -= delta;
        if (_sayLeft <= 0) { _saying = _live.Count > 0 ? _live.Dequeue() : null; _sayLeft = _saying is null ? 0 : Mathf.Clamp(_saying.Length * 0.07f, 4.5f, 9f); }
    }

    public override void Rebuild()
    {
        Clear(Body); Clear(Footer);
        var s = Sim.State; var c = Sim.Content;
        Title.Text = $"PIP  {(int)_hour:00}:{(int)(_hour * 60) % 60 / 10 * 10:00}  {s.Date}";
        Title.AddThemeFontSizeOverride("font_size", 62);
        var step = Onboarding.Current(s, c);
        string counters = $"${s.Cash}"
            + (step >= OnboardingStep.OddJob ? $"   {s.LiveSubscribers} on the network" : "")
            + (step >= OnboardingStep.Install ? $"   {s.PendingInstalls} waiting   {s.Modems} modem{(s.Modems == 1 ? "" : "s")}" : "")
            + (s.Faults.Count > 0 ? $"   {s.Faults.Count} fault{(s.Faults.Count == 1 ? "" : "s")}" : "");
        Text(Body, counters, 54);
        if (_next.Length > 0) Text(Body, _next, 54);
        // what PIP is saying right now, else today's text
        if (_saying is not null) Text(Body, $"» {_saying}", 46);
        else if (PhoneRules.Recent(s, 1).FirstOrDefault() is { } t) Text(Body, $"{(t.Out ? "you" : t.From)}: {t.Body}", 46);
        if (c.Plot.Count > 0) Text(Body, $"Ch {Plot.Index(s, c) + 1}: {Plot.Current(s, c).Title}", 40);
    }
}
