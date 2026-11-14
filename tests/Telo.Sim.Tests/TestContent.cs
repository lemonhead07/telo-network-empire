using Telo.Sim.Content;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

/// <summary>Loads the real shipping content from game/content so tests guard the data, not a copy of it.</summary>
public static class TestContent
{
    private static readonly Lazy<ContentDb> _db = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Telo.slnx"))) dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("Repo root not found");
        var contentDir = Path.Combine(dir.FullName, "game", "content");
        return ContentDb.FromJson(ContentDb.FileNames.ToDictionary(f => f, f => File.ReadAllText(Path.Combine(contentDir, f))));
    });

    public static ContentDb Db => _db.Value;
    /// <summary>Wire every waiting subscriber the proper way (read the tag, test the port, use a spare if it is bad), then throw the switch.</summary>
    public static void WireUp(Simulation sim)
    {
        var s = sim.State; var b = s.Switch;
        foreach (var p in Net.SwitchRules.Pending(s))
        {
            Net.SwitchRules.ReadTag(s, p.CustomerId);
            int port = Net.SwitchRules.TestPort(s, p.AssignedPort) ? p.AssignedPort
                : Net.SwitchRules.SparePorts.First(i => Net.SwitchRules.OnPort(s, i) is null);
            Net.SwitchRules.Jumper(s, p.CustomerId, port);
        }
        sim.ThrowSwitch();
    }

    public static Simulation NewGame(ulong seed = 42) => Simulation.NewGame(Db, seed);
}
