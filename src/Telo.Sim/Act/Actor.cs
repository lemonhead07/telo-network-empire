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

    /// <summary>Kit in their hands or put down where they are standing.</summary>
    public Tools OnSite() => OnSiteOverride?.Invoke()
        ?? Kit.Where(k => k.Value == Place).Aggregate(Holding, (t, k) => t | k.Key);

    public static readonly Tools[] AllKit = { Tools.CableDrum, Tools.Ladder, Tools.Toolbox, Tools.ModemBox, Tools.Pole, Tools.LineCard };
}
