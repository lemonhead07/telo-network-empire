using Godot;

namespace Telo.Game.World;

/// <summary>
/// The Board's lines in, on the wall beside the switchboard: one lamp per line (lit while someone is on it), a card in each
/// fitted slot, an empty slot for the next, and the tally sheet of engaged tones. It only shows what the sim's BoardRules worked out.
/// The look is the `room.line-rack` model when there is one (its "lens-NN" lamps, "card-NN" cards and the "next-slot" work points),
/// else the old boxes.
/// </summary>
public partial class LineRack : Node3D
{
    public const int Sockets = 12;
    private static readonly Color Empty = new(0.07f, 0.07f, 0.07f), Idle = new(0.1f, 0.3f, 0.12f), Busy = new(1f, 0.72f, 0.15f);

    private readonly StandardMaterial3D[] _lamps = new StandardMaterial3D[Sockets];
    private readonly List<Node3D>[] _cards = new List<Node3D>[Sockets];
    private readonly Node3D? _nextSlot;
    private readonly Vector3 _nextSlotHome;
    private readonly Label3D _tally;
    public Interactable Slot { get; }
    /// <summary>The rack's model (null while it is the placeholder): its <c>wp-</c> points sit at the next empty slot.</summary>
    public Node3D? Model { get; }

    /// <summary>Where slot <paramref name="i"/> is on the rack (6 across, 2 down), as the model has them.</summary>
    public static Vector3 SlotAt(int i) => new(-0.225f + i % 6 * 0.09f, 0.24f - i / 6 * 0.1f, 0.035f);

    public LineRack()
    {
        Name = "LineRack";
        for (int i = 0; i < Sockets; i++)
        {
            _lamps[i] = new StandardMaterial3D { AlbedoColor = Empty, EmissionEnabled = true, Emission = Colors.Black };
            _cards[i] = new();
        }
        if (Props.HasModel("room.line-rack"))
        {
            Model = Props.Make("room.line-rack");
            AddChild(Model);
            for (int i = 0; i < Sockets; i++)
            {
                string n = $"{i + 1:00}";
                foreach (var lens in Props.Parts(Model, $"lens-{n}"))
                    if (lens is MeshInstance3D mi) { mi.MaterialOverride = _lamps[i]; mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
                _cards[i].AddRange(Props.Parts(Model, $"card-{n}"));
            }
            _nextSlot = Props.Parts(Model, "next-slot").FirstOrDefault();
            _nextSlotHome = _nextSlot?.Position ?? Vector3.Zero;
        }
        else
        {
            AddChild(new LowPoly().Box(Vector3.Zero, new Vector3(0.62f, 1.0f, 0.05f), new Color(0.2f, 0.21f, 0.22f))
                .Box(new Vector3(0, -0.27f, 0.03f), new Vector3(0.5f, 0.36f, 0.01f), new Color(0.93f, 0.9f, 0.8f)).Instance());   // backplate and tally paper
            for (int i = 0; i < Sockets; i++)
                AddChild(new MeshInstance3D
                {
                    Position = SlotAt(i), Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.03f) },
                    MaterialOverride = _lamps[i], CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
        }
        AddChild(new Label3D { Text = "THE BOARD\nlines in", Position = new Vector3(0, 0.4f, 0.03f), FontSize = 40, PixelSize = 0.0016f, Modulate = new Color(0.85f, 0.83f, 0.75f), OutlineSize = 0 });
        _tally = new Label3D { Position = new Vector3(0, -0.27f, 0.04f), FontSize = 32, PixelSize = 0.0013f, Modulate = new Color(0.12f, 0.1f, 0.08f), OutlineSize = 0, Width = 360, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_tally);
        Slot = new Interactable();
        Slot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.62f, 1.0f, 0.1f) } });
        AddChild(Slot);
        Show(0, 0, "");
    }

    public void Show(int lines, int inUse, string tally)
    {
        for (int i = 0; i < Sockets; i++)
        {
            bool fitted = i < lines, busy = i < inUse;
            _lamps[i].AlbedoColor = !fitted ? Empty : busy ? Busy : Idle;
            _lamps[i].Emission = busy ? Busy : Colors.Black;
            foreach (var card in _cards[i]) card.Visible = fitted;
        }
        // the work points follow the first empty slot (the last slot once the rack is full)
        if (_nextSlot is not null)
        {
            int next = Mathf.Clamp(lines, 0, Sockets - 1);
            var step = SlotAt(next) - SlotAt(0);
            _nextSlot.Position = _nextSlotHome + new Vector3(step.X, step.Y, 0);
        }
        _tally.Text = tally;
    }
}
