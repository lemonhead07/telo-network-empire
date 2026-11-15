using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

/// <summary>The bookings loop (D37): three jobs a day by hand, bookings that won't fit, and customers who give up and go to the monopoly.</summary>
public class BookingTests
{
    private static Simulation PastTheFirstHour(ulong seed)
    {
        var sim = Simulation.NewGame(TestContent.Db, seed);
        sim.AddCustomer("Ada Firth", "family", 30); sim.AddCustomer("Bo Wren", "family", 30);   // the round is learned
        Assert.True(Onboarding.Reached(sim.State, sim.Content, OnboardingStep.Free));
        return sim;
    }

    [Fact]
    public void Three_jobs_is_a_full_day_and_the_count_starts_again_in_the_morning()
    {
        var sim = PastTheFirstHour(31);
        var s = sim.State;
        for (int i = 0; i < 3; i++) sim.OfferJob();
        Assert.True(s.Jobs.Count >= 3);
        foreach (var j in s.Jobs.Take(3).ToList()) TownRules.CompleteJob(s, sim.Content, j.Id);
        Assert.Equal(BookingRules.FullDay, BookingRules.DayFull(s));
        sim.OfferJob();
        var me = sim.Actor();
        me.IgnoreHours = true;
        var lot = s.Jobs.First().LotId;
        me.Place = lot;
        var gen = new Telo.Sim.Planet.PlanetGen(31);
        Assert.Equal(BookingRules.FullDay, Telo.Sim.Act.Actions.WhyNot(sim, me, gen, $"oddjob:{s.Jobs.First().Id}"));
        sim.Sleep();
        Assert.Null(BookingRules.DayFull(s));
        Assert.All(TestContent.Db.Work.Values.SelectMany(w => w.Stages).Where(st => st.Hire is not null),
            st => Assert.InRange(st.Hire!.JobsPerDay, 1, BookingRules.MaxJobsPerDay));
    }

    [Fact]
    public void A_booking_left_too_long_goes_to_the_monopoly_and_the_house_is_theirs()
    {
        var sim = PastTheFirstHour(32);
        var s = sim.State;
        for (int i = 0; i < 6; i++) { s.MessagesWaiting = 1; sim.TakeEnquiry(); }
        var waiting = s.Customers.Where(x => x.Live && !x.SocketFitted && x.LotId is not null).ToList();
        Assert.True(waiting.Count >= 4);
        Assert.All(waiting, x => Assert.NotNull(BookingRules.Of(s, $"install:{x.Id}")));
        int wanted = waiting.Max(x => BookingRules.Of(s, $"install:{x.Id}")!.WantedDay);
        int first = waiting.Min(x => BookingRules.Of(s, $"install:{x.Id}")!.WantedDay);
        while (s.Day <= first) sim.AdvanceDay();
        Assert.True(BookingRules.Late(s) > 0, "nobody went: the first booking is now late");
        Assert.All(waiting, x => Assert.True(x.Live));                       // late, but they haven't given up yet
        while (s.Day <= wanted + BookingRules.GraceDays) sim.AdvanceDay();
        foreach (var x in waiting)
        {
            Assert.False(x.Live);
            Assert.Contains(x.LotId!, s.NezLots);
            Assert.Equal(KnockResult.AlreadySigned, sim.Knock(x.LotId!));
        }
        Assert.Empty(s.Bookings.Keys.Where(k => k.StartsWith("install:")));
        // the calendar survives a save
        s.MessagesWaiting = 1; sim.TakeEnquiry();
        var back = Telo.Sim.Save.SaveCodec.FromJson(Telo.Sim.Save.SaveCodec.ToJson(s), sim.Content);
        Assert.Equal(s.Bookings.Keys.OrderBy(k => k), back.Bookings.Keys.OrderBy(k => k));
    }
}
