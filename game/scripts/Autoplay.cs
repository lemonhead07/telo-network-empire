using Godot;
using Telo.Game.Player;
using Telo.Game.World;
using Telo.Sim.Act;
using Telo.Sim.Ledger;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Net;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Game;

/// <summary>
/// The walking player (spine step 7): the script player's brain (<see cref="Bot"/>) in the player's body.
/// The bot picks the next action from the sim; this file performs it in the world the way a person would:
/// walking, driving, carrying kit off the van, and holding E on the actual things. It decides nothing itself.
///   godot --path game -- --autoplay                    watch it play the first hour
///   godot --path game -- --autoplay=/abs/dir           ...and save photos and a log there
///   extra: --autoplay-until=farm   --autoplay-speed=3   --autoplay-minutes=30
/// </summary>
public partial class GameRoot
{
    private string? _autoDir;
    private int _shotN, _assists;
    private double _lastShot;
    private string _autoWhy = "";
    private readonly HashSet<string> _shotKinds = new();
    private readonly List<string> _autoLog = new();

    private ActorState Me => Sim.Actor("player");

    private async Task RunAutoplay(string? dir, string[] args)
    {
        _autoDir = dir;
        if (dir is not null) DirAccess.MakeDirRecursiveAbsolute(dir);
        string until = args.FirstOrDefault(a => a.StartsWith("--autoplay-until="))?.Split('=')[1] ?? "hour";
        float speed = args.FirstOrDefault(a => a.StartsWith("--autoplay-speed=")) is { } sp ? float.Parse(sp.Split('=')[1]) : 1f;
        float minutes = args.FirstOrDefault(a => a.StartsWith("--autoplay-minutes=")) is { } mn ? float.Parse(mn.Split('=')[1]) : 30f;
        Engine.TimeScale = speed;
        Me.OnSiteOverride = KitInWorkArea;
        // --autoplay-teleport (development): the van jumps to where it's going instead of driving, so the run is all work
        _teleport = args.Contains("--autoplay-teleport");
        _sample = args.FirstOrDefault(a => a.StartsWith("--autoplay-sample=")) is { } smp && int.TryParse(smp.Split('=')[1], out int x) ? Math.Max(1, x) : 1;
        await Frames(40);
        await Snap("start", force: true);

        ulong started = Time.GetTicksMsec();
        string last = ""; int same = 0;
        string Sig() => $"{Sim.State.Day}:{Sim.State.Minute / 20}:{Me.Place}:{Me.Holding}:{string.Join(",", Me.Kit)}:{Sim.State.Cash}:{string.Join(",", Sim.Installs.Values.Select(j => j.ElapsedMinutes))}:{string.Join(",", Sim.Benches.Values.Select(b => b.Task.ElapsedMinutes))}:{Sim.State.Customers.Count}:{Sim.State.Switch.Positions.Sum(p => p.Jumper + (p.TagRead ? 1 : 0))}";
        string before = "";
        // --autoplay-fast (development, with --autoplay-sample): everything but the jobs shown in full runs at the rules' speed,
        // like the script player, so a run sweeps through the acts; a job picked to show is played live in the world
        bool fast = args.Contains("--autoplay-fast") && _sample > 1;
        for (int i = 0; i < (fast ? 400000 : 4000); i++)
        {
            if (Done(until) || Time.GetTicksMsec() - started > minutes * 60000) break;
            bool live = !fast || LiveJobOpen();
            if (live) SyncActor();
            // what's in reach: the world's answer while a job is shown live, the rules' own record at rules speed
            Me.OnSiteOverride = live ? KitInWorkArea : null;
            if (Sim.State.ActIndex != _actLogged)
            {
                _actLogged = Sim.State.ActIndex;
                var act = _content.Acts[Sim.State.ActIndex];
                _autoLog.Add($"===== Act {act.Index}: {act.Name} ({Sim.State.Date}, {Sim.State.TotalSubscribers} subscribers, ${Sim.State.Cash})");
                GD.Print("AUTO ", _autoLog[^1]);
                if (fast) await LineUpWorld();
                await Snap($"act{act.Index}", force: true);
            }
            var (id, why) = Bot.Choose(Sim, Me, _planet);
            _autoWhy = why;
            _autoLog.Add($"{Sim.State.Date} {Clock.Text(Sim.State)}  {id}  — {why}");
            if (!fast || live) GD.Print("AUTO ", _autoLog[^1]);
            if (fast && !live)
            {
                if (SkipBySample(id)) continue;
                if (_liveKeys.Count > 0)
                {
                    // this one is shown in full: put the world where the rules are, then play it live
                    _autoLog.Add($"   (showing in full from {Sim.State.Date} {Clock.Text(Sim.State)})");
                    GD.Print("AUTO ", _autoLog[^1]);
                    await LineUpWorld();
                    SyncActor();
                    await Do(id);
                    await Snap(Kind(id));
                    continue;
                }
                if (!Actions.Perform(Sim, Me, _planet, id)) Actions.Perform(Sim, Me, _planet, "wait");
                if (i % 25 == 0) { RefreshViews(); SyncProjects(); await Physics(1); }
                continue;
            }
            string now = Sig();
            same = id == last && now == before ? same + 1 : 0; last = id; before = now;
            if (same >= 2 && id != "wait" && id != "sleep")
            {
                // the body tried twice and nothing changed: do it directly in the world and carry on (counted, never hidden)
                await Assist(id);
                continue;
            }
            if (SkipBySample(id)) continue;
            await Do(id);
            await Snap(Kind(id));
        }

        await Snap("end", force: true);
        bool farm = Sim.Content.Premises.Values.Where(p => p.PoleLine is not null).All(p => LedgerRules.StatusOf(Sim.State, p.Id) == PromiseStatus.Served);
        string summary = $"AUTOPLAY {(Done(until) ? "OK" : "STOPPED")}: until={until} {Sim.State.Date} step={Onboarding.Current(Sim.State, _content)} act={Sim.State.ActIndex} subs={Sim.State.TotalSubscribers} cash={Sim.State.Cash} farm={farm} photos={_shotN} assists={_assists}";
        _autoLog.Add(summary);
        GD.Print(summary);
        if (_autoDir is not null) Godot.FileAccess.Open($"{_autoDir}/autoplay.txt", Godot.FileAccess.ModeFlags.Write)?.StoreString(string.Join("\n", _autoLog));
        Engine.TimeScale = 1;
        GetTree().Quit(Done(until) ? 0 : 1);
    }

    private bool _teleport;
    private int _actLogged = -1;
    private int _sample = 1, _jobsSeen;
    private readonly HashSet<string> _played = new();
    /// <summary>The job being shown in full right now (fast runs): ticket keys, a project or pole place; and a pole line's progress when it started.</summary>
    private readonly List<string> _liveKeys = new();
    private (int, int) _livePole;

    /// <summary>Is the job being shown in full still going? Its tickets open, its project unbuilt, its pole step not yet done.</summary>
    private bool LiveJobOpen()
    {
        if (_liveKeys.Count == 0) return false;
        var open = Telo.Sim.Work.StaffRules.Tickets(Sim.State, _content).Select(t => t.Key).ToHashSet();
        bool going = _liveKeys.Any(k =>
            Places.IsProject(k, out var pj) ? !Telo.Sim.Build.ProjectRules.Done(Sim.State, pj)
            : Places.IsPole(k, out var pid, out _) ? (Telo.Sim.Build.LineRules.ProgressOf(Sim.State, pid) is var l && (l.Planted, l.Strung) == _livePole)
            : open.Contains(k) && Telo.Sim.Plan.AppointmentRules.HouseWorkLeft(Sim.State, k))
            || Sim.Installs.Count + Sim.Benches.Count > 0;
        if (!going) { _autoLog.Add("   (back to fast)"); _liveKeys.Clear(); }
        return going;
    }

    /// <summary>Fast runs: put the world where the rules say things are. Kit back on the van, the van where it's parked, you beside it.</summary>
    private async Task LineUpWorld()
    {
        if (_van.Driven) ExitVan();
        foreach (var item in _cargo.Items.Where(i => i.Where != Carryable.Place.Van).ToList()) _cargo.Stow(item);
        Me.Kit.Clear(); Me.Holding = Tools.None;
        var vanAt = ParkOf(Me.VanAt) ?? WorldOf(Me.VanAt);
        _van.PlaceAt(vanAt + vanAt.Normalized() * 0.3f, _van.Forward);
        await Physics(5);
        var me = Me.Place == Me.VanAt ? _van.GlobalPosition - _van.GlobalBasis.X * 2.4f : WorldOf(Me.Place);
        _player.PlaceAt(me + me.Normalized() * 0.4f, _player.Forward);
        RefreshViews(); SyncProjects();
        await Physics(10);
    }

    /// <summary>
    /// --autoplay-sample=X (development): play one job in X in full, in the world; finish the rest by decree the moment the
    /// bot sets off for them, so a run gets through the game quickly but still shows every kind of job now and then.
    /// </summary>
    private bool SkipBySample(string id)
    {
        if (_sample <= 1 || !id.StartsWith("go:")) return false;
        string place = id[3..];
        var keys = Telo.Sim.Work.StaffRules.Tickets(Sim.State, _content)
            .Where(t => t.LotId == place && t.AssignedTo is null && Telo.Sim.Plan.AppointmentRules.HouseWorkLeft(Sim.State, t.Key))
            .Select(t => t.Key).ToList();
        bool pole = Places.IsPole(place, out var pid, out _);
        if (pole) keys.Add(place);
        if (Places.IsProject(place, out var projId) && _content.Project(projId) is { } projDef)
        {
            // a project is played in full one time in X like any job; otherwise it's built by decree (paid for, waits skipped)
            if (_played.Contains(place)) return false;
            if (++_jobsSeen % _sample == 1) { _played.Add(place); _liveKeys.Add(place); _autoLog.Add($"   (playing this one in full: {projDef.Name})"); return false; }
            if (!Telo.Sim.Build.ProjectRules.Started(Sim.State, projId)) Sim.State.Cash -= projDef.Cost;
            Telo.Sim.Build.ProjectRules.Finish(Sim.State, _content, projDef);
            _autoLog.Add($"   (skipped by sample: {projDef.Name})");
            SyncProjects(); RefreshViews();
            return true;
        }
        if (keys.Count == 0 || keys.Any(_played.Contains)) return false;   // not a job, or one we're already playing
        if (++_jobsSeen % _sample == 1)
        {
            foreach (var k in keys) _played.Add(k);
            _liveKeys.AddRange(keys);
            if (pole) _livePole = (Telo.Sim.Build.LineRules.ProgressOf(Sim.State, pid).Planted, Telo.Sim.Build.LineRules.ProgressOf(Sim.State, pid).Strung);
            _autoLog.Add($"   (playing this one in full: {string.Join(", ", keys)})");
            return false;
        }

        foreach (var k in keys)
        {
            if (pole && _content.Premises.TryGetValue(pid, out var premise))
            {
                // the pole and the span back to the one before, by decree
                Telo.Sim.Build.LineRules.Plant(Sim.State, _planet, premise, Tools.All);
                while (Telo.Sim.Build.LineRules.CanString(Sim.State, _planet, premise) && Telo.Sim.Build.LineRules.ProgressOf(Sim.State, pid).Strung < Telo.Sim.Build.LineRules.ProgressOf(Sim.State, pid).Planted - 1)
                    Telo.Sim.Build.LineRules.String(Sim.State, _content, _planet, premise, Tools.All);
                if (Telo.Sim.Build.LineRules.ProgressOf(Sim.State, pid).Planted >= Telo.Sim.Build.LineRules.PoleSites(_planet, premise).Count)
                    while (Telo.Sim.Build.LineRules.CanString(Sim.State, _planet, premise)) Telo.Sim.Build.LineRules.String(Sim.State, _content, _planet, premise, Tools.All);
                _autoLog.Add($"   (skipped by sample: {k})");
            }
            else if (Sim.GodFinishTicket(k) is { } title) _autoLog.Add($"   (skipped by sample: {title})");
        }
        RefreshViews();
        return true;
    }

    private bool Done(string until) => until switch
    {
        // --autoplay-until=act3: play on until that act begins (the acts one after another)
        _ when until.StartsWith("act") && int.TryParse(until[3..], out int act) => Sim.State.ActIndex >= act,
        "farm" => Sim.Content.Premises.Values.Where(p => p.PoleLine is not null).All(p => LedgerRules.StatusOf(Sim.State, p.Id) == PromiseStatus.Served),
        _ => Onboarding.Current(Sim.State, _content) == OnboardingStep.Free && Sim.State.LiveSubscribers >= 2 && Sim.Installs.Count == 0 && Sim.Benches.Count == 0,
    };

    // ---- performing one action --------------------------------------------------------------------
    private async Task Do(string id)
    {
        var parts = id.Split(':');
        var lot = _town.Lots.FirstOrDefault(l => l.Lot.Id == Me.Place);
        switch (parts[0])
        {
            case "go": await GoTo(id[3..]); break;
            case "take":
                var item = _cargo.Items.First(i => i.Tool.ToString() == parts[1]);
                await Use(item);
                break;
            case "put":
                // hands are for carrying; the work area is for working. Put it down where the next step that needs it is done,
                // so it is in reach there, and the hands are free to fetch the next piece
                if (WorkArea(lot) is { } area) await WalkTo(area, 1.2f);
                _cargo.Drop();
                break;
            case "stow": await Use(_van.Door); break;
            case "messages": await Use(RoomProp("AnsweringMachine")); break;
            case "sleep":
                // evening is for planning tomorrow: fill the timesheet before bed (once the first hour is over)
                if (Onboarding.Current(Sim.State, _content) == OnboardingStep.Free && !Sim.State.Plans.Any(p => p.Day == Sim.State.Day + 1 && p.Slots.Count > 0)
                    && Clock.Hour(Sim.State) >= 15)
                {
                    await Use(_sheet.Hot);
                    await Snap("timesheet", force: true);
                }
                await Use(RoomProp("Bed"));
                break;
            case "wait": await Seconds(10); break;
            case "install" when parts[1] == "do" && lot?.Install?.SpotFor(Enum.Parse<InstallStep>(parts[2])) is { } spot: await Use(spot); break;
            case "install" when parts[1] == "bodge" && lot?.Install?.SpotFor(Enum.Parse<InstallStep>(parts[2])) is { } bspot: await Use(bspot, alt: true); break;
            case "install" when parts[1] == "test" && lot?.Install is { } site: await Use(site.Phone); break;
            case "install" when parts[1] == "sign" && lot?.Install is { } signSite: await Use(signSite.Sheet); break;
            case "install" or "oddjob" or "knock" or "repair" or "order" or "socket" or "lease" when lot is not null: await Use(lot.Door); break;
            case "buy" when lot?.Counter is { } counter: await Use(counter, alt: parts[1] == "card"); break;
            case "bench" when lot is not null && _oddJobs.TryGetValue(lot.Lot.Id, out var bench): await Use(bench.Bench, alt: parts[1] == "thump"); break;
            case "switch": await SwitchWork(parts); break;
            case "pole":
                var view = _lines.First().View;
                await Use(parts[1] == "plant" ? view.PlantSpot : view.StringSpot);
                break;
            case "card": await Use(_rack.Slot); break;
            case "project" when Places.IsProject(Me.Place, out var projId) && _projects.TryGetValue(projId, out var site): await Use(site.Spot); break;
            case "ticket":
                var ticket = StaffRules.Tickets(Sim.State, _content).FirstOrDefault(t => t.Key == $"{parts[1]}:{parts[2]}");
                if (ticket is null) break;
                await WalkTo(_rail.GlobalPosition + _rail.GlobalBasis.Z * 1.0f, 0.5f);
                _rail.Use(ticket); SyncPaper();
                await Snap("ticket");
                if (_figures.Values.FirstOrDefault() is { } fig) await Use(fig.Hot);
                break;
            case "invest": await AtDesk(RoomProp("Notebook"), () => _notebook.Open(Telo.Sim.Content.InterfaceTier.Paper), () => _notebook.Close(), id); break;
            case "recall": await AtDesk(RoomProp("Handset"), () => _almanac.Open(Telo.Sim.Content.InterfaceTier.Terminal), () => _almanac.Close(), id); break;
            case "board": await AtDesk(_terminalUse, () => _board.Open(Telo.Sim.Content.InterfaceTier.Terminal), () => _board.Close(), id); break;
            default:
                Actions.Perform(Sim, Me, _planet, id, advanceClock: false);
                RefreshViews();
                break;
        }
    }

    private async Task Assist(string id)
    {
        _assists++; _autoLog.Add($"   (assist: {id})");
        var parts = id.Split(':');
        switch (parts[0])
        {
            case "take" when _cargo.Held is null: _cargo.PickUp(_cargo.Items.First(i => i.Tool.ToString() == parts[1])); break;
            case "put": _cargo.Drop(); break;
            case "stow": _cargo.StowHeld(); break;
            case "go": _player.PlaceAt(WorldOf(id[3..]) + WorldOf(id[3..]).Normalized() * 0.4f, _player.Forward); break;
            default:
                Actions.Perform(Sim, Me, _planet, id, advanceClock: false);
                RefreshViews();
                break;
        }
        await Physics(10);
    }

    /// <summary>A screen on the desk: lean in, let the rules do the thing the screen's button would, look at the result, step back.</summary>
    private async Task AtDesk(Interactable prop, Action open, Action close, string id)
    {
        await WalkTo(prop.GlobalPosition, 1.3f);
        open();
        await Seconds(1.2f);
        Actions.Perform(Sim, Me, _planet, id, advanceClock: false);
        _notebook.Rebuild(); _almanac.Rebuild(); _board.Rebuild();
        await Seconds(1.2f);
        await Snap(Kind(id), force: true);
        close();
        await Seconds(0.5f);
    }

    /// <summary>The switchboard by hand: real clicks on the board, as the player makes them.</summary>
    private async Task SwitchWork(string[] parts)
    {
        await WalkTo(_switch.GlobalPosition + _switch.GlobalBasis.Z * 1.2f, 0.4f);
        if (!_switch.IsOpen) { _switch.Open(); await Seconds(1.0f); }
        var s = Sim.State;
        int Row(string cust) => s.Switch.Positions.FindIndex(p => p.CustomerId == cust);
        string[] clicks = parts[1] switch
        {
            "read" => new[] { "probe", $"terminal:{Row(parts[2])}", "probe" },
            "test" => new[] { "testset", $"port:{parts[2]}", "testset" },
            "jumper" => Jumper(parts[2]),
            "throw" => new[] { "switch" },
            _ => Array.Empty<string>(),
        };
        if (parts[1] == "panel") Actions.Perform(Sim, Me, _planet, "switch:panel", advanceClock: false);
        foreach (var key in clicks)
        {
            var at = _switch.DisplayPointFor(key, _player.Camera);
            foreach (bool down in new[] { true, false })
            {
                GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at }, true);
                await Frames(3);
            }
            await Seconds(0.35f);
        }
        await Snap("switch-" + parts[1], force: parts[1] == "throw");
        // step back once nothing else needs doing here
        SyncActor();
        if (!Bot.Choose(Sim, Me, _planet).ActionId.StartsWith("switch:")) { await Seconds(1.5f); _switch.Close(); await Seconds(0.5f); }

        string[] Jumper(string cust)
        {
            var p = s.Switch.Positions.First(x => x.CustomerId == cust);
            int port = SwitchRules.KnownGood(s, p.AssignedPort) == false ? SwitchRules.SparePorts.First(i => SwitchRules.OnPort(s, i) is null) : p.AssignedPort;
            return new[] { "hook", $"terminal:{Row(cust)}", $"port:{port}" };
        }
    }

    // ---- moving the body --------------------------------------------------------------------------
    private async Task GoTo(string place)
    {
        var target = WorldOf(place);
        // drive or walk by the same measure the brain used (place to place), so it never asks for the van to come over
        // and then walks there leaving it behind
        double brainDist = Places.DistanceM(_planet, Sim.Content, Me.Place, place);
        bool vanHere = _van.Driven || (_van.GlobalPosition - BodyPos()).Length() < 40;
        if (_cargo.Held is null && vanHere && brainDist > Places.DriveOverM) { await Drive(ParkOf(place) ?? target); Me.VanAt = place; }
        else await WalkTo(target, 1.0f);
        Me.Place = place;
    }

    /// <summary>
    /// The work area here: where the next step of the job is done (the eaves for the drop wire, the skirting for the cable,
    /// the bench for an odd job, the door for a repair, the pole). Kit put down here counts as on site when that step is done.
    /// Null when there's no job here: put it down where you stand.
    /// </summary>
    private Vector3? WorkArea(LotView? lot)
    {
        if (lot?.Install is { } site && Sim.Installs.TryGetValue(lot.Lot.Id, out var job))
            return job.NextStep is { } step && site.SpotFor(step) is { } spot ? spot.GlobalPosition : site.Phone.GlobalPosition;
        if (lot is not null && _oddJobs.TryGetValue(lot.Lot.Id, out var bench)) return bench.Bench.GlobalPosition;
        if (lot is not null && TownRules.FaultAt(Sim.State, lot.Lot.Id) is not null) return lot.Door.GlobalPosition;
        if (Places.IsProject(Me.Place, out var projId) && _projects.TryGetValue(projId, out var proj)) return proj.Spot.GlobalPosition;
        // at a pole site the work area is the marker itself (plant here, or string the span back from here), whichever is here
        if (Places.IsPole(Me.Place, out var pid, out _) && _lines.FirstOrDefault(l => l.Premise.Id == pid) is { View: { } view })
        {
            var poleSite = WorldOf(Me.Place);
            return new[] { view.PlantSpot, view.StringSpot }.Select(x => x.GlobalPosition).OrderBy(p => p.DistanceTo(poleSite)).FirstOrDefault(p => p.DistanceTo(poleSite) < 15, poleSite);
        }
        return null;
    }

    /// <summary>
    /// What kit is in reach of the next step, judged from where that step is done rather than where we happen to stand:
    /// the drop wire's clips run along the wire, so the drum and ladder have to follow them.
    /// </summary>
    private Tools KitInWorkArea() =>
        _kitEverywhere ? Tools.All
        : WorkArea(_town.Lots.FirstOrDefault(l => l.Lot.Id == Me.Place)) is { } p ? _cargo.OnSite(p) : KitOnSite();

    /// <summary>Where the van stops for a place: its driveway (or yard, or forecourt). Null where there is none, such as a pole by the road.</summary>
    private Vector3? ParkOf(string place) =>
        place == Places.Room ? _town.HqPark : _town.Lots.FirstOrDefault(v => v.Lot.Id == place)?.Park;

    private Vector3 BodyPos() => _van.Driven ? _van.GlobalPosition : _player.GlobalPosition;

    /// <summary>Walk there, using doors: out of the room or a house by its door, and in the same way.</summary>
    private async Task WalkTo(Vector3 target, float within)
    {
        if (_van.Driven) { ExitVan(); await Physics(20); }
        var route = new List<Vector3>();
        var room = _town.Hq;
        bool InRoom(Vector3 p) { var l = room.ToLocal(p); return Mathf.Abs(l.X) < RoomBuilder.W / 2 && Mathf.Abs(l.Z) < RoomBuilder.D / 2 && l.Y < 3; }
        LotView? HouseOf(Vector3 p) => _town.Lots.FirstOrDefault(v => v.Install is not null && Indoors(v, p, 0));
        var from = _player.GlobalPosition;
        if (InRoom(from) && !InRoom(target))
            route.AddRange(new[] { room.ToGlobal(new Vector3(RoomBuilder.DoorX, 0.2f, RoomBuilder.D / 2 - 0.8f)), room.ToGlobal(new Vector3(RoomBuilder.DoorX, 0.2f, RoomBuilder.D / 2 + 1.6f)) });
        if (HouseOf(from) is { } hf && HouseOf(target) != hf)
            route.AddRange(new[] { hf.Body.ToGlobal(new Vector3(0, TownBuilder.HouseFloor + 0.1f, 2.4f)), hf.Body.ToGlobal(new Vector3(0, 0.2f, 5.0f)) });
        if (HouseOf(target) is { } ht && HouseOf(from) != ht)
            route.AddRange(new[] { ht.Body.ToGlobal(new Vector3(0, 0.2f, 5.0f)), ht.Body.ToGlobal(new Vector3(0, TownBuilder.HouseFloor + 0.1f, 2.4f)) });
        if (InRoom(target) && !InRoom(from))
            route.AddRange(new[] { room.ToGlobal(new Vector3(RoomBuilder.DoorX, 0.2f, RoomBuilder.D / 2 + 1.6f)), room.ToGlobal(new Vector3(RoomBuilder.DoorX, 0.2f, RoomBuilder.D / 2 - 0.8f)) });
        route.Add(target);
        foreach (var p in route)
        {
            // outdoors, with a house in the way (Church Lane backs on to Main Street): round by the street, not through the wall
            var start = _player.GlobalPosition;
            if ((p - start).Length() > 6 && !InRoom(start) && HouseOf(start) is null && !InRoom(p) && HouseOf(p) is null)
            {
                _roadNet ??= new RoadNet(_planet, _town, GetWorld3D().DirectSpaceState, new Godot.Collections.Array<Rid> { _van.GetRid(), _player.GetRid() });
                if (!_roadNet.Clear(start, p))
                    foreach (var q in _roadNet.Route(start, p).SkipLast(1)) await WalkStraight(q, 1.5f);
            }
            await WalkStraight(p, p == target ? within : 0.45f);
        }
    }

    private async Task WalkStraight(Vector3 target, float within)
    {
        float stuckClock = 0, best = float.MaxValue, total = 0; int tries = 0;
        while (true)
        {
            var up = _player.Up;
            var d = target - _player.GlobalPosition;
            var flat = d - up * d.Dot(up);
            float dist = flat.Length();
            if (dist < within) break;
            float dt = (float)GetPhysicsProcessDeltaTime();
            _player.FaceTowards(flat, 5f * dt);
            Input.ActionPress("move_forward", Mathf.Clamp(dist / 1.5f, 0.35f, 1f));
            await Physics(1);
            total += dt; stuckClock += dt;
            if (total > 1.5f && Time.GetTicksMsec() - _lastShot > 9000 / Engine.TimeScale) await Snap("walk", force: true);
            if (stuckClock > 1.4f)
            {
                if (dist > best - 0.3f)
                {
                    tries++;
                    // round the obstacle: a sidestep, then on; after a few goes, step past it (counted)
                    Input.ActionRelease("move_forward");
                    Input.ActionPress(tries % 2 == 1 ? "move_right" : "move_left");
                    await Physics(30);
                    Input.ActionRelease("move_right"); Input.ActionRelease("move_left");
                    if (tries >= 4) { _player.PlaceAt(target + up * 0.3f, flat); _assists++; _autoLog.Add("   (assist: stepped past an obstacle)"); break; }
                }
                best = Mathf.Min(best, dist); stuckClock = 0;
            }
            if (total > 120) { _player.PlaceAt(target + up * 0.3f, flat); _assists++; break; }
        }
        Input.ActionRelease("move_forward");
        await Physics(2);
    }

    private async Task Drive(Vector3 target)
    {
        if (!_van.Driven)
        {
            await WalkTo(_van.GlobalPosition - _van.GlobalBasis.X * 2.0f, 0.8f);
            EnterVan();
            await Seconds(0.4f);
        }
        if (_teleport)
        {
            // development: straight there, parked, facing back the way it came (not counted as an assist: it was asked for)
            var from = _van.GlobalPosition;
            var dir = from - target; dir -= target.Normalized() * dir.Dot(target.Normalized());
            _van.PlaceAt(target + target.Normalized() * 0.3f, dir.LengthSquared() > 0.01f ? dir.Normalized() : _van.Forward);
            await Physics(15);
            ExitVan();
            await Physics(10);
            return;
        }
        // keep to the roads: along Main Street, in at the driveway, out along the road; never across lawns and through houses
        _roadNet ??= new RoadNet(_planet, _town, GetWorld3D().DirectSpaceState, new Godot.Collections.Array<Rid> { _van.GetRid(), _player.GetRid() });
        var route = _roadNet.Route(_van.GlobalPosition, target);
        float length = 0; var at = _van.GlobalPosition;
        foreach (var p in route) { length += at.DistanceTo(p); at = p; }
        float limit = 40 + length / 4f;

        float t = 0, stuck = 0, nearT = 0;
        int i = 0, stucks = 0, decided = -1;
        bool shotMid = false, reversing = false;
        Vector3 backTo = Vector3.Zero;
        while (t < limit)
        {
            var up = _van.Up;
            Vector3 Flat(Vector3 v) => v - up * v.Dot(up);
            bool last = i == route.Count - 1;
            var to = Flat(route[i] - _van.GlobalPosition);
            float dist = to.Length();

            if (decided != i)
            {
                // a point close behind (backing out of a driveway): reverse out, swinging the tail away from where we're going next,
                // so the van ends up in the street pointing the right way instead of turning circles on the lawn
                decided = i;
                reversing = Mathf.Abs(_van.Forward.SignedAngleTo(to.Normalized(), up)) > 2.0f && dist < 20;
                var next = last ? Vector3.Zero : Flat(route[i + 1] - route[i]).Normalized();
                backTo = route[i] - next * 3.5f;
            }
            if (reversing)
            {
                var back = Flat(backTo - _van.GlobalPosition);
                if (back.Length() < 2.5f) { reversing = false; if (!last) { i++; decided = i; } continue; }
                to = back; dist = back.Length();
            }
            else
            {
                if (!last && dist < 5f) { i++; stucks = 0; continue; }
                // parked: on the spot, or it has been circling close by for a while (the van can't turn that tight)
                if (last && dist < 6f) nearT += (float)GetPhysicsProcessDeltaTime();
                if (last && (dist < 1.8f || (dist < 3.5f && Mathf.Abs(_van.Speed) < 0.6f)
                    || (nearT > 2.5f && Mathf.Abs(_van.Forward.SignedAngleTo(to.Normalized(), up)) > 0.8f))) break;
            }

            float angle = _van.Forward.SignedAngleTo(to.Normalized(), up);
            foreach (var a in new[] { "move_forward", "move_back", "move_left", "move_right" }) Input.ActionRelease(a);
            float steer;
            if (reversing)
            {
                float back = (-_van.Forward).SignedAngleTo(to.Normalized(), up);
                steer = -Mathf.Clamp(back * 1.8f, -1f, 1f);
                Input.ActionPress("move_back", Mathf.Clamp(dist / 6f, 0.4f, 1f));
            }
            else
            {
                steer = Mathf.Clamp(angle * 1.8f, -1f, 1f);
                // how fast: slow enough to make the next corner, and to stop at the end
                float corner = 0;
                if (!last)
                {
                    float turn = to.AngleTo(Flat(route[i + 1] - route[i]));
                    corner = turn < 0.25f ? Van.MaxSpeed : turn < 0.7f ? 11f : 6f;
                }
                float want = Mathf.Min(Van.MaxSpeed, Mathf.Sqrt(corner * corner + 2 * 9f * Mathf.Max(0, dist - (last ? 1.2f : 4f))));
                if (Mathf.Abs(angle) > 0.9f) want = Mathf.Min(want, 6f);
                if (last) want = Mathf.Min(want, Mathf.Max(2.5f, dist * 0.8f)); // creep up the driveway
                float throttle = Mathf.Clamp(want / (_van.OnRoad ? Van.MaxSpeed : Van.OffRoadSpeed), 0f, 1f);
                if (throttle > 0.03f) Input.ActionPress("move_forward", throttle);
            }
            if (Mathf.Abs(steer) > 0.05f) Input.ActionPress(steer > 0 ? "move_left" : "move_right", Mathf.Abs(steer));

            await Physics(1);
            float dt = (float)GetPhysicsProcessDeltaTime();
            t += dt;
            stuck = Mathf.Abs(_van.Speed) < 0.8f ? stuck + dt : 0;
            if (!shotMid && t > 2.5f) { shotMid = true; await Snap("drive", force: true); }
            if (stuck > 2.0f)
            {
                // hit something: back off (or pull forward, if we were reversing), then try again; after a few goes, aim for the next point
                foreach (var a in new[] { "move_forward", "move_back", "move_left", "move_right" }) Input.ActionRelease(a);
                Input.ActionPress(reversing ? "move_forward" : "move_back");
                await Seconds(1.2f);
                Input.ActionRelease("move_forward"); Input.ActionRelease("move_back");
                stuck = 0;
                if (++stucks >= 3 && !last) { i++; stucks = 0; }
            }
        }
        foreach (var a in new[] { "move_forward", "move_back", "move_left", "move_right" }) Input.ActionRelease(a);
        if (t >= limit) { _van.PlaceAt(target, _van.Forward); _assists++; _autoLog.Add($"   (assist: van placed at the destination; stuck at route point {i + 1} of {route.Count}{(reversing ? ", reversing" : "")})"); }
        for (int k = 0; k < 60 && Mathf.Abs(_van.Speed) > 0.3f; k++) await Physics(1); // let it roll to a stop
        await Seconds(0.5f);
        ExitVan();
        await Physics(10);
    }

    private RoadNet? _roadNet;

    /// <summary>Walk up to a thing, look at it, and press or hold E on it (R for the second way).</summary>
    private async Task Use(Interactable it, bool alt = false)
    {
        var p = it.GlobalPosition;
        await WalkTo(p, 1.4f);
        await Aim(p);
        float hold = alt ? 0 : it.HoldSeconds();
        if (_player.Focus == it)
        {
            if (alt) await Tap("alt_use");
            else if (hold > 0)
            {
                Input.ActionPress("interact");
                await Seconds(hold * 0.6f);
                await Snap("hold");
                await Seconds(hold * 0.4f + 0.25f);
                Input.ActionRelease("interact");
            }
            else await Tap("interact");
        }
        else
        {
            // couldn't line the crosshair up (something in the way): do what the press would have done
            await Seconds(hold);
            if (alt) it.OnAlt?.Invoke(); else if (it.CanUse()) it.OnUse?.Invoke();
            _autoLog.Add($"   (pressed {it.Name} without the crosshair on it)");
        }
        _player.Camera.Rotation = Vector3.Zero;
        await Physics(4);
    }

    private async Task Aim(Vector3 p)
    {
        _player.FaceTowards(p - _player.GlobalPosition);
        await Physics(2);
        var cam = _player.Camera.GlobalPosition;
        var up = _player.Up;
        var v = p - cam;
        float vertical = v.Dot(up), horiz = (v - up * vertical).Length();
        _player.Camera.Rotation = new Vector3(Mathf.Clamp(Mathf.Atan2(vertical, horiz), -1.4f, 1.4f), 0, 0);
        await Physics(3);
    }

    private async Task Tap(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        await Physics(2);
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        await Physics(2);
    }

    private async Task Physics(int frames) { for (int i = 0; i < frames; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Seconds(float s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);

    // ---- keeping the sim's idea of the body in step with the world -----------------------------------------
    private void SyncActor()
    {
        Me.Place = PlaceOf(BodyPos()) ?? Me.Place;
        Me.VanAt = PlaceOf(_van.GlobalPosition) ?? Me.VanAt;
        Me.Holding = _cargo.Held?.Tool ?? Tools.None;
        Me.Kit.Clear();
        foreach (var i in _cargo.Items.Where(i => i.Where == Carryable.Place.Ground))
            if (PlaceOf(i.GlobalPosition) is { } at) Me.Kit[i.Tool] = at;
    }

    /// <summary>The named place nearest a world position, if it is close enough to count as being there.</summary>
    private string? PlaceOf(Vector3 p)
    {
        foreach (var (id, view) in _projects)
            if (view.Visible && (view.GlobalPosition - p).Length() < 8) return Places.Project(id);
        if ((_town.Hq.GlobalPosition - p).Length() < 13) return Places.Room;
        var lot = _town.Lots.OrderBy(v => (v.Body.GlobalPosition - p).Length()).FirstOrDefault();
        if (lot is not null && (lot.Body.GlobalPosition - p).Length() < 13) return lot.Lot.Id;
        foreach (var (premise, view) in _lines)
            for (int k = 0; k < view.Count; k++)
                if ((view.PolePosition(k) - p).Length() < 12) return Places.Pole(premise.Id, k);
        return null;
    }

    private Vector3 WorldOf(string place)
    {
        if (place == Places.Room) return _town.Hq.ToGlobal(new Vector3(0.4f, 0.2f, 0.3f));
        if (_town.Lots.FirstOrDefault(v => v.Lot.Id == place) is { } lot)
        {
            // just outside the front door
            var door = lot.Door.GlobalPosition; var up = door.Normalized();
            var outward = door - lot.Body.GlobalPosition; outward -= up * outward.Dot(up);
            return door + outward.Normalized() * 2.2f;
        }
        if (Places.IsProject(place, out var projId) && _projects.TryGetValue(projId, out var proj))
            return proj.Spot.GlobalPosition + proj.GlobalBasis.Z * 2.5f;   // in front of the work spot
        if (Places.IsPole(place, out var pid, out int k) && _lines.FirstOrDefault(l => l.Premise.Id == pid) is { View: { } view })
            return view.PolePosition(k) + view.PolePosition(k).Normalized().Cross(Vector3.Up).Normalized() * 2.5f;
        return _player.GlobalPosition;
    }

    private Interactable RoomProp(string name) => _town.Hq.GetNode<Interactable>(name);

    private void RefreshViews()
    {
        foreach (var v in _town.Lots) v.Install?.Sync();
        foreach (var r in _lineRefresh) r();
        _cargo.Refresh(); SyncPaper(); RefreshIdleScreens();
    }

    // ---- photos -------------------------------------------------------------------------------------
    private static string Kind(string id) => id.Split(':')[0] switch
    {
        "install" => "install-" + (id.Split(':').ElementAtOrDefault(1) ?? ""),
        var k => k,
    };

    /// <summary>Save what the player sees: the first time each kind of thing happens, and every few seconds on the move.</summary>
    private async Task Snap(string kind, bool force = false)
    {
        if (_autoDir is null || _shotN >= 400) return;
        if (!force && !_shotKinds.Add(kind) && Time.GetTicksMsec() - _lastShot < 20000 / Engine.TimeScale) return;
        _shotKinds.Add(kind);
        await Frames(2);
        _shotN++; _lastShot = Time.GetTicksMsec();
        using (var img = GetViewport().GetTexture().GetImage()) img.SavePng($"{_autoDir}/{_shotN:000}-{kind}.png");
        if (_shotN % 10 == 0) GC.Collect();
    }
}
