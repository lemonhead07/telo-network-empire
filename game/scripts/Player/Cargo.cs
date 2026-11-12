using Godot;
using Telo.Game.World;
using Telo.Sim.Town;

namespace Telo.Game.Player;

/// <summary>A piece of kit that rides on the van and is carried by hand to the job. One thing in your hands at a time.</summary>
public partial class Carryable : Interactable
{
    public enum Place { Van, Held, Ground }

    public Tools Tool { get; init; }
    public string Label { get; init; } = "";
    public bool TwoHanded { get; init; }
    public Transform3D VanSlot { get; init; }
    public Transform3D HeldPose { get; init; }
    /// <summary>How it sits when put down, relative to the spot in front of your feet.</summary>
    public Transform3D GroundPose { get; init; } = Transform3D.Identity;
    public Place Where { get; set; } = Place.Van;
}

/// <summary>
/// The van's kit: cable drum, ladder, toolbox and a boxed modem. Pick up with E, put down with G, put back by using the van.
/// Kit counts as "on site" when it is in your hands or lying within a few metres of you; the sim decides what each job needs.
/// Not saved: after a load everything is back on the van.
/// </summary>
public sealed class Cargo
{
    public const float SiteRadius = 6f, TwoHandedPace = 0.6f;

    private readonly Van _van;
    private readonly FirstPersonController _player;
    private readonly Node3D _world;
    private readonly Func<int> _modemsSpare;
    public List<Carryable> Items { get; } = new();
    public Carryable? Held { get; private set; }
    public event Action<Carryable>? PickedUp;

    public static string Name(Tools t) => t switch
    {
        Tools.CableDrum => "cable drum", Tools.Ladder => "ladder", Tools.Toolbox => "toolbox", Tools.ModemBox => "boxed modem", Tools.Pole => "pole", _ => "kit",
    };

    /// <summary>"the cable drum and the ladder" for a set of missing kit.</summary>
    public static string Names(Tools set)
    {
        var names = new[] { Tools.Pole, Tools.CableDrum, Tools.Ladder, Tools.Toolbox, Tools.ModemBox }.Where(t => (set & t) != 0).Select(t => "the " + Name(t)).ToList();
        return names.Count <= 1 ? string.Concat(names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
    }

    public Cargo(Van van, FirstPersonController player, Node3D world, Func<int> modemsSpare)
    {
        _van = van; _player = player; _world = world; _modemsSpare = modemsSpare;
        var wood = new Color(0.72f, 0.58f, 0.36f); var dark = new Color(0.1f, 0.1f, 0.11f);

        // ladder: modelled standing up, foot at the origin
        var ladder = new LowPoly().Box(new Vector3(-0.2f, 1.5f, 0), new Vector3(0.05f, 3f, 0.05f), wood).Box(new Vector3(0.2f, 1.5f, 0), new Vector3(0.05f, 3f, 0.05f), wood);
        for (float y = 0.3f; y < 3f; y += 0.3f) ladder.Box(new Vector3(0, y, 0), new Vector3(0.4f, 0.03f, 0.04f), wood * 0.9f);
        Add(Tools.Ladder, true, ladder, new Vector3(0.6f, 3f, 0.3f), new Vector3(0, 1.5f, 0),
            van: new Transform3D(new Basis(Vector3.Right, -Mathf.Pi / 2), new Vector3(-0.3f, 2.1f, 1.8f)),
            held: new Transform3D(new Basis(Vector3.Right, -0.25f), new Vector3(0.55f, 0.15f, -0.35f)),
            ground: new Transform3D(new Basis(Vector3.Right, -0.2f), Vector3.Zero));

        // a line pole: there are plenty on the van; planting one puts the next on the rack
        var pole = new LowPoly().Cylinder(Vector3.Zero, 0.12f, 0.09f, 6f, 6, new Color(0.33f, 0.24f, 0.16f));
        Add(Tools.Pole, true, pole, new Vector3(0.4f, 6f, 0.4f), new Vector3(0, 3f, 0),
            van: new Transform3D(new Basis(Vector3.Right, -Mathf.Pi / 2), new Vector3(0.4f, 2.16f, 3.1f)),
            held: new Transform3D(new Basis(Vector3.Right, -Mathf.Pi / 2 + 0.12f), new Vector3(0.42f, 1.4f, 2.4f)),
            ground: new Transform3D(new Basis(Vector3.Right, -Mathf.Pi / 2), new Vector3(0, 0.12f, 0.5f)));

        var drum = new LowPoly().Cylinder(new Vector3(0, 0, 0), 0.24f, 0.24f, 0.04f, 8, wood).Cylinder(new Vector3(0, 0.04f, 0), 0.15f, 0.15f, 0.26f, 8, dark)
            .Cylinder(new Vector3(0, 0.3f, 0), 0.24f, 0.24f, 0.04f, 8, wood);
        Add(Tools.CableDrum, true, drum, new Vector3(0.52f, 0.4f, 0.52f), new Vector3(0, 0.17f, 0),
            van: new Transform3D(Basis.Identity, new Vector3(-0.55f, 0.55f, 2.45f)), held: new Transform3D(Basis.Identity, new Vector3(0.3f, 0.75f, -0.6f)));

        var box = new LowPoly().Box(new Vector3(0, 0.11f, 0), new Vector3(0.46f, 0.22f, 0.2f), new Color(0.7f, 0.16f, 0.12f))
            .Box(new Vector3(0, 0.25f, 0), new Vector3(0.2f, 0.04f, 0.04f), dark);
        Add(Tools.Toolbox, false, box, new Vector3(0.5f, 0.34f, 0.3f), new Vector3(0, 0.14f, 0),
            van: new Transform3D(Basis.Identity, new Vector3(0.05f, 0.55f, 2.45f)), held: new Transform3D(Basis.Identity, new Vector3(0.42f, 0.7f, -0.5f)));

        var modem = new LowPoly().Box(new Vector3(0, 0.07f, 0), new Vector3(0.36f, 0.14f, 0.28f), new Color(0.82f, 0.74f, 0.56f))
            .Box(new Vector3(0, 0.141f, 0), new Vector3(0.2f, 0.004f, 0.1f), new Color(0.2f, 0.3f, 0.5f));
        Add(Tools.ModemBox, false, modem, new Vector3(0.42f, 0.26f, 0.34f), new Vector3(0, 0.1f, 0),
            van: new Transform3D(Basis.Identity, new Vector3(0.6f, 0.55f, 2.45f)), held: new Transform3D(Basis.Identity, new Vector3(0.36f, 0.85f, -0.5f)));
        Refresh();
    }

    private void Add(Tools tool, bool twoHanded, LowPoly mesh, Vector3 grab, Vector3 grabAt, Transform3D van, Transform3D held, Transform3D? ground = null)
    {
        var item = new Carryable
        {
            Name = tool.ToString(), Tool = tool, Label = Name(tool), TwoHanded = twoHanded, VanSlot = van, HeldPose = held,
            GroundPose = ground ?? Transform3D.Identity, CollisionLayer = 2, CollisionMask = 0,
        };
        item.AddChild(mesh.Instance());
        item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = grab }, Position = grabAt });
        item.Prompt = () => Held is null ? $"Pick up the {item.Label}{(item.TwoHanded ? " (two hands: you walk slower)" : "")}" : $"Your hands are full ([G] puts down the {Held.Label})";
        item.CanUse = () => Held is null;
        item.OnUse = () => PickUp(item);
        _van.AddChild(item);
        item.Transform = van;
        Items.Add(item);
    }

    private static void Move(Carryable item, Node parent, Transform3D local, Carryable.Place where)
    {
        item.GetParent()?.RemoveChild(item);
        parent.AddChild(item);
        item.Transform = local;
        item.Where = where;
        item.CollisionLayer = where == Carryable.Place.Held ? 0u : 2u;
    }

    public void PickUp(Carryable item)
    {
        if (Held is not null || item.Where == Carryable.Place.Held) return;
        Move(item, _player, item.HeldPose, Carryable.Place.Held);
        Held = item;
        _player.Pace = item.TwoHanded ? TwoHandedPace : 1f;
        PickedUp?.Invoke(item);
    }

    /// <summary>Put what you are carrying down just in front of your feet.</summary>
    public void Drop()
    {
        if (Held is not { } item) return;
        var spot = _player.GlobalTransform;
        spot.Origin += _player.Forward * 0.9f;
        Move(item, _world, spot * item.GroundPose, Carryable.Place.Ground);
        Release();
    }

    public void Stow(Carryable item)
    {
        if (Held == item) Release();
        Move(item, _van, item.VanSlot, Carryable.Place.Van);
        Refresh();
    }

    public void StowHeld() { if (Held is { } h) Stow(h); }

    private void Release() { Held = null; _player.Pace = 1f; }

    /// <summary>Kit in your hands or lying within reach of where you stand.</summary>
    public Tools OnSite(Vector3 at)
    {
        var t = Held?.Tool ?? Tools.None;
        foreach (var i in Items)
            if (i.Where == Carryable.Place.Ground && (i.GlobalPosition - at).Length() <= SiteRadius) t |= i.Tool;
        return t;
    }

    /// <summary>The boxed modem on the van is only there while there is a modem in stock that isn't already in someone's house.</summary>
    public void Refresh()
    {
        foreach (var i in Items.Where(i => i.Tool == Tools.ModemBox && i.Where == Carryable.Place.Van))
        {
            bool have = _modemsSpare() > 0;
            i.Visible = have;
            i.CollisionLayer = have ? 2u : 0u;
        }
    }
}
