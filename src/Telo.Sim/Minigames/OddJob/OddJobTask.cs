using Telo.Sim.Town;

namespace Telo.Sim.Minigames.OddJob;

public enum OddStep { Open, Trace, Fix, Test }

public sealed record OddStepDef(OddStep Step, string Verb, int Clicks, int Minutes, Tools Needs);

/// <summary>
/// AN ODD JOB, done by hand in a neighbour's front room: open the thing up, trace the fault, fix it, test it, hand it back.
/// Chaos: give it a thump and call it fixed. They pay today; they remember tomorrow (goodwill lost, quietly).
/// No clock and no fail state: it only costs the afternoon.
/// </summary>
public sealed class OddJobTask
{
    public const int BodgeMinutes = 2;

    public static readonly IReadOnlyList<OddStepDef> Steps = new[]
    {
        new OddStepDef(OddStep.Open, "Open it up", 1, 4, Tools.Toolbox),
        new OddStepDef(OddStep.Trace, "Trace the fault", 2, 5, Tools.None),
        new OddStepDef(OddStep.Fix, "Fix it", 2, 6, Tools.Toolbox),
        new OddStepDef(OddStep.Test, "Put it back together and test it", 1, 4, Tools.None),
    };

    public string JobId { get; }
    public string Title { get; }
    private readonly int _scale;          // longer jobs take longer per action
    private readonly int[] _progress = new int[Steps.Count];
    public int ElapsedMinutes { get; private set; }
    public bool Bodged { get; private set; }

    public OddJobTask(string jobId, string title, int hours) { JobId = jobId; Title = title; _scale = Math.Max(1, hours); }

    public OddStepDef? Next => Bodged ? null : Steps.FirstOrDefault(d => _progress[(int)d.Step] < d.Clicks);
    public int Progress(OddStep step) => _progress[(int)step];
    /// <summary>Ready to hand back.</summary>
    public bool Done => Next is null;
    public int MinutesFor(OddStepDef d) => d.Minutes * _scale;
    public Tools Missing(Tools onSite) => Next is { } d ? d.Needs & ~onSite : Tools.None;

    /// <summary>One careful action on the current stage.</summary>
    public bool Do(Tools onSite = Tools.All)
    {
        if (Next is not { } d || Missing(onSite) != Tools.None) return false;
        _progress[(int)d.Step]++;
        ElapsedMinutes += MinutesFor(d);
        return true;
    }

    /// <summary>Chaos option: a firm thump, a confident nod.</summary>
    public bool Bodge()
    {
        if (Done) return false;
        Bodged = true;
        ElapsedMinutes += BodgeMinutes;
        return true;
    }
}
