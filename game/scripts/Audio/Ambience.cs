using Godot;

namespace Telo.Game.Audio;

/// <summary>
/// The background (EA7). Outdoors: wind always (a little stronger up a hill and by the sea), birds by day (a dawn chorus
/// from six, fewer through the day, gone by seven; each call from somewhere in the trees 10-40 m away), crickets at night
/// (fading in from half past eight, out by half past five), the sea near the shore. Indoors the outdoor beds are heard
/// through the walls and the room's own tone comes up; in the data centre the hall's fans take its place, louder the more
/// of the hall is running. Everything crossfades (<see cref="SfxLoop.Slew"/>).
/// Reads <see cref="Sfx.Indoors"/>, <see cref="Sfx.Hour"/> and <see cref="Sfx.Ear"/>; what it can't see for itself
/// (the sea, the hall, the height, where a bird can sing) GameRoot gives it below (AudioHooks.cs). Shows only; decides nothing.
/// </summary>
public partial class Ambience : Node
{
    /// <summary>How near the sea is, 0 (inland) to 1 (on the beach or the jetty).</summary>
    public Func<float> Sea { get; set; } = () => 0f;
    /// <summary>How much of the data centre hall's fans you hear, 0 (none, or not running yet) to 1 (standing in it).</summary>
    public Func<float> Hall { get; set; } = () => 0f;
    /// <summary>The ear's height above the sea in metres (the wind is stronger up a hill).</summary>
    public Func<float> Altitude { get; set; } = () => 0f;
    /// <summary>Can a bird be singing here (over land, not out over the water)?</summary>
    public Func<Vector3, bool> BirdAt { get; set; } = _ => true;

    private SfxLoop? _wind, _crickets, _sea, _room, _hall;
    private float _seaNear, _hallHeard, _alt, _poll, _nextBird = 3f;
    private readonly RandomNumberGenerator _rng = new();

    /// <summary>How quickly the beds follow (dB a second): a doorway takes about half a second.</summary>
    private const float Slew = 30f;
    /// <summary>The outdoor beds heard through a house's walls, and through the data centre's over its fans.</summary>
    private const float ThroughWalls = -16f, ThroughHall = -12f;

    private IEnumerable<SfxLoop> Loops => new[] { _wind, _crickets, _sea, _room, _hall }.OfType<SfxLoop>();

    public override void _Ready()
    {
        _rng.Randomize();
        _wind = Sfx.Loop("amb.wind", db: -80);
        _crickets = Sfx.Loop("amb.crickets", db: -80);
        _sea = Sfx.Loop("amb.sea", db: -80);
        _room = Sfx.Loop("amb.room", db: -80);
        _hall = Sfx.Loop("amb.hall", db: -80);
        foreach (var l in Loops) l.Slew = Slew;
    }

    public override void _ExitTree()
    {
        foreach (var l in Loops) if (IsInstanceValid(l)) l.Stop();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if ((_poll -= dt) <= 0)   // the hooks sample the planet: twice a second is plenty
        {
            _poll = 0.5f;
            _seaNear = Mathf.Clamp(Sea(), 0f, 1f);
            _hallHeard = Mathf.Clamp(Hall(), 0f, 1f);
            _alt = Altitude();
        }
        bool inside = Sfx.Indoors();
        float hour = Sfx.Hour();
        float through = inside ? ThroughWalls + ThroughHall * _hallHeard : 0f;

        Set(_wind, -1f + 4f * Mathf.Clamp(_alt / 180f, 0f, 1f) + 2.5f * _seaNear + through);
        Set(_sea, Db(_seaNear) + through);
        Set(_crickets, Db(Crickets(hour)) + through);
        Set(_room, inside ? Db(1f - _hallHeard) : -80f);   // in the hall its fans are the room tone
        Set(_hall, Db(_hallHeard));

        float busy = _hallHeard > 0.5f ? 0f : Birdsong(hour);
        if (busy <= 0.01f) { _nextBird = Mathf.Max(_nextBird, 1f); return; }
        if ((_nextBird -= dt) > 0) return;
        _nextBird = Mathf.Lerp(14f, 1.6f, busy) * _rng.RandfRange(0.4f, 1.6f);
        Bird(inside);
    }

    /// <summary>One bird somewhere in the trees around you; now and then it sings again from the same branch.</summary>
    private void Bird(bool inside)
    {
        var ear = Sfx.Ear();
        var up = ear.Normalized();   // the planet's centre is the world origin
        var side = up.Cross(Mathf.Abs(up.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
        var fwd = up.Cross(side);
        for (int tries = 0; tries < 3; tries++)
        {
            float a = _rng.RandfRange(0f, Mathf.Tau), d = _rng.RandfRange(10f, 40f);
            var at = ear + (side * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * d + up * _rng.RandfRange(2f, 8f);
            if (!BirdAt(at)) continue;
            float db = inside ? ThroughWalls + 2f : 0f, pitch = _rng.RandfRange(0.93f, 1.07f);
            Sfx.Play("amb.birds", at, db, pitch);
            if (_rng.Randf() < 0.35f) Sfx.PlayAfter(_rng.RandfRange(1.2f, 3.5f), "amb.birds", at, db, pitch);
            return;
        }
    }

    /// <summary>How busy the birds are, 0-1: first light at six, the dawn chorus till eight, quieter through the day, a little evensong, gone by seven.</summary>
    private static float Birdsong(float h)
    {
        if (h < 6f || h >= 19f) return 0f;
        if (h < 8f) return Mathf.SmoothStep(6f, 6.4f, h);
        if (h < 10f) return Mathf.Lerp(1f, 0.4f, (h - 8f) / 2f);
        if (h < 17f) return 0.4f;
        if (h < 18.3f) return 0.5f;
        return Mathf.Lerp(0.5f, 0f, (h - 18.3f) / 0.7f);
    }

    /// <summary>Crickets, 0-1: in from half past eight to half past nine, out from half past four to half past five.</summary>
    private static float Crickets(float h) => h >= 12f ? Mathf.SmoothStep(20.5f, 21.5f, h) : 1f - Mathf.SmoothStep(4.5f, 5.5f, h);

    private static void Set(SfxLoop? l, float db) { if (l is not null) l.Db = Mathf.Max(db, -80f); }

    /// <summary>A 0-1 level as dB, with anything under -50 dB treated as silent.</summary>
    private static float Db(float x) => x < 0.003f ? -80f : Mathf.LinearToDb(x);
}
