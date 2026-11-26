using Godot;
using Telo.Game.World;
using Telo.Sim.Planet;
using Telo.Sim.Town;

namespace Telo.Game;

/// <summary>
/// --shots=look (LOOK1): the outdoors in its light, for before-and-after pairs, and how fast it draws.
///   xvfb-run -a godot --path game -- --from=act6 --shots=look --shot=/abs/x.png [--graphics=low|medium|high] [--look=town,aerial]
/// Saves x-look-*.png (the town morning, midday and evening, Main Street at dusk, Wrenfold, Millbrook from the hill, the
/// hills from Highmoor, grass up close, the aerial, the orbit, Halden lit at night) and prints the frame rate at each view
/// and on average ("LOOK fps ..."). --from=act6 opens every village and lights the data centre; without it those are skipped.
/// </summary>
public partial class GameRoot
{
    private async Task LookShots(Func<string, Vector3, Vector3, Vector3?, Task> shot)
    {
        float R = (float)_planet.RadiusM;
        var times = new List<(string Name, double Fps)>();
        // a point standing on the real ground (not the flattened plane of a site), lift metres up
        Vector3 Ground(Site s, float east, float south, float lift)
        {
            var dir = TownBuilder.Place(_planet, s, east, south).Origin.Normalized();
            return dir * (R + Mathf.Max((float)_planet.Height(PlanetBuilder.ToSim(dir)), 0f) + lift);
        }
        // --look=a,b: only those views (quicker while tuning)
        var only = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--look="))?["--look=".Length..].Split(',');
        async Task Look(string name, float hour, Vector3 from, Vector3 to, Vector3? up = null)
        {
            if (only is not null && !only.Contains(name)) return;
            At(hour);
            await shot($"-look-{name}", from, to, up);
            // how fast this view draws: the time for a run of frames (the first few after a move are not counted)
            ulong t0 = Time.GetTicksUsec();
            await Frames(8);
            double fps = 8 / ((Time.GetTicksUsec() - t0) / 1e6);
            times.Add((name, fps));
            GD.Print($"LOOK {name} fps={fps:0.00}");
        }

        var town = _planet.Town;
        var hq = _town.Centre; var up = hq.Normalized();
        await Look("town", 10.5f, TownBuilder.Place(_planet, 22, 40, 0, 7).Origin, hq + up * 2);
        await Look("town-morning", 7f, TownBuilder.Place(_planet, 22, 40, 0, 7).Origin, hq + up * 2);
        await Look("town-evening", 19.5f, TownBuilder.Place(_planet, -62, 22, 0, 5).Origin, TownBuilder.Place(_planet, -20, 14, 0, 2).Origin);
        await Look("town-dusk", 21f, TownBuilder.Place(_planet, 22, 40, 0, 7).Origin, hq + up * 2);
        if (_planet.SiteById("wrenfold") is { } wf)
            await Look("wrenfold", 10.5f, TownBuilder.Place(_planet, wf, -10, 62, 0, 14).Origin, TownBuilder.Place(_planet, wf, 0, 4, 0, 2).Origin);
        if (_planet.SiteById("millbrook") is { } mb)
        {
            // the highest ground 140-320 m from the market that can still see it, looking down on it
            var market = TownBuilder.Place(_planet, mb, -10, -10, 0, 2).Origin;
            var space = GetWorld3D().DirectSpaceState;
            var best = TownBuilder.Place(_planet, mb, 120, 150, 0, 160).Origin; float top = float.MinValue;
            for (int r = 140; r <= 320; r += 30)
                for (int k = 0; k < 24; k++)
                {
                    var p = Ground(mb, r * Mathf.Cos(k * Mathf.Tau / 24), r * Mathf.Sin(k * Mathf.Tau / 24), 5);
                    if (p.Length() <= top) continue;
                    var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(p, market + market.Normalized() * 6, 1));
                    if (hit.Count == 0) (top, best) = (p.Length(), p);
                }
            await Look("millbrook-hill", 16f, best, market);
        }
        if (_planet.SiteById("highmoor") is { } hm)
        {
            var from = Ground(hm, 0, 0, 6);
            var toward = (hq - from); toward -= from.Normalized() * toward.Dot(from.Normalized());   // level, towards the town
            await Look("hills", 9f, from, from + toward.Normalized() * 400 - from.Normalized() * 60);
        }
        if (_planet.SiteById("wrenfold") is { } meadow)
        {
            // grass at your feet, in the meadows outside Wrenfold
            var from = Ground(meadow, 70, -90, 1.7f);
            await Look("grass", 11f, from, Ground(meadow, 70, -80, 0));
        }
        if (_planet.Roads.FirstOrDefault(r => r.Id.Contains("farm")) is { } road && road.Points.Count > 4)
        {
            // standing at the roadside on the way to the farm, looking along it: the verges and the land either side
            int k = road.Points.Count * 2 / 3;
            var a = PlanetBuilder.ToGodot(road.Points[k]); var b = PlanetBuilder.ToGodot(road.Points[k + 1]);
            var side = (b - a).Cross(a).Normalized();
            var from = (a * R + side * 4.5f).Normalized(); var to = (a + (b - a) * 6f).Normalized();
            float H(Vector3 d) => R + Mathf.Max((float)_planet.Height(PlanetBuilder.ToSim(d)), 0f);
            await Look("road", 9.5f, from * (H(from) + 1.7f), to * H(to));
        }
        await Look("aerial", 10.5f, TownBuilder.Place(_planet, 250, 500, 0, 420).Origin, hq);
        await Look("orbit", 12f, (up + _town.Hq.GlobalBasis.X * 0.5f + _town.Hq.GlobalBasis.Z * 0.35f).Normalized() * R * 3.3f, Vector3.Zero, Vector3.Up);
        // your room: the light indoors by day and at night (the lamps carry it)
        await Look("room", 11f, _town.Hq.ToGlobal(new Vector3(-2.4f, 1.75f, 2.1f)), _town.Hq.ToGlobal(new Vector3(1.2f, 0.7f, -1.9f)));
        await Look("room-night", 21.8f, _town.Hq.ToGlobal(new Vector3(-2.4f, 1.75f, 2.1f)), _town.Hq.ToGlobal(new Vector3(1.2f, 0.7f, -1.9f)));
        if (_dc is not null && _planet.SiteById("halden") is { } flats && Telo.Sim.Build.ProjectRules.Done(Sim.State, "proj-dc-lit"))
        {
            _ignoreHours = false;
            await Look("halden-night", 22.5f, _dc.ToGlobal(new Vector3(-27f, 12f, 36f)), _dc.ToGlobal(new Vector3(0, 2f, 0)));
            _ignoreHours = true;
        }
        GD.Print($"LOOK fps graphics={GameSettings.QualityName(GameSettings.Graphics)} avg={(times.Count > 0 ? times.Average(t => t.Fps) : 0):0.00} min={(times.Count > 0 ? times.Min(t => t.Fps) : 0):0.00} views={times.Count}");
    }

    /// <summary>
    /// --shots=roads: straight down on every place a road out of town meets a settlement, where its drawn asphalt starts
    /// (0.75 of the site's radius from the centre), so you can see whether it meets a street. Saves x-road-&lt;road&gt;-&lt;site&gt;.png
    /// and prints how far from the centre each starts. The check for the road work in docs/TOWNS_AND_ROADS.md.
    /// </summary>
    private async Task RoadShots(Func<string, Vector3, Vector3, Vector3?, Task> shot)
    {
        float R = (float)_planet.RadiusM;
        At(10.5f);
        foreach (var road in _planet.Roads)
            foreach (var site in _planet.Sites)
            {
                var c = PlanetBuilder.ToGodot(site.Dir);
                bool first = (PlanetBuilder.ToGodot(road.Points[0]) - c).Length() * R < 1, last = (PlanetBuilder.ToGodot(road.Points[^1]) - c).Length() * R < 1;
                if (!first && !last) continue;
                var pts = road.Points.Select(PlanetBuilder.ToGodot).ToList();
                if (last) pts.Reverse();
                var start = pts.First(p => (p - c).Length() * R >= site.RadiusM * 0.75f);
                var mid = (start * 0.6f + c * 0.4f).Normalized();   // between the asphalt's start and the middle of the place
                float h = R + Mathf.Max((float)_planet.Height(PlanetBuilder.ToSim(mid)), 0f);
                var north = (TownBuilder.Place(_planet, site, 0, -10).Origin - TownBuilder.Place(_planet, site, 0, 0).Origin).Normalized();
                GD.Print($"ROAD {road.Id} at {site.Id}: asphalt starts {(start - c).Length() * R:0} m from the centre");
                await shot($"-road-{road.Id["road-".Length..]}-{site.Id}", mid * (h + 110f), mid * h, north);
            }
    }

    /// <summary>Sets the clock (and so the sun) to this hour for a picture.</summary>
    private void At(float hour) { _dayClock = DayLengthSeconds * (hour - 6f) * 60f / SleepRules.Bedtime; _slowTick = 0; }
}
