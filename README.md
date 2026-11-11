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
| E | Use what you're looking at (answering machine, frame, handset, notebook, bed, van) / get out of the van |
| W/S, A/D in the van | Drive / steer |
| F | Survey flight on/off (fly where you look; Shift = fast, Space = up). Fly high enough and you're in space |
| E on the handset / notebook / frame | Lean in and use it with the mouse. These are objects in the room, not menus. Esc steps back |
| F5 / F9 | Save / load |
| Esc | Close menu / free the mouse |

**The world** is one small planet (2.5 km radius): walk out of your room, take the van down Main Street, or press F and fly around it.

**First minutes:** follow the yellow "▶" line at the top of the screen; a ▼ floats over the building it means, and `$` marks a paid odd job. A typical start: do an odd job for cash, knock on doors until someone signs up, fit their wall socket at their house (you start with one modem; Sol's Electronics at the east end of the street sells more), then wire their line at the frame in your room. Drive the road out to Hollow Farm for better-paid jobs.

**At the frame:** click a customer line, then click its port. Probe smudged labels, test ports, reroute faulty ones to a spare, and leave the party line alone.

## Develop

Day to day, just build and play. Run the checks only before a release or when game rules change:

```bash
dotnet test Telo.slnx
```

```bash
godot --headless --path game -- --smoke
```

The same checks can be run for free on GitHub: Actions tab → **Check** → **Run workflow**. Nothing runs automatically on a normal push.

See [CLAUDE.md](CLAUDE.md) for architecture and rules, and `docs/` for the [review](docs/REVIEW.md), [decisions](docs/DECISIONS.md), [build plan](docs/BUILD_PLAN.md) and [business plan](docs/BUSINESS_PLAN.md). The full design vision is in [TELO_GAMEPLAY_HANDOFF.md](TELO_GAMEPLAY_HANDOFF.md).
