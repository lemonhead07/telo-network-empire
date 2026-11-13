using Godot;
using Telo.Sim.Net;
using Telo.Sim.World;

namespace Telo.Game.UI;

/// <summary>
/// The sysop's console: the first screen in the game that is a screen in the story too. There is nothing to click.
/// It is worked with the number keys, the way a bulletin board was. Rules are in the sim's BoardService.
/// </summary>
public partial class BoardView : ModalView
{
    public Simulation Sim { get; set; } = null!;
    /// <summary>Work at the terminal takes in-game time.</summary>
    public Action<float> SpendMinutes { get; set; } = _ => { };

    private enum Page { Main, Callers, Files, OneFile }
    private Page _page = Page.Main;
    private string? _fileId;
    private string _status = "";
    private readonly List<Action> _keys = new();

    public override void Rebuild()
    {
        Clear(Body); Clear(Footer); _keys.Clear();
        var s = Sim.State;
        if (!IsOpen) { _page = Page.Main; _status = ""; }
        Title.Text = "TELO BOARD // SYSOP CONSOLE";

        void Line(string text, int size = 0) => Text(Body, text, size);
        void Key(string text, Action act) { _keys.Add(act); Line($" [{_keys.Count}] {text}"); }

        switch (_page)
        {
            case Page.Main:
                var ex = BoardRules.Home(s);
                int waiting = BoardService.Waiting(s).Count, fresh = BoardService.NewFiles(s).Count;
                Line($"{s.Date}    {s.Customers.Count(c => c.Live && c.Installed && c.HasAccount)} callers on the books    {ex.LinesIn} lines in");
                if (s.LastEvening is { } eve) Line($"last night: {eve.Attempts} calls, peak {eve.Peak}/{eve.LinesIn}, {eve.Engaged} engaged tones");
                Line($"file area: {BoardService.Shelf(s).Count} on the shelf, appeal {BoardService.Appeal(s)}/{BoardService.AppealCap}"
                     + (s.BoardWarned ? "    ** letter from a solicitor on file **" : ""));
                if (BoardService.MailOpen(s))
                    Line(BoardService.MailRunToday(s) ? $"mail: swapped tonight.    phone bill this month: ${s.MailBillCents / 100.0:0.00}"
                        : $"mail: NOT RUN TONIGHT.    phone bill this month: ${s.MailBillCents / 100.0:0.00}");
                else Line("mail: none. No other board to ring. (Yet.)");
                if (BoardService.Delegated(s, Sim.Content)) Line("co-sysop: TOBI is logged in and looking after things.");
                Line("");
                Key($"Let new callers in ({waiting} waiting)", () => { if (waiting > 0) _page = Page.Callers; else _status = "nobody is waiting."; });
                Key($"File area ({fresh} new upload{(fresh == 1 ? "" : "s")})", () => { if (fresh > 0) _page = Page.Files; else _status = "nothing new on the upload shelf."; });
                if (BoardService.MailOpen(s))
                    Key($"Run tonight's mail call (about {BoardService.MailMinutes(s)} min long distance)", () =>
                    {
                        int min = BoardService.RunMail(s);
                        if (min == 0) { _status = "tonight's mail has already gone."; return; }
                        SpendMinutes(BoardService.MailSetupMinutes + min);
                        _status = $"dialling... handshake... {min} minutes... mail swapped. ${min * BoardService.CentsPerMinute / 100.0:0.00} on the phone bill.";
                    });
                break;

            case Page.Callers:
                Line("WAITING TO BE LET IN");
                foreach (var cust in BoardService.Waiting(s).Take(8))
                    Key($"{cust.Name}", () => { if (BoardService.LetIn(s, cust.Id)) { SpendMinutes(BoardService.LetInMinutes); _status = $"{cust.Name} has an account."; } if (BoardService.Waiting(s).Count == 0) _page = Page.Main; });
                break;

            case Page.Files:
                Line("NEW UPLOADS");
                foreach (var f in BoardService.NewFiles(s).Take(8)) Key(f.Title, () => { _fileId = f.Id; _page = Page.OneFile; });
                break;

            case Page.OneFile:
                var file = s.BoardFiles.FirstOrDefault(f => f.Id == _fileId);
                if (file is null) { _page = Page.Files; Rebuild(); return; }
                Line(file.Title);
                Line(file.Kind switch
                {
                    FileKind.Useful => "Somebody made this. It is exactly what it says.",
                    FileKind.Fun => "Harmless. Will be the most downloaded thing on the Board by Friday.",
                    _ => "Everyone will want this. It is not, strictly, anyone here's to give.",
                }, 15);
                void Decide(FileStatus st, string said)
                {
                    if (BoardService.Decide(s, file.Id, st)) { SpendMinutes(BoardService.FileMinutes); _status = said; }
                    _page = BoardService.NewFiles(s).Count > 0 ? Page.Files : Page.Main;
                }
                Key("Put it on the shelf", () => Decide(FileStatus.Kept, "on the shelf."));
                Key("Keep it under the counter (regulars only)", () => Decide(FileStatus.Hidden, "under the counter."));
                Key("Bin it", () => Decide(FileStatus.Binned, "gone."));
                break;
        }
        Line("");
        Line(_page == Page.Main ? " [0] Log off" : " [0] Back");
        if (_status.Length > 0) Line("> " + _status);
        Line(IsOpen ? "type a number_" : "", 14);
    }

    /// <summary>A number key was pressed while the player is at the terminal.</summary>
    public override bool OnKey(InputEventKey key)
    {
        int n = key.PhysicalKeycode switch
        {
            >= Godot.Key.Key0 and <= Godot.Key.Key9 => (int)(key.PhysicalKeycode - Godot.Key.Key0),
            >= Godot.Key.Kp0 and <= Godot.Key.Kp9 => (int)(key.PhysicalKeycode - Godot.Key.Kp0),
            _ => -1,
        };
        if (n < 0) return false;
        Press(n);
        return true;
    }

    public void Press(int n)
    {
        _status = "";
        if (n == 0) { if (_page == Page.Main) { Close(); return; } _page = _page == Page.OneFile ? Page.Files : Page.Main; }
        else if (n <= _keys.Count) _keys[n - 1]();
        Rebuild();
    }
}
