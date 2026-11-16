using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Minigames.OddJob;
using Telo.Sim.Town;

namespace Telo.Sim.Minigames.Assembly;

/// <summary>
/// Turns an assembly into the steps of a job (spine step 6, D31). Always five steps or fewer, in the eight verbs:
/// a repair is undo (remove) → find the fault (test) → replace it (fasten) → do it all up (fasten) → test;
/// an install is fit each part (fasten/connect) → test. Which part is bad is seeded, so the same job is the same fault.
/// </summary>
public static class AssemblyRules
{
    public const int MinutesPerClick = 3;
    private static readonly string[] Tooled = { "screws", "bolts", "solder", "clamp", "screw-on" };

    public static AssemblyDef? ForJob(ContentDb c, string jobTitle) => c.Assemblies.Values.FirstOrDefault(a => a.Job == jobTitle);

    public static (List<OddStepDef> Steps, PartDef? Fault) Steps(AssemblyDef a, ulong seed)
    {
        Tools Need(IEnumerable<PartDef> parts) =>
            a.Needs.Aggregate(parts.Any(p => Tooled.Contains(p.Fastener)) ? Tools.Toolbox : Tools.None, (t, n) => t | Enum.Parse<Tools>(n, true));
        var steps = new List<OddStepDef>();
        if (a.Mode == "install")
        {
            // fit the parts (grouped so the job stays at five steps or fewer), then test
            foreach (var group in a.Parts.Chunk(Math.Max(1, (int)Math.Ceiling(a.Parts.Count / 4.0))))
                steps.Add(new OddStepDef($"Fit the {string.Join(" and ", group.Select(p => p.Name))}", group.Sum(p => p.Count), MinutesPerClick, Need(group)));
            steps.Add(new OddStepDef("Test it", 1, MinutesPerClick, Tools.None));
            return (steps, null);
        }
        var can = a.Parts.Where(p => p.CanFail).ToList();
        var fault = can[(int)(new DetRng(seed).NextU64() % (ulong)can.Count)];
        var outer = a.Parts.TakeWhile(p => p != fault).ToList();
        if (outer.Count > 0)
            steps.Add(new OddStepDef($"Undo the {string.Join(" and ", outer.Select(p => p.Name))}", outer.Sum(p => p.Count), MinutesPerClick, Need(outer)));
        steps.Add(new OddStepDef("Test to find the fault", 2, MinutesPerClick, Tools.None));
        steps.Add(new OddStepDef($"Replace the {fault.Name}", fault.Count, MinutesPerClick, Need(new[] { fault })));
        if (outer.Count > 0)
            steps.Add(new OddStepDef($"Do the {string.Join(" and ", outer.Select(p => p.Name))} up again", outer.Sum(p => p.Count), MinutesPerClick, Need(outer)));
        steps.Add(new OddStepDef($"Test the {a.Name}", 1, MinutesPerClick, Tools.None));
        return (steps, fault);
    }
}
