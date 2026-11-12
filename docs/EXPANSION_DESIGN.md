# Expansion Design: a loop that grows on itself

Written 2026-11-12 after the owner's second playtest (39 minutes, whole town connected, farm line built, nothing left to do). This is the plan for what the game *is* after the first hour and how we build it. It fits the canon in `TELO_GAMEPLAY_HANDOFF.md`; where it changes a locked decision it says so in section 10.

> **Status, 2026-11-12.** The owner agreed to all of section 10 (now D17–D21 in `DECISIONS.md`) and added D22, the catalogue. **Built so far:** phase A (early act start, pager, saved installs, money pass), phase B (evening calls, engaged tones, line lamps and tally, line cards), and a first version of the catalogue (section 4a). **Next:** phase C, the Board's terminal.

## 1. What the playtest told us

- **The first 40 minutes work.** Pacing good, the "quiet day" line liked, the hands-on install liked.
- **Then the game runs out.** 12 houses, a shop and a farm is all there is. Nothing a subscriber does creates more work, so the loop stops dead when the last door says yes.
- **Building didn't feel like achieving.** The farm line was 14 poles for $5 a month. Nothing in the world changed because of it.

The cause is one missing piece: **customers don't use the network.** A line is either on or off. Nobody makes a call, so nothing fills up, so nothing needs building. Everything below follows from fixing that.

## 2. The loop

Five jobs, always the same five, at any size of company:

```
   SELL ───► CONNECT ───► CARRY ───► KEEP ───► COUNT
    ▲                                            │
    │                                            ▼
    └──────── go further out ◄──────────── HAND OVER
```

| Job | What it means | Today | Missing |
|---|---|---|---|
| **Sell** | Find people who want a line | Answering machine, door-knocking | Reasons people say yes or no that you can change |
| **Connect** | Physically reach them | House install, frame, pole line | Cabinets, radio links, sea cable |
| **Carry** | Have room for what they do on the line | Nothing | **The whole job** (section 3) |
| **Keep** | Stop it breaking, fix it when it does | Faults from bad installs | Weather, wear, finding where the break is |
| **Count** | Bill it, collect it, pay for it | Automatic on the 1st | Doing it by hand, then handing it over |
| **Hand over** | Hire or automate the job you've mastered | Technician for installs | A hire for each job, each with its own flaw |

**Why it grows on itself.** Each job's success is the next job's workload. More sold means more to connect. More connected means more calls to carry. More carried means more to break and more to bill. Enough of all that pays for a hire, and justifies one. The hire frees your hands, makes its own quiet mistakes, and lets you reach the next place, where selling starts again one size bigger.

**The rule for every round.** Each round adds exactly:

- **one new place** on the planet,
- **one new thing you build by hand** to reach it,
- **one new bottleneck** that the last round's success caused,
- **one new hire** who takes over the thing you just learned,
- **one new problem** that the hire creates.

That is the canon's core rule ("every improvement creates a new problem") turned into a production recipe. If a proposed feature doesn't fill one of those five slots, it waits.

## 3. The missing middle: what a line carries

### The Board

What subscribers are paying for is **the Board**: a bulletin-board computer in your room that they ring up with the modem you fitted. They read messages, swap files, send mail to each other. This is the canon's Act 1 ("The Bulletin Board") and it gives every line a purpose.

The Board is also the first screen in the game that isn't paper. It is a green terminal on your desk (an in-world screen, like the notebook). On it you:

- **let new callers in** (approve accounts, a few seconds each),
- **tend the file area** (keep the useful, quietly hide the embarrassing: the canon's "Borrowed Library"),
- **run the night mail call** (the Board rings a bigger board far away and swaps mail; it costs long-distance money by the minute, so a faster modem from the Almanac halves the bill),
- **answer the "sysop?" bell** when someone is stuck.

Later you hand it to a co-sysop: Tobi Okafor, the teenager at lot n4 who already knows more about it than you do.

### Lines in, and the evening rush

The Board has a number of **lines in** (start with 2). Every subscriber rings in for a while each evening, how long depends on who they are (the gamer for hours, Dot Brennan for six minutes on Sundays). When more people ring than there are lines, they get the **engaged tone**.

- You see it: a row of line lamps on the frame. All lit at 7 pm means full. A tally sheet next to it counts engaged tones.
- They feel it: every engaged tone costs that subscriber a little satisfaction. Too many and they cancel.
- You fix it: buy a line card at Sol's, carry it home, slot it in, wire it. Each line also costs rental every month.
- It bites back: more lines bring more callers, the one computer slows down, the fuse in your rented room starts to matter. That is the door to the next round's kit.

This single mechanic is what makes 13 subscribers different from 5. It is also the same shape as every later bottleneck in the game (modem banks, trunks, backhaul, spectrum), so building it once pays for all of them.

### Three sizes of "full"

| Bottleneck | Where | Appears | Fix |
|---|---|---|---|
| **Lines in** | The Board in your room | Round 1 | Line cards, then a modem bank |
| **Trunk** | The cable between a village and town | Round 2 | More pairs on the poles, or a cabinet in the village so local calls stay local |
| **Upstream** | The Board's link to the outside world | Round 4 | Faster modems, a leased line, then your own route |

## 4. The telco catalogue: what else there is to do

Every entry is something real telephone and network companies do, fictionalised. "By hand" is what the player does first; "handed to" is who takes it over.

| Thing | By hand | Fixes | Creates | Handed to | Round |
|---|---|---|---|---|---|
| House install | Built | A customer | Bad installs | Technician | 0 |
| The frame | Built | Line goes live | Crossed lines | Technician | 0 |
| Pole line | Built (fixed route) | Reaches a place | Storm damage; someone else's field | Line crew | 0, 2 |
| **Line card** | Buy, carry, slot in, wire | Engaged tones | Rental; heat; the fuse | Technician | 1 |
| **The Board** | Approve callers, tend files, night mail | A reason to subscribe | The file area's reputation; the phone bill | Co-sysop (Tobi) | 1 |
| **Billing round** | Write the invoices, then knock for the late ones | Money arrives | People who can't pay | Bookkeeper (already in data) | 1 |
| **The phone** | Listen to messages: enquiries, complaints, fault reports | You find out what's wrong | You can't be in when it rings | Receptionist (already in data) | 1 |
| **The pager** | Carried; beeps when something needs you | You can leave town | It beeps | (stays with you) | 1 |
| **Line length** | Fit a repeater on a pole when a house is too far | The far house works | Another thing on a pole to fail | Line crew | 2 |
| **Trunk cable** | Choose how many pairs to string | A village can ring town | Costs more; fills up | Line crew | 2 |
| **Cabinet** | Stand a small exchange box in a village, wire it | Short lines, local calls | A second frame to keep right | Technician | 2 |
| **Wayleave** | Ask the landowner before planting poles | The straight route | A favour owed | (stays with you) | 2 |
| **Line patrol** | Test from the frame ("break is past pole 6"), drive out, find it | A dead village | Travel time while everyone waits | Line crew | 2 |
| **Stock and the van** | Cable by the metre, poles by the dozen, loaded each morning | You can build | Running out 2 km from town | Storeman | 2 |
| **Radio link** | Climb, mount a dish, aim it until the meter peaks | Crosses what poles can't | Needs power; fog; aim drifts | Rigger | 3 |
| **Relay hut** | Generator and batteries on a ridge; refuel it | Radio past the horizon | It goes dark when nobody comes | Driver | 3 |
| **Sea cable** | Lay from a boat, splice the ends | An island; another shore | Anchors, and one very long fault | Cable crew | 4 |
| **Second exchange** | A proper frame in another town | A town too far for one Board | Two of everything | A manager | 4 |
| **The incumbent** | The national phone monopoly notices you | (pressure) | Price leaflets; pole rent in town | Lawyer, later | 3 |
| **Contracts** | School, clinic, quarry: a promise with a date and a penalty | Steady money | A clock | Sales, later | 3 |

Later eras (canon Acts 2 to 7) reuse the same slots with bigger kit: modem banks, the trench, towers and aiming for mobile, datacentres, and finally satellites launched off this planet. They are not designed here.

## 4a. The catalogue: more to sell than "a line"

Owner, 2026-11-12: ordering only lines "can feel monotonous". So a subscriber is not finished once they are connected. They ring back wanting things, and each thing is a different small job.

**How it works (built, first version).** Once the basic round is learned, some answering-machine messages are existing subscribers ordering from the catalogue. You go to their house, they let you in, and you fit it by hand: each product has its own steps and its own kit. It pays a price today (less what Sol charges you for the hardware) and sometimes adds to their monthly bill. Thump it instead and it comes back as a fault. An order left for ten days goes to the national phone monopoly.

| Product | Who wants it | Pays | Notable |
|---|---|---|---|
| Extension telephone | Anyone | $30, +$1/month | Needs the cable drum |
| Answering machine | Families, businesses, the careful, the curious | $55 | "Record their greeting. Again. Once more." |
| Loud outdoor bell | Gardeners and the hard of hearing | $25 | Needs the ladder |
| Second line | Families, businesses, gamers, students | $45, +$8/month | They ring the Board for longer |
| Fax machine | Businesses | $150, +$6/month | Biggest ticket in Act 0 |
| 2400 fast modem | Gamers, students, the curious, businesses | $80, +$4/month | **Needs its Almanac page recalled.** Halves their time on the Board, so it eases the evening rush |

**Why it matters to the loop.** It gives Sell a second half (selling *more* to the people you have), so a fully connected town keeps producing work and money. And it ties the Almanac to the shop: every page you recall can put something new on the shelf.

**Where it goes next** (not built):

- **More products per act**, each tied to an Almanac page where it makes sense: pager rental, payphone (the pub at the Quay), a business switchboard for Sol's, long-distance by the minute, a library terminal, the "offline mail reader" once the Board has mail.
- **Plans, not just products:** evening-only (cheaper, off the rush), business hours, a lifeline tariff for the Promise Ledger.
- **Choosing at the door:** when someone signs up, they pick from two or three things on a leaflet instead of always "a line", and what they pick changes the install.
- **Stock:** hardware you buy ahead and carry, so a popular product can sell out.
- **The technician takes orders** once you have hired a second one; until then orders are the player's job.
- **Fashion:** what the neighbours have, people want. An answering machine next door raises the odds here.

## 5. The planet: places worth reaching

One rule makes the map matter: **every place has one thing in the way, and that thing teaches one new build.**

| Place | Where | In the way | Teaches | People |
|---|---|---|---|---|
| **Main Street** (built) | Town | Nothing | Install, frame | 12 houses, Sol |
| **Hollow Farm** (built) | 1 to 1.5 km inland | Distance | Pole line | Ruth Adeyemi |
| **The farm road** | Between the two | Line length | Repeater | Mr. Pell (40 m past the limit, canon), two cottages |
| **Riverside Caravan Park** | Where the road meets the river | The owner | Wayleave | Residents who'd like to be asked (canon) |
| **Saltmarsh Quay** | Fishing village, 2 km round the bay | The river; one shared cable | Trunk, cabinet, patrol | 8 to 10 households, a harbourmaster, a pub |
| **Harrow Ridge** | Hamlet above the tree line | A cliff | Radio link, relay hut | Northfield Clinic (canon), a hill farm, a lookout |
| **Gull Rock** | Island light | The sea | Sea cable (first, short one) | The keeper |
| **Easthaven** | Second town, far shore or far side | All of the above at once | Second exchange | 20+ households, the incumbent's local office |

Three geography rules, all computed by the simulation from the real terrain:

1. **Wire has a length limit.** A line works up to about 700 m from an exchange or cabinet. Past that it needs a repeater (adds about 500 m) or a cabinet (starts the count again). Every house's distance is known, so the map itself tells you what a street needs.
2. **Radio needs to see.** A link works only if one dish can see the other over hills and over the curve of the planet. On a world this small the horizon is close, which is why ridges are valuable.
3. **Water stops poles.** Rivers need a long span or a bridge; the sea needs a cable or a radio hop.

**Every build opens something.** A pole line makes every house along it sellable. Reaching a village unlocks its odd jobs, its shop stock and the road beyond it. This is what the farm line was missing.

## 6. The rounds

| Round | Place | You build by hand | It fills up at | You hand over | Their flaw | Play time | Game time |
|---|---|---|---|---|---|---|---|
| **0 The town** (built) | Main Street | Installs, the frame | Your hours | Installs to a technician | Bad installs surface as faults | 40 min | 2 weeks |
| **1 The Board** | Your room, then 1 Main Street | Line cards, the Board, invoices | Lines in at 7 pm | Billing, the phone, the Board | The bookkeeper notices things; complaints arrive as summaries | 1 hour | 2 to 3 months |
| **2 The road and the Quay** | Farm road, caravan park, Saltmarsh Quay | Repeaters, trunk, cabinet, line patrol | The trunk; your stock | Pole building to a line crew | They build exactly what you marked, mistakes included | 1.5 hours | 6 months |
| **3 The high ground** | Harrow Ridge | Radio link, relay hut | Line of sight; fuel | Rigging and fuel runs | The relay goes dark when nobody goes | 2 hours | 1 year |
| **4 The far shore** | Gull Rock, Easthaven | Sea cable, second exchange | Trunks between towns; the upstream bill | Town 1 to a manager | You stop knowing your own town | 3 hours | 2 years |

Rounds 0 to 2 are the demo and then some. Rounds 0 to 4 are most of the canon's Acts 0 and 1 and reach the door of Act 2 (dial-up).

### Round 1 in detail: The Board

*Starts when:* the technician is hired. PIP: "People are ringing in. Two at a time is all it takes."

1. **The Board arrives** on your desk as a terminal. First job: let in the callers who are waiting.
2. **7 pm, the lamps are all lit.** The tally sheet shows engaged tones. The top line says who is fed up.
3. **Buy and fit a line card.** Sol's, van, carry, slot, wire at the frame (a short version of the frame job).
4. **Month end, by hand.** Invoices at the desk, then two late payers to visit. Gus Pike has a reason.
5. **The phone fills up.** Enquiries, complaints and fault reports are now mixed on the answering machine. A fault you don't listen to is a fault you don't know about.
6. **Hire the bookkeeper, then the receptionist.** Billing happens without you. Messages become a summary on a pad.
7. **Tobi asks to help run the Board.** Hand it over. The file area gets livelier.
8. **Sign the lease on 1 Main Street** (the canon's Act 0 exit): a bigger frame, a counter, a desk for staff.

*Ends when:* lines in are keeping up, two people are on the payroll, and the pager is on your belt.

### Round 2 in detail: the road and the Quay

*Starts when:* the pager arrives and the council barrier comes down.

1. **Mr. Pell.** His house is 40 m past the limit. Fit the first repeater. Now the farm road cottages can sign up.
2. **The caravan park.** The owner says no. A favour for the owner, or the long way round.
3. **Survey the Quay.** Walk the route and mark where poles go (you choose now; the route is no longer fixed).
4. **String the trunk.** Choose 4 pairs or 10. Ten costs more than twice as much.
5. **Stand the cabinet** in the village and wire it.
6. **Sell the Quay.** Door-knocking again, in a place that has never had a line. The pub wants a payphone.
7. **First storm.** The village goes dead. Test from the frame, drive the line, find the branch.
8. **Hire the line crew.** From now on you mark and they build.

*Ends when:* the Quay is connected, the trunk has filled once, and the crew has built something without you.

## 7. Making a build feel like something

Every completed build must do all five. The farm line did one.

1. **Change the world for good:** lights in a window at night, a person at the gate, a sign on a wall.
2. **Pay now:** a grant, a connection fee, a contract signing.
3. **Open something:** new doors to knock on, a new road, a new shelf at Sol's.
4. **Be said out loud:** the person it reached says a line of their own.
5. **Show on a map:** a pin on the wall map in your room (new prop) that fills in as the network grows.

## 8. Pace and overlap

- **The pager is the overlap.** Once you leave town, town keeps happening. The pager beeps for a fault, a full board, a late payer. You decide whether to turn the van round. Travel time on a real planet is the cost.
- **Time speeds up as you hand over.** Sleeping already skips to the next thing that needs you. With one hire that is a few days. With five it is weeks. Nobody has to design "faster rounds": they fall out of delegation.
- **Each round begins before the last one is quiet.** Round 2's first job arrives while Round 1's bookkeeper is still new.
- **One new thing at a time still holds.** The unlock rules that stage the first hour extend to the whole game (section 9, foundation 3).

## 9. How we build it

### Foundations (built once, used by every round)

| # | Foundation | What it is | Why first |
|---|---|---|---|
| 1 | **A network in the simulation** | Places things connect at (exchange, cabinet, relay, house) and the links between them (drop wire, pole cable with pairs, radio hop, sea cable), each with length, capacity and condition | Today a customer is just "on" or "off". Load, engaged tones, line length and "where is the break" all need this |
| 2 | **Daily use** | Each subscriber's evening calls, added up per bottleneck | The missing middle |
| 3 | **Unlocks as data** | One list: what must be true, what it reveals, what PIP says. Replaces the hand-written first-hour steps | Stages the whole game the way the first hour is staged; the F4 god key simply walks the list |
| 4 | **Step jobs as data** | Install, odd job and pole work are three separate pieces of code. Make one kind of "job with steps, kit, minutes and a bodge", described in a file | A line card, a cabinet, a repeater and a dish each become a short data entry plus where the click points go |
| 5 | **Crews and work orders** | The technician becomes a crew with a queue, a home depot and travel time across the planet | Distance from the depot is why you'll want a second depot |
| 6 | **Places as data** | Settlements listed in a file; the planet finds a spot for each by rule (coastal, high, island) and builds the road | A new village stops being a programming job |
| 7 | **The pager** | A queue of things that need you | Overlap |

### Phases

Each phase ends playable and checked. "Session" means one working session like the ones so far.

| Phase | What | Sessions | Playable at the end |
|---|---|---|---|
| **A. Unblock** | Act 1 can start early by recalling it from the Almanac (section 10); the pager; saving mid-job; first money pass | 1 | Nothing new, but nothing in the way |
| **B. Carry** | Foundations 1 and 2; line lamps and tally on the frame; line cards | 2 | The town can be too popular |
| **C. The Board** | The terminal and its three jobs; night mail and the phone bill; Tobi | 2 | A reason to subscribe, and the first non-paper screen |
| **D. Count and listen** | Invoices by hand and late payers; mixed phone messages; bookkeeper and receptionist made real; move to 1 Main Street | 2 | **Round 1 complete** |
| **E. Tooling** | Foundations 3, 4 and 6: unlock list, step jobs, places as data | 2 | Same game, far cheaper to extend |
| **F. The road** | Line length and repeaters; Mr. Pell; caravan park wayleave; cottages | 1 | The farm road has people on it |
| **G. The Quay** | The village, river, survey-your-own route, trunk pairs, cabinet, cable by the metre | 3 | A second settlement |
| **H. Keep** | Storms, test-from-the-frame, line patrol; foundation 5; the line crew | 2 | **Round 2 complete** |
| **I. High ground** | Line of sight in the simulation; mast, dish, aiming; relay hut and fuel; Harrow Ridge | 3 | Round 3 |
| **J. Far shore** | Boat, sea cable, second exchange, the incumbent reacts, first contracts | 4 | Round 4 |

About 22 sessions to the end of Round 4. **Rounds 0 to 2 (phases A to H, about 15 sessions) are a strong demo:** 3 to 4 hours, three kinds of place, four hires, and the loop visibly feeding itself.

### Art and the trailer run alongside

Art is its own track (`docs/ASSET_LIST.md`) and doesn't block any phase: every new thing ships first as a labelled box. Sensible points to spend art sessions:

- after **B**: the house exterior, the van and the carried kit (most-seen objects);
- after **D**: residents who walk and talk, the technician, your room and desk;
- after **H**: the Quay, and then the trailer, which by then has a town, a village, a storm and a line going up.

### What we are not building yet

Rivals as full companies, crime and evidence, the office designer, contracts with hidden clauses, mobile, datacentres, satellites. All are in the canon and all slot into this loop later. None is needed for the loop to feed itself.

## 10. Decisions (all agreed by the owner, 2026-11-12)

| # | Question | Recommendation | Why |
|---|---|---|---|
| D17 | Should an act be startable early? | **Yes: recalling the act's key page from PIP's Almanac starts it now, at a cost in Archive and Echo.** Waiting for the public date stays free. | This is the canon's own rule ("ship it early and pay"). It removes the 16-month wait and makes the Almanac matter in hour one. Changes D4 slightly. |
| D18 | What do subscribers use the line for? | **The Board** (dial-in bulletin board), as in section 3 | Matches the canon's Act 1, gives the first terminal screen, and makes "lines in" a natural bottleneck |
| D19 | Which place comes after the farm road? | **Saltmarsh Quay** (village) before Harrow Ridge (radio) | It reuses pole lines and installs, so it is mostly new content and little new machinery |
| D20 | Poles: fixed spots or player-placed? | **Player surveys the route from Round 2**; the farm line keeps its fixed spots as the tutorial | Choosing the route is where terrain, wayleaves and cost become decisions |
| D21 | Imported models | **Allow Blender models now** (changes D15, "everything in code") | Already the owner's stated wish; `ASSET_LIST.md` assumes it |
