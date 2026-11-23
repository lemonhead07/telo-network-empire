using Godot;
using Telo.Sim.Content;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Game.World;

/// <summary>
/// People who walk (EA2c, D44): each household's person, out on the pavement while their routine says they're out
/// (<see cref="PeopleRules"/>): down their garden path, along the street, up a neighbour's path to chat on the step, or into
/// Sol's, and home again. Looks only: nothing bumps into them. Look at one and you read who they are, how they feel and
/// whose line they're on; a disc over their head says the same at a glance (amber: your line, grey-blue: NezTel's; the face
/// is their mood). The schedule is the sim's; this only draws it. Only households within <see cref="Range"/> of the camera
/// are walked.
/// </summary>
public partial class Walkers : Node3D
{
    public const float Range = 200f;
    /// <summary>Where people walk: this far from a street's centre line (the road is 3.25 m either side; poles and lamps stand at 5).</summary>
    private const float Pavement = 4.1f;
    /// <summary>A comfortable walking pace, used for the parts of a trip you can see when the rest is out of sight (round the back).</summary>
    private const float PaceMps = 1.9f;
    /// <summary>Feet above the lot's ground: in the doorway (top of the ramp), on the step, on paths and grass, crossing the road.</summary>
    private const float DoorLift = 0.14f, StepLift = 0.05f, WalkLift = 0.04f, RoadLift = 0.1f;

    private readonly Simulation _sim;
    private readonly ContentDb _content;
    private readonly PlanetGen _gen;
    private readonly List<LotView> _homes;
    private readonly Dictionary<string, LotView> _views;
    private readonly Dictionary<string, Walker> _walkers = new();
    private readonly Dictionary<string, Route> _routes = new();
    private readonly Dictionary<string, IReadOnlyList<Outing>> _today = new();
    private int _cachedDay = -1;

    /// <summary>The clock as the world shows it: whole minutes from the sim plus the fraction lived since.</summary>
    public required Func<double> Minute { get; init; }
    /// <summary>Real seconds a game minute lasts while you're up and about.</summary>
    public required Func<float> RealSecondsPerMinute { get; init; }
    /// <summary>
    /// A job under way at this lot right now (an install, or something on their rug). Odd jobs and orders in progress live in the
    /// game, not the sim, so the sim may have them out on an errand: they stay in with you instead, and the street doesn't show them.
    /// </summary>
    public Func<string, bool> WorkUnderWay { get; init; } = _ => false;

    public Walkers(Simulation sim, ContentDb content, PlanetGen gen, Town town)
    {
        Name = "Walkers";
        _sim = sim; _content = content; _gen = gen;
        _homes = town.Lots.Where(l => l.Lot.Kind == LotKind.House && l.Lot.Premise is null && !l.Lot.Business && gen.SiteById(l.Lot.Site) is not null).ToList();
        _views = town.Lots.GroupBy(l => l.Lot.Id).ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>Everyone out and in view right now: their household, the figure, what you look at, and where they are in their day.</summary>
    public IEnumerable<(LotDef Lot, Node3D Body, Interactable Hot, WhereKind Kind)> Shown =>
        _walkers.Values.Where(w => w.Body.Visible).Select(w => (w.Home.Lot, w.Body, w.Hot, w.Where.Kind));

    /// <summary>
    /// Where someone on this trip would be at that minute of its day, in site metres (x east, y south); null while out of
    /// sight (indoors at the shop, or round the corner). For choosing a picture only: the street itself follows
    /// <see cref="PeopleRules.Now"/>.
    /// </summary>
    public Vector2? SpotOn(LotDef home, Outing o, double minute)
    {
        if (!_views.TryGetValue(o.To, out var dest)) return null;
        var route = RouteBetween(home, dest.Lot);
        float seconds = o.WalkMinutes * RealSecondsPerMinute();
        float? t = minute >= o.Leave && minute < o.Arrive ? (float)((minute - o.Leave) / o.WalkMinutes)
            : minute >= o.StartBack && minute < o.Back ? 1 - (float)((minute - o.StartBack) / o.WalkMinutes) : null;
        if (t is { } f) return route.At(f, seconds) is { Hidden: false } p ? p.Pos : null;
        return minute >= o.Arrive && minute < o.StartBack && !PopsInto(dest.Lot) ? route.End : null;
    }

    private IReadOnlyList<Outing> Today(LotDef lot)
    {
        int day = _sim.State.Day;
        if (day != _cachedDay) { _today.Clear(); _cachedDay = day; }
        if (!_today.TryGetValue(lot.Id, out var list)) _today[lot.Id] = list = PeopleRules.Day(_sim.State, _content, lot, day);
        return list;
    }

    public override void _Process(double delta)
    {
        var cam = GetViewport().GetCamera3D();
        if (cam is null) return;
        var eye = cam.GlobalPosition;
        double minute = Minute();
        float dt = (float)delta;
        foreach (var home in _homes)
        {
            _walkers.TryGetValue(home.Lot.Id, out var w);
            bool near = (home.Position - eye).LengthSquared() < Range * Range;
            // only someone near you and inside one of today's trips is asked where they are (most of the town is at home)
            Whereabouts where = near && !WorkUnderWay(home.Lot.Id) && Today(home.Lot).Any(o => minute >= o.Leave && minute < o.Back)
                ? PeopleRules.Now(_sim.State, _content, home.Lot, minute)
                : new Whereabouts(WhereKind.Home, home.Lot.Id, home.Lot.Id, 0, null);
            // at the other end: chatting on a neighbour's step, or out of sight inside Sol's (or the pub, the post office...)
            bool shown = where.Kind == WhereKind.Walking
                || where.Kind == WhereKind.There && _views.TryGetValue(where.To, out var at) && !PopsInto(at.Lot);
            if (!shown && w is null) continue;
            w ??= Make(home);
            w.Where = where;
            if (shown && Pose(w, where, minute, dt, out var xf))
            {
                w.Body.GlobalTransform = xf;
                w.Body.Visible = true;
                w.Hot.CollisionLayer = Player.FirstPersonController.LookLayer;
                if (home.Resident is not null) home.Resident.Visible = false;   // they're out here, not by their window
                Badge(w, cam, dt);
            }
            else
            {
                w.Body.Visible = false;
                w.Hot.CollisionLayer = 0;
                w.Settled = false;   // next time they're seen, they start facing the right way
            }
        }
    }

    // ---- where someone stands this frame -------------------------------------------------------
    private bool Pose(Walker w, Whereabouts where, double minute, float dt, out Transform3D xf)
    {
        xf = default;
        var lot = w.Home.Lot;
        if (where.Outing is not { } o || _gen.SiteById(lot.Site) is not { } site || !_views.TryGetValue(o.To, out var dest)) return false;
        var route = RouteBetween(lot, dest.Lot);
        Vector2 at; float lift, yaw; bool moving;
        if (where.Kind == WhereKind.There)
        {
            // on the neighbour's step, facing their door, passing the time of day
            at = route.End; lift = StepLift; moving = false;
            yaw = Yaw(new Vector2(0, -Facing(dest.Lot)));
            yaw += Mathf.Sin((float)minute * 0.5f + lot.Id.Length) * 12f;   // turning to look up the street now and then
        }
        else
        {
            bool back = where.From != lot.Id;                 // the walk home is the same route, the other way
            float seconds = o.WalkMinutes * RealSecondsPerMinute();
            var p = route.At(back ? 1 - (float)where.T : (float)where.T, seconds);
            if (p.Hidden) return false;                       // round the corner, out of sight
            at = p.Pos; lift = p.Lift; moving = true;
            var dir = back ? -p.Dir : p.Dir;
            yaw = Yaw(dir);
            w.Stride = p.Distance;
        }
        // turn smoothly at corners; a newcomer starts already facing the right way
        float target = Mathf.DegToRad(yaw);
        w.Yaw = w.Settled ? Mathf.LerpAngle(w.Yaw, target, 1 - Mathf.Exp(-9f * dt)) : target;
        w.Settled = true;
        // a small bob and sway with each step; standing, just a slow shift of weight
        float phase = moving ? w.Stride / 0.72f * Mathf.Pi : (float)minute * 0.6f;
        float bob = moving ? Mathf.Abs(Mathf.Sin(phase)) * 0.035f : 0;
        float sway = moving ? Mathf.Sin(phase) * 2.0f : Mathf.Sin(phase) * 1.2f;
        xf = TownBuilder.Place(_gen, site, at.X, at.Y, Mathf.RadToDeg(w.Yaw), lift + bob);
        xf.Basis *= new Basis(Vector3.Back, Mathf.DegToRad(sway));
        return true;
    }

    /// <summary>Yaw (degrees) that turns a figure's face (+Z) to point along a direction in site metres (x east, y south).</summary>
    private static float Yaw(Vector2 dir) => dir.LengthSquared() < 1e-6f ? 0 : Mathf.RadToDeg(Mathf.Atan2(dir.X, dir.Y));

    /// <summary>+1 for a building whose front faces south (yaw 0), -1 for one facing north (yaw 180).</summary>
    private static int Facing(LotDef lot) => Mathf.Abs(Mathf.Wrap((float)lot.Yaw, -180f, 180f)) < 90 ? 1 : -1;

    private static float FrontOf(LotDef lot) => lot.Kind == LotKind.Shop ? 4f : 3.5f;

    /// <summary>Somewhere you go in rather than chat on the step: Sol's, or a village business (the pub, the post office...).</summary>
    private static bool PopsInto(LotDef lot) => lot.Kind == LotKind.Shop || lot.Business;

    /// <summary>A point in a building's own frame (x across its front, z out of its front door) as site metres (x east, y south).</summary>
    private static Vector2 Local(LotDef lot, float x, float z) => new((float)lot.East + x * Facing(lot), (float)lot.South + z * Facing(lot));

    /// <summary>A point straight out from a building's front door, this far beyond the front wall.</summary>
    private static Vector2 OutFront(LotDef lot, float beyond) => Local(lot, 0, FrontOf(lot) + beyond);

    /// <summary>The way down the side of a house that its driveway (and car) isn't on, just clear of the wall and the shrubs.</summary>
    private static float SidePath(LotDef lot) =>
        TownBuilder.StreetOf(lot) is { } st ? -TownBuilder.DriveSide(lot, Facing(lot) == 1, st) * 6.0f : 6.0f;

    // ---- routes: front door, garden path, pavement, and up the other path ----------------------------
    private Route RouteBetween(LotDef home, LotDef dest)
    {
        string key = home.Id + ">" + dest.Id;
        if (_routes.TryGetValue(key, out var cached)) return cached;
        var r = new Route();
        bool shop = PopsInto(dest);
        var end = shop ? OutFront(dest, 0.3f) : OutFront(dest, 1.15f);   // in at the shop door, or onto the neighbour's step
        float endLift = shop ? WalkLift : StepLift;
        r.Add(OutFront(home, 0.3f), DoorLift);
        var sa = TownBuilder.StreetOf(home); var sb = TownBuilder.StreetOf(dest);
        int fa = Facing(home), fb = Facing(dest);
        float ea = (float)home.East, eb = (float)dest.East;
        if (sa is null || sb is null) { r.Add(end, endLift); return _routes[key] = r.Done(); }
        if (sa != sb)
        {
            // another street, back to back with theirs (Church Lane and Main Street): round the back, as the sim measures it.
            // Down the side of the house and out of sight behind it, through the gardens, and round the side of theirs.
            float xa = SidePath(home), xb = SidePath(dest);
            r.Add(OutFront(home, 1.6f), WalkLift);
            r.Add(Local(home, xa, FrontOf(home) + 1.6f), WalkLift);
            r.Add(Local(home, xa, -FrontOf(home) - 0.6f), WalkLift);
            r.Add(Local(dest, xb, -FrontOf(dest) - 0.6f), WalkLift, hidden: true);
            r.Add(Local(dest, xb, FrontOf(dest) + 1.6f), WalkLift);
            if (!shop) r.Add(OutFront(dest, 1.6f), WalkLift);
        }
        else if (fa == fb && Mathf.Abs(ea - eb) <= 24)
        {
            // next door but one at most: across the front lawns, in front of the houses (the side fences stop at the front wall)
            r.Add(OutFront(home, 2.3f), WalkLift);
            r.Add(OutFront(dest, 2.3f), WalkLift);
        }
        else
        {
            // down the garden path to the pavement, along it (over the road halfway along if they live across it), up their path
            float pa = sa.At - fa * Pavement, pb = sb.At - fb * Pavement;
            r.Add(new Vector2(ea, pa), WalkLift);
            if (fa != fb)
            {
                float mid = (ea + eb) / 2;
                r.Add(new Vector2(mid, pa), WalkLift);
                r.Add(new Vector2(mid, sa.At - fa * 3.2f), RoadLift);
                r.Add(new Vector2(mid, sa.At + fa * 3.2f), RoadLift);
                r.Add(new Vector2(mid, pb), WalkLift);
            }
            r.Add(new Vector2(eb, pb), WalkLift);
        }
        r.Add(end, endLift);
        return _routes[key] = r.Done();
    }

    /// <summary>A walk as a line through site metres (x east, y south), with the height of the ground under each corner.</summary>
    private sealed class Route
    {
        private readonly List<Vector2> _p = new();
        private readonly List<float> _lift = new();
        private readonly List<bool> _hide = new();   // _hide[i]: the leg into point i is walked out of sight
        private float[] _len = Array.Empty<float>();
        public float Length { get; private set; }
        private float _hidden;
        public Vector2 End => _p[^1];

        public void Add(Vector2 p, float lift, bool hidden = false)
        {
            if (_p.Count > 0 && _p[^1].DistanceTo(p) < 0.05f) return;
            _p.Add(p); _lift.Add(lift); _hide.Add(hidden);
        }

        public Route Done()
        {
            _len = new float[_p.Count];
            for (int i = 1; i < _p.Count; i++)
            {
                _len[i] = _p[i - 1].DistanceTo(_p[i]);
                Length += _len[i];
                if (_hide[i]) _hidden += _len[i];
            }
            return this;
        }

        public readonly record struct Point(Vector2 Pos, Vector2 Dir, float Lift, float Distance, bool Hidden);

        /// <summary>
        /// Where a walker is a fraction <paramref name="t"/> of the way through a walk lasting <paramref name="seconds"/>. A route
        /// in plain sight is walked evenly. One with a stretch out of sight is walked at a comfortable pace where you can see it,
        /// and the stretch out of sight takes up the rest of the time (or none, if the clock allows little).
        /// </summary>
        public Point At(float t, float seconds)
        {
            int n = _p.Count;
            if (n < 2) return new Point(_p[0], Vector2.Zero, _lift[0], 0, false);
            t = Mathf.Clamp(t, 0, 1);
            bool squeeze = seconds > 0 && _hidden > 0 && Length > _hidden;
            // time on each leg, as fractions of the walk
            float pace = squeeze ? Mathf.Max(PaceMps, (Length - _hidden) / seconds) : 0;
            float hiddenTime = squeeze ? Mathf.Max(0, seconds - (Length - _hidden) / pace) : 0;
            float clock = 0, walked = 0;
            for (int i = 1; i < n; i++)
            {
                float legTime = !squeeze ? _len[i] / Length
                    : (_hide[i] ? (hiddenTime * _len[i] / _hidden) : _len[i] / pace) / seconds;
                if (t <= clock + legTime || i == n - 1)
                {
                    float f = legTime > 1e-6f ? Mathf.Clamp((t - clock) / legTime, 0, 1) : 1;
                    var dir = (_p[i] - _p[i - 1]).Normalized();
                    return new Point(_p[i - 1].Lerp(_p[i], f), dir, Mathf.Lerp(_lift[i - 1], _lift[i], f), walked + _len[i] * f, squeeze && _hide[i]);
                }
                clock += legTime; walked += _len[i];
            }
            return new Point(_p[^1], Vector2.Zero, _lift[^1], Length, false);
        }
    }

    // ---- the figure ----------------------------------------------------------------------------
    private sealed class Walker
    {
        public required LotView Home { get; init; }
        public required Node3D Body { get; init; }
        public required Interactable Hot { get; init; }
        public required MeshInstance3D Disc { get; init; }
        public Whereabouts Where { get; set; }
        public float Yaw, Stride, Recheck;
        public bool Settled;
        public (Provider, Mood)? Shown;
    }

    private Walker Make(LotView home)
    {
        var lot = home.Lot;
        var body = new Node3D { Name = $"Walker_{lot.Id}", Visible = false };
        string model = $"person.res-{lot.Archetype}";
        body.AddChild(Props.HasModel(model) ? Props.Make(model) : Capsule(lot));
        // what you look at: a person-sized box on its own layer, so it never stops you walking and a door behind it still wins up close
        var hot = new Interactable { Position = new Vector3(0, 0.92f, 0), CollisionLayer = 0, CollisionMask = 0 };
        hot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.62f, 1.84f, 0.5f) } });
        hot.Prompt = () => PeopleRules.Describe(_sim.State, _content, lot);
        hot.CanUse = () => false;
        body.AddChild(hot);
        var disc = new MeshInstance3D { Name = "Badge", MaterialOverride = BadgeMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        body.AddChild(disc);
        AddChild(body);
        var w = new Walker { Home = home, Body = body, Hot = hot, Disc = disc };
        _walkers[lot.Id] = w;
        return w;
    }

    /// <summary>Stand-in when an archetype has no model yet: a coat, a head, and their name.</summary>
    private static Node3D Capsule(LotDef lot)
    {
        var coat = new[] { new Color(0.55f, 0.3f, 0.25f), new Color(0.3f, 0.4f, 0.55f), new Color(0.35f, 0.45f, 0.3f), new Color(0.5f, 0.45f, 0.35f) }[lot.Id.Sum(ch => ch) % 4];
        var node = new LowPoly().Cylinder(Vector3.Zero, 0.2f, 0.17f, 1.42f, 8, coat)
            .Gem(new Vector3(0, 1.6f, 0), 0.14f, 0.16f, 0.14f, 6, new Color(0.82f, 0.64f, 0.5f)).Instance("Person");
        node.AddChild(new Label3D { Text = lot.Resident, Position = new Vector3(0, 1.0f, 0.22f), FontSize = 32, PixelSize = 0.003f, Modulate = new Color(0.98f, 0.95f, 0.85f), OutlineSize = 8 });
        return node;
    }

    // ---- the disc over their head: whose line (its colour) and how they feel about it (its face) -----
    private static readonly Color Amber = new(1.0f, 0.69f, 0.16f), AmberRim = new(0.42f, 0.24f, 0.05f);
    private static readonly Color NezFill = new(0.42f, 0.53f, 0.7f), NezRim = new(0.1f, 0.16f, 0.36f);
    private static readonly Color NoneFill = new(0.6f, 0.6f, 0.58f), NoneRim = new(0.3f, 0.3f, 0.3f);
    private static readonly Color Ink = new(0.14f, 0.09f, 0.05f);
    private static StandardMaterial3D? _badgeMat;
    private static StandardMaterial3D BadgeMaterial => _badgeMat ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };
    private static readonly Dictionary<(Provider, Mood), Mesh> _badges = new();

    private void Badge(Walker w, Camera3D cam, float dt)
    {
        w.Recheck -= dt;
        if (w.Recheck <= 0 || w.Shown is null)
        {
            w.Recheck = 0.5f;
            var now = (PeopleRules.ProviderOf(_sim.State, _content, w.Home.Lot), PeopleRules.MoodOf(_sim.State, _content, w.Home.Lot));
            if (w.Shown != now) { w.Shown = now; w.Disc.Mesh = BadgeMesh(now.Item1, now.Item2); }
        }
        // turned to the camera, and a little bigger further off so it stays readable down the street
        var top = w.Body.GlobalTransform * new Vector3(0, 1.0f, 0);
        float s = Mathf.Clamp(cam.GlobalPosition.DistanceTo(top) / 10f, 0.8f, 2.2f);
        var up = w.Body.GlobalBasis.Y.Normalized();
        w.Disc.GlobalTransform = new Transform3D(cam.GlobalBasis.Orthonormalized().Scaled(Vector3.One * s), w.Body.GlobalPosition + up * (2.02f + 0.16f * s));
    }

    private static Mesh BadgeMesh(Provider p, Mood m)
    {
        if (_badges.TryGetValue((p, m), out var made)) return made;
        var (fill, rim) = p switch { Provider.Yours => (Amber, AmberRim), Provider.NezTel => (NezFill, NezRim), _ => (NoneFill, NoneRim) };
        var lp = new LowPoly();
        Disc(lp, new Vector3(0, 0, -0.01f), 0.16f, rim);
        Disc(lp, Vector3.Zero, 0.132f, fill);
        // the face: two eyes and a mouth, smiling, level or turned down
        foreach (float x in new[] { -0.046f, 0.046f }) lp.Box(new Vector3(x, 0.034f, 0.01f), new Vector3(0.028f, 0.04f, 0.004f), Ink);
        for (int i = -2; i <= 2; i++)
        {
            float y = m switch { Mood.Happy => -0.064f + 0.0075f * i * i, Mood.Fed => -0.04f - 0.0075f * i * i, _ => -0.052f };
            lp.Box(new Vector3(i * 0.021f, y, 0.01f), new Vector3(0.026f, 0.019f, 0.004f), Ink);
        }
        return _badges[(p, m)] = lp.Build();
    }

    private static void Disc(LowPoly lp, Vector3 c, float r, Color col, int sides = 20)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.Tau * i / sides, a1 = Mathf.Tau * (i + 1) / sides;
            lp.Tri(c, c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * r, c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * r, col, Vector3.Back);
        }
    }
}
