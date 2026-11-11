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
| E | Use what you're looking at (answering machine, frame, handset, notebook, bed) |
| Tab | PIP's Almanac (recall future tech) |
| N | Company notebook (automation + Promise Ledger) |
| F5 / F9 | Save / load |
| Esc | Close menu / free the mouse |

**First minute:** play the answering machine three times, then work the frame. Click a customer line, then click its port. Probe smudged labels, test ports, reroute faulty ones to a spare, and leave the party line alone.

## Develop

```bash
dotnet test Telo.slnx
godot --headless --path game -- --smoke
```

See [CLAUDE.md](CLAUDE.md) for architecture and rules, and `docs/` for the [review](docs/REVIEW.md), [decisions](docs/DECISIONS.md), [build plan](docs/BUILD_PLAN.md) and [business plan](docs/BUSINESS_PLAN.md). The full design vision is in [TELO_GAMEPLAY_HANDOFF.md](TELO_GAMEPLAY_HANDOFF.md).
