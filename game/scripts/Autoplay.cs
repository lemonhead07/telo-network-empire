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
        Me.OnSiteOverride = KitOnSite;
        await Frames(40);
        await Snap("start", force: true);

        ulong started = Time.GetTicksMsec();
        string last = ""; int same = 0;
        string Sig() => $"{Sim.State.Day}:{Sim.State.Minute / 20}:{Me.Place}:{Me.Holding}:{string.Join(",", Me.Kit)}:{Sim.State.Cash}:{string.Join(",", Sim.Installs.Values.Select(j => j.ElapsedMinutes))}:{string.Join(",", Sim.Benches.Values.Select(b => b.Task.ElapsedMinutes))}:{Sim.State.Customers.Count}:{Sim.State.Switch.Positions.Sum(p => p.Jumper + (p.TagRead ? 1 : 0))}";
        string before = "";
        for (int i = 0; i < 4000; i++)
        {
            if (Done(until) || Time.GetTicksMsec() - started > minutes * 60000) break;
            SyncActor();
            var (id, why) = Bot.Choose(Sim, Me, _planet);
            _autoWhy = why;
            _autoLog.Add($"{Sim.State.Date} {Clock.Text(Sim.State)}  {id}  — {why}");
            GD.Print("AUTO ", _autoLog[^1]);
            string now = Sig();
            same = id == last && now == before ? same + 1 : 0; last = id; before = now;
            if (same >= 2 && id != "wait" && id != "sleep")
            {
                // the body tried twice and nothing changed: do it directly in the world and carry on (counted, never hidden)
                await Assist(id);
                continue;
            }
            await Do(id);
            await Snap(Kind(id));
        }

        await Snap("end", force: true);
        bool farm = Sim.Content.Premises.Values.Where(p => p.PoleLine is not null).All(p => LedgerRules.StatusOf(Sim.State, p.Id) == PromiseStatus.Served);
        string summary = $"AUTOPLAY {(Done(until) ? "OK" : "STOPPED")}: until={until} {Sim.State.Date} step={Onboarding.Current(Sim.State, _content)} subs={Sim.State.LiveSubscribers} cash={Sim.State.Cash} farm={farm} photos={_shotN} assists={_assists}";
        _autoLog.Add(summary);
        GD.Print(summary);
        if (_autoDir is not null) Godot.FileAccess.Open($"{_autoDir}/autoplay.txt", Godot.FileAccess.ModeFlags.Write)?.StoreString(string.Join("\n", _autoLog));
        Engine.TimeScale = 1;
        GetTree().Quit(Done(until) ? 0 : 1);
    }

    private bool Done(string until) => until switch
    {
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
                if (lot is { Install: not null }) await WalkTo(lot.Body.ToGlobal(new Vector3(0.6f, TownBuilder.HouseFloor + 0.1f, 2.2f)), 0.5f);
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
        float dist = (target - BodyPos()).Length();
        if (_cargo.Held is null && dist > (float)Places.DriveOverM) await Drive(target);
        else await WalkTo(target, 1.0f);
        Me.Place = place;
    }

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
        foreach (var p in route) await WalkStraight(p, p == target ? within : 0.45f);
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
        float t = 0, stuck = 0;
        bool shotMid = false;
        while (t < 90)
        {
            var up = _van.Up;
            var d = target - _van.GlobalPosition;
            var flat = d - up * d.Dot(up);
            float dist = flat.Length();
            if (dist < 7) break;
            float angle = _van.Forward.SignedAngleTo(flat.Normalized(), up);
            float steer = Mathf.Clamp(angle * 1.8f, -1f, 1f);
            Input.ActionRelease("move_left"); Input.ActionRelease("move_right");
            if (Mathf.Abs(steer) > 0.05f) Input.ActionPress(steer > 0 ? "move_left" : "move_right", Mathf.Abs(steer));
            Input.ActionPress("move_forward", Mathf.Clamp(dist / 35f, 0.35f, 1f) * (Mathf.Abs(angle) > 1.2f ? 0.5f : 1f));
            await Physics(1);
            float dt = (float)GetPhysicsProcessDeltaTime();
            t += dt;
            stuck = Mathf.Abs(_van.Speed) < 1f ? stuck + dt : 0;
            if (!shotMid && t > 2.5f) { shotMid = true; await Snap("drive", force: true); }
            if (stuck > 2.5f)
            {
                // reverse out of whatever we hit, then try again
                Input.ActionRelease("move_forward"); Input.ActionPress("move_back");
                await Seconds(1.2f);
                Input.ActionRelease("move_back");
                stuck = 0;
            }
        }
        foreach (var a in new[] { "move_forward", "move_back", "move_left", "move_right" }) Input.ActionRelease(a);
        if (t >= 90) { _van.PlaceAt(target, _van.Forward); _assists++; _autoLog.Add("   (assist: van placed at the destination)"); }
        await Seconds(0.8f);
        ExitVan();
        await Physics(10);
    }

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
