using Godot;
using Telo.Game.World;
using Telo.Sim.Build;

namespace Telo.Game;

/// <summary>
/// What the background sound (EA7, Audio/Ambience) can't see for itself: how near the sea is, how much of the data centre's
/// hall you hear, how high you are, and where a bird could be singing. Read twice a second, so kept cheap.
/// </summary>
public partial class GameRoot
{
    private Telo.Game.Audio.Ambience NewAmbience() => new()
    {
        Sea = () => SeaNearness(Telo.Game.Audio.Sfx.Ear()),
        Hall = () => HallHeard(Telo.Game.Audio.Sfx.Ear()),
        Altitude = () => Telo.Game.Audio.Sfx.Ear().Length() - (float)_planet.RadiusM,
        BirdAt = p => !Wet(p),
    };

    /// <summary>Rings of points round the ear: how far out (m), and how loud the sea is if they're in the water.</summary>
    private static readonly (float M, float Loud)[] SeaRings = { (14f, 1f), (40f, 0.75f), (80f, 0.45f), (140f, 0.22f) };

    /// <summary>
    /// How near the sea is: 1 over the water or with the waves at your feet, falling away to nothing about 150 m inland.
    /// Eight points on each ring; a ring counts in full once three of them are in the water.
    /// </summary>
    private float SeaNearness(Vector3 ear)
    {
        if (Wet(ear)) return 1f;
        var up = ear.Normalized();
        var side = up.Cross(Mathf.Abs(up.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
        var fwd = up.Cross(side);
        float near = 0;
        for (int r = 0; r < SeaRings.Length; r++)
        {
            var (m, loud) = SeaRings[r];
            if (loud <= near) break;   // a nearer ring already beats anything further out
            int wet = 0;
            for (int k = 0; k < 8; k++)
            {
                float a = (k + 0.5f * (r % 2)) * Mathf.Tau / 8;   // alternate rings look between the last one's points
                if (Wet(ear + (side * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * m)) wet++;
            }
            near = Mathf.Max(near, Mathf.Min(1f, wet / 3f) * loud);
        }
        return near;
    }

    /// <summary>Is the ground under this point below the sea? (Roads and building sites only reshape land, so the raw terrain will do.)</summary>
    private bool Wet(Vector3 world) => _planet.RawHeight(PlanetBuilder.ToSim(world.Normalized())) < 0;

    /// <summary>
    /// How much of the hall's fans you hear in the data centre: it hums once it has power, the cooling adds its fans,
    /// the racks come on with the board lit. Full in the hall, about a third through the partitions (they stop short of the roof).
    /// </summary>
    private float HallHeard(Vector3 ear)
    {
        if (_dc is null) return 0f;
        var room = _dc.RoomAt(ear);
        if (room == DataCentre.Room.Outside) return 0f;
        float running = ProjectRules.Done(Sim.State, "proj-dc-lit") ? 1f
            : ProjectRules.Done(Sim.State, "proj-dc-cooling") ? 0.6f
            : ProjectRules.Done(Sim.State, "proj-dc-power") ? 0.3f : 0f;
        return running * (room == DataCentre.Room.Hall ? 1f : 0.35f);
    }
}
