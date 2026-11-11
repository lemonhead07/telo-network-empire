using Godot;
using Telo.Sim.Minigames.Frame;
using Telo.Sim.World;

namespace Telo.Game.UI;

/// <summary>
/// THE FRAME — greybox UI over the pure FramePuzzle. Target: replace with the physical frame on the wall
/// (diegetic), keeping this as the accessibility / no-timer fallback.
/// Click a line, then click a port to jumper it.
/// </summary>
public partial class FrameView : ModalView
{
    public Simulation Sim { get; set; } = null!;
    public FramePuzzle? Job { get; private set; }
    private int? _selected;
    public int? SelectedLine => _selected;
    /// <summary>The on-screen button for the first customer line (used by the automated click check).</summary>
    public Button? FirstLineButton { get; private set; }
    private FrameOutcome? _result;

    public void Start(FramePuzzle job) { Job = job; _selected = null; _result = null; }

    public override void Rebuild()
    {
        Clear(Body); Clear(Footer);
        if (Job is null || (!IsOpen && _result is null))
        {
            Title.Text = "THE FRAME";
            int ready = Sim.State.ReadyForFrame;
            Text(Body, ready > 0 ? $"{ready} new line(s) waiting to be jumpered." : "No work order on the board.", 26);
            Text(Body, "\nEvery customer's pair of wires ends here. Nothing works until its jumper is in.", 20);
            foreach (var c in Sim.State.Customers.Where(c => c.Live && c.Installed).Take(8)) Text(Body, $"   ● {c.Name}", 20);
            return;
        }
        Title.Text = $"THE FRAME — {Job.Lines.Count} lines   ⏱ {Job.ElapsedSeconds}s / {FramePuzzle.SoftClockSeconds}s{(Job.OverClock ? "  (running late — customers notice)" : "")}";

        if (_result is { } r) { ShowResult(r); return; }

        Text(Body, "Jumper each customer pair to its exchange port. Unreadable label? Tone-probe it. Test a port before trusting it; reroute bad pairs to a spare. The party line is shared with next door.", 15);

        var grid = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("separation", 40);
        Body.AddChild(grid);

        var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(lines);
        Text(lines, "CUSTOMER PAIRS", 16);
        foreach (var l in Job.Lines)
        {
            var target = Job.VisibleTarget(l.Index);
            var jumper = Job.JumperOf(l.Index);
            string label = target is { } t ? $"→ P{t + 1}" : "→ ▒▒ (label smudged)";
            string party = l.IsPartyLine ? "  [PARTY LINE — shared, already live]" : "";
            string wired = jumper is { } j ? $"   jumpered to {PortName(j)}{(Job.IsHotWired(l.Index) ? " (hot-wired)" : "")}" : "";
            var b = Btn(lines, $"{(_selected == l.Index ? "▶ " : "  ")}L{l.Index + 1}  {l.CustomerName}  {label}{party}{wired}", () => { _selected = l.Index; Rebuild(); });
            b.Alignment = HorizontalAlignment.Left;
            if (l.Index == 0) FirstLineButton = b;
        }

        var ports = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(ports);
        Text(ports, "EXCHANGE PORTS", 16);
        for (int p = 0; p < Job.PortCount; p++)
        {
            int port = p;
            var row = new HBoxContainer();
            ports.AddChild(row);
            string health = Job.KnownFaulty(port) switch { true => "  ✗ FAULTY", false => "  ✓ tested", _ => "" };
            Btn(row, $"{PortName(port)}{health}", () => { if (_selected is { } sel) { Job.Jumper(sel, port); _selected = null; Rebuild(); } }, _selected is not null)
                .SizeFlagsHorizontal = SizeFlags.ExpandFill;
            Btn(row, "Test (4s)", () => { Job.TestPort(port); Rebuild(); }, Job.KnownFaulty(port) is null);
        }

        if (_selected is { } s)
        {
            Btn(Footer, "Tone-probe (8s)", () => { Job.Probe(s); Rebuild(); }, Job.VisibleTarget(s) is null);
            Btn(Footer, "Pull jumper", () => { Job.Remove(s); Rebuild(); }, Job.JumperOf(s) is not null);
            Btn(Footer, "Hot-wire a dirty pair (1s) ⚠", () => { Job.HotWire(s); _selected = null; Rebuild(); });
        }
        Btn(Footer, "Finish & test lines", () => { _result = Job.Evaluate(); Sim.ApplyFrameOutcome(_result); Rebuild(); });
        CloseButton();
    }

    private string PortName(int p) => Job!.IsSpare(p) ? $"SPARE {p - Job.Lines.Count + 1}" : $"P{p + 1}";

    private void ShowResult(FrameOutcome r)
    {
        Text(Body, r.Perfect ? "Every line live. Dial tone, modem shriek, silence." : "Lines tested.", 22);
        Text(Body, $"Live: {r.Live}   Dead: {r.Dead}   Crossed: {r.Crossed}   Time: {r.ElapsedSeconds}s (over by {r.OvertimeSeconds}s)");
        if (r.LatentDefects > 0) Text(Body, $"{r.LatentDefects} hot-wired pair(s) are working… for now.");
        if (r.PartyLineCut) Text(Body, "Next door's phone is dead. They know who did it.");
        Btn(Footer, "Done", Close);
    }
}
