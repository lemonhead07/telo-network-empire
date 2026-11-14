# Next Session — start here

*Left off: 2026-11-12, commit "the switchboard". Everything is pushed; the working tree is clean.*

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

**Owner playtest 2, 2026-11-12 (39 minutes, reached 15 Jan 1983):** 13 subscribers (the whole town), technician hired, farm line built. Liked the pacing and the "quiet day" line. Problems: too slow on the calendar; the farm line "didn't feel like I accomplished anything" and paid almost nothing; odd jobs too shallow; wants a way to skip through stages and ages. Done in response: god keys (F1–F4, F6), connection fees, the rural lines grant, sleeping through quiet days, hands-on odd jobs.

**The biggest gap now: the town runs out.** 12 houses, a shop and a farm is about 40 minutes of content. After that the only thing left is waiting for June 1984. The next age (Act 1, the bulletin board) has data but no gameplay. That, or a second settlement, is what makes "overlap and pace increase between rounds" real.

## Plan for next time
The switchboard is built (2026-11-14); the owner wants to **work on other ideas next**, so ask what they are before picking from this list. The agreed backlog, from `docs/DESK_AND_DAY_DESIGN.md`:

1. **Desk objects that replace the text strip** (cash tin, job spike, card index, telex roll); PIP and highlights explain tasks as they are done.
2. **Training** (a person earns a new kind of ticket by coming along for three of them) and **each person's own visible place**.
3. Appointments, night rate for the mail, light tiredness.
4. Phase D of `docs/EXPANSION_DESIGN.md` (invoices, late payers, mixed phone messages, bookkeeper, receptionist), built as objects.
5. The art track (`docs/ASSET_LIST.md`).

Things to check with the owner after they play this build:
- The switchboard: is leaning in and clicking with the mouse comfortable? Is it clear which port a tag names (the `›3` on the tag)?
- Is a day of 8 real minutes the right length now that nothing jumps the clock? (`GameRoot.DayLengthSeconds`)
- Hold times: a step is held for as long as its game minutes say, between 0.8 and 6 seconds.
- Handing Jory tickets every morning is deliberate friction until the depot ($150 down). Fun or a chore?
- Half the town being out from nine to five on weekdays.

## Rules to keep
- Outcomes and defects are decided in `src/Telo.Sim`; Godot only shows and drives it.
- No overlay menus: any reading/clicking surface is a `WorldScreen` or an object in the world.
- Develop lots, test little: compile + screenshot; full tests only before a commit that changes sim rules.

## Known rough edges
- Kit you put down, and odd jobs or orders in progress, are forgotten on save/load (the install itself is now saved).
- Pole line: poles go in fixed sites in order; you can't choose the route. Wires are straight (no sag).
- Repair is a single press at the door (needs the toolbox with you).
- The technician is invisible; you only see the results.
- Act 1 itself still waits for the June 1984 announcement; the first hire no longer does.
- House interiors are one template; no residents visible. Markers show through walls indoors.
- You can drive through trees; the shop and farm are solid boxes.
- The HUD text strip is still an overlay. The frame is still a printed work sheet.
