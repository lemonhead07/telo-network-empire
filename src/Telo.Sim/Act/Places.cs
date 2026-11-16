using Telo.Sim.Build;
using Telo.Sim.Content;
using Telo.Sim.Planet;
using Telo.Sim.Town;

namespace Telo.Sim.Act;

/// <summary>
/// Places an actor can be, and how long it takes to get between them. Distances come from the planet, times from lived time:
/// a real second is three game minutes (a day is eight real minutes), so a walk costs what it costs the player.
/// </summary>
public static class Places
{
    public const string Room = "room";
    /// <summary>Game minutes per real second (1440 minutes in 480 seconds).</summary>
    public const double MinutesPerSecond = 3;
    public const double WalkMps = 2.8;
    public static double DriveMps => Vehicles.All["van"].SpeedMps;
    /// <summary>Further than this, an actor with empty hands takes the van.</summary>
    public const double DriveOverM = 25;

    public static string Pole(string premiseId, int index) => $"pole:{premiseId}:{index}";
    public static string Project(string projectId) => $"proj:{projectId}";
    public static bool IsProject(string place, out string projectId)
    {
        projectId = place.StartsWith("proj:") ? place[5..] : "";
        return projectId.Length > 0;
    }

    public static bool IsPole(string place, out string premiseId, out int index)
    {
        premiseId = ""; index = -1;
        if (!place.StartsWith("pole:")) return false;
        var parts = place.Split(':');
        premiseId = parts[1];
        return int.TryParse(parts[2], out index);
    }

    /// <summary>The direction (unit vector from the planet's centre) of a place.</summary>
    public static V3 Dir(PlanetGen gen, ContentDb c, string place)
    {
        if (place == Room) return Offset(gen, gen.Town, 0, 0);
        if (c.Lots.TryGetValue(place, out var lot)) return Offset(gen, gen.SiteById(lot.Site) ?? gen.Town, lot.East, lot.South);
        if (IsPole(place, out var pid, out int k) && c.Premises.TryGetValue(pid, out var p))
        {
            var sites = LineRules.PoleSites(gen, p);
            if (sites.Count > 0) return sites[Math.Clamp(k, 0, sites.Count - 1)].Normalized();
        }
        if (IsProject(place, out var proj) && c.Project(proj) is { } pd) return Offset(gen, gen.SiteById(pd.Site) ?? gen.Town, pd.East, pd.South);
        return gen.Town.Dir;
    }

    /// <summary>A point east/south of a site's centre (the same frame the game builds the town in).</summary>
    public static V3 Offset(PlanetGen gen, Site site, double east, double south)
    {
        var c = site.Dir.Normalized();
        var e = new V3(0, 1, 0).Cross(c).Normalized();
        var n = c.Cross(e);
        double r = gen.RadiusM + site.HeightM;
        return (c * r + e * east - n * south).Normalized();
    }

    public static double DistanceM(PlanetGen gen, ContentDb c, string a, string b) =>
        a == b ? 0 : gen.SurfaceDistance(Dir(gen, c, a), Dir(gen, c, b));

    public static int WalkMinutes(double metres) => Math.Max(1, (int)Math.Ceiling(metres / WalkMps * MinutesPerSecond));
    /// <summary>Driving, plus a minute to get in and out.</summary>
    public static int DriveMinutes(double metres, string vehicle = "van") =>
        1 + Math.Max(1, (int)Math.Ceiling(metres / Vehicles.All[vehicle].SpeedMps * MinutesPerSecond));
}

/// <summary>Ways across the planet, as data (AGES.md §6). Only the van exists yet; the rest are named so nothing assumes a van.</summary>
public sealed record VehicleDef(string Id, string Name, double SpeedMps, bool Road, bool Water, bool Air);

public static class Vehicles
{
    public static readonly IReadOnlyDictionary<string, VehicleDef> All = new Dictionary<string, VehicleDef>
    {
        ["van"] = new("van", "service van", 15, Road: true, Water: false, Air: false),
    };
}

/// <summary>What a link is made of. Unused values cost nothing now and save a rewrite later (AGES.md §6).</summary>
public enum LinkMedium { Copper, PoleCable, Radio, Microwave, Fibre, SeaCable, Cell, Satellite }
