using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.World;

namespace Telo.Sim.Net;

public enum ThrowResult { Live, Dead, Crossed }

/// <summary>
/// THE SWITCHBOARD: the board on your wall where each subscriber's pair is jumpered to a port of the Board.
/// It is one persistent object, not a puzzle per visit: every wire stays where it was put, by you or by anyone else.
///  • a new subscriber gets a terminal and an assigned port; some name tags are smudged (tone-probe to read them)
///  • some ports are bad (test before trusting; a bad port's subscriber goes on a spare instead)
///  • every live wire is somebody's telephone: pull it or plug over it and they go dead
///  • chaos: hot-wire with an old dirty jumper. Works today, latent defect later
///  • staff jumpers can be loose; you can see them, and re-seat them before they become faults
///  • the board holds only so many subscribers until another panel is bolted on
/// </summary>
public static class SwitchRules
{
    public const int Spares = 2, MaxStage = 1;
    public const long PanelPrice = 120;

    public static int Size(BoardState b) => b.Stage == 0 ? 8 : 16;
    /// <summary>The two spare ports keep the same numbers however big the board grows, so a wire on one never has to move.</summary>
    public const int SpareBase = 16;
    public static IEnumerable<int> SparePorts => Enumerable.Range(SpareBase, Spares);
    /// <summary>Every port on the board: one per subscriber terminal, then the spares.</summary>
    public static List<int> PortList(BoardState b) => Enumerable.Range(0, Size(b)).Concat(SparePorts).ToList();
    public static bool IsSpare(BoardState b, int port) => port >= SpareBase;
    public static bool Full(GameState s) => s.Switch.Positions.Count >= Size(s.Switch);
    public static BoardPosition? Pos(GameState s, string customerId) => s.Switch.Positions.FirstOrDefault(p => p.CustomerId == customerId);
    public static Customer? Who(GameState s, BoardPosition p) => s.Customers.FirstOrDefault(c => c.Id == p.CustomerId);
    public static bool IsLive(GameState s, BoardPosition p) => Who(s, p) is { Live: true, Installed: true };
    /// <summary>Terminals whose subscriber is waiting to be switched on.</summary>
    public static List<BoardPosition> Pending(GameState s) => s.Switch.Positions.Where(p => Who(s, p) is { Live: true, Installed: false }).ToList();
    /// <summary>Subscribers whose house is wired but who have no terminal because the board is full.</summary>
    public static List<Customer> Unseated(GameState s) => s.Customers.Where(c => c.Live && !c.Installed && c.SocketFitted && Pos(s, c.Id) is null).ToList();

    /// <summary>Give a subscriber a terminal and an assigned port. The first two are plain; after that tags smudge and ports go bad.</summary>
    public static bool Seat(GameState s, Customer cust)
    {
        var b = s.Switch;
        if (Pos(s, cust.Id) is not null) return true;
        if (Full(s)) return false;
        var taken = b.Positions.Select(p => p.AssignedPort).ToHashSet();
        int port = Enumerable.Range(0, Size(b)).First(i => !taken.Contains(i));
        var rng = new DetRng(DetRng.Derive(s.Seed, $"seat:{cust.Id}"));
        bool twists = b.Positions.Count >= 2;
        bool smudged = twists && rng.ChancePermille(350);
        int sparesOwed = b.Positions.Count(p => b.FaultyPorts.Contains(p.AssignedPort));
        if (twists && rng.ChancePermille(250) && !b.FaultyPorts.Contains(port) && sparesOwed < Spares) b.FaultyPorts.Add(port);
        b.Positions.Add(new BoardPosition { CustomerId = cust.Id, AssignedPort = port, TagRead = !smudged });
        return true;
    }

    public static void SeatWaiting(GameState s) { foreach (var c in Unseated(s)) Seat(s, c); }

    /// <summary>Tone-probe a terminal: the smudged tag can be read.</summary>
    public static bool ReadTag(GameState s, string customerId)
    {
        if (Pos(s, customerId) is not { TagRead: false } p) return false;
        p.TagRead = true;
        return true;
    }

    /// <summary>Hold the test set to a port. True if it is good.</summary>
    public static bool TestPort(GameState s, int port)
    {
        if (!s.Switch.TestedPorts.Contains(port)) s.Switch.TestedPorts.Add(port);
        return !s.Switch.FaultyPorts.Contains(port);
    }

    public static bool? KnownGood(GameState s, int port) => s.Switch.TestedPorts.Contains(port) ? !s.Switch.FaultyPorts.Contains(port) : null;
    public static BoardPosition? OnPort(GameState s, int port) => s.Switch.Positions.FirstOrDefault(p => p.Jumper == port);

    /// <summary>Run a jumper from a subscriber's terminal to a port. A port takes one wire: whoever was on it is pulled.</summary>
    /// <returns>The id of a live subscriber cut off by this, if any.</returns>
    public static string? Jumper(GameState s, string customerId, int port)
    {
        if (Pos(s, customerId) is not { } p || !PortList(s.Switch).Contains(port)) return null;
        string? cut = Unplug(s, p);
        if (OnPort(s, port) is { } other) cut = Unplug(s, other) ?? cut;
        p.Jumper = port;
        return cut;
    }

    /// <summary>Pull a subscriber's jumper out. If their line was live, it isn't now, and they know who did it.</summary>
    public static string? Pull(GameState s, string customerId) => Pos(s, customerId) is { } p ? Unplug(s, p) : null;

    private static string? Unplug(GameState s, BoardPosition p)
    {
        if (p.Jumper < 0) return null;
        p.Jumper = -1; p.HotWired = false; p.Loose = false;
        if (Who(s, p) is not { Live: true, Installed: true } cust) return null;
        cust.Installed = false;
        cust.Satisfaction = Math.Max(0, cust.Satisfaction - 15);
        s.Emit(EventKind.Defect, $"You pulled {cust.Name}'s jumper. They have no dial tone and know exactly who did it.", cust.Id);
        return cust.Id;
    }

    /// <summary>Chaos option: reuse an old dirty jumper. No need to read the tag or test the port. It works today.</summary>
    public static bool HotWire(GameState s, string customerId)
    {
        if (Pos(s, customerId) is not { } p || IsLive(s, p)) return false;
        Unplug(s, p);
        if (OnPort(s, p.AssignedPort) is { } other) Unplug(s, other);
        p.Jumper = p.AssignedPort; p.HotWired = true;
        return true;
    }

    /// <summary>Push a loose jumper home before it becomes somebody's fault.</summary>
    public static bool Reseat(GameState s, string customerId)
    {
        if (Pos(s, customerId) is not { Loose: true } p) return false;
        p.Loose = false;
        s.Defects.RemoveAll(d => d.CustomerId == customerId && d.Source == "technician");
        return true;
    }

    /// <summary>Throw the test switch: every waiting subscriber with a jumper is tried.</summary>
    public static List<(string CustomerId, ThrowResult Result)> Throw(GameState s, ContentDb c, DetRng rng)
    {
        var b = s.Switch;
        var results = new List<(string, ThrowResult)>();
        bool clean = true;
        foreach (var p in Pending(s).Where(p => p.Jumper >= 0))
        {
            var cust = Who(s, p)!;
            bool assignedBad = b.FaultyPorts.Contains(p.AssignedPort);
            ThrowResult r =
                p.HotWired ? ThrowResult.Live
                : p.Jumper == p.AssignedPort ? (assignedBad ? ThrowResult.Dead : ThrowResult.Live)
                : IsSpare(b, p.Jumper) ? (assignedBad ? ThrowResult.Live : ThrowResult.Dead)
                : b.Positions.Any(o => o != p && o.AssignedPort == p.Jumper) ? ThrowResult.Crossed
                : ThrowResult.Dead;
            results.Add((cust.Id, r));
            switch (r)
            {
                case ThrowResult.Live:
                    if (p.EverLive) cust.Installed = true; else Simulation.GoLive(s, c, cust);
                    p.EverLive = true;
                    if (p.HotWired)
                    {
                        clean = false;
                        s.Defects.Add(new LatentDefect { Id = s.NewId("defect"), SurfaceDay = s.Day + rng.Range(10, 45), Source = "frame", CustomerId = cust.Id, Text = "a dirty pair you hot-wired in a hurry" });
                    }
                    break;
                case ThrowResult.Dead:
                    clean = false;
                    cust.Satisfaction = Math.Max(0, cust.Satisfaction - 5);
                    break;
                case ThrowResult.Crossed:
                    clean = false;
                    cust.Satisfaction = Math.Max(0, cust.Satisfaction - 30);
                    s.Emit(EventKind.Defect, $"Crossed lines: {cust.Name} can hear a stranger's calls. Privacy incident logged.", cust.Id);
                    break;
            }
        }
        if (clean && results.Count >= 3) s.Emit(EventKind.Milestone, "Every line live, first time. The switchboard is a thing of beauty.");
        return results;
    }

    /// <summary>Somebody else wires a subscriber in (staff, or a decree): terminal, right port, switched on. A sloppy job leaves a loose jumper you can see.</summary>
    public static bool AutoWire(GameState s, ContentDb c, Customer cust, bool sloppy)
    {
        if (!Seat(s, cust)) return false;
        var b = s.Switch; var p = Pos(s, cust.Id)!;
        int port = p.AssignedPort;
        if (b.FaultyPorts.Contains(port))
        {
            int spare = SparePorts.FirstOrDefault(i => OnPort(s, i) is null, -1);
            if (spare >= 0) port = spare; else b.FaultyPorts.Remove(port);   // no spare left: they mend the port instead
        }
        if (OnPort(s, port) is { } other && other != p) Unplug(s, other);
        p.Jumper = port; p.TagRead = true; p.HotWired = false; p.Loose = sloppy;
        if (!cust.Installed) { if (p.EverLive) cust.Installed = true; else Simulation.GoLive(s, c, cust); }
        p.EverLive = true;
        return true;
    }

    public static bool CanAddPanel(GameState s) => s.Switch.Stage < MaxStage && s.Cash >= PanelPrice;

    /// <summary>Bolt another panel on: room for more subscribers.</summary>
    public static bool AddPanel(GameState s)
    {
        if (!CanAddPanel(s)) return false;
        s.Cash -= PanelPrice; s.Switch.Stage++;
        SeatWaiting(s);
        s.Emit(EventKind.Money, $"A second panel for the switchboard: ${PanelPrice}. Room for {Size(s.Switch)} subscribers.");
        return true;
    }

    /// <summary>Daily: people who have left give their terminal back; anyone waiting for one gets it.</summary>
    public static void Tidy(GameState s)
    {
        s.Switch.Positions.RemoveAll(p => Who(s, p) is not { Live: true });
        SeatWaiting(s);
    }

    /// <summary>For saves from before the switchboard was an object: everyone already connected is wired as they should be.</summary>
    public static void Rebuild(GameState s, ContentDb? c)
    {
        foreach (var cust in s.Customers.Where(x => x.Live && x.LotId is not null && x.SocketFitted).OrderBy(x => x.JoinedDay))
        {
            if (Full(s) && s.Switch.Stage < MaxStage) s.Switch.Stage++;
            if (!Seat(s, cust) || !cust.Installed) continue;
            var p = Pos(s, cust.Id)!;
            s.Switch.FaultyPorts.Remove(p.AssignedPort);
            p.Jumper = p.AssignedPort; p.TagRead = true; p.EverLive = true;
        }
    }
}
