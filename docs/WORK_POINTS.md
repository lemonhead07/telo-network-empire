# Work points: no progress bars, the model says where you work

*First built 2026-11-19 on the parked branch `claude/world-building-system-fmjwd3` (owner: "removing all of the progress bars with the real completable tasks, clicking in certain spots, maybe programmable spots based off the models' pieces, so we can develop geometry and the building of the world as one system"). Ported onto today's code by hand on 2026-11-26 as **HO1** (D58, `docs/POLISH.md` §1.7).*

## What it is

Every job step that used to be "hold E and watch a bar fill" is now done at **2–5 points on the thing itself**: a spadeful at the foot of the pole, the clip at the top, each screw of a cabinet. The next point glows; you look at it and hold E; **the piece moves while you work it** (a screw turns and sinks, earth piles up, a clip slides home). When the last piece is done the step is done, exactly as the old hold did. The total real time is the same, so the clock (D23, time is lived) is unchanged, and so are the rules: no outcome moved into `game/`.

- Code: `game/scripts/World/WorkPoints.cs`. One call converts any held interaction: `WorkPoints.Attach(host, places)`.
- The host `Interactable` keeps every rule (Prompt, CanUse, HoldSeconds, OnUse, OnAlt/bodge) and its sounds. WorkPoints only splits the hold across points and fires `OnUse` after the last.
- While there is work (the host's `HoldSeconds` > 0) the host is parked on collision layer 32, where no look ray finds it, and only the next point can be looked at. Otherwise the host is back on its owner's layer, so a wait or "built" still reads when you look at it.
- A point's prompt is the host's plus the motion: "Stand the cabinet (1 of 1): tighten the bolt [3/3]". Missing kit still shows the points, with the reason.
- R (the bodge) works on any point: the whole step is bodged at once, as before.
- **Sounds** (EA7): the first point plays the host's start sound, each point its motion's own while held (`tool.screw`, `tool.dig`, `tool.clip` …) and a small one when done, and the last point the host's done sound (the step's own, from `SoundCue.ForStep`).
- **No bar:** the HUD bar shows only when the thing you hold E on has no `OnHold` (work points and install spots animate the piece instead). What is left on the bar: talking and travelling (the Yard's counter, the ferry ticket, the data centre's ladder) and the two jobs still done at a front door (repairs, odd jobs at Sol's and the farm), which HO4 moves indoors. `--autoplay` prints `HOLD BARS n: …` at the end, listing every hold that still showed the bar.
- The walking player (`Autoplay.Use`) works through the points in order (R on the next point for a bodge); `--smoke` and `--shot` still call `OnUse` directly.

## Geometry and gameplay as one system

A model names its own work points. Any node in a `.glb` called **`wp-…`** is a point:

```
wp-01-dig            first point, motion "dig"
wp-02-lift
wp-03-tamp
wp-s2-01-bolt        tagged with a stage: FromModel(model, "s2") takes only these
```

- Order is the name order, so number them `01`, `02`, ….
- The motion is the last part of the name that is a known motion: `screw bolt clip place dig tamp pull tape plug check solder staple lift cut` (`WorkPoints.Motions`). Unknown → `place`.
- In Blender (`tools/models/_kit.py`): `empty("wp-01-screw", (x, y, z))`; in a site model, `wp(stage, i, motion, at)` (`tools/models/batch.py`). Empties survive the glTF export as `Node3D`s. Put them where a hand would go, on the surface, facing the side the player works from.
- In the game: `WorkPoints.Attach(host, w => w.FromModel(model, "s2") is { Count: > 0 } p ? p : fallback)`.
- No model yet? Give fallback places in code, or nothing at all: `Spread()` lays points across the host's box with motions guessed from the prompt's verb (`WorkPoints.Guess`).
- The little piece you work at each point is code-built; a prop called `piece.<motion>` (e.g. `piece.screw`) replaces it when modelled.

So a modeller can change *where* work happens by moving empties and re-exporting, with no code change; the sim still decides *how long* and *what it means*.

## Converted (HO1)

| Where | Host | Points |
|---|---|---|
| Every project step, Acts 0–5 (32 projects) | `ProjectSiteView.Spot` (`Projects.cs`: `WorkPoints.Attach(view.Spot, view.Places)`) | From the model: `wp-s<stage>-NN-<motion>` in each `site.*` builder (`tools/models/batch.py`). Starting: at the crates (lift, place), the lorry's delivery (check, check) or the signboard (paperwork). No model or no points for that stage (the data centre's rooms, the homing projects, waits): across the front of the thing at working height, motions from the step's words |
| Pole line: plant a pole | `PoleLineView.PlantSpot` | dig, dig, lift, tamp round its foot (code) |
| Pole line: string a span | `PoleLineView.StringSpot` | clip, pull, clip (code) |
| A line card into the rack | `LineRack.Slot` | place, plug, screw at the next empty slot (the `room.line-rack` model's `wp-` empties under `next-slot`) |
| Odd jobs and catalogue orders on the rug | the bench (`GameRoot.StartBenchJob`) | on the thing's front and top, motions from the step ("Undo the screws" → screw, screw, lift) |
| House install steps and the handset test | `InstallSite` spots (already points on the house) | no extra points: while you hold, the piece the step fits slides into place and the marker shrinks (`OnHold`); the telephone lifts while you listen |

## Project sites: stages and points in one model

A `site.*` model is the site as you find it plus one group per stage:

```python
pegs("mark", -0.6, -0.35, 0.6, 0.35)          # outside any stage: always there (pegs, the old shed, a crate)
with stage(1, "cabinet"):                       # empty "stage-1-cabinet"
    cabinet("cab", 1.0, 0.45, 1.25, "cabinet-green", with_plinth=False)
wp(1, 1, "lift", (-0.35, -0.3, 0.9))            # empty "wp-s1-01-lift": where a hand works on stage 1
```

- A project step builds the stage with its own index, unless `projects.json` gives the step a `"stage"` (looks only; no rule reads it). Several projects share a model (the street cabinet stands in for five), so a step can name a later stage, or `-1` for one that builds nothing (a wait, showing the Thursday club). Stages below the first one a project builds are already there when you arrive.
- `ProjectSiteView` shows built stages solid, the current one as a ghost (60% see-through, no shadow), later ones hidden; everything once built. During a wait there is no ghost and no points; the signboard says what it waits on and until when. Crates of parts stand beside it once the parts are there and empty as the work goes on.
- Points sit on the surface on the -Y side (Godot +Z, towards the player), 0.3–2.2 m up, or on the ground for dig/tamp. A tall thing (mast, tower) is worked at its foot or the bottom of its ladder (climbing is HO2).
- A model with no `stage-` nodes falls back to the old look (hidden until built, crates meanwhile); no model at all, to the growing frame.
- Look at them: `xvfb-run -a godot --path game -- --shot-projects=/abs/p.png [--only=<id part>]` writes `p-<project>.png` and `p-<project>-hands.png` (close on the next point) for every project half-built; add `--from=act5` for the data centre's rooms.

## Next (HO2, HO3)

Work points are the **fasten** and **remove** verbs. HO2 adds the others from `POLISH.md` §2 (carry, run, connect, test, climb, hand over, wait) as a `"verb"` per step, Acts 0–1 first; HO3 tags Acts 2–5.
