using Telo.Sim.Act;
using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Nez;

/// <summary>How far into meeting Nez the player is (spine step 8). Derived from the save, never stored.</summary>
public enum NezStage { Background, Notice, Competition, Territory }

/// <summary>
/// Nez: the national phone monopoly with a face and an address, at the exact opposite point of the planet (D34, N1).
/// Never bodges, never goes broke, but slow and indifferent: it connects one house a week once it has noticed you,
/// and never the last house on the road (N3). Its "time" on any job is the by-the-book par the player can beat.
/// </summary>
public static class NezRules
{
    public const string Id = "nez", Exchange = "nadir";
    public const int NoticeAt = 10, CompetitionAt = 12, TerritoryAt = 15;
    /// <summary>Nez does it by the book: half as long again as the player's own time for the same job.</summary>
    public const int ParPermille = 1500;

    public static NezStage Stage(GameState s) =>
        s.LiveSubscribers >= TerritoryAt ? NezStage.Territory
        : s.LiveSubscribers >= CompetitionAt ? NezStage.Competition
        : s.LiveSubscribers >= NoticeAt ? NezStage.Notice
        : NezStage.Background;

    /// <summary>Nez's by-the-book time for a job the player does in <paramref name="minutes"/>.</summary>
    public static int Par(int minutes) => minutes * ParPermille / 1000;

    /// <summary>Nez's par for a whole house install.</summary>
    public static int InstallPar => Par(InstallJob.Steps.Sum(d => d.Clicks * d.MinutesPerClick) + InstallJob.TestMinutes);

    /// <summary>Nez's exchange, Nadir: the antipode of your town.</summary>
    public static V3 NadirDir(PlanetGen gen) => gen.Town.Dir.Normalized() * -1;

    /// <summary>Nez as an actor: same shape as the player, its own owner and region (AGES.md section 6).</summary>
    public static ActorState Actor(Simulation sim)
    {
        var a = sim.Actor(Id);
        a.Owner = Id; a.Region = Exchange; a.Place = Exchange; a.VanAt = Exchange;
        return a;
    }

    /// <summary>Once a week, from the Territory stage: Nez connects one house you have not reached.
    /// Never a Promise Ledger home, never the farm or the last house on the road.</summary>
    public static void Week(GameState s, ContentDb c)
    {
        var stage = Stage(s);
        if (stage >= NezStage.Notice && !s.NezNoticed)
        {
            s.NezNoticed = true;
            s.Emit(EventKind.Info, "A van in the national phone monopoly's colours has parked at the end of your street. Nobody gets out.");
        }
        if (stage >= NezStage.Competition && !s.NezCompeting)
        {
            s.NezCompeting = true;
            s.Emit(EventKind.Info, "A leaflet through every door: the national phone monopoly is \"now taking orders in your area\". Waiting list: six weeks.");
        }
        if (stage < NezStage.Territory) return;
        var take = TownRules.Prospects(s, c).FirstOrDefault(l => l.Kind == LotKind.House);
        if (take is null) return;
        s.NezLots.Add(take.Id);
        s.Emit(EventKind.Info, $"{take.Resident} signed with the national phone monopoly. Their van was outside for three weeks.", take.Id);
    }

    public static bool Taken(GameState s, string lotId) => s.NezLots.Contains(lotId);
}
