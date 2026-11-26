namespace Telo.Game.Audio;

/// <summary>
/// Foley: footsteps, doors, kit, tools, paper, money, and the hold-to-work tick (EA7).
/// Each recipe builds its sound from <see cref="Synth"/> with its own seeded <see cref="Synth.Rng"/> (thread-safe, the same
/// every run). The helpers at the bottom are the shared vocabulary: <see cref="Thud"/> (a body's low thump, falling in
/// pitch), <see cref="Burst"/> (a band of noise, struck), <see cref="Ring"/> (struck modes: wood, steel, brass, plastic),
/// <see cref="Grit"/> (tiny impacts scattered: crunch, crackle, rattle) and <see cref="Creak"/> (stick-slip friction:
/// hinges, boards, screws). Sizes, pitches and timings are drawn per variant so repeats never sound the same.
/// </summary>
public static partial class Sounds
{
    static partial void RegisterFoley()
    {
        const string F = "foley";
        Add(F, "step.grass", "a footstep on grass or earth: a soft low thud and a crunch of grass", v => StepGrass("step.grass", v), variants: 6, gainDb: -16);
        Add(F, "step.road", "a footstep on tarmac or pavement: a heel click, a scuff of grit, the toe", v => StepRoad("step.road", v), variants: 6, gainDb: -16);
        Add(F, "step.floor", "a footstep indoors: floorboards under carpet, a dull hollow thump", v => StepFloor("step.floor", v), variants: 6, gainDb: -14);
        Add(F, "step.metal", "a footstep on steel: the data centre's raised floor, a ladder rung, the jetty", v => StepMetal("step.metal", v), variants: 6, gainDb: -17);
        Add(F, "step.land", "landing from a jump: both feet, then a half-step to catch the balance", v => StepLand("step.land", v), variants: 3, gainDb: -12);
        Add(F, "door.knock", "three knocks on a front door", v => DoorKnock("door.knock", v), variants: 3, gainDb: -5);
        Add(F, "door.open", "a house door opening: handle, latch, a creak of the hinges", v => DoorOpen("door.open", v), variants: 2, gainDb: -10);
        Add(F, "door.close", "a house door closing: swing, thud, latch", v => DoorClose("door.close", v), variants: 2, gainDb: -8);
        Add(F, "door.bell", "the bell over a shop door, jangling on its spring", v => DoorBell("door.bell", v), gainDb: -15);
        Add(F, "van.door.open", "the van's door opening: handle, the latch lets go, the check-strap stops it", v => VanDoorOpen("van.door.open", v), gainDb: -8);
        Add(F, "van.door.close", "the van's door slammed shut: latch, a steel thump, a rattle in the back", v => VanDoorClose("van.door.close", v), gainDb: -4);
        Add(F, "kit.pickup", "picking something up: a shuffle and lift", v => KitPickup("kit.pickup", v), variants: 3, gainDb: -12);
        Add(F, "kit.putdown", "putting something down on the ground", v => KitPutdown("kit.putdown", v), variants: 3, gainDb: -9);
        Add(F, "kit.ladder", "an aluminium ladder rattling as it's carried or set against a wall", v => KitLadder("kit.ladder", v), variants: 2, gainDb: -14);
        Add(F, "kit.drum", "a heavy cable drum set down: a wooden thump, a rock on its flanges", v => KitDrum("kit.drum", v), gainDb: -8);
        Add(F, "kit.toolbox", "a steel toolbox with tools rattling inside", v => KitToolbox("kit.toolbox", v), variants: 2, gainDb: -14);
        Add(F, "kit.crate", "a wooden parts crate set down, the parts shifting inside", v => KitCrate("kit.crate", v), gainDb: -7);
        Add(F, "work.tick", "a soft tick while you hold E to work (about four a second): a small clock escapement", v => WorkTick("work.tick", v), variants: 2, gainDb: -23);
        Add(F, "work.done", "the satisfying click-clunk when a held job finishes", v => WorkDone("work.done", v), gainDb: -8);
        Add(F, "tool.drill", "an electric hand drill through brick, about a second: winds up, bites, breaks through", v => ToolDrill("tool.drill", v), gainDb: -8);
        Add(F, "tool.staple", "a cable staple tapped in with a small hammer", v => ToolStaple("tool.staple", v), variants: 3, gainDb: -8);
        Add(F, "tool.clip", "a plastic clip or cover snapping home", v => ToolClip("tool.clip", v), variants: 3, gainDb: -10);
        Add(F, "tool.screw", "a screwdriver turning a few times into a wall plug", v => ToolScrew("tool.screw", v), variants: 2, gainDb: -10);
        Add(F, "tool.hammer", "a hammer blow on wood", v => ToolHammer("tool.hammer", v), variants: 3, gainDb: -8);
        Add(F, "tool.cable", "cable pulled off a drum or through a hole: a rustle and zip", v => ToolCable("tool.cable", v), variants: 2, gainDb: -14);
        Add(F, "tool.dig", "a spade into earth", v => ToolDig("tool.dig", v), variants: 3, gainDb: -13);
        Add(F, "tool.thump", "the bodge: a thump with the heel of the hand on something plastic", v => ToolThump("tool.thump", v), variants: 2, gainDb: -4);
        Add(F, "cash.coins", "coins counted into a hand: being paid", v => CashCoins("cash.coins", v), gainDb: -14);
        Add(F, "cash.till", "a shop till: keys, the bell, the drawer running out", v => CashTill("cash.till", v), gainDb: -12);
        Add(F, "paper.page", "a page turned", v => PaperPage("paper.page", v), variants: 2, gainDb: -17);
        Add(F, "paper.slip", "a paper slip pulled off a spike or handed over", v => PaperSlip("paper.slip", v), gainDb: -15);
    }

    // ================================================================ footsteps
    // Every step is a heel then a toe 50-120 ms later (v spreads the gap so the six variants differ in rhythm, not just
    // colour), all under 0.3 s.

    private static float[] StepGrass(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        double toe = 0.05 + 0.011 * v + 0.01 * r.Next(), heel = 0.75 + 0.25 * r.Next(), roll = 0.4 + 0.35 * r.Next();
        double crunch = 650 + 650 * r.Next();
        var x = Synth.Silence(0.27);
        double h = 0.004;
        if (v % 3 == 1) { x.MixAt(Burst(0.05, ref r, 1500, 0.8, 0.018, 0.012), 0, 0.14); h = 0.022; }   // the foot brushes the grass first
        // heel: soft and low, the ground gives
        x.MixAt(Thud(0.16, 105 + 30 * r.Next(), 55 + 10 * r.Next(), 0.03, 0.003), h, 0.8 * heel);
        x.MixAt(Burst(0.12, ref r, crunch, 0.8, 0.003, 0.028), h, 0.6);
        x.MixAt(Grit(0.10, 20 + r.Int(25), ref r, 1600 + 900 * r.Next(), 0.9, 1.7).Decay(0.035), h + 0.002, 0.4);
        // toe: a lighter press and a longer rustle as the foot rolls off
        x.MixAt(Thud(0.12, 130 + 20 * r.Next(), 75, 0.02, 0.005), h + toe, heel * roll);
        x.MixAt(Burst(0.16, ref r, crunch * 1.35, 0.9, 0.012, 0.04), h + toe, 0.45 * (0.6 + 0.6 * roll));
        x.MixAt(Grit(0.14, 25 + r.Int(30), ref r, 2000 + 700 * r.Next(), 1, 1.3).Decay(0.045), h + toe, 0.32);
        return Out(x.LowPassQ(3600));
    }

    private static float[] StepRoad(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        double toe = 0.055 + 0.011 * v + 0.012 * r.Next(), heel = 0.8 + 0.2 * r.Next(), slap = 0.35 + 0.35 * r.Next();
        double click = 1500 + 800 * r.Next();
        var x = Synth.Silence(0.25);
        // heel: a hard little click on a short thump
        x.MixAt(Burst(0.03, ref r, click, 1.3, 0.0004, 0.005), 0.003, heel);
        x.MixAt(Thud(0.08, 190 + 40 * r.Next(), 120, 0.012, 0.001), 0.003, 0.6 * heel);
        // the sole scuffs the grit as it rolls
        x.MixAt(Burst(0.09, ref r, 1400 + 700 * r.Next(), 0.8, 0.012, 0.025), 0.012, 0.22 + 0.14 * (v % 2));
        x.MixAt(Grit(0.08, 6 + r.Int(10), ref r, 2600, 1.4, 1.2).Decay(0.04), 0.01, 0.16);
        // toe: a flatter slap
        x.MixAt(Burst(0.04, ref r, click * 0.8, 1.0, 0.0008, 0.009), 0.003 + toe, slap);
        x.MixAt(Thud(0.07, 170, 115, 0.01, 0.001), 0.003 + toe, slap * 0.5);
        return Out(x.LowPassQ(4200));
    }

    private static float[] StepFloor(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        double toe = 0.06 + 0.01 * v + 0.012 * r.Next(), heel = 0.8 + 0.2 * r.Next(), roll = 0.35 + 0.35 * r.Next();
        double board = 125 + 40 * r.Next();   // each board its own pitch
        var x = Synth.Silence(0.27);
        // heel: a dull thump with the hollow under the boards ringing faintly
        x.MixAt(Thud(0.2, 100 + 20 * r.Next(), 62, 0.03, 0.004), 0.004, 0.6 * heel);
        x.MixAt(Board(0.24, board, ref r), 0.004, 0.85 * heel);
        // toe
        x.MixAt(Thud(0.15, 115, 75, 0.022, 0.006), 0.004 + toe, 0.6 * heel * roll);
        x.MixAt(Board(0.2, board * 1.06, ref r), 0.004 + toe, 0.7 * roll);
        if (v == 4)   // one board that creaks a little
            x.MixAt(Creak(0.13, ref r, u => 55 + 30 * u, 0.3, (700, 6, 1), (1250, 7, 0.5)).Shape(u => Math.Sin(Math.PI * u)), 0.004 + toe * 0.6, 0.14);
        x.LowPassQ(1500);   // the carpet takes the edge off everything
        // the shoe on the carpet pile itself
        x.MixAt(Burst(0.1, ref r, 1100, 0.7, 0.008, 0.025).LowPass(2500), 0.006, 0.2);
        x.MixAt(Burst(0.1, ref r, 900, 0.7, 0.01, 0.03).LowPass(2500), 0.006 + toe, 0.14);
        return Out(x);
    }

    private static float[] StepMetal(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        double toe = 0.06 + 0.01 * v + 0.012 * r.Next(), heel = 0.8 + 0.2 * r.Next(), roll = 0.35 + 0.3 * r.Next();
        double plate = 240 + 160 * r.Next();
        var x = Synth.Silence(0.28);
        // heel: the plate rings (inharmonic, damped by the shoe), a thump under it, the contact itself
        x.MixAt(Plate(0.27, plate, ref r), 0.003, 0.8 * heel);
        x.MixAt(Thud(0.1, 160, 105, 0.018, 0.001), 0.003, 0.7 * heel);
        x.MixAt(Burst(0.02, ref r, 2300, 1.2, 0.0003, 0.003), 0.003, 0.4 * heel);
        if (v % 2 == 0) x.MixAt(Burst(0.015, ref r, 1900, 2, 0.0003, 0.002), 0.015 + 0.012 * r.Next(), 0.28);   // the tile knocks on its pedestal
        // toe
        x.MixAt(Plate(0.2, plate, ref r), 0.003 + toe, 0.55 * roll);
        x.MixAt(Burst(0.02, ref r, 2000, 1.2, 0.0005, 0.003), 0.003 + toe, 0.35 * roll);
        return Out(x.LowPassQ(4500).Fade(0.0005, 0.04));
    }

    private static float[] StepLand(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.4);
        double gap = 0.012 + 0.02 * r.Next();
        for (int f = 0; f < 2; f++)
        {
            double t = 0.004 + f * gap, g = f == 0 ? 1 : 0.8;
            x.MixAt(Thud(0.3, 90 + 20 * r.Next(), 46, 0.06, 0.002), t, g);
            x.MixAt(Burst(0.15, ref r, 550 + 300 * r.Next(), 0.7, 0.002, 0.04), t, 0.65 * g);
            x.MixAt(Grit(0.12, 20, ref r, 1700, 1, 1.6).Decay(0.04), t, 0.45 * g);
        }
        // clothes and kit settle; a half-step to catch the balance
        x.MixAt(Burst(0.2, ref r, 1400, 0.7, 0.02, 0.05), 0.03, 0.15);
        double catchAt = 0.16 + 0.05 * r.Next();
        x.MixAt(Thud(0.1, 120, 80, 0.02, 0.004), catchAt, 0.3);
        x.MixAt(Burst(0.08, ref r, 900, 0.8, 0.005, 0.02), catchAt, 0.18);
        return Out(x.LowPassQ(3500));
    }

    /// <summary>A floorboard's hollow: a few low modes, quick to die.</summary>
    private static float[] Board(double s, double hz, ref Synth.Rng r) =>
        Ring(s, hz, ref r, 0.04, (1, 1, 0.045), (1.52, 0.7, 0.032), (2.3, 0.5, 0.022), (3.3, 0.3, 0.014), (4.6, 0.18, 0.009));

    /// <summary>A steel floor plate or rung: inharmonic modes, damped.</summary>
    private static float[] Plate(double s, double hz, ref Synth.Rng r) =>
        Ring(s, hz, ref r, 0.03, (1, 1, 0.08), (1.47, 0.85, 0.07), (2.09, 0.7, 0.055), (2.56, 0.55, 0.045), (3.18, 0.45, 0.038),
            (3.61, 0.4, 0.03), (4.83, 0.25, 0.022), (6.1, 0.15, 0.016));

    // ================================================================ doors

    private static float[] DoorKnock(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        // three rhythms: even and polite; rat-a-tat; two and a firmer third
        double[][] at = { new[] { 0, 0.25, 0.5 }, new[] { 0, 0.15, 0.3 }, new[] { 0, 0.2, 0.48 } };
        double[][] hard = { new[] { 0.9, 0.85, 0.95 }, new[] { 0.85, 0.8, 1 }, new[] { 0.8, 0.8, 1 } };
        double door = 220 + 35 * r.Next();
        var x = Synth.Silence(0.95);
        for (int k = 0; k < 3; k++)
            x.MixAt(Knock(ref r, door * (1 + 0.03 * r.Bi()), hard[v][k]), 0.005 + at[v][k] + 0.01 * r.Bi() * (k > 0 ? 1 : 0), hard[v][k]);
        return Out(x.LowPassQ(6000).Room(0.9, 0.2));
    }

    /// <summary>A knuckle on a solid wooden door: the panel's modes, a thump, the contact.</summary>
    private static float[] Knock(ref Synth.Rng r, double hz, double hard)
    {
        var x = Synth.Silence(0.2);
        x.MixAt(Ring(0.2, hz, ref r, 0.03, (0.5, 0.45, 0.03), (1, 1, 0.028), (1.71, 0.8, 0.02), (2.53, 0.6, 0.014), (3.74, 0.4, 0.009), (5.2, 0.25, 0.006)), 0, 1);
        x.MixAt(Thud(0.1, 160, 105, 0.014, 0.001), 0, 0.4);
        x.MixAt(Burst(0.025, ref r, 1800, 1, 0.0004, 0.004), 0, 0.5 * hard);
        return x;
    }

    private static float[] DoorOpen(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        double len = v == 0 ? 1.25 : 0.95;
        var x = Synth.Silence(len);
        // the handle turns and the latch draws back: a rub, then a clack
        x.MixAt(Burst(0.04, ref r, 2200, 1.5, 0.004, 0.008), 0, 0.22);
        x.MixAt(Latch(ref r, 1500), 0.07, 0.5);
        // the door comes off the frame
        x.MixAt(Thud(0.12, 140, 95, 0.02, 0.003), 0.11, 0.35);
        x.MixAt(Burst(0.06, ref r, 600, 0.8, 0.002, 0.015), 0.11, 0.2);
        // the hinges creak as it swings (one door squeaks higher, the other groans)
        double cs = len - 0.22, wob = 2 + r.Next(), ph = r.Next() * 6;
        var creak = v == 0
            ? Creak(cs, ref r, u => 150 + 120 * Math.Sin(Math.PI * u * 0.9), 0.08, (780, 5, 1), (1450, 5, 0.7), (2350, 6, 0.3))
            : Creak(cs, ref r, u => 90 + 60 * u, 0.12, (620, 4, 1), (1180, 5, 0.6), (2100, 6, 0.25));
        creak.Shape(u => Math.Pow(Math.Sin(Math.PI * u), 1.2) * (0.7 + 0.3 * Math.Sin(2 * Math.PI * wob * u + ph)));
        x.MixAt(creak, 0.16, v == 0 ? 1.3 : 1.1);
        // air moving with the door
        x.MixAt(Synth.Pink(cs, r.NextU()).LowPass(350).Normalize(1f).Shape(u => Math.Sin(Math.PI * u)), 0.15, 0.15);
        return Out(x.LowPassQ(6000).Room(0.9, 0.18));
    }

    private static float[] DoorClose(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        double hit = v == 0 ? 0.16 : 0.12, panel = 140 + 30 * r.Next();
        var x = Synth.Silence(0.85);
        // the swing pushes air
        x.MixAt(Synth.Pink(hit, r.NextU()).LowPass(450).Normalize(1f).Shape(u => u * u), 0, 0.22);
        // the latch tongue rides the strike plate
        x.MixAt(Burst(0.02, ref r, 2200, 2, 0.0003, 0.002), hit - 0.012, 0.35);
        // the slab meets the frame
        x.MixAt(Thud(0.35, 95, 55, 0.06, 0.001), hit, 0.85);
        x.MixAt(Ring(0.35, panel, ref r, 0.03, (1, 1, 0.06), (1.6, 0.65, 0.04), (2.5, 0.55, 0.03), (3.7, 0.4, 0.02), (5.1, 0.3, 0.012), (7.0, 0.15, 0.008)), hit, 1);
        x.MixAt(Burst(0.05, ref r, 900, 0.7, 0.001, 0.02), hit, 0.35);
        // and the latch snaps home
        x.MixAt(Latch(ref r, 1300), hit + 0.025 + 0.01 * r.Next(), 0.55);
        if (v == 1) x.MixAt(Latch(ref r, 1700), hit + 0.07, 0.15);   // the letterbox flap answers
        return Out(x.LowPassQ(6000).Room(1.0, 0.28));
    }

    /// <summary>A small steel latch: a bright snap and a short ring.</summary>
    private static float[] Latch(ref Synth.Rng r, double hz)
    {
        var x = Synth.Silence(0.08);
        x.MixAt(Burst(0.02, ref r, hz * 1.6, 1.6, 0.0003, 0.0018), 0, 1);
        x.MixAt(Ring(0.08, hz, ref r, 0.03, (1, 1, 0.018), (2.35, 0.55, 0.011), (3.7, 0.3, 0.007)), 0, 0.6);
        return x;
    }

    private static float[] DoorBell(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        const double len = 1.7;
        var x = Synth.Silence(len);
        // two little brass bells on a curled spring; the clapper swings and strikes, less each time
        double[] hz = { 1180 + 40 * r.Next(), 1480 + 50 * r.Next() };
        var bells = new float[2][];
        for (int b = 0; b < 2; b++)
        {
            bells[b] = Ring(1.2, hz[b], ref r, 0.002, (1, 1, 0.45), (1.0035, 0.55, 0.42), (2.31, 0.45, 0.25), (2.316, 0.3, 0.22),
                (3.84, 0.25, 0.12), (5.6, 0.12, 0.06));
            bells[b].MixAt(Burst(0.01, ref r, 3800, 1.2, 0.0002, 0.0006), 0, 0.2);
        }
        double[] at = { 0, 0.09, 0.17, 0.28, 0.4, 0.55, 0.74 };
        double[] amp = { 1, 0.7, 0.8, 0.5, 0.45, 0.25, 0.14 };
        for (int k = 0; k < at.Length; k++)
        {
            int b = (k + r.Int(2)) % 2;
            x.MixAt(bells[b], at[k] + 0.012 * r.Bi() * (k > 0 ? 1 : 0), amp[k] * (0.85 + 0.15 * r.Next()));
        }
        return Out(x.LowPassQ(6500).Room(0.8, 0.15).Fade(0, 0.25));
    }

    private static float[] VanDoorOpen(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.7);
        // the handle: a plastic clack
        x.MixAt(Burst(0.02, ref r, 1800, 1.5, 0.0004, 0.003), 0, 0.45);
        x.MixAt(Ring(0.05, 700, ref r, 0.03, (1, 1, 0.012), (2.2, 0.5, 0.008)), 0, 0.3);
        // the latch lets go: a steel chunk
        x.MixAt(Thud(0.1, 220, 160, 0.016, 0.001), 0.05, 0.6);
        x.MixAt(Ring(0.12, 420, ref r, 0.03, (1, 1, 0.04), (1.53, 0.7, 0.03), (2.37, 0.5, 0.025), (3.41, 0.3, 0.015)), 0.05, 0.55);
        x.MixAt(Burst(0.02, ref r, 2400, 1.4, 0.0003, 0.002), 0.05, 0.4);
        // the seal unsticks, a small squeak from the hinge as it swings
        x.MixAt(Burst(0.06, ref r, 300, 0.8, 0.004, 0.025), 0.09, 0.4);
        x.MixAt(Creak(0.22, ref r, u => 260 + 90 * u, 0.1, (1500, 6, 1), (2600, 6, 0.4)).Shape(u => Math.Sin(Math.PI * u)), 0.15, 0.12);
        // the check-strap stops it
        x.MixAt(Thud(0.1, 180, 130, 0.015, 0.001), 0.44, 0.4);
        x.MixAt(Burst(0.02, ref r, 1600, 1.6, 0.0003, 0.002), 0.44, 0.3);
        return Out(x.LowPassQ(6000));
    }

    private static float[] VanDoorClose(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.85);
        const double hit = 0.09;
        // a quick swing
        x.MixAt(Synth.Pink(hit, r.NextU()).LowPass(400).Normalize(1f).Shape(u => u * u), 0, 0.2);
        // latch strikes, then the steel door and the body take it
        x.MixAt(Latch(ref r, 950), hit - 0.004, 0.6);
        x.MixAt(Thud(0.4, 80, 48, 0.07, 0.001), hit, 1);
        x.MixAt(Ring(0.4, 160, ref r, 0.02, (1, 1, 0.08), (1.41, 0.8, 0.07), (1.93, 0.6, 0.06), (2.67, 0.5, 0.045), (3.52, 0.35, 0.035),
            (4.6, 0.25, 0.025), (6.1, 0.15, 0.018)), hit, 0.8);
        x.MixAt(Burst(0.05, ref r, 1200, 0.7, 0.001, 0.025), hit, 0.5);
        // things in the back rattle
        for (int k = 0; k < 5; k++)
            x.MixAt(Ring(0.06, 900 + 900 * r.Next(), ref r, 0.05, (1, 1, 0.015), (2.4, 0.5, 0.009)), hit + 0.04 + 0.25 * Math.Pow(r.Next(), 1.5), 0.08 + 0.08 * r.Next());
        return Out(x.LowPassQ(6000).Room(0.7, 0.12));
    }

    // ================================================================ kit

    private static float[] KitPickup(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.45);
        double lift = 0.09 + 0.04 * v + 0.02 * r.Next();
        // hands take hold: a shuffle of glove and grip on the ground
        x.MixAt(Burst(0.14, ref r, 1200 + 400 * r.Next(), 0.8, 0.03, 0.04), 0, 0.35);
        x.MixAt(Grit(0.1, 12, ref r, 1800, 1, 1.3), 0.02, 0.18);
        // it leaves the ground: a small knock of its own body
        x.MixAt(Ring(0.12, 260 + 260 * r.Next(), ref r, 0.04, (1, 1, 0.025), (1.7, 0.6, 0.018), (2.8, 0.35, 0.012)), lift, 0.45);
        x.MixAt(Thud(0.08, 170, 120, 0.015, 0.002), lift, 0.35);
        // and up
        x.MixAt(Synth.Pink(0.2, r.NextU()).LowPass(500).Normalize(1f).Shape(u => Math.Sin(Math.PI * u)), lift, 0.18);
        return Out(x.LowPassQ(5000));
    }

    private static float[] KitPutdown(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.42);
        double land = 0.05 + 0.02 * r.Next(), body = 220 + 200 * r.Next();
        // a short drop of air
        x.MixAt(Synth.Pink(land, r.NextU()).LowPass(500).Normalize(1f).Shape(u => u), 0, 0.12);
        // contact: a thump, the thing's own body, grit underneath
        x.MixAt(Thud(0.15, 140, 85, 0.025, 0.0015), land, 1);
        x.MixAt(Ring(0.15, body, ref r, 0.04, (1, 1, 0.03), (1.7, 0.6, 0.022), (2.9, 0.4, 0.014), (4.3, 0.2, 0.008)), land, 0.75);
        x.MixAt(Burst(0.05, ref r, 1200, 0.9, 0.001, 0.018), land, 0.4);
        // one corner settles after the other, a little scrape
        double settle = land + 0.05 + 0.04 * v + 0.02 * r.Next();
        x.MixAt(Thud(0.08, 160, 110, 0.015, 0.001), settle, 0.35);
        x.MixAt(Ring(0.1, body * 1.1, ref r, 0.04, (1, 1, 0.025), (1.7, 0.6, 0.018)), settle, 0.25);
        x.MixAt(Burst(0.08, ref r, 1500, 0.9, 0.01, 0.025), settle + 0.01, 0.18);
        return Out(x.LowPassQ(5000));
    }

    private static float[] KitLadder(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(1.0);
        if (v == 0)
        {
            // carried: the rungs jostle in their sockets at each step
            double[] bursts = { 0.0, 0.33, 0.64 };
            for (int b = 0; b < bursts.Length; b++)
            {
                int n = 4 + r.Int(4);
                for (int k = 0; k < n; k++)
                    x.MixAt(Clank(ref r, 0.2), 0.003 + bursts[b] + 0.08 * Math.Pow(r.Next(), 1.5), (b == 0 ? 1 : 0.75) * (0.3 + 0.5 * r.Next()) * (1 - 0.08 * k));
                if (b == 1) x.MixAt(Tube(ref r, 0.3), bursts[b] + 0.01, 0.4);
            }
        }
        else
        {
            // set against a wall: the top clanks on, the feet scrape, then it settles and rattles out
            x.MixAt(Tube(ref r, 0.45), 0.003, 1);
            x.MixAt(Burst(0.12, ref r, 1500, 0.8, 0.01, 0.04), 0.02, 0.25);
            double t = 0;
            double[] gaps = { 0.05, 0.07, 0.1, 0.13, 0.17, 0.22 };
            for (int k = 0; k < gaps.Length; k++)
            {
                t += gaps[k] * (0.85 + 0.3 * r.Next());
                x.MixAt(Clank(ref r, 0.2), t, 0.6 * Math.Pow(0.72, k));
            }
        }
        return Out(x.LowPassQ(6500).Fade(0, 0.1));
    }

    /// <summary>A small aluminium knock: a rung moving in its socket (free-bar modes 1, 2.76, 5.40).</summary>
    private static float[] Clank(ref Synth.Rng r, double s)
    {
        var x = Ring(s, 450 + 600 * r.Next(), ref r, 0.05, (1, 1, 0.06), (2.76, 0.55, 0.04), (5.4, 0.28, 0.022), (8.93, 0.12, 0.012));
        x.MixAt(Burst(0.01, ref r, 2600, 1.3, 0.0002, 0.0008), 0, 0.5);
        return x;
    }

    /// <summary>The hollow bonk of a long aluminium stile.</summary>
    private static float[] Tube(ref Synth.Rng r, double s)
    {
        var x = Ring(s, 200 + 60 * r.Next(), ref r, 0.03, (1, 1, 0.12), (2.76, 0.7, 0.08), (5.4, 0.45, 0.05), (8.93, 0.25, 0.03), (13.3, 0.12, 0.02));
        x.MixAt(Clank(ref r, s), 0.002, 0.5);
        x.MixAt(Thud(0.1, 150, 100, 0.02, 0.001), 0, 0.4);
        return x;
    }

    private static float[] KitDrum(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.95);
        const double hit = 0.1;
        // the wood strains as it's lowered
        x.MixAt(Creak(0.12, ref r, u => 45 + 20 * u, 0.25, (420, 4, 1), (900, 5, 0.5)).Shape(u => Math.Sin(Math.PI * u)), 0, 0.15);
        // down: a big wooden thump, the flanges ringing low, the ground, the coil of cable shifting on the barrel
        x.MixAt(Thud(0.5, 70, 42, 0.09, 0.002), hit, 0.85);
        x.MixAt(Ring(0.5, 120 + 15 * r.Next(), ref r, 0.03, (1, 1, 0.09), (1.52, 0.75, 0.07), (2.3, 0.6, 0.05), (3.2, 0.5, 0.035), (4.4, 0.4, 0.025), (6.1, 0.25, 0.015), (8.3, 0.15, 0.01)), hit, 1);
        x.MixAt(Burst(0.06, ref r, 800, 0.7, 0.001, 0.025), hit, 0.6);
        x.MixAt(Burst(0.08, ref r, 800, 1, 0.005, 0.035), hit + 0.02, 0.25);
        // it rocks back on its flanges, twice
        x.MixAt(Thud(0.3, 85, 55, 0.05, 0.003), hit + 0.2, 0.4);
        x.MixAt(Ring(0.3, 100, ref r, 0.03, (1, 1, 0.06), (1.52, 0.6, 0.045), (2.3, 0.4, 0.03)), hit + 0.2, 0.3);
        x.MixAt(Thud(0.2, 90, 60, 0.04, 0.003), hit + 0.34, 0.15);
        return Out(x.LowPassQ(4000));
    }

    private static float[] KitToolbox(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.75);
        double box = 210 + 50 * r.Next(), hit = v == 0 ? 0.01 : 0.06;
        // the box: hollow pressed steel; set down (v0) or lifted and swung (v1)
        x.MixAt(Ring(0.3, box, ref r, 0.03, (1, 1, 0.05), (1.38, 0.8, 0.045), (2.11, 0.6, 0.035), (2.93, 0.45, 0.025), (4.07, 0.3, 0.018), (5.6, 0.2, 0.012)), hit, v == 0 ? 0.8 : 0.45);
        x.MixAt(Thud(0.12, 150, 100, 0.02, 0.001), hit, v == 0 ? 0.6 : 0.25);
        x.MixAt(Ring(0.06, 1600, ref r, 0.03, (1, 1, 0.03), (2.7, 0.4, 0.015)), hit + 0.03, 0.25);   // the handle drops
        // the tools inside: spanners, pliers, screwdrivers clattering against each other
        int n = 11 + r.Int(6);
        for (int k = 0; k < n; k++)
        {
            double t = v == 0 ? hit + 0.01 + 0.42 * Math.Pow(r.Next(), 1.8) : (r.Next() < 0.5 ? 0 : 0.28) + 0.2 * Math.Pow(r.Next(), 1.4);
            x.MixAt(Ring(0.06, 900 + 1700 * r.Next(), ref r, 0.05, (1, 1, 0.02 + 0.015 * r.Next()), (2.4, 0.5, 0.012), (4.1, 0.25, 0.007)), t, 0.2 + 0.3 * r.Next());
        }
        return Out(x.LowPassQ(6500).Fade(0, 0.05));
    }

    private static float[] KitCrate(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.65);
        const double hit = 0.02;
        // a wooden box of parts meets the ground: thump, slats, grit
        x.MixAt(Thud(0.3, 100, 60, 0.05, 0.002), hit, 0.9);
        x.MixAt(Ring(0.3, 140 + 20 * r.Next(), ref r, 0.03, (1, 1, 0.05), (1.47, 0.7, 0.04), (2.2, 0.55, 0.03), (3.3, 0.4, 0.02), (4.6, 0.25, 0.012)), hit, 0.8);
        x.MixAt(Grit(0.04, 6, ref r, 1200, 1.5, 1.5), hit, 0.25);
        x.MixAt(Burst(0.05, ref r, 1000, 0.8, 0.001, 0.015), hit, 0.3);
        // the far corner settles
        x.MixAt(Thud(0.15, 120, 80, 0.025, 0.001), hit + 0.1 + 0.03 * r.Next(), 0.35);
        // the parts inside shift: insulators, brackets, bolts
        for (int k = 0; k < 9; k++)
            x.MixAt(Ring(0.05, 700 + 1500 * r.Next(), ref r, 0.05, (1, 1, 0.012), (2.2, 0.5, 0.008), (3.6, 0.2, 0.005)), hit + 0.02 + 0.3 * Math.Pow(r.Next(), 1.6), 0.1 + 0.15 * r.Next());
        return Out(x.LowPassQ(5500));
    }

    // ================================================================ working

    private static float[] WorkTick(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        // a small escapement: two tiny impacts 2.5 ms apart, through a little brass-and-wood resonance; tick and tock
        double hz = v == 0 ? 1450 : 1250;
        var x = Synth.Silence(0.045);
        foreach (var (t, g) in new[] { (0.0, 1.0), (0.0025, 0.45) })
        {
            x.MixAt(Ring(0.04, hz, ref r, 0.01, (1, 1, 0.005), (1.8, 0.4, 0.0035), (2.9, 0.15, 0.002)), t, g);
            x.MixAt(Ring(0.04, 430, ref r, 0.02, (1, 1, 0.007), (1.6, 0.5, 0.005)), t, 0.5 * g);
        }
        return Out(x.LowPassQ(4000).Env(0.0004, 0.012));
    }

    private static float[] WorkDone(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.32);
        // click: the catch lets go
        x.MixAt(Burst(0.02, ref r, 2300, 1.4, 0.0003, 0.0018), 0, 0.45);
        x.MixAt(Ring(0.05, 1750, ref r, 0.01, (1, 1, 0.012), (2.3, 0.4, 0.007)), 0, 0.3);
        // clunk: it drops home, solid
        const double home = 0.045;
        x.MixAt(Thud(0.25, 210, 150, 0.035, 0.001), home, 1);
        x.MixAt(Ring(0.25, 280, ref r, 0.01, (1, 1, 0.05), (1.58, 0.6, 0.035), (2.43, 0.4, 0.025), (3.6, 0.25, 0.015)), home, 0.8);
        x.MixAt(Burst(0.03, ref r, 1200, 1, 0.0005, 0.004), home, 0.3);
        return Out(x.LowPassQ(6000));
    }

    // ================================================================ tools

    private static float[] ToolDrill(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        const double len = 1.25, bite = 0.17, through = 0.93, off = 1.06;
        static double S(double z) => 1 / (1 + Math.Exp(-z));
        // motor speed 0..1: winds up, drags as the bit bites into the brick, races when it breaks through, winds down
        double Speed(double sec)
        {
            double up = 1 - Math.Exp(-sec / 0.05);
            double load = 0.12 * S((sec - bite - 0.03) / 0.015) * (1 - S((sec - through) / 0.012)) * (1 + 0.25 * Math.Sin(sec * 19));
            double down = sec < off ? 1 : Math.Exp(-(sec - off) / 0.07);
            return up * (1 - load) * (0.3 + 0.7 * down);
        }
        double Level(double sec) => Math.Min(1, sec / 0.01) * (sec < off ? 1 : Math.Exp(-(sec - off) / 0.06));
        double Working(double sec) => S((sec - bite) / 0.01) * (1 - S((sec - through) / 0.008));

        // the motor: a buzzy whine, its body around 1 kHz and a gear whine higher up
        var motor = Buzz(len, u => 185 * Speed(u * len), 0.9);
        var body = ((float[])motor.Clone()).LowPassQ(1100, 1.3).Normalize(1f);
        var whine = motor.BandPass(2700, 4).Normalize(1f);
        var x = Synth.Mix(body.Gain(0.5), whine.Gain(0.16));
        x.MixAt(Synth.Noise(len, ref r).BandPass(3500, 1.2).Normalize(1f), 0, 0.05);   // brushes
        x.Shape(u => Level(u * len) * Math.Pow(Speed(u * len), 1.2));

        // the hammer action against the brick: gritty impacts at about 44 a second while it's in the wall
        var hits = Synth.Silence(len);
        for (double t = bite; t < through; t += 1 / (44 * Math.Max(0.3, Speed(t))) * (1 + 0.06 * r.Bi()))
            hits.MixAt(Synth.Noise(0.006, ref r).Decay(0.0012), t, 0.5 + 0.5 * r.Next());
        var grit = ((float[])hits.Clone()).BandPass(1900, 0.9).Normalize(1f);
        var wall = hits.BandPass(240, 1.5).Normalize(1f);
        // brick dust grinding, rough and uneven
        var rough = Synth.Noise(len, ref r).LowPass(30).Normalize(1f);
        var grind = Synth.Noise(len, ref r).BandPass(1300, 0.7).Normalize(1f);
        for (int i = 0; i < grind.Length; i++) grind[i] *= (float)((0.5 + 0.5 * Math.Abs(rough[i])) * Working(i / (double)Synth.Rate));
        x.MixAt(grit, 0, 0.45).MixAt(wall, 0, 0.5).MixAt(grind, 0, 0.3);
        // the bit breaks through: a crumble
        x.MixAt(Grit(0.08, 25, ref r, 1400, 0.9, 1.5), through - 0.01, 0.35);
        return Out(x.LowPassQ(6000));
    }

    private static float[] ToolStaple(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        // a light tap to start it, then firmer; each tap a little duller as the staple goes home
        double[][] at = { new[] { 0, 0.28 }, new[] { 0, 0.2, 0.38 }, new[] { 0, 0.24, 0.45 } };
        double[][] force = { new[] { 0.55, 1 }, new[] { 0.5, 0.8, 1 }, new[] { 0.6, 0.75, 1 } };
        double ring = 1900 + 400 * r.Next(), wood = 210 + 40 * r.Next();
        var x = Synth.Silence(0.7);
        for (int k = 0; k < at[v].Length; k++)
        {
            double t = 0.003 + at[v][k] + (k > 0 ? 0.015 * r.Bi() : 0), g = force[v][k], home = k / (double)(at[v].Length - 1);
            x.MixAt(Burst(0.02, ref r, 2500, 1.5, 0.0002, 0.002), t, 0.75 * g);
            x.MixAt(Ring(0.08, ring * (1 + 0.08 * home), ref r, 0.02, (1, 1, 0.02 * (1 - 0.5 * home)), (1.63, 0.5, 0.012), (2.5, 0.3, 0.008)), t, 0.55 * g * (1 - 0.4 * home));
            x.MixAt(Ring(0.1, wood, ref r, 0.03, (1, 1, 0.03), (1.7, 0.6, 0.02), (2.6, 0.4, 0.012)), t, 0.5 * g);
            x.MixAt(Thud(0.06, 160, 110, 0.015, 0.001), t, 0.25 * g * (0.6 + 0.4 * home));
        }
        return Out(x.LowPassQ(6500).Room(0.6, 0.15));
    }

    private static float[] ToolClip(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.14);
        // the clip flexes, then snaps: a bright snick, the case's plastic ring, a small knock; v1 snaps twice
        x.MixAt(Grit(0.02, 4, ref r, 1500, 1.2), 0, 0.15);
        const double snap = 0.025;
        x.MixAt(Burst(0.015, ref r, 2600 + 500 * r.Next(), 1.2, 0.0002, 0.0012), snap, 1);
        x.MixAt(Ring(0.05, 900 + 400 * r.Next(), ref r, 0.04, (1, 1, 0.012), (1.8, 0.6, 0.008), (2.9, 0.4, 0.005)), snap, 0.6);
        x.MixAt(Ring(0.05, 330 + 60 * r.Next(), ref r, 0.03, (1, 1, 0.015), (1.6, 0.4, 0.01)), snap, 0.4);
        if (v == 1) x.MixAt(Burst(0.015, ref r, 2400, 1.2, 0.0002, 0.001), snap + 0.009, 0.45);
        if (v == 2) x.MixAt(Ring(0.05, 600, ref r, 0.05, (1, 1, 0.01), (2.2, 0.4, 0.006)), snap + 0.004, 0.3);
        return Out(x.LowPassQ(6500));
    }

    private static float[] ToolScrew(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        int turns = v == 0 ? 3 : 4;
        var x = Synth.Silence(v == 0 ? 1.0 : 1.25);
        double t = 0.01;
        for (int k = 0; k < turns; k++)
        {
            double tight = k / (double)(turns - 1), dur = 0.17 + 0.05 * r.Next() - 0.02 * tight;
            // the blade seats in the slot: a tiny steel tick
            x.MixAt(Burst(0.01, ref r, 3000, 1.5, 0.0002, 0.0008), t, 0.22);
            x.MixAt(Ring(0.03, 2300 + 300 * r.Next(), ref r, 0.03, (1, 1, 0.008), (2.2, 0.4, 0.005)), t, 0.15);
            // the thread bites into the plug: stick-slip friction, slower and louder as it tightens
            double rate0 = 115 - 45 * tight;
            var c = Creak(dur, ref r, u => rate0 * (1 + 0.2 * Math.Sin(Math.PI * u)), 0.25, (520, 2.5, 0.8), (1150, 3.5, 1), (2300, 4, 0.45));
            var grip = Synth.Noise(dur, ref r).LowPass(25).Normalize(1f);   // the hand's pressure wavers
            for (int i = 0; i < c.Length; i++) c[i] *= 0.55f + 0.45f * Math.Abs(grip[i]);
            c.Shape(u => Math.Pow(Math.Sin(Math.PI * u), 0.6));
            x.MixAt(c, t + 0.01, 0.45 + 0.35 * tight);
            // the hand on the handle
            x.MixAt(Burst(dur, ref r, 700, 0.8, 0.03, dur * 0.5).Shape(u => 1 - u), t, 0.08);
            t += dur + 0.07 + 0.04 * r.Next();
        }
        // the head pulls down tight
        x.MixAt(Thud(0.06, 260, 200, 0.012, 0.001), t - 0.06, 0.25);
        return Out(x.LowPassQ(6000));
    }

    private static float[] ToolHammer(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.42);
        double wood = 185 + 30 * v + 20 * r.Next();
        // steel face on wood: the strike, the timber's modes, a thump, a little ring from the head
        x.MixAt(Burst(0.04, ref r, 2000, 0.9, 0.0002, 0.006), 0.002, 0.9);
        x.MixAt(Ring(0.35, wood, ref r, 0.03, (1, 1, 0.05), (1.58, 0.8, 0.035), (2.44, 0.7, 0.025), (3.5, 0.6, 0.018), (4.9, 0.5, 0.013), (6.6, 0.45, 0.01), (8.7, 0.35, 0.008), (11.2, 0.25, 0.006)), 0.002, 1);
        x.MixAt(Thud(0.2, 120, 75, 0.03, 0.001), 0.002, 0.35);
        x.MixAt(Ring(0.06, 2400 + 300 * r.Next(), ref r, 0.02, (1, 1, 0.025), (2.6, 0.3, 0.012)), 0.002, 0.12);
        return Out(x.LowPassQ(6500).Room(0.7, 0.18));
    }

    private static float[] ToolCable(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.95);
        double[] pulls = v == 0 ? new[] { 0.0, 0.46 } : new[] { 0.0, 0.4 };
        double band = v == 0 ? 700 : 1000, end = 0;
        foreach (double p in pulls)
        {
            double dur = 0.36 + 0.06 * r.Next();
            // the jacket sliding: a hiss rising as the pull speeds up, uneven under the hand
            var hiss = Synth.Noise(dur, ref r).BandSweep(u => band * (1 + 1.4 * u), 1.3).Normalize(1f);
            var rough = Synth.Noise(dur, ref r).LowPass(40).Normalize(1f);
            for (int i = 0; i < hiss.Length; i++) hiss[i] *= 0.55f + 0.45f * Math.Abs(rough[i]);
            static double Pull(double u) => Math.Min(1, u / 0.25) * (u < 0.85 ? 0.7 + 0.3 * u : (1 - u) / 0.15);
            hiss.Shape(Pull);
            x.MixAt(hiss, p + 0.005, 0.7);
            x.MixAt(Grit(dur, v == 0 ? 30 : 55, ref r, 2500, 1.2).Shape(Pull), p + 0.005, v == 0 ? 0.22 : 0.32);
            if (v == 0)
            {
                // the drum lurches round on its spindle
                x.MixAt(Ring(0.15, 120 + 20 * r.Next(), ref r, 0.03, (1, 1, 0.04), (1.52, 0.6, 0.03), (2.3, 0.4, 0.02)), p + 0.01, 0.45);
                x.MixAt(Synth.Brown(dur, r.NextU()).LowPass(200).Normalize(1f).Shape(Pull), p, 0.2);
            }
            end = p + dur;
        }
        // the end whips free
        x.MixAt(Burst(0.05, ref r, 900, 1, 0.001, 0.012), end - 0.02, 0.4);
        return Out(x.LowPass(4500).LowPassQ(6000));
    }

    private static float[] ToolDig(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.62);
        double push = 0.1 + 0.03 * v + 0.02 * r.Next();
        // the blade goes in: a scrape on grit and stones, a faint ring of the steel
        x.MixAt(Burst(0.06, ref r, 2100, 1, 0.002, 0.03), 0.003, 0.5);
        x.MixAt(Grit(0.06, 15, ref r, 2800, 1.2, 1.2), 0.003, 0.3);
        x.MixAt(Ring(0.15, 520 + 120 * r.Next(), ref r, 0.03, (1, 1, 0.06), (2.1, 0.6, 0.04), (3.4, 0.4, 0.03), (5.0, 0.2, 0.02)), 0.003, 0.2);
        // a boot on the spade: it sinks through the soil
        var soil = Synth.Noise(0.3, ref r).BandPass(600, 0.7).Normalize(1f).Env(0.02, 0.1);
        x.MixAt(soil, 0.04, 0.65);
        x.MixAt(Grit(0.3, 45, ref r, 1200, 0.9, 1.4).Decay(0.12), 0.04, 0.4);
        x.MixAt(Thud(0.2, 100, 60, 0.04, 0.003), push, 0.45);
        // levered up: earth trickles off the blade
        x.MixAt(Burst(0.25, ref r, 900, 0.8, 0.03, 0.08), push + 0.2, 0.2);
        x.MixAt(Grit(0.25, 18, ref r, 1500, 1, 1.5).Decay(0.1), push + 0.2, 0.2);
        return Out(x.LowPassQ(5000));
    }

    private static float[] ToolThump(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.36);
        double body = v == 0 ? 175 : 215;
        // the heel of the hand: soft-fronted, heavy, on a hollow plastic case
        x.MixAt(Thud(0.25, 150, 82, 0.04, 0.003), 0, 0.9);
        x.MixAt(Ring(0.25, body, ref r, 0.03, (1, 1, 0.045), (1.62, 0.8, 0.032), (2.47, 0.6, 0.022), (3.6, 0.4, 0.014), (5.0, 0.25, 0.009)), 0.001, 0.9);
        x.MixAt(Burst(0.04, ref r, 900, 0.7, 0.002, 0.01), 0, 0.3);
        // something loose inside complains
        int n = 4 + r.Int(4);
        for (int k = 0; k < n; k++)
            x.MixAt(Ring(0.04, 1300 + 1200 * r.Next(), ref r, 0.05, (1, 1, 0.008), (2.3, 0.4, 0.005)), 0.012 + 0.12 * Math.Pow(r.Next(), 1.4), 0.12 + 0.12 * r.Next());
        return Out(x.LowPassQ(6000));
    }

    // ================================================================ money and paper

    private static float[] CashCoins(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(1.25);
        // counted out one at a time into a cupped palm, the last two quicker
        double[] at = { 0, 0.16, 0.3, 0.45, 0.58, 0.74, 0.86 };
        for (int k = 0; k < at.Length; k++)
        {
            double t = 0.003 + at[k] + (k > 0 ? 0.02 * r.Bi() : 0), g = 0.7 + 0.3 * r.Next();
            x.MixAt(Coin(ref r, 0.15), t, g);
            x.MixAt(Coin(ref r, 0.1), t + 0.008 + 0.015 * r.Next(), g * 0.35);   // it settles against the others
            x.MixAt(Thud(0.05, 300, 200, 0.008, 0.002), t, 0.12);              // the palm
        }
        return Out(x.LowPassQ(6000));
    }

    /// <summary>A coin striking coins: a thin disc's inharmonic modes, damped by the hand.</summary>
    private static float[] Coin(ref Synth.Rng r, double s)
    {
        var x = Ring(s, 1400 + 800 * r.Next(), ref r, 0.02, (1, 1, 0.035), (1.73, 0.6, 0.026), (2.33, 0.45, 0.02));
        x.MixAt(Burst(0.01, ref r, 3000, 1.2, 0.0002, 0.0007), 0, 0.35);
        return x;
    }

    private static float[] CashTill(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(1.7);
        // two keys rung up: chunky keys on levers
        foreach (double t in new[] { 0.003, 0.13 })
        {
            x.MixAt(Burst(0.02, ref r, 1500, 1.4, 0.0005, 0.0025), t, 0.45);
            x.MixAt(Ring(0.06, 650 + 100 * r.Next(), ref r, 0.03, (1, 1, 0.014), (2.1, 0.5, 0.009), (3.3, 0.25, 0.006)), t, 0.35);
            x.MixAt(Thud(0.05, 230, 180, 0.01, 0.001), t, 0.3);
        }
        // the total lever: the works let go
        x.MixAt(Thud(0.15, 170, 110, 0.03, 0.001), 0.3, 0.6);
        x.MixAt(Ring(0.12, 380, ref r, 0.03, (1, 1, 0.04), (1.43, 0.8, 0.035), (2.2, 0.5, 0.025), (3.1, 0.3, 0.016)), 0.3, 0.5);
        x.MixAt(Burst(0.02, ref r, 2000, 1.3, 0.0003, 0.002), 0.3, 0.4);
        // ching
        var bell = Ring(1.3, 1550, ref r, 0.002, (1, 1, 0.55), (1.0045, 0.6, 0.5), (2.43, 0.45, 0.28), (2.44, 0.25, 0.25), (3.98, 0.22, 0.14), (5.3, 0.1, 0.07));
        bell.MixAt(Burst(0.01, ref r, 4000, 1.2, 0.0002, 0.0006), 0, 0.25);
        x.MixAt(bell, 0.33, 0.75);
        // the drawer runs out on its rollers, coins sliding in the tray
        const double from = 0.36, run = 0.24, stop = from + run;
        var roll = Synth.Noise(run, ref r).BandPass(500, 1.2).LowPass(1500).Normalize(1f).Shape(u => Math.Min(1, u * 8) * (1 - 0.3 * u) * (0.7 + 0.3 * Math.Sin(2 * Math.PI * 9 * u)));
        x.MixAt(roll, from, 0.3);
        x.MixAt(Synth.Brown(run, r.NextU()).LowPass(300).Normalize(1f).Shape(u => Math.Min(1, u * 8)), from, 0.3);
        for (int k = 0; k < 6; k++) x.MixAt(Coin(ref r, 0.08), from + 0.01 + run * r.Next(), 0.08 + 0.08 * r.Next());
        // and hits its stop
        x.MixAt(Thud(0.25, 130, 80, 0.04, 0.001), stop, 0.8);
        x.MixAt(Ring(0.2, 270, ref r, 0.04, (1, 1, 0.06), (1.38, 0.8, 0.05), (2.11, 0.6, 0.04), (2.93, 0.4, 0.03), (4.07, 0.25, 0.02)), stop, 0.6);
        for (int k = 0; k < 8; k++) x.MixAt(Coin(ref r, 0.1), stop + 0.005 + 0.09 * Math.Pow(r.Next(), 1.5), 0.12 + 0.15 * r.Next());
        return Out(x.LowPassQ(7000).Fade(0, 0.2));
    }

    private static float[] PaperPage(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        double len = v == 0 ? 0.55 : 0.42, land = len - 0.14;
        var x = Synth.Silence(len);
        // fingers lift the corner
        x.MixAt(Burst(0.06, ref r, 2200, 0.9, 0.01, 0.015), 0.003, 0.15);
        x.MixAt(Grit(0.05, 8, ref r, 2800, 1.2), 0.005, 0.12);
        // the page swings over: a swish rising and falling, fluttering
        double sw = land - 0.04, flut = 22 + 8 * r.Next();
        var swish = Synth.Noise(sw, ref r).BandSweep(u => 700 + 1300 * Math.Sin(Math.PI * u), 0.9).LowPass(4000).Normalize(1f);
        swish.Shape(u => Math.Pow(Math.Sin(Math.PI * u), 1.5) * (0.75 + 0.25 * Math.Sin(2 * Math.PI * flut * u * sw)));
        x.MixAt(swish, 0.04, 0.7);
        x.MixAt(Grit(sw, 25, ref r, 2600, 1).Shape(u => Math.Sin(Math.PI * u)), 0.04, 0.16);
        // and lands flat
        x.MixAt(Burst(0.06, ref r, 450, 0.8, 0.003, 0.025), land, 0.4);
        x.MixAt(Burst(0.03, ref r, 1500, 1, 0.002, 0.01), land, 0.2);
        return Out(x.LowPassQ(6000));
    }

    private static float[] PaperSlip(string id, int v)
    {
        var r = new Synth.Rng(Seed(id, v));
        var x = Synth.Silence(0.45);
        // the slip drags up the spike: the hole tears and rubs
        x.MixAt(Grit(0.12, 70, ref r, 2300, 1, 0.8).Shape(u => 0.4 + 0.6 * u), 0.003, 0.55);
        x.MixAt(Burst(0.12, ref r, 1800, 2, 0.02, 0.06).Shape(u => 0.5 + 0.5 * u), 0.003, 0.3);
        // pops off the tip
        x.MixAt(Burst(0.04, ref r, 1200, 1, 0.001, 0.01), 0.12, 0.5);
        x.MixAt(Ring(0.05, 2700, ref r, 0.02, (1, 1, 0.01), (2.3, 0.4, 0.006)), 0.12, 0.12);
        // a flick as it's handed over
        var flick = Synth.Noise(0.22, ref r).BandSweep(u => 1600 - 600 * u, 1).Normalize(1f).Shape(u => Math.Sin(Math.PI * u));
        x.MixAt(flick, 0.16, 0.3);
        return Out(x.LowPassQ(6500));
    }

    // ================================================================ helpers (pure: local state only)

    /// <summary>Modes above this are dropped (nothing in the foley should whistle).</summary>
    private const double MaxModeHz = 7500;

    /// <summary>
    /// Finish a recipe: take out any DC, then set the level so the peak is 0.9 after <see cref="Render"/>'s own DC block
    /// (which shifts the phase of the low thumps and can push a plainly normalised peak to the edge).
    /// </summary>
    private static float[] Out(float[] x)
    {
        x.DcBlock();
        float after = Synth.Peak(((float[])x.Clone()).DcBlock());
        return after < 1e-9f ? x : x.Gain(0.9 / after);
    }

    /// <summary>A short fade over the last 20 ms (or a quarter of a short buffer), so a ring cut off at the buffer's end doesn't click.</summary>
    private static float[] EndFade(float[] x) => x.Fade(0, Math.Min(0.02, x.Seconds() * 0.25));

    /// <summary>A struck body's low thump: a sine sliding from hz0 down to hz1 as it decays (a foot, a box, a door). Peak 1.</summary>
    private static float[] Thud(double s, double hz0, double hz1, double tau, double attack = 0.0015)
    {
        var x = new float[Synth.N(s)];
        double ph = 0, glide = 1 / Math.Max(1e-4, tau * 0.6);
        int a = Synth.N(attack);
        for (int i = 0; i < x.Length; i++)
        {
            double t = i / (double)Synth.Rate;
            ph += 2 * Math.PI * (hz1 + (hz0 - hz1) * Math.Exp(-t * glide)) / Synth.Rate;
            double env = i < a ? i / (double)a : Math.Exp(-(i - a) / (double)Synth.Rate / tau);
            x[i] = (float)(Math.Sin(ph) * env);
        }
        return EndFade(x.DcBlock()).Normalize(1f);   // a struck sine's first lobe leaves DC behind
    }

    /// <summary>White noise through a band, struck: attack then decay (seconds). Peak 1.</summary>
    private static float[] Burst(double s, ref Synth.Rng r, double hz, double q, double attack, double tau) =>
        EndFade(Synth.Noise(s, ref r).BandPass(hz, q).Env(attack, tau)).Normalize(1f);

    /// <summary>Struck modes (ratio to hz, amplitude, decay seconds), each ratio nudged by up to ±jitter and each amplitude by ±20%, so variants differ. Peak 1.</summary>
    private static float[] Ring(double s, double hz, ref Synth.Rng r, double jitter, params (double ratio, double amp, double decay)[] modes)
    {
        var m = new List<(double, double, double)>(modes.Length);
        foreach (var (ratio, amp, decay) in modes)
        {
            double q = ratio * (1 + jitter * r.Bi());
            if (hz * q > MaxModeHz) continue;
            m.Add((q, amp * (0.8 + 0.4 * r.Next()), decay));
        }
        return EndFade(Synth.Modal(s, hz, m.ToArray()).Env(0.0003, 1e3)).Normalize(1f);
    }

    /// <summary>Tiny impacts scattered through s seconds (bunched toward the start when skew > 1), through a band: grit, crunch, crackle. Peak 1.</summary>
    private static float[] Grit(double s, int count, ref Synth.Rng r, double hz, double q, double skew = 1)
    {
        var x = new float[Synth.N(s)];
        for (int k = 0; k < count; k++)
        {
            int at = (int)(s * Math.Pow(r.Next(), skew) * Synth.Rate), len = 8 + r.Int(40);
            float g = (r.Next() < 0.5f ? -1f : 1f) * (0.3f + 0.7f * r.Next());
            for (int i = 0; i < len && at + i < x.Length; i++) x[at + i] += g * r.Bi() * (float)Math.Exp(-i / (len * 0.35));
        }
        return x.BandPass(hz, q).Normalize(1f);
    }

    /// <summary>
    /// Stick-slip friction (a hinge, a floorboard, a screw in a plug): an uneven train of impulses at rate(u) per second
    /// (u 0..1 through the sound, jittered by ±jitter), rung through resonances (centre Hz, Q, level). Peak 1.
    /// </summary>
    private static float[] Creak(double s, ref Synth.Rng r, Func<double, double> rate, double jitter, params (double hz, double q, double amp)[] res)
    {
        var train = new float[Synth.N(s)];
        for (double t = 0; t < s; t += 1 / Math.Max(5, rate(t / s) * (1 + jitter * r.Bi())))
        {
            int at = (int)(t * Synth.Rate);
            if (at >= train.Length) break;
            float g = 0.5f + 0.5f * r.Next();
            train[at] += g;
            if (at + 1 < train.Length) train[at + 1] -= 0.3f * g;
        }
        var y = new float[train.Length];
        foreach (var (hz, q, amp) in res) y.MixAt(((float[])train.Clone()).BandPass(hz, q).Normalize(1f), 0, amp);
        return y.LowPass(4000).Normalize(1f);   // the bare impulses leak hiss above the resonances: keep it warm
    }

    /// <summary>A buzzy harmonic tone following hzAt(u), harmonics falling as 1/k^tilt and stopping below maxHz: a motor. Peak 1.</summary>
    private static float[] Buzz(double s, Func<double, double> hzAt, double tilt, double maxHz = 6000)
    {
        var x = new float[Synth.N(s)];
        double ph = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double hz = hzAt(i / (double)x.Length);
            ph += 2 * Math.PI * hz / Synth.Rate;
            if (ph > 2 * Math.PI) ph -= 2 * Math.PI;
            double val = 0;
            for (int k = 1; k <= 48 && k * hz < maxHz; k++) val += Math.Sin(k * ph) / Math.Pow(k, tilt);
            x[i] = (float)val;
        }
        return x.Normalize(1f);
    }
}
