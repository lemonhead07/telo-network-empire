using Telo.Sim.Content;
using Telo.Sim.Net;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>One thing that needs the player, wherever they are.</summary>
public sealed record Need(string Key, string Text, string? LotId);

/// <summary>
/// The pager: what needs you right now, derived from the state every time (never stored). You get the pager with your
/// first hire, because that is when you can first be somewhere else while the town carries on.
/// </summary>
public static class Attention
{
    public static bool HasPager(GameState s) => s.Staff > 0;

    public static List<Need> Needs(GameState s, ContentDb c)
    {
        var needs = new List<Need>();
        foreach (var f in s.Faults)
            if (s.Customers.FirstOrDefault(x => x.Id == f.CustomerId) is { } cust)
                needs.Add(new Need($"fault:{f.Id}", $"Fault at {cust.Name}'s: {f.Text}.", cust.LotId));
        foreach (var o in s.Orders)
            if (s.Customers.FirstOrDefault(x => x.Id == o.CustomerId) is { } buyer && c.Products.TryGetValue(o.ProductId, out var p))
                needs.Add(new Need($"order:{o.Id}", $"{buyer.Name} has ordered: {p.Name.ToLowerInvariant()}.", buyer.LotId));
        if (s.MessagesWaiting > 0) needs.Add(new Need("messages", $"{s.MessagesWaiting} message(s) on the answering machine.", null));
        bool tech = c.Work.ContainsKey("install") && WorkRules.Current(s, c, "install").Mode != WorkMode.Manual;
        if (tech && s.PendingInstalls > 0 && s.Modems == 0) needs.Add(new Need("modems", "Your technician is out of modems.", null));
        if (BoardService.Waiting(s) is { Count: > 0 } waiting) needs.Add(new Need("callers", $"{waiting.Count} new caller(s) waiting to be let in to the Board.", null));
        if (s.LineCards > 0) needs.Add(new Need("card", "A line card is waiting to be fitted.", null));
        if (s.LastEvening is { GaveUp.Count: >= 2 } eve)
            needs.Add(new Need($"engaged:{eve.Day}", $"The Board was engaged last night. {eve.GaveUp.Count} people gave up.", null));
        return needs;
    }
}
