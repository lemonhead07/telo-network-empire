# Work points: no progress bars, the model says where you work

*2026-11-19, cloud session `claude/world-building-system-fmjwd3`. Owner's words: "removing all of the progress bars with the real completable tasks, clicking in certain spots, maybe programmable spots based off the models' pieces, so we can develop geometry and the building of the world as one system, then update the game with pieces at a time."*

## What it is

Every job step that used to be "hold E and watch a bar fill" is now done at **2–5 points on the thing itself**: a spadeful at the foot of the pole, the clip at the top, each screw of a socket. You look at the glowing piece and hold E; **the piece moves while you work it** (a screw turns and sinks, earth piles up, a clip slides home). When the last piece is done the step is done, exactly as the old hold did. The total real time is the same, so the clock (D23, time is lived) is unchanged.

- Code: `game/scripts/World/WorkPoints.cs`. One call converts any held interaction: `WorkPoints.Attach(host, places)`.
- The host `Interactable` keeps every rule (Prompt, CanUse, HoldSeconds, OnUse, OnAlt/bodge). WorkPoints only splits the hold across points and fires `OnUse` at the end. **No rule moves into `game/`.**
- R (the bodge) still works on any point: the whole step is bodged at once.
- The HUD bar no longer shows for work points (`GameRoot` → `_hud.SetHold`). Anything still showing a bar has not been converted yet.
- The walking player (`Autoplay.Use`) works through the points in order; `--smoke` and `--shot` still call `OnUse` directly.

## Geometry and gameplay as one system

A model names its own work points. Any node in a `.glb` called **`wp-…`** is a point:

```
wp-01-dig            first point, motion "dig"
wp-02-lift
wp-03-tamp
wp-plant-01-dig      tagged with a step: FromModel(model, "plant") takes only these
```

- Order is the name order, so number them `01`, `02`, ….
- The motion is the last part of the name that is a known motion: `screw bolt clip place dig tamp pull tape plug check solder staple lift cut` (`WorkPoints.Motions`). Unknown → `place`.
- In Blender (`tools/models/_kit.py`): `empty("wp-01-screw", (x, y, z))`. Empties survive the glTF export as `Node3D`s. Put them where a hand would go, on the surface, facing the side the player works from.
- In the game: `WorkPoints.Attach(host, w => w.FromModel(model, "step") is { Count: > 0 } p ? p : fallback)`.
- No model yet? Give fallback places in code, or nothing at all: `Spread()` lays points across the host's box with motions guessed from the prompt's verb (`WorkPoints.Guess`).
- The little piece you work at each point is code-built; a prop called `piece.<motion>` (e.g. `piece.screw`) replaces it when modelled.

So a modeller can change *where* work happens by moving empties and re-exporting, with no code change; the sim still decides *how long* and *what it means*. A new building piece arrives in the game one model at a time.

## Converted

| Where | Host | Points |
|---|---|---|
| Pole line: plant a pole | `PoleLineView.PlantSpot` | dig, dig, lift, tamp (code) |
| Pole line: string a span | `PoleLineView.StringSpot` | clip, pull, clip (code) |
| Every project step (14 projects, 12 site models) | `ProjectSiteView.Spot` (`Projects.cs`: `WorkPoints.Attach(view.Spot, view.Places)`) | from the model: `wp-s<step>-NN-<motion>` in each `site.*` builder (`tools/models/batch.py`); fallback `Spread()`. One round of points per click of the step |

(Later conversions add rows here.)

## Project sites: stages and points in one model (EA5)

A `site.*` model is the site as you find it plus one group per project step:

```python
pegs("mark", -0.6, -0.35, 0.6, 0.35)          # outside any stage: always there (pegs, the old shed, a crate)
with stage(1, "cabinet"):                       # empty "stage-1-cabinet": shown once step 1 is done
    cabinet("cab", 1.0, 0.45, 1.25, "cabinet-green", with_plinth=False)
wp(1, 1, "lift", (-0.35, -0.3, 0.9))            # empty "wp-s1-01-lift": where a hand works during step 1
```

- `ProjectSiteView` shows stages below the current step solid, the current one as a ghost (60% see-through, no shadow), later ones hidden; everything once built. During a wait there is no ghost and no points; the signboard says what it waits on and until when.
- Points sit on the surface on the -Y side (Godot +Z, towards the player), 0.3–2.2 m up, or on the ground for dig/tamp. A tall thing (mast, tower) is worked at its foot or the bottom of its ladder.
- A model with no `stage-` nodes falls back to the old look (hidden until built, crates meanwhile); no model at all, to the growing frame.
- Look at them: `xvfb-run -a godot --path game -- --shot-projects=/abs/p.png` writes `p-<project>.png` and `p-<project>-hands.png` (close on the next point) for every project half-built.
