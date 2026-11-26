using Godot;
using Telo.Game.Audio;
using Telo.Game.World;
using Telo.Sim.Content;
using Telo.Sim.Town;

namespace Telo.Game;

/// <summary>
/// What you hear that isn't a press of a key (EA7): the ground under your feet, the answering machine taking a call, the bell
/// over Sol's door, the van's door. Only sounds; nothing here decides anything.
/// </summary>
public partial class GameRoot
{
    private Interactable? _answering;
    private int _messagesHeard = -1, _messagesDay;
    private float _messageBeepIn;
    private bool _atSols;

    // ---- footsteps: what the ground is made of where you stand ---------------------------------------------------
    /// <summary>The footstep for this spot: steel in the data centre, on its roof and on the ferry jetty; floorboards indoors; tarmac on a street, a driveway or a road; grass everywhere else.</summary>
    private string GroundSound(Vector3 at)
    {
        if (_dc is not null && (_dc.Inside(at) || _dc.OnRoof(at))) return "step.metal";
        if (OnJetty(at)) return "step.metal";
        if (PlayerIndoors(at)) return "step.floor";
        if (OnStreet(at) || OnDriveway(at) || OnRoad(at)) return "step.road";
        return "step.grass";
    }

    private bool OnJetty(Vector3 at)
    {
        foreach (var q in _quayViews)
        {
            var l = q.Node.ToLocal(at);
            if (Mathf.Abs(l.X) < 3.6f && l.Z > -0.5f && l.Z < q.Length + 0.5f && l.Y > -1f && l.Y < 2.5f) return true;
        }
        return false;
    }

    /// <summary>Each street in its site's frame (as TownBuilder.Place lays it): centre, east, north, radius at street level.</summary>
    private List<(TownBuilder.Street St, Vector3 C, Vector3 E, Vector3 N, float R)>? _streetFrames;

    /// <summary>On the tarmac of a street in town, a village or a far town (a little margin for the kerb).</summary>
    private bool OnStreet(Vector3 at)
    {
        _streetFrames ??= TownBuilder.Streets.Select(st => _planet.SiteById(st.Site) is { } site ? Frame(st, site) : default).Where(f => f.St is not null).ToList();
        var d = at.Normalized();
        foreach (var (st, c, e, n, r) in _streetFrames)
        {
            float dc = d.Dot(c);
            if (dc < 0.99f) continue;   // nowhere near this site
            float k = r / dc, east = k * d.Dot(e), south = -k * d.Dot(n);
            float along = st.NorthSouth ? south : east, across = st.NorthSouth ? east : south;
            if (Mathf.Abs(across - st.At) < TownBuilder.StreetHalfWidth + 0.4f && along > st.From - 3.3f && along < st.To + 3.3f) return true;
        }
        return false;

        (TownBuilder.Street St, Vector3 C, Vector3 E, Vector3 N, float R) Frame(TownBuilder.Street st, Telo.Sim.Planet.Site site)
        {
            var c = PlanetBuilder.ToGodot(site.Dir).Normalized();
            var e = Vector3.Up.Cross(c).Normalized();
            return (st, c, e, c.Cross(e), (float)(_planet.RadiusM + site.HeightM));
        }
    }

    /// <summary>On a concrete driveway: your own, or a house's from the street to where the car stands.</summary>
    private bool OnDriveway(Vector3 at)
    {
        if (Near(at, _town.HqEntry, _town.HqPark)) return true;
        foreach (var v in _town.Lots)
            if (v is { DriveEntry: { } a, Park: { } b } && Near(at, a, b)) return true;
        return false;

        static bool Near(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(1e-6f, ab.LengthSquared()), 0, 1);
            return (p - (a + ab * t)).Length() < 1.8f;
        }
    }

    /// <summary>The roads between places, as unit directions, with a quick bound for each (built once).</summary>
    private List<(Vector3[] Pts, Vector3 Mid, float Reach, float Half)>? _roadDirs;

    /// <summary>On one of the planet's roads between the settlements (a real distance to its centreline; streets cover the settlements).</summary>
    private bool OnRoad(Vector3 at)
    {
        float R = (float)_planet.RadiusM;
        _roadDirs ??= _planet.Roads.Where(r => r.Points.Count > 1).Select(r =>
        {
            var pts = r.Points.Select(p => PlanetBuilder.ToGodot(p).Normalized()).ToArray();
            var mid = pts[pts.Length / 2];
            return (pts, mid, pts.Max(p => (p - mid).Length()) * R + (float)r.HalfWidthM + 2f, (float)r.HalfWidthM);
        }).ToList();
        var d = at.Normalized();
        // the drawn road starts where a settlement's streets end (0.75 of its radius): inside that it's gardens and greens
        foreach (var site in _planet.Sites)
            if ((d - PlanetBuilder.ToGodot(site.Dir)).Length() * R < site.RadiusM * 0.75) return false;
        foreach (var (pts, mid, reach, half) in _roadDirs)
        {
            if ((d - mid).Length() * R > reach) continue;
            for (int i = 1; i < pts.Length; i++)
            {
                var a = pts[i - 1]; var ab = pts[i] - a;
                float t = Mathf.Clamp((d - a).Dot(ab) / Mathf.Max(1e-12f, ab.LengthSquared()), 0, 1);
                if ((d - (a + ab * t)).Length() * R < half + 0.3f) return true;
            }
        }
        return false;
    }

    // ---- things in the world making their own noises ---------------------------------------------------------------
    /// <summary>What working at a door sounds like (the Yard's counter, a repair, an odd job at the shop or farm), as LotHold times it.</summary>
    private (string? Hold, string? Done) LotSounds(LotDef lot)
    {
        var s = Sim.State;
        if (lot.Kind == LotKind.Yard) return ("work.tick", "cash.till");
        if (TownRules.FaultAt(s, lot.Id) is not null) return ("tool.screw", "sw.test.ok");
        if (TownRules.JobAt(s, lot.Id) is { } job) return (SoundCue.ForStep(job.Title).Hold, "work.done");
        return (null, null);
    }

    /// <summary>The van's door: open, then shut behind you (getting in or out).</summary>
    private void VanDoor()
    {
        if (!_van.Door.IsInsideTree()) return;
        var at = _van.Door.GlobalPosition;
        Sfx.Play("van.door.open", at);
        Sfx.PlayAfter(0.55, "van.door.close", at);
    }

    /// <summary>
    /// Twice a second: a call coming in rings the telephone and the answering machine takes it (a message left overnight is
    /// only the machine's beep); while one waits it beeps now and then if you're in the room; the bell over Sol's door rings as
    /// you step up to his counter.
    /// </summary>
    private void ListenToTheWorld(float seconds)
    {
        var s = Sim.State;
        if (_answering is not null && _answering.IsInsideTree())
        {
            var machine = _answering.GlobalPosition;
            if (_messagesHeard >= 0 && s.MessagesWaiting > _messagesHeard && !_frontDoor)
            {
                if (s.Day == _messagesDay) { Sfx.Play("phone.ring", machine); Sfx.PlayAfter(2.6, "am.beep", machine); }
                else Sfx.Play("am.beep", machine);
                _messageBeepIn = 20f;
            }
            _messageBeepIn -= seconds;
            if (s.MessagesWaiting > 0 && _messageBeepIn <= 0 && !_frontDoor && !_van.Driven && machine.DistanceTo(_player.GlobalPosition) < 5f)
            {
                Sfx.Play("am.beep", machine, db: -4);
                _messageBeepIn = 20f;
            }
            _messagesHeard = s.MessagesWaiting; _messagesDay = s.Day;
        }

        if (!_van.Driven && _town.Lots.FirstOrDefault(l => l.Counter is not null) is { Counter: { } counter } sol && counter.IsInsideTree())
        {
            float d = counter.GlobalPosition.DistanceTo(_player.GlobalPosition);
            bool open = _ignoreHours || !HomeRules.ShopShut(s, _content, sol.Lot);
            if (!_atSols && d < 3f && open) { _atSols = true; Sfx.Play("door.bell", sol.Door.GlobalPosition); }
            else if (d > 6f) _atSols = false;
        }
    }
}
