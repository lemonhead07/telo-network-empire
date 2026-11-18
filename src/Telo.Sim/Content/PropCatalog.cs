using System.Text.Json;

namespace Telo.Sim.Content;

/// <summary>
/// One visible thing's brief (game/content/props.json, D30): what it is, how big, where its pivot and anchors are.
/// The same brief makes the placeholder box and, later, the Blender model, so both agree on size and attachment points.
/// </summary>
public sealed record PropDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public string What { get; init; } = "";
    /// <summary>Width, height, depth in metres (x, y, z).</summary>
    public float[] Size { get; init; } = Array.Empty<float>();
    public string Pivot { get; init; } = "";
    public int Tris { get; init; }
    public List<string> Colours { get; init; } = new();
    public Dictionary<string, float[]> Anchors { get; init; } = new();
    /// <summary>"box" (placeholder only) or "modelled" (a .glb exists in game/assets/models).</summary>
    public string Status { get; init; } = "box";
}

/// <summary>All prop briefs by id. Presentation data, so it is loaded on its own rather than through <see cref="ContentDb"/>.</summary>
public sealed class PropCatalog
{
    private sealed record File { public List<PropDef> Props { get; init; } = new(); }

    public IReadOnlyDictionary<string, PropDef> Props { get; }
    private readonly List<PropDef> _all;

    public PropCatalog(IEnumerable<PropDef> props)
    {
        _all = props.ToList();
        var errors = Validate();
        if (errors.Count > 0) throw new InvalidDataException("props.json invalid:\n" + string.Join("\n", errors));
        Props = _all.ToDictionary(p => p.Id);
    }

    public static PropCatalog FromJson(string text) =>
        new((JsonSerializer.Deserialize<File>(text, ContentDb.Json) ?? new File()).Props);

    public PropDef? Get(string id) => Props.TryGetValue(id, out var p) ? p : null;

    public List<string> Validate()
    {
        var e = new List<string>();
        foreach (var g in _all.GroupBy(p => p.Id).Where(g => g.Count() > 1)) e.Add($"prop {g.Key}: duplicate id");
        foreach (var p in _all)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(p.Id, @"^[a-z]+\.[a-z0-9-]+$")) e.Add($"prop {p.Id}: id should look like 'group.name'");
            if (p.Size.Length != 3 || p.Size.Any(s => s <= 0 || s > 100)) e.Add($"prop {p.Id}: size needs three positive metres");
            if (p.Status is not ("box" or "modelled")) e.Add($"prop {p.Id}: unknown status '{p.Status}'");
            foreach (var (name, a) in p.Anchors) if (a.Length != 3) e.Add($"prop {p.Id}: anchor '{name}' needs x, y, z");
        }
        return e;
    }
}
