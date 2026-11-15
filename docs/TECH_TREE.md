# Tech Tree & Hand-off Design

**Status:** 📄 design, not built yet. Design canvas: https://claude.ai/code/artifact/0a398f7c-ea51-4285-9488-2cba471067b4
**Scope:** Acts 0–3 in detail (Early Access is Acts 0–2; Act 3 is here so the shape is right). Acts 4–7 are one-line stubs.
**Who reads this:** dev sessions read §1–§4, then only the branch section they are building. Idea sessions edit any of it.

---

## 0. How this fits the current branch (read first)

Written 2026-11-15 against an older `master`; `feature/gameplay-rapid` has since built a lot of what the tree describes. Treat built systems as the truth and this doc as the shape to grow into:

- **Already built:** walk-in houses and the hands-on install (≈ `lines.drop`), van kit and carrying (≈ `field.van`), the switchboard (`Net/SwitchRules`, ≈ `lines.frame`; its verb is `switchboard`, not `frame`), the Board and line cards (≈ `room.bbs`), Jory Pike and job tickets handed over in person (≈ `people.hire` + the hand-off rung), pay day and the wall calendar, faults and repairs, the pole line to Hollow Farm.
- **Actors and actions (`NEZ_AND_THE_SPINE.md`):** a tree **task is an action**; a **stage says which actor performs it** (player, staff, a machine, or Nez) and at what speed and accuracy. "You're always the fastest" becomes: the player's speed is the best of any actor for that action. Build the spine's steps 1–2 first, then TT1.
- **Phone, not notebook:** where this doc says the tree shows in PIP's Almanac or the notebook, read "on the phone" if the spine's phone decision holds.
- Where this doc and the spine disagree, the spine wins until an idea session decides otherwise.

## 1. The rule

> **You're always the fastest. You just can't be everywhere.**

The tree is a list of jobs the player does with their own hands, and the ways to buy those hours back. Every **node** unlocks **tasks**. Every task climbs the same **hand-off ladder**, and every rung creates the next problem (`newProblem`, already enforced by a test).

| Rung | Who | Speed | Accuracy | Catch |
|---|---|---|---|---|
| 1 Do it | Player | 1.0× (faster with mastery) | 100% | Costs the player's hours |
| 2 Show it | Ride-along | 1.0× (player works) | +2% per rep to the trainee | Time spent teaching is time not working |
| 3 Hand it off | Staff | 1.5–2.5× | 80–92% | Runs in parallel. Defects are latent and surface later |
| 4 Automate | Tool or system | ~0.05× | 95–99% | When it breaks, it breaks for everyone at once |
| 5 Maintain | Upkeep chore | — | — | Skip it and the stage slides back down a rung |

Hard rules:
- Staff accuracy is **always below 1000** and staff time is **always above** the player's. The player never loses the "fastest hands" title.
- Rungs 2 and 5 are not separate `WorkMode`s. Show it is training on a staff member; Maintain is an `upkeepTask` plus `wear` on an automated stage.
- Some tasks stop at a rung on purpose ("Pitch an investor", "Warn Tobi about the raid"). Their stage list is just shorter. That is fine and intended.

## 2. Data model

A branch is **one JSON file** in `game/content/tree/`. Code is only needed for a brand-new **verb** (a minigame or interaction).

```
Branch  → file: game/content/tree/<branch>.json
 Node   → unlocks tasks, grants stats. Gate = Almanac recall and/or Conditions, plus required nodes
  Task  → one job with a verb (frame, drop-wire, counter, queue, phone, paperwork…)
   Stage → Manual → Delegated → Automated (today's WorkStageDef, plus who/timeMul/requiresNode/upkeepTask)
State   → per task: stage index, assignee, mastery, wear, backlog
```

### 2.1 JSON (one branch file)

```json
{
  "id": "lines", "name": "Lines", "order": 1,
  "nodes": [
    { "id": "lines.hunt", "tier": 2, "name": "Hunt Group", "act": 1,
      "requires": ["lines.frame"],
      "unlock": { "entry": "alm-hunt", "conditions": [] },
      "grants": [ { "stat": "capacity.lines", "add": 4 } ],
      "creates": "More lines, bigger bill from the incumbent.",
      "chaos": { "id": "borrow-neighbour", "echo": 2, "defect": "party-line" },
      "tasks": [
        { "id": "lines.hunt.chain", "name": "Chain the rollover lines",
          "verb": "frame", "baseSeconds": 60,
          "stages": [
            { "mode": "manual", "interface": "paper" },
            { "mode": "delegated", "interface": "paper", "who": "tech",
              "accuracyPermille": 850, "timeMul": 2.0,
              "newProblem": "Wrong order means a silent busy signal." },
            { "mode": "automated", "interface": "terminal",
              "accuracyPermille": 970, "timeMul": 0.05, "cost": 900, "monthlyUpkeep": 30,
              "requiresNode": "sight.monitoring", "upkeepTask": "sight.monitoring.poll",
              "newProblem": "One bad config takes down all four." } ] } ] } ] }
```

Conventions: node ids are the short ones in §5 headings (`lines.frame`, `sight.pager`); task ids add a short verb-ish slug (`lines.frame.jumper`, `lines.frame.reroute`), lower-case, stable forever. JSON is camelCase like the rest of `content/`.

### 2.2 C# records (`src/Telo.Sim/Content/`)

```csharp
public sealed record BranchDef { required string Id; required string Name; int Order; List<TechNodeDef> Nodes; }
public sealed record TechNodeDef {
  required string Id; int Tier; int Act; string Name;
  List<string> Requires;          // node ids
  UnlockDef Unlock;               // Entry (almanac id, optional) + List<Condition>
  List<EffectDef> Grants;         // { Stat, Add } typed stat modifiers
  string Creates; ChaosDef? Chaos;
  List<TaskDef> Tasks;
}
public sealed record TaskDef {    // today's WorkKindDef + a verb
  required string Id; required string Verb; int BaseSeconds;
  List<WorkStageDef> Stages;      // WorkStageDef gains: Who, TimeMul, RequiresNode, UpkeepTask
}
// GameState (serialisable, ids + numbers only)
Dictionary<string, TaskState> Tasks;   // TaskState { Stage, Assignee, Mastery, Wear, Backlog }
HashSet<string> UnlockedNodes;
```

### 2.3 Rules (`TreeRules`, `TaskRules`, pure, use `DetRng`)

```
yourTime    = baseSeconds × max(0.5, 1 − mastery × 0.02)
staffTime   = baseSeconds × stage.timeMul × (1 + stress)        // always > yourTime
accuracy    = stage.accuracy + training + sheetQuality − stress  // capped below 1000
defect?     = DetRng.Roll(1000 − accuracy) → latent, surfaces later (existing D6 rules)
wear       += days since upkeepTask last done;  wear > limit → stage −= 1
canUnlock   = all requires unlocked ∧ entry recalled (if any) ∧ all conditions met
```

All numbers are first guesses. Tune in playtests; never hard-code them outside content or one constants class.

### 2.4 Migration from today's `work.json`

The five shipped work kinds become tasks. Saves store work kind id → stage index, so `SaveCodec` gets a migration that maps old ids (never delete them silently):

| Old work kind | New task |
|---|---|
| `install` | `lines.frame.jumper` |
| `billing` | `money.billing.run` |
| `support` | `customers.machine.diagnose` |
| `repair` | `lines.frame.reroute` |
| `board` | decide in TT1 (likely `room.bbs`) |

Callers like `WorkRules.Current(state, content, "install")` in `Simulation.cs` / `GameRoot.cs` switch to the new task ids in the same commit.

### 2.5 New Condition kinds needed

Existing: `Subscribers, Cash, Staff, MonthlyRevenue, LedgerServed, RecalledEntries`. Add only when a node needs one: `Vans`, `Sites`, `Outages`, `NodeUnlocked`.

### 2.6 Adding a branch (checklist)

1. Write `game/content/tree/<branch>.json`. Reuse existing verbs.
2. A new verb needs C#: a pure class in `src/Telo.Sim/Minigames/<Name>/` (see CLAUDE.md, "Adding a new building minigame").
3. Every `Recall` gate needs an entry in `almanac.json` with its announcement.
4. A new stat named in `grants` is registered once in `StatIds`.
5. Give each task a **world tell** (§4): one prop that shows its backlog.
6. `ContentDb.Validate()` catches: duplicate ids, unresolved `requires`/`requiresNode`/`upkeepTask`/`entry`, tasks without a manual stage, delegated accuracy ≥ 1000, staff `timeMul` ≤ 1.

## 3. The tree at a glance

Within a branch, each tier requires the tier above it. Cross-branch links are written in the task text, e.g. "(needs Sight › Pager)", and become `requiresNode` on that stage.

| Branch | T0 | T1 | T2 | T3 | Later (Acts 4–7) |
|---|---|---|---|---|---|
| **A Lines** | Drop & Socket *(A0, Start)* | The Frame *(A0, Start)* | Hunt Group *(A1, Recall alm-hunt)* | Street Cabinet *(A3, Recall alm-adsl)* | Fibre to the home · Small cells · LEO gateway |
| **B The Room** | BBS Machine *(A0, Recall alm-bbs)* | 2400 Modems *(A0, Recall alm-2400)* | Modem Bank *(A1, Recall alm-14k)* | Dial-in Login *(A2, Recall alm-radius)* | Cold aisle datacentre · CDN node · Edge compute |
| **C Field** | The Van *(A0, Start)* | Parts Run *(A0, Start)* | Dispatch *(A1, Milestone: 2 staff)* | Second Crew *(A2, Milestone: 5 staff, 2 vans)* | Trench crews · Cherry pickers · Drone surveys |
| **D Customers** | Door Knocking *(A0, Start)* | Answering Machine *(A0, Start)* | Helpdesk *(A1, Milestone: 30 subscribers)* | Call Centre *(A3, Milestone: 500 subscribers)* | Web self-service · App support · Chatbots that annoy |
| **E Money** | Cash Tin *(A0, Start)* | Bookkeeper *(A1, Milestone: $2,000 cash)* | Billing Software *(A1, Recall alm-billing)* | The Boardroom *(A3, Milestone: $1M revenue)* | IPO · Acquisitions · Spectrum auction bids |
| **F People** | First Hire *(A1, Milestone: 10 subs, $500/mo)* | Show Me *(A1, Needs First Hire)* | Procedures Binder *(A2, Needs Show Me)* | Foremen *(A3, Milestone: 15 staff)* | Unions · Executives · The company outgrows you |
| **G Sight** | The Notebook *(A0, Start)* | Pager *(A1, Milestone: first outage)* | Monitoring *(A1, Recall alm-monitoring)* | The NOC *(A3, Needs Monitoring, 20 sites)* | AI ops · Digital twin · The dashboard that lies |
| **H Community** | Promise Ledger *(A0, Start)* | Tobi's File Area *(A0, Recall alm-bbs)* | Library Terminal *(A2, Recall alm-web)* | Promise Meeting *(A3, Milestone: 3 promises kept)* | Lifeline tariff · Clinic lines · Satellite to the islands |

The "Later" column is filled out age by age in `docs/AGES.md` §4.

Gates: **Start** = open from day one. **Recall alm-x** = an Almanac entry (`alm-2400`, `alm-bbs`, `alm-hunt`, `alm-billing`, `alm-monitoring`, `alm-14k`, `alm-web`, `alm-radius`, `alm-adsl` already exist). **Milestone** = a Condition. **Needs X** = a required node.

## 4. In-world gameplay (what makes hand-off fun)

Build these as sim rules first; each says where it lives.

1. **Your hours are the currency.** One day is 8 real minutes. Every task you do yourself spends them. The tree is a shop where you buy hours back. *Sim: a per-day hour budget. The HUD wall clock gets a second hand that turns red when you're behind.*
2. **Take over.** Walk up to anyone mid-task and press E: you finish it at your speed, and they learn by watching (+accuracy). *Makes "player is always fastest" a verb, not a stat. Works on every task for free.*
3. **Job sheets you actually write.** Handing off means filling in a paper job sheet on the desk. Steps you've done yourself pre-fill. Skipped steps = lower accuracy. *Delegate accuracy = stage base + sheet quality + training. Diegetic, no menu.*
4. **Latent defects you can hunt.** Delegated work hides faults that surface weeks later. Spot-check with a torch at the frame, or ride along, to catch them cheap. *Already in the sim (D6). Adds an inspection verb and a reason to visit your crews.*
5. **The world shows the backlog.** Job sheets pile up on the desk. The answering machine light blinks a count. The modem bank shimmers with heat. *Every un-handed-off task has a physical tell. No dashboard until Sight earns it.*
6. **Pager triage.** An outage pages you mid-install. Drop it, or let a tech handle it at 85%? Some pages only you can fix early on. *Creates interesting choices from systems you already have. Gets calmer as Sight grows.*
7. **Mastery.** Each rep of a task makes you faster (to a floor). Your staff cap below your mastery, so teaching well matters. *Per-task mastery counter. Old hands at the frame feel genuinely quick.*
8. **Staff are people.** Names, quirks, stress, ambition. Overwork them and accuracy drops; they quit and take what they know, unless it's in the Binder. *Bus factor as gameplay. Pairs with People › Procedures Binder.*
9. **Rush jobs.** Tell a crew to rush: 30% faster, double the defects. Great before a deadline, painful a month later. *One lever, reused on every hand-off stage.*
10. **Automation rots.** Each automated stage has an upkeep chore. Skip it and the stage slides back down a rung, often on the worst day. *"Maintain" is a real rung: a cheap, recurring task to delegate in turn.*
11. **Chaos shortcuts.** Every node has one fast, cheap, wrong option that persists in the world: a stapled cable, a dirty pair, a skipped backup. *Matches the minigame rule. Feeds Echo, evidence and latent defects.*
12. **The boss problem.** From Act 3 the Boardroom pulls you into meetings. Going back to the frame lifts crew morale but costs the quarter. *The emotional ladder: the company becomes bigger than you.*

**World tells** (no dashboards until Sight earns them): job sheets piling on the desk, the answering machine's blinking count, heat shimmer on the modem bank, a full tray of invoices, a pager on the bedside table.

## 5. Branches in full

### A. Lines (`lines`)

*Getting a customer connected. Buys back: Your install hours.*

#### A0 Drop & Socket · `lines.drop` · Act 0 · Start

Creates: Every install is half a day of your time.  Chaos shortcut: Staple the cable across a doorway.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Run the drop wire | Ladder up the pole, clip six points to the wall. | Installer · 85%, slower, sometimes misses a clip | Pre-terminated drop kits: three clips, no splicing |
| Fit the wall socket | Pick the spot, screw it on, run cable along the skirting. | Installer · 85%, two a day | Push-fit sockets. You spot-check one in five |
| Test for dial tone | Plug in the handset, listen, fix the polarity. | Installer with a test set · 90% | Line test from the frame (needs Sight › Pager) |

#### A1 The Frame · `lines.frame` · Act 0 · Start

Creates: Labels decay. Every jumper is a future fault.  Chaos shortcut: Reuse a dirty pair.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Jumper a new customer | Click line, then port. Tone-probe to confirm. | Junior tech working from your job sheets · 85% | Ticket database shows bad pairs · 92% (alm-monitoring) |
| Relabel smudged blocks | Probe each pair and write a fresh tag. | Apprentice on quiet afternoons · 90% | Label printer: tags stop fading |
| Reroute a faulty port | Test it and move it to a spare. | Tech with a spares list · 85% | Spare-pair register flags reroutes before customers notice |

#### A2 Hunt Group · `lines.hunt` · Act 1 · Recall alm-hunt

Creates: More lines, bigger bill from the incumbent.  Chaos shortcut: Borrow the neighbour's line.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Chain the rollover lines | Wire four lines in order at the frame. | Tech · 85%. Wrong order means a silent busy signal | Terminal config: one line per change |
| Order lines from the incumbent | Drive to their office, queue, fill forms in triplicate. | Bookkeeper posts the forms · two-week lag | Account manager at the incumbent, a relationship to keep warm |
| Count busy signals | Sit by the phone with a tally sheet. | Receptionist keeps the tally | Call counter prints a daily strip (Sight) |

#### A3 Street Cabinet · `lines.cabinet` · Act 3 · Recall alm-adsl

Creates: Distance from the exchange decides who gets speed.  Chaos shortcut: Leave the cabinet door unlocked.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Wire a street cabinet | Open it in the rain, jumper 50 pairs. | Cabinet crew · 88% | Remote provisioning: switch on from a screen · 99% |
| Qualify an address | Measure the copper run on the paper map. | Sales reads the line-length table · 90% | Address checker on the phone menu |
| Chase a noisy pair | Night test with a meter. | Line tech · 85% | Auto line-tuning, wakes you only when it gives up |

Later: Fibre to the home · Small cells · LEO gateway

### B. The Room (`room`)

*Capacity behind the lines. Buys back: Your 3 am wake-ups.*

#### B0 BBS Machine · `room.bbs` · Act 0 · Recall alm-bbs

Creates: It hangs at 3 am and only you know the reset.  Chaos shortcut: Skip the backup.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Unhang the board | Walk to the room, power-cycle, check the disk. | Tobi as volunteer sysop · free, unreliable | Watchdog card reboots it. You swap the card yearly |
| Back up the disk | Feed 20 floppies at 2 am. | Sysop on a rota | Tape drive at night. Someone still rotates tapes |
| Moderate the boards | Read every post, delete the rude ones. | Volunteer moderators, paid in free hours | Word filter (blocks the wrong words too) |

#### B1 2400 Modems · `room.modem2400` · Act 0 · Recall alm-2400

Creates: Customers expect speed now. All at 7 pm.  Chaos shortcut: Sell a modem with a cracked case.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Configure a modem | Type the init string on the handset. | Tech with a cheat card · 90% | Supplier ships them pre-set (Field › Parts Run) |
| Swap a dead modem | Unscrew, swap, retest. | Tech with a spares shelf · 90% | Hot-spare slot: the bank fails over by itself |
| Tune a bad connection | Trial and error with the customer on the line. | Helpdesk script · 80% | Auto-negotiating firmware |

#### B2 Modem Bank · `room.bank` · Act 1 · Recall alm-14k

Creates: Heat, noise and the upstream bill.  Chaos shortcut: Run it with the case off.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Rack the bank | Lift, power, cable it to the hunt group. | Two techs. It's a two-person lift | Done once, then it needs keeping |
| Keep it cool | Move the fan, chalk temperatures on the wall. | Caretaker reads the thermometer twice a day | Thermostat with an alarm to your pager |
| Clean the fan filters | Weekly, with a ladder and a vacuum. | Cleaner on a schedule | Maintenance contract. Miss a payment and they clog |

#### B3 Dial-in Login · `room.login` · Act 2 · Recall alm-radius

Creates: One server checks every login. If it dies, nobody gets in.  Chaos shortcut: One password for 40 customers.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Add a new account | Type a username and password into the terminal. | Receptionist at sign-up · 95% | Billing creates accounts automatically |
| Reset a password | Phone call, verify, reset. | Helpdesk · 90% (can be talked into it) | Self-reset on the web |
| Run a second server | Build, sync, test the failover. | Server admin · 90% | Automatic failover. Test it monthly or it rots |

Later: Cold aisle datacentre · CDN node · Edge compute

### C. Field (`field`)

*Getting there with the right part. Buys back: Your driving time.*

#### C0 The Van · `field.van` · Act 0 · Start

Creates: Every trip is time the frame isn't worked.  Chaos shortcut: Overload the van.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Drive to the job | Drive, park, walk the last metres. | Driver drops a tech off | Route plan printed each morning (Dispatch) |
| Load the day's cargo | Carry modems, drum and ladder from the shop. | Junior loads from a list · 90% (forgets the ladder) | Pre-packed kits by job type |
| Service the van | Fuel, oil, tyre pressure. | Mechanic on Main St, monthly | Fleet contract. Breakdowns become someone else's call |

#### C1 Parts Run · `field.parts` · Act 0 · Start

Creates: Stock is cash sitting on a shelf.  Chaos shortcut: Buy stolen stock at the back door.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Haggle at Sol's | Counter trade: price against lead time. | Bookkeeper orders by phone, pays list price | Standing weekly order |
| Carry stock home | Two-handed and slow. | Sol delivers, for a fee | Supplier delivers to a stockroom |
| Count the shelf | Tally sheet on Sunday nights. | Junior counts · 90% | Reorder points restock below a number |

#### C2 Dispatch · `field.dispatch` · Act 1 · Milestone: 2 staff

Creates: Whoever plans the day decides who waits.  Chaos shortcut: Promise everyone "this morning".

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Plan the day's route | Pins on the paper map. | Dispatcher at a desk · 85% | Dispatch software (Act 3) |
| Book appointments | Calendar on the wall. | Receptionist | Customers pick slots on the web |
| Reshuffle on an outage | Radio each van yourself. | Dispatcher with a CB radio | Auto-reroute, asks you on a tie |

#### C3 Second Crew · `field.crew2` · Act 2 · Milestone: 5 staff, 2 vans

Creates: Two crews, two standards.  Chaos shortcut: Hire unlicensed subcontractors.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Run a crew | Lead, carry, check. | Crew lead · 90% | Crew leads train their own juniors (People › Foremen) |
| Inspect a crew's job | Drive out and spot-check. | Quality inspector, finds 70% of defects | Photo report with every job |
| Stock a second van | Duplicate the kit by hand. | Stores clerk | Van kits restock themselves (Parts Run) |

Later: Trench crews · Cherry pickers · Drone surveys

### D. Customers (`customers`)

*Selling and keeping people happy. Buys back: Your evenings on the phone.*

#### D0 Door Knocking · `customers.door` · Act 0 · Start

Creates: Salespeople sell what you can't build yet.  Chaos shortcut: Promise speeds you don't have.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Pitch a household | Knock, read the archetype, negotiate. | Salesperson on commission · 80% (overpromises) | Flyers and radio ads bring leads to you |
| Book the install | Write it on the calendar. | Receptionist | Dispatch books it (Field) |
| Win back a cancel | Visit, apologise, discount. | Account rep with a discount budget | Retention offers mailed automatically |

#### D1 Answering Machine · `customers.machine` · Act 0 · Start

Creates: Every message is a callback you owe.  Chaos shortcut: Erase the tape unheard.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Clear the tape | Listen and write down each message. | Receptionist with a notebook | Voicemail with mailboxes |
| Diagnose over the phone | "Is the light green?" | Helpdesk with a fault notebook · 85% | Fault flowchart, then a self-diagnosis page |
| Call back angry customers | They want the boss. | Receptionist. They still want the boss | Status line: "we know, we're on it" (needs Sight) |

#### D2 Helpdesk · `customers.helpdesk` · Act 1 · Milestone: 30 subscribers

Creates: Calls you used to hear never reach you.  Chaos shortcut: Close tickets unread.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Log a ticket | A card in a shoebox. | Helpdesk · 95% | Ticket database (alm-monitoring) |
| Escalate a hard fault | Walk the card to the frame. | Senior tech | Escalation rules |
| Read the complaints pile | Friday evenings. | Support lead summarises, misses the odd one | Weekly printed report |

#### D3 Call Centre · `customers.callcentre` · Act 3 · Milestone: 500 subscribers

Creates: Queue times become a headline.  Chaos shortcut: Hang up at ten minutes.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Triage the queue | The Queue: severity vs revenue vs SLA. | Shift supervisor · 88% | Auto-routing by account type |
| Train new agents | Sit beside them. | Trainer. Agents cap at 85% | Scripted screens |
| Answer a business account | Personally. They pay the most. | Business desk | Account manager with an SLA dashboard |

Later: Web self-service · App support · Chatbots that annoy

### E. Money (`money`)

*Knowing whether you're profitable. Buys back: Your kitchen-table nights.*

#### E0 Cash Tin · `money.tin` · Act 0 · Start

Creates: You don't know if you're profitable.  Chaos shortcut: Pay cash, no receipts.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Write carbon invoices | Kitchen table, one per customer. | Part-time bookkeeper · 90% | Billing software (T2) |
| Chase a late payer | Knock on their door. | Bookkeeper's polite letters | Auto-reminders, then disconnection |
| Bank the takings | Drive to the bank before 4. | Bookkeeper | Direct debit |

#### E1 Bookkeeper · `money.bookkeeper` · Act 1 · Milestone: $2,000 cash

Creates: The bookkeeper notices things. All of them.  Chaos shortcut: Ask them not to notice.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Pay the bills | Cheques and envelopes. | Bookkeeper · 95% | Standing orders |
| Do the quarterly tax | Shoebox of receipts and a calculator. | Accountant: expensive, accurate | Accounting software |
| Price a new plan | Guess, then test on the neighbours. | Bookkeeper runs the numbers | Price experiments (Act 3) |

#### E2 Billing Software · `money.billing` · Act 1 · Recall alm-billing

Creates: One bug bills everyone twice.  Chaos shortcut: Ship it untested.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Run the monthly bill | Press go, check ten by hand. | Bookkeeper checks samples · 97% | Billing platform with graphs (vendor lock-in) |
| Fix a wrong bill | Refund and apologise. | Helpdesk credit button | Automatic credits for outages |
| Enter new customers | Retype them from the sign-up sheet. | Receptionist | Sign-ups flow straight into billing |

#### E3 The Boardroom · `money.boardroom` · Act 3 · Milestone: $1M revenue

Creates: Investors want growth every quarter.  Chaos shortcut: Lie to the analysts.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Set the quarterly budget | The Boardroom minigame. | Finance director proposes, you approve | Rolling forecasts |
| Pitch an investor | Always you. It's your story. | CFO handles the follow-ups | Never automated |
| Answer earnings questions | You, live. | Investor relations: safer, blander | Never automated |

Later: IPO · Acquisitions · Spectrum auction bids

### F. People (`people`)

*Makes every other hand-off better. Buys back: Mistakes, and your patience.*

#### F0 First Hire · `people.hire` · Act 1 · Milestone: 10 subs, $500/mo

Creates: Payroll every fortnight, rain or shine.  Chaos shortcut: Pay cash in hand.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Post a job ad | A card on Sol's noticeboard. | Trusted staff refer people (may hire friends) | Recruiter (Act 2) |
| Interview | Ask, then watch them try the frame. | Senior staff interview | Assessment day |
| First-day ride-along | They watch you work. Accuracy goes up. | Senior staff take ride-alongs | Induction programme |

#### F1 Show Me · `people.showme` · Act 1 · Needs First Hire

Creates: Time spent teaching is time not working.  Chaos shortcut: Throw them in the deep end.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Teach a task | Do it with them watching. Each rep +2%, cap 90%. | Senior trains junior, cap 85% | Training room with a practice frame |
| Correct a mistake | Find the defect and show them. | Crew lead | Mistake log everyone reads |
| Certify a tech | Watch them solo once. | Trainer | Certificate programme |

#### F2 Procedures Binder · `people.binder` · Act 2 · Needs Show Me

Creates: Wrong procedures get followed perfectly.  Chaos shortcut: Copy a rival's binder.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Write a procedure | From your own notes. Quality = steps you've really done. | Senior tech writes, lower quality | Binder becomes a database (Sight) |
| Update after an outage | Post-mortem, add the missing step. | Ops lead | Root-cause reviews |
| Audit the floor | Walk around with the binder. | Quality inspector | Checklist on every job sheet |

#### F3 Foremen · `people.foremen` · Act 3 · Milestone: 15 staff

Creates: Managers manage you, too.  Chaos shortcut: Promote your friend.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Run the morning meeting | Assign and motivate. | Ops manager | Stand-up board |
| Settle a staff conflict | You, awkwardly. | HR person | Policies, and their loopholes |
| Keep morale up | Show up at the frame. Crews love it. | Managers. Decays without you | Culture: it outlives you or it doesn't |

Later: Unions · Executives · The company outgrows you

### G. Sight (`sight`)

*How you know what's happening. Buys back: Driving out to look.*

#### G0 The Notebook · `sight.notebook` · Act 0 · Start

Creates: It only knows what you wrote.  Chaos shortcut: Rip out a page.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Log a fault | Time, line, symptom. | Anyone who picks it up | Fault database (T2) |
| Keep the customer list | Name, line, port, fee. | Receptionist | Customer database |
| Draw the network | Pencil diagram on the wall. | Tech redraws it monthly · 85% | Network map on screen |

#### G1 Pager · `sight.pager` · Act 1 · Milestone: first outage

Creates: You are never off duty.  Chaos shortcut: Leave it in the van.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Respond to a page | Drop everything and drive. | On-call tech | Auto-escalate if nobody answers |
| Hand over on-call | Write the handover note. | On-call rota | Rota software |
| Decide what pages you | Rules on paper. | Ops lead | Alarm thresholds |

#### G2 Monitoring · `sight.monitoring` · Act 1 · Recall alm-monitoring

Creates: Equipment says it's ill. Most of the time.  Chaos shortcut: Silence the noisy one.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Read the poll printout | Dot-matrix strip each morning. | Tech skims it, misses slow trends | Terminal status screen |
| Chase a flapping port | Watch, test, replace. | Tech | Auto-quarantine plus a ticket |
| Graph the traffic | Plot by hand on graph paper. | Network engineer | Traffic graphs feed capacity planning |

#### G3 The NOC · `sight.noc` · Act 3 · Needs Monitoring, 20 sites

Creates: Thirty alarms, one cause.  Chaos shortcut: Silence every alarm.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Correlate an alarm storm | The Alarm Storm minigame. | NOC shift · 85% | Correlation engine (Act 6) |
| Write the incident report | You, the night after. | Shift lead | Auto-timeline |
| Run a failover drill | Pull a cable on purpose. | NOC manager | Scheduled chaos tests (Act 7) |

Later: AI ops · Digital twin · The dashboard that lies

### H. Community (`community`)

*The Last Mile and the Borrowed Light. Buys back: Nothing. That's the point.*

#### H0 Promise Ledger · `community.ledger` · Act 0 · Start

Creates: Every promise has a date.  Chaos shortcut: Promise and forget.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Visit an unserved home | Drive to Hollow Farm, hear Ruth's reason. | Community officer | Ledger fills from census data (Act 4) |
| Cost a connection | Pace the distance, count the poles. | Tech estimates · 85% | Hex planner prices routes |
| Keep a promise | Build the first one yourself. | Crew assigned to it | USO contract pays after build-out |

#### H1 Tobi's File Area · `community.files` · Act 0 · Recall alm-bbs

Creates: Famous for the wrong reason.  Chaos shortcut: Let everything through.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Curate the files | The Borrowed Library minigame. | Tobi curates, lets grey stuff in | Upload rules (they'll test them) |
| Answer the complaint letter | Carefully. | Bookkeeper drafts it | Published policy page |
| Warn Tobi about the raid | Only you. It's personal. | Can't be handed off | Can't be automated |

#### H2 Library Terminal · `community.library` · Act 2 · Recall alm-web

Creates: Low margin, high visibility.  Chaos shortcut: Put ads on it.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Install the terminal | Carry, wire, test. | Tech | Remote-managed terminals |
| Hand out time slots | The Queue: who needs it most? | Librarian with a sheet | Booking on the terminal itself |
| Fix it after the kids | Every week, again. | Tech, monthly visit | Locked image restores nightly |

#### H3 Promise Meeting · `community.meeting` · Act 3 · Milestone: 3 promises kept

Creates: Fail and they sign with a co-op or a rival.  Chaos shortcut: Overstate the coverage map.

| Task | You do | Hand off | Then (automate / maintain) |
|---|---|---|---|
| Present the coverage plan | The Promise Meeting minigame. | Community officer, less trusted | Can't be automated |
| Recruit local volunteers | Town hall, folding chairs. | Co-op organiser | The co-op maintains itself |
| Hand the network over | Sign over the co-op shares. | Lawyer | It runs without you. The point |

Later: Lifeline tariff · Clinic lines · Satellite to the islands

## 6. Open questions (idea sessions decide, then record in DECISIONS.md)

- Do staff have one task each, or a role covering several tasks (installer = drop + socket + dial tone)? Leaning: a role, with per-task accuracy.
- Is mastery per task or per verb? Per verb is less bookkeeping and rewards reuse.
- Does the tree appear as one object (PIP's Almanac for tech, the notebook for people/money), or two? Leaning: split, both diegetic.
- How many staff before Dispatch becomes mandatory rather than optional?
