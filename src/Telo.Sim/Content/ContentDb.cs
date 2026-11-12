using System.Text.Json;
using System.Text.Json.Serialization;

namespace Telo.Sim.Content;

/// <summary>All static game content. Loaded from JSON strings so the sim never touches the file system itself.</summary>
public sealed class ContentDb
{
    public IReadOnlyList<ActDef> Acts { get; }
    public IReadOnlyDictionary<string, AnnouncementDef> Announcements { get; }
    public IReadOnlyDictionary<string, AlmanacEntryDef> Almanac { get; }
    public IReadOnlyDictionary<string, WorkKindDef> Work { get; }
    public IReadOnlyDictionary<string, PremiseDef> Premises { get; }
    public IReadOnlyDictionary<string, LotDef> Lots { get; }
    public IReadOnlyDictionary<string, ProductDef> Products { get; }

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true,
    };

    public ContentDb(IEnumerable<ActDef> acts, IEnumerable<AnnouncementDef> announcements,
        IEnumerable<AlmanacEntryDef> almanac, IEnumerable<WorkKindDef> work, IEnumerable<PremiseDef> premises,
        IEnumerable<LotDef>? lots = null, IEnumerable<ProductDef>? products = null)
    {
        Acts = acts.OrderBy(a => a.Index).ToList();
        Announcements = announcements.ToDictionary(a => a.Id);
        Almanac = almanac.ToDictionary(a => a.Id);
        Work = work.ToDictionary(w => w.Id);
        Premises = premises.ToDictionary(p => p.Id);
        Lots = (lots ?? Array.Empty<LotDef>()).ToDictionary(l => l.Id);
        Products = (products ?? Array.Empty<ProductDef>()).ToDictionary(p => p.Id);
        var errors = Validate();
        if (errors.Count > 0) throw new InvalidDataException("Content invalid:\n" + string.Join("\n", errors));
    }

    /// <summary>Build from file name → JSON text. Expected names: acts, announcements, almanac, work, premises (.json).</summary>
    public static ContentDb FromJson(IReadOnlyDictionary<string, string> files)
    {
        List<T> Load<T>(string name) =>
            files.TryGetValue(name + ".json", out var text)
                ? JsonSerializer.Deserialize<List<T>>(text, Json) ?? new()
                : throw new InvalidDataException($"Missing content file {name}.json");

        return new ContentDb(Load<ActDef>("acts"), Load<AnnouncementDef>("announcements"),
            Load<AlmanacEntryDef>("almanac"), Load<WorkKindDef>("work"), Load<PremiseDef>("premises"), Load<LotDef>("lots"), Load<ProductDef>("products"));
    }

    public static readonly string[] FileNames = { "acts.json", "announcements.json", "almanac.json", "work.json", "premises.json", "lots.json", "products.json" };

    public ActDef ActAt(int index) => Acts[Math.Clamp(index, 0, Acts.Count - 1)];

    /// <summary>Referential integrity. Broken content should fail loudly at load, never mid-game.</summary>
    public List<string> Validate()
    {
        var e = new List<string>();
        for (int i = 0; i < Acts.Count; i++)
        {
            if (Acts[i].Index != i) e.Add($"act {Acts[i].Id}: index {Acts[i].Index} should be {i}");
            if (Acts[i].GateAnnouncement is { } g && !Announcements.ContainsKey(g))
                e.Add($"act {Acts[i].Id}: unknown gate announcement '{g}'");
        }
        foreach (var a in Announcements.Values)
            try { Core.GameDate.Parse(a.Date); } catch { e.Add($"announcement {a.Id}: bad date '{a.Date}'"); }
        foreach (var entry in Almanac.Values)
        {
            if (!Announcements.ContainsKey(entry.Announcement)) e.Add($"almanac {entry.Id}: unknown announcement '{entry.Announcement}'");
            foreach (var r in entry.Requires)
                if (!Almanac.ContainsKey(r)) e.Add($"almanac {entry.Id}: unknown requirement '{r}'");
        }
        foreach (var w in Work.Values)
        {
            if (w.Stages.Count == 0) e.Add($"work {w.Id}: no stages");
            foreach (var s in w.Stages)
                if (s.RequiresEntry is { } r && !Almanac.ContainsKey(r)) e.Add($"work {w.Id}: unknown entry '{r}'");
        }
        foreach (var p in Products.Values)
        {
            if (p.Steps.Count == 0) e.Add($"product {p.Id}: no steps");
            if (p.RequiresEntry is { } r && !Almanac.ContainsKey(r)) e.Add($"product {p.Id}: unknown entry '{r}'");
            if (p.Cost > p.Price) e.Add($"product {p.Id}: costs more than it sells for");
        }
        foreach (var l in Lots.Values)
            if (l.Premise is { } pr && !Premises.ContainsKey(pr)) e.Add($"lot {l.Id}: unknown premise '{pr}'");
        return e;
    }
}
