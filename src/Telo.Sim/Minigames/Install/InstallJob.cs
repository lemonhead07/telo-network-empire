using Telo.Sim.Core;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Minigames.Install;

public enum InstallStep { DropWire, JunctionBox, Socket, Cable, Modem, Test }

/// <summary>One stage of a house install. A stage is several small actions ("clicks"), each costing minutes.</summary>
public sealed record InstallStepDef(
    InstallStep Step, string Title, string Verb, int Clicks, int MinutesPerClick, bool Outside, bool CanRush, Tools Needs,
    string RushDefect, string FaultDefect);

public sealed record InstallTestResult(bool DialTone, InstallStep? FaultAt);

/// <summary>
/// A twist (D31): something about this particular house that adds one action at the start of one stage, so installs stop
/// feeling identical. Bodging that stage leaves the twist's own trouble behind as well.
/// </summary>
public sealed record InstallTwist(string Id, InstallStep Stage, string Title, string Verb, int Minutes, Tools Needs, string Defect);

public sealed record InstallOutcome(
    string CustomerId, bool Complete, bool Tested, int ElapsedMinutes, int OvertimeMinutes, bool Delegated, IReadOnlyList<string> Defects)
{
    public bool Perfect => Complete && Defects.Count == 0;
}

/// <summary>
/// THE INSTALL (customer's house, Act 0–1). Bring the line from the eave to a wall socket, by hand, in order:
/// clip the drop wire down the wall, fit the junction box, mount the socket, staple the cable along the skirting,
/// plug in the modem, then listen for dial tone.
///  • chaos: bodge a stage — done in a minute, works today, latent defect weeks later
///  • some houses hide a fault (a clip that didn't bite, a damp terminal). Only the dial-tone test finds it;
///    skipping the test leaves it in the wall.
/// Soft clock: going over never fails, it only costs that customer's patience.
/// </summary>
public sealed class InstallJob
{
    public const int SoftClockMinutes = 90, RushMinutes = 1, TestMinutes = 4;

    public static readonly IReadOnlyList<InstallStepDef> Steps = new[]
    {
        new InstallStepDef(InstallStep.DropWire, "Drop wire", "Clip the drop wire", 3, 8, true, true, Tools.CableDrum,
            "a drop wire held up by one clip and optimism", "a wall clip that never bit into the brick"),
        new InstallStepDef(InstallStep.JunctionBox, "Junction box", "Fit the junction box", 2, 6, true, true, Tools.Toolbox,
            "a junction box with no lid, in the rain", "a damp terminal in the junction box"),
        new InstallStepDef(InstallStep.Socket, "Wall socket", "Screw on the wall socket", 2, 5, false, true, Tools.Toolbox,
            "a wall socket hanging by one screw", ""),
        new InstallStepDef(InstallStep.Cable, "Skirting cable", "Staple the cable to the skirting", 3, 5, false, true, Tools.CableDrum | Tools.Toolbox,
            "a cable left loose across the carpet, found by the vacuum cleaner", "a staple driven straight through the cable"),
        new InstallStepDef(InstallStep.Modem, "Modem", "Plug in the modem", 1, 3, false, false, Tools.ModemBox, "", ""),
        new InstallStepDef(InstallStep.Test, "Dial tone", "Listen for dial tone", 1, TestMinutes, false, false, Tools.None, "", ""),
    };

    private static readonly InstallStep[] FaultCandidates = { InstallStep.DropWire, InstallStep.JunctionBox, InstallStep.Cable };

    public const int TwistPermille = 333;
    public static readonly IReadOnlyList<InstallTwist> Twists = new[]
    {
        new InstallTwist("ivy", InstallStep.DropWire, "Ivy up the wall", "Cut back the ivy", 8, Tools.Toolbox, "ivy growing back through the drop wire clips"),
        new InstallTwist("dog", InstallStep.DropWire, "A dog in the garden", "Make friends with the dog", 5, Tools.None, "a dog that now goes for your van on sight"),
        new InstallTwist("old-box", InstallStep.JunctionBox, "An old NezTel box", "Take the old NezTel box off the wall", 6, Tools.Toolbox, "the old NezTel box left live behind yours"),
        new InstallTwist("wasps", InstallStep.JunctionBox, "A wasps' nest", "Move the wasps' nest from behind the junction box", 10, Tools.Toolbox, "a junction box the wasps moved back into"),
        new InstallTwist("kitchen", InstallStep.Socket, "Socket by the kettle", "Find a spot away from the kettle for the socket", 6, Tools.Toolbox, "a socket over the kettle, steaming"),
        new InstallTwist("wallpaper", InstallStep.Cable, "New wallpaper", "Ease the skirting off without marking the new wallpaper", 6, Tools.Toolbox, "a cable tacked over the new wallpaper (they noticed)"),
    };

    public string CustomerId { get; }
    public string CustomerName { get; }
    /// <summary>This house's twist, or null for a plain install.</summary>
    public InstallTwist? Twist { get; }
    private readonly int[] _progress = new int[Steps.Count];
    private readonly bool[] _rushed = new bool[Steps.Count];
    private InstallStep? _fault;
    private bool _twistSkipped;

    public int ElapsedMinutes { get; private set; }
    /// <summary>The last dial-tone test passed and nothing has been touched since.</summary>
    public bool Tested { get; private set; }
    /// <summary>A stage the test showed to be bad and that still needs redoing.</summary>
    public InstallStep? KnownFault { get; private set; }

    private InstallJob(string customerId, string customerName, InstallStep? fault, InstallTwist? twist = null)
    {
        CustomerId = customerId; CustomerName = customerName; _fault = fault; Twist = twist;
    }

    /// <summary>Same seed, same job: the same hidden fault and the same twist. <paramref name="twists"/> is off for a new player's first install.</summary>
    public static InstallJob Generate(ulong seed, string customerId, string customerName, int faultPermille = 350, bool twists = false)
    {
        var rng = new DetRng(seed);
        InstallStep? fault = rng.ChancePermille(faultPermille) ? FaultCandidates[rng.Next(FaultCandidates.Length)] : null;
        var trng = new DetRng(DetRng.Derive(seed, "twist"));   // its own stream, so twists never change which jobs have faults
        InstallTwist? twist = twists && trng.ChancePermille(TwistPermille) ? Twists[trng.Next(Twists.Count)] : null;
        return new InstallJob(customerId, customerName, fault, twist);
    }

    /// <summary>Clicks this stage takes here: one more where the twist is.</summary>
    public int Clicks(InstallStep step) => Def(step).Clicks + (Twist?.Stage == step ? 1 : 0);
    /// <summary>Is the next action on this stage the twist?</summary>
    public bool TwistNext(InstallStep step) => Twist?.Stage == step && _progress[(int)step] == 0;
    /// <summary>How many of this stage's clicks come before its ordinary work (the twist, if it's here).</summary>
    public int TwistOffset(InstallStep step) => Twist?.Stage == step ? 1 : 0;
    public string VerbFor(InstallStep step) => TwistNext(step) ? Twist!.Verb : Def(step).Verb;
    public int MinutesFor(InstallStep step) => TwistNext(step) ? Twist!.Minutes : Def(step).MinutesPerClick;

    // ---- saving a job in the middle --------------------------------------------------------------
    public InstallProgress Snapshot() => new()
    {
        Progress = (int[])_progress.Clone(), Rushed = (bool[])_rushed.Clone(), ElapsedMinutes = ElapsedMinutes, Tested = Tested,
        FaultFixed = _fault is null, KnownFault = KnownFault is { } k ? (int)k : -1,
    };

    /// <summary>Put a freshly generated job back to where a save left it. A snapshot from a different shape of job is ignored.</summary>
    public InstallJob Restore(InstallProgress p)
    {
        if (p.Progress.Length != _progress.Length || p.Rushed.Length != _rushed.Length) return this;
        p.Progress.CopyTo(_progress, 0); p.Rushed.CopyTo(_rushed, 0);
        ElapsedMinutes = p.ElapsedMinutes; Tested = p.Tested;
        if (p.FaultFixed) _fault = null;
        KnownFault = p.KnownFault >= 0 && p.KnownFault < Steps.Count ? (InstallStep)p.KnownFault : null;
        return this;
    }

    // ---- what the player can see -------------------------------------------------------------
    public static InstallStepDef Def(InstallStep step) => Steps[(int)step];
    public int Progress(InstallStep step) => _progress[(int)step];
    public bool IsDone(InstallStep step) => step == InstallStep.Test ? Tested : _progress[(int)step] >= Clicks(step);
    public bool IsRushed(InstallStep step) => _rushed[(int)step];
    public bool OverClock => ElapsedMinutes > SoftClockMinutes;
    /// <summary>Everything physical is in. The job can be signed off (tested or not).</summary>
    public bool CanFinish => Steps.All(d => d.Step == InstallStep.Test || IsDone(d.Step));

    /// <summary>The stage to work on now; null when only the (optional) test is left.</summary>
    public InstallStep? NextStep => Steps.Where(d => d.Step != InstallStep.Test && !IsDone(d.Step)).Select(d => (InstallStep?)d.Step).FirstOrDefault();

    public bool CanDo(InstallStep step) => step == InstallStep.Test ? CanFinish : NextStep == step;

    // ---- verbs --------------------------------------------------------------------------------
    /// <summary>What has to be on site for the next action on this stage. The top clip of the drop wire is up a ladder; a bodge skips the ladder.</summary>
    public Tools Needs(InstallStep step, bool rush = false) =>
        !rush && TwistNext(step) ? Twist!.Needs
        : Def(step).Needs | (step == InstallStep.DropWire && Progress(step) == TwistOffset(step) && !rush ? Tools.Ladder : Tools.None);

    public Tools Missing(InstallStep step, Tools onSite, bool rush = false) => Needs(step, rush) & ~onSite;

    /// <summary>One careful action on a stage. Returns false if it isn't this stage's turn or the kit isn't there.</summary>
    public bool Do(InstallStep step, Tools onSite = Tools.All)
    {
        if (step == InstallStep.Test) return CanFinish && Test().DialTone;
        if (!CanDo(step) || Missing(step, onSite) != Tools.None) return false;
        ElapsedMinutes += MinutesFor(step);
        _progress[(int)step]++;
        Tested = false;
        if (IsDone(step) && KnownFault == step) { KnownFault = null; _fault = null; } // redone properly
        return true;
    }

    /// <summary>A tired hand: the stage was done carefully but went in wrong all the same. It counts like a bodge, and nobody knows yet.</summary>
    public void Slip(InstallStep step)
    {
        if (step != InstallStep.Test && IsDone(step)) _rushed[(int)step] = true;
    }

    /// <summary>Chaos option: call the stage finished. Fast now; persists in the world.</summary>
    public bool Rush(InstallStep step, Tools onSite = Tools.All)
    {
        if (!CanDo(step) || !Def(step).CanRush || Missing(step, onSite, rush: true) != Tools.None) return false;
        ElapsedMinutes += RushMinutes;
        if (TwistNext(step)) _twistSkipped = true;
        _progress[(int)step] = Clicks(step);
        _rushed[(int)step] = true;
        Tested = false;
        if (KnownFault == step) { KnownFault = null; _fault = null; } // the bodge replaces the fault with its own defect
        return true;
    }

    /// <summary>Lift the handset. Finds a hidden fault (that stage then needs one more careful action). Never finds a bodge.</summary>
    public InstallTestResult Test()
    {
        if (!CanFinish) return new InstallTestResult(false, null);
        ElapsedMinutes += TestMinutes;
        if (_fault is { } f)
        {
            KnownFault = f;
            _progress[(int)f] = Clicks(f) - 1;
            _rushed[(int)f] = false;
            Tested = false;
            return new InstallTestResult(false, f);
        }
        Tested = true;
        return new InstallTestResult(true, null);
    }

    // ---- result -------------------------------------------------------------------------------
    public InstallOutcome Evaluate()
    {
        var defects = Steps.Where(d => _rushed[(int)d.Step]).Select(d => d.RushDefect).ToList();
        if (_fault is { } f) defects.Add(Def(f).FaultDefect);
        if (_twistSkipped && Twist is { } t) defects.Add(t.Defect);   // the twist was bodged past, not dealt with
        return new InstallOutcome(CustomerId, CanFinish, Tested, ElapsedMinutes, Math.Max(0, ElapsedMinutes - SoftClockMinutes), false, defects);
    }

    /// <summary>A technician does it instead (80–90% right). Mistakes look fine today and surface later.</summary>
    public InstallOutcome Delegate(ulong seed, int accuracyPermille)
    {
        var rng = new DetRng(seed);
        var defects = rng.ChancePermille(accuracyPermille) ? new List<string>() : new List<string> { "a socket the technician swore was tested" };
        return new InstallOutcome(CustomerId, true, true, 0, 0, true, defects);
    }
}
