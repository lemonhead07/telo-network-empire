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
| Tab | PIP's Almanac (recall future tech) |
| N | Company notebook (automation + Promise Ledger) |
| F5 / F9 | Save / load |
| Esc | Close menu / free the mouse |

**The world** is one small planet (2.5 km radius): walk out of your room, take the van down Main Street, or press F and fly around it.

**First minute:** play the answering machine three times, then work the frame. Click a customer line, then click its port. Probe smudged labels, test ports, reroute faulty ones to a spare, and leave the party line alone.

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
