namespace Telo.Game.Audio;

/// <summary>Ambience and engines: the outdoor bed, indoor room tone, the van, the ferry (EA7).</summary>
public static partial class Sounds
{
    static partial void RegisterAmbience()
    {
        const string A = "ambience";
        // placeholders: each id is the contract; replace the recipe, keep the id
        Add(A, "amb.wind", "a quiet outdoor wind bed (loops)", v => Synth.Silence(1), loop: true, gainDb: -20, bus: "Amb");
        Add(A, "amb.birds", "one bird call: a few chirps (scattered through the day)", v => Placeholder(3200), variants: 8, gainDb: -20, bus: "Amb");
        Add(A, "amb.crickets", "crickets at night (loops)", v => Synth.Silence(1), loop: true, gainDb: -22, bus: "Amb");
        Add(A, "amb.room", "room tone indoors: still air, a faint hum (loops)", v => Synth.Silence(1), loop: true, gainDb: -24, bus: "Amb");
        Add(A, "amb.sea", "waves on the shore (loops)", v => Synth.Silence(1), loop: true, gainDb: -18, bus: "Amb");
        Add(A, "amb.hall", "the data centre hall: fans and a deep electrical hum (loops)", v => Synth.Silence(1), loop: true, gainDb: -18, bus: "Amb");
        Add(A, "van.engine", "the van's engine at idle (loops; pitched up with speed)", v => Synth.Silence(1), loop: true, gainDb: -10);
        Add(A, "van.start", "the van starting: starter motor, then the engine catches", v => Placeholder(140), gainDb: -8);
        Add(A, "van.stop", "the engine switched off", v => Placeholder(130), gainDb: -10);
        Add(A, "ferry.horn", "the car ferry's horn", v => Placeholder(110), gainDb: -6);
    }
}
