# CLAUDE.md — TELO: Network Empire

First-person telecom business sim, 1983–2035. You start alone wiring a frame in a rented room and end up running infrastructure millions depend on. **The core idea: the job itself changes as you play.** Verbs you do by hand become delegated, then automated, and the interface you see goes from paper to terminal to database to dashboard.

Design canon: `TELO_GAMEPLAY_HANDOFF.md` (the full vision). Locked decisions: `docs/DECISIONS.md`. What to build next: `docs/BUILD_PLAN.md`.

## Layout

```
src/Telo.Sim/          Pure C# simulation (net8.0). ALL game rules live here.
tests/Telo.Sim.Tests/  xUnit tests for the sim. Loads the real game/content JSON.
game/                  Godot 4.7 .NET project. The presentation layer: it shows the sim and drives it, but holds no rules.
  content/*.json       Data: acts, announcements, almanac, work (automation stages), premises (Last Mile)
  scripts/GameRoot.cs  Composition root: loads content, owns Simulation, builds room, wires views
  scripts/UI/          HUD + modal views (Frame, Almanac, Notebook). TierStyle = paper/terminal/db/dashboard look
  scripts/World/       Greybox room builder + Interactable
  scripts/Player/      First-person controller
docs/                  Review, decisions, build plan, business plan
```

## Commands

```bash
dotnet test Telo.slnx                                   # sim unit tests (must stay green)
dotnet build game/Telo.Game.csproj                      # compile the game scripts
godot --headless --path game --import                   # first run / after adding assets
godot --headless --path game -- --smoke                 # end-to-end boot + install + 700 days + save; prints "SMOKE OK", exit 0
godot --path game -- --shot=/abs/path/room.png          # windowed screenshot (+ room-frame.png) then quit
godot --path game                                       # play
godot --editor --path game                              # open editor
```

**Definition of done:** `dotnet test Telo.slnx` is green AND the `--smoke` run prints `SMOKE OK`. For UI changes, also check a `--shot` screenshot.

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

## Sim architecture cheatsheet

- `World/Simulation.cs`: facade. `NewGame`, `AdvanceDay`, `TakeEnquiry`, `FrameWorkOrder`, `NewFrameJob`, `TryDelegateFrame`, `ApplyFrameOutcome`.
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
- Use `SetAnchorsAndOffsetsPreset`, not `SetAnchorsPreset`, for full-screen Controls (the latter left modals collapsed).
- Content JSON is camelCase and enums are camelCase strings (`"mode": "delegated"`).
- Exported builds need `include_filter="content/*.json"` (already in `export_presets.cfg`).
- Economy is **not balanced yet**: Act 0 runs at a loss because counter-trade/odd-job income isn't built. See `docs/BUILD_PLAN.md`.
