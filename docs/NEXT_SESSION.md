# Next Session — start here

*Left off: 2026-11-12, commit "the pole line to Hollow Farm". Everything is pushed; the working tree is clean.*

## Where things stand (the demo loop so far)
1. **By hand:** sign up neighbours (answering machine, door-knocking), buy modems at Sol's, drive to the house, carry the kit from the van, do the six-stage install, wire the frame. Odd jobs for cash.
2. **Hire:** with 4 paying subscribers and $300, the company notebook lets you hire a junior technician ($90 a month). The "what next" line tells the player when they can.
3. **Automated:** the technician connects one waiting customer a day without you, as long as there are modems in stock. They are right 85% of the time.
4. **The new problem:** wrong installs (theirs, or your own bodges) surface weeks later as faults. A fault keeps hurting that customer until you drive out with the toolbox and fix it. `!` marks the house.

Van kit: `[E]` pick up, `[G]` put down, use the van to put it back. Drum and ladder are two-handed and slow you. Parking close matters.

5. **New infrastructure:** with installs running themselves, the "what next" line sends the player to build the pole line to Hollow Farm: 14 poles beside the farm road. Take a pole off the van roof, plant it at the yellow marker ($15, 30 min), then string each span from the blue marker (cable drum and ladder, 15 min). The last span reaches the farmhouse and connects Ruth Adeyemi. The old "Connect them ($350)" button is gone for her: this line can only be built.

**The first hour is staged.** A new game starts with one phone message and nothing else: no odd jobs, no door-knocking, no Almanac notices, a short HUD. Each thing appears only after the one before has been done once: answering machine → install → frame → first odd job → door-knocking → everything. Later systems were already gated (the hire needs 4 subscribers, faults come weeks later, pole-line markers appear after the hire).

## Plan for next time (in order)
1. **Repairs hands-on:** the fault names a stage; fix it at that point on the house (reuse `InstallSite` click points) instead of one press at the door.
2. **The technician as a person:** a named placeholder figure in your room and at the house they are working on, with one line of dialogue.
3. **Ruth's farmhouse install:** make the farmhouse enterable and finish her line with the same six-stage install instead of connecting on the last span.
4. **Second automation stage hooks:** bookkeeper (billing) and receptionist (support) can be bought but change nothing yet. Give each a manual verb first, then the hire.
5. **More to build:** a second pole-line destination (fishing village or mountain hamlet) needs a new `Site` and road in `PlanetGen`.
6. Odd jobs hands-on; synthesised sound; enterable shop; economy pass.

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
