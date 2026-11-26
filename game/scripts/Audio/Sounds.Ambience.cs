namespace Telo.Game.Audio;

/// <summary>
/// Ambience and engines: the outdoor bed, indoor room tone, the data centre hall, the van, the ferry (EA7).
/// Loops are made to repeat without a seam in one of two ways: tones and pulses are laid out on a circle exactly one loop
/// long (whole cycles, events wrapped round the end), and noise is built a little longer and its overhang cross-faded
/// into the start (<see cref="AmbSeam"/>). Mixes are set by RMS, layer by layer, so the balance doesn't depend on how
/// loud a filter happens to leave its noise. Helpers are prefixed Amb/Eng so they can't collide with the other Sounds files.
/// </summary>
public static partial class Sounds
{
    static partial void RegisterAmbience()
    {
        const string A = "ambience";
        Add(A, "amb.wind", "a quiet outdoor wind bed: slow gusts, a soft whoosh, leaves when it blows (loops, 11 s)", AmbWind, loop: true, gainDb: -20, bus: "Amb");
        Add(A, "amb.birds", "one bird call (8 kinds: trill, two-note, warble, wood pigeon, whistle, sparrows, falling trill, pink call)", AmbBird, variants: 8, gainDb: -9, bus: "Amb");
        Add(A, "amb.crickets", "crickets at night: a few close by, a chorus further off (loops, 6 s)", AmbCrickets, loop: true, gainDb: -21, bus: "Amb");
        Add(A, "amb.room", "room tone indoors: still air, a fridge humming in the next room (loops, 8 s)", AmbRoomTone, loop: true, gainDb: -24, bus: "Amb");
        Add(A, "amb.sea", "waves on the shore: three swells, each breaking and drawing back over the shingle (loops, 22 s)", AmbSea, loop: true, gainDb: -15, bus: "Amb");
        Add(A, "amb.hall", "the data centre hall: server fans, their whine, a deep electrical hum (loops, 6 s)", AmbHall, loop: true, gainDb: -18, bus: "Amb");
        Add(A, "van.engine", "the van's four-cylinder engine at idle, 800 rpm (loops, 1.5 s; pitched up with speed)", EngIdle, loop: true, gainDb: -10);
        Add(A, "van.start", "the van starting: starter motor cranking, the engine catches, revs and settles to idle", EngStart, gainDb: -10);
        Add(A, "van.stop", "the engine switched off: it runs down and shudders still", EngStop, gainDb: -10);
        Add(A, "ferry.horn", "the car ferry's horn: a deep two-tone blast, the hills answering across the water", FerryHorn, gainDb: -6);
    }

    // ================================================================ helpers

    /// <summary>Smoothstep of x, clamped to 0..1.</summary>
    private static double AmbS(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>A smooth random wander, 0..1, through <paramref name="points"/> control points that repeat every <paramref name="period"/> seconds.</summary>
    private static Func<double, double> AmbWander(ref Synth.Rng r, double period, int points)
    {
        var c = new double[points];
        for (int k = 0; k < points; k++) c[k] = r.Next();
        return s =>
        {
            double u = (s / period % 1 + 1) % 1 * points;
            int i = (int)u;
            double f = AmbS(u - i);
            return c[i % points] + (c[(i + 1) % points] - c[i % points]) * f;
        };
    }

    /// <summary>
    /// A noise layer for a loop exactly <paramref name="n"/> samples long: <paramref name="make"/> builds it
    /// <paramref name="xf"/> seconds longer (its argument is that length in seconds) and the overhang is cross-faded into
    /// the start. Anything time-varying inside should repeat every n samples so the overhang matches the start.
    /// </summary>
    private static float[] AmbSeam(int n, double xf, Func<double, float[]> make)
    {
        int f = Synth.N(xf);
        return AmbFit(make((n + f) / (double)Synth.Rate).LoopSeam(xf), n);
    }

    private static float[] AmbFit(float[] x, int n)
    {
        if (x.Length == n) return x;
        var y = new float[n];
        Array.Copy(x, y, Math.Min(n, x.Length));
        return y;
    }

    /// <summary>Multiply by g(seconds).</summary>
    private static float[] AmbShape(float[] x, Func<double, double> g)
    {
        for (int i = 0; i < x.Length; i++) x[i] = (float)(x[i] * g(i / (double)Synth.Rate));
        return x;
    }

    /// <summary>A low pass whose corner follows hz(seconds): a wave's brightness as it breaks. poles 2 is steeper.</summary>
    private static float[] AmbSweepLow(float[] x, Func<double, double> hzAt, int poles = 1)
    {
        double a = 0;
        var y = new double[poles];
        for (int i = 0; i < x.Length; i++)
        {
            if ((i & 31) == 0) a = 1 - Math.Exp(-Math.Tau * Math.Clamp(hzAt(i / (double)Synth.Rate), 5, 20000) / Synth.Rate);
            double v = x[i];
            for (int p = 0; p < poles; p++) { y[p] += a * (v - y[p]); v = y[p]; }
            x[i] = (float)v;
        }
        return x;
    }

    /// <summary>Add a sine that repeats exactly over the buffer (the frequency is nudged to a whole number of cycles).</summary>
    private static float[] AmbTone(float[] x, double hz, double amp, double phase = 0)
    {
        double cycles = Math.Max(1, Math.Round(hz * x.Length / Synth.Rate)), w = Math.Tau * cycles / x.Length;
        for (int i = 0; i < x.Length; i++) x[i] += (float)(amp * Math.Sin(phase + w * i));
        return x;
    }

    /// <summary>Add src into dst at sample <paramref name="at"/>, wrapping round the end (dst is a loop).</summary>
    private static void AmbAddWrapped(float[] dst, float[] src, int at, double gain)
    {
        int n = dst.Length;
        at = (at % n + n) % n;
        for (int i = 0; i < src.Length; i++) dst[(at + i) % n] += (float)(src[i] * gain);
    }

    /// <summary>Delayed copies round a loop (a little space that doesn't break the seam).</summary>
    private static float[] AmbCircEcho(float[] x, params (double delay, double gain)[] taps)
    {
        int n = x.Length;
        var y = (float[])x.Clone();
        foreach (var (d, g) in taps)
        {
            int k = Synth.N(d) % n;
            for (int i = 0; i < n; i++) y[i] += (float)(g * x[(i - k + n) % n]);
        }
        return y;
    }

    /// <summary>Scale to a given RMS level (how the layers of a bed are balanced).</summary>
    private static float[] AmbRms(float[] x, double rms)
    {
        float now = Synth.Rms(x);
        return now < 1e-9f ? x : x.Gain(rms / now);
    }

    /// <summary>Remove the average (a loop can't be DC-blocked without breaking its seam; a constant can).</summary>
    private static float[] AmbCentre(float[] x)
    {
        double m = 0;
        foreach (var v in x) m += v;
        m /= Math.Max(1, x.Length);
        for (int i = 0; i < x.Length; i++) x[i] -= (float)m;
        return x;
    }

    /// <summary>
    /// Finish a loop: remove the DC with the same filter <see cref="Render"/> uses on one-shots, run round the circle so it
    /// has no start-up, then turn the loop so it begins at its quietest zero crossing (still seamless; the seam check reads cleanly).
    /// </summary>
    private static float[] AmbLoop(float[] x, float peak = 0.9f)
    {
        int n = x.Length;
        var y = new float[n];
        double o = 0, prev = 0;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++) { double v = x[i]; o = v - prev + 0.995 * o; prev = v; if (pass == 1) y[i] = (float)o; }
        int best = 0;
        double bestScore = double.MaxValue;
        for (int i = 0; i < n; i++)
        {
            double a = y[(i - 1 + n) % n], b = y[i];
            if (a > 0 == b > 0) continue;
            double score = Math.Abs(b - a) + Math.Abs(a) + Math.Abs(b);
            if (score < bestScore) { bestScore = score; best = i; }
        }
        var z = new float[n];
        for (int i = 0; i < n; i++) z[i] = y[(i + best) % n];
        return z.Normalize(peak);
    }

    private static float[] AmbSum(int n, params (float[] x, double g)[] parts)
    {
        var y = new float[n];
        foreach (var (x, g) in parts)
            for (int i = 0; i < n && i < x.Length; i++) y[i] += (float)(x[i] * g);
        return y;
    }

    // ================================================================ wind

    private static float[] AmbWind(int v)
    {
        var r = new Synth.Rng(Seed("amb.wind", v));
        const double L = 11.0, Xf = 1.5;
        int n = Synth.N(L);
        var gust = AmbWander(ref r, L, 5);       // a gust every few seconds
        var flutter = AmbWander(ref r, L, 31);   // the quicker unrest inside it
        var swirl = AmbWander(ref r, L, 9);
        ulong s1 = r.NextU(), s2 = r.NextU(), s3 = r.NextU();
        // gust strength 0..1: never quite still, never a gale
        double G(double s) => 0.1 + 0.9 * AmbS(gust(s) * 1.3 - 0.15) * (0.8 + 0.2 * flutter(s));

        // the body: soft low air moving past the ears
        var body = AmbSeam(n, Xf, sec => AmbShape(Synth.Pink(sec, s1).HighPass(55).LowPass(480).LowPass(900), s => 0.45 + 0.55 * G(s)));
        // the whoosh: a broad band that rises as a gust comes through (Q low, so it breathes rather than howls)
        var band = AmbSeam(n, Xf, sec => AmbShape(Synth.Pink(sec, s2).BandSweep(t => 230 + 560 * G(t * sec) + 150 * swirl(t * sec), 1.2),
            s => 0.12 + 0.88 * G(s) * G(s)));
        // leaves and grass, only when it blows
        var leaves = AmbSeam(n, Xf, sec => AmbShape(Synth.Pink(sec, s3).HighPassQ(1500).LowPassQ(5200), s => Math.Pow(G(s), 2.6)));
        return AmbLoop(AmbSum(n, (AmbRms(body, 0.16), 1), (AmbRms(band, 0.07), 1), (AmbRms(leaves, 0.022), 1)));
    }

    // ================================================================ birds

    /// <summary>One note of birdsong: a sine following hz(t) with a touch of its octave, shaped by amp(t); t is 0..1 through the note.</summary>
    private static float[] AmbNote(double dur, Func<double, double> hz, Func<double, double> amp, double octave = 0.1)
    {
        var x = new float[Synth.N(dur)];
        double ph = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double t = i / (double)x.Length;
            ph += Math.Tau * hz(t) / Synth.Rate;
            x[i] = (float)(amp(t) * (Math.Sin(ph) + octave * Math.Sin(2 * ph + 0.6)));
        }
        return x;
    }

    /// <summary>A note's envelope: up over the first <paramref name="a"/> of it, down over the last <paramref name="d"/> (fractions of the note).</summary>
    private static Func<double, double> AmbEnv(double a, double d) => t => AmbS(t / a) * AmbS((1 - t) / d);

    private static float[] AmbBird(int v)
    {
        var r = new Synth.Rng(Seed("amb.birds", v));
        float[] x;
        switch (v % 8)
        {
            case 0:   // a chirping trill that speeds up and ends in a flourish (chaffinch-ish)
            {
                x = Synth.Silence(1.0);
                int notes = 7 + r.Int(3);
                double at = 0.02, gap = 0.085;
                for (int k = 0; k < notes; k++)
                {
                    double top = 4500 * (1 + 0.04 * r.Bi()), bot = 3000 * (1 + 0.05 * r.Bi());
                    double lvl = 0.55 + 0.45 * Math.Sin(Math.PI * (k + 0.5) / notes);
                    x.MixAt(AmbNote(0.035, t => top * Math.Pow(bot / top, t), AmbEnv(0.12, 0.5)), at, lvl);
                    at += gap; gap *= 0.96;
                }
                x.MixAt(AmbNote(0.07, t => 3100 + 1200 * AmbS(t * 1.4), AmbEnv(0.15, 0.4)), at + 0.02, 0.8);
                break;
            }
            case 1:   // a two-note call, high-low, three or four times ("tea-cher", great tit-ish)
            {
                x = Synth.Silence(1.3);
                double hi = 4100 * (1 + 0.03 * r.Bi()), lo = 2950 * (1 + 0.03 * r.Bi());
                int pairs = 3 + r.Int(2);
                for (int k = 0; k < pairs; k++)
                {
                    double t0 = 0.02 + k * 0.29;
                    x.MixAt(AmbNote(0.1, t => hi * (1.02 - 0.04 * t), AmbEnv(0.1, 0.3)), t0, 1);
                    x.MixAt(AmbNote(0.11, t => lo * (1.03 - 0.05 * t), AmbEnv(0.1, 0.35), 0.14), t0 + 0.13, 0.85);
                }
                break;
            }
            case 2:   // a warble: wandering pitch with a fast vibrato, chopped into syllables (robin-ish)
            {
                const double dur = 1.2;
                var w = AmbWander(ref r, dur, 11);
                var w2 = AmbWander(ref r, dur, 7);
                double rate = 13 + 4 * r.Next(), syl = 7 + 2 * r.Next();
                var env = AmbEnv(0.04, 0.1);
                x = AmbNote(dur, t => 2500 + 1400 * w(t * dur) + 380 * Math.Sin(Math.Tau * rate * t * dur),
                    t => env(t) * Math.Pow(Math.Max(0, Math.Sin(Math.Tau * syl * t * dur + 2 * w2(t * dur))), 1.2), 0.08);
                break;
            }
            case 3:   // a wood pigeon: soft low coos, coo-COO-coo, coo-coo
            {
                x = Synth.Silence(1.6);
                double f = 470 * (1 + 0.04 * r.Bi());
                (double at, double len, double rise, double lvl)[] coos =
                    { (0.02, 0.2, 1.0, 0.6), (0.27, 0.36, 1.12, 1.0), (0.68, 0.17, 1.0, 0.65), (1.02, 0.2, 1.03, 0.75), (1.27, 0.2, 1.0, 0.7) };
                foreach (var (at, len, rise, lvl) in coos)
                    x.MixAt(AmbNote(len, t => f * (1 + (rise - 1) * Math.Sin(Math.PI * t)), AmbEnv(0.3, 0.45), 0.3), at, lvl);
                var level = x.Select(a => Math.Abs(a)).ToArray().LowPass(30);   // how loud the coo is, moment to moment
                var breath = Synth.Noise(1.6, ref r).BandPass(900, 1.5).LowPass(2200).Mul(level);
                x.LowPassQ(1700).MixAt(breath, 0, 0.6);
                break;
            }
            case 4:   // a fluting whistle: up, held with a waver, a drop, then a little twiddle (blackbird-ish)
            {
                x = Synth.Silence(1.1);
                double k = 1 + 0.05 * r.Bi();
                const double len = 0.56;
                var env = AmbEnv(0.12, 0.15);
                x.MixAt(AmbNote(len, t =>
                {
                    double u = t * len;
                    return k * (u < 0.24 ? 1900 + 900 * AmbS(u / 0.24) : u < 0.40 ? 2800 + 50 * Math.Sin(Math.Tau * 22 * u) : 3150 - 950 * AmbS((u - 0.40) / 0.16));
                }, t => env(t) * (1 - 0.75 * Math.Exp(-Math.Pow((t * len - 0.40) / 0.015, 2))), 0.18), 0.02, 1);
                (double a, double b)[] twiddle = { (3400, 3000), (3000, 3600), (3500, 3100) };
                for (int j = 0; j < twiddle.Length; j++)
                {
                    var (a, b) = twiddle[j];
                    x.MixAt(AmbNote(0.045, t => k * (a + (b - a) * t), AmbEnv(0.15, 0.4), 0.15), 0.66 + j * 0.065, 0.7);
                }
                break;
            }
            case 5:   // sparrows chattering: a run of tiny random chirps
            {
                x = Synth.Silence(1.0);
                double at = 0.02;
                while (at < 0.85)
                {
                    double len = 0.018 + 0.02 * r.Next(), f0 = 3300 + 1900 * r.Next(), f1 = Math.Min(5600, f0 * (0.75 + 0.5 * r.Next()));
                    x.MixAt(AmbNote(len, t => f0 + (f1 - f0) * t, AmbEnv(0.15, 0.4), 0.12), at, 0.5 + 0.5 * r.Next());
                    at += len + 0.025 + 0.05 * r.Next();
                }
                break;
            }
            case 6:   // a falling trill, each note a little up-slur, the whole run stepping down (willow warbler-ish)
            {
                x = Synth.Silence(1.5);
                int notes = 11 + r.Int(3);
                double at = 0.02, gap = 0.1;
                for (int k = 0; k < notes; k++)
                {
                    double u = k / (double)(notes - 1), f = 4700 - 1800 * Math.Pow(u, 0.8);
                    x.MixAt(AmbNote(0.055, t => f * (0.93 + 0.12 * t), AmbEnv(0.2, 0.4), 0.1), at, AmbS(u * 4 + 0.2) * (1 - 0.5 * u));
                    at += gap; gap = Math.Max(0.08, gap * 0.985);
                }
                break;
            }
            default:   // a sharp "pink, pink" call
            {
                x = Synth.Silence(0.75);
                int calls = 2 + r.Int(2);
                for (int k = 0; k < calls; k++)
                    x.MixAt(AmbNote(0.05, t => 3700 + 800 * Math.Sin(Math.PI * Math.Min(1, t * 1.3)), AmbEnv(0.06, 0.5), 0.15), 0.02 + k * 0.23, 1 - 0.1 * k);
                break;
            }
        }
        // soften the top, and a little of the tree it's sitting in
        return x.LowPassQ(7000).Room(1.2, 0.1).Normalize(0.9f);
    }

    // ================================================================ crickets

    /// <summary>One cricket round a loop: chirps of a few carrier pulses, evenly but not exactly spaced.</summary>
    private static void AmbCricket(float[] x, double hz, int chirps, int pulses, double on, double every, double lvl, ref Synth.Rng r)
    {
        int n = x.Length;
        double L = n / (double)Synth.Rate;
        hz = Math.Round(hz * L) / L;   // whole cycles round the loop, so the carrier is the same either side of the seam
        double phase = r.Next() * Math.Tau, drift = 0.15;
        int pon = Synth.N(on);
        for (int c = 0; c < chirps; c++)
        {
            double t0 = (c + 0.5 + drift * r.Bi()) * L / chirps, cl = lvl * (0.8 + 0.2 * r.Next());
            for (int p = 0; p < pulses; p++)
            {
                int s0 = (int)((t0 + p * every) * Synth.Rate);
                double pl = cl * (p == 0 ? 0.75 : 1.0);
                for (int j = 0; j < pon; j++)
                {
                    int i = (s0 + j) % n;
                    double e = Math.Sin(Math.PI * j / pon);
                    x[i] += (float)(pl * e * e * Math.Sin(phase + Math.Tau * hz * i / Synth.Rate));
                }
            }
        }
    }

    private static float[] AmbCrickets(int v)
    {
        var r = new Synth.Rng(Seed("amb.crickets", v));
        int n = Synth.N(6.0);
        var x = new float[n];
        // close by: (carrier, chirps per loop, pulses per chirp, pulse length, pulse spacing, level)
        (double hz, int chirps, int pulses, double on, double every, double lvl)[] near =
        {
            (4500, 9, 3, 0.014, 0.028, 1.0), (4720, 8, 4, 0.012, 0.026, 0.7), (4310, 11, 3, 0.015, 0.030, 0.55),
            (4950, 7, 4, 0.011, 0.024, 0.45), (4180, 13, 2, 0.016, 0.032, 0.35), (4620, 10, 3, 0.013, 0.027, 0.3),
        };
        foreach (var c in near) AmbCricket(x, c.hz, c.chirps, c.pulses, c.on, c.every, c.lvl, ref r);
        // a chorus further off
        for (int k = 0; k < 9; k++)
            AmbCricket(x, 4000 + 1100 * r.Next(), 6 + r.Int(10), 2 + r.Int(3), 0.011 + 0.006 * r.Next(), 0.026 + 0.008 * r.Next(), 0.07 + 0.1 * r.Next(), ref r);
        // a tree cricket trilling on and off
        AmbCricket(x, 3900, 3, 55, 0.011, 0.021, 0.16, ref r);
        return AmbLoop(AmbCircEcho(x, (0.023, 0.22), (0.041, 0.14), (0.067, 0.08)));
    }

    // ================================================================ room tone

    private static float[] AmbRoomTone(int v)
    {
        var r = new Synth.Rng(Seed("amb.room", v));
        const double L = 8.0;
        int n = Synth.N(L);
        ulong s1 = r.NextU(), s2 = r.NextU();
        var air = AmbSeam(n, 1.0, sec => Synth.Brown(sec, s1).HighPass(25).LowPass(220));
        var hiss = AmbSeam(n, 1.0, sec => Synth.Pink(sec, s2).HighPass(150).LowPass(1400));
        // the fridge next door: 50 Hz mains, its compressor's 100 Hz buzz and a few harmonics, through the wall
        var hum = new float[n];
        (double hz, double a)[] parts = { (50, 0.35), (100, 1.0), (150, 0.3), (200, 0.22), (250, 0.06), (300, 0.1), (400, 0.04) };
        foreach (var (hz, a) in parts) AmbTone(hum, hz, a, r.Next() * Math.Tau);
        AmbShape(hum, s => 1 + 0.08 * Math.Sin(Math.Tau * 0.25 * s));   // the compressor wavers (two cycles a loop)
        return AmbLoop(AmbSum(n, (AmbRms(air, 0.12), 1), (AmbRms(hiss, 0.025), 1), (AmbRms(hum, 0.03), 1)));
    }

    // ================================================================ sea

    private static float[] AmbSea(int v)
    {
        var r = new Synth.Rng(Seed("amb.sea", v));
        const double L = 22.0, Xf = 1.5;
        int n = Synth.N(L);
        double[] at = { 0.6 + 0.3 * r.Bi(), 7.9 + 0.4 * r.Bi(), 16.2 + 0.4 * r.Bi() };   // when each wave breaks
        double[] big = { 1.0, 0.78 + 0.1 * r.Next(), 0.9 + 0.1 * r.Next() };
        var far = AmbWander(ref r, L, 6);
        var grit = AmbWander(ref r, L, 420);   // the shingle's rattle under the hiss, about 20 changes a second
        ulong s1 = r.NextU(), s2 = r.NextU(), s3 = r.NextU(), s4 = r.NextU();

        // seconds since wave k broke (negative before), round the loop
        double U(double s, int k) { double u = ((s - at[k]) % L + L) % L; return u > L * 0.6 ? u - L : u; }
        double Sum(double s, Func<double, double> one) { double y = 0; for (int k = 0; k < at.Length; k++) y += big[k] * one(U(s, k)); return y; }
        double Swell(double s) => Sum(s, u => u < 0 ? AmbS((u + 2.4) / 2.4) : Math.Exp(-u / 1.8));                  // the water heaping up, then falling
        double Crash(double s) => Sum(s, u => u < 0 ? Math.Pow(AmbS((u + 0.7) / 0.7), 1.5) : Math.Exp(-u / 1.4));     // the break
        double Wash(double s) => Sum(s, u => AmbS((u - 0.6) / 1.0) * Math.Exp(-Math.Max(0, u - 1.6) / 2.0));       // drawing back over the stones

        var rumble = AmbSeam(n, Xf, sec => AmbShape(Synth.Brown(sec, s1).HighPass(30).LowPass(280), s => 0.35 + 0.65 * Swell(s)));
        var crash = AmbSeam(n, Xf, sec => AmbShape(AmbSweepLow(Synth.Pink(sec, s2).HighPass(60), s => 280 + 3000 * Math.Min(1, Crash(s)), 2), s => 0.06 + Crash(s)));
        var wash = AmbSeam(n, Xf, sec => AmbShape(Synth.Noise(sec, s3).HighPassQ(1500).LowPassQ(7000), s => Wash(s) * (0.5 + 0.5 * grit(s))));
        var distant = AmbSeam(n, Xf, sec => AmbShape(Synth.Pink(sec, s4).HighPass(80).LowPass(600), s => 0.7 + 0.3 * far(s)));
        return AmbLoop(AmbSum(n, (AmbRms(rumble, 0.1), 1), (AmbRms(crash, 0.1), 1), (AmbRms(wash, 0.045), 1), (AmbRms(distant, 0.065), 1)));
    }

    // ================================================================ data centre hall

    private static float[] AmbHall(int v)
    {
        var r = new Synth.Rng(Seed("amb.hall", v));
        const double L = 6.0;
        int n = Synth.N(L);
        var wob = AmbWander(ref r, L, 12);
        ulong s1 = r.NextU(), s2 = r.NextU(), s3 = r.NextU();
        // hundreds of fans: broadband air with a lift around a kilohertz or so
        var air = AmbSeam(n, 0.8, sec =>
        {
            var p = Synth.Pink(sec, s1).HighPass(90);
            var lift = ((float[])p.Clone()).BandPass(1200, 0.7);
            var hiss = Synth.Noise(sec, s2).HighPass(500).LowPass(5000);
            return AmbShape(Synth.Mix(p, lift.Gain(1.4), hiss.Gain(0.08)).LowPass(6500), s => 0.94 + 0.06 * wob(s));
        });
        // the air handlers: a low rumble
        var rumble = AmbSeam(n, 0.8, sec => Synth.Brown(sec, s3).HighPass(25).LowPass(110));
        // fan whines: blade-pass tones of a few fan sizes, pairs a hair apart so they beat slowly
        var whine = new float[n];
        (double hz, double a)[] fans = { (587, 1.0), (588.5, 0.8), (742, 0.7), (743, 0.6), (1174, 0.35), (1486, 0.3), (2963, 0.12), (3310, 0.08) };
        foreach (var (hz, a) in fans) AmbTone(whine, hz, a, r.Next() * Math.Tau);
        // the mains: transformers and UPS, 50 Hz and its harmonics
        var hum = new float[n];
        (double hz, double a)[] mains = { (50, 0.5), (100, 1.0), (150, 0.45), (200, 0.2), (250, 0.15), (300, 0.12), (350, 0.06) };
        foreach (var (hz, a) in mains) AmbTone(hum, hz, a, r.Next() * Math.Tau);
        return AmbLoop(AmbSum(n, (AmbRms(air, 0.16), 1), (AmbRms(rumble, 0.06), 1), (AmbRms(whine, 0.055), 1), (AmbRms(hum, 0.05), 1)));
    }

    // ================================================================ the van's engine

    /// <summary>The firing rate at idle: 800 rpm, four cylinders, a firing every half turn (26.7 a second).</summary>
    private const double EngIdleHz = 40 / 1.5;
    /// <summary>How loud the engine's own note is against its firing pulses, and the drive into the saturator (shared, so start, idle and stop match).</summary>
    private const double EngBody = 0.55, EngDrive = 1.6;
    /// <summary>One gain for every engine sound, set so the idle peaks at about 0.55 going into the saturator.</summary>
    private static readonly Lazy<double> EngScale = new(() => 0.55 / Math.Max(1e-6, Synth.Peak(EngIdleRaw(0))));
    private static readonly double[] EngCyl = { 1.0, 0.84, 0.95, 0.78 };   // a slightly uneven engine: the lope

    /// <summary>One cylinder firing: a low thump through the exhaust and body, an exhaust crack, a diesel tick. c (0-3) shifts the colour a little.</summary>
    private static float[] EngFiring(int c, ref Synth.Rng r)
    {
        double k = 1 + 0.035 * (c - 1.5);
        var thump = Synth.Modal(0.1, 1, new[] { (60 * k, 1.0, 0.02), (115 * k, 0.75, 0.013), (210 * k, 0.5, 0.008), (340 * k, 0.3, 0.005), (590 * k, 0.14, 0.003) }).Normalize(1f);
        var crack = Synth.Noise(0.03, ref r).BandPass(750 * k, 1.1).Decay(0.005).Normalize(0.22f);
        var tick = Synth.Noise(0.015, ref r).BandPass(1900 * k, 2.5).Decay(0.0018).Normalize(0.12f);
        return Synth.Mix(thump, crack, tick).Fade(0.0005, 0.01);
    }

    /// <summary>The engine's note at firing phase ph (radians of the firing rate): its low harmonics, a half-order lope.</summary>
    private static double EngNote(double ph, double hz)
    {
        double b = 0.25 * Math.Sin(0.5 * ph) + 0.18 * Math.Sin(1.5 * ph + 0.4);
        for (int h = 1; h <= 14 && h * hz < 6000; h++) b += (h == 1 ? 0.5 : 1.0 / Math.Pow(h, 0.9)) * Math.Sin(h * ph + 0.3 * h);
        return b * 0.2;
    }

    private static float[] EngOut(float[] x) => x.Gain(EngScale.Value).Saturate(EngDrive);

    private static float[] EngIdle(int v)
    {
        var x = EngOut(EngIdleRaw(v));
        return AmbLoop(x, Synth.Peak(x));   // keep the shared level, so the start and stop match it
    }

    /// <summary>The idle loop before the shared gain and saturation.</summary>
    private static float[] EngIdleRaw(int v)
    {
        var r = new Synth.Rng(Seed("van.engine", v));
        const double L = 1.5;
        int n = Synth.N(L), firings = (int)Math.Round(EngIdleHz * L);   // 40 firings, ten turns of the cycle
        var fire = new float[4][];
        for (int c = 0; c < 4; c++) fire[c] = EngFiring(c, ref r);
        var x = new float[n];
        double step = n / (double)firings;
        for (int k = 0; k < firings; k++)
            AmbAddWrapped(x, fire[k % 4], (int)Math.Round(k * step + step * 0.025 * r.Bi()), EngCyl[k % 4] * (1 + 0.07 * r.Bi()));
        // its note, laid out round the loop in whole cycles
        double w = Math.Tau * firings / n;
        for (int i = 0; i < n; i++) x[i] += (float)(EngBody * EngNote(w * i, EngIdleHz));
        // the mechanical whirr of the rest of it
        ulong s = r.NextU();
        var mech = AmbSeam(n, 0.25, sec => Synth.Pink(sec, s).HighPass(500).LowPass(2500));
        return AmbSum(n, (x, 1), (AmbRms(mech, 0.012), 1));
    }

    /// <summary>The engine with its firing rate following hz(seconds) and its loudness amp(seconds): catching, revving, dying.</summary>
    private static float[] EngVoice(double dur, Func<double, double> hz, Func<double, double> amp, ref Synth.Rng r, double firstPhase = 0.9)
    {
        int n = Synth.N(dur);
        var fire = new float[4][];
        for (int c = 0; c < 4; c++) fire[c] = EngFiring(c, ref r);
        var x = new float[n];
        double ph = firstPhase, nph = 0;
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            double s = i / (double)Synth.Rate, f = Math.Max(0, hz(s)), a = Math.Max(0, amp(s));
            ph += f / Synth.Rate;
            if (ph >= 1)
            {
                ph -= 1;
                x.MixAt(fire[k % 4], s, a * EngCyl[k % 4] * (1 + 0.07 * r.Bi()));
                k++;
            }
            nph += Math.Tau * f / Synth.Rate;
            x[i] += (float)(EngBody * a * EngNote(nph, f));
        }
        return x;
    }

    private static float[] EngStart(int v)
    {
        var r = new Synth.Rng(Seed("van.start", v));
        const double Dur = 2.25, Catch = 0.74;
        // the starter: its motor whirring, slowing a little against each compression, the engine chugging over
        const double crankHz = 6.7;   // compressions a second at 200 rpm
        var crank = new float[Synth.N(0.95)];
        double pm = 0;
        for (int i = 0; i < crank.Length; i++)
        {
            double s = i / (double)Synth.Rate, c = s * crankHz % 1, load = Math.Exp(-Math.Pow((c - 0.5) / 0.14, 2));
            double fm = 52 * (0.6 + 0.4 * AmbS(s / 0.12)) * (1 - 0.13 * load);
            pm += Math.Tau * fm / Synth.Rate;
            double y = 0;
            for (int h = 1; h * fm < 3200; h++)
            {
                double fh = h * fm;
                y += (Math.Exp(-Math.Pow((fh - 950) / 420, 2)) + 0.25 / h) * Math.Sin(h * pm + 0.7 * h);
            }
            double env = AmbS(s / 0.03) * (1 - AmbS((s - Catch - 0.04) / 0.14));
            crank[i] = (float)(y * env * (0.75 + 0.25 * (1 - load)));
        }
        crank = AmbRms(crank, 0.09);
        var brushes = Synth.Noise(0.95, ref r).BandPass(3200, 1.5);
        AmbShape(brushes, s => AmbS(s / 0.03) * (1 - AmbS((s - Catch - 0.04) / 0.14)));
        crank.MixAt(AmbRms(brushes, 0.012), 0);
        var chug = Synth.Modal(0.14, 1, new[] { (55.0, 1.0, 0.04), (95.0, 0.5, 0.025), (160.0, 0.3, 0.015) }).Normalize(0.5f);
        for (double t = 0.5 / crankHz; t < Catch; t += 1 / crankHz) crank.MixAt(chug, t, 0.7 + 0.3 * r.Next());

        // the engine catches: a stumble, a rev, then down to idle (faded at the end, where the loop takes over)
        double Hz(double s) => s < Catch ? 0 : s < 1.0 ? 14 + 32 * AmbS((s - Catch) / 0.26) : EngIdleHz + (46 - EngIdleHz) * Math.Exp(-(s - 1.0) / 0.25);
        double Amp(double s) => (s < Catch ? 0 : s < 1.0 ? 0.75 + 0.5 * AmbS((s - Catch) / 0.26) : 1 + 0.25 * Math.Exp(-(s - 1.0) / 0.25)) * (1 - AmbS((s - 1.75) / 0.48));
        var engine = EngVoice(Dur, Hz, Amp, ref r, firstPhase: 0.95);
        var x = new float[Synth.N(Dur)];
        x.MixAt(crank, 0).MixAt(engine, 0);
        return EngOut(x);
    }

    private static float[] EngStop(int v)
    {
        var r = new Synth.Rng(Seed("van.stop", v));
        const double Dur = 0.95, Die = 0.55;
        // the firing slows and weakens to nothing
        var x = EngVoice(Dur, s => s < Die ? EngIdleHz * Math.Pow(1 - s / Die, 0.55) : 0, s => s < Die ? 1 - 0.6 * s / Die : 0, ref r, firstPhase: 0.97);
        // the engine rocks back on its mounts, and something rattles
        x.MixAt(Synth.Modal(0.38, 1, new[] { (38.0, 1.0, 0.07), (70.0, 0.5, 0.05), (130.0, 0.25, 0.03) }).Fade(0.002, 0.12).Normalize(0.55f), Die - 0.03);
        x.MixAt(Synth.Noise(0.06, ref r).BandPass(1200, 3).Decay(0.02).Normalize(0.06f), Die - 0.02);
        return EngOut(x);
    }

    // ================================================================ the ferry

    private static float[] FerryHorn(int v)
    {
        var r = new Synth.Rng(Seed("ferry.horn", v));
        const double Blast = 2.1, Total = 4.8;
        var drift = AmbWander(ref r, Blast, 7);
        var blast = new float[Synth.N(Blast)];
        // two reeds a major third apart; the pitch comes up as the air pressure builds and sags as it runs out
        foreach (var f0 in new[] { 104.0, 131.0 })
        {
            double ph = r.Next() * Math.Tau;
            for (int i = 0; i < blast.Length; i++)
            {
                double s = i / (double)Synth.Rate;
                double bend = 0.955 + 0.045 * AmbS(s / 0.18) - 0.02 * AmbS((s - (Blast - 0.32)) / 0.32) + 0.004 * (drift(s) - 0.5);
                ph += Math.Tau * f0 * bend / Synth.Rate;
                double y = 0;
                for (int h = 1; h * f0 < 4500; h++) y += Math.Sin(h * ph) / Math.Pow(h, 0.75);
                blast[i] += (float)y;
            }
        }
        // the horn's flare: a broad resonance that gives it the brass, over a darker body
        var flare = ((float[])blast.Clone()).BandPass(420, 0.9);
        blast = Synth.Mix(blast.LowPass(1600).Normalize(0.5f), flare.Normalize(1f));
        blast.Saturate(1.4).LowPassQ(2800, 0.6);
        var breath = Synth.Noise(Blast, ref r).BandPass(700, 1.2).Normalize(0.05f);
        blast.MixAt(breath, 0);
        AmbShape(blast, s => AmbS(s / 0.1) * (1 - AmbS((s - (Blast - 0.28)) / 0.28)));
        blast.Normalize(1f);
        // the hills across the water answer, darker each time
        var x = new float[Synth.N(Total)];
        x.MixAt(blast, 0);
        x.MixAt(((float[])blast.Clone()).LowPass(900).LowPass(1400), 1.25, 0.3);
        x.MixAt(((float[])blast.Clone()).LowPass(600).LowPass(800), 2.5, 0.12);
        return x.DcBlock().Normalize(0.8f);
    }
}
