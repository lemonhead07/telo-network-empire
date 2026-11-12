using Telo.Sim.Net;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

public class CatalogueTests
{
    private static Simulation WholeTown()
    {
        var sim = TestContent.NewGame();
        var gen = new Telo.Sim.Planet.PlanetGen(1983);
        for (int i = 0; i < 8; i++) sim.GodNextStage(gen);
        return sim;
    }

    [Fact]
    public void SubscribersOrderDifferentThings_EachFittedByHandForMoneyNowAndLater()
    {
        var sim = WholeTown();
        var s = sim.State; var c = sim.Content;
        var seen = new HashSet<string>();
        for (int i = 0; i < 12; i++)
        {
            var order = sim.OfferOrder();
            if (order is null) break;
            var cust = s.Customers.First(x => x.Id == order.CustomerId);
            var product = c.Products[order.ProductId];
            seen.Add(product.Id);
            Assert.True(product.Archetypes.Count == 0 || product.Archetypes.Contains(cust.Archetype));
            var task = OrderRules.NewTask(c, order);
            Assert.Equal(product.Steps.Count, task.Steps.Count);
            while (!task.Done) Assert.True(task.Do());
            long cash = s.Cash, fee = cust.MonthlyFee;
            Assert.True(sim.CompleteOrder(order.Id, task.Bodged));
            Assert.Equal(cash + product.Price - product.Cost, s.Cash);
            Assert.Equal(fee + product.MonthlyExtra, cust.MonthlyFee);
            Assert.Contains(product.Id, cust.Products);
        }
        Assert.True(seen.Count >= 4, $"only {seen.Count} kinds of product were ordered");
        Assert.DoesNotContain("prod-fastmodem", seen);       // not until its page is recalled from the Almanac
        Assert.Empty(s.Defects.Where(d => d.Source == "order"));
    }

    [Fact]
    public void AThumpedOrderComesBackAsAFault_AndAFastModemShortensCalls()
    {
        var sim = WholeTown();
        var s = sim.State; var c = sim.Content;
        var order = sim.OfferOrder()!;
        var task = OrderRules.NewTask(c, order);
        task.Bodge();
        sim.CompleteOrder(order.Id, task.Bodged);
        Assert.Contains(s.Defects, d => d.Source == "order");

        s.Entries["alm-2400"] = EntryStatus.Recalled;
        Assert.Contains(s.Customers, x => OrderRules.Sellable(s, c, x).Any(p => p.Id == "prod-fastmodem"));
        var ex = BoardRules.Home(s); ex.LinesIn = 2;
        int Engaged() => Enumerable.Range(0, 30).Sum(d => BoardRules.Evening(s, 900 + d, ex, c).Engaged);
        int before = Engaged();
        foreach (var cust in s.Customers) cust.Products.Add("prod-fastmodem");
        Assert.True(Engaged() < before, "everyone on fast modems should free the lines sooner");
    }
}
