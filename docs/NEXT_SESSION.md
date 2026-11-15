# Next Session — start here

*Left off: 2026-11-15. Branches: `feature/gameplay-rapid` has everything up to the bookings design; `claude/zen-keller-e0i2s3` = that plus the bookings build below (merge it into `feature/gameplay-rapid` when the owner is happy). `master`/`main` are behind both. 72 sim tests and the smoke test pass. Saves are at v12.*

## Latest: bookings and the calendar (D37, Up next item 1) is built

- **Every new line and catalogue order is a booking** with the day the customer wants it (1–4 days out, seeded). Ticket slips on the rail show "wanted Sat 8" or "LATE".
- **Three jobs a day by hand** (installs, orders, odd jobs; repairs are never refused). A fourth door says "That's a full day"; the "next" line says text PIP goodnight. Staff do their `jobsPerDay` (content check: 1 to 5).
- **The calendar can overflow.** `BookingRules.Late` counts bookings that won't be done by their day. Three days past it, the customer signs with the national phone monopoly and the house is theirs (not a Promise Ledger home, not a job already started or handed to Jory).
- **Ads: the ad board by the front door** (`room.ad-board`, brief in `docs/props/room.json`). E pins a leaflet ($6 a week, up to 4), R takes one down. Each brings more calls. Word of mouth now scales with reputation (average satisfaction).
- **The loop works on the bot:** the script player pins leaflets when the calendar has room, then hires Jory *because* the calendar overflows (the test checks the reason). The "next" line no longer says hire just because you can afford it.
- **The phone** shows "This week: N booked, room for M" and "jobs today 1/3". PIP has new lines (`leaflet`, `fullday`, `late`).
- Measured: with no leaflets, one person keeps up with the whole 15-lot town, so the hire only comes once leaflets push the calls up. Leaflet strength (450‰ a day each) is a first guess.

**Next:** `BUILD_PLAN.md` → Up next item 2, *Service in nines and VIP jobs* (D38, D39). Ask the owner B1–B3 in `BOOKINGS_AND_NEZTEL.md` §6 when convenient (B1 was taken as "reputation on top of satisfaction").

## Before that: the spine, first slice of all eight steps

| Step | What exists now | Where |
|---|---|---|
| 1 Actors and actions | An actor has a place, hands, kit and a van; `Actions.For` lists what it can do and what stops the rest | `src/Telo.Sim/Act/` |
| 2 Script player | A bot reads the "next" line and plays a fresh game to the farm line in a test, no cheats | `Act/Bot.cs`, `tests/.../ScriptPlayerTests.cs` |
| 3 Plot as data | Eight chapters in one line; the first-hour steps are read from it | `game/content/plot.json`, `Town/Onboarding.cs` (`Plot`) |
| 4 The phone | PIP in your hand: clock, money, next line, today's text, Nez's par; the status strip and bubbles are gone; bed sends the night text and saves | `UI/PhoneView.cs`, `Phone/PhoneRules.cs` |
| 5 Day planner | A timesheet on the wall: E fills tomorrow with what fits (travel, who's home); the phone shows the next job | `Plan/PlanRules.cs`, `DeskWall.cs` (`Timesheet`) |
| 6 Assemblies | Odd jobs are generated from parts and fasteners, five steps or fewer, one seeded fault | `game/content/assemblies.json`, `Minigames/Assembly/` |
| 7 Walking player | `godot --path game -- --autoplay[=photo dir]` plays the first hour in the world; it finished it (125 photos, 15 assists) | `game/scripts/Autoplay.cs` |
| 8 Nez | Notices you at 10 subscribers (a van in your street), competes at 12, takes one house a week from 15 (never the last house on the road); par times; Nadir at the antipode | `Nez/NezRules.cs`, `game/scripts/NezView.cs` |

**Owner decisions taken provisionally (D34):** N1–N8 as the spine doc recommended. Ask the owner to confirm after they play.

**Measured (N8):** alone, about ten installs fit in an 8-minute day (07:00–22:00 at 72 min each plus travel). The bot reaches the farm line in under three game years.

**Next:** `BUILD_PLAN.md` → Up next (the rest of each step, then props, models and twists).

## Read these, in this order

1. **`docs/NEZ_AND_THE_SPINE.md`**: the newest direction. Thinking only; nothing in it is built. Its section 9 has eight questions (N1–N8) for the owner. **Ask the owner for their answers before building from it.**
2. `CLAUDE.md`: how every built system works.
3. `docs/DECISIONS.md`: D1–D29.

## What the owner wants next (their words, 2026-11-14)

- A branch for **rapid gameplay development**. Modelling and animation later.
- The game **fully playable by an in-game character, programmatically**, who can do every action the player can. This character is **Nez**, the opponent to beat: starts on the far side of the planet, already owning the infrastructure. An overthrow-the-monopoly story.
- That same ability is the **playtest**: play every job in a test.
- **Mechanically simple, theoretically complex:** few verbs, many jobs, assembling and taking apart many kinds of simple things.
- **All text on a phone** you can read while walking. "Light hardcore."
- **One plot, one trajectory, no overlap.** A first five minutes that hints at the depth and shows the player round the room.
- **Time travel: one text a day.** Texting other people only later in the game.
- **The day cycle as real gameplay:** a timesheet beside the job board for upcoming jobs; plan a day, then weeks, then months.

## The build order proposed (section 8 of the new document)

1. **Actors and actions in the simulation.** Place, hands, vehicle, travel time move into `src/Telo.Sim`. One question for anyone: "what can this actor do now, and what is stopping the rest?" `GameRoot` (1,200 lines, much of it player-only logic such as `LotAction`, `StartInstall`, `StartBenchJob`, `KitOnSite`) shrinks to showing and forwarding.
2. **The script player:** a test that plays today's game from the first message to the farm line by choosing actions. No cheats (`GodNextStage` is not this).
3. **The plot as a data file,** replacing `Onboarding`'s hand-written steps.
4. **The phone** (PIP in your hand), removing the HUD strip and speech bubbles; the first five minutes.
5. **The day planner** beside the ticket rail.
6. **Assemblies:** parts, fasteners, connections as data; odd jobs first.
7. **The walking player** (`--autoplay`).
8. **Nez.**

Steps 1 and 2 are the foundation. Do not skip them.

## What is built (all on this branch)

The first hour, staged one thing at a time; walk-in houses and the hands-on install; van kit; the switchboard as a real object; the Board with lines in, engaged tones and line cards; the Board's terminal (callers, file area, night mail); the catalogue of orders; Jory Pike and job tickets handed over in person; pay day and the wall calendar; the depot; who is home by time of day; lived time with hold-to-work; faults and repairs; the pole line to Hollow Farm; early act start through the Almanac; god keys.

## Still open from earlier plans (lower priority than the list above)

- Things to ask the owner after they play: day length (8 real minutes), hold times, whether handing Jory tickets each morning is fun, half the town being out nine to five, whether the switchboard is comfortable.
- `docs/DESK_AND_DAY_DESIGN.md` backlog: training staff, each person's own visible place, appointments, night rate, light tiredness, folding the lines-in rack into the switchboard.
- `docs/EXPANSION_DESIGN.md`: phase D (invoices, late payers, bookkeeper, receptionist) and the places beyond the farm.
- `docs/ASSET_LIST.md`: all of it.

## New since 2026-11-15
- Sessions are split into **idea** and **dev**: see `docs/SESSIONS.md`. In Claude Code start with `/idea <topic>` or `/dev`.
- `docs/TECH_TREE.md` is the progression design (8 branches, 96 tasks, the hand-off ladder). It is design input, not the next build: it fits on top of the actors-and-actions foundation above (its §0 says how). Build order is in `BUILD_PLAN.md` → **Up next**.

- **Model requests and job variety** (idea session, 2026-11-15): `docs/MODELS_AND_VARIETY.md`, decisions D30–D33. Every visible thing becomes a named prop with a brief in one list (`props.json`), so models can replace boxes with no code change. Jobs stay simple (8 verbs, ≤5 steps, a twist adds one step). The switchboard's 13 briefs are written (`docs/props/switchboard.json`). New Up-next items 4–6 (props by name, switchboard models, install twists) come after spine steps 1–2. Every new build item now lists its props (Definition of Ready).

## Rules to keep

- Outcomes are decided in `src/Telo.Sim`; Godot only shows and drives it.
- Time is lived, not spent: never jump the clock for work.
- Develop lots, test little: compile and screenshot; full tests only before a commit that changes sim rules.
- Everything new ships as a labelled box first.

## Known rough edges

- Kit you put down, and odd jobs or orders in progress, are forgotten on save and load.
- Repair is a single hold at the door.
- Jory is the only visible member of staff; Tobi is not shown.
- The shop and farm are solid boxes; buying a line card is `R` at Sol's counter.
- PIP's bubble can cover the bottom of the switchboard.
- Floating name text on doors; `$`, `▼` and `!` markers show through walls.
- The HUD text strip is still an overlay.
