namespace Telo.Sim.Town;

/// <summary>Kit that lives in the van and has to be carried to where the work is.</summary>
[Flags]
public enum Tools { None = 0, CableDrum = 1, Ladder = 2, Toolbox = 4, ModemBox = 8, All = 15 }
