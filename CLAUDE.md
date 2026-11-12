# CLAUDE.md — TELO: Network Empire

**The owner is not a developer.** Claude does all design, code, models, images and sound. Explain choices in plain language, keep the game runnable at all times, and prefer showing a screenshot over describing code.

First-person, open-world telecom business sim, 1983–2035, set on **one small planet** (2.5 km radius, ~16 km lap) that you walk, drive, sail and fly around, and eventually launch satellites from. You start alone wiring a frame in a rented room and end up running infrastructure millions depend on. **The core idea: the job itself changes as you play.** Verbs you do by hand become delegated, then automated, and the interface you see goes from paper to terminal to database to dashboard.

**Starting a session? Read `docs/NEXT_SESSION.md` first.** **What the game becomes after the first hour, and the order to build it: `docs/EXPANSION_DESIGN.md`** (the five-job loop, the Board and line capacity, places on the planet, rounds, phases A–J). Models, textures, characters and the trailer plan: `docs/ASSET_LIST.md`. Design canon: `TELO_GAMEPLAY_HANDOFF.md` (the full vision). Locked decisions: `docs/DECISIONS.md`. What to build next: `docs/BUILD_PLAN.md`.

## Layout

```
src/Telo.Sim/          Pure C# simulation (net8.0). ALL game rules live here.
tests/Telo.Sim.Tests/  xUnit tests for the sim. Loads the real game/content JSON.
game/                  Godot 4.7 .NET project. The presentation layer: it shows the sim and drives it, but holds no rules.
  content/*.json       Data: acts, announcements, almanac, work (automation stages), premises (Last Mile)
  scripts/GameRoot.cs  Composition root: loads content, owns Simulation, builds room, wires views
  scripts/UI/          HUD + modal views (Frame, Almanac, Notebook). TierStyle = paper/terminal/db/dashboard look
  scripts/World/       PlanetBuilder (terrain/ocean/trees), PlanetSky, TownBuilder, RoomBuilder, LowPoly (code-built models), Interactable
  scripts/Player/      SphereBody (planet gravity) → FirstPersonController, Van
docs/                  Review, decisions, build plan, business plan
```

## Commands

```bash
dotnet test Telo.slnx                                   # sim unit tests (must stay green)
dotnet build game/Telo.Game.csproj                      # compile the game scripts
godot --headless --path game --import                   # first run / after adding assets
godot --headless --path game -- --smoke                 # walk + drive + install + 700 days + save; prints "WORLD OK" and "SMOKE OK", exit 0
godot --path game -- --shot=/abs/path/x.png             # saves x-town, x-install, x-house, x-aerial, x-orbit, x-room ... .png, then quits
xvfb-run -a godot --path game -- --shot=/abs/path/x.png # the same in a cloud session (no screen or graphics card there)
godot --path game                                       # play
godot --editor --path game                              # open editor
```

## Testing policy: develop lots, test little

The owner wants fast iteration. **Do not run the test suite or the smoke test after every change.**

- Always: `dotnet build game/Telo.Game.csproj` (a few seconds) so the game is never left broken.
- For anything visual: take a `--shot` screenshot and look at it. That is the main check.
- Run `dotnet test` + `--smoke` only: before a commit that changed rules in `src/Telo.Sim`, before a release/tag, or when asked.
- GitHub Actions (`.github/workflows/check.yml`) is **manual-trigger only** (plus `v*` tags). Never add `on: push` for branches.
- Add new tests sparingly: one or two per new sim system, for the rules that would be painful to get wrong (determinism, saves, money).

## Non-negotiables

- **`src/Telo.Sim` is pure.** No `Godot` references, no file I/O, no `DateTime.Now`, no `System.Random`; use `Core/DetRng`. Same seed means the same game. There's a test that checks this.
- **Rules live in the sim, never in `game/`.** If a script in `game/` decides an outcome, move that logic into `Telo.Sim` and test it.
- **Data-driven.** New acts, tech, automation stages and Last Mile people go in `game/content/*.json` with stable string IDs. `ContentDb.Validate()` must catch broken references. Never rename a shipped ID, because saves reference them.
- **Every improvement creates a new problem.** Every work stage after the first must set `newProblem` (a test enforces it).
- **Failure costs time, money or satisfaction, never progress.** Minigames use soft clocks with no fail screens. Chaos options (fast, cheap, wrong) leave *latent defects* that surface later.
- **Delegation is 80–90% accurate.** Mistakes are hidden until they surface as defects.
- **Saves are versioned JSON of `GameState` only.** Bump `GameState.CurrentVersion` and add a migration step in `SaveCodec` whenever the shape changes. Derived state is recomputed on load.
- **Notifications are for milestones and errors only.** Use `state.Emit(...)`.
- **Tone:** wry, warm, bureaucratic comedy. Cynical about institutions, never about the people who need the network. Piracy is folk comedy, never instruction. The Last Mile is named people, not a score. Crime is abstract: no real hacking, frequencies or evasion techniques.
- **Don't recreate existing games** or use real company or product trademarks in content. Fictionalise them ("the touchscreen phone", "the national phone monopoly").

## The planet

- `src/Telo.Sim/Planet/PlanetGen.cs` is the truth: `Height(dir)`, `BiomeAt(dir)`, `Sites` (flattened ground for buildings), `SurfaceDistance`. Pure maths, no trig (determinism). Positions in the sim are **directions on the unit sphere** (`V3`).
- `game/scripts/World/PlanetBuilder.cs` meshes it as a cube-sphere (6 faces × 12×12 chunks × 20×20 quads, ~16 m facets), skips deep seabed, adds an ocean sphere and region-bucketed tree/rock MultiMeshes with distance culling.
- **"Down" is toward the world origin.** Anything that moves derives from `SphereBody` (`MoveOnPlanet`, `AlignToPlanet`, `PlaceAt`). Never assume +Y is up outside a building's local space.
- Place things on the surface one by one with `TownBuilder.Place(gen, eastM, southM, yawDeg, lift)`: a long flat object would float off the curve (2 cm at 10 m, 1.3 m at 80 m).
- A new town needs a `Site` in `PlanetGen` (flattens terrain) before buildings go on it.
- One fixed world for now: seed 1983 (`GameRoot.DefaultSeed`). The planet is rebuilt from the save's seed on load.
- Planet size is `PlanetGen.DefaultRadiusM`. The owner chose it for travel time (5 min flight, 12–20 min drive per lap); don't change it casually.

## No floating menus: screens live in the world

The owner wants to avoid GUIs. Anything you read or click is the surface of an object in the world.

- `WorldScreen` renders a `ModalView` into a `SubViewport` shown on a quad (the frame board, PIP's handset, the notebook). Using the object leans the camera in (`GameRoot.LeanInto`); mouse clicks are projected onto the surface. Esc steps back.
- Screens stay visible and live when idle (`view.Show(tier)`); `IsOpen` means the player is using one.
- New "menus" must be added as a `WorldScreen` on a prop, never as a `CanvasLayer` overlay.
- The remaining overlay is the small HUD strip (date, cash, the "▶ next" line, event feed). It is a placeholder to be replaced by a wall calendar, cash tin, pager and fax roll.
- **Placeholders are fine.** The owner wants gameplay first and polish later: boxes with labels are acceptable for new buildings and props.

## Town gameplay (Act 0–1)

- `game/content/lots.json`: every building someone lives or works in (site, east/south offsets, resident, archetype, fee). `TownBuilder` builds them; `GameRoot.LotAction` decides what each door offers from sim state.
- `Telo.Sim/Town/TownRules.cs`: door-knocking, odd jobs (cash + goodwill), modem stock, fitting sockets. `Guidance.Next` gives the "what next" line and target lot (shown as ▼ above the building; `$` marks an odd job).
- **One thing at a time** (`Telo.Sim/Town/Onboarding.cs`): a new game introduces the answering machine → the install → the frame → an odd job → door-knocking, then everything (`Free`). The step is derived from the save, never stored. It gates odd-job offers, knocking, word-of-mouth calls, and which HUD numbers show; `Guidance.Next` explains each step once. When adding a new system, decide which step first shows it.
- **PIP's speech bubbles** (`game/scripts/UI/PipTips.cs`, `Hud.Say`): one short bubble the first time the player does each thing, fired from the action itself (pick up kit, first step, bodge, test, sign-off, hire, first fault…). The owner approved bubbles as an exception to "no overlays". Keep the top "▶ next" line short; explanations belong in a bubble. New systems should add a line here.
- **Held back until needed** (`Onboarding`): the Almanac and notebook open at `Free` (`DeskOpen`); the road out of town has a council barrier and no farm odd jobs until the first hire or first pole (`RoadOpen`).
- **God keys** (owner's testing tool): `F1` lists them, `F2` +$500, `F3` next day, `F4` finishes whatever the game is asking for (`Simulation.GodNextStage`: each first-hour step, then the hire, the pole line, the rest of the town, then the next act), `F6` jumps to the next act (`GodNextAct`).
- **Pace**: a line going live pays a connection fee that day (2 months' fee). When `Guidance.IsQuiet`, the bed sleeps until something needs the player (`Simulation.RestUntilNeeded`, up to 30 days), so the calendar speeds up as the work gets delegated. Finishing a pole line pays its `grant` and lifts goodwill in every household.
- **Odd jobs by hand** (`Telo.Sim/Minigames/OddJob/OddJobTask.cs`): in a house, the resident lets you in and the thing to fix is on the rug: open it up, trace, fix, test, hand it back. `[R]` thumps it: paid today, goodwill lost. The shop and farm still do odd jobs at the door.
- Install flow: sign up (knock or answering machine) → hands-on install at their house (uses a modem from Sol's Electronics) → wire at the frame → billed monthly.
- The house install is `Telo.Sim/Minigames/Install/InstallJob.cs` (drop wire → junction box → socket → skirting cable → modem → optional dial-tone test). `[E]` does a step properly, `[R]` bodges it (latent defect). `game/scripts/World/InstallSite.cs` only shows the job as click points on and in the house. A half-done job is not saved; it restarts.
- **Van kit** (`game/scripts/Player/Cargo.cs`): cable drum, ladder, toolbox and a boxed modem ride on the van. `[E]` picks one up (one thing in your hands; drum and ladder slow you), `[G]` puts it down, using the van puts it back. Kit is "on site" when held or lying within 6 m of you. What each step needs is a sim rule (`InstallJob.Needs`, `TownRules.RepairNeeds`, the `Tools` flags). Not saved: after a load it is all back on the van.
- **First automation stage** (`work.json` → `install` stage 1, $300, needs 4 paying subscribers): once hired, the technician connects one waiting customer a day on their own (`Simulation.TechnicianDay`), needs modems in stock, and is right 85% of the time. The player can still do any install by hand.
- **Faults**: a latent defect that surfaces at a real address becomes a `Fault` in `GameState.Faults`. The line stays bad (satisfaction −2 a day) until the player drives out with the toolbox and fixes it at the door (`TownRules.Repair`). `!` marks the house.
- **Pole lines** (`Telo.Sim/Build/LineRules.cs`, `game/scripts/World/PoleLineView.cs`): a premise with `poleLine` in `premises.json` can't be bought, only built. Pole sites run beside a `PlanetGen` road; plant each pole (needs a pole from the van, $15, 30 min), then string each span (cable drum + ladder, 15 min). The last span connects the premise. Progress is `GameState.Lines` (save version 3).
- Houses (`LotKind.House`) are hollow and enterable: the door opens when the resident lets you in for an install. The shop and farm are still solid boxes.
- Work costs in-game time via `GameRoot.SpendHours`.
- `PlanetGen.Roads`: graded routes that cut/fill terrain; `IsPaved` makes the van full speed on roads and in settlements, half speed off-road. Road surfaces are ray-cast onto the terrain (`TownBuilder.BuildRoads`).

## Assets: everything is made in code

- **Models:** `LowPoly` builder (boxes, cylinders/cones, gems, gable roofs), flat-shaded and vertex-coloured, sharing one material. Style: low-poly with small details (window trim, chimneys, stripes, per-facet colour jitter). Colours are sRGB.
- **No imported textures or model files yet.** Sound should be synthesised in code too (tones, clicks, noise) when audio work starts.
- **Blender is planned for later models.** The owner wants to build models in Blender in future; the cloud environment has Blender installed (`blender -b --python script.py`, export `.glb` into `game/`). Until that work starts, keep building props in code.
- Room interiors still use `RoomBuilder.Box` primitives; migrate them to `LowPoly` when touched.

## Sim architecture cheatsheet

- `World/Simulation.cs`: facade. `NewGame`, `AdvanceDay`, `TakeEnquiry`, `FrameWorkOrder`, `NewFrameJob`, `ApplyFrameOutcome`, `NewInstallJob`, `ApplyInstallOutcome`. Saves are at version 3.
- `World/GameState.cs`: all mutable state (serialisable).
- `Almanac/AlmanacRules.cs`: research is recall. Archive (earned from your network) is spent to recall future tech. Recalling it early adds Echo, and too much Echo triggers a Correction (the entry drifts).
- `Work/WorkRules.cs`: earned automation. Manual → Delegated → Automated, plus the interface tier.
- `World/ActRules.cs`: the act advances when its exit milestone is met **and** the next act's gate announcement has fired. The announcement queue is the gate, not the calendar.
- `Ledger/LedgerRules.cs`: the Promise Ledger. Promises with dates, broken promises remembered.
- `Minigames/Frame/FramePuzzle.cs`: the first building minigame (probe / test / jumper / reroute / party line / hot-wire).

## Adding a new building minigame

1. Write a pure class in `src/Telo.Sim/Minigames/<Name>/` with: `Generate(seed, ...)`, the player verbs, `Evaluate()`, and `Delegate(seed, accuracy)`.
2. Tests: same seed gives the same puzzle; the proper solution is perfect; the chaos option leaves latent defects; the soft clock never fails.
3. Add `Simulation.Apply<Name>Outcome` to turn the result into state changes.
4. Add a `ModalView` UI in `game/scripts/UI/` (later: make it diegetic, in the 3D world), and an `Interactable` in the room.

## Gotchas

- Godot C# classes that derive from Godot types must be `partial`.
- A class named `Sky` clashes with `Godot.Sky`; ours is `PlanetSky`.
- Vertex colours need `VertexColorIsSrgb = true` or everything looks washed out.
- `LookAt` fails when the up vector is parallel to the view direction (e.g. looking straight down at the planet).
- Use `SetAnchorsAndOffsetsPreset`, not `SetAnchorsPreset`, for full-screen Controls (the latter left modals collapsed).
- Content JSON is camelCase and enums are camelCase strings (`"mode": "delegated"`).
- Exported builds need `include_filter="content/*.json"` (already in `export_presets.cfg`).
- Economy is **not balanced yet**: Act 0 runs at a loss because counter-trade/odd-job income isn't built. See `docs/BUILD_PLAN.md`.
