# Next Session — start here

*Left off: 2026-11-12, commit "walk-in houses and the hands-on install". Everything is pushed; the working tree is clean.*

## Where things stand
- Playable: `godot --path game`. Planet, town, van, farm road, odd jobs, door-knocking, modems, frame, in-world screens.
- **New:** the 12 houses are hollow. When a customer is waiting for their line, knock and they let you in (the door swings open).
- **New:** the install is a physical job in six stages, shown as yellow markers you walk up to:
  outside (drop wire down the wall in 3 clips, junction box), then inside (wall socket, 3 staples along the skirting, plug in the modem, lift their telephone to test, sign the job sheet by the door).
  - `[E]` does a step properly (a few minutes of game time each). `[R]` bodges a stage in one minute and leaves a latent defect.
  - About a third of houses hide a fault that only the dial-tone test finds; signing off untested leaves it in the wall.
- Cloud sessions now use the `telo-network-empire` environment (.NET, Godot 4.7.1, Blender 4.0 preinstalled). Screenshots there need `xvfb-run -a` in front of the godot command.

## Plan for next time (in order)
1. **Carrying things.** Van cargo (modems, cable drum, ladder, toolbox) that you pick up and carry by hand; the drop-wire stage needs the drum and the top clip needs the ladder. Two-handed items slow you down.
2. **Odd jobs as hands-on tasks** using the same verbs (open a casing, swap a part, test), reusing `InstallSite`-style click points.
3. **Sound for the install** (synthesised): clip click, drill, staple, dial tone, the "nothing" of a dead line.
4. Residents: a placeholder person standing in the room who says one line when you arrive and when you sign off.
5. Enterable shop (buy at a real counter indoors) and farmhouse.

## Rules to keep
- Outcomes and defects are decided in `src/Telo.Sim`; Godot only shows and drives it.
- No overlay menus: any reading/clicking surface is a `WorldScreen` or an object in the world.
- Develop lots, test little: compile + screenshot; full tests only before a commit that changes sim rules.

## Known rough edges
- A half-finished install is forgotten on save/load (it restarts; nothing is lost but time).
- House interiors are one template with the outside wall colour inside; no residents visible.
- The `$`/`▼` markers show through walls when you are indoors.
- You can drive through trees; the shop and farm are solid boxes you can't enter yet.
- Odd jobs are still instant (time passes, no activity).
- The HUD text strip is still an overlay (to become calendar, cash tin, pager).
- The frame is a printed work sheet on the board, not real terminal blocks and wires.
- Economy is untuned.
