using Godot;
using Telo.Sim.Ledger;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Game.UI;

/// <summary>The company notebook: how each kind of work gets done (earned automation) and the Promise Ledger.</summary>
public partial class NotebookView : ModalView
{
    public Simulation Sim { get; set; } = null!;

    public override void Rebuild()
    {
        Clear(Body); Clear(Footer);
        var s = Sim.State; var c = Sim.Content;
        var act = c.ActAt(s.ActIndex);
        Title.Text = "COMPANY NOTEBOOK";
        Text(Body, $"Act {act.Index}: {act.Name} ({act.Years}). Business: {act.BusinessModel}. Holding you back: {act.BindingConstraint}.", 16);
        var exit = string.Join(", ", act.ExitMilestone.Select(m => $"{m.Kind} {ActRules.Measure(s, m.Kind)}/{m.Value}"));
        if (exit.Length > 0) Text(Body, $"To move on: {exit}", 15);

        Text(Body, "\nHOW THE WORK GETS DONE", 20);
        foreach (var w in c.Work.Values)
        {
            var cur = WorkRules.Current(s, c, w.Id);
            Text(Body, $"{w.Name}: {cur.Mode} on {cur.Interface}. {cur.Description}");
            if (WorkRules.Next(s, c, w.Id) is { } next)
            {
                var can = WorkRules.CanUpgrade(s, c, w.Id);
                string why = can switch
                {
                    UpgradeResult.NeedsAct => $" (needs Act {next.RequiresAct})",
                    UpgradeResult.NeedsSubscribers => $" (needs {next.RequiresSubscribers} paying subscribers; you have {s.LiveSubscribers})",
                    UpgradeResult.NeedsEntry => $" (needs Almanac: {c.Almanac[next.RequiresEntry!].Title})",
                    UpgradeResult.NotEnoughCash => " (not enough cash)",
                    _ => "",
                };
                Text(Body, $"   Next: {next.Description}  New problem: {next.NewProblem}", 14);
                Btn(Body, $"   Invest ${next.Cost}{(next.MonthlyUpkeep > 0 ? $" + ${next.MonthlyUpkeep}/mo" : "")}{why}",
                    () => { WorkRules.Upgrade(s, c, w.Id); Rebuild(); }, can == UpgradeResult.Ok);
            }
        }

        Text(Body, "\nTHE PROMISE LEDGER", 20);
        foreach (var p in c.Premises.Values.Where(p => p.Act <= s.ActIndex + 1))
        {
            var st = LedgerRules.StatusOf(s, p.Id);
            Text(Body, $"{p.Name} — {p.Reason}. {p.Story}  [{st}]");
            var row = new HBoxContainer();
            Body.AddChild(row);
            if (st is PromiseStatus.Unserved)
                Btn(row, "Promise a line within 90 days", () => { LedgerRules.Promise(s, c, p.Id, s.Day + 90); Rebuild(); });
            if (st is not PromiseStatus.Served && p.PoleLine is not null)
            {
                var line = Telo.Sim.Build.LineRules.ProgressOf(s, p.Id);
                Text(row, $"  No fee buys this. Build the pole line along the road yourself: {line.Planted} pole(s) planted, {line.Strung} span(s) strung. Poles are on the van, ${p.PoleLine.PoleCost} each.", 14);
            }
            else if (st is not PromiseStatus.Served)
                Btn(row, $"Connect them (${p.CostToServe})", () => { LedgerRules.Serve(s, c, p.Id); Rebuild(); }, s.Cash >= p.CostToServe);
        }
        CloseButton();
    }
}
