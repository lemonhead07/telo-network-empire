namespace Telo.Game.Audio;

/// <summary>The line: telephone tones, the switchboard, the test set, PIP, the answering machine and screens (EA7).</summary>
public static partial class Sounds
{
    static partial void RegisterLine()
    {
        const string L = "line";
        // placeholders: each id is the contract; replace the recipe, keep the id
        Add(L, "line.dial", "dial tone, heard down the line (loops)", v => Synth.Silence(1), loop: true, gainDb: -14);
        Add(L, "line.dead", "a dead line: the click, then nothing but faint hiss", v => Placeholder(100), gainDb: -14);
        Add(L, "line.engaged", "the engaged tone, a few cycles", v => Placeholder(400), gainDb: -14);
        Add(L, "line.ringback", "ringing heard by the caller, a couple of rings", v => Placeholder(420), gainDb: -14);
        Add(L, "line.crossed", "a crossed line: someone else's call murmuring faintly (no words)", v => Placeholder(430), gainDb: -16);
        Add(L, "line.modem", "a modem handshake down the line, three or four seconds", v => Placeholder(1300), gainDb: -14);
        Add(L, "line.hook", "a handset lifted or put down: the hook switch", v => Placeholder(900), variants: 2, gainDb: -12);
        Add(L, "phone.ring", "a house telephone's bell, one double ring", v => Placeholder(1400), gainDb: -8);
        Add(L, "sw.jumper.in", "a jumper wire pushed into a terminal on the frame", v => Placeholder(1600), variants: 3, gainDb: -10);
        Add(L, "sw.jumper.out", "a jumper wire pulled out", v => Placeholder(1500), variants: 3, gainDb: -10);
        Add(L, "sw.relay", "a relay clicking over on the switchboard", v => Placeholder(2500), variants: 3, gainDb: -12);
        Add(L, "sw.probe", "the tone probe's warble as it reads a tag", v => Placeholder(1000), gainDb: -14);
        Add(L, "sw.test.ok", "the test set: a good line", v => Placeholder(880), gainDb: -12);
        Add(L, "sw.test.bad", "the test set: a bad port", v => Placeholder(220), gainDb: -12);
        Add(L, "sw.throw", "the big switch: a heavy clunk, then relays rippling across the board", v => Placeholder(150), gainDb: -6);
        Add(L, "sw.card", "a line card slid into the rack and seated", v => Placeholder(700), gainDb: -10);
        Add(L, "pip.beep", "PIP's pager beep: something needs you", v => Placeholder(2400), gainDb: -10);
        Add(L, "pip.text", "a text arriving on PIP", v => Placeholder(2000), gainDb: -12);
        Add(L, "pip.blip", "PIP's voice: one syllable blip (played in a run as it speaks)", v => Placeholder(1700), variants: 6, gainDb: -16);
        Add(L, "am.beep", "the answering machine's beep: a message waiting", v => Placeholder(1000), gainDb: -10);
        Add(L, "ui.key", "a terminal key pressed", v => Placeholder(3000), variants: 4, gainDb: -14);
        Add(L, "ui.move", "moving between choices on a screen", v => Placeholder(1800), gainDb: -16);
        Add(L, "ui.select", "choosing something on a screen", v => Placeholder(1200), gainDb: -12);
        Add(L, "ui.back", "stepping back from a screen", v => Placeholder(800), gainDb: -14);
        Add(L, "ui.error", "a screen refusing: can't do that", v => Placeholder(180), gainDb: -12);
    }
}
