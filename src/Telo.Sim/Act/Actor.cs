using System.Text.Json.Serialization;
using Telo.Sim.Town;

namespace Telo.Sim.Act;

/// <summary>
/// Anyone who can act in the world: the player, a test, a member of staff, Nez. Plain data, saved with the game (v8).
/// Where they are, what is in their hands, where each piece of their kit is, and where their van is parked.
/// </summary>
public sealed class ActorState
{
    public string Id { get; set; } = "player";
    /// <summary>Whose side they are on: "player", "nez", a rival or a co-op (AGES.md §6). Assets they build carry it.</summary>
    public string Owner { get; set; } = "player";
    /// <summary>The region they work in (a site id). A manager can later be handed a whole region.</summary>
    public string Region { get; set; } = "town-start";
    /// <summary>The vehicle they drive (an id in <see cref="Vehicles"/>).</summary>
    public string Vehicle { get; set; } = "van";
    /// <summary>A place id (see <see cref="Places"/>): "room", a lot id, or "pole:&lt;premise&gt;:&lt;n&gt;".</summary>
    public string Place { get; set; } = Places.Room;
    /// <summary>Where the van is parked.</summary>
    public string VanAt { get; set; } = Places.Room;
    /// <summary>The one thing in their hands (kit is carried one piece at a time).</summary>
    public Tools Holding { get; set; }
    /// <summary>Kit put down somewhere, by tool. Kit not listed here is on the van.</summary>
    public Dictionary<Tools, string> Kit { get; set; } = new();

    /// <summary>The presentation can say what is to hand from the 3D world (kit lying within a few metres). Not saved.</summary>
    [JsonIgnore] public Func<Tools>? OnSiteOverride { get; set; }
    /// <summary>Automated checks ignore who is home. Not saved.</summary>
    [JsonIgnore] public bool IgnoreHours { get; set; }
    /// <summary>The job (ticket key) this actor has committed to, so it sees one thing through instead of dithering. Not saved.</summary>
    [JsonIgnore] public string? Focus { get; set; }

    /// <summary>Kit in their hands or put down where they are standing.</summary>
    public Tools OnSite() => OnSiteOverride?.Invoke()
        ?? Kit.Where(k => k.Value == Place).Aggregate(Holding, (t, k) => t | k.Key);

    public static readonly Tools[] AllKit = { Tools.CableDrum, Tools.Ladder, Tools.Toolbox, Tools.ModemBox, Tools.Pole, Tools.LineCard, Tools.Parts };
}
