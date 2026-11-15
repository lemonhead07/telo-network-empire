using Telo.Sim.Minigames.Install;
using Telo.Sim.Town;
using Telo.Sim.World;
using Xunit;

namespace Telo.Sim.Tests;

public class ServiceTests
{
    /// <summary>A game past the first hour with one new customer waiting for a line, made a VIP due the day after tomorrow.</summary>
    private static (Simulation Sim, Customer Vip) WithVip()
    {
        var sim = Simulation.NewGame(TestContent.Db, 11);
        var s = sim.State;
        s.Modems = 10;
        for (int i = 0; i < 4; i++) sim.AddCustomer($"Sub {i}", "family", 30);
        s.MessagesWaiting = 1;
        var vip = sim.TakeEnquiry()!;
        vip.VipDay = s.Day + ServiceRules.VipLeadDays;
        return (sim, vip);
    }

    private static InstallJob Fitted(Simulation sim, Customer c, bool bodge, bool test)
    {
        var job = sim.NewInstallJob(c.Id)!;
        foreach (var d in InstallJob.Steps.Where(d => d.Step != InstallStep.Test))
        {
            if (bodge && d.CanRush && job.Rush(d.Step)) { bodge = false; continue; }
            for (int k = 0; k < d.Clicks; k++) job.Do(d.Step);
        }
        if (test) for (int k = 0; k < 3 && !job.Do(InstallStep.Test); k++) foreach (var d in InstallJob.Steps) job.Do(d.Step);   // fix what the test finds
        return job;
    }

    [Fact]
    public void A_VIP_done_on_the_day_unbodged_and_tested_pays_and_raises_reputation()
    {
        var (sim, vip) = WithVip();
        var s = sim.State;
        long cash = s.Cash; int rep = BookingRules.Reputation(s);
        Assert.True(sim.ApplyInstallOutcome(Fitted(sim, vip, bodge: false, test: true).Evaluate()));
        Assert.Equal(cash + ServiceRules.VipBonus, s.Cash);
        Assert.True(BookingRules.Reputation(s) > rep);
        Assert.Null(vip.VipDay);
        Assert.True(vip.Live);
    }

    [Fact]
    public void A_bodged_VIP_goes_to_NezTel_and_costs_a_little_face()
    {
        var (sim, vip) = WithVip();
        var s = sim.State;
        long cash = s.Cash; int rep = BookingRules.Reputation(s);
        Assert.True(sim.ApplyInstallOutcome(Fitted(sim, vip, bodge: true, test: false).Evaluate()));
        Assert.False(vip.Live);
        Assert.Contains(vip.LotId!, s.NezLots);
        Assert.Equal(cash, s.Cash);
        Assert.True(BookingRules.Reputation(s) < rep);
    }

    [Fact]
    public void Service_is_counted_in_nines_and_a_nine_better_than_NezTel_brings_its_customers_over()
    {
        var sim = Simulation.NewGame(TestContent.Db, 11);
        var s = sim.State;
        for (int i = 0; i < 6; i++) sim.AddCustomer($"Sub {i}", "family", 30);
        Assert.Equal("99.0%", ServiceRules.Percent(ServiceRules.NezTelPpm));
        while (!Onboarding.Reached(s, sim.Content, OnboardingStep.Free)) sim.GodNextStage(null);
        foreach (var x in s.Customers.Where(x => x.Live)) x.Installed = true;
        s.Faults.Clear();
        var lot = sim.Content.Lots.Values.First(l => l.Kind == Content.LotKind.House && TownRules.HouseholdOf(s, l.Id).Status != HouseholdStatus.Signed);
        s.NezLots.Add(lot.Id);
        for (int d = 0; d < 21; d++) { s.Faults.Clear(); sim.AdvanceDay(); }   // three weeks without a fault
        Assert.True(ServiceRules.Nines(ServiceRules.Ppm(s)) >= 3, ServiceRules.Percent(ServiceRules.Ppm(s)));
        Assert.DoesNotContain(lot.Id, s.NezLots);
        Assert.Contains(s.Log, e => e.Text.Contains("fed up with NezTel"));
    }
}
