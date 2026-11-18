# Next Session — start here

*Left off: 2026-11-18 (cloud dev session, EA2e). Branch: `feature/gameplay-rapid`. 82 sim tests and `--smoke` pass. Saves are at v17. The owner wants **early access to cover Acts 0–5**, gameplay first, models later.*

**Start here:** `BUILD_PLAN.md` → "Early access: Acts 0–5". Next up: **EA2f** (Acts 1–2 ask for more), then EA2b (the Yard), EA2c, EA2d, EA3–EA6. Before committing a rules change, run `dotnet test tests/Telo.Sim.Tests` (the Act 5 script player is in it) and `--smoke`. To watch the whole path: `godot --path game -- --autoplay --from=free --autoplay-fast --autoplay-teleport --autoplay-sample=15 --autoplay-speed=4 --autoplay-until=act5` (add `=/abs/dir` to `--autoplay` for photos and a log; in the cloud, `xvfb-run -a` in front).

## Newest (2026-11-19): 28 models through Act 5

`tools/models/batch.py -- all` builds every model the player handles through Act 5 (van kit, install parts, the pole, 13 project sites) and keeps `props.json` in step; the game picks each up by prop id (BUILD_PLAN 11b). See them: `docs/img/props/lineup-handheld.png`, `lineup-sites.png`, or `godot --path game res://scenes/Showroom.tscn`. Next model batches: the switchboard (item 11 then 12), houses, residents.

## Side branch (2026-11-18): the model pipeline works`n`n`feature/model-pipeline` (off `feature/gameplay-rapid`) proves Blender-to-game end to end with the van: `game/content/props.json` (briefs; `PropCatalog` validates), `Props.Make(id, placeholder)` loads `game/assets/models/<id>.glb` or falls back to the box, `tools/models/_kit.py` + `tools/models/vehicle.van.py` build the model and preview renders (`docs/img/props/`). Blender 5.3 is installed locally (`C:\Program Files\Blender Foundation\Blender 5.3\blender.exe`). The van's wheels spin (`wheel-*` nodes). Next model work: BUILD_PLAN item 11 (switch the switchboard to `Props.Make`), then the asset list order. Merge this branch into `feature/gameplay-rapid` when the owner says.`n`n## Newest idea (2026-11-18): shop days

`docs/SHOP_DAYS.md` (D45, provisional): days open and close at the shop sign, the clock follows the work, before/after hours are the open world, a cash register later, a Clue Card for the line of progression. Build items SD1–SD3 in `BUILD_PLAN.md`, after EA2f. Ask the owner the five questions in its §11.

## Newest (2026-11-18): the pace is fixed (EA2e)

- **The Act 5 script player now takes 14.3 real hours** of awake play: 0.8 / 1.3 / 1.9 / 5.5 / 4.8 per act. The test fails if any act passes 8 h or the run leaves 12–30 h, and it prints where the hours went (top 15 lines followed). Set `TELO_BOTLOG=/abs/file` to write the bot's whole run to a file.
- **The old 122 h was wrong twice over:** the meter counted sleep as play (honestly 76 h), and the "what next" line (`Town/Guidance.cs`) wasted the rest. Fixed in the line: hand the Board to Tobi when you can (`invest:board`); the mail call only after 17:00; projects come before filler odd jobs and the hunt-group hint; "switchboard full" only when a panel can be added. Fixed in the bot (`Act/Bot.cs`): trips over 100 m go by van (`LongTripM`); kit the next job needs is taken from the van when you leave it. Fixed in actions: `install:sign` says "no boxed modem" instead of failing silently, and clears a job someone else already finished.
- **Money:** a project starts only when next month's fixed bills are still covered (`ProjectRules.CanAffordStart`). Broadband $2,500, fibre ring $4,500, ocean cable upkeep $400/month. D41's bands amended (Acts 3–4 begin just after paying for a project).
- **Left:** Acts 1–2 are short (EA2f); Act 5 now arrives in Dec 1983, so the calendar question is back (IDEAS inbox); a human is slower than the bot (measure once).

## Earlier (2026-11-16, a long local session): the path from the first message into Act 5

- **Projects** (`game/content/projects.json`, `src/Telo.Sim/Build/ProjectRules.cs`, world view `game/scripts/Projects.cs` + `World/ProjectSiteView.cs`): things built in the world, step by step with kit from the van, that open the next part of the game. Gated by act, earlier projects, an Almanac page, the depot, or size. Rewards: lines in, a service on every line, income, a bill (or ending one), or a **region**. 15 projects through Act 5 (Church Lane poles → Eastgate cabinet → modem bank → leased line → microwave hop → Millbrook → cold-store datacentre → Kestrel Point landing → ocean cable → broadband → fibre ring → Farland city → first cell mast → coast cell sites → fibre to every mast). Add more as data; `ContentDb.Validate()` checks requirements come earlier in the file.
- **Regions**: a project can open a whole town (Millbrook 150, Farland coast 260, Easthaven and the bay 700, Farland city 1000, mobile 1500 + 4000) whose subscribers sign over ~3 weeks, billed as a block. `GameState.TotalSubscribers` is what acts measure. The valley's ~37 households stay individual.
- **The "what next" line** points at the next project, or at what it waits on (the Almanac page to recall, the depot to lease). Every act gate has an Almanac page readable in the act before it, so no act waits years for a calendar date.
- **The script player plays from the first message into Act 5** (`ScriptPlayerTests.The_bot_plays_from_the_first_message_into_act_5`, ~6 s): Act 1 Jan 1983, Act 2 Feb 1983, Act 3 Jul 1984, Act 4 Oct 1984, Act 5 Feb 1985. If a gate ever breaks, this test says where it stuck and what the line said.
- Also this session: roads and driveways (`World/RoadNet.cs`), the waking day and sleep debt (`Town/SleepRules.cs`), appointments and the job clock (`Plan/AppointmentRules.cs`), service in nines and VIP jobs (`Town/ServiceRules.cs`), install twists (`InstallJob.Twists`), Church Lane and Eastgate (`TownBuilder.Streets`, `PlanetGen.FindVillageSite`), a 32-line switchboard.
- **Dev shortcuts** (`game/scripts/DevStart.cs`, `Autoplay.cs`): `--from=free|hire|poles|act1..act7` (act starts build the earlier acts' projects), `--autoplay-teleport`, `--autoplay-sample=X` (play one job in X in full, decree the rest), `--autoplay-until=actN`, `--autoplay-speed=N`.
- **Known and next:** the economy is generous by Act 4–5 (cash in the hundreds of thousands): tune. Historical years run fast (Act 5 by 1985) because every gate is recalled early: decide if that's the time-travel joke or needs Echo to bite harder. Project sites are placeholder boxes with a signboard. The far towns (Millbrook, Farland) are only regions: places to visit come with world-building.

## What just happened: the spine, first slice of all eight steps

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

**Since then (same day):** the booking loop (D37) is in: `Town/BookingRules.cs`, the ad board on the room's right wall (E more ads, R fewer), three jobs a day for you, a three-week wait sends the customer to NezTel, the phone shows booked vs. can-do. Saves are at v13.

**Next:** `BUILD_PLAN.md` → Up next (service in nines and VIP jobs) (the rest of each step, then props, models and twists).

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
