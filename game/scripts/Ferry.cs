using Godot;
using Telo.Game.World;
using Telo.Sim.Act;
using Telo.Sim.Planet;

namespace Telo.Game;

/// <summary>
/// The Farland ferry (EA3b): a slipway at each end of the crossing, with a ticket hut, bollards, a sign and the boat at the
/// slip. Hold E at the hut's window to sail: the rules (<see cref="Ferries"/>) say whether it's running and move the clock on
/// by the crossing; this fades out, puts you (and the van, if it's on the slipway) at the other quay, and fades back in.
/// Placeholders built from boxes; nothing here decides anything.
/// </summary>
public partial class GameRoot
{
    private sealed class QuayView
    {
        public required Quay Quay { get; init; }
        public required Quay Other { get; init; }
        public required Crossing Crossing { get; init; }
        public required Node3D Node { get; init; }
        public required Interactable Hut { get; init; }
        public required Node3D Boat { get; init; }
        /// <summary>Where you step off the boat, where the van rolls off to (on the road above the slip), and the way inland.</summary>
        public Vector3 Arrive { get; init; }
        public Vector3 Park { get; init; }
        public Vector3 Inland { get; init; }
        /// <summary>How far the jetty runs out from the road's end.</summary>
        public float Length { get; init; }
    }

    private readonly List<QuayView> _quayViews = new();
    private ColorRect? _fade;
    private bool _sailing;
    /// <summary>Screenshots show the boat at the slip whatever the act.</summary>
    private bool _showFerry;
    /// <summary>How far up the road from the jetty the van can be left and still go aboard.</summary>
    private const float VanAboardM = 30f;

    private void BuildQuays(PhysicsDirectSpaceState3D space)
    {
        float R = (float)_planet.RadiusM;
        Vector3 Ground(Vector3 dir)
        {
            dir = dir.Normalized();
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(dir * (R + 400), dir * (R - 100), 1));
            return hit.Count > 0 ? (Vector3)hit["position"] : dir * (R + Mathf.Max(0f, (float)_planet.Height(PlanetBuilder.ToSim(dir))));
        }
        foreach (var x in _planet.Crossings)
            foreach (var (q, other) in new[] { (x.From, x.To), (x.To, x.From) })
            {
                // the slip carries on the line of the road coming down to it, out into the water
                var top = Ground(PlanetBuilder.ToGodot(q.Dir));
                var up = top.Normalized();
                var road = _planet.Roads.FirstOrDefault(r => (r.Points[^1] - q.Dir).Length * _planet.RadiusM < 1);
                var sea = road is not null ? PlanetBuilder.ToGodot(road.Points[^1]) - PlanetBuilder.ToGodot(road.Points[^2]) : PlanetBuilder.ToGodot(other.Dir) - up;
                sea = (sea - up * sea.Dot(up)).Normalized();
                var basis = new Basis(up.Cross(sea).Normalized(), up, sea);   // +Z out to sea, +X to the right looking out
                var node = new Node3D { Name = q.Id, Transform = new Transform3D(basis, top) };
                AddChild(node);
                // a point on the ground at (right, out) metres from the top of the slip, in the quay's frame
                Transform3D At(float right, float outM, float yawDeg = 0, float lift = 0)
                {
                    var g = Ground(top + basis.X * right + basis.Z * outM);
                    return new Transform3D(new Basis(g.Normalized().Cross(sea).Normalized(), g.Normalized(), sea).Rotated(g.Normalized(), Mathf.DegToRad(yawDeg)), g + g.Normalized() * lift);
                }
                float seaLevel = R - top.Length();   // the water, in the quay's own height (negative: below the top of the slip)

                // the jetty: a concrete deck on piles from the road's end out over the beach to water deep enough for the boat
                float length = 10;
                for (float m = 4; m < 90; m += 2)
                    if (_planet.Height(PlanetBuilder.ToSim((top + sea * m).Normalized())) < -1.2) { length = Mathf.Max(10, m + 4); break; }
                    else length = m + 4;
                var concrete = new Color(0.62f, 0.6f, 0.56f);
                var jetty = new LowPoly().Box(new Vector3(0, -0.1f, length / 2), new Vector3(7f, 0.4f, length), concrete)
                    .Box(new Vector3(-3.35f, 0.2f, length / 2), new Vector3(0.3f, 0.25f, length), concrete * 0.9f)   // kerbs
                    .Box(new Vector3(3.35f, 0.2f, length / 2), new Vector3(0.3f, 0.25f, length), concrete * 0.9f);
                for (float z = 3; z < length; z += 5)
                    foreach (float px in new[] { -3f, 3f })
                        jetty.Cylinder(new Vector3(px, seaLevel - 4f, z), 0.28f, 0.28f, 4f - seaLevel - 0.3f, 6, new Color(0.3f, 0.26f, 0.22f));   // piles
                jetty.Box(new Vector3(0, seaLevel + 0.02f, length - 0.5f), new Vector3(7.2f, 0.6f, 1.2f), new Color(0.36f, 0.44f, 0.3f));   // weed at the waterline
                node.AddChild(jetty.Instance("Jetty"));

                // bollards along the jetty and at its head
                var bollard = new LowPoly().Cylinder(Vector3.Zero, 0.24f, 0.2f, 0.75f, 8, new Color(0.14f, 0.14f, 0.15f))
                    .Cylinder(new Vector3(0, 0.75f, 0), 0.3f, 0.3f, 0.08f, 8, new Color(0.14f, 0.14f, 0.15f)).Build();
                for (float z = 2; z < length; z += 9)
                    foreach (float bx in new[] { -3f, 3f })
                        node.AddChild(new MeshInstance3D { Mesh = bollard, Position = new Vector3(bx, 0.1f, z) });

                // the ticket hut beside the road, its window facing the road
                var hut = new StaticBody3D { Name = "TicketHut", Transform = At(-8.5f, -8f, 90f) };
                var white = new Color(0.9f, 0.88f, 0.82f);
                hut.AddChild(new LowPoly().Box(new Vector3(0, 1.3f, 0), new Vector3(2.6f, 2.6f, 2.2f), white)
                    .GableRoof(new Vector3(0, 2.6f, 0), 2.9f, 2.5f, 0.7f, new Color(0.55f, 0.16f, 0.14f))
                    .Box(new Vector3(0, 1.45f, 1.11f), new Vector3(1.3f, 0.8f, 0.04f), new Color(0.2f, 0.28f, 0.34f))    // the window
                    .Box(new Vector3(0, 1.0f, 1.25f), new Vector3(1.5f, 0.08f, 0.3f), new Color(0.45f, 0.32f, 0.2f))     // its counter
                    .Box(new Vector3(0, 0.08f, 0), new Vector3(2.8f, 0.16f, 2.4f), new Color(0.5f, 0.5f, 0.48f)).Instance());
                hut.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.6f, 2.6f, 2.2f) }, Position = new Vector3(0, 1.3f, 0) });
                hut.AddChild(new Label3D
                {
                    Text = "FERRY TICKETS", Position = new Vector3(0, 2.2f, 1.13f), FontSize = 64, PixelSize = 0.25f / 64f,
                    Modulate = new Color(0.12f, 0.1f, 0.08f), OutlineSize = 0,
                });
                AddChild(hut);
                var window = new Interactable { Name = "FerryWindow", Position = new Vector3(0, 1.3f, 1.3f), CollisionLayer = 2, CollisionMask = 0 };
                window.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.6f, 1.4f, 0.5f) } });
                hut.AddChild(window);

                // a sign for drivers coming down the road
                var sign = new Node3D { Name = "FerrySign", Transform = At(6.8f, -11f, 180f) };
                sign.AddChild(new LowPoly().Box(new Vector3(-1.6f, 0.9f, 0), new Vector3(0.1f, 1.8f, 0.1f), new Color(0.35f, 0.27f, 0.2f))
                    .Box(new Vector3(1.6f, 0.9f, 0), new Vector3(0.1f, 1.8f, 0.1f), new Color(0.35f, 0.27f, 0.2f))
                    .Box(new Vector3(0, 1.75f, 0), new Vector3(3.7f, 1.1f, 0.06f), new Color(0.16f, 0.3f, 0.5f)).Instance());
                sign.AddChild(new Label3D
                {
                    Text = $"FERRY TO {(other.Land == PlanetGen.Mainland ? other.Name : _planet.LandName(other.Land)).ToUpperInvariant()}\n{Ferries.Minutes(x)} minutes. Vans welcome",
                    Position = new Vector3(0, 1.75f, 0.04f), FontSize = 48, PixelSize = 0.0042f, Modulate = new Color(0.95f, 0.95f, 0.9f), OutlineSize = 0,
                });
                AddChild(sign);

                // the boat: a little roll-on roll-off ferry lying off the jetty's head, bow door down onto it
                var boat = new Node3D { Name = "Ferry", Position = new Vector3(0, seaLevel, length + 12.6f) };
                var hull = new Color(0.17f, 0.24f, 0.33f);
                boat.AddChild(new LowPoly()
                    .Box(new Vector3(0, -0.2f, 0), new Vector3(9f, 2.2f, 24f), hull)                                  // hull
                    .Box(new Vector3(0, -0.95f, 0), new Vector3(9.05f, 0.5f, 24.05f), new Color(0.6f, 0.16f, 0.13f))  // boot-top
                    .Box(new Vector3(0, 0.95f, 0), new Vector3(9.2f, 0.12f, 24.2f), new Color(0.85f, 0.84f, 0.8f))    // rubbing strake
                    .Box(new Vector3(-4.3f, 1.5f, 0), new Vector3(0.3f, 1.0f, 22f), hull)                              // bulwarks
                    .Box(new Vector3(4.3f, 1.5f, 0), new Vector3(0.3f, 1.0f, 22f), hull)
                    .Box(new Vector3(3.2f, 3.0f, 6.5f), new Vector3(2.4f, 3.6f, 7f), new Color(0.93f, 0.92f, 0.88f))   // the wheelhouse, to one side
                    .Box(new Vector3(3.2f, 4.2f, 4.0f), new Vector3(2.45f, 0.6f, 1.6f), new Color(0.18f, 0.22f, 0.27f))
                    .Cylinder(new Vector3(3.2f, 4.8f, 8.5f), 0.45f, 0.4f, 1.8f, 8, new Color(0.75f, 0.18f, 0.14f))     // funnel
                    .Cylinder(new Vector3(3.2f, 6.6f, 8.5f), 0.41f, 0.41f, 0.35f, 8, new Color(0.1f, 0.1f, 0.1f))
                    .Instance("Hull"));
                var door = new LowPoly().Box(new Vector3(0, 0, -2.1f), new Vector3(6.5f, 0.22f, 4.2f), new Color(0.38f, 0.4f, 0.42f)).Instance("BowDoor");
                door.Position = new Vector3(0, 0.95f, -12f);   // hinged at the bow, its far edge resting on the jetty
                door.Rotation = new Vector3(Mathf.Atan2(0.1f - (seaLevel + 0.95f), 4.2f), 0, 0);
                boat.AddChild(door);
                boat.AddChild(new Label3D
                {
                    Text = "FARLAND FERRY", Position = new Vector3(-4.62f, 0.3f, 2f), RotationDegrees = new Vector3(0, -90, 0), FontSize = 64,
                    PixelSize = 0.012f, Modulate = new Color(0.95f, 0.95f, 0.92f), OutlineSize = 0,
                });
                node.AddChild(boat);

                var view = new QuayView
                {
                    Quay = q, Other = other, Crossing = x, Node = node, Hut = window, Boat = boat, Length = length,
                    Arrive = At(-3.2f, -7f, 0, 0.6f).Origin, Park = At(1.6f, -13f, 0, 0.4f).Origin, Inland = -sea,
                };
                _quayViews.Add(view);
                window.Prompt = () => Ferries.WhyNot(Sim.State, _planet, x) is { } why ? $"The {Ferries.Boat(x).Name}: {why}"
                    : _sailing ? "All aboard"
                    : Ferries.Label(x, other) + (VanOnSlip(view) ? ", you and the van" : ", on foot (the van stays here)");
                window.CanUse = () => !_sailing && Ferries.WhyNot(Sim.State, _planet, x) is null;
                window.HoldSeconds = () => Ferries.WhyNot(Sim.State, _planet, x) is null ? Hold(6) : 0f;   // a ticket, and the van up the bow door
                window.OnUse = () => _ = Sail(view);
            }
        SyncQuays();
    }

    /// <summary>The van left on the jetty, or on the road just above it: it goes aboard with you.</summary>
    private bool VanOnSlip(QuayView v)
    {
        if (_van.Driven) return false;
        var l = v.Node.ToLocal(_van.GlobalPosition);
        return l.Z > -VanAboardM && l.Z < v.Length + 2 && Mathf.Abs(l.X) < 14;
    }

    /// <summary>The boats are laid up until the ferry runs (Act 3).</summary>
    private void SyncQuays()
    {
        foreach (var v in _quayViews) v.Boat.Visible = _showFerry || Ferries.WhyNot(Sim.State, _planet, v.Crossing) is null;
    }

    /// <summary>Sail from this quay to the other one: the rules move the clock on by the crossing; you (and the van) land across the water.</summary>
    private async Task Sail(QuayView from)
    {
        if (_sailing) return;
        _sailing = true;   // before anything waits, so whoever asked (the autoplay) sees we're under way
        var to = _quayViews.First(v => v.Crossing == from.Crossing && v.Quay == from.Other);
        bool van = VanOnSlip(from);
        if (_van.Driven) ExitVan();
        Telo.Game.Audio.Sfx.Play("ferry.horn", from.Boat.GlobalPosition);   // all aboard (EA7)
        await Fade(1f, 0.6f);
        if (Ferries.Sail(Sim, _planet, from.Crossing))
        {
            _minuteFrac = 0;
            if (van) _van.PlaceAt(to.Park, to.Inland);
            _player.PlaceAt(to.Arrive, to.Inland);
            _player.Camera.Rotation = Vector3.Zero;
            for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            RefreshIdleScreens();
            Telo.Game.Audio.Sfx.Play("ferry.horn", to.Boat.GlobalPosition, db: -4);   // tied up at the other side
        }
        await Fade(0f, 0.9f);
        _sailing = false;
        _pip.Once("ferry");
    }

    /// <summary>Screenshots (EA3b): Farland's harbour town from off its slipway, the ferry at Kestrel Point, and Farland across the water.</summary>
    private async Task FarlandShots(Func<string, Vector3, Vector3, Vector3?, Task> shot)
    {
        _showFerry = true; SyncQuays();
        // Farland's harbour from the water: the jetty and the boat in front, Quay Street up the slope behind
        if (_planet.SiteById("farland") is { } fl && _quayViews.FirstOrDefault(v => v.Quay.Land == fl.Id) is { } far)
        {
            await shot("-farland", far.Node.ToGlobal(new Vector3(-36f, 24f, far.Length + 42f)), TownBuilder.Place(_planet, fl, 0, -10, 0, 2).Origin, null);
            // Quay Street from the inland side, like the villages' pictures
            await shot("-farland-street", TownBuilder.Place(_planet, fl, -10, 62, 0, 14).Origin, TownBuilder.Place(_planet, fl, 0, 4, 0, 2).Origin, null);
        }
        if (_quayViews.FirstOrDefault(v => v.Quay.Land == PlanetGen.Mainland) is { } home)
        {
            // the slipway at Kestrel Point: the road's end, the ticket hut and the sign, the jetty and the boat
            await shot("-ferry", home.Node.ToGlobal(new Vector3(13f, 9f, -26f)), home.Node.ToGlobal(new Vector3(-3f, 0f, home.Length * 0.55f)), null);
            // across the water, from the air (on a planet this small, Farland is under the horizon from the beach)
            if (_planet.SiteById("farland") is { } across)
            {
                var mid = (home.Node.GlobalPosition + TownBuilder.Place(_planet, across, 0, 0).Origin) / 2;
                await shot("-farland-across", home.Node.ToGlobal(new Vector3(-60f, 260f, -170f)), mid.Normalized() * (float)_planet.RadiusM, null);
            }
        }
        _showFerry = false; SyncQuays();
    }

    /// <summary>A short fade to black and back (the crossing is lived, but not watched).</summary>
    private async Task Fade(float to, float seconds)
    {
        if (_fade is null)
        {
            var layer = new CanvasLayer { Layer = 50 };
            AddChild(layer);
            _fade = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
            layer.AddChild(_fade);
            _fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", to, seconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
