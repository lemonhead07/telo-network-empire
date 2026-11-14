# Next Session — start here

*Left off: 2026-11-14. Branch: `feature/gameplay-rapid` (the same commit is also on `claude/charming-allen-6djprx`). Everything is pushed; the working tree is clean. 64 sim tests and the smoke test pass.*

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
