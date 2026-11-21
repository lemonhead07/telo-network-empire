using Godot;
using Telo.Game.World;
using Telo.Sim.Net;
using Telo.Sim.Town;
using Telo.Sim.Work;

namespace Telo.Game;

/// <summary>
/// Developer shortcuts for playtesting new work without replaying the opening (the god keys, from the command line).
///   godot --path game -- --from=free        the first hour done (everyone keeps hours, everything is open)
///   godot --path game -- --from=hire        ...and the first technician hired
///   godot --path game -- --from=poles       ...up to the pole line to the farm, ready to build
///   godot --path game -- --from=act2        straight into an act (act1 ... act8)
///   godot --path game -- --from=act5        The Core: on Halden Flats with the van, by the empty plot (DC1)
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
                    // the world you'd have built by then: earlier acts' projects up, their towns signed, the pages they needed known
                    foreach (var p in _content.Projects.Where(p => p.Act < act))
                    {
                        if (p.RequiresEntry is { } e) s.Entries[e] = Telo.Sim.World.EntryStatus.Recalled;
                        if (p.RequiresDepot) s.HasDepot = true;
                        Telo.Sim.Build.ProjectRules.Finish(s, _content, p);
                        if (p.Reward.Region > 0) s.RegionSigned[p.Id] = p.Reward.Region;
                    }
                    Telo.Sim.Almanac.AlmanacRules.RefreshStatuses(s, _content, quiet: true);
                    // ...and everyone in the streets and villages they opened on the line (F4: the hire, the farm poles, the rest), Tobi on the Board
                    for (int i = 0; i < 8 && TownRules.Prospects(s, _content).Any(); i++)
                        if (Sim.GodNextStage(_planet) == "The whole town is connected.") break;
                    if (_content.Work.ContainsKey(BoardService.WorkId) && !BoardService.Delegated(s, _content) && WorkRules.Next(s, _content, BoardService.WorkId) is { } coSysop)
                    {
                        s.Cash = Math.Max(s.Cash, coSysop.Cost);
                        WorkRules.Upgrade(s, _content, BoardService.WorkId);
                    }
                    s.Cash = Math.Max(s.Cash, 2000L * (act + 1) * (act + 1));
                }
                else GD.PrintErr($"--from={from}: use free, hire, poles or act1..act{_content.Acts.Count - 1}");
                break;
        }
        // a working start: cash in hand, morning, rested, in the room with the van outside
        s.Cash = Math.Max(s.Cash, 2000);
        s.Modems = Math.Max(s.Modems, 3);
        s.Minute = 60; s.SleepDebt = 0;
        // The Core begins where the data centre will stand: on Halden Flats, the van parked by the empty plot
        if (from == "act5" && _planet.SiteById("halden") is { } flats)
        {
            var park = TownBuilder.Place(_planet, flats, -40, -40, 0, 0.6f);
            _van.PlaceAt(park.Origin, -park.Basis.Z);
            var stand = TownBuilder.Place(_planet, flats, -36, -32, 0, 1.0f);
            _player.PlaceAt(stand.Origin, TownBuilder.Place(_planet, flats, -24, -18).Origin - stand.Origin);
        }
        RefreshViews();
        GD.Print($"FROM {from}: {s.Date}, act {s.ActIndex}, {s.LiveSubscribers} subscribers. Next: {Line()}");
    }
}
