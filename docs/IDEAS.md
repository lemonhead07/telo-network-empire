# Ideas

The parking lot. Dev sessions **add** one-liners to the Inbox and never build from here. Idea sessions shape them, then promote ready ones into `BUILD_PLAN.md` (see `docs/SESSIONS.md`).

## Inbox
*(one line each: date, idea, where it came from)*
- 2026-11-21 · [gameplay] **Act 2 now runs 6.4 h** (target 4–5): Highmoor plus the archive wait for the modem bank. If it drags in play, shorten Highmoor's gale wait or let the modem bank page be read sooner. From EA2f.
- 2026-11-21 · [gameplay] **A 56-row switchboard is cramped** (rows ~1.7 cm at full size). Grow the board taller per panel, or give each village its own small board on the wall beside it. From EA2f.
- 2026-11-21 · [gameplay] **Odd jobs are offered in villages you haven't wired yet** (Wrenfold on 4 Jan). Fine as a way to meet them first, or gate on `needsProject`. From EA2f.
- 2026-11-21 · [gameplay] **The bot waits 0.9 h in Act 2 for $120 to bolt on a panel.** Cash is tight there (good), but the phone could suggest an odd job instead of waiting. From EA2f.
- ~~2026-11-21 · [geometry] **The three new villages are copies of one street.**~~ Done 2026-11-21 (`tools/models/village.py`, `World/VillageLandmarks.cs`): Wrenfold's school and green, Kettle Cross's Drover's Arms and boatyard, Highmoor's chapel and weather station, behind the row of houses away from the road in. *Left:* a footpath from the street to each; Kettle Cross's sits in the village photo's foreground.
- 2026-11-21 · [gameplay] **Parked on `claude/world-building-system-fmjwd3` (2026-11-19, never merged):** EA3 Millbrook as a drivable town (a `PlanetGen` site + street + lots), EA5 project sites built in stages with work points per step (`World/WorkPoints.cs`, `docs/WORK_POINTS.md`, `ProjectSiteView`). Its switchboard models were taken into master on 2026-11-21; the rest conflicts with EA2f (`PlanetGen` site order, `lots.json`, `TownBuilder.Streets`) and would collide with DC2's Halden Flats site. Bring it over after DC2: Millbrook's site goes *after* Halden in the `PlanetGen` constructor, then rerun the Act 5 script player.
- 2026-11-21 · [gameplay] **The Data Centre already has models** (`tools/models/dc.py`, ids from `DATA_CENTRE_DESIGN.md` §12: `dc.rack-42u`, `dc.box-router` … `dc.hall-shell`, `person.dana`…). When DC2–DC9 put a thing in the world, use `Props.Make("dc.…", placeholder)` and the model appears; briefs (sizes, pivots, empties like `u01`, `port-1`, `site-core`, `screen-0..3`, `needle-pivot`) are in `props.json`. Don't add a second brief with the same id.
- 2026-11-21 · [gameplay] **Later ages have models waiting** (`tools/models/modern.py`): PIP's handset by era (`phone.brick`, `phone.flip`, `phone.touch`, each with a `screen` empty for the phone view), `room.flat-monitor`, `room.laptop`, `room.ops-wall`, `mast.small-cell-bench`, `mast.clock-tower-cell`, `shop.phone-shop`, `space.rocket`, `space.launch-pad`, `space.satellite`, `space.ground-dish`, `kit.drone`. Swap PIP's handset model when the interface tier moves on.
- 2026-11-18 · **Opening the neighbourhood further.** Houses are now 20 m apart with real gardens and fields round each settlement. Next steps if wanted: a village green or churchyard on Church Lane, a back lane between the gardens you can walk, collision on fences and hedges (needs the bot and van routes checked), lamps that light at dusk, a second road out of town. Note: the Act 2 cash band (D41) is on a knife-edge, and a 22 m spacing with Church Lane at −46 tipped it to $319. From the owner ("open up the neighbourhood").
- 2026-11-18 · **The calendar now runs fast again: Act 5 arrives in Dec 1983.** With the pace fixed, a year of play covers the whole early access. Decide whether the dates should lag (each act's page only readable from a real year, or Echo biting harder when you build early) or whether racing ahead of history is the time-travel joke. From EA2e.
- 2026-11-18 · **The bot is a perfect player.** Measure a human against it once (one evening of the owner playing, timed per act) so the 20–30 h target can be set for people, not the script. From EA2e.
- 2026-11-16 · **Upgrades happen in the world, unexplained but always visible.** No screens explaining how things work: when tech improves (an Almanac page built, a stage automated), the change simply appears on the objects (new box on the pole, a different lamp, a faster modem's lights, a tidier frame). The player may not be told, but must always be able to see it. Fits D26 (objects, not panels) and the "world tells" rule in TECH_TREE §4. From the owner.

## Shaped
*(talked through, not ready to build; link to TECH_TREE / HANDOFF sections where relevant)*

- 2026-11-15 · **Tech tree & hand-off ladder** shaped into `docs/TECH_TREE.md` (8 branches × 4 tiers, 96 tasks, 12 gameplay mechanics). Open questions in its §6.
- 2026-11-15 · **Model requests and job variety** shaped into `docs/MODELS_AND_VARIETY.md` (D30–D33). Switchboard twists (mouse nest, tag in someone else's hand, empty hook, blown lamp, caller mid-jumper) wait for the full kit model. Other families to shape later in the same form: pole line, odd jobs, catalogue products.
- 2026-11-15 · **The Ages** shaped into `docs/AGES.md`: Acts 0–7 at planet scale, 128 jobs in the seven verbs, places per age, Nez per age, and the systems to prepare (§6). Open questions A1–A6 in its §8.

- 2026-11-15 · **Bookings, service and NezTel** shaped into `docs/BOOKINGS_AND_NEZTEL.md` (D35–D39). Open: B1–B3 in its §6. Story amended same day: you are Nez (amnesiac, time running backwards), PIP's notes are your own invention, NezTel is your worse future, the stakes are the Blackout (D35, D40).

- 2026-11-18 · **Shop days and the Clue Card** shaped into `docs/SHOP_DAYS.md` (D45, provisional): the day opens and closes at the shop sign, the clock follows the work, before/after hours are the open world, cash register at the counter, the shift grows day → week → month, a Clue Card for the line of progression (33 steps through Act 5). Owner questions in its §11.

- 2026-11-21 · **The Data Centre ends early access** shaped into `docs/DATA_CENTRE_DESIGN.md` (D46–D50): new Act 5 "The Core" on Halden Flats, the pin board (plan by string, fairy lights when lit), the easy rack, crossed patches and follow the lights, Dana / Moss / the night shift, migration night and the lit-board finale. Mobile moves to Act 6. Open: its §13 (Halden vs Eastgate, a look-only pin board from Act 2, second landing vs leasing Nez's cable, migration night length, years).

## Parked
*(decided not now; say why)*
