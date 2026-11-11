using Telo.Sim.Minigames.Frame;

namespace Telo.Sim.Tests;

public class FrameTests
{
    private static readonly (string, string)[] Six =
        { ("c1", "Ada"), ("c2", "Bo"), ("c3", "Cy"), ("c4", "Di"), ("c5", "Ed"), ("c6", "Flo") };

    private static FramePuzzle Make(ulong seed = 7) => FramePuzzle.Generate(seed, Six);

    [Fact]
    public void SameSeedSamePuzzle()
    {
        var a = Make(); var b = Make();
        Assert.Equal(a.Lines, b.Lines);
    }

    [Fact]
    public void HasTwoUnreadableOneFaultOnePartyLine()
    {
        var p = Make();
        Assert.Equal(2, p.Lines.Count(l => !l.LabelReadable));
        Assert.Single(p.Lines, l => l.IsPartyLine);
        Assert.Equal(1, Enumerable.Range(0, 6).Count(port => !p.TestPort(port)));
    }

    /// <summary>The intended solution: probe the unknowns, test ports, reroute the bad pair, leave the party line.</summary>
    private static void SolveProperly(FramePuzzle p)
    {
        foreach (var l in p.Lines.Where(l => !l.IsPartyLine))
        {
            int target = p.VisibleTarget(l.Index) ?? p.Probe(l.Index);
            int port = p.TestPort(target) ? target : p.SparePorts[0];
            p.Jumper(l.Index, port);
        }
    }

    [Fact]
    public void ProperSolutionIsPerfect()
    {
        var p = Make();
        SolveProperly(p);
        var o = p.Evaluate();
        Assert.True(o.Perfect, $"live={o.Live} dead={o.Dead} crossed={o.Crossed}");
        Assert.Equal(6, o.Live);
    }

    [Fact]
    public void HotWiringIsFastButLeavesLatentDefects()
    {
        var p = Make();
        foreach (var l in p.Lines.Where(l => !l.IsPartyLine)) p.HotWire(l.Index);
        var o = p.Evaluate();
        Assert.Equal(6, o.Live);
        Assert.Equal(5, o.LatentDefects);
        Assert.True(o.ElapsedSeconds < 10);
    }

    [Fact]
    public void TouchingThePartyLineCutsTheNeighbour()
    {
        var p = Make();
        var party = p.Lines.Single(l => l.IsPartyLine);
        p.Remove(party.Index);
        Assert.True(p.Evaluate().PartyLineCut);
    }

    [Fact]
    public void WrongPortIsACrossedLine()
    {
        var p = Make();
        var a = p.Lines.First(l => !l.IsPartyLine);
        var b = p.Lines.First(l => !l.IsPartyLine && l.Index != a.Index);
        p.Jumper(a.Index, b.TargetPort);
        Assert.Contains(a.CustomerId, p.Evaluate().CrossedCustomerIds);
    }

    [Fact]
    public void SoftClockNeverFails_OnlyCostsOvertime()
    {
        var p = Make();
        for (int i = 0; i < 10; i++) p.Probe(0);
        SolveProperly(p);
        var o = p.Evaluate();
        Assert.True(o.Perfect);
        Assert.True(o.OvertimeSeconds > 0);
    }

    [Fact]
    public void DelegateIsImperfectAtScale()
    {
        int defects = 0;
        for (ulong s = 0; s < 200; s++) defects += Make(s).Delegate(s, 850).LatentDefects;
        double rate = defects / 1200.0;
        Assert.InRange(rate, 0.10, 0.20);
    }
}
