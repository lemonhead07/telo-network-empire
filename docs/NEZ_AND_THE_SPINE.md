# Nez, the Spine, the Phone and the Planner

**Status: thinking only, written 2026-11-14 at the owner's request. Nothing in this document is built.** The owner asked for deep thought, not work, before starting a fresh session on a rapid-gameplay branch. Section 9 lists what the owner still has to decide. Section 8 is the build order a fresh session should follow once they have.

## The idea in the owner's words

- A character in the game, **Nez**, who can do every action the player can, and does them by program. Nez is **the character to beat**: the near-unstoppable opponent.
- Nez starts at the same kind of spot as the player, **on the other side of the planet**, but not from the bottom: **Nez already owns the infrastructure.** The story is overthrowing the monopoly.
- Nez is also how we **playtest**: if a program can play the whole game, we can test all of it and reason about it.
- The game should be **mechanically simple and theoretically complex**: a few verbs, many jobs, assembling and taking apart many kinds of simple things (radios, computers, telephones).
- Because everything is modelled in the world, **all text lives on a phone** you can read while walking. "Light hardcore."
- Every job and level strings along **one plot, one trajectory**, with no overlap, so the player is never confused.
- **The first five minutes** spawn the player in, hint at the depth, then show them round the room with well-timed, readable, scrollable text that feels natural.
- It is time travel, so: **you can only text one day at a time**, and you can only text other people later in the game. Until then a roster is made some other way.
- **The day cycle becomes a real part of play:** a calendar or timesheet beside the job board for *upcoming* jobs, so you prepare a full day at a time, then weeks, then months as the business grows.
- Models and animation come later. This branch is for gameplay.

## 1. The one idea underneath all of it

All of these are the same request: **the game must be a list of actions that anyone can perform.**

If every thing the player can do is a named action with a place, a duration, the kit it needs and a result, then:

- a **test** can perform them in order and prove the whole game can be finished;
- **Nez** can perform them as an opponent;
- **staff** can perform them when handed a ticket (they already do, for one action);
- the **phone** can list them ("today: three jobs");
- the **planner** can lay them out on a day;
- the **plot** is simply the order in which each action is first asked for.

Today that is only half true. The rules are in the simulation (good), but a lot of "what can I do here, do I have the kit, is anyone home, how long does it take" lives in the game's main script, written once for the human player. Nobody else can use it. **Moving that into the simulation as a proper action list is the piece of engineering everything else stands on.**

## 2. Actions, and anyone who can do them

### An actor

Someone who can do things. The player, Nez, Jory, later anyone. In the simulation an actor has:

- **a place** on the planet (which building, or where on the road);
- **hands** (what kit they are carrying) and a **vehicle** (what is on it);
- **what they know how to do** (the tickets idea from the staff design);
- **their own day** (when they started, how tired they are).

### An action

One thing an actor can do. Every action answers the same five questions:

| Question | Example: clip the drop wire |
|---|---|
| Where? | At Dot Brennan's front wall |
| What do you need? | The cable drum; the ladder for the top clip |
| When is it possible? | When Dot is home and the step before is done |
| How long does it take? | 8 minutes of the day |
| What happens? | One clip on; or, bodged, a crooked one and a fault later |

The simulation would offer one question to anyone: **"what can this actor do right now, and what is stopping the rest?"** Objects in the world, the phone, the planner and the bots all ask that same question.

### Three ways to perform an action

| Who | How | What it proves |
|---|---|---|
| **The script player** (a test) | Performs actions directly in the simulation, thousands of days a second | The rules allow the game to be finished; how long each chapter takes; whether the money works |
| **The walking player** (in the game) | A character that really walks, drives, carries, looks at the object and holds E | The world allows it: the door opens, the kit is reachable, the marker can be seen |
| **The human** | The same objects, by hand | That it is fun |

The script player is the "play the game in a test" the owner asked for. The walking player is Nez's body, and also films the trailer. They share one brain: a planner that picks the next action.

### What has to move into the simulation first

| Today in the game's main script | Why it must move |
|---|---|
| What is in your hands and what is lying within reach | An action needs kit; a bot has hands too |
| Where you are, and how long it takes to get somewhere | A plan for the day needs travel time; Nez's reach depends on distance |
| What a door offers right now | The single biggest block of player-only logic |
| Starting, continuing and finishing an install, an odd job, an order | So anyone can do them |
| Buying at Sol's | An action like any other |

This is a rewrite of how the game talks to the simulation, not of the rules. It is the main cost of the whole idea, and it pays for every later feature.

## 3. Nez

### Who Nez is

Nez **is** the national phone monopoly the game has hinted at since the first screenshot: the poles in your street were there before you. Give it a face and an address. Nez works from an exchange at the exact opposite point of the planet, half a lap away, and owns the lines everywhere, including the ones over your head.

You are one person in a rented room. Nez has every pole, every exchange and a hundred staff. You start from nothing and grow outward; Nez starts with everything and has to hold it.

### How Nez plays

Nez uses **the same actions as the player**, through the same planner, which is what makes it a fair opponent and an honest test. What differs is the starting position and the temperament:

| | You | Nez |
|---|---|---|
| Starts with | A room, a van, $400 | Infrastructure across the planet, staff, money |
| Speed | As fast as your hands | Slow: committees, forms, a waiting list |
| Quality | Up to you; you can bodge | Never bodges. Every job by the book |
| Prices | Whatever you dare | High, because it can |
| Reach | The next street | Everywhere, eventually |
| Weak spot | One person, one day | Does not care about the last house on the road |

**Nez is "unstoppable" because it never makes a mistake and never runs out of money. It is beatable because it is slow and indifferent.** That is also the game's theme: the Promise Ledger people are exactly the ones Nez decided were not worth a pole.

### Nez as the measure

Every job can carry **Nez's time**: how long the by-the-book version takes. That gives the player a par to beat on every task, and gives us a number for free: if a human cannot beat Nez's time on a job, the job is too fiddly.

### When the player meets Nez

Not in the first hour. The plot (section 5) introduces Nez in stages:

1. **Background:** the poles, the name on the phone bill, the leaflet through a customer's door.
2. **Notice:** when you connect your tenth subscriber, a Nez van parks in your street.
3. **Competition:** orders you leave too long go to Nez (this rule already exists, credited to "the national phone monopoly").
4. **Territory:** Nez starts connecting the houses you have not reached. A map shows two colours.
5. **Contact:** you drive half a lap and see the exchange.

### Open story question

The canon has an ending called *The Loop*, where the player turns out to be the founder whose disappearance they caused. The old project folder was called NetworkNez. Whether **Nez is who the player becomes** is the owner's call; the mechanics above work either way.

## 4. Mechanically simple, theoretically complex

### Seven verbs

Everything in the game should be one of these, done to an object in the world:

| Verb | Means | Already in the game as |
|---|---|---|
| **Take / put** | Pick a thing up, put it down, hand it over | Kit from the van; job tickets |
| **Carry** | Move it somewhere, on foot or in the van | Ladder, drum, modem |
| **Fasten / undo** | Hold to screw, clip, staple, bolt | Install steps, poles |
| **Connect / pull** | Run a wire or cable between two points | Jumpers on the switchboard |
| **Test** | Probe, listen, check a lamp | Tone probe, test set, dial tone |
| **Switch** | Throw it, turn it on, send it | Test switch, the night mail |
| **Say** | Knock, ask, promise, hand over a ticket | Doors, Jory |

No new verb should be added without a strong reason. Complexity comes from what the verbs are applied to.

### Things are assemblies

A radio, a telephone, a modem, a junction box, a computer, a switchboard, a pole: each is a small **assembly**: a list of parts, what fastens them, and what connects to what. All described in data files.

- **A fault** is one bad part or one loose connection somewhere inside.
- **A repair** is: undo fasteners until you reach it, test to find it, swap or re-seat it, do everything up again, test.
- **An install** is building an assembly that is not there yet.
- **A bodge** is skipping a fastener or forcing a part. It is visible (crooked, a screw missing) and it compounds, as now.

One system then covers odd jobs, installs, catalogue orders, the switchboard and pole work, which today are four separate pieces of code. A new kind of radio is a data file, not programming. And a bot can solve any assembly by the same method, so every one of them is tested automatically.

Each assembly starts as labelled boxes. When models are made later, each part is a named piece of the model, so the same data drives the real thing.

## 5. One plot, one line

### The rule

**The game is a single ordered list of chapters. Each chapter asks for exactly one new thing. Until it is done, the next chapter does not exist.**

Things already learned keep happening in the background (customers still ring), but never as something new to learn. There is never a second arrow on the screen.

This is the existing "one thing at a time" first hour, made into the whole game and moved into a data file: for each chapter, what must be true to start it, what it teaches, what the phone says, and what counts as done. The F4 god key already walks this kind of list; it would walk the real one.

### Why this is also the test

The script player plays the chapters in order. That one test proves: the plot can be completed, no chapter needs something a later chapter unlocks, and how long each chapter takes. If someone adds a job that breaks the line, the test fails.

### The chapters that exist today, as one line

1. Wake up. The phone.
2. The room.
3. The answering machine: your first customer.
4. The van and the kit.
5. The install.
6. The switchboard.
7. First money: an odd job.
8. Knocking on doors.
9. The Board fills up: a line card.
10. Hiring Jory: tickets.
11. Planning a day (new, section 7).
12. The depot.
13. The Board's terminal: callers, files.
14. The catalogue: orders.
15. The road and the farm line.
16. Nez notices.

Everything after that comes from `EXPANSION_DESIGN.md`, in the same form.

## 6. The phone, and the first five minutes

### The phone is PIP

PIP is already a handset from 2035 sitting on the desk. It becomes **the phone in your pocket**, and the only place text ever appears.

- **Raise it** with a key. It comes up in your hand; the world keeps going; you can walk and read. Lower it and it is gone. Nothing else is ever drawn over the world.
- **Doing** is always an object. **Reading** is always the phone. That is the rule that reconciles "everything is an object" with "all text on a phone".

| On the phone | Replaces | Arrives |
|---|---|---|
| **Today's text** | PIP's speech bubbles, the "what next" line | First minute |
| **Notes** (the steps of the job in front of you) | The prompt under the crosshair | First job |
| **Clock and calendar** | The corner text | Chapter 2 |
| **Almanac** | The handset screen on the desk | When the desk opens |
| **Money** | The corner text | First payment |
| **Map** | The floating ▼ and `$` markers | First trip out of town |
| **Roster** | The paper job board | The mobile era |

### One text a day

The fiction: the phone can reach across time, barely. **One message arrives each morning, and you can send one each night.** That is the whole budget.

- **The morning text** is the plot: the hint for the day, a page of the Almanac, a warning. One screen, scrollable, readable in ten seconds.
- **The evening text** is your one message back. Early on there is nobody to send it to but the future, so it is your day's report (which is also the save). Later it is the one instruction you can give that day.
- **Texting other people comes late,** and for a good reason: there is no mobile network in 1983. **You have to build it.** When the mobile era arrives, texting your staff becomes possible because of what you built, and the paper job board becomes the roster on the phone.

Until then a roster is paper: tickets handed over in person, and the planner on the wall (section 7).

### The first five minutes

| Time | What happens | What the player learns |
|---|---|---|
| 0:00 | Dark room. A phone lights up on the floor beside the camp bed | There is a phone |
| 0:15 | Pick it up. One text: *"…signal. Year reads 1983. That can't be right. One message a day is all I can push through. Look around."* | One text a day; something is wrong with time |
| 0:45 | Look at the window: morning, a street. Text scrolls: *"You have a room, a van and $400. I have forty years of things that haven't happened yet."* | The stakes, and the depth, in two lines |
| 1:15 | The calendar on the wall is highlighted; look at it and the phone adds a line: today, rent day | The day and the calendar |
| 1:45 | The job board: empty. *"Jobs go here. There aren't any. Yet."* | Where work comes from |
| 2:15 | The switchboard: eight empty terminals. *"Every one of these is a person who can reach another person. You have none."* | What the game is about |
| 2:45 | The answering machine starts blinking | The first job |
| 3:00 | Play it: Dot Brennan wants a line | A named person |
| 3:30 | A ticket prints onto the job board | Jobs are paper |
| 4:00 | Out of the door: the street, the van, poles overhead with someone else's name on them | The world, and Nez |
| 5:00 | Driving to Dot's | The game has begun |

Each beat is triggered by the player looking at or touching the thing, never by a timer alone, so a slow reader is never rushed and a fast one never waits. The phone holds the text until it is read.

## 7. The day, planned

### The planner beside the job board

Today the wall has a rail of tickets and a calendar. Add the piece between them: **a timesheet for tomorrow.**

- **A column per person** (at first, only you). **Rows are the hours** from seven to ten.
- **You pin tickets into hours.** A ticket knows how long it takes, where it is, and when that household is home, so the sheet shows a ticket in grey where it will not work (nobody in) and travel time between two jobs as a gap.
- **A day is what fits.** That is the puzzle: an install is two hours, the Lindqvists are only in after five, Sol shuts at half past, the Board needs watching at seven.
- **Evening is for planning tomorrow; morning is for doing it.** The day ends when you send the evening text. That is the rhythm.
- **The plan is a suggestion to yourself, never a lock.** You can ignore it. The phone's Notes simply show the next ticket on your column.

### The same sheet grows

| Stage | The sheet | You are planning |
|---|---|---|
| Alone | One column, one day | Your own hours |
| First hire | Two columns, one day | Who does what tomorrow; handing tickets over |
| A depot | A week, a row per person | Days, not hours; staff fill their own hours |
| Several crews | A month wall-planner | Projects: a pole line, a village, a contract deadline |
| Later | It moves onto a terminal, then a dashboard | Someone else plans; you watch |

This is the game's central promise ("the job changes as you play") shown on one object: the same wall, zooming out.

### Why the bots matter here

A plan is a list of actions with times. The script player can fill a planner and run it, which tells us how many jobs fit in a day, and whether the day is too short or too long, without anyone having to play forty days by hand. Nez's "day" is the same sheet filled by rule.

## 8. Build order for a fresh session

Each step ends with the game playable and the checks passing.

| # | Step | What it is | Why in this order |
|---|---|---|---|
| 1 | **Actors and actions in the simulation** | Place, hands, vehicle and travel time move into the simulation. One question: "what can this actor do now, and what is stopping the rest?" Existing jobs are re-expressed as actions. The game's main script shrinks to showing and forwarding | Everything else needs it |
| 2 | **The script player** | A test that plays today's game from the first text to the farm line by choosing actions, with no cheats | Proves step 1; from now on every change is checked against a full playthrough |
| 3 | **The plot as a data file** | Chapters in order, replacing the hand-written first-hour steps; the script player follows it | Locks in "one line, no overlap" |
| 4 | **The phone** | PIP in your hand: today's text, notes, clock; the corner text and bubbles removed; the first five minutes | The plot needs somewhere to speak |
| 5 | **The day planner** | The timesheet beside the job board; evening plan, morning do; one text a day closes the day | Needs actions with durations and places |
| 6 | **Assemblies** | Parts, fasteners and connections as data; odd jobs converted first, then orders, installs | The "many jobs, few verbs" promise |
| 7 | **The walking player** | A character that performs the script player's choices in the world; an `--autoplay` run | Proves the world, not just the rules; becomes Nez's body |
| 8 | **Nez** | The opposite exchange, Nez's lines, par times, the first four stages of meeting Nez | Needs everything above |

Steps 1 and 2 are the foundation and should not be skipped or reordered. Steps 4, 5 and 6 can be done in any order after 3.

**What this replaces.** `EXPANSION_DESIGN.md` (places, rounds) and `DESK_AND_DAY_DESIGN.md` (objects, lived time, staff) still stand as content and direction. This document changes *how* they are built: on actions, along one plot, with text on a phone. Where they conflict, this one is newer:

- "Desk objects that replace the text strip" becomes "the phone replaces the text strip".
- "A job board with a row per person" becomes the planner in section 7.
- PIP's speech bubbles and the pager become texts.
- Phase D onward of the expansion plan waits until step 3 here.

## 9. Decisions for the owner

| # | Question | My recommendation |
|---|---|---|
| N1 | Is Nez the monopoly given a face, based at the opposite point of the planet? | Yes |
| N2 | Is Nez secretly who the player becomes (the canon's *Loop*)? | Decide later; build so that it could be |
| N3 | Nez never bodges and never goes broke, but is slow and ignores the last house on the road. Right temperament? | Yes |
| N4 | One text in each morning, one out each night; sending the night text ends the day and saves. | Yes |
| N5 | Does the phone replace **all** on-screen text, including the small prompt under the crosshair? | Yes, with a faint highlight on the object you are looking at so you know it can be used |
| N6 | Is the plan for the day only a suggestion, or does the day refuse work that was not planned? | Suggestion only |
| N7 | May the walking player be visible in the normal game from the start (as Nez's staff in your street), or only once Nez "notices" you? | Only once Nez notices |
| N8 | Day length. Planning makes each day denser; 8 real minutes may be too short. | Try 12, measured by the script player |

## 10. Risks, plainly

- **Step 1 is a large rewrite with nothing new to see at the end of it.** It is the right thing to do and it will feel like a week of standing still. The script player (step 2) is the reward: after it, changes stop breaking things silently.
- **A perfect opponent is not fun by default.** Nez must be slow and indifferent, or the player simply loses. The par times will need tuning by play.
- **A phone can become a menu by another name.** The guard is the rule in section 6: it is for reading; every decision is still made by moving a thing.
- **One plot line can feel like a corridor.** The world stays open to walk and drive; only *new things to learn* are in a line. Jobs already learned come in any order.
- **This branch will drift from the art plan.** That is the owner's intent: gameplay first, models and animation after.
