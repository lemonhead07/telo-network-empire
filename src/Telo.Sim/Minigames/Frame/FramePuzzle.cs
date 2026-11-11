using Telo.Sim.Core;

namespace Telo.Sim.Minigames.Frame;

/// <summary>One customer pair arriving at the frame.</summary>
public sealed record FrameLine(int Index, string CustomerId, string CustomerName, int TargetPort, bool LabelReadable, bool IsPartyLine);

public sealed record FrameOutcome(
    int Live, int Dead, int Crossed, int LatentDefects, bool PartyLineCut,
    int ElapsedSeconds, int OvertimeSeconds, bool Delegated,
    IReadOnlyList<string> CrossedCustomerIds, IReadOnlyList<string> DeadCustomerIds, IReadOnlyList<string> DefectCustomerIds,
    IReadOnlyList<string> Involved)
{
    public bool Perfect => Dead == 0 && Crossed == 0 && LatentDefects == 0 && !PartyLineCut;
}

/// <summary>
/// THE FRAME (exchange, Act 1–2). Jumper customer pairs to exchange ports.
/// Twists, all read from state rather than random fail rolls:
///  • unreadable labels → tone-probe to reveal (costs time)
///  • a faulty pair → test the port, reroute to a spare
///  • a party line already wired → leave it alone or the neighbour loses service
///  • chaos: hot-wire a dirty pair — instant, works today, latent defect weeks later
/// Soft clock: going over never fails, it only costs satisfaction.
/// </summary>
public sealed class FramePuzzle
{
    public const int SoftClockSeconds = 60;
    public const int ProbeSeconds = 8, TestPortSeconds = 4, JumperSeconds = 5, HotWireSeconds = 1, RemoveSeconds = 2;

    public IReadOnlyList<FrameLine> Lines { get; }
    public int PortCount { get; }
    public IReadOnlyList<int> SparePorts { get; }
    private readonly HashSet<int> _faultyPorts;
    private readonly Dictionary<int, int> _jumpers = new();   // line -> port
    private readonly HashSet<int> _probed = new();
    private readonly HashSet<int> _tested = new();
    private readonly HashSet<int> _hotWired = new();

    public int ElapsedSeconds { get; private set; }
    public bool PartyLineDisturbed { get; private set; }

    private FramePuzzle(List<FrameLine> lines, int portCount, List<int> spares, HashSet<int> faulty)
    {
        Lines = lines; PortCount = portCount; SparePorts = spares; _faultyPorts = faulty;
        foreach (var l in lines.Where(l => l.IsPartyLine)) _jumpers[l.Index] = l.TargetPort;
    }

    /// <param name="customers">(id, display name) per line, usually 4–6.</param>
    public static FramePuzzle Generate(ulong seed, IReadOnlyList<(string Id, string Name)> customers, int unreadable = 2, bool withFault = true, bool withPartyLine = true)
    {
        if (customers.Count < 1) throw new ArgumentException("The Frame needs at least one line.");
        // Small jobs are simpler: the twists arrive as the work order grows.
        if (customers.Count < 3) withPartyLine = false;
        if (customers.Count < 2) { withFault = false; unreadable = 0; }
        var rng = new DetRng(seed);
        int n = customers.Count;
        var targets = Enumerable.Range(0, n).ToList();
        rng.Shuffle(targets);

        int party = withPartyLine ? rng.Next(n) : -1;
        var others = Enumerable.Range(0, n).Where(i => i != party).ToList();
        rng.Shuffle(others);
        var hidden = others.Take(Math.Max(0, Math.Min(unreadable, others.Count - 1))).ToHashSet();
        var faulty = new HashSet<int>();
        if (withFault) faulty.Add(targets[others[^1]]); // never the party line's port

        var lines = Enumerable.Range(0, n).Select(i =>
            new FrameLine(i, customers[i].Id, customers[i].Name, targets[i], !hidden.Contains(i), i == party)).ToList();
        return new FramePuzzle(lines, n + 2, new List<int> { n, n + 1 }, faulty);
    }

    // ---- what the player can see -------------------------------------------------------------
    public int? VisibleTarget(int line) => Lines[line].LabelReadable || _probed.Contains(line) ? Lines[line].TargetPort : null;
    public bool? KnownFaulty(int port) => _tested.Contains(port) ? _faultyPorts.Contains(port) : null;
    public int? JumperOf(int line) => _jumpers.TryGetValue(line, out var p) ? p : null;
    public bool IsHotWired(int line) => _hotWired.Contains(line);
    public bool IsSpare(int port) => SparePorts.Contains(port);
    public bool OverClock => ElapsedSeconds > SoftClockSeconds;

    // ---- verbs --------------------------------------------------------------------------------
    public int Probe(int line) { ElapsedSeconds += ProbeSeconds; _probed.Add(line); return Lines[line].TargetPort; }

    public bool TestPort(int port) { ElapsedSeconds += TestPortSeconds; _tested.Add(port); return !_faultyPorts.Contains(port); }

    public void Jumper(int line, int port)
    {
        if (port < 0 || port >= PortCount) throw new ArgumentOutOfRangeException(nameof(port));
        ElapsedSeconds += JumperSeconds;
        foreach (var other in _jumpers.Where(kv => kv.Value == port && kv.Key != line).Select(kv => kv.Key).ToList())
        {
            _jumpers.Remove(other); // a port takes one jumper; the old one is pulled
            if (Lines[other].IsPartyLine) PartyLineDisturbed = true;
        }
        if (Lines[line].IsPartyLine && port != Lines[line].TargetPort) PartyLineDisturbed = true;
        _hotWired.Remove(line);
        _jumpers[line] = port;
    }

    public void Remove(int line)
    {
        if (!_jumpers.Remove(line)) return;
        ElapsedSeconds += RemoveSeconds;
        _hotWired.Remove(line);
        if (Lines[line].IsPartyLine) PartyLineDisturbed = true;
    }

    /// <summary>Chaos option: reuse whatever dirty jumper is nearest. Saves time now; persists in the world.</summary>
    public void HotWire(int line)
    {
        ElapsedSeconds += HotWireSeconds;
        _jumpers[line] = Lines[line].TargetPort;
        _hotWired.Add(line);
    }

    // ---- result -------------------------------------------------------------------------------
    public FrameOutcome Evaluate()
    {
        int live = 0;
        var crossed = new List<string>(); var dead = new List<string>(); var defects = new List<string>();
        var otherTargets = Lines.ToDictionary(l => l.TargetPort, l => l.Index);
        foreach (var l in Lines)
        {
            if (!_jumpers.TryGetValue(l.Index, out var port)) { dead.Add(l.CustomerId); continue; }
            if (_hotWired.Contains(l.Index)) { live++; defects.Add(l.CustomerId); continue; }
            bool targetFaulty = _faultyPorts.Contains(l.TargetPort);
            if (port == l.TargetPort) { if (targetFaulty) dead.Add(l.CustomerId); else live++; }
            else if (IsSpare(port)) { if (targetFaulty) live++; else dead.Add(l.CustomerId); }
            else if (otherTargets.ContainsKey(port)) crossed.Add(l.CustomerId);
            else dead.Add(l.CustomerId);
        }
        return new FrameOutcome(live, dead.Count, crossed.Count, defects.Count, PartyLineDisturbed,
            ElapsedSeconds, Math.Max(0, ElapsedSeconds - SoftClockSeconds), false, crossed, dead, defects,
            Lines.Select(l => l.CustomerId).ToList());
    }

    /// <summary>A technician does it instead (80–90% optimal). Mistakes look fine today and surface later.</summary>
    public FrameOutcome Delegate(ulong seed, int accuracyPermille)
    {
        var rng = new DetRng(seed);
        var defects = Lines.Where(_ => !rng.ChancePermille(accuracyPermille)).Select(l => l.CustomerId).ToList();
        return new FrameOutcome(Lines.Count, 0, 0, defects.Count, false, 0, 0, true,
            Array.Empty<string>(), Array.Empty<string>(), defects, Lines.Select(l => l.CustomerId).ToList());
    }
}
