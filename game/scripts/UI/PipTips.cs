namespace Telo.Game.UI;

/// <summary>
/// PIP talks while you work: one short speech bubble the first time you do each thing, at the moment you do it.
/// These are hints, not rules, so they live with the presentation. Each is said once per session.
/// </summary>
public sealed class PipTips
{
    private readonly Hud _hud;
    private readonly HashSet<string> _said = new();
    public PipTips(Hud hud) => _hud = hud;

    private static readonly Dictionary<string, string> Lines = new()
    {
        ["hello"] = "…I'm PIP. I'm the handset on your desk. I'll speak up when something's new. Start with the answering machine.",
        ["customer"] = "A customer. Their house has the ▼ over it. Everything you need is on the van outside.",
        ["van"] = "W and S to drive, A and D to steer, E to get out. Roads are twice as fast as grass.",
        ["carry"] = "One thing in your hands at a time. G puts it down. Anything within a few steps of you counts as 'to hand'.",
        ["install"] = "They've let you in. Yellow markers show the work: outside wall first, top to bottom, then indoors.",
        ["step"] = "Hold E to do a step properly, and keep holding to carry on to the next. It takes as long as it takes. R bodges the whole stage: quick today, crooked, and trouble in a few weeks.",
        ["bodge"] = "It'll hold. Probably. I've made a note of the date.",
        ["modem"] = "Modem's in. Lift their telephone and listen before you sign the job sheet by the door.",
        ["nodial"] = "No dial tone. One thing you fitted is bad. Its marker is back: redo it and test again.",
        ["dial"] = "Dial tone. Sign the job sheet by the door and collect your kit.",
        ["signed"] = "House done. The line is still dead until you jumper it at the switchboard in your room.",
        ["switch"] = "The switchboard. Take a jumper from the hook, touch the customer's terminal, then the port their tag names. Then throw the test switch.",
        ["oddjob"] = "One subscriber. That won't cover rent. A neighbour has an odd job: look for the $.",
        ["knock"] = "Cash in hand. Customers won't all ring you: go and knock on a door.",
        ["free"] = "You know the round now. The phone will start ringing by itself, and my Almanac and your notebook are open on the desk.",
        ["nomodem"] = "No modems left. Sol's Electronics, east end of the street: the counter window.",
        ["canhire"] = "Four paying customers. You can afford help. The notebook on your desk has the hiring page.",
        ["hired"] = "Jory Pike starts in the morning and waits at your door from eight. Jory only works on tickets you hand over: take one from the rail on your wall, walk out, and give it to them. Two a day.",
        ["fault"] = "A fault. Someone's line has gone bad. Take the toolbox to their door, the one marked !",
        ["pole"] = "First pole in. Thirteen to go. String each span with the drum and the ladder from the blue marker.",
        ["oddstart"] = "They've let you in. The patient is on the rug. Toolbox first, then E your way through it. R is a thump.",
        ["thump"] = "A firm thump and a confident nod. They'll pay. They'll also remember.",
        ["linedone"] = "The farm has dial tone. The county has paid its rural lines grant, and the whole town has heard who built it.",
        ["engaged"] = "Last night someone rang the Board and got the engaged tone. The lamps beside your frame are its lines in. Two is not enough for this many people.",
        ["card"] = "A line card. It's on the van's tailboard. Carry it in and slot it into the rack beside the frame.",
        ["cardfit"] = "One more line in from tonight. Watch the lamps around seven. It also costs rental every month.",
        ["cap"] = "Four lines is all a home-made board can answer. There's a page in my Almanac about rolling calls across more: hunt groups.",
        ["almanac"] = "You've outgrown this chapter and the world hasn't caught up. My Almanac has the page. Recall it early and you start ahead of everyone. It costs Archive, and the world notices.",
        ["headstart"] = "You're ahead of the calendar now. When other companies come, and they will, they start from the day the world finds out.",
        ["pager"] = "Your technician left you a pager. It beeps when something needs you, wherever you are.",
        ["order"] = "This one isn't a repair, it's a sale. Fit it step by step, then show them how it works. Some of these add to their bill every month.",
        ["catalogue"] = "People who already have a line start wanting things to go with it. Extensions. Answering machines. It all goes through you.",
        ["terminal"] = "That's the Board's own terminal, on the table by your desk. Sol found it in a skip. Number keys, like a proper sysop. New callers wait there until you let them in.",
        ["files"] = "People upload things to the Board. You decide what goes on the shelf, what stays under the counter, and what goes in the bin. A good shelf brings more callers.",
        ["mail"] = "Act one. Boards have started swapping mail overnight over long-distance lines. Run the call every night at the terminal. It costs by the minute.",
        ["nomail"] = "No mail tonight means no mail tomorrow morning. People notice by the second day.",
        ["tobi"] = "Tobi Okafor keeps asking to help run the Board. There's a page for it in your notebook.",
        ["solicitor"] = "A solicitor's letter about the file area. The Board has never been more popular. Those two facts are related.",
        ["ticket"] = "Ticket on your clipboard. Hand it to Jory in person. No ticket, no work, and no pay either: they're paid by the job.",
        ["given"] = "That's theirs now. It'll be done by the end of their working day. You'll find them standing outside the house they're on.",
        ["hours"] = "People keep hours now you know the round. A car on the drive or a lit window means someone's in. Most families are out from nine to five.",
        ["payday"] = "Friday. Pay day. It's on the calendar by your bed, along with rent day and who is off.",
        ["tolet"] = "1 Main Street is to let, at the east end of the far row. With a depot your staff collect their own tickets each morning and go on a weekly wage.",
        ["depot"] = "Keys to 1 Main Street. From tomorrow nobody waits at your door: tickets go from the rail to the depot on their own.",
        ["probe"] = "That's what the tone probe is for. Now you know which port they belong on.",
        ["badport"] = "Red: that port is bad. Put them on a spare, S1 or S2, instead.",
        ["hotwire"] = "An old jumper off the hook. It'll carry a call today. I've made a note of the date.",
        ["pulled"] = "That wire was somebody's telephone. They've gone dead. Put it back and throw the switch.",
        ["reseat"] = "Pushed home. That was going to be a fault in a few weeks, and now it isn't. Worth a look whenever Jory's been at the board.",
        ["thrown"] = "Lamps on. Those lines are live, and they've paid to be connected.",
        ["thrownbad"] = "Not all of them lit. Dark means the wrong port or a bad one. Two blinking red means crossed: someone can hear a stranger.",
        ["closed"] = "Road closed. The council is resurfacing it. Nobody has seen the council.",
    };

    /// <summary>Say this line if it hasn't been said yet.</summary>
    public void Once(string key)
    {
        if (_said.Add(key) && Lines.TryGetValue(key, out var text)) _hud.Say(text);
    }

    /// <summary>Say something that isn't a one-off tip.</summary>
    public void Say(string text) => _hud.Say(text);

    /// <summary>Mark lines as already known (a loaded save past the first hour shouldn't be talked through it).</summary>
    public void Skip(params string[] keys) { foreach (var k in keys) _said.Add(k); }
}
