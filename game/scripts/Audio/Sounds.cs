namespace Telo.Game.Audio;

/// <summary>
/// Every sound in the game, by id (EA7). Pure C# like <see cref="Synth"/>: `tools/soundreel` renders the whole list to .wav.
/// The recipes live in three files, one per family: <c>Sounds.Foley.cs</c> (footsteps, doors, tools, kit, the hold tick),
/// <c>Sounds.Line.cs</c> (telephone tones, the switchboard, PIP and the answering machine) and <c>Sounds.Ambience.cs</c>
/// (the van's engine, wind, birds, crickets, room tone). Ids are stable strings like content ids: code calls
/// <c>Sfx.Play("door.knock", at)</c>, and an id nobody has made yet is simply silent.
///
/// Levels: a recipe returns its sound normalised (peak about 0.9); <see cref="Recipe.GainDb"/> sets how loud it plays
/// relative to the others, so the mix is tuned in one place. Rough guide: footsteps -16, small handling -12, tools -8,
/// UI and PIP -10, doors and knocks -6, ambience loops -18 to -24, the engine -10.
/// </summary>
public static partial class Sounds
{
    /// <summary>One sound. <paramref name="Make"/> builds variant v (0 to Variants-1); each play picks one at random.</summary>
    public sealed record Recipe(string Id, string Family, int Variants, bool Loop, float GainDb, string Bus, string Note, Func<int, float[]> Make);

    public static readonly List<Recipe> All = new();
    private static readonly Dictionary<string, Recipe> ById = new();

    static Sounds()
    {
        RegisterFoley();
        RegisterLine();
        RegisterAmbience();
    }

    public static Recipe? Get(string id) => ById.TryGetValue(id, out var r) ? r : null;

    /// <param name="variants">How many versions to build (footsteps want 5-8 so a walk doesn't machine-gun).</param>
    /// <param name="loop">Plays seamlessly round and round (use <see cref="Synth.LoopSeam"/> in the recipe).</param>
    /// <param name="bus">"Sfx" for things that happen, "Amb" for the background beds.</param>
    private static void Add(string family, string id, string note, Func<int, float[]> make, int variants = 1, float gainDb = 0, bool loop = false, string bus = "Sfx")
    {
        var r = new Recipe(id, family, Math.Max(1, variants), loop, gainDb, bus, note, make);
        if (ById.ContainsKey(id)) All.RemoveAll(x => x.Id == id);   // a later Add replaces a placeholder
        ById[id] = r;
        All.Add(r);
    }

    /// <summary>Build one variant, ready to play: DC removed, ends faded (loops are left as made), clamped to -1..1.</summary>
    public static float[] Render(Recipe r, int variant)
    {
        var x = r.Make(variant);
        if (!r.Loop) x.DcBlock().Fade(0.002, 0.01);
        for (int i = 0; i < x.Length; i++) x[i] = Math.Clamp(x[i], -1f, 1f);
        return x;
    }

    /// <summary>A seed for variant v of a sound, so each variant differs and every run makes the same sound.</summary>
    private static ulong Seed(string id, int v)
    {
        ulong h = 1469598103934665603UL;
        foreach (char c in id) { h ^= c; h *= 1099511628211UL; }
        return h ^ (ulong)(v + 1) * 0x9E3779B97F4A7C15UL;
    }

    static partial void RegisterFoley();
    static partial void RegisterLine();
    static partial void RegisterAmbience();

    /// <summary>A stand-in beep for an id not made yet (so wiring can be heard before the recipe exists).</summary>
    private static float[] Placeholder(double hz) => Synth.Sine(0.08, hz).Env(0.002, 0.03).Normalize(0.5f);
}
