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
    public static Simulation NewGame(ulong seed = 42) => Simulation.NewGame(Db, seed);
}
