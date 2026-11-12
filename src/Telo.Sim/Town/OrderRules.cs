using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Minigames.OddJob;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>
/// The catalogue: things an existing subscriber can order besides the line itself (see content/products.json).
/// Each order is a short hands-on visit with its own steps and kit, pays a price today and sometimes a little more
/// every month, and some change how that household uses the Board. New pages in the Almanac add new things to sell.
/// </summary>
public static class OrderRules
{
    public const int ExpiresAfterDays = 10;

    public static bool OnSale(GameState s, ProductDef p) =>
        s.ActIndex >= p.RequiresAct && (p.RequiresEntry is null || s.Entries.GetValueOrDefault(p.RequiresEntry) is EntryStatus.Recalled or EntryStatus.Drifting);

    /// <summary>What this customer could still be sold.</summary>
    public static IEnumerable<ProductDef> Sellable(GameState s, ContentDb c, Customer cust) =>
        c.Products.Values.Where(p => OnSale(s, p) && !cust.Products.Contains(p.Id) && (p.Archetypes.Count == 0 || p.Archetypes.Contains(cust.Archetype))).OrderBy(p => p.Id, StringComparer.Ordinal);

    public static Order? OrderAt(GameState s, string lotId) =>
        TownRules.CustomerAt(s, lotId) is { } cust ? s.Orders.FirstOrDefault(o => o.CustomerId == cust.Id) : null;

    /// <summary>A subscriber rings to order something. Only people whose line is live, who live somewhere you can go indoors, and who aren't already waiting.</summary>
    public static Order? Offer(GameState s, ContentDb c, DetRng rng)
    {
        var buyers = s.Customers.Where(x => x.Live && x.Installed && x.LotId is not null && c.Lots.TryGetValue(x.LotId, out var lot) && lot.Kind == LotKind.House
                                            && s.Orders.All(o => o.CustomerId != x.Id) && Sellable(s, c, x).Any()).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        if (buyers.Count == 0) return null;
        var cust = buyers[rng.Next(buyers.Count)];
        var wants = Sellable(s, c, cust).ToList();
        var product = wants[rng.Next(wants.Count)];
        var order = new Order { Id = s.NewId("order"), CustomerId = cust.Id, ProductId = product.Id, PlacedDay = s.Day };
        s.Orders.Add(order);
        s.Emit(EventKind.Info, $"{cust.Name} rings: \"{product.Pitch}\" {product.Name}, ${product.Price}.", cust.LotId);
        return order;
    }

    public static OddJobTask NewTask(ContentDb c, Order order)
    {
        var p = c.Products[order.ProductId];
        return new OddJobTask(order.Id, p.Name, 1, p.Steps.Select(st => new OddStepDef(st.Verb, st.Clicks, st.Minutes, st.Needs.Aggregate(Tools.None, (a, b) => a | b))).ToList());
    }

    /// <summary>Fitted and handed over. A bodge is paid for today and comes back as a fault.</summary>
    public static bool Complete(GameState s, ContentDb c, DetRng rng, string orderId, bool bodged)
    {
        var order = s.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order is null || s.Customers.FirstOrDefault(x => x.Id == order.CustomerId) is not { } cust) return false;
        var p = c.Products[order.ProductId];
        s.Orders.Remove(order);
        s.Cash += p.Price - p.Cost;
        cust.MonthlyFee += p.MonthlyExtra;
        cust.Products.Add(p.Id);
        s.OrdersDone++;
        if (bodged)
            s.Defects.Add(new LatentDefect { Id = s.NewId("defect"), SurfaceDay = s.Day + rng.Range(5, 30), Source = "order", CustomerId = cust.Id, Text = p.BodgeDefect });
        else cust.Satisfaction = Math.Min(100, cust.Satisfaction + 5);
        s.Emit(EventKind.Money, $"{p.Name} fitted at {cust.Name}'s: ${p.Price - p.Cost} after Sol's bill{(p.MonthlyExtra > 0 ? $", and ${p.MonthlyExtra} more a month" : "")}.", cust.LotId);
        return true;
    }

    public static void Tick(GameState s, ContentDb c)
    {
        foreach (var o in s.Orders.Where(o => s.Day - o.PlacedDay > ExpiresAfterDays).ToList())
        {
            s.Orders.Remove(o);
            if (s.Customers.FirstOrDefault(x => x.Id == o.CustomerId) is not { } cust) continue;
            cust.Satisfaction = Math.Max(0, cust.Satisfaction - 3);
            s.Emit(EventKind.Info, $"{cust.Name} stopped waiting and got their {c.Products[o.ProductId].Name.ToLowerInvariant()} from the national phone monopoly.", cust.LotId);
        }
    }
}
