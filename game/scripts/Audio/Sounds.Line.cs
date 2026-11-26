namespace Telo.Game.Audio;

/// <summary>The line: telephone tones, the switchboard, the test set, PIP, the answering machine and screens (EA7).</summary>
public static partial class Sounds
{
    static partial void RegisterLine()
    {
        const string L = "line";
        // Heard in a handset: band-limited to the phone line (300-3400 Hz), a carbon earpiece's grit, the line's hiss.
        // British tones, to suit the small town: dial 350+450 Hz, engaged 400 Hz, ringing 400+450 Hz in double rings.
        Add(L, "line.dial", "dial tone down the line: 350 + 450 Hz, steady (loops)", LineKit.Dial, loop: true, gainDb: -20);
        Add(L, "line.dead", "a dead line: the click, then nothing but faint hiss", LineKit.Dead, gainDb: -12);
        Add(L, "line.engaged", "the engaged tone, 400 Hz on and off, four cycles", LineKit.Engaged, gainDb: -22);
        Add(L, "line.ringback", "ringing heard by the caller: two double rings", LineKit.Ringback, gainDb: -20);
        Add(L, "line.crossed", "a crossed line: someone else's call murmuring faintly (no words)", LineKit.Crossed, gainDb: -20);
        Add(L, "line.modem", "a 300-baud modem handshake down the line: answer tone, carriers, data warble", LineKit.Modem, gainDb: -21);
        Add(L, "line.hook", "a handset lifted (0) or put down (1) on the cradle: plastic and the hook switch", LineKit.Hook, variants: 2, gainDb: -10);
        Add(L, "phone.ring", "a house telephone's bell, one double ring, heard in the room", LineKit.Ring, gainDb: -10);
        // The switchboard: a frame of spring terminals, jumper wires, port lamps and relays.
        Add(L, "sw.jumper.in", "a jumper wire pushed into a spring terminal: scrape, click", LineKit.JumperIn, variants: 3, gainDb: -10);
        Add(L, "sw.jumper.out", "a jumper wire pulled out: a short zip, the spring letting go", LineKit.JumperOut, variants: 3, gainDb: -13);
        Add(L, "sw.relay", "a relay clicking over on the switchboard", LineKit.RelayOne, variants: 3, gainDb: -11);
        Add(L, "sw.probe", "the tone probe's warble as it reads a tag, from its tiny speaker", LineKit.Probe, gainDb: -20);
        Add(L, "sw.test.ok", "the test set: a good line (two cheerful beeps)", LineKit.TestOk, gainDb: -17);
        Add(L, "sw.test.bad", "the test set: a bad port (a low flat buzz)", LineKit.TestBad, gainDb: -17);
        Add(L, "sw.throw", "the big switch: a heavy clunk, then relays rippling across the board", LineKit.Throw, gainDb: -6);
        Add(L, "sw.card", "a line card slid into the rack and seated", LineKit.Card, gainDb: -10);
        // PIP, the pocket handset from the future, sounding like a small 1980s pager.
        Add(L, "pip.beep", "PIP's pager beep: something needs you (bip-bip-beep)", LineKit.PipBeep, gainDb: -18);
        Add(L, "pip.text", "a text arriving on PIP: a soft two-note chime", LineKit.PipText, gainDb: -16);
        Add(L, "pip.blip", "PIP's voice: one syllable blip (played in a run as it speaks)", LineKit.PipBlip, variants: 6, gainDb: -19);
        Add(L, "am.beep", "the answering machine's beep: a message waiting (a slightly wobbly tape beep)", LineKit.AmBeep, gainDb: -20);
        // Screens: the green-screen terminal and the menus.
        Add(L, "ui.key", "a terminal key pressed", LineKit.Key, variants: 4, gainDb: -13);
        Add(L, "ui.move", "moving between choices on a screen: a tiny soft tick", LineKit.Move, gainDb: -12);
        Add(L, "ui.select", "choosing something on a screen: a soft rising blip", LineKit.Select, gainDb: -13);
        Add(L, "ui.back", "stepping back from a screen: a soft falling blip", LineKit.Back, gainDb: -15);
        Add(L, "ui.error", "a screen refusing: a short low double buzz", LineKit.Error, gainDb: -17);
    }

    /// <summary>The recipes for <see cref="RegisterLine"/>, kept in their own class so their names can't clash with the other families'.</summary>
    private static class LineKit
    {
        private const double Tau = Math.PI * 2;
        private const int Hz = Synth.Rate;

        // ------------------------------------------------------------ shared pieces

        private static double U(ref Synth.Rng r, double a, double b) => a + (b - a) * r.Next();

        private static float[] ToRms(float[] x, double target)
        {
            double rms = Synth.Rms(x);
            return rms < 1e-9 ? x : x.Gain(target / rms);
        }

        /// <summary>0 to 1 and back: a linear ramp of <paramref name="ramp"/> seconds at each end of a span <paramref name="dur"/> long.</summary>
        private static double Edge(double t, double dur, double ramp) => Math.Clamp(Math.Min(t, dur - t) / ramp, 0, 1);

        /// <summary>
        /// Set the final level. The player takes the DC out again before playing (<see cref="Render"/>), and on a low buzz or
        /// thump that filter's phase shift lifts the peaks, so scale for the peak measured after it.
        /// </summary>
        private static float[] Level(float[] x, float peak = 0.9f)
        {
            float p = Synth.Peak(((float[])x.Clone()).DcBlock());
            return p < 1e-9f ? x : x.Gain(peak / p);
        }

        /// <summary>A linear fade-in over the first seconds (Modal and Sine start at full level).</summary>
        private static float[] Attack(float[] x, double s)
        {
            int a = Math.Min(x.Length, Synth.N(s));
            for (int i = 0; i < a; i++) x[i] *= i / (float)a;
            return x;
        }

        /// <summary>The line's own noise: white noise in the telephone band, at an RMS level.</summary>
        private static float[] Hiss(double s, ref Synth.Rng r, double rms) => ToRms(Synth.Noise(s, ref r).Telephone(), rms);

        /// <summary>
        /// A sound as heard in a handset: squeezed into the phone band, a carbon earpiece's grit (gentle saturation, which
        /// also makes dual tones growl a little the way real ones do), and the line hiss underneath (RMS, relative to a peak of 0.8).
        /// </summary>
        private static float[] Handset(float[] x, ref Synth.Rng r, double hissRms)
        {
            x.Normalize(0.8f).Telephone().Saturate(1.4);
            var h = Hiss(x.Seconds(), ref r, hissRms);
            for (int i = 0; i < x.Length; i++) x[i] += h[i];
            return x.LowPassQ(3400, 0.7).Normalize();
        }

        /// <summary>A bright tick: an impulse rung through a band pass. Switch contacts, latches, keys.</summary>
        private static float[] Tick(double hz, double q = 1.5, double tau = 0.0006) => Synth.Click(0.02, tau).BandPass(hz, q).Normalize(1f);

        /// <summary>A short noise burst, coloured and decaying: the body of a knock.</summary>
        private static float[] Burst(double s, double hz, double q, double tau, ref Synth.Rng r) => Synth.Noise(s, ref r).BandPass(hz, q).Decay(tau).Normalize(1f);

        /// <summary>Stick-slip friction: noise chopped into little grains about <paramref name="gap"/> seconds apart. Colour it after.</summary>
        private static float[] Grit(double s, ref Synth.Rng r, double gap)
        {
            var x = Synth.Noise(s, ref r);
            var env = new float[x.Length];
            int len = Synth.N(gap * 1.5);
            double tau = Synth.N(gap * 0.35);
            for (double t = 0; t < s; t += gap * U(ref r, 0.5, 1.5))
            {
                int i0 = Synth.N(t);
                float a = (float)U(ref r, 0.3, 1);
                for (int i = i0; i < Math.Min(env.Length, i0 + len); i++) env[i] = Math.Max(env[i], a * (float)Math.Exp(-(i - i0) / tau));
            }
            return x.Mul(env);
        }

        /// <summary>A loop is seamless however it is rotated; start it where it is quiet and still, so the seam check (and the first play) is clean.</summary>
        private static float[] QuietStart(float[] x)
        {
            int best = 0;
            double bestScore = double.MaxValue;
            for (int k = 0; k < x.Length; k++)
            {
                double s = Math.Abs(x[k]) + 2 * Math.Abs(x[k] - x[(k - 1 + x.Length) % x.Length]);
                if (s < bestScore) { bestScore = s; best = k; }
            }
            var y = new float[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = x[(i + best) % x.Length];
            return y;
        }

        /// <summary>The bell gongs of a 1980s house phone: two steel domes, slightly different sizes, each with a split (beating) fundamental.</summary>
        private static float[] Gong(int which, double s) => which == 0
            ? Synth.Modal(s, 1020, new[] { (1.0, 1.0, 0.85), (1.004, 0.6, 0.8), (2.76, 0.45, 0.42), (2.772, 0.3, 0.38), (5.2, 0.1, 0.12) })
            : Synth.Modal(s, 1185, new[] { (1.0, 1.0, 0.75), (1.0035, 0.5, 0.7), (2.58, 0.4, 0.38), (4.55, 0.1, 0.12) });

        /// <summary>One relay pulling in: the armature's click, a contact bounce or two a few ms later, the bobbin's knock and a breath of coil buzz.</summary>
        private static float[] Relay(ref Synth.Rng r)
        {
            var x = Synth.Silence(0.06);
            double f = U(ref r, 2500, 3400), body = U(ref r, 850, 1300), gap = U(ref r, 0.003, 0.007), t0 = 0.003;
            x.MixAt(Tick(f, 1.6), t0, 1.0);
            x.MixAt(Synth.Modal(0.03, body, new[] { (1.0, 1.0, 0.004), (2.3, 0.5, 0.0025) }), t0, 0.5);
            x.MixAt(Tick(f * 1.12, 2), t0 + gap, 0.55);
            x.MixAt(Tick(f * 0.9, 2), t0 + gap + U(ref r, 0.0015, 0.003), 0.22);
            x.MixAt(Synth.Pulse(0.04, U(ref r, 95, 125)).LowPass(900).LowPass(1500).Decay(0.007), 0, 0.15);
            return x.LowPass(6000);
        }

        /// <summary>A hook switch: a small leaf contact snapping over, with the spring's tiny ring.</summary>
        private static float[] HookSwitch(ref Synth.Rng r) => Synth.Mix(
            Tick(U(ref r, 2600, 3200), 1.8),
            Synth.Modal(0.06, U(ref r, 2900, 3300), new[] { (1.0, 0.4, 0.009), (1.6, 0.25, 0.005) }));

        /// <summary>Hard plastic meeting hard plastic: a handset in its cradle, a line card seating. hz sets the size.</summary>
        private static float[] Clunk(ref Synth.Rng r, double hz, double ring = 1) => Synth.Mix(
            Synth.Modal(0.25, hz, new[] { (1.0, 1.0, 0.035 * ring), (2.15, 0.6, 0.022 * ring), (3.45, 0.4, 0.014 * ring), (5.6, 0.22, 0.008 * ring) }),
            Burst(0.05, hz * 4.5, 0.7, 0.008, ref r).Gain(0.5));

        /// <summary>Continuous-phase frequency-shift keying (a 300-baud modem's carrier): mark only until <paramref name="dataAt"/>, then framed characters.</summary>
        private static float[] Fsk(int n, double start, double dataAt, double end, double mark, double space, ref Synth.Rng r)
        {
            var x = new float[n];
            int nBits = (int)((end - dataAt) * 300) + 2, b = 0;
            var bits = new bool[nBits];
            while (b < nBits)
            {
                if (r.Next() < 0.15) { int idle = 1 + r.Int(6); for (int k = 0; k < idle && b < nBits; k++) bits[b++] = true; continue; }
                int c = r.Int(256);
                bits[b++] = false;                                                   // start bit
                for (int k = 0; k < 8 && b < nBits; k++) bits[b++] = ((c >> k) & 1) == 1;
                if (b < nBits) bits[b++] = true;                                     // stop bit
            }
            int i0 = Synth.N(start), iD = Synth.N(dataAt), i1 = Math.Min(n, Synth.N(end));
            double ph = 0, f = mark, dur = end - start;
            for (int i = i0; i < i1; i++)
            {
                double target = i < iD ? mark : bits[Math.Min(nBits - 1, (int)((i - iD) * 300L / Hz))] ? mark : space;
                f += (target - f) * 0.05;                                            // a hair of glide takes the grit off each shift
                ph += Tau * f / Hz;
                x[i] = (float)(Math.Sin(ph) * Edge((i - i0) / (double)Hz, dur, 0.012));
            }
            return x;
        }

        /// <summary>A soft electronic note: a triangle with a quick attack and a decay.</summary>
        private static float[] Note(double hz, double s, double tau) => Attack(Synth.Triangle(s, hz).Decay(tau), 0.002);

        /// <summary>A buzzer note: a pulse with the edges rounded off, held flat.</summary>
        private static float[] Buzz(double hz, double s) => Synth.Harmonics(s, hz, k => k % 2 == 1 ? 1.0 / k : 0.3 / k)
            .LowPassQ(900, 0.8).LowPass(1600).Normalize(1f).Adsr(0.004, 0.01, 0.9, Math.Min(0.03, s / 4)).Saturate(1.3);

        // ------------------------------------------------------------ the line, in a handset

        public static float[] Dial(int v)
        {
            var r = new Synth.Rng(Seed("line.dial", v));
            const double len = 2.0;   // 350 and 450 Hz both make whole cycles in a second, so a 2 s piece of the steady tone loops perfectly
            int n = Synth.N(len);
            var tone = Synth.Tone(len + 1.0, 350, 450).Normalize(0.8f).Telephone().Saturate(1.4).LowPassQ(3400, 0.7);
            var x = tone[Synth.N(1.0)..(Synth.N(1.0) + n)].Normalize(0.85f);   // past the filters' settling, where it repeats exactly
            var hiss = Hiss(len + 0.25, ref r, 0.008).LoopSeam(0.25);
            for (int i = 0; i < n; i++) x[i] += hiss[i];
            return QuietStart(x);
        }

        public static float[] Dead(int v)
        {
            var r = new Synth.Rng(Seed("line.dead", v));
            const double len = 1.6;
            var x = Synth.Silence(len);
            // the exchange's relay dropping the line: a sharp click and its bounce, with a little thump behind it
            x.MixAt(Tick(1700, 1.2, 0.0009), 0.004, 1.0);
            x.MixAt(Tick(2300, 1.6, 0.0006), 0.021, 0.35);
            x.MixAt(Synth.Sine(0.05, 340).Decay(0.012), 0.004, 0.5);
            // then the open line: faint hiss swelling in, a crackle or two, fading out
            var hiss = Hiss(len, ref r, 0.022).Shape(t => { double s = t * len; return Math.Min(1, Math.Max(0, (s - 0.02) / 0.15)) * Math.Min(1, (len - s) / 0.35); });
            for (int i = 0; i < x.Length; i++) x[i] += hiss[i];
            for (int k = 0; k < 3; k++) x.MixAt(Tick(U(ref r, 900, 2200), 2, 0.0004), U(ref r, 0.3, 1.2), U(ref r, 0.04, 0.09));
            return x.Telephone().Normalize();
        }

        public static float[] Engaged(int v)
        {
            var r = new Synth.Rng(Seed("line.engaged", v));
            const double len = 3.0;   // four cycles of 0.375 s on, 0.375 s off
            return Handset(Synth.Tone(len, 400).Cadence(len, 0.375, 0.375), ref r, 0.008);
        }

        public static float[] Ringback(int v)
        {
            var r = new Synth.Rng(Seed("line.ringback", v));
            const double len = 4.3;   // two double rings: 0.4 on, 0.2 off, 0.4 on, 2.0 off; the 50 Hz beat of 400 + 450 is the British purr
            var x = Synth.Concat(Synth.Silence(0.12), Synth.Tone(len - 0.12, 400, 450).Cadence(len - 0.12, 0.4, 0.2, 0.4, 2.0));
            return Handset(x, ref r, 0.008);
        }

        /// <summary>
        /// Someone else's call bleeding through: two voices taking turns, made of a glottal buzz with a falling phrase pitch,
        /// shaped into syllables and pushed through gliding vowel formants, then muffled so nothing is quite a word.
        /// </summary>
        public static float[] Crossed(int v)
        {
            var r = new Synth.Rng(Seed("line.crossed", v));
            const double len = 2.3;
            int n = Synth.N(len);
            var env = new float[n];
            var pitch = new double[n];
            var f1 = new double[n];
            var f2 = new double[n];
            Array.Fill(pitch, double.NaN);
            (double a, double b)[] vowels = { (730, 1090), (530, 1840), (300, 2200), (570, 840), (330, 900), (660, 1720), (490, 1350), (640, 1190) };
            var phrases = new[] { (start: 0.06, end: 1.06, f0: 116.0), (start: 1.30, end: 2.18, f0: 200.0) };
            foreach (var ph in phrases)
            {
                double t = ph.start;
                while (t < ph.end - 0.07)
                {
                    double d = Math.Min(U(ref r, 0.07, 0.24), ph.end - t);
                    var vw = vowels[r.Int(vowels.Length)];
                    double amp = U(ref r, 0.3, 1.0), lift = r.Next() < 0.4 ? U(ref r, 0.06, 0.18) : 0.0;
                    int i0 = Synth.N(t), i1 = Math.Min(n, Synth.N(t + d));
                    for (int i = i0; i < i1; i++)
                    {
                        double u = (i - i0) / (double)(i1 - i0);
                        double e = Math.Sin(Math.PI * Math.Pow(u, 0.65));                  // a quick onset and a longer fall
                        env[i] = (float)Math.Max(env[i], amp * e);
                        double along = (i / (double)Hz - ph.start) / (ph.end - ph.start);   // the phrase's pitch falls as it goes
                        pitch[i] = ph.f0 * (1.12 - 0.24 * along) * (1 + lift * Math.Sin(Math.PI * u));
                        f1[i] = vw.a; f2[i] = vw.b;
                    }
                    t += d + (r.Next() < 0.2 ? U(ref r, 0.03, 0.08) : 0.0);              // now and then a breath between words
                }
            }
            // carry values through the gaps, then glide them so the vowels and the pitch move rather than jump
            double p = 150, a1 = 500, a2 = 1500;
            for (int i = 0; i < n; i++)
            {
                if (double.IsNaN(pitch[i])) { pitch[i] = p; f1[i] = a1; f2[i] = a2; }
                p += (pitch[i] - p) * 0.004; a1 += (f1[i] - a1) * 0.0012; a2 += (f2[i] - a2) * 0.0012;
                pitch[i] = p; f1[i] = a1; f2[i] = a2;
            }
            var src = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++) { phase += pitch[i] / Hz; if (phase >= 1) phase -= 1; src[i] = (float)(1 - 2 * phase); }
            src.LowPass(1800);
            var breath = Synth.Noise(len, ref r).BandPass(1500, 0.8).Gain(0.05);
            for (int i = 0; i < n; i++) src[i] += breath[i];
            var lo = ((float[])src.Clone()).BandSweep(t => f1[Math.Min(n - 1, (int)(t * n))], 5);
            var hi = ((float[])src.Clone()).BandSweep(t => f2[Math.Min(n - 1, (int)(t * n))], 7);
            var voice = new float[n];
            double sm = 0;
            for (int i = 0; i < n; i++) { sm += (env[i] - sm) * 0.004; voice[i] = (float)((lo[i] + 0.65 * hi[i]) * sm); }
            // the crosstalk is faint and woolly: much less top, and the line hiss stands out beside it
            voice.LowPass(1300).LowPass(1700);
            return Handset(voice, ref r, 0.03);
        }

        /// <summary>
        /// A 300-baud handshake as a British modem of 1983 would do it: the answering modem's 2100 Hz tone (with its soft
        /// phase-reversal ticks), a pause, its carrier (1650 Hz), the caller's carrier (980 Hz) joining, then both warbling data.
        /// </summary>
        public static float[] Modem(int v)
        {
            var r = new Synth.Rng(Seed("line.modem", v));
            const double len = 3.8;
            int n = Synth.N(len);
            var x = new float[n];
            x.MixAt(Tick(1400, 1.2, 0.0008), 0.02, 0.35);                          // the line opening
            const double a0 = 0.12, a1 = 1.02;
            for (int i = Synth.N(a0); i < Synth.N(a1); i++)
            {
                double t = i / (double)Hz - a0, intoFlip = t % 0.45;
                double soft = Math.Min(1, Math.Min(intoFlip, 0.45 - intoFlip) / 0.002);   // a short dip where the phase flips
                double sign = ((int)(t / 0.45)) % 2 == 0 ? 1 : -1;
                x[i] += (float)(0.32 * sign * Math.Sin(Tau * 2100 * t) * Edge(t, a1 - a0, 0.015) * (t < 0.45 ? 1 : soft));
            }
            var answer = Fsk(n, 1.10, 1.95, 3.52, 1650, 1850, ref r);
            var caller = Fsk(n, 1.48, 2.02, 3.47, 980, 1180, ref r);
            var rush = Synth.Noise(len, ref r).BandPass(1700, 0.7).Shape(t => { double s = t * len; return s > 1.95 && s < 3.5 ? 0.05 * Edge(s - 1.95, 1.55, 0.2) : 0; });
            for (int i = 0; i < n; i++) x[i] += 0.30f * answer[i] + 0.34f * caller[i] + rush[i];
            x.MixAt(Tick(1200, 1.2, 0.0008), 3.525, 0.25);                          // the carriers drop
            return Handset(x, ref r, 0.012);
        }

        public static float[] Hook(int v)
        {
            var r = new Synth.Rng(Seed("line.hook", v));
            if (v == 0)
            {
                // lifted: the handset slides off its rests with a light knock, and the hook switch springs up
                var x = Synth.Silence(0.26);
                x.MixAt(Burst(0.1, 1300, 0.9, 0.018, ref r), 0.003, 0.25);
                x.MixAt(Synth.Modal(0.12, 520, new[] { (1.0, 1.0, 0.018), (2.4, 0.5, 0.01), (3.9, 0.3, 0.006) }), 0.012, 0.4);
                x.MixAt(HookSwitch(ref r), 0.034, 0.9);
                x.MixAt(HookSwitch(ref r), 0.038, 0.35);
                return x.LowPass(6000).Room(0.4, 0.12).Normalize();
            }
            else
            {
                // put down: a heavy plastic clunk, a second as it rocks into place, the switch pressed, and the bell's faint ting
                var x = Synth.Silence(0.6);
                x.MixAt(Clunk(ref r, 185), 0.004, 1.0);
                x.MixAt(HookSwitch(ref r), 0.014, 0.6);
                x.MixAt(HookSwitch(ref r), 0.019, 0.35);
                x.MixAt(Clunk(ref r, 235), 0.062, 0.3);
                x.MixAt(Attack(Gong(0, 0.55), 0.002).Decay(0.09), 0.02, 0.05);
                return Level(x.LowPass(6000).Room(0.4, 0.12));
            }
        }

        /// <summary>The bell: a clapper at 20 Hz striking two gongs in turn (40 strikes a second), 0.4 s on, 0.2 off, 0.4 on, then ringing out in a room.</summary>
        public static float[] Ring(int v)
        {
            var r = new Synth.Rng(Seed("phone.ring", v));
            const double len = 2.4;
            var gongs = new[] { Gong(0, len), Gong(1, len) };   // as long as the sound, so no strike's ring is cut short
            var x = Synth.Silence(len);
            const double step = 1 / 40.0;
            foreach (var (on, off) in new[] { (0.01, 0.41), (0.61, 1.01) })
            {
                int k = 0;
                for (double t = on; t < off - 1e-6; t += step, k++)
                {
                    double warm = Math.Min(1, 0.5 + 0.12 * k), at = t + U(ref r, -0.0004, 0.0004);   // the armature gets going
                    x.MixAt(gongs[k % 2], at, warm * U(ref r, 0.85, 1) * (k % 2 == 0 ? 1 : 0.8));
                    x.MixAt(Tick(2400, 1.2), at, 0.25 * warm);                                       // the clapper's own tap
                }
            }
            x.Shape(t => Math.Min(1, (len - t * len) / 0.6));
            return x.LowPass(5500).Room(0.7, 0.22).Normalize();
        }

        // ------------------------------------------------------------ the switchboard

        public static float[] JumperIn(int v)
        {
            var r = new Synth.Rng(Seed("sw.jumper.in", v));
            double scrape = U(ref r, 0.035, 0.06), at = 0.003 + scrape;
            var x = Synth.Silence(at + 0.1);
            // the wire's end scraping down between the spring's leaves, tighter (and brighter) as it goes in
            x.MixAt(Grit(scrape, ref r, 0.0025).BandSweep(t => 2000 + 1300 * t, 3).Shape(t => 0.35 + 0.65 * t).Normalize(1f), 0.003, 0.35);
            // home: the spring snaps onto the bare copper, and the terminal block knocks
            x.MixAt(Tick(U(ref r, 3000, 3700), 1.6), at, 1.0);
            x.MixAt(Synth.Modal(0.1, U(ref r, 2200, 2550), new[] { (1.0, 0.5, 0.018), (1.58, 0.3, 0.012), (2.3, 0.15, 0.006) }), at, 0.5);
            x.MixAt(Synth.Modal(0.04, U(ref r, 650, 800), new[] { (1.0, 1.0, 0.008), (2.3, 0.5, 0.005) }), at + 0.001, 0.4);
            return x.LowPass(6500).Normalize();
        }

        public static float[] JumperOut(int v)
        {
            var r = new Synth.Rng(Seed("sw.jumper.out", v));
            double zip = U(ref r, 0.045, 0.075), at = 0.003 + zip, f0 = U(ref r, 1700, 2100);
            var x = Synth.Silence(at + 0.16);
            // the wire dragged out through the clip: fast, falling grit
            x.MixAt(Grit(zip, ref r, 0.0018).BandSweep(t => 3100 - 1300 * t, 2.5).Shape(t => Math.Min(1, t * 6) * (1 - 0.45 * t)).Normalize(1f), 0.003, 0.5);
            // free: the spring leaves slap shut and twang, dropping a touch in pitch as they settle
            x.MixAt(Tick(U(ref r, 2600, 3200), 1.8), at, 0.8);
            var twang = Synth.Mix(
                Synth.SineGlide(0.15, t => f0 * (1.03 - 0.03 * Math.Min(1, t * 5))).Decay(0.022),
                Synth.SineGlide(0.15, t => 2.05 * f0 * (1.03 - 0.03 * Math.Min(1, t * 5))).Decay(0.012).Gain(0.35));
            x.MixAt(Attack(twang, 0.001), at, 0.35);
            return x.LowPass(6500).Normalize();
        }

        public static float[] RelayOne(int v)
        {
            var r = new Synth.Rng(Seed("sw.relay", v));
            return Relay(ref r).Normalize();
        }

        /// <summary>The probe's oscillator swaps between two tones 13 times a second, the pair rising and falling once; out of a tiny speaker.</summary>
        public static float[] Probe(int v)
        {
            var r = new Synth.Rng(Seed("sw.probe", v));
            const double len = 1.05;
            int n = Synth.N(len);
            var x = new float[n];
            double ph = 0, f = 720;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Hz;
                double baseHz = 720 + 260 * Math.Sin(Math.PI * t / len);
                bool high = ((int)(t * 13)) % 2 == 1;
                f += (baseHz * (high ? 1.26 : 1.0) - f) * 0.004;
                ph += Tau * f / Hz;
                x[i] = (float)Math.Tanh(2.2 * Math.Sin(ph));
            }
            var hiss = Synth.Noise(len, ref r).HighPass(900).LowPass(4000).Gain(0.06);
            for (int i = 0; i < n; i++) x[i] += hiss[i];
            x.HighPassQ(650, 0.8).LowPassQ(3000, 1.1).Normalize(0.9f).Crush(6, 1).LowPassQ(4200, 0.7).LowPass(5000);
            return x.Shape(t => Edge(t * len, len, 0.05)).Saturate(1.3).Normalize();
        }

        public static float[] TestOk(int v)
        {
            static float[] Beep(double hz, double s) => Synth.Harmonics(s, hz, k => k == 1 ? 1 : k == 2 ? 0.12 : k == 3 ? 0.16 : 0).Adsr(0.003, 0.015, 0.8, 0.014);
            var x = Synth.Silence(0.2);
            x.MixAt(Beep(1175, 0.065), 0.004, 0.85);
            x.MixAt(Beep(1568, 0.085), 0.096, 1.0);
            return x.HighPass(350).LowPass(4500).Normalize();
        }

        public static float[] TestBad(int v) => Level(Synth.Concat(Synth.Silence(0.004), Buzz(118, 0.38)).DcBlock(), 0.85f);

        /// <summary>The knife switch's blades scrape into their clips and clunk home; the board wakes with a hum and a ripple of relays, thinning out.</summary>
        public static float[] Throw(int v)
        {
            var r = new Synth.Rng(Seed("sw.throw", v));
            const double len = 1.95;
            var x = Synth.Silence(len);
            x.MixAt(Grit(0.04, ref r, 0.002).BandSweep(t => 1500 - 600 * t, 2).Normalize(1f), 0.003, 0.3);
            const double hit = 0.04;
            x.MixAt(Synth.Modal(0.6, 62, new[] { (1.0, 1.0, 0.12), (1.9, 0.6, 0.07), (3.1, 0.4, 0.045) }), hit, 1.0);
            x.MixAt(Synth.Modal(0.3, 240, new[] { (1.0, 1.0, 0.05), (2.7, 0.5, 0.03), (4.4, 0.3, 0.02) }), hit, 0.5);
            x.MixAt(Synth.Modal(0.4, 820, new[] { (1.0, 0.6, 0.08), (2.35, 0.4, 0.05), (3.9, 0.25, 0.03), (5.3, 0.15, 0.02) }), hit, 0.35);
            x.MixAt(Burst(0.04, 1800, 0.6, 0.01, ref r), hit, 0.4);
            x.MixAt(Tick(2100, 2), hit + 0.026, 0.15);                                                 // the handle rattling in its pivot
            var hum = Synth.Harmonics(len, 100, k => k <= 6 ? 1.0 / k : 0).LowPass(700)
                .Shape(t => { double s = t * len; return s < 0.1 ? 0 : Math.Min(1, (s - 0.1) / 0.25) * Math.Min(1, (len - s) / 0.7); });
            x.MixAt(hum, 0, 0.05);
            for (int k = 0; k < 44; k++)
            {
                double u = r.Next(), at = 0.11 + 1.55 * Math.Pow(u, 1.9);                            // crowded at first, then stragglers
                x.MixAt(Relay(ref r), at, (3.2 - 1.9 * u) * U(ref r, 0.55, 1));
            }
            return Level(x.LowPass(6000).Room(0.6, 0.15).DcBlock());
        }

        public static float[] Card(int v)
        {
            var r = new Synth.Rng(Seed("sw.card", v));
            double slide = U(ref r, 0.2, 0.25), at = 0.005 + slide;
            var x = Synth.Silence(at + 0.24);
            // plastic edges running along the rack's guide rails
            var run = Grit(slide, ref r, 0.006).BandSweep(t => 1300 + 900 * t, 1.4);
            var body = Synth.Noise(slide, ref r).BandPass(700, 1.2).Gain(0.06);
            for (int i = 0; i < run.Length; i++) run[i] += body[i];
            x.MixAt(run.Shape(t => Math.Min(1, t * 5) * (0.6 + 0.4 * t)).Normalize(1f), 0.005, 0.4);
            for (int k = 0; k < 2; k++) x.MixAt(Tick(U(ref r, 1900, 2500), 2), 0.005 + U(ref r, 0.25, 0.85) * slide, 0.12);
            // seated: the edge connector bites, the latch snaps over
            x.MixAt(Tick(2600, 1.4), at, 0.8);
            x.MixAt(Clunk(ref r, 300, 0.85), at, 0.9);
            x.MixAt(Tick(3400, 1.8), at + 0.009, 0.55);
            return x.LowPass(6000).Room(0.35, 0.1).Normalize();
        }

        // ------------------------------------------------------------ PIP and the answering machine

        public static float[] PipBeep(int v)
        {
            static float[] Piezo(double hz, double s) => Synth.Harmonics(s, hz, k => k == 1 ? 1 : k == 3 ? 0.13 : 0).Adsr(0.002, 0.012, 0.85, 0.01);
            var x = Synth.Silence(0.31);
            x.MixAt(Piezo(1175, 0.05), 0.004, 0.8);
            x.MixAt(Piezo(1175, 0.05), 0.094, 0.8);
            x.MixAt(Piezo(1568, 0.11), 0.184, 1.0);
            return x.LowPass(5000).Normalize();
        }

        public static float[] PipText(int v)
        {
            static float[] Chime(double hz) => Attack(Synth.Modal(0.75, hz, new[] { (1.0, 1.0, 0.16), (2.0, 0.22, 0.07), (3.0, 0.07, 0.035), (4.0, 0.03, 0.02) }), 0.004);
            var x = Synth.Silence(0.88);
            x.MixAt(Chime(880), 0.004, 0.75);
            x.MixAt(Chime(1319), 0.11, 1.0);
            return x.LowPass(5000).Normalize();
        }

        /// <summary>One syllable of PIP's voice: a buzzy pulse at a small voice's pitch, sliding down a touch, through two vowel formants.</summary>
        public static float[] PipBlip(int v)
        {
            var r = new Synth.Rng(Seed("pip.blip", v));
            (double f0, double f1, double f2)[] voices = { (330, 850, 1350), (370, 560, 2000), (294, 560, 1000), (415, 380, 2400), (349, 420, 950), (392, 720, 1800) };
            var (f0, ff1, ff2) = voices[v % voices.Length];
            double len = U(ref r, 0.06, 0.085);
            int n = Synth.N(len);
            var src = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++) { ph += f0 * (1.05 - 0.09 * i / n) / Hz; if (ph >= 1) ph -= 1; src[i] = (float)(1 - 2 * ph); }
            src.LowPass(2500);
            var x = Synth.Mix(
                ((float[])src.Clone()).BandPass(ff1, 4),
                ((float[])src.Clone()).BandPass(ff2, 6).Gain(0.6),
                ((float[])src.Clone()).BandPass(2900, 5).Gain(0.12));
            return x.Shape(t => Math.Min(1, t * len / 0.006) * Math.Pow(1 - t, 0.6)).HighPass(220).Normalize();
        }

        /// <summary>A tape machine's beep: 1 kHz with wow and flutter, a little tape saturation, hiss, out of a small speaker.</summary>
        public static float[] AmBeep(int v)
        {
            var r = new Synth.Rng(Seed("am.beep", v));
            const double beep = 0.5, lead = 0.03, len = beep + lead + 0.07;
            var tone = Synth.SineGlide(beep, t => { double s = t * beep; return 1000 * (1 + 0.005 * Math.Sin(Tau * 1.4 * s + 0.6) + 0.0018 * Math.Sin(Tau * 9.5 * s)); })
                .Saturate(1.8).Shape(t => 1 + 0.05 * Math.Sin(Tau * 9.5 * t * beep + 1)).Adsr(0.01, 0.02, 0.92, 0.025);
            var x = Synth.Silence(len).MixAt(tone, lead, 0.85);
            var hiss = ToRms(Synth.Noise(len, ref r).HighPass(1500).LowPass(5000), 0.012);
            for (int i = 0; i < x.Length; i++) x[i] += hiss[i];
            return x.HighPass(280).LowPassQ(3800, 0.7).Normalize();
        }

        // ------------------------------------------------------------ screens

        public static float[] Key(int v)
        {
            var r = new Synth.Rng(Seed("ui.key", v));
            var x = Synth.Silence(0.075);
            double down = 0.002 + U(ref r, 0.004, 0.008);
            x.MixAt(Tick(U(ref r, 2700, 3500), 1.8), 0.002, 0.55);                                      // the switch's click
            x.MixAt(Synth.Modal(0.06, U(ref r, 360, 520), new[] { (1.0, 1.0, 0.012), (2.15, 0.55, 0.008), (3.7, 0.3, 0.005) }), down, 0.8);   // the cap bottoming out
            x.MixAt(Burst(0.02, 1600, 0.8, 0.003, ref r), down, 0.35);
            x.MixAt(Tick(U(ref r, 2000, 2600), 2), down + U(ref r, 0.012, 0.02), 0.1);                // a little rattle on the way back up
            return x.LowPass(5500).Normalize();
        }

        public static float[] Move(int v) => Attack(Synth.Mix(Synth.Sine(0.035, 1400), Synth.Sine(0.035, 2800).Gain(0.12)), 0.001).Decay(0.007).Normalize();

        public static float[] Select(int v)
        {
            var x = Synth.Silence(0.13);
            x.MixAt(Note(880, 0.08, 0.018), 0.002, 0.8);
            x.MixAt(Note(1320, 0.09, 0.02), 0.034, 1.0);
            return x.LowPass(5000).Normalize();
        }

        public static float[] Back(int v) => Attack(Synth.SineGlide(0.12, t => 700 * Math.Pow(470 / 700.0, Math.Min(1, t * 1.5))).Saturate(1.3).Decay(0.025), 0.002).Normalize();

        public static float[] Error(int v)
        {
            var x = Synth.Silence(0.135);
            x.MixAt(Buzz(150, 0.048), 0.003, 1.0);
            x.MixAt(Buzz(140, 0.05), 0.082, 0.9);
            return Level(x.DcBlock(), 0.85f);
        }
    }
}
