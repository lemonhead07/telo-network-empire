# Shop Days and the Clue Card

*Idea session, 2026-11-18, from the owner's direction. Proposed decision D45 (provisional, like D34: built on the recommendations below until the owner says otherwise). Replaces D23's "a day is 8 real minutes" and changes how the sky moves. Everything else about lived work (holding E, bodges that show) stays.*

> **The owner, 2026-11-18:** "I feel like the days should be planned for the customer and only progress when they have reached a milestone, instead of the day/night cycle being linked so directly to the rotation and time speed. We should have days, and we should open the shop to progress the day, and there are before-day and after-day activities. We will need cash register gameplay at some point. This should fix any issues about game time, as we will just add more things on the planet for them to do. … I want it large and grand, and step by step, so the player has a simple clue screen for the line of progression that we can hint for the rest."

---

## 1. The idea in one picture

A day is not a stretch of real time. **A day is a shift you open and close.** Nothing on the calendar moves until you flip the sign on your shop door to OPEN, and the day doesn't end until the work promised for it is done (or you give up on it and close).

```
        ┌──────────── BEFORE OPENING (dawn, frozen) ─────────────┐
        │  read the overnight text · check the Clue Card         │
        │  plan today's sheet · load the van · buy stock         │
        │  brief staff · fix what broke in the night             │
        └──────────────────────┬─────────────────────────────────┘
                               │  flip the sign: OPEN
        ┌──────────────────────▼─────────────────────────────────┐
        │  THE DAY (the sun moves only as the work moves)        │
        │  customers at the counter · booked jobs round town     │
        │  walk-ins · faults · deliveries · NezTel's van         │
        └──────────────────────┬─────────────────────────────────┘
                               │  today's list done → CLOSE UP
        ┌──────────────────────▼─────────────────────────────────┐
        │  CASH UP (dusk): the till, the takings, the ledger     │
        └──────────────────────┬─────────────────────────────────┘
        ┌──────────────────────▼─────────────────────────────────┐
        │  AFTER HOURS (night, frozen, optional, the open world) │
        │  the Board's evening rush · projects by lamplight      │
        │  explore the planet · the pub · Nez · the Almanac      │
        └──────────────────────┬─────────────────────────────────┘
                               │  bed: send the night text
                               ▼  next morning
```

Three things fall out of this:

1. **Game time never runs away from a slow player.** You can stand in the shop reading for an hour; the sun won't move.
2. **The calendar moves at the speed of progress**, not the speed of the planet. That is what fixes the pace problem in `BUILD_PLAN.md` EA2e (122 hours to Act 5 against 20–30): a day that is all milestones can't be wasted.
3. **There's always more to do than the day asks.** Before and after hours are open-ended, so every new thing on the planet (a village, a dock, a hill to survey, a stranger at the antipode) is something to do *between* shifts, and it doesn't crowd the shift.

---

## 2. The three forks (options, with the recommendation taken)

### Fork A: how does time move while the shop is open?

| Option | How it works | Good | Bad |
|---|---|---|---|
| **A1. The clock follows the work** ✅ | Every action already has a time cost (holds, travel, `AppointmentRules`). While open, the clock moves only by those costs. Standing still costs nothing. The sun's height is the clock. | Never rushed; the sky still tells the story of the day; who-is-home (D24) keeps working because there is still a clock. | Players can't "feel" time pass while idle; needs a soft reason to get on with it (see the queue at the counter). |
| A2. Real time while open | Opening starts an 8–12 minute real-time day; before and after hours are frozen. | Simple; a little pressure. | Brings back the problem the owner wants gone: the day runs out under you. |
| A3. No clock at all | The day is a checklist; the sun's position is "fraction of today's list done". | Purest. | Breaks who-is-home, appointments, the evening rush, and the script player's timings. Too much to rebuild. |

**Taken: A1.** Holding E still takes real seconds (the work is lived, D23 keeps that part), but those seconds are *the work's* minutes, and the clock waits for you between jobs. The only gentle pressure is people: a customer at the counter loses patience in real time (a soft clock, never a fail), and a booking left past its day goes to NezTel (D37).

### Fork B: where does the Clue Card live?

| Option | Good | Bad |
|---|---|---|
| **B1. A page on PIP's phone, with a physical twin on the corkboard by the door** ✅ | All text on the phone (spine N5); the corkboard card is the "world tell" you see before opening. | Two places to keep in step (one source in the sim, two views). |
| B2. Only the corkboard | Most diegetic. | You can't read it out on the road. |
| B3. Only the phone | Simplest. | The morning ritual has nothing to look at. |

### Fork C: is a "day" always a day?

| Option | Good | Bad |
|---|---|---|
| **C1. The shift grows with the business** ✅ (day → week → month) | The game's promise ("the job changes as you play") on the calendar itself; years pass naturally in later acts; matches the planner growing from one column to a month wall-planner (spine §7). | Needs care at each change-over so the first longer shift is explained. |
| C2. Always a day | Simple. | Act 5 is thousands of identical days; the 20–30 hour target is impossible. |

---

## 3. The day in detail

### Before opening (dawn; the clock is frozen at 07:00)

The morning is a ritual, and it's where planning lives. Nothing here costs game time.

- **The night text** waiting on the phone (one text a day, the time-travel joke).
- **The Clue Card** on the corkboard: where you are on the line, and the next clue (section 6).
- **Today's sheet**: the timesheet (`Plan/PlanRules.cs`) with the bookings for today already pinned. You can move them, add walk-in slots, leave gaps for the counter.
- **Load the van** for the jobs on the sheet (kit is a sim rule: forget the ladder and you drive back).
- **Stock**: buy modems at Sol's (Sol opens before you, at 07:30 in this model) or, later, collect parts at the Yard (D43).
- **Staff**: hand out tickets at the door (D25); later, a morning briefing at the depot.
- **Overnight faults**: anything that broke in the night is a `!` on the map and a card on the rail.

**Flip the sign to OPEN.** This is the only way the calendar moves forward in the daytime. The sign is an object on the shop door: look at it, hold E.

### The day (the sun moves as the work moves)

- **Booked jobs** round town, from the sheet: installs, orders, odd jobs, repairs, VIPs (D39).
- **The counter**: people walk in while the shop is open (section 5). If you're out on a job, the door has a bell and a note; a walk-in leaves a message on the machine, or waits a little and then leaves grumbling. Later a shop assistant minds it.
- **The day's events**: a delivery, NezTel's van in your street, a pole down after a storm, the mayor's call. One or two a day, never more.

**When the day's list is done**, the phone says so and the sign glows: **Close up.** You can also close early (the jobs left roll to tomorrow and those customers lose a little patience), or stay open past the list for walk-ins until 17:30, when the shop shuts anyway.

### Cash up (dusk)

The day's takings are counted at the till: what you rang up, against what's in the drawer. Mistakes at the counter show here as a short or over drawer (the counter's version of a latent defect). The day's line in the ledger is written. This is short: one look, one press, a number, a one-line comment from PIP.

### After hours (night; the clock is frozen at 19:00 again until you choose something that takes time)

The open world. Nothing here is required by today; everything here is the way forward.

- **The Board's evening rush** (`BoardRules.Evening`): sit at the switchboard and watch the lamps, or don't.
- **Projects by lamplight**: plant poles, string spans, fit out a cabinet. Night work is slower (a torch, not daylight) but doesn't eat a shift.
- **Explore**: drive the valley road, sail to Kestrel Point, survey a hill for a mast, visit Nadir.
- **Social**: the pub on Main Street (word of mouth, gossip that names the next customer), the parish council, Sol's back room.
- **The Almanac**: recall a page.
- **Bed**: send the night text. The next day starts at dawn, frozen.

Things done after hours that take time (a project step, a long drive) move the night clock; at midnight you're tired (`SleepRules`, gently), and dawn comes when you sleep, never before.

### The sky

The planet still spins in the picture, but **the sun is placed by the clock, and the clock is placed by the phase.** Dawn while you prepare, morning to afternoon as the day's list empties, a golden dusk at cash-up, night after hours. From orbit the terminator sits where it has to for your town's hour. Real-time rotation no longer drives anything.

---

## 4. The shop

The shop is the place the day turns on, so it moves house with the business.

| Act | The shop | The sign | The counter |
|---|---|---|---|
| 0 Bedroom | The front door of the rented room, a card in the window: *TELO — LINES FITTED* | A card you turn round | A biscuit tin and a receipt book |
| 1 BBS | **1 Main Street** (the Act 0 exit text already signs this lease) | An enamel OPEN/CLOSED sign | A mechanical cash register |
| 2 Dial-up | 1 Main Street, now with a back office | A neon OPEN | An electronic till with a printer |
| 3 Boom | A proper shopfront plus a call room upstairs | A lit fascia | A till computer: bills paid by card |
| 4 Broadband | Shops in Millbrook and Farland, each with an assistant | One sign per shop, flipped by its staff | Card terminals; online billing starts taking the queue away |
| 5 Mobile | A flagship store; phones in the window | Opening hours on a timer | The queue is for handsets, not bills |

In Acts 4–5 you don't flip every sign yourself. You open **your** day; the shops open themselves.

---

## 5. The cash register (gameplay, built later)

A small, satisfying counter job in the same eight verbs (D31): mostly *connect* (ring it up) and *test* (count the change).

**Who comes to the counter:**
- Pay a bill (most visits; cash, then cheque, then card).
- Buy kit: a phone, a modem, an extension lead, a fax roll.
- Sign up (a walk-in booking: the counter becomes a source of bookings beside the machine and the ads).
- Report a fault ("it crackles when it rains").
- Complain, or ask for a refund.
- A NezTel customer asking how to switch (D38).
- Characters: Sol with gossip, the mayor's secretary, a kid with a stack of floppy disks.

**The verbs at the till:**
1. **Ring up**: press the keys for what they want (the price list is on the wall; numbers keys, like the Board's terminal).
2. **Take the money**: they hand over a note.
3. **Give change**: pick coins and notes from the drawer.
4. **Receipt** (optional at first; later the ledger needs it).

**Mistakes**: ring up the wrong price or give the wrong change and the drawer is short or over at cash-up. A short drawer costs money; an over drawer is a customer who'll come back cross. Rushing (`[R]`, the bodge) is instant and usually right. Like everything else: no fail screen, just consequences.

**The queue** is the soft clock: two or three people at most; each loses patience in real time while you fumble (the only real-time pressure in the day).

**Handed to**: a shop assistant (a work stage in the tree: `money.counter`), 80–90% right; their mistakes show at cash-up. Then the till computer, then online billing: the paper → terminal → database → dashboard ladder, on the counter.

---

## 6. The Clue Card

### What it is

One card, always the same shape, that answers "what am I working towards?" without telling you how. It reads from `plot.json` (chapters) and `projects.json` (gated builds), the same source as the "what next" line (`Guidance`), so it can never disagree with the game.

### What it looks like

```
 ┌─────────────────────────────────────────────┐
 │  THE LINE                    Act 1 · BBS    │
 │                                             │
 │  ✓ A message on the machine                 │
 │  ✓ The first install                        │
 │  ✓ The farm line                            │
 │  ▶ BEHIND MAIN STREET                       │
 │     "Father Roe says the Lord can wait.     │
 │      The parish council can't."             │
 │     Clue: the church has no line.           │
 │  ? ─ ─ ─ down the valley ─ ─ ─              │
 │  ? ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─                │
 │                                             │
 │  [hint]                    Day 41 · Tue     │
 └─────────────────────────────────────────────┘
```

- **Done** chapters are ticked (tap one to re-read its text).
- **Now** shows the chapter title, its line of story, and a **clue**: a few words, never an instruction.
- **Next** shows a shadow: a three-word teaser.
- **Beyond** is just question marks. The line visibly goes on.

### Three levels of help, all free

| Level | When it appears | Example (Behind Main Street) |
|---|---|---|
| **Clue** | Always | "The church has no line." |
| **Nudge** | After 3 opened days with no progress on this chapter, or when you press [hint] | "Church Lane needs poles, like the farm did." |
| **Answer** | After 3 more days, or a second press | "Buy poles at Sol's, plant them up Church Lane (the sign by the lych-gate), then string the spans." Plus the ▼ marker on the place. |

Help is never a penalty (failure costs time, money or satisfaction, never progress); it just waits a little before saying more, so a player who likes to work it out gets the chance. The "what next" line on the phone stays as it is today: it's the answer for players who want it now.

### The line of progression (Acts 0–5)

The full line the card walks through. Each step has its clue. Steps are today's chapters and projects, plus the new shop beats (marked **new**). This is the backbone the hints hang off; content goes in `plot.json`.

**Act 0 — The bedroom (1983)**

| # | Step | Clue | Nudge |
|---|---|---|---|
| 1 | A message on the machine | "Something is blinking." | The answering machine on the desk. |
| 2 | The first install | "Everything you need is on the van." | Outside wall first, then indoors. |
| 3 | The switchboard | "Their socket works. Their line doesn't." | Jumper them at the board in your room. |
| 4 | **Open the shop** (new) | "A card in the window, face down." | Turn the card round. The day starts when you do. |
| 5 | Odd jobs | "One subscriber won't cover the rent." | A neighbour's radio is on the rug. |
| 6 | Door to door | "Not everyone rings." | Knock. |
| 7 | **The first cash-up** (new) | "Count it before you spend it." | The tin, at the end of the day. |
| 8 | The round | "There's a page in the notebook about help." | Hire Jory. |
| 9 | The farm line | "Ruth has asked twice." | No fee buys a line out there: build it. |

**Act 1 — The BBS (1983)**

| # | Step | Clue | Nudge |
|---|---|---|---|
| 10 | **1 Main Street** (new) | "The lease is signed. The keys are on the desk." | Move the shop: the counter, the till, the sign. |
| 11 | **The till** (new, with the register) | "People want to pay you in person." | Ring up, take, change. |
| 12 | Behind Main Street | "The church has no line." | Poles up Church Lane. |
| 13 | The evening rush | "Everyone rings at seven." | Line cards from Sol's, in the rack. |
| 14 | The valley road | "The Post Office has waited since 1971." | Eastgate's street cabinet. |
| 15 | Ninety-nine percent | "Do the sum on the back of the leaflet." | Fix faults the day they show. |

**Act 2 — Dial-up (1984)**

| # | Step | Clue | Nudge |
|---|---|---|---|
| 16 | The valley is yours | "Every house in the valley rings you." | 24 subscribers, then the modem bank. |
| 17 | **A week at a time** (new: the shift becomes a week) | "Jory can open without you." | The depot; the week planner replaces the day sheet. |
| 18 | Rack the modem bank | "The Board can't take a fifth caller." | The Almanac page on hunt groups. |
| 19 | The road out is NezTel's | "You need their wire to reach the world." | Lease the line. |
| 20 | Over the ridge | "Line of sight." | The microwave hop. |
| 21 | Millbrook | "A whole town, one exchange." | Build it; they sign over three weeks. |

**Act 3 — The boom (1984–)**

| # | Step | Clue | Nudge |
|---|---|---|---|
| 22 | **The second shop** (new) | "Millbrook pays its bills in Millbrook." | Hire an assistant for the Millbrook counter. |
| 23 | The cold store | "Somewhere cold and quiet for machines." | Fit out the old cold store. |
| 24 | Kestrel Point | "The sea is the next road." | The landing station. |
| 25 | Across the water | "Farland is listening." | The ocean cable. |

**Act 4 — Always on**

| # | Step | Clue | Nudge |
|---|---|---|---|
| 26 | **A month at a time** (new: the shift becomes a month) | "The wall planner has run out of days." | Crews; the month planner. |
| 27 | Broadband | "The exchange can do more than talk." | The page, then the rack in the exchange. |
| 28 | Round the bay | "A ring, not a line." | The fibre ring. |
| 29 | Farland city | "A thousand doors at once." | Farland's exchange. |

**Act 5 — In everyone's pocket**

| # | Step | Clue | Nudge |
|---|---|---|---|
| 30 | The first mast | "The highest hill." | Survey the ridge, raise the mast. |
| 31 | Along the coast | "Where the signal drops." | Cell sites; hills block coverage (D44). |
| 32 | Fibre to every mast | "A mast is only as good as its wire." | Backhaul. |
| 33 | **The flagship** (new) | "People queue for phones now." | The store; the counter sells handsets. |

Steps 4, 7, 10–11, 17, 22, 26 and 33 are new beats; the rest are today's chapters and projects with clues added.

---

## 7. The shift grows

| Acts | One shift is | You open | Before | After |
|---|---|---|---|---|
| 0–1 | **A day** | Your door, then 1 Main Street | Plan the day sheet, load the van | The Board, poles by lamplight, the pub |
| 2–3 | **A week** | Monday's sign; the week runs as you play its jobs, staff fill the days you don't | The week planner, staff tickets, the Yard | Projects, far towns, the sea |
| 4–5 | **A month** | Your office; shops open themselves | The month wall-planner, crews, budgets | Expeditions, masts, Nez |

Within a week or month shift the clock still follows the work (A1): your own jobs take their minutes; the days you don't touch are run by staff, and the calendar jumps to the next one where something needs you (today's `RestUntilNeeded`, reframed as "the week goes on without you"). A week ends when the week's list is done, then cash-up for the week, then after hours.

The change-over is a beat on the Clue Card (steps 17 and 26), never a silent switch.

---

## 8. More things on the planet (what the before/after hours are for)

The owner's point: once time is set by progress, the world can grow without crowding the day. A catalogue to draw from, in rough act order:

- **The pub** (Main Street): gossip names a household that wants a line; darts with Jory (morale).
- **Sol's back room**: trade-in parts, a cheap secondhand till, rumours of NezTel price changes.
- **The council chamber**: permits for poles, the parish council's grumbles (the Church Lane beat).
- **Night survey**: drive a road with a torch, mark where poles could go (D20).
- **The dock at Saltmarsh Quay** (D19): a boat, then the sea.
- **Hill walks**: find mast sites by looking (line of sight from the top).
- **Lost-and-found odd jobs**: a radio in a barn, a phone in the church tower.
- **Night calls**: an emergency fault at 23:00 that pays double and costs sleep.
- **The Almanac nook**: a chair and a lamp where recalling a page happens.
- **Nadir**: the expedition to the antipode (Nez, D35).
- **Market day** (a Saturday event): the counter is busy all day; a good one is reputation.

Each is a place with a door and a reason, added as data where possible.

---

## 9. What changes in what's built

| System | Today | With shop days |
|---|---|---|
| D23 time | A day is 8 real minutes; the clock runs | Replaced: the clock moves by work while open, frozen before/after; holding E unchanged |
| `GameState.Minute` | Runs with real time | Moves only by action costs; new `GameState.Phase` (BeforeOpen, Open, CashUp, AfterHours) |
| `Simulation.AdvanceMinutes` | Called every frame | Called by actions (hold completes, travel arrives) |
| D24 who is home | By clock hour | Unchanged (the clock still has hours) |
| `SleepRules` | Waking day, sleep debt | Kept, gentler: only after-hours time counts |
| `RestUntilNeeded` | Bed skips quiet days | Becomes the week/month shift (section 7) |
| `BoardRules.Evening` | 18:00–23:00 | After hours, unchanged |
| Bookings (D37) | Days on the calendar | Today's bookings are the day's list; the day can't close until they're done or rolled |
| `PlanetSky` | Sun from real-time spin | Sun from `GameState.Minute` and `Phase` |
| `Guidance.Next` | The "what next" line | Feeds the Clue Card's Answer level; the line stays on the phone |
| Script player | Plays by real hours | Counts **shifts** and real hours; the 20–30 h target is measured the same way |
| Nez (spine step 8) | Weekly turns | Nez's shift is the same shape (Nez opens too) |

The shop's flip-the-sign, the close-up and the cash-up are sim actions in `Act/Actions`, so the bot, staff and Nez can do them (spine N-rules).

---

## 10. Build order

Each is one dev session. SD1 and SD2 are the foundation; nothing visual is needed for them to matter.

1. **SD1 The day has phases.** Sim: `GameState.Phase`, `DayRules` (Open, Close, CashUp, ToBed); the clock moves only by action costs while open and is frozen before/after; save bump. Game: stop feeding real time into the clock; the sign on the room door; the phone shows the phase. Script player and smoke pass.
2. **SD2 The sky follows the clock.** `PlanetSky` places the sun from `Minute`; dawn/dusk/night by phase.
3. **SD3 The Clue Card.** `ClueRules` reading `plot.json` + `projects.json` (clue, nudge, answer per chapter; days-stuck counter derived from the save); a phone page and the corkboard card. Clues for the existing chapters written as data.
4. **SD4 Today's list.** The day's bookings become the list; "close up" when it's done; close early rolls the rest; walk-ins.
5. **SD5 The counter, paper tier.** Counter visits (pay a bill, buy kit, sign up, report a fault) at a biscuit tin and receipt book; cash-up compares tin to book.
6. **SD6 1 Main Street.** The shop moves at the Act 0 exit; a mechanical cash register with the ring-up / change verbs and the soft queue.
7. **SD7 The week shift.** At the depot, the shift becomes a week; the week planner.
8. **SD8 The shop assistant.** `money.counter` hand-off at 80–90%; mistakes at cash-up.
9. **SD9 After-hours places**: the pub and the council chamber first.
10. **SD10 The month shift and the second shop.**

---

## 11. Questions for the owner

1. **Forks A–C** above: taken as recommended (clock follows the work; Clue Card on phone and corkboard; the shift grows day → week → month). Change any?
2. **Sundays**: shop shut (a free after-hours day, good for projects), or every day a shop day?
3. **Closing early**: allowed, with the cost of rolled jobs (recommended), or only when the list is done?
4. **Hints**: wait 3 opened days before the nudge, or show nudges at once and keep only the answer back?
5. **The counter**: should the till be in the very first hour (Act 0 tin), or held back until 1 Main Street (Act 1)?
