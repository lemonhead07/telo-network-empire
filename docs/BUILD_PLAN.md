# Build Plan

> **2026-11-12:** the order of work after M0.6 is now set by `docs/EXPANSION_DESIGN.md` (rounds and phases A–J). The milestones below are kept for history and for the release checkpoints (playtest, demo, Early Access).

Each milestone ends playable, with green tests and `SMOKE OK`. Week counts assume one part-time developer working with AI help.

## Up next
*Dev sessions take the top **[Dev]** item (see `docs/SESSIONS.md`). Idea sessions take **[Idea]** items and add new ones, written to the Definition of Ready.*

**Spine built 2026-11-15, first slice of each step (all eight):** actors and actions in the sim (`Act/`), the script player (test plays to the farm line), the plot as data (`plot.json`), the phone, the day planner (timesheet), assemblies (odd jobs from `assemblies.json`), the walking player (`--autoplay`), Nez (stages 1–4, par times, Nadir). Saves are at v11. N1–N8 taken as recommended, provisionally (D34).

### Early access: Acts 0–5 (the owner's goal, set 2026-11-16). Take these first, in order.
*State on 2026-11-16: the whole path plays. 82 sim tests and `--smoke` pass. The script player reaches Act 5 by Feb 1985; fast autoplay reaches Act 5 in ~25 real minutes with 0 assists. See `NEXT_SESSION.md` → "Newest". Owner's words: "bulk dev please, then models later"; "make sure the game timeline gameplay makes sense and flows from task to task having everything you need perfectly gated behind something else".*

EA1. ✅ **Economy pass, Acts 2–5**, done 2026-11-16 (D41): towns pay a margin (`regionCost`), infrastructure has monthly bills; cash at each act start is held in a band by the Act 5 script-player test. *Left:* money still piles up after Act 5 (nothing to buy); crews (EA4) and Act 6 are the sinks. Also: `src/Telo.Sim/Build/ProjectRules.cs` was rebuilt in the cloud because the local session never committed it; if the original turns up, compare and keep the better.
EA2. **[Idea] Pace across the acts.** *Goal:* decide how many game years (and real hours) each act should take. Today every gate is recalled early, so Act 5 arrives in 1985 (the history says 2007). Options: Echo bites harder the earlier you recall; act exits ask for more; or keep it as the time-travel joke. *Done when:* the owner has chosen; numbers are in `DECISIONS.md`.
EA3. **[Dev] Places for the far towns.** *Goal:* Millbrook (Act 2) and the Farland coast (Act 3) become places you can drive (or sail) to: a `Site` in `PlanetGen`, a street in `TownBuilder.Streets`, a handful of named lots, and the project sites moved there. Regions keep counting the bulk of each town. *Done when:* a `--shot` of Millbrook; the Act 5 script player still passes.
EA4. **[Dev] Hand projects to crews.** *Goal:* AGES.md's "Handed to" column: after your first project of a kind, a line crew / rigger / DC team can be hired (work.json stage) and does project steps themselves, slower and 80–90% right, with defects that surface later. *Done when:* a test: a crew finishes a project step overnight; a crew mistake becomes a fault.
EA5. **[Dev] Project variety in the world.** *Goal:* each project's steps show as labelled placeholder props at the site (a pole, a cabinet, a rack, a dish, a mast) instead of one growing box; waits show as a sign ("Engineer booked until 12 Mar"). *Done when:* `--shot` pictures of three projects mid-build. *Not in scope:* models (later, D15).
EA6. **[Dev] Early-access shell.** *Goal:* what a player needs that a developer doesn't: a title screen with New / Continue, a settings page (mouse, volume, the day's real length), save slots, and a short "how to play" on PIP's phone. *Done when:* a fresh install can be played without the command line.

### Earlier list (some done this session)
1. ✅ **Bookings and the calendar** (D37), done 2026-11-15: word of mouth (from reputation) and ads (the ad board in your room: leaflets → paper → radio) bring bookings; you start 3 jobs a day, staff at most 5; a customer left waiting three weeks signs with NezTel. The phone shows booked vs. what you can do; save v13. *Left for later:* mastery raising your cap; installs counting as two slots (B2); bookings with a wanted date on the timesheet.
2. ✅ **Service in nines and VIP jobs** (D38, D39), done 2026-11-16 (`Town/ServiceRules.cs`). *Goal:* your network's service level shows on the phone in nines beside NezTel's 99%; VIP bookings pay a bonus and reputation when done on the day, unbodged and tested; customers start switching from NezTel when you beat it by a nine. *Done when:* a test: a VIP done perfectly pays and raises reputation; one bodged goes to NezTel. *Not in scope:* era standards beyond Act 1.
3. **[Dev] The name NezTel** (D36). *Goal:* "the national phone monopoly" in content and messages becomes NezTel, with the motto on its van and at Nadir. *Not in scope:* the reveal that Nez is you (Act 7).
4. **[Idea] Confirm or change D34** (the spine answers N1–N8 taken as recommended). *Done when:* the owner has played the phone and the planner and said yes or changed them. *Not in scope:* code.
5. **[Dev] Doors through Actions** (spine step 1, the rest). *Goal:* `GameRoot.LotAction` asks `Actions.For` instead of repeating the rules, so the player, the bot and Nez share one list. *Done when:* LotAction is a few lines; smoke and script player pass. *Not in scope:* new verbs.
6. **[Done 2026-11-15] Fewer autoplay assists** (step 7): `assists=0` for the first hour. Fixed by roads and driveways (`World/RoadNet.cs`, the van keeps to the street and parks on drives), kit put down in the work area of the next step, and `InstallSite.SpotFor` pointing at the next click, not the first. Left: a few kit pick-ups pressed without the crosshair on them. Also added the same day: the waking day and sleep debt (`Town/SleepRules.cs`, saves v12). Original goal: *Goal:* the walking player finishes the first hour with no assists (15 today: doors, obstacles, a missed crosshair). *Where:* `game/scripts/Autoplay.cs` (paths round houses, aim at small things). *Done when:* `--autoplay` prints `assists=0`.
7. **[Dev] The prompt moves to the phone** (step 4, N5). *Goal:* no text under the crosshair; the thing you look at glows faintly and the phone says what E does. *Props:* `ui.highlight` (outline). *Not in scope:* the bubbles (already gone).
8. **[Dev] Pin tickets by hand** (step 5). *Goal:* take a ticket off the rail and pin it into an hour on the timesheet; staff get their own column after the first hire. *Not in scope:* the week and month sheets.
9. **[Dev] Installs and orders as assemblies** (step 6). *Goal:* the house install and catalogue orders read from `assemblies.json` like odd jobs; repairs become "find the bad part". *Not in scope:* the switchboard.
10. **[Dev] Nez, contact** (step 8, stage 5). *Goal:* drive or fly half a lap to Nadir; Nez's actor plays its weekly connection through `Actions` like the player, at its par times. *Not in scope:* rivals beyond Nez.
11. **[Dev] Props by name** (D30, D32). *Goal:* every switchboard part comes from a named prop, so a model can replace it with no code change. *Where:* new `game/content/props.json` (copy `props` from `docs/props/switchboard.json`); prop records and validation in `Content/` (unknown ids and bad sizes caught by `ContentDb.Validate()`); a `Props.Make(id)` helper in `game/scripts/World/` that loads `game/assets/models/<id>.glb` if present, else builds the labelled box from the brief's size and first colour; `SwitchboardStation.cs`, `LineRack.cs` and the line card in `Cargo.cs` switch to it. *Done when:* the room screenshot looks the same as before; dropping any test `.glb` named after a slot replaces that box; one test validates `props.json`. *Props:* the 13 switchboard slots (briefs written). *Not in scope:* making the models; migrating other scripts (each migrates when next touched).
12. **[Dev] Switchboard models, first pass** (D33). *Goal:* the switchboard and its parts are real low-poly models. *Where:* one Blender script per slot in `tools/models/<id>.py` → `game/assets/models/<id>.glb`; a contact sheet PNG of each prop's three takes (plain / old / bodge) for the owner to pick from; `status` in `props.json` moves to `modelled`. *Done when:* the room screenshot shows the models in place, anchors line up (wires land on terminals, lamps sit by ports), and the owner has seen the contact sheet. *Props:* the 13 switchboard slots. *Not in scope:* new rules, textures beyond flat colours, animation.
13. ✅ **Twists on the house install** (D31), done 2026-11-16 (`InstallJob.Twists`; props are labelled boxes). *Goal:* about one install in three has a one-step complication (old monopoly box, ivy, wasp nest, new wallpaper, dog in the garden, kitchen socket), so installs stop feeling identical. *Where:* `Minigames/Install/InstallJob.cs` (a seeded twist adding one step with an existing verb; bodging it leaves a latent defect), `InstallSite.cs` (the twist's prop as a labelled box); table in `MODELS_AND_VARIETY.md` §7. *Done when:* a screenshot shows a twisted install; a test pins that the same seed gives the same twist, a twist adds exactly one step, and the first install of a new game has none. *Props:* `install.old-junction-box`, `twist.ivy`, `twist.wasp-nest`, `twist.wallpaper`, `twist.garden-gate`, `twist.kitchen-socket` (write briefs in the same form as the switchboard batch). *Not in scope:* places, modes, switchboard twists.
14. **[Dev] TT1 Tree schema over work.json.** *Goal:* tech-tree content loads from `game/content/tree/*.json`, each task being an action any actor can perform at its own speed and accuracy; today's 5 work kinds migrate as tasks. *Where:* `Content/ContentDefs.cs`, `ContentDb.Validate()`, `SaveCodec` migration, `WorkRules` callers. Spec: `TECH_TREE.md` §0–§2. *Done when:* the game plays as before; one test validates every branch file. *Not in scope:* unlock rules, new tasks.
15. **[Dev] TT2 Tree rules.** *Goal:* nodes unlock from recalls, conditions and required nodes; grants apply; automated stages wear without upkeep. *Where:* `Tree/TreeRules.cs`, `Work/TaskRules.cs`. *Done when:* two tests (unlock gating; wear drops a stage). *Not in scope:* UI.

### Tech tree track (after Up next; one branch per session or two)
- [ ] TT3 Tree in the world: tech nodes in PIP's Almanac (on the phone, per the spine), people/money nodes in the notebook; backlog props (job-sheet pile, blinking machine).
- [ ] TT4 Field `field.van` + `field.parts` (matches the cargo backlog item below).
- [ ] TT5 Customers `customers.door` + `customers.machine`; Money `money.tin` + `money.bookkeeper`.
- [ ] TT6 Sight `sight.notebook` + `sight.pager`; People `people.showme`.
- [ ] TT7 Room `room.bbs` + `room.modem2400`; Community `community.ledger` + `community.files`.
- [ ] TT8 Tier 2 of every branch (Act 1–2 content), then tune numbers in a balancing pass.

## M0 — Scaffold ✅ (done 2026-11-11)
Pure sim + tests; Godot room; answering machine → Frame install → billing; Almanac; Notebook (automation + Promise Ledger); save/load; smoke test.

## M0.5 — Planet foundation ✅ (done 2026-11-11)
A 2.5 km low-poly planet (oceans, mountains, forests, deserts, snow) generated by the sim; day/night sky you can fly out of into space; the starting town with houses, Main Street and telephone poles; walk, drive the van, and a survey-flight mode (F).

## M0.6 — First job loop ✅ (done 2026-11-11, placeholder art)
Odd jobs at neighbours' doors for cash; door-knocking to win customers; modems bought at Sol's Electronics; sockets fitted at the customer's house before the frame work; Hollow Farm out of town on a graded road (van is half speed off-road); a "what next" line with markers over buildings; the frame, PIP and notebook as in-world screens instead of menus.

### Gameplay backlog (next)
- [x] The install as a hands-on, multi-step job outside and inside the house, with a bodge option and hidden faults (2026-11-12).
- [ ] Odd jobs as hands-on tasks (currently press E and time passes).
- [x] Carry kit from the van to the house: drum, ladder, toolbox, boxed modem (2026-11-12).
- [x] First automation stage playable end to end: hire the technician, they install on their own, their mistakes become repair visits (2026-11-12).
- [ ] Replace the HUD strip with in-world objects (wall calendar, cash tin, pager, fax roll).
- [ ] The frame as real terminal blocks and wires rather than a printed work sheet.
- [x] Go inside houses (2026-11-12). [ ] Go inside the shop and the farmhouse.
- [x] Connect Hollow Farm for real: 14 poles planted and strung by hand along the farm road (2026-11-12).

### Planet backlog (build features on top of the world)
- [x] First road (town → Hollow Farm), fast on-road / slow off-road. [ ] A road *network* with junctions and curves.
- [ ] **Terrain as the challenge**: rivers needing bridges, cliffs needing switchbacks, swamps, snow passes that close in winter.
- [ ] Trees and buildings that block the van (currently trees are scenery only).
- [ ] More settlements: a farm (Ruth Adeyemi), a fishing village, a mountain hamlet — each a Last Mile story.
- [ ] Boat, then light aircraft (replacing the developer survey flight), each earned.
- [ ] Poles and cables you place yourself along the ground; coverage and line-of-sight from the sim.
- [ ] Level-of-detail terrain so the planet can be finer near you (currently ~16 m facets everywhere).
- [ ] Much later: rockets and satellites (final era).

## M1 — "The first hour is fun" (4–6 weeks) → **private playtest**
- [ ] **Counter trade** (Act 0 income): fix telex machines and sell modems at the electronics shop counter. Balances the economy so Act 0 makes money.
- [ ] Make the Frame **diegetic**: click the actual terminal blocks on the wall and see jumper wires drawn in 3D. Keep the menu as an accessibility mode.
- [ ] Audio pass: relay clicks, dial tone, modem handshake, room ambience. This matters more than art for "feel".
- [ ] Paper props: wall calendar shows the date, a cash tin, a fax roll for events (replaces the HUD text in Act 0).
- [ ] Settings: mouse sensitivity, key rebinding, no-timer mode, subtitles.
- [ ] Tutorial through PIP lines, driven by state (no scripted sequence).

## M2 — "Steam page + demo" (6–8 weeks) → **announce**
- [ ] Art direction: low-poly 80s with warm tungsten light and CRT glow. Use a consistent asset pack, or commission a few hero props.
- [ ] Second location: the **exchange building** (move out of the bedroom) and **1 Main Street** as the Act 1 office.
- [ ] Door-knocking in a small street (8 houses), with appointments.
- [ ] Hire the first technician as a *person* with a name and a line of dialogue (delegation exists already).
- [ ] Steamworks integration (Steamworks.NET or Facepunch.Steamworks): achievements, cloud saves.
- [ ] **Demo = Act 0 + the start of Act 1 (≈60–90 min).** Capsule art, trailer, store page live.

## M3 — "Early Access content" (3–5 months)
- [ ] Act 1 (BBS): BBS file-area curation (Borrowed Library), hunt groups, the first rival (incumbent carrier).
- [ ] Act 2 (dial-up): modem bank minigame, upstream bill, heat/power, second rival (discount provider).
- [ ] Outage diagnosis minigame: clues, travel, root cause.
- [ ] Contracts v1: two templates, one hidden clause, SLA credits.
- [ ] Van + town map; travel time.
- [ ] Economy balancing pass with a telemetry log (local file only).
- [ ] Steam Next Fest with the demo. **Early Access launch.**

## Post-launch (funded by sales)
Acts 3–4 → 1.0. Office designer, crime/evidence, Acts 5–7, Steam Workshop (content JSON is already mod-friendly), console ports.

## Engineering backlog
- Customer names/archetypes into `content/` (currently a placeholder array in `Simulation.cs`).
- The party-line slot sometimes lands on a *pending* customer, which gets them installed for free. Generate the party line from existing subscribers only.
- Save slots and autosave on quit.
- ~~CI~~ done: `.github/workflows/check.yml`, manual trigger only (free).
- Localisation: move strings out of the sim into keyed tables before Early Access.
