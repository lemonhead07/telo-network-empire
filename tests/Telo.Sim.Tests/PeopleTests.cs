using Telo.Sim.Content;
using Telo.Sim.Plan;
using Telo.Sim.Town;
using Telo.Sim.World;
using Xunit;

namespace Telo.Sim.Tests;

/// <summary>People who walk (D44, EA2c): a seeded errand a day, and nobody at the door while they're out.</summary>
public class PeopleTests
{
    private static Simulation PastFirstHour(ulong seed)
    {
        var sim = Simulation.NewGame(TestContent.Db, seed);
        sim.AddCustomer("Ada Firth", "family", 30); sim.AddCustomer("Bo Wren", "family", 30);   // past the first hour: people keep hours
        Assert.True(Onboarding.Reached(sim.State, sim.Content, OnboardingStep.Free));
        return sim;
    }

    [Fact]
    public void The_same_seed_and_day_give_the_same_trips_and_every_household_has_somewhere_to_go()
    {
        var a = PastFirstHour(7); var b = PastFirstHour(7);
        var c = a.Content;
        var homes = c.Lots.Values.Where(l => l.Kind == LotKind.House && l.Premise is null && !l.Business).ToList();
        var went = new HashSet<string>();
        int firstWeek = 0;
        for (int day = a.State.Day; day < a.State.Day + 28; day++)
            foreach (var lot in homes)
            {
                var trips = PeopleRules.Day(a.State, c, lot, day);
                Assert.Equal(trips, PeopleRules.Day(b.State, c, lot, day));
                foreach (var o in trips)
                {
                    var to = c.Lots[o.To];
                    Assert.Equal(lot.Site, to.Site);                                    // a walk, on the same street
                    Assert.True(PeopleRules.Along(lot, to) <= PeopleRules.MaxErrandM);
                    Assert.True(o.Back < 16 * 60, $"{lot.Id} still out at {Clock.TextAt(o.Back)}");   // home before the lights go off
                    went.Add(lot.Id);
                    if (day < a.State.Day + 7) firstWeek++;
                }
            }
        Assert.True(firstWeek > homes.Count, $"only {firstWeek} trips in a week from {homes.Count} homes");
        Assert.Empty(homes.Where(l => !went.Contains(l.Id)).Select(l => l.Id));   // nobody is stuck with nowhere to go
        // businesses mind the counter
        Assert.All(c.Lots.Values.Where(l => l.Business || l.Kind != LotKind.House), l => Assert.Empty(PeopleRules.Day(a.State, c, l, a.State.Day)));
    }

    [Fact]
    public void While_they_are_out_nobody_answers_and_a_booked_day_keeps_them_in()
    {
        var sim = PastFirstHour(7);
        var s = sim.State; var c = sim.Content;
        LotDef? lot = null; Outing? trip = null;
        for (int i = 0; i < 7 && trip is null; i++)
        {
            foreach (var l in c.Lots.Values.OrderBy(l => l.Id, StringComparer.Ordinal))
                if (PeopleRules.Day(s, c, l, s.Day) is [var o, ..]) { lot = l; trip = o; break; }
            if (trip is null) sim.AdvanceDay();
        }
        Assert.NotNull(trip);

        s.Minute = trip!.Leave + 1;                                              // just left
        Assert.Equal(WhereKind.Walking, PeopleRules.Now(s, c, lot!).Kind);
        var why = HomeRules.WhyNot(s, c, lot!);
        Assert.NotNull(why);
        Assert.False(HomeRules.IsHome(s, c, lot!));
        var to = c.Lots[trip.To].Resident.Replace("The ", "").Split(" (")[0];
        Assert.Contains(to, why, StringComparison.OrdinalIgnoreCase);           // the door says where they went
        Assert.Contains($"Back by {Clock.TextAt(trip.Back)}", why);
        s.Minute = trip.Arrive;
        Assert.Equal(WhereKind.There, PeopleRules.Now(s, c, lot!).Kind);
        s.Minute = trip.Back;                                                    // home again
        Assert.Null(HomeRules.WhyNot(s, c, lot!));

        // they've booked you in for this afternoon: no errands today, not even before the window
        s.Appointments.Add(new Appointment { TicketKey = "install:test", LotId = lot!.Id, Day = s.Day, Start = trip.Back + 120, End = trip.Back + 240, Status = AppointmentStatus.Confirmed });
        s.Minute = trip.Leave + 1;
        Assert.Null(HomeRules.WhyNot(s, c, lot));
        Assert.Equal(WhereKind.Home, PeopleRules.Now(s, c, lot).Kind);
    }
}
