namespace Telo.Sim.Town;

/// <summary>Kit that lives in the van and has to be carried to where the work is.</summary>
[Flags]
public enum Tools { None = 0, CableDrum = 1, Ladder = 2, Toolbox = 4, ModemBox = 8, Pole = 16, LineCard = 32, Parts = 64, All = 127 }
// Parts (EA2b): a crate of a project's parts, bought at the Yard; it rides on the van until it is carried to the site.
