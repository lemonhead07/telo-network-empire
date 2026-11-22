using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Build;

/// <summary>
/// The Yard (EA2b, D43): the builders' and line merchants on the east edge of town where every project's parts are bought.
/// A project's cost is paid at the Yard's counter, not at the site. Small parts go in a crate on your van and you carry
/// the crate to the site; big ones (racks, steel, a generator) come on the Yard's lorry the next working morning.
/// Only once the parts are on site can the project start. Paperwork projects (a lease, a handshake) have no parts.
/// </summary>
public static class YardRules
{
    public const int OpensHour = 7, ClosesHour = 17;
    /// <summary>Unloading the parts at the site (or checking the lorry's delivery against the order).</summary>
    public const int UnloadMinutes = 10;
    /// <summary>Buying at the counter and loading the van.</summary>
    public const int BuyMinutes = 10;

    public static LotDef? Lot(ContentDb c) => c.Lots.Values.FirstOrDefault(l => l.Kind == LotKind.Yard);
    public static string Name(ContentDb c) => Lot(c)?.Resident ?? "the Yard";

    public static bool NeedsParts(ProjectDef p) => p.Parts != PartsBy.None;
    public static bool Bought(GameState s, string projectId) => s.Parts.ContainsKey(projectId);

    /// <summary>Crates riding on your van: one for each van order not yet unloaded.</summary>
    public static int OnVan(GameState s) => s.Parts.Values.Count(o => !o.Lorry);

    /// <summary>Projects whose parts the counter will sell you now: open to you, not started, nothing bought yet.</summary>
    public static IEnumerable<ProjectDef> ForSale(GameState s, ContentDb c) =>
        c.Projects.Where(p => NeedsParts(p) && !Bought(s, p.Id) && !ProjectRules.Started(s, p.Id) && ProjectRules.Available(s, c, p));

    /// <summary>Why the counter won't sell you this project's parts (null when it will). Opening hours are the door's business.</summary>
    public static string? CantBuy(GameState s, ContentDb c, ProjectDef p)
    {
        if (!NeedsParts(p)) return "nothing to buy: it's paperwork";
        if (Bought(s, p.Id)) return "already bought";
        if (ProjectRules.Locked(s, c, p) is { } locked) return locked;
        if (ProjectRules.Started(s, p.Id)) return "already under way";
        return s.Cash < p.Cost ? $"the parts are ${p.Cost}; you have ${s.Cash}" : null;
    }

    /// <summary>Pay for the parts. A crate goes on the van now; a lorry load leaves for the site the next working morning.</summary>
    public static bool Buy(GameState s, ContentDb c, ProjectDef p)
    {
        if (CantBuy(s, c, p) is not null) return false;
        s.Cash -= p.Cost;
        bool lorry = p.Parts == PartsBy.Lorry;
        s.Parts[p.Id] = new PartsOrder { Lorry = lorry, ArrivesDay = lorry ? NextDelivery(s.Day) : s.Day };
        return true;
    }

    /// <summary>The lorry goes out first thing the next day the Yard is open.</summary>
    public static int NextDelivery(int day)
    {
        int d = day + 1;
        while (Clock.Weekday(d) == DayOfWeek.Sunday) d++;
        return d;
    }

    /// <summary>
    /// What stops this project starting, in words; null when its parts are on site (or it has none and you can pay).
    /// <paramref name="onSite"/> is the kit in your hands or put down here.
    /// </summary>
    public static string? NotOnSite(GameState s, ContentDb c, ProjectDef p, Tools onSite)
    {
        if (!NeedsParts(p)) return s.Cash < p.Cost ? $"${p.Cost} to start; you have ${s.Cash}" : null;
        if (!s.Parts.TryGetValue(p.Id, out var order)) return $"buy the parts at {Name(c)} (${p.Cost})";
        if (order.Lorry) return order.ArrivesDay > s.Day ? $"the Yard's lorry brings the parts on {new GameDate(order.ArrivesDay)}" : null;
        return (onSite & Tools.Parts) != 0 ? null : "bring the parts crate from the van";
    }

    /// <summary>The parts are unloaded: the order is done with. (The crate leaves your hands; the caller says whose.)</summary>
    public static void Unload(GameState s, ProjectDef p) => s.Parts.Remove(p.Id);

    /// <summary>The lorry has left the parts at the site (or will today).</summary>
    public static bool LorryArrived(GameState s, string projectId) => s.Parts.TryGetValue(projectId, out var o) && o.Lorry && o.ArrivesDay <= s.Day;

    /// <summary>The Yard keeps trade hours: seven till five, shut on Sundays (no hours during the first hour of the game).</summary>
    public static bool Shut(GameState s, ContentDb c, LotDef lot) =>
        lot.Kind == LotKind.Yard && Onboarding.Reached(s, c, OnboardingStep.Free)
        && (Clock.Weekday(s.Day) == DayOfWeek.Sunday || Clock.Hour(s) is < OpensHour or >= ClosesHour);

    public static string WhyShut(GameState s) =>
        Clock.Weekday(s.Day) == DayOfWeek.Sunday ? "shut on Sundays" : "shut. Open seven till five, Monday to Saturday";

    /// <summary>Shut all day today (a Sunday): not worth pointing the player there.</summary>
    public static bool ShutAllDay(GameState s, ContentDb c) =>
        Onboarding.Reached(s, c, OnboardingStep.Free) && Clock.Weekday(s.Day) == DayOfWeek.Sunday;

    /// <summary>The Yard grows with the acts (D43): a loading bay once its lorry has big parts to deliver.</summary>
    public static bool HasBay(GameState s, ContentDb c) => c.Projects.Any(p => p.Parts == PartsBy.Lorry && p.Act <= s.ActIndex);
}
