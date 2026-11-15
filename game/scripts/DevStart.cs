using Godot;
using Telo.Sim.Town;

namespace Telo.Game;

/// <summary>
/// Developer shortcuts for playtesting new work without replaying the opening (the god keys, from the command line).
///   godot --path game -- --from=free        the first hour done (everyone keeps hours, everything is open)
///   godot --path game -- --from=hire        ...and the first technician hired
///   godot --path game -- --from=poles       ...up to the pole line to the farm, ready to build
///   godot --path game -- --from=act2        straight into an act (act1 ... act7)
/// Works for playing by hand and with --autoplay. Nothing here is a rule: it only presses F4 / F6 for you.
/// </summary>
public partial class GameRoot
{
    private void StartFrom(string from)
    {
        var s = Sim.State;
        string Line() => Guidance.Next(s, _content).Text;
        int guard = 0;
        bool Step() { Sim.GodNextStage(_planet); return ++guard < 60; }

        switch (from)
        {
            case "free":
                while (Onboarding.Current(s, _content) != OnboardingStep.Free && Step()) { }
                break;
            case "hire":
                while (s.Staff == 0 && Step()) { }
                break;
            case "poles":
                while (!Line().Contains("Pole line") && Step()) { s.Faults.Clear(); s.Orders.Clear(); s.Jobs.Clear(); }
                break;
            default:
                if (from.StartsWith("act") && int.TryParse(from[3..], out int act))
                {
                    while (Onboarding.Current(s, _content) != OnboardingStep.Free && Step()) { }
                    while (s.ActIndex < act && s.ActIndex + 1 < _content.Acts.Count) Sim.GodNextAct();
                }
                else GD.PrintErr($"--from={from}: use free, hire, poles or act1..act{_content.Acts.Count - 1}");
                break;
        }
        // a working start: cash in hand, morning, rested, in the room with the van outside
        s.Cash = Math.Max(s.Cash, 2000);
        s.Modems = Math.Max(s.Modems, 3);
        s.Minute = 60; s.SleepDebt = 0;
        RefreshViews();
        GD.Print($"FROM {from}: {s.Date}, act {s.ActIndex}, {s.LiveSubscribers} subscribers. Next: {Line()}");
    }
}
