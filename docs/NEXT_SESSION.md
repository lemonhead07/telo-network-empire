# Next Session — start here

*Left off: 2026-11-12, commit "PIP bubbles, held-back world, asset list". Everything is pushed; the working tree is clean.*

## Where things stand (the demo loop so far)
1. **By hand:** sign up neighbours (answering machine, door-knocking), buy modems at Sol's, drive to the house, carry the kit from the van, do the six-stage install, wire the frame. Odd jobs for cash.
2. **Hire:** with 4 paying subscribers and $300, the company notebook lets you hire a junior technician ($90 a month). The "what next" line tells the player when they can.
3. **Automated:** the technician connects one waiting customer a day without you, as long as there are modems in stock. They are right 85% of the time.
4. **The new problem:** wrong installs (theirs, or your own bodges) surface weeks later as faults. A fault keeps hurting that customer until you drive out with the toolbox and fix it. `!` marks the house.

Van kit: `[E]` pick up, `[G]` put down, use the van to put it back. Drum and ladder are two-handed and slow you. Parking close matters.

5. **New infrastructure:** with installs running themselves, the "what next" line sends the player to build the pole line to Hollow Farm: 14 poles beside the farm road. Take a pole off the van roof, plant it at the yellow marker ($15, 30 min), then string each span from the blue marker (cable drum and ladder, 15 min). The last span reaches the farmhouse and connects Ruth Adeyemi. The old "Connect them ($350)" button is gone for her: this line can only be built.

**The first hour is staged.** A new game starts with one phone message and nothing else: no odd jobs, no door-knocking, no Almanac notices, a short HUD. Each thing appears only after the one before has been done once: answering machine → install → frame → first odd job → door-knocking → everything. Later systems were already gated (the hire needs 4 subscribers, faults come weeks later, pole-line markers appear after the hire).

**PIP talks as you work.** One short speech bubble the first time you do each thing (`PipTips.cs`). The "▶ next" line is now short. The desk (Almanac, notebook) opens once the basic round is learned; the farm road has a council barrier until the first hire.

**Owner playtest, 2026-11-12 (10 minutes):** pacing good; still a lot of information; wants polish and each part reviewed in more detail; bubbles are welcome; floating name text on doors should go once there are real models; wants models, textures, walking talking residents, then a gameplay trailer. See `docs/ASSET_LIST.md`.

## Plan for next time (in order)
1. **Start the art** from `docs/ASSET_LIST.md`, step 1: a Blender-scripted house exterior with a nameplate (removes the floating door text), the van, and the five carried items. Set up `tools/` for Blender scripts and `game/assets/` for `.glb` output, and update the "no imported models" line in `CLAUDE.md`.
2. **Residents:** one base person, 12 variations, speech bubbles above heads, simple walking between home, street and shop.
3. **The technician as a person**, and hands-on repairs at the faulty point on the house.
4. **Sound**, synthesised.
5. **Review pass, part by part** (the owner asked for more detail in each): install step feedback, frame readability, shop counter, odd jobs hands-on.
6. Then the trailer mode (`--trailer`), following the shot list.

## Rules to keep
- Outcomes and defects are decided in `src/Telo.Sim`; Godot only shows and drives it.
- No overlay menus: any reading/clicking surface is a `WorldScreen` or an object in the world.
- Develop lots, test little: compile + screenshot; full tests only before a commit that changes sim rules.

## Known rough edges
- A half-finished install and anything you put down are forgotten on save/load (the job restarts, the kit is back on the van).
- Pole line: poles go in fixed sites in order; you can't choose the route. Wires are straight (no sag).
- Repair is a single press at the door (needs the toolbox with you).
- The technician is invisible; you only see the results.
- Act 1 itself still waits for the June 1984 announcement; the first hire no longer does.
- House interiors are one template; no residents visible. Markers show through walls indoors.
- You can drive through trees; the shop and farm are solid boxes.
- The HUD text strip is still an overlay. The frame is still a printed work sheet.
