# Next Session — start here

*Left off: 2026-11-11, commit after "first job loop". Everything is pushed; the working tree is clean.*

## Where things stand
- Playable: `godot --path game`. Planet, town, van, farm road, odd jobs, door-knocking, modems, sockets, frame, in-world screens.
- The owner played it and found it fun. Their direction for what comes next is below.

## The owner's request: make the work hands-on and longer
"Enter the houses, and do work on the outside and inside, to make it more tedious" — meaning satisfying, physical, step-by-step work in the style of job simulators, instead of "press E and time passes".

### Plan for tomorrow (in order)
1. **Enterable houses.** Give house lots a door you walk through and a simple placeholder interior (hall, living room, one or two props). Start with one house template reused for all 12.
2. **An install as a multi-step physical job** (replaces the instant "fit socket"):
   - Outside: carry a cable drum from the van, clip the drop wire from the telephone pole to the house wall (a few click points), fit the junction box by the door.
   - Inside: pick the spot for the wall socket, screw it on, run the cable along the skirting, plug in the modem, test for dial tone.
   - Each step is a small interaction with a soft clock; skipping or rushing a step leaves a latent defect (rules already exist in the sim).
3. **Carrying things.** Van cargo (modems, cable drum, ladder, toolbox) that you pick up and carry by hand; two-handed items slow you down.
4. **Odd jobs as hands-on tasks** using the same verbs (open a casing, swap a part, test).
5. Keep everything placeholder (boxes with labels). Polish later.

### Rules to keep
- Outcomes and defects are decided in `src/Telo.Sim` (add an `InstallJob` there with steps, like `FramePuzzle`); Godot only shows and drives it.
- No overlay menus: any reading/clicking surface is a `WorldScreen` on an object.
- Develop lots, test little: compile + screenshot; full tests only before a commit that changes sim rules.

## Known rough edges
- You can drive through trees; houses and the shop are solid boxes you can't enter yet.
- Jobs and socket-fitting are instant (time passes, no activity).
- The HUD text strip is still an overlay (to become calendar, cash tin, pager).
- The frame is a printed work sheet on the board, not real terminal blocks and wires.
- Economy is untuned.
