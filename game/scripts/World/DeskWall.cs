using Godot;
using Telo.Sim.Work;

namespace Telo.Game.World;

/// <summary>
/// The rail of job tickets on the wall of your room: one paper slip per open job. This is the day's work, read off the wall.
/// Take a slip to hand to a member of staff. It only shows the sim's tickets; what a ticket is and who may take it is decided there.
/// </summary>
public partial class TicketRail : Node3D
{
    public const int Slots = 8;
    private readonly (MeshInstance3D Paper, StandardMaterial3D Mat, Label3D Text, Interactable Hot)[] _slips = new (MeshInstance3D, StandardMaterial3D, Label3D, Interactable)[Slots];
    private readonly Label3D _more;
    private List<Ticket> _shown = new();

    public Func<Ticket, string> Prompt { get; set; } = t => t.Title;
    public Func<Ticket, bool> CanUse { get; set; } = _ => false;
    public Action<Ticket> Use { get; set; } = _ => { };

    private static Color Paper(TicketKind k) => k switch
    {
        TicketKind.Install => new Color(0.96f, 0.93f, 0.8f), TicketKind.Order => new Color(0.8f, 0.88f, 0.96f),
        TicketKind.Repair => new Color(0.97f, 0.8f, 0.78f), _ => new Color(0.82f, 0.93f, 0.8f),
    };

    public TicketRail()
    {
        Name = "TicketRail";
        AddChild(Props.Make("room.ticket-rail", () => new LowPoly().Box(Vector3.Zero, new Vector3(1.36f, 1.0f, 0.03f), new Color(0.62f, 0.47f, 0.3f))
            .Box(new Vector3(0, 0.36f, 0.025f), new Vector3(1.3f, 0.03f, 0.03f), new Color(0.3f, 0.3f, 0.32f)).Instance()));   // cork, a rail over each row of slips
        AddChild(new Label3D { Text = "JOB TICKETS", Position = new Vector3(0, 0.44f, 0.02f), FontSize = 40, PixelSize = 0.0016f, Modulate = new Color(0.14f, 0.1f, 0.06f), OutlineSize = 0 });
        for (int i = 0; i < Slots; i++)
        {
            var at = new Vector3(-0.33f + i % 2 * 0.66f, 0.24f - i / 2 * 0.21f, 0.03f);
            var mat = new StandardMaterial3D { AlbedoColor = Colors.White, Roughness = 0.9f };
            var paper = new MeshInstance3D { Position = at, Mesh = new BoxMesh { Size = new Vector3(0.6f, 0.18f, 0.006f) }, MaterialOverride = mat, Visible = false };
            var text = new Label3D { Position = at + new Vector3(0, 0, 0.006f), FontSize = 32, PixelSize = 0.0011f, Modulate = new Color(0.1f, 0.08f, 0.2f), OutlineSize = 0, Width = 520, AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
            var hot = new Interactable { Position = at, CollisionLayer = 0, CollisionMask = 0 };
            hot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.6f, 0.18f, 0.1f) } });
            int slot = i;
            hot.Prompt = () => slot < _shown.Count ? Prompt(_shown[slot]) : "";
            hot.CanUse = () => slot < _shown.Count && CanUse(_shown[slot]);
            hot.OnUse = () => { if (slot < _shown.Count && CanUse(_shown[slot])) Use(_shown[slot]); };
            AddChild(paper); AddChild(text); AddChild(hot);
            _slips[i] = (paper, mat, text, hot);
        }
        _more = new Label3D { Position = new Vector3(0, -0.46f, 0.02f), FontSize = 32, PixelSize = 0.0012f, Modulate = new Color(0.14f, 0.1f, 0.06f), OutlineSize = 0 };
        AddChild(_more);
    }

    /// <param name="line">What to write on each slip (who has it, how long it has waited).</param>
    public void Show(IReadOnlyList<Ticket> tickets, Func<Ticket, string> line)
    {
        _shown = tickets.Take(Slots).ToList();
        for (int i = 0; i < Slots; i++)
        {
            bool on = i < _shown.Count;
            var (paper, mat, text, hot) = _slips[i];
            paper.Visible = text.Visible = on;
            hot.CollisionLayer = on ? 2u : 0u;
            if (!on) continue;
            mat.AlbedoColor = Paper(_shown[i].Kind);
            text.Text = line(_shown[i]);
        }
        _more.Text = tickets.Count > Slots ? $"…and {tickets.Count - Slots} more in the tray" : tickets.Count == 0 ? "nothing waiting" : "";
    }
}

/// <summary>A tear-off calendar and the time, on the wall: today, pay day, rent day, who is off. Read it, don't click it.</summary>
public partial class WallCalendar : Node3D
{
    private readonly Label3D _text;

    public WallCalendar()
    {
        Name = "WallCalendar";
        AddChild(Props.Make("room.wall-calendar", () => new LowPoly().Box(Vector3.Zero, new Vector3(0.7f, 0.86f, 0.02f), new Color(0.95f, 0.93f, 0.86f))
            .Box(new Vector3(0, 0.37f, 0.012f), new Vector3(0.7f, 0.12f, 0.01f), new Color(0.6f, 0.14f, 0.12f)).Instance()));
        _text = new Label3D { Position = new Vector3(0, -0.04f, 0.014f), FontSize = 32, PixelSize = 0.00115f, Modulate = new Color(0.1f, 0.08f, 0.06f), OutlineSize = 0, Width = 560, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Left };
        AddChild(_text);
    }

    public void Show(string text) => _text.Text = text;
}

/// <summary>A placeholder person: a member of staff standing where they are waiting or working, with their name over their head.</summary>
public partial class StaffFigure : Node3D
{
    public Interactable Hot { get; }

    public StaffFigure(string name, Color overalls)
    {
        Name = "Staff";
        var skin = new Color(0.82f, 0.62f, 0.48f);
        string model = name.Contains("Tobi") ? "person.sysop" : "person.engineer";   // whoever runs the Board dresses for it
        if (Props.HasModel(model)) AddChild(Props.Make(model));
        else AddChild(new LowPoly()
            .Box(new Vector3(-0.1f, 0.42f, 0), new Vector3(0.16f, 0.84f, 0.2f), overalls * 0.8f).Box(new Vector3(0.1f, 0.42f, 0), new Vector3(0.16f, 0.84f, 0.2f), overalls * 0.8f)   // legs
            .Box(new Vector3(0, 1.14f, 0), new Vector3(0.46f, 0.62f, 0.26f), overalls)                                                                                     // body
            .Box(new Vector3(-0.3f, 1.1f, 0), new Vector3(0.12f, 0.58f, 0.14f), overalls).Box(new Vector3(0.3f, 1.1f, 0), new Vector3(0.12f, 0.58f, 0.14f), overalls)       // arms
            .Box(new Vector3(0, 1.6f, 0), new Vector3(0.24f, 0.26f, 0.24f), skin)                                                                                          // head
            .Box(new Vector3(0, 1.75f, 0.03f), new Vector3(0.27f, 0.07f, 0.32f), new Color(0.2f, 0.25f, 0.4f))                                                              // cap
            .Box(new Vector3(0.17f, 0.95f, 0.15f), new Vector3(0.2f, 0.26f, 0.03f), new Color(0.5f, 0.35f, 0.2f)).Instance());                                              // clipboard
        AddChild(new Label3D { Text = name, Position = new Vector3(0, 2.05f, 0), FontSize = 48, PixelSize = 0.0028f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color(0.98f, 0.95f, 0.85f), OutlineSize = 12 });
        Hot = new Interactable { Position = new Vector3(0, 0.95f, 0), CollisionLayer = 2, CollisionMask = 0 };
        Hot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.8f, 1.9f, 0.6f) } });
        AddChild(Hot);
    }
}

/// <summary>
/// The timesheet between the calendar and the bed (spine step 5): tomorrow, hour by hour. E fills it with what fits (a suggestion,
/// never a lock); R clears it. Jobs where nobody will be in show greyed with the reason. Placeholder: a printed sheet.
/// </summary>
public partial class Timesheet : Node3D
{
    private readonly Label3D _text;
    public Interactable Hot { get; }

    public Timesheet()
    {
        Name = "Timesheet";
        AddChild(Props.Make("room.timesheet", () => new LowPoly().Box(Vector3.Zero, new Vector3(0.62f, 0.86f, 0.02f), new Color(0.93f, 0.95f, 0.9f))
            .Box(new Vector3(0, 0.37f, 0.012f), new Vector3(0.62f, 0.1f, 0.01f), new Color(0.16f, 0.2f, 0.36f)).Instance()));
        AddChild(new Label3D { Text = "TIMESHEET", Position = new Vector3(0, 0.37f, 0.02f), FontSize = 32, PixelSize = 0.0014f, Modulate = new Color(0.93f, 0.95f, 0.9f), OutlineSize = 0 });
        _text = new Label3D { Position = new Vector3(-0.28f, -0.04f, 0.014f), FontSize = 32, PixelSize = 0.0009f, Modulate = new Color(0.1f, 0.1f, 0.18f), OutlineSize = 0, Width = 620, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Left };
        AddChild(_text);
        Hot = new Interactable { CollisionLayer = 2, CollisionMask = 0 };
        Hot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.62f, 0.86f, 0.1f) } });
        AddChild(Hot);
    }

    public void Show(string text) => _text.Text = text;
}

/// <summary>
/// The ad board on the right-hand wall (D37): the one lever of the booking loop. E puts up the next ad, R takes one down.
/// Shows what's running, what it costs a week, and bookings against what your people can do. Placeholder: a pinboard.
/// </summary>
public partial class AdBoard : Node3D
{
    private readonly Label3D _text;
    public Interactable Hot { get; }

    public AdBoard()
    {
        Name = "AdBoard";
        AddChild(Props.Make("room.ad-board", () => new LowPoly().Box(Vector3.Zero, new Vector3(0.8f, 0.6f, 0.03f), new Color(0.55f, 0.42f, 0.28f))
            .Box(new Vector3(-0.22f, 0.08f, 0.02f), new Vector3(0.22f, 0.22f, 0.005f), new Color(0.95f, 0.9f, 0.55f))
            .Box(new Vector3(0.2f, 0.08f, 0.02f), new Vector3(0.28f, 0.2f, 0.005f), new Color(0.92f, 0.92f, 0.88f)).Instance()));
        AddChild(new Label3D { Text = "ADVERTISING", Position = new Vector3(0, 0.25f, 0.025f), FontSize = 32, PixelSize = 0.0014f, Modulate = new Color(0.15f, 0.1f, 0.06f), OutlineSize = 0 });
        _text = new Label3D { Position = new Vector3(0, -0.2f, 0.03f), FontSize = 32, PixelSize = 0.001f, Modulate = new Color(0.12f, 0.1f, 0.08f), OutlineSize = 0, Width = 760, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_text);
        Hot = new Interactable { CollisionLayer = 2, CollisionMask = 0 };
        Hot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.8f, 0.6f, 0.1f) } });
        AddChild(Hot);
    }

    public void Show(string text) => _text.Text = text;
}
