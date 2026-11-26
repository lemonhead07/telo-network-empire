using System.Collections.Concurrent;
using Godot;

namespace Telo.Game.Audio;

/// <summary>
/// Plays the game's sounds (EA7). Everything is synthesised from <see cref="Sounds"/> at startup on a background thread
/// (no files), turned into <see cref="AudioStreamWav"/>s, and played from a small pool of players.
///
///   Sfx.Play("door.knock", door.GlobalPosition);   // in the world: heard from where it happens
///   Sfx.Play("pip.beep");                          // no position: in your ear (PIP, screens, your own footsteps)
///   var engine = Sfx.Loop("van.engine", van);      // a loop that follows a node; set engine.Pitch / engine.Db; engine.Stop()
///
/// An id that isn't in <see cref="Sounds"/> is silent (printed once in debug runs), so wiring never breaks the game.
/// Two buses under Master: "Sfx" and "Amb" (the background beds), so a later settings page can mix them; the Master
/// volume in Settings (EA6) already covers both.
/// </summary>
public partial class Sfx : Node
{
    private static Sfx? _i;
    private static readonly ConcurrentDictionary<string, float[][]> Samples = new();
    private static readonly Dictionary<string, AudioStreamWav[]> Streams = new();
    private static readonly HashSet<string> Warned = new();
    private static Task? _warming;
    private static readonly Random Pick = new();

    /// <summary>What the listener is in, set by GameRoot: footsteps, the ambience and reverb read it.</summary>
    public static Func<bool> Indoors { get; set; } = () => false;
    /// <summary>The hour of the day, 0-24 (06:00 is 6).</summary>
    public static Func<float> Hour { get; set; } = () => 12f;
    /// <summary>The listener's position in the world (the camera that's current).</summary>
    public static Func<Vector3> Ear { get; set; } = () => Vector3.Zero;

    private readonly List<AudioStreamPlayer3D> _pool3 = new();
    private readonly List<AudioStreamPlayer> _pool2 = new();
    private int _next3, _next2;
    private const int Pool3 = 28, Pool2 = 12;

    /// <summary>Unit size for 3D sounds: full loudness within this many metres, then falling off.</summary>
    public const float UnitSize = 3f, MaxDistance = 70f;

    public override void _EnterTree()
    {
        _i = this;
        EnsureBuses();
        Warm();
        for (int k = 0; k < Pool3; k++)
        {
            var p = new AudioStreamPlayer3D { UnitSize = UnitSize, MaxDistance = MaxDistance, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled, Bus = "Sfx", PanningStrength = 0.7f };
            AddChild(p); _pool3.Add(p);
        }
        for (int k = 0; k < Pool2; k++) { var p = new AudioStreamPlayer { Bus = "Sfx" }; AddChild(p); _pool2.Add(p); }
    }

    public override void _ExitTree() { if (_i == this) _i = null; }

    /// <summary>Start building every sound on a background thread (once per launch; the samples survive a reload of the world).</summary>
    public static void Warm()
    {
        _warming ??= Task.Run(() =>
        {
            foreach (var r in Sounds.All.ToList())
                if (!Samples.ContainsKey(r.Id)) Samples.TryAdd(r.Id, Build(r));
        });
    }

    private static float[][] Build(Sounds.Recipe r) => Enumerable.Range(0, r.Variants).Select(v => Sounds.Render(r, v)).ToArray();

    private static void EnsureBuses()
    {
        foreach (var name in new[] { "Sfx", "Amb" })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            int i = AudioServer.BusCount;
            AudioServer.AddBus(i);
            AudioServer.SetBusName(i, name);
            AudioServer.SetBusSend(i, "Master");
        }
    }

    /// <summary>The streams for an id (built now if the background thread hasn't got to it yet), or null if there's no such sound.</summary>
    public static AudioStreamWav[]? StreamsFor(string id)
    {
        if (Streams.TryGetValue(id, out var have)) return have;
        if (Sounds.Get(id) is not { } r)
        {
            if (OS.IsDebugBuild() && Warned.Add(id)) GD.Print($"Sfx: no sound called \"{id}\" yet");
            return null;
        }
        var samples = Samples.GetOrAdd(id, _ => Build(r));
        var made = samples.Select(s => ToWav(s, r.Loop)).ToArray();
        Streams[id] = made;
        return made;
    }

    public static AudioStreamWav ToWav(float[] s, bool loop)
    {
        var bytes = new byte[s.Length * 2];
        for (int i = 0; i < s.Length; i++)
        {
            short v = (short)Math.Clamp((int)MathF.Round(s[i] * 32767f), -32768, 32767);
            bytes[2 * i] = (byte)(v & 0xff); bytes[2 * i + 1] = (byte)((v >> 8) & 0xff);
        }
        var w = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Synth.Rate, Stereo = false, Data = bytes };
        if (loop) { w.LoopMode = AudioStreamWav.LoopModeEnum.Forward; w.LoopBegin = 0; w.LoopEnd = s.Length; }
        return w;
    }

    /// <summary>
    /// Play a sound once. With a position it's heard from there (3D); without one it's in your ear.
    /// <paramref name="db"/> adds to the sound's own level; <paramref name="pitch"/> 1 is as made, and each play varies
    /// it by a few percent unless <paramref name="vary"/> is false.
    /// </summary>
    public static void Play(string id, Vector3? at = null, float db = 0, float pitch = 1, bool vary = true)
    {
        if (_i is null || StreamsFor(id) is not { Length: > 0 } streams) return;
        var r = Sounds.Get(id)!;
        var stream = streams[Pick.Next(streams.Length)];
        float p = pitch * (vary ? 1f + (float)(Pick.NextDouble() - 0.5) * 0.07f : 1f);
        if (at is { } pos)
        {
            if (pos.DistanceTo(Ear()) > MaxDistance) return;
            var pl = _i.Next3();
            pl.Stream = stream; pl.GlobalPosition = pos; pl.VolumeDb = r.GainDb + db; pl.PitchScale = p; pl.Bus = r.Bus;
            pl.Play();
        }
        else
        {
            var pl = _i.Next2();
            pl.Stream = stream; pl.VolumeDb = r.GainDb + db; pl.PitchScale = p; pl.Bus = r.Bus;
            pl.Play();
        }
    }

    /// <summary>Play a sound a little later (seconds): a knock then the door, a relay cascade.</summary>
    public static void PlayAfter(double seconds, string id, Vector3? at = null, float db = 0, float pitch = 1)
    {
        if (_i is null) return;
        _i.GetTree().CreateTimer(seconds).Timeout += () => Play(id, at, db, pitch);
    }

    private AudioStreamPlayer3D Next3()
    {
        for (int k = 0; k < _pool3.Count; k++) { var p = _pool3[(_next3 + k) % _pool3.Count]; if (!p.Playing) { _next3 = (_next3 + k + 1) % _pool3.Count; return p; } }
        var steal = _pool3[_next3]; _next3 = (_next3 + 1) % _pool3.Count; return steal;
    }

    private AudioStreamPlayer Next2()
    {
        for (int k = 0; k < _pool2.Count; k++) { var p = _pool2[(_next2 + k) % _pool2.Count]; if (!p.Playing) { _next2 = (_next2 + k + 1) % _pool2.Count; return p; } }
        var steal = _pool2[_next2]; _next2 = (_next2 + 1) % _pool2.Count; return steal;
    }

    /// <summary>
    /// A looping sound. With a node it follows that node in the world (the van's engine); without, it's in your ear (wind,
    /// room tone). Starts silent-safe: if the id doesn't exist the handle does nothing. Change Db and Pitch every frame
    /// if you like; Stop() frees it.
    /// </summary>
    public static SfxLoop Loop(string id, Node3D? follow = null, float db = 0)
    {
        var loop = new SfxLoop(id, follow, db);
        _i?.AddChild(loop);
        return loop;
    }
}

/// <summary>One looping sound (see <see cref="Sfx.Loop"/>). Db is on top of the sound's own level; -80 is silent.</summary>
public partial class SfxLoop : Node
{
    private readonly string _id;
    private readonly Node3D? _follow;
    private AudioStreamPlayer3D? _p3;
    private AudioStreamPlayer? _p2;
    private float _base;
    public float Db { get; set; }
    public float Pitch { get; set; } = 1f;
    /// <summary>How fast Db moves to its new value (dB per second), so loops fade rather than jump. 0 jumps.</summary>
    public float Slew { get; set; } = 40f;
    private float _db = -80f;

    public SfxLoop() { _id = ""; }
    public SfxLoop(string id, Node3D? follow, float db) { _id = id; _follow = follow; Db = db; }

    public override void _Ready()
    {
        if (Sfx.StreamsFor(_id) is not { Length: > 0 } s) return;
        var r = Sounds.Get(_id)!;
        _base = r.GainDb;
        if (_follow is not null)
        {
            _p3 = new AudioStreamPlayer3D { Stream = s[0], UnitSize = Sfx.UnitSize * 2, MaxDistance = Sfx.MaxDistance * 1.5f, Bus = r.Bus,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance, DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled, VolumeDb = -80 };
            AddChild(_p3); _p3.Play();
        }
        else { _p2 = new AudioStreamPlayer { Stream = s[0], Bus = r.Bus, VolumeDb = -80 }; AddChild(_p2); _p2.Play(); }
    }

    public override void _Process(double delta)
    {
        _db = Slew <= 0 ? Db : Mathf.MoveToward(_db, Db, Slew * (float)delta);
        float vol = _db <= -79f ? -80f : _base + _db;
        if (_p3 is not null)
        {
            if (IsInstanceValid(_follow)) _p3.GlobalPosition = _follow!.GlobalPosition;
            _p3.VolumeDb = vol; _p3.PitchScale = Mathf.Max(0.05f, Pitch);
        }
        else if (_p2 is not null) { _p2.VolumeDb = vol; _p2.PitchScale = Mathf.Max(0.05f, Pitch); }
    }

    public void Stop() => QueueFree();
}
