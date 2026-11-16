using Telo.Sim.Planet;
using Telo.Sim.Plan;
using Telo.Sim.Town;
using Telo.Sim.World;
using Xunit;

namespace Telo.Sim.Tests;

public class AppointmentTests
{
    [Fact]
    public void Planning_asks_them_to_be_in_overnight_they_agree_they_are_home_for_it_and_a_missed_visit_is_remembered()
    {
        var sim = Simulation.NewGame(TestContent.Db, 11);
        var gen = new PlanetGen(11);
        var s = sim.State; var c = sim.Content;
        s.Modems = 20;
        sim.AddCustomer("Ada Firth", "family", 30); sim.AddCustomer("Bo Wren", "family", 30);   // past the first hour: people keep hours
        for (int i = 0; i < 12; i++) { s.MessagesWaiting = 1; sim.TakeEnquiry(); }
        int tomorrow = s.Day + 1;
        while (Clock.IsWeekend(tomorrow)) tomorrow++;

        // a household that is out at work, wanted at eleven on a weekday
        var away = Work.StaffRules.Tickets(s, c).First(t => t.LotId is { } l && HomeRules.HasCar(c.Lots[l]));
        var lot = c.Lots[away.LotId!];
        Assert.NotNull(PlanRules.AwayAt(s, c, lot, tomorrow, 5 * 60));
        var sheet = PlanRules.PlanFor(s, tomorrow);
        sheet.Slots.Clear();
        sheet.Slots.Add(new PlanSlot { TicketKey = away.Key });
        PlanRules.Lay(s, c, gen, sheet);
        sheet.Slots[0].Start = 5 * 60;
        AppointmentRules.Sync(s, c, sheet, Work.StaffRules.Tickets(s, c).ToDictionary(t => t.Key));
        var appt = AppointmentRules.For(s, away.Key)!;
        Assert.Equal(AppointmentStatus.Asked, appt.Status);

        // they ring back overnight: agreed (eleven, or the evening if they can't get off work)
        while (s.Day < tomorrow) sim.Sleep();
        Assert.Equal(AppointmentStatus.Confirmed, appt.Status);
        Assert.Contains(appt.Start, new[] { 5 * 60, AppointmentRules.EveningStart });
        s.Minute = appt.Start + 10;
        Assert.Null(HomeRules.WhyNot(s, c, lot));                         // home for it
        Assert.StartsWith("Booked", Guidance.Next(s, c).Text);            // and it's what to do now

        // nobody came: they remember (the job clock never started, so the world ran at its usual pace)
        Assert.Null(AppointmentRules.OnTheClock(s));
        sim.Sleep();
        Assert.Null(AppointmentRules.For(s, away.Key));
        Assert.Contains(s.Log, e => e.Text.Contains("nobody came"));
    }

    [Fact]
    public void Work_inside_the_window_starts_the_job_clock_and_quick_work_earns_a_tip_slow_work_does_not()
    {
        foreach (int took in new[] { 40, 100 })   // game minutes on the job: inside the first hour, and after it
        {
            var sim = Simulation.NewGame(TestContent.Db, 11);
            var s = sim.State; var c = sim.Content;
            s.Modems = 20;
            sim.AddCustomer("Ada Firth", "family", 30);
            for (int i = 0; i < 4; i++) { s.MessagesWaiting = 1; sim.TakeEnquiry(); }
            var t = Work.StaffRules.Tickets(s, c).First(x => x.Kind == Work.TicketKind.Install && x.LotId is not null);
            var a = new Appointment { TicketKey = t.Key, LotId = t.LotId!, Day = s.Day, Start = 3 * 60, End = 3 * 60 + AppointmentRules.Window, Status = AppointmentStatus.Confirmed };
            s.Appointments.Add(a);
            s.Minute = a.Start;

            // start the install at their house: the job clock starts
            var cust = s.Customers.First(x => $"install:{x.Id}" == t.Key);
            var job = sim.NewInstallJob(cust.Id)!;
            sim.Installs[t.LotId!] = job;
            sim.AdvanceMinutes(1);
            Assert.NotNull(AppointmentRules.OnTheClock(s));

            // the work: every step done properly, then signed off
            sim.AdvanceMinutes(took);
            foreach (var d in Minigames.Install.InstallJob.Steps.Where(d => d.Step != Minigames.Install.InstallStep.Test))
                for (int k = 0; k < d.Clicks; k++) job.Do(d.Step);
            Assert.True(sim.ApplyInstallOutcome(job.Evaluate()));
            sim.Installs.Remove(t.LotId!);
            long before = s.Cash;
            sim.AdvanceMinutes(1);
            Assert.NotNull(a.Done);
            Assert.Null(AppointmentRules.OnTheClock(s));
            Assert.Equal(took <= AppointmentRules.BonusMinutes ? before + AppointmentRules.BonusPay : before, s.Cash);
        }
    }
}
