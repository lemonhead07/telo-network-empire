using Telo.Sim.Content;

namespace Telo.Sim.Tests;

public class ContentTests
{
    [Fact] public void ShippingContentIsValid() => Assert.Empty(TestContent.Db.Validate());

    [Fact] public void NineActs() => Assert.Equal(9, TestContent.Db.Acts.Count);   // D46: The Core is Act 5, mobile moved to 6

    [Fact] public void EveryImprovementNamesTheProblemItCreates()
    {
        foreach (var w in TestContent.Db.Work.Values)
            foreach (var s in w.Stages.Skip(1))
                Assert.False(string.IsNullOrWhiteSpace(s.NewProblem), $"{w.Id} {s.Mode}/{s.Interface} has no new problem");
    }

    [Fact] public void LastAlmanacEntryIsExchange0() =>
        Assert.Equal("alm-exchange0", TestContent.Db.Almanac.Values.MaxBy(e => e.Act)!.Id);

    [Fact] public void BrokenReferenceFailsLoudly()
    {
        var files = ContentDb.FileNames.ToDictionary(f => f, _ => "[]");
        files["almanac.json"] = """[{ "id": "x", "title": "x", "announcement": "nope" }]""";
        Assert.Throws<InvalidDataException>(() => ContentDb.FromJson(files));
    }
}
