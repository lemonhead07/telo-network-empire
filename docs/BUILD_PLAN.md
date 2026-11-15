# Build Plan

> **2026-11-12:** the order of work after M0.6 is now set by `docs/EXPANSION_DESIGN.md` (rounds and phases A–J). The milestones below are kept for history and for the release checkpoints (playtest, demo, Early Access).

Each milestone ends playable, with green tests and `SMOKE OK`. Week counts assume one part-time developer working with AI help.

## Up next
*Dev sessions take the top **[Dev]** item (see `docs/SESSIONS.md`). Idea sessions take **[Idea]** items and add new ones, written to the Definition of Ready.*

1. **[Idea] Answer the spine questions.** *Goal:* the owner answers N1–N8 in `docs/NEZ_AND_THE_SPINE.md` §9 and confirms how the tech tree fits on actors and actions (`docs/TECH_TREE.md` §0). *Done when:* answers recorded in `DECISIONS.md`; items 2–3 below confirmed or changed. *Not in scope:* any code.
2. **[Dev] Actors and actions in the sim** (spine step 1). *Goal:* one question works for anyone: "what can this actor do now, and what is stopping the rest?" *Where:* place, hands, vehicle and travel time move into `src/Telo.Sim`; `GameRoot` player-only logic (`LotAction`, `StartInstall`, `StartBenchJob`, `KitOnSite`) shrinks to showing and forwarding. *Done when:* the game plays the same; tests pass; `GameRoot` is noticeably smaller. *Not in scope:* Nez, the phone, the tree schema.
3. **[Dev] The script player** (spine step 2). *Goal:* a test plays the game from the first message to the farm line by choosing actions, no cheats. *Where:* `tests/`, using the actions from item 2. *Done when:* the test passes from a fresh game. *Not in scope:* `--autoplay`.
4. **[Dev] Props by name** (D30, D32). *Goal:* every switchboard part comes from a named prop, so a model can replace it with no code change. *Where:* new `game/content/props.json` (copy `props` from `docs/props/switchboard.json`); prop records and validation in `Content/` (unknown ids and bad sizes caught by `ContentDb.Validate()`); a `Props.Make(id)` helper in `game/scripts/World/` that loads `game/assets/models/<id>.glb` if present, else builds the labelled box from the brief's size and first colour; `SwitchboardStation.cs`, `LineRack.cs` and the line card in `Cargo.cs` switch to it. *Done when:* the room screenshot looks the same as before; dropping any test `.glb` named after a slot replaces that box; one test validates `props.json`. *Props:* the 13 switchboard slots (briefs written). *Not in scope:* making the models; migrating other scripts (each migrates when next touched).
5. **[Dev] Switchboard models, first pass** (D33). *Goal:* the switchboard and its parts are real low-poly models. *Where:* one Blender script per slot in `tools/models/<id>.py` → `game/assets/models/<id>.glb`; a contact sheet PNG of each prop's three takes (plain / old / bodge) for the owner to pick from; `status` in `props.json` moves to `modelled`. *Done when:* the room screenshot shows the models in place, anchors line up (wires land on terminals, lamps sit by ports), and the owner has seen the contact sheet. *Props:* the 13 switchboard slots. *Not in scope:* new rules, textures beyond flat colours, animation.
6. **[Dev] Twists on the house install** (D31). *Goal:* about one install in three has a one-step complication (old monopoly box, ivy, wasp nest, new wallpaper, dog in the garden, kitchen socket), so installs stop feeling identical. *Where:* `Minigames/Install/InstallJob.cs` (a seeded twist adding one step with an existing verb; bodging it leaves a latent defect), `InstallSite.cs` (the twist's prop as a labelled box); table in `MODELS_AND_VARIETY.md` §7. *Done when:* a screenshot shows a twisted install; a test pins that the same seed gives the same twist, a twist adds exactly one step, and the first install of a new game has none. *Props:* `install.old-junction-box`, `twist.ivy`, `twist.wasp-nest`, `twist.wallpaper`, `twist.garden-gate`, `twist.kitchen-socket` (write briefs in the same form as the switchboard batch). *Not in scope:* places, modes, switchboard twists.
7. **[Dev] TT1 Tree schema over work.json.** *Goal:* tech-tree content loads from `game/content/tree/*.json`, each task being an action any actor can perform at its own speed and accuracy; today's 5 work kinds migrate as tasks. *Where:* `Content/ContentDefs.cs`, `ContentDb.Validate()`, `SaveCodec` migration, `WorkRules` callers. Spec: `TECH_TREE.md` §0–§2. *Done when:* the game plays as before; one test validates every branch file. *Not in scope:* unlock rules, new tasks.
8. **[Dev] TT2 Tree rules.** *Goal:* nodes unlock from recalls, conditions and required nodes; grants apply; automated stages wear without upkeep. *Where:* `Tree/TreeRules.cs`, `Work/TaskRules.cs`. *Done when:* two tests (unlock gating; wear drops a stage). *Not in scope:* UI.

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
