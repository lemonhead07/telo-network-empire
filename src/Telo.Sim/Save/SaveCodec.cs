using System.Text.Json;
using Telo.Sim.Content;
using Telo.Sim.World;

namespace Telo.Sim.Save;

/// <summary>Versioned JSON save. Serialise the simulation, rebuild the view — never save engine objects.</summary>
public static class SaveCodec
{
    public static string ToJson(GameState s) => JsonSerializer.Serialize(s, ContentDb.Json);

    public static GameState FromJson(string json)
    {
        var s = JsonSerializer.Deserialize<GameState>(json, ContentDb.Json) ?? throw new InvalidDataException("Empty save");
        if (s.Version > GameState.CurrentVersion)
            throw new InvalidDataException($"Save is from a newer build (v{s.Version}).");
        // Future: migrate older versions here, one step at a time (v1 -> v2 -> ...).
        s.Version = GameState.CurrentVersion;
        return s;
    }
}
