using Telo.Sim.Town;

namespace Telo.Sim.Minigames.OddJob;

/// <summary>One stage of a small indoor job: a few actions, some minutes each, and the kit it needs to hand.</summary>
public sealed record OddStepDef(string Verb, int Clicks, int Minutes, Tools Needs);

/// <summary>
/// A SMALL JOB done by hand in someone's front room: an odd job (open it up, trace the fault, fix it, test it) or a
/// catalogue order with its own steps. Chaos: give it a thump and call it done. No clock, no fail state: it only costs the afternoon.
/// </summary>
public sealed class OddJobTask
{
    public const int BodgeMinutes = 2;

    /// <summary>The steps of a repair when nothing more specific is known.</summary>
    public static readonly IReadOnlyList<OddStepDef> Repair = new[]
    {
        new OddStepDef("Open it up", 1, 4, Tools.Toolbox),
        new OddStepDef("Trace the fault", 2, 5, Tools.None),
        new OddStepDef("Fix it", 2, 6, Tools.Toolbox),
        new OddStepDef("Put it back together and test it", 1, 4, Tools.None),
    };

    public string JobId { get; }
    public string Title { get; }
    public IReadOnlyList<OddStepDef> Steps { get; }
    private readonly int _scale;          // longer jobs take longer per action
    private readonly int[] _progress;
    public int ElapsedMinutes { get; private set; }
    public bool Bodged { get; private set; }

    public OddJobTask(string jobId, string title, int hours, IReadOnlyList<OddStepDef>? steps = null)
    {
        JobId = jobId; Title = title; _scale = Math.Max(1, hours);
        Steps = steps is { Count: > 0 } ? steps : Repair;
        _progress = new int[Steps.Count];
    }

    private int NextIndex => Bodged ? -1 : Enumerable.Range(0, Steps.Count).Where(i => _progress[i] < Steps[i].Clicks).DefaultIfEmpty(-1).First();
    public OddStepDef? Next => NextIndex >= 0 ? Steps[NextIndex] : null;
    /// <summary>Actions already done on the current stage.</summary>
    public int NextProgress => NextIndex >= 0 ? _progress[NextIndex] : 0;
    /// <summary>Ready to hand back.</summary>
    public bool Done => NextIndex < 0;
    public int MinutesFor(OddStepDef d) => d.Minutes * _scale;
    public Tools Missing(Tools onSite) => Next is { } d ? d.Needs & ~onSite : Tools.None;

    /// <summary>One careful action on the current stage.</summary>
    public bool Do(Tools onSite = Tools.All)
    {
        int i = NextIndex;
        if (i < 0 || Missing(onSite) != Tools.None) return false;
        _progress[i]++;
        ElapsedMinutes += MinutesFor(Steps[i]);
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
