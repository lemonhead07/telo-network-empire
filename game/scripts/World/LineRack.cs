using Godot;

namespace Telo.Game.World;

/// <summary>
/// The Board's lines in, on the wall beside the frame: one lamp per line (lit while someone is on it), empty sockets for
/// lines not yet fitted, and the tally sheet of engaged tones. It only shows what the sim's BoardRules worked out.
/// </summary>
public partial class LineRack : Node3D
{
    public const int Sockets = 12;
    private static readonly Color Empty = new(0.07f, 0.07f, 0.07f), Idle = new(0.1f, 0.3f, 0.12f), Busy = new(1f, 0.72f, 0.15f);

    private readonly StandardMaterial3D[] _lamps = new StandardMaterial3D[Sockets];
    private readonly Label3D _tally;
    public Interactable Slot { get; }

    public LineRack()
    {
        Name = "LineRack";
        AddChild(new LowPoly().Box(Vector3.Zero, new Vector3(0.62f, 1.0f, 0.05f), new Color(0.2f, 0.21f, 0.22f))
            .Box(new Vector3(0, -0.27f, 0.03f), new Vector3(0.5f, 0.36f, 0.01f), new Color(0.93f, 0.9f, 0.8f)).Instance());   // backplate and tally paper
        AddChild(new Label3D { Text = "THE BOARD\nlines in", Position = new Vector3(0, 0.4f, 0.03f), FontSize = 40, PixelSize = 0.0016f, Modulate = new Color(0.85f, 0.83f, 0.75f), OutlineSize = 0 });
        for (int i = 0; i < Sockets; i++)
        {
            _lamps[i] = new StandardMaterial3D { AlbedoColor = Empty, EmissionEnabled = true, Emission = Colors.Black };
            AddChild(new MeshInstance3D
            {
                Position = new Vector3(-0.225f + i % 6 * 0.09f, 0.24f - i / 6 * 0.1f, 0.035f), Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.03f) },
                MaterialOverride = _lamps[i], CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        _tally = new Label3D { Position = new Vector3(0, -0.27f, 0.04f), FontSize = 32, PixelSize = 0.0013f, Modulate = new Color(0.12f, 0.1f, 0.08f), OutlineSize = 0, Width = 360, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_tally);
        Slot = new Interactable();
        Slot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.62f, 1.0f, 0.1f) } });
        AddChild(Slot);
    }

    public void Show(int lines, int inUse, string tally)
    {
        for (int i = 0; i < Sockets; i++)
        {
            bool fitted = i < lines, busy = i < inUse;
            _lamps[i].AlbedoColor = !fitted ? Empty : busy ? Busy : Idle;
            _lamps[i].Emission = busy ? Busy : Colors.Black;
        }
        _tally.Text = tally;
    }
}
