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
    /// <summary>The plot: chapters in order (plot.json).</summary>
    public IReadOnlyList<ChapterDef> Plot { get; }
    /// <summary>Things as parts and fasteners, by id (assemblies.json).</summary>
    public IReadOnlyDictionary<string, AssemblyDef> Assemblies { get; }

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
        IEnumerable<LotDef>? lots = null, IEnumerable<ProductDef>? products = null, IEnumerable<ChapterDef>? plot = null, IEnumerable<AssemblyDef>? assemblies = null)
    {
        Acts = acts.OrderBy(a => a.Index).ToList();
        Announcements = announcements.ToDictionary(a => a.Id);
        Almanac = almanac.ToDictionary(a => a.Id);
        Work = work.ToDictionary(w => w.Id);
        Premises = premises.ToDictionary(p => p.Id);
        Lots = (lots ?? Array.Empty<LotDef>()).ToDictionary(l => l.Id);
        Products = (products ?? Array.Empty<ProductDef>()).ToDictionary(p => p.Id);
        Plot = (plot ?? Array.Empty<ChapterDef>()).ToList();
        Assemblies = (assemblies ?? Array.Empty<AssemblyDef>()).ToDictionary(a => a.Id);
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
            Load<AlmanacEntryDef>("almanac"), Load<WorkKindDef>("work"), Load<PremiseDef>("premises"), Load<LotDef>("lots"), Load<ProductDef>("products"), Load<ChapterDef>("plot"), Load<AssemblyDef>("assemblies"));
    }

    public static readonly string[] FileNames = { "acts.json", "announcements.json", "almanac.json", "work.json", "premises.json", "lots.json", "products.json", "plot.json", "assemblies.json" };

    public ActDef ActAt(int index) => Acts[Math.Clamp(index, 0, Acts.Count - 1)];

    /// <summary>Referential integrity. Broken content should fail loudly at load, never mid-game.</summary>
    public List<string> Validate()
    {
        var e = new List<string>();
        foreach (var a in Assemblies.Values)
        {
            if (a.Parts.Count == 0) e.Add($"assembly {a.Id}: no parts");
            if (a.Mode == "repair" && !a.Parts.Any(p => p.CanFail)) e.Add($"assembly {a.Id}: a repair needs a part that can fail");
            if (a.Mode is not ("repair" or "install")) e.Add($"assembly {a.Id}: unknown mode '{a.Mode}'");
            foreach (var n in a.Needs) if (!Enum.TryParse<Town.Tools>(n, true, out _)) e.Add($"assembly {a.Id}: unknown kit '{n}'");
        }
        // the plot is one line: ids unique, first-hour steps never go backwards, every chapter but the last can finish
        for (int i = 0; i < Plot.Count; i++)
        {
            if (Plot.Take(i).Any(p => p.Id == Plot[i].Id)) e.Add($"plot {Plot[i].Id}: duplicate id");
            if (i > 0 && Plot[i].Step < Plot[i - 1].Step) e.Add($"plot {Plot[i].Id}: step goes backwards");
            if (i < Plot.Count - 1 && Plot[i].DoneWhen.Count == 0) e.Add($"plot {Plot[i].Id}: only the last chapter may have no end");
        }
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
            {
                if (s.RequiresEntry is { } r && !Almanac.ContainsKey(r)) e.Add($"work {w.Id}: unknown entry '{r}'");
                // D37: one to five jobs a day, nobody more
                if (s.Hire is { } h && (h.JobsPerDay < 1 || h.JobsPerDay > Town.BookingRules.MaxJobsPerDay)) e.Add($"work {w.Id}: {h.Name} does {h.JobsPerDay} jobs a day (1 to {Town.BookingRules.MaxJobsPerDay})");
            }
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
