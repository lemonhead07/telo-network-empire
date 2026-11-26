namespace Telo.Game.Audio;

/// <summary>Foley: footsteps, doors, kit, tools, paper, money, and the hold-to-work tick (EA7).</summary>
public static partial class Sounds
{
    static partial void RegisterFoley()
    {
        const string F = "foley";
        // placeholders: each id is the contract; replace the recipe, keep the id
        Add(F, "step.grass", "a footstep on grass or earth", v => Placeholder(300), variants: 6, gainDb: -16);
        Add(F, "step.road", "a footstep on tarmac or pavement", v => Placeholder(320), variants: 6, gainDb: -16);
        Add(F, "step.floor", "a footstep indoors: floorboards under carpet", v => Placeholder(340), variants: 6, gainDb: -16);
        Add(F, "step.metal", "a footstep on steel: the data centre's raised floor, a ladder rung, the jetty", v => Placeholder(360), variants: 4, gainDb: -16);
        Add(F, "step.land", "landing from a jump", v => Placeholder(280), variants: 2, gainDb: -12);
        Add(F, "door.knock", "three knocks on a front door", v => Placeholder(200), variants: 3, gainDb: -6);
        Add(F, "door.open", "a house door opening: latch, creak", v => Placeholder(220), variants: 2, gainDb: -8);
        Add(F, "door.close", "a house door closing: thud, latch", v => Placeholder(240), variants: 2, gainDb: -8);
        Add(F, "door.bell", "the bell over a shop door", v => Placeholder(1200), gainDb: -10);
        Add(F, "van.door.open", "the van's door opening (handle, hinge)", v => Placeholder(260), gainDb: -8);
        Add(F, "van.door.close", "the van's door slammed shut", v => Placeholder(180), gainDb: -6);
        Add(F, "kit.pickup", "picking something up: a shuffle and lift", v => Placeholder(500), variants: 3, gainDb: -12);
        Add(F, "kit.putdown", "putting something down on the ground", v => Placeholder(450), variants: 3, gainDb: -12);
        Add(F, "kit.ladder", "an aluminium ladder rattling as it's carried or set against a wall", v => Placeholder(700), variants: 2, gainDb: -10);
        Add(F, "kit.drum", "a heavy cable drum set down: a wooden thump", v => Placeholder(120), gainDb: -8);
        Add(F, "kit.toolbox", "a steel toolbox with tools rattling inside", v => Placeholder(650), variants: 2, gainDb: -10);
        Add(F, "kit.crate", "a wooden parts crate set down", v => Placeholder(150), gainDb: -8);
        Add(F, "work.tick", "a soft tick while you hold E to work (about four a second)", v => Placeholder(1800), variants: 2, gainDb: -20);
        Add(F, "work.done", "the satisfying click when a held job finishes", v => Placeholder(900), gainDb: -10);
        Add(F, "tool.drill", "a hand drill through a wall, about a second", v => Placeholder(400), gainDb: -10);
        Add(F, "tool.staple", "a cable staple hammered or stapled in", v => Placeholder(800), variants: 3, gainDb: -10);
        Add(F, "tool.clip", "a plastic clip or cover snapping home", v => Placeholder(1500), variants: 3, gainDb: -10);
        Add(F, "tool.screw", "a screwdriver turning a few times", v => Placeholder(600), variants: 2, gainDb: -12);
        Add(F, "tool.hammer", "a hammer blow on wood", v => Placeholder(250), variants: 3, gainDb: -8);
        Add(F, "tool.cable", "cable pulled off a drum or through a hole: a rustle and zip", v => Placeholder(550), variants: 2, gainDb: -12);
        Add(F, "tool.dig", "a spade into earth", v => Placeholder(170), variants: 3, gainDb: -10);
        Add(F, "tool.thump", "the bodge: a thump with the heel of the hand on something plastic", v => Placeholder(160), variants: 2, gainDb: -6);
        Add(F, "cash.coins", "coins counted into a hand: being paid", v => Placeholder(2000), gainDb: -10);
        Add(F, "cash.till", "a shop till: bell and drawer", v => Placeholder(2200), gainDb: -10);
        Add(F, "paper.page", "a page turned", v => Placeholder(1000), variants: 2, gainDb: -14);
        Add(F, "paper.slip", "a paper slip pulled off a spike or handed over", v => Placeholder(1100), gainDb: -14);
    }
}
