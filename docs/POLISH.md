# The Polish Bar: looks finished, and doing something is doing something

*Idea session, 2026-11-26. The owner chose launch plan B, keeping 11 March, and asked for "more polished before I push anything out, like fully connected textures n things, and actual tasked gameplay so doing something is actually doing something". Decisions D56–D58. Items LOOK1–2, ROOM1–2 and HO1–4 in `BUILD_PLAN.md`; dates in `docs/LAUNCH.md` §4.*

## 1. The bar

Something is **polished** when a stranger would not guess it's unfinished:

**It looks finished (D57)**
- Light that flatters: soft shadows, ambient occlusion, haze in the distance, warm mornings and evenings.
- Land that reads as land: hills that shade smoothly instead of showing big flat triangles, grass that varies, rock on steep slopes.
- Surfaces made of something: roof tiles, brick, weatherboard, asphalt, carpet, floorboards, concrete.
- Rooms that are lived or worked in: no bare grey rooms, no labelled boxes in the places a player goes in the first hours.
- The style stays: low-poly and flat-shaded, a few colours per object. Every one of the 135 models stays. "Finished" comes from light, land, surfaces and furniture, not from a new art style.

**Doing something is doing something (D58)**
1. **You do it where it happens:** at the foot of the pole, in the trench, at the rack. Not at one marker beside a growing box.
2. **The thing is in your hands:** the part, the cable end, the test set. You can see it.
3. **Each step is a verb on a part** (below), and you can tell which part.
4. **Each step changes the world for good:** a pole stands, a wire hangs, a lamp lights, a trench is dug.
5. **A test tells you something:** a lamp, a tone, a needle. If you bodged it, the test shows it and you go back to that part. Never a fail screen (failure costs time, never progress).
6. **Waiting is something you can see:** tape round wet concrete, a letter on the mat, a ship on the horizon.
7. **No bare progress bars.** Holding E still takes time, but at a point on the thing, and the piece moves while you work it.

**Sounds alive** (EA7) and **nothing floats** (D51, IW1) are part of the bar too; they have their own items.

## 2. The verbs

All 119 project steps in Acts 0–5 fall into a few kinds of work. Build each kind once; then a step is data (`"verb"` in `projects.json`), not code.

| Verb | Steps | How it plays | Example |
|---|---|---|---|
| **Fasten** | 39 | Work points on the part (HO1): each bolt, clamp or screw is a short hold, and the part settles straighter with each one. Bodging skips points: the part stays crooked. | Stand the cabinet; fit the terminal box |
| **Run** | 26 | Take the cable end (drum in your hands), walk the route point to point (pole, duct mouth, trench marker); the cable lays behind you and is clipped at each. Digging a trench is the same walk with a spade. | String the wire up Church Lane; trench to Halden |
| **Test** | 14 | Clip the test set on and read it: a green lamp, a tone, a needle. Bad means go back to the part that's wrong. | Test every pair; watch the lamps come up |
| **Connect** | 13 | Take each end to its terminal by its tag, like the switchboard. In a hurry, one can go on the wrong terminal (crossed); the next test finds it. | Wire the cabinet; splice the last fibre |
| **Carry** | 10 | Pick the part up where the van or the lorry left it, walk it to its ghost outline, set it down. | Carry the rack into the depot |
| **Wait** | 8 | Nothing to hold: a visible thing in the world and a date on the signboard; a letter or a phone message when it's over. | Wait for the council to pass the wayleave |
| **Hand over** | 7 | Walk to a person with the form or the thing in your hand and give it to them (the same as handing Jory a ticket). | Fill in NezTel's leased-line form; show the Thursday club |
| **Climb** | 1 | Ladder against it, climb, and the points at the top come within reach. | Climb and bolt on the dish |
| **Remove** | 1 | Work points that take something away (the reverse of fasten). | Clear out the old cold room |

## 3. Where the game is today (2026-11-26)

**Already hands-on:** house installs (steps at points on the house, with kit), the switchboard, odd jobs inside houses, catalogue orders, the Board's terminal, carrying kit.

**Bare holds still in the game:**
| What | Today | Fixed by |
|---|---|---|
| 32 projects in Acts 0–5 (119 steps, about 250 holds) | Stand at one spot, hold E, a box grows | HO1 (points), HO2 (Acts 0–1 verbs), HO3 (Acts 2–5 verbs) |
| Pole line: plant a pole, string a span | One hold each (kit needed) | HO1 |
| A line card into the rack | One hold | HO1 |
| Repairs | One hold at the front door | HO4 |
| Odd jobs at Sol's and the farm | One hold at the door | HO4 |
| Buying at the Yard, the ferry ticket, the ladder | Short holds | Fine as they are: talking and travelling, not work |

**The parked branch `claude/world-building-system-fmjwd3`** (2026-11-19, never merged) already turns every project step into 2–5 work points named in the model (`wp-…` empties), with the site built in stages and the current step shown as a ghost (`docs/WORK_POINTS.md` there). HO1 ports it rather than rebuilding it.

**Looks unfinished:**
- Hills are big flat triangles; grass is one colour; the light is flat at midday (`docs/img/village-wrenfold.png`).
- No textures anywhere: `game/assets` holds no images; every surface is flat colour.
- The data centre's rooms are grey boxes with a few props in them (`docs/img/halden-rooms.png`); house interiors have placeholder furniture; your room is built from plain boxes.
- Still boxes in the first hours: Sol's Electronics and Hollow Farm (solid), the Yard (`yard.counter`, `yard.bay`, `kit.parts-crate`), the title letter and PIP's handset, staff figures, NezTel's plaques, the ferry.

## 4. What has to meet the bar, and when

| Date | What must meet the bar |
|---|---|
| **11 Dec** (store page submitted) | The places in the store pictures: the town, your room and the switchboard, a village, the data centre at night (LOOK1, then MK1). Pictures can be swapped after the page is live. |
| **1 Feb** (demo submitted) | All of Act 0, the demo: the look (LOOK1–2, ROOM1–2), every task (HO1, HO2, HO4), sound (EA7), nothing floating (IW1). |
| **11 Mar** (early access) | Act 1 the same as Act 0 (the first hours are when players decide on a refund). Acts 2–5: no progress bars anywhere (HO1), and verbs as far as HO3 gets. Anything left becomes a free update. |

Both lanes work at once (`docs/BRANCHES.md`): **geometry** takes LOOK1 → MK1 → LOOK2 → ROOM1 → ROOM2 → MK2; **gameplay** takes EA7 → HO1 → IW1 → HO2 → HO4 → LK1 → DEMO1 → HO3.
