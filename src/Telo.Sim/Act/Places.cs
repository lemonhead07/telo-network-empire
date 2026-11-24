using Telo.Sim.Build;
using Telo.Sim.Content;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.World;

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

    /// <summary>The land a place stands on (<see cref="PlanetGen.Mainland"/>, or an island such as Farland).</summary>
    public static string LandOf(PlanetGen gen, ContentDb c, string place) => gen.LandOf(Dir(gen, c, place));

    /// <summary>
    /// The way from one place to another: metres over land, and the sea crossing on the way when they're on different land
    /// (quay to quay: drive or walk to the slipway, sail, drive or walk on from the other one).
    /// </summary>
    public static Route RouteOf(PlanetGen gen, ContentDb c, string a, string b)
    {
        if (a == b) return new(0, null, null);
        V3 da = Dir(gen, c, a), db = Dir(gen, c, b);
        string la = gen.LandOf(da), lb = gen.LandOf(db);
        if (la == lb || gen.CrossingBetween(la, lb) is not { } x || x.Ends(la) is not { } ends)
            return new(gen.SurfaceDistance(da, db), null, null);   // same land (or no boat yet: as the crow flies)
        return new(gen.SurfaceDistance(da, ends.Here.Dir) + gen.SurfaceDistance(ends.There.Dir, db), x, ends.There);
    }

    /// <summary>How far, all told: over land, plus the sea crossing.</summary>
    public static double DistanceM(PlanetGen gen, ContentDb c, string a, string b) => Metres(gen, RouteOf(gen, c, a, b));

    public static double Metres(PlanetGen gen, Route r) => r.By is { } x ? r.LandM + gen.SurfaceDistance(x.From.Dir, x.To.Dir) : r.LandM;

    /// <summary>How long it takes to get there: driving or walking the land, plus the crossing (a fixed sailing) if there is one.</summary>
    public static int TravelMinutes(PlanetGen gen, ContentDb c, string a, string b, bool drive, string vehicle = "van") =>
        Minutes(RouteOf(gen, c, a, b), drive, vehicle);

    public static int Minutes(Route r, bool drive, string vehicle = "van") =>
        (drive ? DriveMinutes(r.LandM, vehicle) : WalkMinutes(r.LandM)) + (r.By is { } x ? Ferries.Minutes(x) : 0);

    public static int WalkMinutes(double metres) => Math.Max(1, (int)Math.Ceiling(metres / WalkMps * MinutesPerSecond));
    /// <summary>Driving, plus a minute to get in and out.</summary>
    public static int DriveMinutes(double metres, string vehicle = "van") =>
        1 + Math.Max(1, (int)Math.Ceiling(metres / Vehicles.All[vehicle].SpeedMps * MinutesPerSecond));
}

/// <summary>The way between two places: metres over land, and the sea crossing on the way (with the quay you land at), if any.</summary>
public readonly record struct Route(double LandM, Crossing? By, Quay? LandAt);

/// <summary>
/// Ways across the planet, as data (AGES.md §6). The van on the roads, and the car ferry over to Farland (EA3b); the rest are
/// named as they come so nothing assumes a van. A boat keeps a timetable: <see cref="VehicleDef.FixedMinutes"/> is its
/// sailing, whatever the distance, and it runs from act <see cref="VehicleDef.FromAct"/>.
/// </summary>
public sealed record VehicleDef(string Id, string Name, double SpeedMps, bool Road, bool Water, bool Air, int FixedMinutes = 0, int FromAct = 0);

public static class Vehicles
{
    public static readonly IReadOnlyDictionary<string, VehicleDef> All = new Dictionary<string, VehicleDef>
    {
        ["van"] = new("van", "service van", 15, Road: true, Water: false, Air: false),
        // EA3b: a roll-on roll-off ferry across to Farland. Forty minutes, you and the van; it starts running in Act 3,
        // the year the ocean cable makes Farland your business (before that nobody on this side has a reason to cross)
        ["ferry"] = new("ferry", "Farland ferry", 6, Road: false, Water: true, Air: false, FixedMinutes: 40, FromAct: 3),
    };
}

/// <summary>
/// Sailing (EA3b). One rule for everyone: the player at the slipway, the script player and the autoplay on their way,
/// staff and Nez later. A crossing is lived time like a drive: the clock moves on by the sailing.
/// </summary>
public static class Ferries
{
    public static VehicleDef Boat(Crossing x) => Vehicles.All[x.Vehicle];
    public static int Minutes(Crossing x) => Boat(x).FixedMinutes;

    /// <summary>Why the boat won't take you today, or null when it will.</summary>
    public static string? WhyNot(GameState s, PlanetGen gen, Crossing x) =>
        s.ActIndex < Boat(x).FromAct
            ? $"no sailings yet: the {Boat(x).Name} is laid up until there's trade across the water (it runs from Act {Boat(x).FromAct})"
            : null;

    /// <summary>What the slipway offers: "Take the ferry to Farland harbour (40 min)".</summary>
    public static string Label(Crossing x, Quay to) => $"Take the ferry to {to.Name} ({Minutes(x)} min)";

    /// <summary>Sail across. False (nothing happens) when the boat isn't running.</summary>
    public static bool Sail(Simulation sim, PlanetGen gen, Crossing x)
    {
        if (WhyNot(sim.State, gen, x) is not null) return false;
        sim.AdvanceMinutes(Minutes(x));
        return true;
    }
}

/// <summary>What a link is made of. Unused values cost nothing now and save a rewrite later (AGES.md §6).</summary>
public enum LinkMedium { Copper, PoleCable, Radio, Microwave, Fibre, SeaCable, Cell, Satellite }
