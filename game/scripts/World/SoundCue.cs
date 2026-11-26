using Godot;
using Telo.Game.Audio;

namespace Telo.Game.World;

/// <summary>
/// Small helpers for wiring sounds to what happens in the world (EA7). The sounds themselves are in Audio/; this only says
/// which one goes with which action. Nothing here changes what happens.
/// </summary>
public static class SoundCue
{
    private static ulong _lastCoins;

    /// <summary>Being paid: coins into your hand, at most once a second however many payments land at once.</summary>
    public static void Coins(Vector3? at = null, double after = 0)
    {
        ulong now = Time.GetTicksMsec();
        if (now - _lastCoins < 1000 && _lastCoins != 0) return;
        _lastCoins = now;
        if (after > 0) Sfx.PlayAfter(after, "cash.coins", at); else Sfx.Play("cash.coins", at);
    }

    /// <summary>A looping sound (the dial tone) heard from a thing for a little while, then stopped.</summary>
    public static void PlayFor(string loopId, Node3D from, double seconds, float db = 0)
    {
        if (!from.IsInsideTree()) return;
        var loop = Sfx.Loop(loopId, from, db);
        loop.Slew = 0;   // straight in, not faded up
        from.GetTree().CreateTimer(seconds).Timeout += () => { if (GodotObject.IsInstanceValid(loop)) loop.Stop(); };
    }

    /// <summary>
    /// The sounds of a job step, from what the step says it is: the small jobs on someone's rug (odd jobs, the catalogue) and
    /// the steps of a project. Hold is heard over and over while E is held; Done when the hold completes.
    /// </summary>
    public static (string Hold, string Done) ForStep(string verb)
    {
        var v = verb.ToLowerInvariant();
        bool Has(params string[] words) => words.Any(v.Contains);
        string hold =
              Has("wait", "send") ? "work.tick"
            : Has("mains", "generator test", "throw", "commission", "bring it up", "light it up") ? "work.tick"
            : Has("carry", "lift", "ship the") ? "work.tick"
            : Has("dig", "trench", "peg out", "pour", "plant") ? "tool.dig"
            : Has("label", "mark out", "plan", "survey", "fill in", "post the", "buy the", "agree", "shake hands", "show the", "record") ? "work.tick"
            : Has("staple") ? "tool.staple"
            : Has("trace", "aim") ? "sw.probe"
            : Has("climb") ? "step.metal"
            : Has("re-jumper", "cut ") ? "sw.jumper.in"
            : Has("feed in") ? "paper.slip"
            : Has("cable", "wire", "pull", "run ", "string", "blow", "rod ", "haul", "splice", "joint", "patch", "duct", "fibre", "pair") ? "tool.cable"
            : Has("open it up", "unbox", "swap", "screw", "bolt", "fit", "mount", "fasten", "stand the", "hang", "rack", "install", "clad", "frame",
                  "put it back together", "set up", "raise", "clear out") ? "tool.screw"
            : Has("fix", "switches", "clip", "lay the") ? "tool.clip"
            : Has("test", "check") ? "sw.probe"
            : "work.tick";
        string done =
              Has("mains", "throw") ? "sw.throw"
            : Has("commission", "bring it up", "light it up", "cut ") ? "sw.relay"
            : Has("greeting", "message") ? "am.beep"
            : Has("send", "records", "time it") ? "line.modem"
            : Has("first call") ? "line.ringback"
            : Has("test", "check") ? "sw.test.ok"
            : v.StartsWith("ring ") ? "phone.ring"
            : Has("plant", "peg out") ? "tool.hammer"
            : Has("carry it in", "terminal in") ? "kit.putdown"
            : Has("carry", "lift", "ship the") ? "kit.crate"
            : Has("label", "mark out", "plan", "survey", "fill in", "post the", "buy the", "agree") ? "paper.page"
            : "work.done";
        return (hold, done);
    }
}
