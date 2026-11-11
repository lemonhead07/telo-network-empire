namespace Telo.Sim.Tests;

/// <summary>Guards the architecture: the simulation must stay engine-free and deterministic.</summary>
public class PurityTests
{
    private static readonly string[] Banned = { "using Godot", "System.Random", "new Random(", "DateTime.Now", "DateTime.UtcNow", "Environment.TickCount", "File.", "Directory." };

    [Fact]
    public void SimSourceHasNoEngineIoOrNondeterminism()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Telo.slnx"))) dir = dir.Parent;
        var simDir = Path.Combine(dir!.FullName, "src", "Telo.Sim");
        var offences = Directory.EnumerateFiles(simDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => !x.line.TrimStart().StartsWith("//") && !x.line.TrimStart().StartsWith("///"))
            .Where(x => Banned.Any(b => x.line.Contains(b)))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}: {x.line.Trim()}")
            .ToList();
        Assert.True(offences.Count == 0, "Impure sim code:\n" + string.Join("\n", offences));
    }
}
