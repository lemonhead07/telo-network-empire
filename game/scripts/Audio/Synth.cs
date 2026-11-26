namespace Telo.Game.Audio;

/// <summary>
/// A tiny synthesiser: every sound in the game is built from these at startup (EA7, D15: no sound files).
/// Pure C#, no Godot types, so `tools/soundreel` can render the whole bank to .wav files outside the game.
/// Buffers are mono float samples at <see cref="Rate"/>, nominally -1..1. Generators make a new buffer;
/// processors change the buffer they are given and return it, so calls chain:
/// <c>Synth.Noise(0.2, rng).BandPass(1800, 2).Decay(0.04).Normalize()</c>.
/// Thread-safe as long as each recipe uses its own <see cref="Rng"/> (sounds are generated on a background thread).
/// </summary>
public static class Synth
{
    public const int Rate = 44100;
    private const double Tau = Math.PI * 2;

    public static int N(double seconds) => Math.Max(1, (int)Math.Round(seconds * Rate));
    public static double Seconds(this float[] x) => x.Length / (double)Rate;

    /// <summary>A small seeded random source (xorshift), one per recipe call. Never share one between threads.</summary>
    public struct Rng
    {
        private ulong _s;
        public Rng(ulong seed) { _s = seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; if (_s == 0) _s = 1; }
        public ulong NextU() { _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17; return _s; }
        /// <summary>0 to 1.</summary>
        public float Next() => (NextU() >> 40) / (float)(1UL << 24);
        /// <summary>-1 to 1.</summary>
        public float Bi() => Next() * 2f - 1f;
        public float Range(float a, float b) => a + (b - a) * Next();
        public int Int(int max) => (int)(NextU() % (ulong)Math.Max(1, max));
    }

    // ---------------------------------------------------------------- generators

    public static float[] Silence(double s) => new float[N(s)];

    /// <summary>A sine at a fixed frequency.</summary>
    public static float[] Sine(double s, double hz, double phase = 0)
    {
        var x = new float[N(s)];
        for (int i = 0; i < x.Length; i++) x[i] = (float)Math.Sin(phase + Tau * hz * i / Rate);
        return x;
    }

    /// <summary>Several sines summed at equal level (telephone tones: 350 + 440 and so on), scaled to peak 1.</summary>
    public static float[] Tone(double s, params double[] hz)
    {
        var x = new float[N(s)];
        foreach (var f in hz)
            for (int i = 0; i < x.Length; i++) x[i] += (float)Math.Sin(Tau * f * i / Rate);
        return x.Gain(1.0 / Math.Max(1, hz.Length));
    }

    /// <summary>A sine whose frequency follows a function of time (0..1 through the buffer): chirps, sirens, engine whine.</summary>
    public static float[] SineGlide(double s, Func<double, double> hzAt)
    {
        var x = new float[N(s)];
        double ph = 0;
        for (int i = 0; i < x.Length; i++) { ph += Tau * hzAt(i / (double)x.Length) / Rate; x[i] = (float)Math.Sin(ph); }
        return x;
    }

    /// <summary>A sine sweep from one frequency to another (exponential, so it sounds even).</summary>
    public static float[] Sweep(double s, double hz0, double hz1) => SineGlide(s, t => hz0 * Math.Pow(hz1 / hz0, t));

    /// <summary>A band-limited-ish square (sum of odd harmonics up to 8 kHz), softer than a raw square.</summary>
    public static float[] Square(double s, double hz) => Harmonics(s, hz, k => k % 2 == 1 ? 1.0 / k : 0);
    /// <summary>A band-limited-ish sawtooth (all harmonics up to 8 kHz).</summary>
    public static float[] Saw(double s, double hz) => Harmonics(s, hz, k => 1.0 / k);
    /// <summary>A triangle (odd harmonics falling fast): mellow, like a cheap electronic beeper.</summary>
    public static float[] Triangle(double s, double hz) => Harmonics(s, hz, k => k % 2 == 1 ? (((k - 1) / 2) % 2 == 0 ? 1 : -1) / (double)(k * k) : 0);

    /// <summary>Additive: harmonic k (1, 2, 3 ...) at amplitude amp(k), stopping below 8 kHz.</summary>
    public static float[] Harmonics(double s, double hz, Func<int, double> amp)
    {
        var x = new float[N(s)];
        for (int k = 1; k * hz < 8000; k++)
        {
            double a = amp(k);
            if (a == 0) continue;
            for (int i = 0; i < x.Length; i++) x[i] += (float)(a * Math.Sin(Tau * hz * k * i / Rate));
        }
        return x.Normalize(1f);
    }

    /// <summary>A raw (aliasing) pulse wave: buzzy, cheap, right for relays, buzzers and old electronics.</summary>
    public static float[] Pulse(double s, double hz, double width = 0.5)
    {
        var x = new float[N(s)];
        for (int i = 0; i < x.Length; i++) x[i] = (i * hz / Rate) % 1.0 < width ? 1f : -1f;
        return x;
    }

    /// <summary>White noise.</summary>
    public static float[] Noise(double s, ref Rng r)
    {
        var x = new float[N(s)];
        for (int i = 0; i < x.Length; i++) x[i] = r.Bi();
        return x;
    }
    public static float[] Noise(double s, ulong seed) { var r = new Rng(seed); return Noise(s, ref r); }

    /// <summary>Pink noise (Paul Kellet's filter): wind, rustle, room tone.</summary>
    public static float[] Pink(double s, ulong seed)
    {
        var r = new Rng(seed);
        var x = new float[N(s)];
        double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double w = r.Bi();
            b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.96900 * b2 + w * 0.1538520;
            b3 = 0.86650 * b3 + w * 0.3104856; b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
            x[i] = (float)((b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362) * 0.11);
            b6 = w * 0.115926;
        }
        return x;
    }

    /// <summary>Brown (red) noise: deep rumble, distant traffic, sea.</summary>
    public static float[] Brown(double s, ulong seed)
    {
        var r = new Rng(seed);
        var x = new float[N(s)];
        double v = 0;
        for (int i = 0; i < x.Length; i++) { v = (v + r.Bi() * 0.02) * 0.998; x[i] = (float)(v * 3.5); }
        return x;
    }

    /// <summary>A short click: a few samples of a decaying impulse, then filtered by the caller for colour.</summary>
    public static float[] Click(double s = 0.01, double tau = 0.0015)
    {
        var x = new float[N(s)];
        for (int i = 0; i < x.Length; i++) x[i] = (float)Math.Exp(-(i / (double)Rate) / tau) * (i % 2 == 0 ? 1 : -0.6f);
        return x;
    }

    /// <summary>A struck resonance (modal synthesis): sines at frequency × ratio, each decaying at its own rate. Bells, metal, wood, glass.</summary>
    public static float[] Modal(double s, double hz, (double ratio, double amp, double decay)[] modes)
    {
        var x = new float[N(s)];
        foreach (var (ratio, amp, decay) in modes)
        {
            double f = hz * ratio;
            if (f >= Rate / 2.2) continue;
            for (int i = 0; i < x.Length; i++)
            {
                double t = i / (double)Rate;
                x[i] += (float)(amp * Math.Exp(-t / decay) * Math.Sin(Tau * f * t));
            }
        }
        return x;
    }

    // ---------------------------------------------------------------- processors (in place)

    public static float[] Gain(this float[] x, double g) { for (int i = 0; i < x.Length; i++) x[i] = (float)(x[i] * g); return x; }
    public static float[] GainDb(this float[] x, double db) => x.Gain(Math.Pow(10, db / 20));

    /// <summary>Scale so the loudest sample is at <paramref name="peak"/> (default 0.9). Silence stays silence.</summary>
    public static float[] Normalize(this float[] x, float peak = 0.9f)
    {
        float m = 0;
        foreach (var v in x) m = Math.Max(m, Math.Abs(v));
        return m < 1e-9f ? x : x.Gain(peak / m);
    }

    /// <summary>Remove any DC offset (average level), which clicks at the start and end of a sound.</summary>
    public static float[] DcBlock(this float[] x)
    {
        double y = 0, prev = 0;
        for (int i = 0; i < x.Length; i++) { double v = x[i]; y = v - prev + 0.995 * y; prev = v; x[i] = (float)y; }
        return x;
    }

    /// <summary>One-pole low pass: takes the edge off.</summary>
    public static float[] LowPass(this float[] x, double hz)
    {
        double a = 1 - Math.Exp(-Tau * hz / Rate), y = 0;
        for (int i = 0; i < x.Length; i++) { y += a * (x[i] - y); x[i] = (float)y; }
        return x;
    }

    /// <summary>One-pole high pass: thins the bottom out.</summary>
    public static float[] HighPass(this float[] x, double hz)
    {
        double a = Math.Exp(-Tau * hz / Rate), y = 0, prev = 0;
        for (int i = 0; i < x.Length; i++) { double v = x[i]; y = a * (y + v - prev); prev = v; x[i] = (float)y; }
        return x;
    }

    /// <summary>A resonant band pass (RBJ biquad, constant peak gain). Q around 1 is broad, 10 is a ring.</summary>
    public static float[] BandPass(this float[] x, double hz, double q = 1) => Biquad(x, hz, q, kind: 0);
    /// <summary>A resonant low pass (RBJ biquad). Q 0.7 is flat; higher rings at the corner.</summary>
    public static float[] LowPassQ(this float[] x, double hz, double q = 0.707) => Biquad(x, hz, q, kind: 1);
    /// <summary>A resonant high pass (RBJ biquad).</summary>
    public static float[] HighPassQ(this float[] x, double hz, double q = 0.707) => Biquad(x, hz, q, kind: 2);

    /// <summary>Telephone line: a band pass of roughly 300-3400 Hz, as heard down an old copper pair.</summary>
    public static float[] Telephone(this float[] x) => x.HighPassQ(300, 0.7).LowPassQ(3400, 0.7);

    private static float[] Biquad(float[] x, double hz, double q, int kind)
    {
        hz = Math.Clamp(hz, 10, Rate * 0.45);
        double w = Tau * hz / Rate, cos = Math.Cos(w), alpha = Math.Sin(w) / (2 * q);
        double b0, b1, b2, a0 = 1 + alpha, a1 = -2 * cos, a2 = 1 - alpha;
        switch (kind)
        {
            case 0: b0 = alpha; b1 = 0; b2 = -alpha; break;
            case 1: b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = (1 - cos) / 2; break;
            default: b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = (1 + cos) / 2; break;
        }
        b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double v = x[i], y = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = v; y2 = y1; y1 = y; x[i] = (float)y;
        }
        return x;
    }

    /// <summary>A sweeping band pass: the centre frequency follows hzAt(t), t 0..1 through the buffer (wind gusts, a drill winding up).</summary>
    public static float[] BandSweep(this float[] x, Func<double, double> hzAt, double q = 1)
    {
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double hz = Math.Clamp(hzAt(i / (double)x.Length), 10, Rate * 0.45);
            double w = Tau * hz / Rate, cos = Math.Cos(w), alpha = Math.Sin(w) / (2 * q), a0 = 1 + alpha;
            double b0 = alpha / a0, b2 = -alpha / a0, a1 = -2 * cos / a0, a2 = (1 - alpha) / a0;
            double v = x[i], y = b0 * v + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = v; y2 = y1; y1 = y; x[i] = (float)y;
        }
        return x;
    }

    /// <summary>Exponential decay from full: the shape of anything struck. tau in seconds (time to fall to 37%).</summary>
    public static float[] Decay(this float[] x, double tau)
    {
        for (int i = 0; i < x.Length; i++) x[i] = (float)(x[i] * Math.Exp(-(i / (double)Rate) / tau));
        return x;
    }

    /// <summary>Attack then exponential decay. attack in seconds (linear), tau as in <see cref="Decay"/>.</summary>
    public static float[] Env(this float[] x, double attack, double tau)
    {
        int a = N(attack);
        for (int i = 0; i < x.Length; i++)
            x[i] = (float)(x[i] * (i < a ? i / (double)a : Math.Exp(-((i - a) / (double)Rate) / tau)));
        return x;
    }

    /// <summary>Attack, decay to sustain level, release over the last <paramref name="r"/> seconds (all linear). For held tones.</summary>
    public static float[] Adsr(this float[] x, double a, double d, double sustain, double r)
    {
        int na = N(a), nd = N(d), nr = N(r), n = x.Length;
        for (int i = 0; i < n; i++)
        {
            double g = i < na ? i / (double)na : i < na + nd ? 1 - (1 - sustain) * (i - na) / nd : sustain;
            if (i > n - nr) g *= (n - i) / (double)nr;
            x[i] = (float)(x[i] * g);
        }
        return x;
    }

    /// <summary>Short linear fades at each end (seconds) so nothing starts or stops with a click.</summary>
    public static float[] Fade(this float[] x, double fadeIn = 0.003, double fadeOut = 0.01)
    {
        int a = Math.Min(N(fadeIn), x.Length), b = Math.Min(N(fadeOut), x.Length);
        for (int i = 0; i < a; i++) x[i] *= i / (float)a;
        for (int i = 0; i < b; i++) x[x.Length - 1 - i] *= i / (float)b;
        return x;
    }

    /// <summary>Multiply by a shape over time, g(t) with t 0..1 through the buffer (tremolo, swells, gusts).</summary>
    public static float[] Shape(this float[] x, Func<double, double> g)
    {
        for (int i = 0; i < x.Length; i++) x[i] = (float)(x[i] * g(i / (double)x.Length));
        return x;
    }

    /// <summary>Multiply sample by sample with another buffer (ring modulation, or a noise burst shaped by an envelope buffer).</summary>
    public static float[] Mul(this float[] x, float[] by)
    {
        for (int i = 0; i < x.Length; i++) x[i] *= i < by.Length ? by[i] : 0;
        return x;
    }

    /// <summary>Soft saturation (tanh). drive 1 is gentle, 5 is crunchy. Keeps the peak at 1 or under.</summary>
    public static float[] Saturate(this float[] x, double drive = 2)
    {
        double k = Math.Tanh(drive);
        for (int i = 0; i < x.Length; i++) x[i] = (float)(Math.Tanh(x[i] * drive) / k);
        return x;
    }

    /// <summary>Crude bit-crush and sample hold: an old answering machine, a cheap speaker.</summary>
    public static float[] Crush(this float[] x, int bits = 6, int hold = 2)
    {
        float steps = (1 << (bits - 1)), h = 0;
        for (int i = 0; i < x.Length; i++) { if (i % Math.Max(1, hold) == 0) h = MathF.Round(x[i] * steps) / steps; x[i] = h; }
        return x;
    }

    /// <summary>A short room: a few early reflections and a small decaying tail. wet 0..1.</summary>
    public static float[] Room(this float[] x, double size = 1, double wet = 0.25)
    {
        var y = (float[])x.Clone();
        int[] taps = { 211, 347, 467, 619, 787, 953 };
        for (int t = 0; t < taps.Length; t++)
        {
            int d = (int)(taps[t] * size * Rate / 22050.0);
            float g = (float)(wet * Math.Pow(0.72, t));
            for (int i = d; i < y.Length; i++) y[i] += x[i - d] * g;
        }
        return Comb(y, (int)(1557 * size * Rate / 44100.0), 0.55 * wet).Normalize(Math.Max(0.01f, Peak(x)));
    }

    private static float[] Comb(float[] x, int d, double fb)
    {
        for (int i = d; i < x.Length; i++) x[i] += (float)(x[i - d] * fb);
        return x;
    }

    public static float Peak(float[] x) { float m = 0; foreach (var v in x) m = Math.Max(m, Math.Abs(v)); return m; }
    public static float Rms(float[] x) { double s = 0; foreach (var v in x) s += v * v; return (float)Math.Sqrt(s / Math.Max(1, x.Length)); }

    // ---------------------------------------------------------------- arranging

    /// <summary>Sum buffers (the result is as long as the longest).</summary>
    public static float[] Mix(params float[][] parts)
    {
        var x = new float[parts.Max(p => p.Length)];
        foreach (var p in parts) for (int i = 0; i < p.Length; i++) x[i] += p[i];
        return x;
    }

    /// <summary>Add <paramref name="src"/> into <paramref name="dst"/> starting at a time in seconds, scaled by gain. Grows nothing: what runs past the end is dropped.</summary>
    public static float[] MixAt(this float[] dst, float[] src, double atSeconds, double gain = 1)
    {
        int o = (int)(atSeconds * Rate);
        for (int i = 0; i < src.Length && o + i < dst.Length; i++) if (o + i >= 0) dst[o + i] += (float)(src[i] * gain);
        return dst;
    }

    /// <summary>One after another.</summary>
    public static float[] Concat(params float[][] parts)
    {
        var x = new float[parts.Sum(p => p.Length)];
        int o = 0;
        foreach (var p in parts) { Array.Copy(p, 0, x, o, p.Length); o += p.Length; }
        return x;
    }

    /// <summary>Copy of the first <paramref name="seconds"/> (or the whole thing if shorter).</summary>
    public static float[] Take(this float[] x, double seconds) => x[..Math.Min(x.Length, N(seconds))];

    /// <summary>Telephone cadence: the tone switched on and off in a pattern (seconds on, off, on, off ...), repeated to fill <paramref name="total"/> seconds. 5 ms ramps on each edge.</summary>
    public static float[] Cadence(this float[] tone, double total, params double[] onOff)
    {
        var x = new float[N(total)];
        double cycle = onOff.Sum();
        int ramp = N(0.005);
        for (int i = 0; i < x.Length; i++)
        {
            double t = (i / (double)Rate) % cycle, acc = 0;
            for (int k = 0; k < onOff.Length; k++)
            {
                if (t < acc + onOff[k])
                {
                    if (k % 2 == 0)
                    {
                        int into = (int)((t - acc) * Rate), left = (int)((acc + onOff[k] - t) * Rate);
                        float g = Math.Min(1f, Math.Min(into, left) / (float)ramp);
                        x[i] = tone[i % tone.Length] * g;
                    }
                    break;
                }
                acc += onOff[k];
            }
        }
        return x;
    }

    /// <summary>
    /// Make a buffer loop without a seam: the last <paramref name="xfade"/> seconds are cross-faded into the start, and the
    /// result is that much shorter. Use for every looping sound (wind, engine, room tone).
    /// </summary>
    public static float[] LoopSeam(this float[] x, double xfade = 0.25)
    {
        int f = Math.Min(N(xfade), x.Length / 3), n = x.Length - f;
        var y = new float[n];
        Array.Copy(x, y, n);
        for (int i = 0; i < f; i++)
        {
            double t = i / (double)f, a = Math.Sqrt(t), b = Math.Sqrt(1 - t);   // equal power
            y[i] = (float)(x[i] * a + x[n + i] * b);
        }
        return y;
    }

    /// <summary>Scatter copies of a short sound through a buffer at random times (rain of clicks, a flock of chirps, gravel).</summary>
    public static float[] Scatter(double seconds, int count, ref Rng r, Func<int, float[]> make, double gainSpread = 0.5)
    {
        var x = new float[N(seconds)];
        for (int k = 0; k < count; k++) x.MixAt(make(k), r.Next() * seconds, 1 - gainSpread * r.Next());
        return x;
    }
}
