using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Ledger;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Build;

/// <summary>
/// Building a pole line by hand to a Last Mile premise: plant poles along a road, then string the spans between them.
/// The first infrastructure job. Nothing is abstract: every pole is a place on the planet and a trip with the van.
/// Span i runs from pole i to pole i+1; the last span runs from the last pole to the building.
/// </summary>
public static class LineRules
{
    public const int PlantMinutes = 30, StringMinutes = 15;
    public const double SideOffsetM = 13;
    public const Tools PlantNeeds = Tools.Pole, StringNeeds = Tools.CableDrum | Tools.Ladder;

    /// <summary>Where the poles go: beside the road, evenly spaced, outside the settlements at either end. Empty if this premise has no line to build.</summary>
    public static IReadOnlyList<V3> PoleSites(PlanetGen gen, PremiseDef p)
    {
        var sites = new List<V3>();
        if (p.PoleLine is not { } def || gen.Roads.FirstOrDefault(r => r.Id == def.Road) is not { } road) return sites;
        var pts = road.Points;
        int step = Math.Max(1, (int)(def.SpacingM / PlanetGen.RoadSpacingM + 0.5));
        for (int i = 0; i < pts.Count; i += step)
        {
            var c = pts[i];
            if (gen.Sites.Any(s => (s.Dir - c).Length * gen.RadiusM < s.RadiusM * 0.75)) continue;
            var along = (pts[Math.Min(i + 1, pts.Count - 1)] - pts[Math.Max(i - 1, 0)]).Normalized();
            var side = along.Cross(c).Normalized();
            sites.Add((c * gen.RadiusM + side * SideOffsetM).Normalized());
        }
        return sites;
    }

    public static LineProgress ProgressOf(GameState s, string premiseId) =>
        s.Lines.TryGetValue(premiseId, out var l) ? l : new LineProgress();

    public static bool Complete(GameState s, PlanetGen gen, PremiseDef p) =>
        PoleSites(gen, p).Count is var n and > 0 && ProgressOf(s, p.Id).Strung >= n;

    public static bool CanPlant(GameState s, PlanetGen gen, PremiseDef p) =>
        p.PoleLine is { } def && ProgressOf(s, p.Id).Planted < PoleSites(gen, p).Count && s.Cash >= def.PoleCost;

    public static bool CanString(GameState s, PlanetGen gen, PremiseDef p)
    {
        int n = PoleSites(gen, p).Count; var l = ProgressOf(s, p.Id);
        return l.Strung < n && (l.Strung < l.Planted - 1 || l.Planted == n);
    }

    /// <summary>Plant the next pole in the row. Needs a pole from the van and the price of one.</summary>
    public static bool Plant(GameState s, PlanetGen gen, PremiseDef p, Tools onSite)
    {
        if (!CanPlant(s, gen, p) || (onSite & PlantNeeds) != PlantNeeds) return false;
        var l = Touch(s, p.Id);
        s.Cash -= p.PoleLine!.PoleCost;
        l.Planted++;
        return true;
    }

    /// <summary>String the next span. The last one reaches the building and connects them.</summary>
    public static bool String(GameState s, ContentDb c, PlanetGen gen, PremiseDef p, Tools onSite)
    {
        if (!CanString(s, gen, p) || (onSite & StringNeeds) != StringNeeds) return false;
        var l = Touch(s, p.Id);
        l.Strung++;
        if (l.Strung >= PoleSites(gen, p).Count)
        {
            s.Emit(EventKind.Milestone, $"The last span is up. {l.Planted} poles, every one of them yours. There is dial tone at {p.Name}.", p.Id);
            LedgerRules.Serve(s, c, p.Id, alreadyBuilt: true);
        }
        return true;
    }

    private static LineProgress Touch(GameState s, string premiseId)
    {
        if (!s.Lines.TryGetValue(premiseId, out var l)) s.Lines[premiseId] = l = new LineProgress();
        return l;
    }
}
