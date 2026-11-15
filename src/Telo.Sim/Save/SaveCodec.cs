using System.Text.Json;
using Telo.Sim.Content;
using Telo.Sim.World;

namespace Telo.Sim.Save;

/// <summary>Versioned JSON save. Serialise the simulation, rebuild the view — never save engine objects.</summary>
public static class SaveCodec
{
    public static string ToJson(GameState s) => JsonSerializer.Serialize(s, ContentDb.Json);

    /// <param name="content">Needed to migrate saves from before staff were named people (v5 and earlier). Optional otherwise.</param>
    public static GameState FromJson(string json, ContentDb? content = null)
    {
        var s = JsonSerializer.Deserialize<GameState>(json, ContentDb.Json) ?? throw new InvalidDataException("Empty save");
        if (s.Version > GameState.CurrentVersion)
            throw new InvalidDataException($"Save is from a newer build (v{s.Version}).");
        // Migrate older versions one step at a time (v1 -> v2 -> ...).
        if (s.Version < 2) { s.Faults ??= new(); s.TechInstalls = 0; } // v2: surfaced faults became repair visits; staff installs are counted
        if (s.Version < 3) s.Lines ??= new(); // v3: pole lines built by hand
        if (s.Version < 4) // v4: exchanges and lines in, line cards, evening reports, saved installs, head starts
        {
            s.Installs ??= new(); s.HeadStartDays ??= new(); s.Orders ??= new();
            foreach (var cust in s.Customers) cust.Products ??= new();
            if (s.Exchanges is null || s.Exchanges.Count == 0) s.Exchanges = new() { new Exchange { Id = "ex-town", Name = "Main Street" } };
        }
        if (s.Version < 5) s.BoardFiles ??= new(); // v5: Board accounts (default: everyone already let in), file area, night mail
        if (s.Version < 6) // v6: the clock, staff as named people, job tickets, the depot
        {
            s.StaffMembers ??= new(); s.Assigned ??= new();
            Work.WorkRules.RestoreHires(s, content);
        }
        if (s.Version < 7) { s.Switch ??= new(); Net.SwitchRules.Rebuild(s, content); } // v7: the switchboard is a persistent object
        if (s.Version < 8) s.Actors ??= new(); // v8: actors (place, hands, kit, van) live in the simulation
        if (s.Version < 9) s.Texts ??= new(); // v9: texts on PIP's phone
        if (s.Version < 10) s.Plans ??= new(); // v10: the day planner
        if (s.Version < 11) s.NezLots ??= new(); // v11: Nez takes territory
        if (s.Version < 12) { s.SleepDebt = 0; s.LastSleep = Town.SleepRules.Need; } // v12: sleep debt; older saves wake rested
        // v13: ads and the daily job cap (new fields default to zero: no ads, nothing done today)
        if (s.Version < 14) s.Appointments ??= new(); // v14: appointments and the job clock
        // v15: service level (line-days up, counted from now on) and VIP bookings (none yet): new fields default to zero/null
        s.Version = GameState.CurrentVersion;
        return s;
    }
}
