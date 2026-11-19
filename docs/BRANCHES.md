# Branches: two lanes into master

Two kinds of work run side by side, often in different sessions or different models at once. Each has its own long-lived branch, and both feed **`master`** (GitHub's default branch). `main` is an old copy and is not used.

```
feature/geometry ──┐
                   ├──► master   (always runs; only finished, checked work arrives here)
feature/gameplay ──┘
```

| Lane | Branch | What it is for | Touches | Leaves alone |
|---|---|---|---|---|
| **Geometry** | `feature/geometry` | How the world *looks*: models, textures, set dressing, buildings, people, vehicles, nature, previews. A pass like the 2026-11-19 town modelling. | `tools/models/`, `game/assets/`, `docs/img/props/`, `game/content/props.json`, `World/TownDressing.cs`, `World/Props.cs`, and one-line hooks (`Props.Make`, `GameRoot.Dress`, `look`) where a model replaces a box | `src/Telo.Sim/`, `tests/`, rules in `game/content/*.json`, lot and project positions |
| **Gameplay** | `feature/gameplay` | How the game *works*: rules, systems, processes and procedures for jobs, content, balance, the BUILD_PLAN "Early access" items. | `src/Telo.Sim/`, `tests/`, `game/content/*.json`, gameplay scripts in `game/scripts/` | Model scripts and `.glb` files (use placeholders: `LowPoly` boxes are fine) |

## Rules of the road

1. **Start a session by naming the lane** ("geometry pass" or "gameplay: EA2f"). Check out that branch and **merge `master` into it first**, so you have the other lane's latest work.
2. **Stay in your lane.** Something for the other lane goes in `docs/IDEAS.md` as one line, tagged `[geometry]` or `[gameplay]`.
3. **Finish a piece, then merge it into `master`.** Before merging: `dotnet build game/Telo.Game.csproj`, and a `--shot` you have looked at. Gameplay also runs `dotnet test` and `--smoke`. Merge little and often: a branch that sits for weeks is where clashes come from.
4. **The shared files** are `game/scripts/GameRoot.cs` and `game/scripts/World/TownBuilder.cs`. Both lanes edit them, so keep geometry changes there to small hooks, and merge `master` in again before you push if the other lane has moved.
5. **Anything that moves a building, a street or a lot is gameplay**, even if it is for looks: travel times change, and the Act 5 script player's cash bands are on a knife-edge (`dotnet test` must pass).
6. **Cloud sessions** get their own `claude/...` branch. Tell them the lane: they work on their branch, then merge it into `feature/geometry` or `feature/gameplay` when the piece is done (or straight into `master` if you say so).
7. `feature/gameplay-rapid` was the old single lane. Finish what is on it, merge it into `master`, then carry on in `feature/gameplay`.
