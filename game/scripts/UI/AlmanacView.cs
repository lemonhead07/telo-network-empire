using Godot;
using Telo.Sim.Almanac;
using Telo.Sim.World;

namespace Telo.Game.UI;

/// <summary>PIP's corrupted handset. Research is recall: spend Archive to remember the future; too early and the world notices (Echo).</summary>
public partial class AlmanacView : ModalView
{
    public Simulation Sim { get; set; } = null!;

    public override void Rebuild()
    {
        Clear(Body); Clear(Footer);
        var s = Sim.State; var c = Sim.Content;
        Title.Text = $"PIP — ALMANAC     Archive {s.Archive}   Echo {s.Echo} ({AlmanacRules.Band(s.Echo)})";
        Text(Body, "Archive grows from the network you build: customers, staff, traffic. Recalling a page before the world announces it costs Echo.", 15);

        foreach (var e in c.Almanac.Values.OrderBy(e => AlmanacRules.PublicDate(s, c, e.Announcement).Day))
        {
            var status = s.Entries.GetValueOrDefault(e.Id);
            var box = new VBoxContainer();
            Body.AddChild(box);
            var pub = AlmanacRules.PublicDate(s, c, e.Announcement);
            switch (status)
            {
                case EntryStatus.Sealed:
                    Text(box, $"▓▓▓▓ {new string('▒', e.Title.Length)} ▓▓▓▓  (corrupted)");
                    break;
                case EntryStatus.Readable:
                    int years = AlmanacRules.YearsEarly(s, c, e);
                    Text(box, $"◆ {e.Title} — world learns of it {pub.Year}");
                    Text(box, e.Blurb, 15);
                    string when = years > 0 ? $"{years} yr early, +{AlmanacRules.EchoCost(s, c, e)} Echo" : "on time, no Echo";
                    Btn(box, $"Recall — {e.ArchiveCost} Archive ({when})", () => { AlmanacRules.Recall(s, c, e.Id); Rebuild(); }, s.Archive >= e.ArchiveCost);
                    break;
                case EntryStatus.Recalled:
                    Text(box, $"✓ {e.Title} — recalled");
                    break;
                case EntryStatus.Drifting:
                    Text(box, $"≈ {e.Title} — DRIFTING: your notes no longer match the world");
                    break;
            }
        }
        CloseButton();
    }
}
