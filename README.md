# TELO: Network Empire

*Start by wiring one phone line in 1983. End up running the network a country depends on. The job changes as you play.*

A first-person telecom business sim built in **Godot 4.7 (C#)** on a pure, tested simulation core.

## Play

```bash
godot --path game
```

| Key | Action |
|---|---|
| WASD / mouse | Move / look |
| Shift | Sprint |
| E | Use what you're looking at. **Hold E** for anything that takes time (a bar fills) |
| R | The quick, wrong way: bodge a step, thump a repair |
| G | Put down what you're carrying |
| W/S, A/D in the van | Drive / steer. E gets out |
| 0–9 | Work the Board's terminal |
| Esc | Step back from the switchboard, terminal, handset or notebook |
| F5 / F9 | Save / load |
| F1 | Lists the god keys (skip ahead while testing): F2 cash, F3 next day, F4 finish the current step, F6 next act |
| F | Survey flight on/off (a developer tool) |

**The world** is one small planet (2.5 km radius): walk out of your room, take the van down Main Street, drive out to Hollow Farm.

**First minutes:** follow the yellow "▶" line at the top of the screen and PIP's speech bubbles; a ▼ floats over the building it means. Play the answering machine, drive to your first customer, carry the kit from the van, hold E on each yellow marker to install the line, then go back to your room and jumper them in at the switchboard: take a jumper from the hook, click their terminal, click the port their tag names, throw the test switch.

**After that:** odd jobs for cash, knocking on doors, line cards when the Board is engaged, hiring Jory and handing over job tickets, the depot at 1 Main Street, the Board's terminal, catalogue orders, and the pole line to the farm.

## Develop

Day to day, just build and play. Run the checks only before a release or when game rules change:

```bash
dotnet test Telo.slnx
```

```bash
godot --headless --path game -- --smoke
```

The same checks can be run for free on GitHub: Actions tab → **Check** → **Run workflow**. Nothing runs automatically on a normal push.

## Documents

| File | What it is |
|---|---|
| [CLAUDE.md](CLAUDE.md) | Architecture, rules and how every built system works. Start here if you are an agent |
| [docs/NEXT_SESSION.md](docs/NEXT_SESSION.md) | Where things stand and what to do next |
| [docs/NEZ_AND_THE_SPINE.md](docs/NEZ_AND_THE_SPINE.md) | Newest direction: actions anyone can perform, Nez the opponent, one plot line, the phone, the day planner |
| [docs/DESK_AND_DAY_DESIGN.md](docs/DESK_AND_DAY_DESIGN.md) | Objects instead of panels, lived time, staff and tickets |
| [docs/EXPANSION_DESIGN.md](docs/EXPANSION_DESIGN.md) | The loop after the first hour: the Board, places on the planet, rounds |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Every locked decision, D1 onward |
| [docs/ASSET_LIST.md](docs/ASSET_LIST.md) | Models, textures and the trailer plan (later) |
| [TELO_GAMEPLAY_HANDOFF.md](TELO_GAMEPLAY_HANDOFF.md) | The original full vision (canon) |
| [docs/REVIEW.md](docs/REVIEW.md), [docs/BUILD_PLAN.md](docs/BUILD_PLAN.md), [docs/BUSINESS_PLAN.md](docs/BUSINESS_PLAN.md) | The first review, the original milestones, the business plan (historical) |
