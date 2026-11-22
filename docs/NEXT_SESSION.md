# Next Session — start here

*Left off: 2026-11-21 (cloud dev session, DC1, branch `claude/awesome-gates-qdvx3n`, gameplay lane). 86 sim tests and `--smoke` pass. Saves are at v19. The owner wants **early access to cover Acts 0–5**, gameplay first, models later; Act 5 is now The Core, the data centre (D46).*

**Start here:** `BUILD_PLAN.md` → "Early access". DC1 is done; next up is **EA2b** (the Yard), then EA2c, EA3–EA6 and DC2–DC9 (DC2, the hall, no longer needs the site or road). Before committing a rules change, run `dotnet test Telo.slnx` (the script player is in it, now `The_bot_plays_from_the_first_message_through_act_5`) and `--smoke`. To watch the whole path: `godot --path game -- --autoplay --from=free --autoplay-fast --autoplay-teleport --autoplay-sample=15 --autoplay-speed=4 --autoplay-until=act6` (add `=/abs/dir` to `--autoplay` for photos and a log; in the cloud, `xvfb-run -a` in front). To start on Halden Flats: `godot --path game -- --from=act5`.

## Newest (2026-11-21): Act 5 is The Core (DC1)

- **Acts:** `act5-core` "The Core" is Act 5 (gate `ann-core` "Server farms", Sept 2006; Almanac page `alm-core` "The carrier hotel", 35 Archive, readable in Act 4). Mobile (`act5-mobile`, ids never change) is Act 6, Platform 7, Intelligent 8. `alm-smartphone` is now readable in Act 5 and needs `alm-core`. A v18 save in mobile or later moves up one act (v19 migration, tested).
- **Thirteen data-centre projects** (`projects.json`, design §6) as placeholder steps on today's click-and-hold mechanic: slab ($1,500, 5-day cure), hall (roller door on a lorry, 7 days), power (generator, 6 days), cold aisle, Meet-Me Room (+$300/month cross-connects), core rack, then bring home the valley, Eastgate (the first ring), migration night (ends the cold store's bill, +$600 colo), Millbrook and Farland (a leased second path from Nez), and light the board (+$1,500). Peering with NezTel is optional and listed after the finale. Monthly bills add up to $2,450. The masts moved to Act 6 and wait on `proj-dc-lit`.
- **An act can end on one named project**: a new condition kind, `{ "kind": "built", "id": "proj-dc-lit" }` (`ConditionKind.Built`, `Condition.Id`; `ContentDb.Validate` checks the id). Act 6 now ends on `proj-backhaul` + 10,000 subscribers.
- **Halden Flats** is a `PlanetGen` site (`halden`, radius 80, added last so nothing else moved) with a road from town, on the coastal flats between the town, Eastgate and the shore, 4 m above the sea. The Halden projects stand there; the homing projects stand at the far end of each link (beside the depot, the Eastgate cabinet, the Millbrook relay, Kestrel Point). Picture: `docs/img/halden-flats.png`; `--shot` saves `x-halden` (placeholder frames only; the hall is DC2).
- **Pace and money:** the script player now plays to the lit board. **Act 5 takes 5.0 real hours (2 Dec 1983 → 28 Feb 1984); the whole run is 26.8 h** (1.0 / 3.2 / 6.4 / 4.7 / 6.5 / 5.0). Cash: $13,691 entering Act 5, $6,750 entering Act 6 (new band $1,500–60,000, D41 amended), $261k a year into Act 6. The first cost guess ($70k) made Act 5 13 hours of saving up; at $23.5k the waits set the pace. Money still piles up afterwards: DC8's power and heat bills are the real sink (IDEAS inbox).
- **`--from=actN`** now also connects the households the earlier acts opened and hands the Board to Tobi, so the "next" line points at the act's own work; `--from=act5` parks you and the van on Halden by the empty plot.
- **Fixed on the way:** the plot chapters' project counts were stale after EA2f (the mobile chapter would have shown in Act 2); there is now a `ch-core` chapter.
- **Not done (later DC items):** the hall and rooms (DC2), the rack (DC3), the pin board (DC4), both fiddling games (DC5–6), the DC hires (DC7), colo and the load dial (DC8), migration night as a timed set piece and the finale (DC9).

## Also 2026-11-21 (a parallel cloud session, geometry lane): models for every age

The switchboard models (left on an unmerged branch since 11-19) are now in master; each village has two landmarks (school and green, pub and boatyard, chapel and weather station) and Eastgate a parade of shops; **every Data Centre prop in `DATA_CENTRE_DESIGN.md` §12 has a model under its id** (so DC2–DC9 get them through `Props.Make` for free); later ages are modelled ahead (PIP's handsets by era, monitors, laptop, ops wall, small cells, phone shop, rocket, pad, satellite, dish, drone); every in-world screen now draws its own pointer in its era's style; and `World/ScreenFaces` lights the `screen*` empties on any model (see the Showroom's new Data Centre and ops-room corners, `docs/img/showroom-dc-and-screens.png`). Details: `ASSET_LIST.md` top, `IDEAS.md` inbox. No rules changed.

## Newest (2026-11-21): three villages of hand work (EA2f)

- **Owner, mid-session:** "feel free to add more towns for hand work, I'm happy to fill out the world more."
- **Wrenfold** (Act 1, under the mountains, 514 m out), **Kettle Cross** (Act 1, on the dunes by the sea, 483 m) and **Highmoor** (Act 2, 1.2 km up the hill): 32 households in all. Pictures: `docs/img/village-*.png`; `--shot` now saves `x-wrenfold`, `x-kettle-cross`, `x-highmoor`.
- Each opens with a pole project (plant, string, a bureaucratic wait, the terminal box), then a smaller job there once enough people are on: Wrenfold's reading-room terminal, the Kettle Cross surgery's night line, Highmoor's weather-station data line. Plus **a second trunk cable to Eastgate** at the end of Act 1.
- **The switchboard grows with the villages:** each village project allows one more $120 panel (`panels` reward → `BoardState.ExtraPanels`), so 56 subscribers at most. The spare ports moved from 40–41 to 64–65 (v18 migration, tested).
- **Act exits:** Act 1 now needs 44 subscribers and 7 projects; then 13 / 16 / 19 / 22 projects for Acts 2–5. When DC1 shifts the acts, start from these numbers.
- **Pace: the Act 5 script player now takes 21.8 real hours (1.0 / 3.2 / 6.4 / 4.7 / 6.5 per act)**; the test's floor is now 20 h. Cash bands all held. Act 2 is a bit long (IDEAS inbox).
- **Why villages and not just more projects:** the hours are busy game days (3 game minutes a real second, so a 15-hour working day is 5 real minutes). A project's clicks add minutes; households add weeks of installs, orders, odd jobs and faults. The switchboard's 32 was the real ceiling on hand work.

## Decided 2026-11-21 (idea session): early access ends in a data centre

New Act 5, **The Core**: build a data centre on Halden Flats, plan the network on a pin board with string, patch it in at an easy rack, play crossed patches and follow the lights, then hire Dana, Moss and the night shift. Mobile moves to Act 6. Design: `DATA_CENTRE_DESIGN.md` (concept sheet `img/dc-concept.png`); decisions D46–D50; build items DC1–DC9 in `BUILD_PLAN.md`. **DC1 (the act swap, content only) is next.** EA2d (masts) is no longer early access.

## Branches (2026-11-19)

Everything (gameplay-rapid plus the town and models work) is merged into **`master`**. Two lanes now branch from it: **`feature/geometry`** (models, looks) and **`feature/gameplay`** (rules, systems, procedures). How to use them: `docs/BRANCHES.md`. `feature/gameplay-rapid` is the old single lane: merge what is left on it into master, then use `feature/gameplay`.

## Newest (2026-11-19, cloud, branch `claude/friendly-volta-0c3lcj`): the town modelled, and opened up

- **Town layout:** houses 20 m apart with gardens (lawns, fences, hedges, sheds, apple trees, washing lines), street lamps, a phone box, and patchwork fields with hedgerows round every settlement (`World/TownDressing.cs`). Church Lane at −40. The Act 5 script player's cash bands are on a knife-edge: 21 and 22 m spacing both failed it.
- **~45 models in `tools/models/town.py`:** six houses (`house.a–f`, furnished front rooms), Sol's shop, the depot, Hollow Farm (with tractor), the outside of your room plus its desk, answering machine, camp bed, terminal, PIP's handset, notebook and clutter; Jory, Tobi, Sol and eight residents (one per archetype; they stand in their front room while home); four cars, Nez's van, Nadir; oak, pine, rock, cactus, apple tree, street lamp; the council barrier with its worker. See `docs/img/props/lineup-buildings.png` and `lineup-town-life.png`.
- **Not done:** the switchboard and line rack (BUILD_PLAN 11/12, left to avoid clashing with the model branch), the wall boards (tickets, calendar, timesheet, ads), Eastgate shops, animation.

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
