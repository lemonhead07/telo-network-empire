using Telo.Sim.Phone;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

/// <summary>The morning text tells the player when there's more than a day's work waiting (owner, 2026-11-26).</summary>
public class MorningTextTests
{
    [Fact]
    public void A_busy_morning_says_two_jobs_is_a_full_day_and_the_pole_line_will_keep()
    {
        var sim = TestContent.NewGame();
        for (int i = 0; i < 5; i++) sim.GodNextStage(null);   // the first hour: from here the phone rings
        var s = sim.State;
        Assert.True(Onboarding.PhoneRings(s, sim.Content));
        foreach (var c in s.Customers) c.Installed = true;   // nothing waiting: an ordinary day says nothing extra
        s.Faults.Clear(); s.Orders.Clear();
        Assert.Null(PhoneRules.Today(s, sim.Content));

        sim.AddCustomer("Ada Hale", "family", 10, installed: false);
        sim.AddCustomer("Gus Moss", "gamer", 12, installed: false);
        var farm = sim.Content.Premises.Values.First(p => p.PoleLine is not null);
        s.Lines[farm.Id] = new LineProgress { Planted = 3, Strung = 2 };

        var today = PhoneRules.Today(s, sim.Content);
        Assert.NotNull(today);
        Assert.Contains("2 jobs waiting", today);
        Assert.Contains("3 poles up", today);
        Assert.Contains("full day", today);
    }
}
