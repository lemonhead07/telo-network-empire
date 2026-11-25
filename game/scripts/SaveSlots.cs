using System.Globalization;
using System.Text.Json;
using Godot;
using Telo.Sim.Content;
using Telo.Sim.Save;
using Telo.Sim.World;

namespace Telo.Game;

/// <summary>
/// The save files on disk (EA6a): three slots the player picks, and an autosave written each morning and on quitting, all
/// under user://saves/. Every file is SaveCodec JSON of GameState, as saves always were. A slot's label (game date, act,
/// cash) is read from the top level of its JSON without loading the game, and "when" is the file's own time.
/// </summary>
public static class SaveSlots
{
    /// <summary>Where saves live. Screenshots point this at a scratch folder so they never touch the player's saves.</summary>
    public static string Dir { get; set; } = "user://saves";
    public const string Autosave = "autosave";
    public static readonly string[] Manual = { "slot-1", "slot-2", "slot-3" };
    /// <summary>Where the one save lived before there were slots.</summary>
    public const string OldSave = "user://save.json";

    public static IEnumerable<string> All => Manual.Prepend(Autosave);
    public static string PathOf(string slot) => $"{Dir}/{slot}.json";
    public static string Name(string slot) => slot == Autosave ? "Autosave" : $"Slot {slot[^1]}";

    /// <summary>What a slot holds, for its label. SavedUnix is the file's time (seconds, UTC).</summary>
    public sealed record Info(string Slot, int Day, int Act, long Cash, ulong SavedUnix)
    {
        public string Date => new Telo.Sim.Core.GameDate(Day).ToString();
        public string Saved => DateTimeOffset.FromUnixTimeSeconds((long)SavedUnix).ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>The label facts of a slot, or null if it is empty or can't be read. Reads only the few top-level numbers it needs.</summary>
    public static Info? Read(string slot)
    {
        string path = PathOf(slot);
        if (!Godot.FileAccess.FileExists(path)) return null;
        try
        {
            var bytes = Godot.FileAccess.GetFileAsBytes(path);
            var r = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            int? day = null, act = null; long? cash = null;
            if (!r.Read() || r.TokenType != JsonTokenType.StartObject) return null;
            while (r.Read() && r.TokenType == JsonTokenType.PropertyName && (day is null || act is null || cash is null))
            {
                string name = r.GetString() ?? "";
                r.Read();
                if (name.Equals("day", StringComparison.OrdinalIgnoreCase)) day = r.GetInt32();
                else if (name.Equals("actIndex", StringComparison.OrdinalIgnoreCase)) act = r.GetInt32();
                else if (name.Equals("cash", StringComparison.OrdinalIgnoreCase)) cash = r.GetInt64();
                else r.Skip();
            }
            return day is null ? null : new Info(slot, day.Value, act ?? 0, cash ?? 0, Godot.FileAccess.GetModifiedTime(path));
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>The most recently written save of all, for Continue.</summary>
    public static Info? Newest() => All.Select(Read).Where(i => i is not null).MaxBy(i => i!.SavedUnix);

    public static bool Write(string slot, GameState s)
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        using var f = Godot.FileAccess.Open(PathOf(slot), Godot.FileAccess.ModeFlags.Write);
        if (f is null) { GD.PrintErr($"Could not write {PathOf(slot)}: {Godot.FileAccess.GetOpenError()}"); return false; }
        f.StoreString(SaveCodec.ToJson(s));
        return true;
    }

    /// <summary>The game in a slot, migrated to this build, or null with the reason (a save from a newer build, a damaged file).</summary>
    public static GameState? Load(string slot, ContentDb content, out string? error)
    {
        error = null;
        string path = PathOf(slot);
        if (!Godot.FileAccess.FileExists(path)) { error = "That slot is empty."; return null; }
        try { return SaveCodec.FromJson(Godot.FileAccess.GetFileAsString(path), content); }
        catch (Exception e) when (e is JsonException or InvalidDataException or NotSupportedException)
        {
            error = e is InvalidDataException ? e.Message : "That save is damaged and can't be read.";
            return null;
        }
    }

    /// <summary>
    /// A game saved before there were slots (user://save.json) moves into the first empty slot, once, so nobody loses it.
    /// The old file is renamed rather than deleted.
    /// </summary>
    public static void MigrateOldSave()
    {
        if (!Godot.FileAccess.FileExists(OldSave)) return;
        if (Manual.FirstOrDefault(s => !Godot.FileAccess.FileExists(PathOf(s))) is not { } empty) return;
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        if (DirAccess.CopyAbsolute(OldSave, PathOf(empty)) != Error.Ok) return;
        DirAccess.RenameAbsolute(OldSave, "user://save-before-slots.json");
        GD.Print($"Moved the old save into {Name(empty)}.");
    }
}
