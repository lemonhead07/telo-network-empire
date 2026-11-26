# Towns and roads: making them line up, and adding new ones

*Plan written 2026-11-26 in the LOOK1 dev session, at the owner's request ("plan for more towns with the roads and how to make them line up properly and building new ones for future"). Nothing here is built yet. Items RD1–RD3 are in `BUILD_PLAN.md`; the owner's questions are at the end.*

## 1. How places and roads are made today

- **Places** are `Site`s in `src/Telo.Sim/Planet/PlanetGen.cs`: a direction on the planet, a radius and a height. Each is found by its own search (town: gentle coastal grass; farm: 0.9–1.6 km out; villages: 450–1,400 m out, clear of the others; Millbrook past the farm; Halden on the flats; Farland raised out of the sea). Each new one is added **last**, so nothing found before it moves. The ground inside a site is flattened.
- **Streets** are a hand-written list in the game layer, `TownBuilder.Streets`: every street runs east–west (or north–south) in a frame that comes from the planet's axis, at an offset from the site's centre.
- **Houses** (`game/content/lots.json`, 86 lots) stand at east/south offsets from their site's centre and face their street.
- **Roads** (`PlanetGen.BuildRoad`) are a **straight line from one site's centre to the other's**, sampled every 20 m. Their height is a moving average of the ground (cut and filled 38 m either side), pinned to the two sites at the ends. The drawn asphalt (`TownBuilder.BuildRoads`) starts **0.75 of the way out from each site's centre**, wherever that happens to be.
- **The van's road map** (`World/RoadNet.cs`) joins each road's end to the nearest street point within 150 m that it can drive straight to, or failing that to the nearest point at all (through whatever is in the way).

## 2. What goes wrong

Because a road is a straight line between centres, where it arrives has nothing to do with where the streets are. Measured for seed 1983 (where the asphalt starts, in each place's own street frame; a street end is where it should be):

| Road | End | Where the asphalt starts | Nearest street end | Gap |
|---|---|---|---|---|
| Town to the farm | town | east 88, south −47 | Church Lane's east end (84, −40), placed by hand to meet it | ~8 m, fine |
| Town to Eastgate | town | east −100, south 9 | Main Street's west end (−112, 16.5) | ~14 m, comes in from the side |
| | Eastgate | east 59, south −8 | High Street's east end (48, 8) | ~19 m |
| Town to Wrenfold | town | east −66, south −79 | Church Lane (south −40) | **~39 m, behind Church Lane's back gardens** |
| | Wrenfold | east 41, south 45 | Wrenfold Green (south 8) | **~37 m, through the south row's gardens** |
| Town to Kettle Cross | town | east 18, south 99 | Main Street (south 16.5) | **~80 m, behind Main Street's south gardens** |
| | Kettle Cross | east −10, south −59 | Kettle Cross Row (south 8) | **~67 m, behind the north row** |
| Town to Highmoor | town | east −85, south −50 | Mill Row / Church Lane corner (−86, −40) | ~10 m, fine |
| | Highmoor | east 55, south 22 | Highmoor Street's east end (60, 8) | ~15 m |
| Town to Halden | town | east −74, south 76 | Main Street (south 16.5) | **~60 m, behind the gardens** |
| Farm to Millbrook | Millbrook | east −47, south −37 | Mill Lane's top (−41, −36), placed by hand | ~6 m, fine |
| Eastgate to the quay | Eastgate | east 33, south 58 | High Street (south 8) | **~50 m, behind the back fences** (on purpose, to miss Kestrel Point) |
| Farland to its quay | Farland | east −8, south −69 | Harbour Lane's top (−5, −56) | ~13 m |

Pictures: `--shots=roads` (below) looks straight down on each of these.

Other things the straight lines cause:
- **Steep roads.** The Highmoor road climbs at **22.6%** in places (a van would struggle; a real road would wind). The others are 3–9%.
- **Spokes, not a network.** Every village's road starts at the town's centre, so there are no junctions: roads out of town overlap inside it, and a new place can't join the road that already passes near it.
- **Every good join is hand work.** Church Lane, Mill Lane and Harbour Lane were each placed afterwards to meet their road. A new village means a programmer reading numbers like the table above.
- **Streets live in the game layer**, so the sim (which builds the roads) can't aim for them.

## 3. The goal

1. **Every road meets a street end**, along the street's own line: the asphalt carries straight on into the street, with no grass gap and no gardens in the way.
2. **Roads follow the land**: they bend round hills and keep under about 10% (a hairpin or two up to Highmoor), stay off beaches and out of other places' gardens.
3. **Roads make a network**: a new place joins the nearest road or street with a junction, instead of running a new spoke all the way from the town's centre.
4. **A new town is data**, not a programming job: a rule for where it goes, a layout, the names of the people who live there, and a project that opens it. The checks say whether it lines up.

## 4. How

### A. Streets and gates become data (in the sim)
- New `game/content/places.json`: for each site, its **streets** (today's `TownBuilder.Streets`, unchanged numbers) and its **gates**: the street ends a road may arrive at, each with the direction it faces out of town. Example: Wrenfold Green's east end, facing east.
- `ContentDb` loads it and checks it (every lot fronts a street, every road end has a gate, no gate opens into a garden). `PlanetGen` takes the gates (an optional constructor argument, so tests that only need terrain still work), and `TownBuilder`, `RoadNet`, `Hearing` and `TownDressing` read streets from content instead of their own list.
- Saves are not touched: nothing positional is saved (places are rebuilt from the seed and the content).

### B. A road arrives along its gate
- A road is built **gate to gate**, not centre to centre. Its last 40 m at each end run straight out along the gate's direction (so the asphalt continues the street), then it turns towards the other place.
- The drawn asphalt starts **at the gate**, not at 0.75 of the radius. Where a road meets a street end the street's asphalt and the road's overlap by a metre (no seam).
- **Choosing a gate:** the place's gate that faces most nearly towards where the road is going, unless that one is taken; a town can have several. Where no gate faces the right way (Kettle Cross: the road comes from the north, the street runs east–west), the layout gets a **lane** through a gap in the row (as Millbrook's Mill Lane and Farland's Harbour Lane already do), which becomes the gate.

### C. Roads are routed, then graded
- A path search (A*) over a 10 m grid laid on the ground between the two gates, deterministic (pure maths, the sim's `DetRng`, fixed tie-breaks: same seed, same roads). Its costs: slope (steeply rising cost above 8%), the sea and beaches (forbidden), other places' gardens (forbidden), a small cost per bend, and a **discount for running along an existing road**, which is what turns spokes into a network.
- Smooth the path (a few rounds of corner-cutting), then grade it: heights from the moving average as today, with a **10% cap**: where the ground is steeper than that, the road cuts or fills, and if it still can't make it the search's slope cost sends it the long way round (Highmoor's hairpins).
- **Junctions:** where a new road joins an existing road or street, the join is a point both share. `RoadNet` links them; `TownBuilder` draws a small paved patch so the corner is filled.
- Cost to the game: a few thousand grid cells per road when the planet is built (milliseconds); nothing per frame.

### D. A new town from a recipe
- **Where:** a rule in `places.json`, not a new search function each time: `{ "near": "farm-hollow", "distance": [400, 900], "ground": "coastal" | "hilltop" | "valley" | "flat", "clearOf": 420 }`. `PlanetGen` runs the one search it already has in five variants, with these numbers. New places are still **added last** (append-only), so adding one never moves another.
- **Layout:** a template: *street village* (one street, 8–12 houses, a gate at each end: Wrenfold today), *crossroads* (two streets crossing, four gates), *green* (houses round a green), *harbour* (a quay street and a lane up), *market town* (two streets and a square: Millbrook today). The template gives street lines, gates and plots (house positions); the site's **bearing** turns the whole layout so its main street lies along the road that reaches it (the classic English street village: the road *is* the high street).
- **People:** the households stay hand-written in `lots.json` (the Last Mile is named people, never generated): the template says how many plots, the data says who lives on each.
- **Opening it:** a project in `projects.json` (`needsProject` on its lots), as today.

### E. The checks
- **Tests** (`tests/Telo.Sim.Tests/PlanetTests.cs`): every road's ends sit on a gate (within 1 m) and leave along it (within 10°); no stretch is steeper than 10%; no road crosses the sea, a beach or another place; every junction is shared by both roads; the same seed gives the same roads. One test per rule, cheap (the planet is built once).
- **Pictures:** `--shots=roads` (built in LOOK1, `game/scripts/LookShots.cs`) looks straight down on every road end; after the work, every picture shows asphalt running into a street.
- **The Act 5 script player** (in `dotnet test`): routed roads are longer, so travel times change. Its per-act hours and cash bands must still pass; Act 3 is at 7.5 h of its 8 h limit, so watch it.

## 5. What moves, and the risk

- Houses, streets and project sites do **not** move (their offsets stay). Roads move, and where a road's cut-and-fill corridor moves, the ground near it changes height a little.
- Travel times change (longer, winding roads), which is why this is **gameplay-lane work** under `docs/BRANCHES.md` rule 5, whichever lane does it, and why the Act 5 script player is the gate. If Act 3 goes over 8 h, the fix is in the bot (keep kit on the van for far jobs; IDEAS inbox), not in straighter roads.
- Fields and hedgerows (`TownDressing.Fields`) are laid out round sites and skip roads; they will rearrange themselves round the new roads (looks only).
- The bot drives by `RoadNet`; with gates and junctions in the data it gets simpler (no more "nearest clear point" guess).

## 6. Order of work (racing to market)

The demo (Act 0) uses only the town and the farm road, which already line up, so none of this blocks the store page or the demo. Early access (Acts 0–5) does show Wrenfold, Kettle Cross, Eastgate and Halden, so the joins should be fixed **before 11 March**; routing and recipes can wait for the updates after launch, which is when new towns arrive anyway.

| Item | What | When | Lane |
|---|---|---|---|
| **RD1** | Streets and gates as data (A), roads arrive along a gate with the drawn asphalt starting there (B), lanes where a village needs one (Kettle Cross, the town's south side), junction patches. Roads stay straight in the middle. | Before launch: after ROOM2, before MK2 (the trailer shows roads) | Geometry session, run with the gameplay checks (`dotnet test`, `--smoke`) |
| **RD2** | Routed, graded roads and junctions (C): the Highmoor hairpins, a network instead of spokes. | After launch, before the first new town | Gameplay |
| **RD3** | Places as recipes (D): the rule, the templates, the bearing; then the first new towns. | After launch, with Act 6 content | Gameplay, then geometry for the looks |

**New towns already named in the design** (AGES.md), and what each would need: **Larkfield Estate**, **Crown Heights** and **Tannery Row** read as new quarters of the town (Act 4, "towns that grow": a crossroads or green template off a new gate on the town); **Northfield College**, **Copperhill Quarry** and **Easthaven Docks** as single big sites like the farm (one lot, a road and a yard); **Meridian** (Act 6, the capital on Farland) needs more land than the island has (IDEAS inbox, 2026-11-24): a second, larger island and a crossing; **Nadir** (Nez's city) is already placed at the antipode; **Cape Lantern** and **Rime Station** are single sites at the end of a long road (Act 7).

## 7. Questions for the owner

1. **Winding roads?** Routed roads are prettier and make the planet feel bigger, but every drive gets longer (perhaps 10–30%). Yes, or keep them mostly straight and only fix the joins (RD1 alone)?
2. **How many new towns, and when?** The plan assumes none before early access and the first ones with Act 6 (mobile masts need places to cover). More hand-work villages in Acts 1–2 would lengthen those acts (they are 4.6 and 3.9 h today).
3. **Do towns grow visibly?** (AGES.md question A4.) If yes, templates should leave empty plots that fill in as the act goes on.
4. **The Ring Highway** (AGES.md, Act 5): a real road right round the continent would be the network's spine. Is it a road you drive, or a project you build (with its own lanes and junctions)?
