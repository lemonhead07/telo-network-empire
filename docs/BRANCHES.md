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

## Topic branches: a layer under each lane

For one piece of work inside a lane, make a short-lived **topic branch** off the lane, named `<lane>/<topic>`:

```
geometry/roads ──┐
geometry/homes ──┼──► feature/geometry ──┐
                 │                       ├──► master
gameplay/jobs  ──┼──► feature/gameplay ──┘
```

- Examples: `geometry/roads`, `geometry/homes`, `geometry/people`, `geometry/nature`; `gameplay/jobs`, `gameplay/staff`, `gameplay/eastgate`.
- (Git can't have both `feature/geometry` and `feature/geometry/roads`, so topics use the lane's short name as their prefix.)
- Make one when the work starts (from the lane, after merging `master` into the lane), merge it back into its lane when the piece is done, then delete it. Topic branches live days, not months.
- Two topics in the same lane can run at once if they touch different files: `geometry/roads` (`TownBuilder.BuildRoads`, `RoadNet`, road models) and `geometry/homes` (`tools/models/town.py` houses, `TownDressing` gardens) barely overlap.
- `geometry/roads` and `geometry/homes` exist already (2026-11-19).

## Rules of the road

1. **Start a session by naming the lane, and the topic if there is one** ("geometry/roads", "gameplay: EA2f"). Check out that branch and **merge `master` into it first**, so you have the other lane's latest work.
2. **Stay in your lane.** Something for the other lane goes in `docs/IDEAS.md` as one line, tagged `[geometry]` or `[gameplay]`.
3. **Finish a piece, then merge it into `master`.** Before merging: `dotnet build game/Telo.Game.csproj`, and a `--shot` you have looked at. Gameplay also runs `dotnet test` and `--smoke`. Merge little and often: a branch that sits for weeks is where clashes come from.
4. **The shared files** are `game/scripts/GameRoot.cs` and `game/scripts/World/TownBuilder.cs`. Both lanes edit them, so keep geometry changes there to small hooks, and merge `master` in again before you push if the other lane has moved.
5. **Anything that moves a building, a street or a lot is gameplay**, even if it is for looks: travel times change, and the Act 5 script player's cash bands are on a knife-edge (`dotnet test` must pass).
6. **Cloud sessions** get their own random branch (`ccr-...` or `claude/...`). The AI does all the merging itself (owner, 2026-11-23: "keep lanes, AI merges to lane"), with `tools/branches.sh`:
   - **Start:** `tools/branches.sh start gameplay` (or `geometry`) merges `master` and the lane into the session branch, then lists any **stranded** work: branches whose commits are in neither lane nor master. Read that list before building anything: the item may already be done somewhere (the Yard sat finished on `ccr-07a608a6-jndwwk` for a day). If a stranded branch holds finished work for your lane, merge it in first.
   - **Finish:** once the checks in rule 3 pass and the work is committed, `tools/branches.sh finish gameplay` merges the lane in again, pushes the session branch and pushes it on to the lane. Every session ends this way, so the next one starts from everything.
   - **Standing permission:** a session may push its own branch and **its lane** (`feature/gameplay` or `feature/geometry`). It never pushes `master`: the owner merges into `master` through pull requests (below).
   - `tools/branches.sh` with no arguments just lists stranded work.
7. `feature/gameplay-rapid` was the old single lane. Finish what is on it, merge it into `master`, then carry on in `feature/gameplay`.

## Pull requests into master (owner, 2026-11-25)

The owner merges into `master` on GitHub; sessions never push it. To keep that to a click or two:

1. **One standing pull request per lane:** `feature/gameplay → master` and `feature/geometry → master`. The first session that finishes with its lane ahead of `master` opens it (title "Gameplay lane → master" or "Geometry lane → master"); every later push to the lane updates the same PR by itself. After the owner merges it, the next session that finishes opens a new one. `tools/branches.sh finish` says whether the lane is ahead of `master` and so needs one.
2. **A PR is only opened or left open when the lane passed its checks** (rule 3): build, `--shot` looked at, and for gameplay `dotnet test` and `--smoke`. The PR body lists what is in it in plain words and the checks run.
3. **Every session ends with a note to the owner, "Git: what to do"**, in plain words: which PR to merge now (with its link), in what order if there are two, or "nothing to merge". Default order when both are open: the one finished first; the other lane picks up `master` automatically at its next `branches.sh start`, so merging one never breaks the other.
4. **Subagents** work in local worktree branches that are never pushed. The lead session merges them into its own branch, runs the checks on the combination, and pushes only its branch and lane. So GitHub only ever sees session branches, the two lanes and `master`.
5. **Old branches:** `tools/branches.sh` lists stranded work. A session never deletes a branch on GitHub itself; it names the ones that are safe to delete in its note, and the owner deletes them (or says "delete them").

