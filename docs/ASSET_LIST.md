# Asset List and Trailer Plan

What needs to be modelled, textured and animated to take TELO from placeholder boxes to something that can be shown.
Written 2026-11-12 after the owner's first 10-minute playtest ("pacing good, missing polish").

**How assets get made.** Models are built in Blender from Python scripts (`blender -b --python tools/<name>.py`) and exported as `.glb` into `game/assets/`, so every model can be regenerated and tweaked from code. Textures are small painted or generated images (128–512 px). Blender 4.0 is installed in the cloud environment.

**Style target.** Low-poly, flat-shaded, early-1980s small town: warm tungsten light indoors, sun-bleached paint outdoors, slightly toy-like proportions. A few colours per object, small readable details (a nameplate, a dial, a sticker) rather than fine geometry. Budget: props 50–400 triangles, buildings 500–2,000, characters about 1,000.

Priority: **A** = the demo needs it, **B** = the trailer needs it, **C** = later.

## 1. Characters

| Pri | Asset | Notes |
|---|---|---|
| A | Townsperson base body | One rigged low-poly person; variations by colour, hair, hat, height. Idle, walk, talk, wave, knock-answer. |
| A | 12 residents | Variations of the base for each house in `lots.json` (Dot Brennan, Chidi Achebe, the Lindqvists, Tobi Okafor, Hilde Moss, Gus Pike, Priya Duarte, the Kowalskis, Marek Thorne, Nell Hale, Eamon Reyes, Rosa Vasquez). One distinguishing item each (cardigan, headphones, apron…). |
| A | The technician | Overalls, tool belt, clipboard. Seen in your room and at the house they are working on. |
| A | Sol (shopkeeper) | Behind the counter at Sol's Electronics. |
| B | Ruth Adeyemi | Farmer; stands at the farm gate watching the pole line arrive. |
| B | Player hands | Hands and forearms holding each piece of kit (first-person view). |
| C | Bookkeeper, receptionist | The next two hires. |
| C | Council worker | Leaning on the road barrier. Never working. |

Each person needs a **speech bubble** above their head (same look as PIP's bubble) and simple walking between a few points (home, street, shop).

## 2. Buildings

| Pri | Asset | Notes |
|---|---|---|
| A | House, 3 exterior variants | Replace the box house: porch, door with a **nameplate and house number** (replaces the floating name text), letterbox, window frames, gutter, chimney. Same footprint (8 × 7 m) so the install points still line up. |
| A | House interior kit | Skirting board, wallpaper walls, carpet or boards, ceiling light, door frames. One hall/living-room layout to start. |
| A | Your rented room | Desk, frame backboard with real terminal blocks, camp bed, window, shelf of radio parts. |
| A | Sol's Electronics | Shopfront with sign, display window, a counter you walk up to indoors. |
| B | Hollow Farm | Farmhouse, barn, silo, fence, gate. |
| B | Main Street dressing | Pavement, kerbs, street lamps, a postbox, a bench, a phone box, a bus stop. |
| C | The exchange building, 1 Main Street | Later acts. |

## 3. Vehicles

| Pri | Asset | Notes |
|---|---|---|
| A | The service van | Proper body, wheels that turn, opening rear doors, roof rack, tailboard, company lettering. |
| C | Residents' cars, a tractor | Parked dressing. |
| C | Boat, light aircraft | Later travel. |

## 4. Kit and tools (things you carry)

| Pri | Asset | Notes |
|---|---|---|
| A | Cable drum | Wooden spool with black cable. |
| A | Ladder | Wooden, two-section. |
| A | Toolbox | Red metal, cantilever lid. |
| A | Boxed modem and the modem itself | Beige box with lights; cardboard carton with a printed label. |
| A | Line pole | With crossarm, insulators and climbing steps. |
| B | Tone probe, screwdriver, staple gun, hand drill | Shown in the hand during install steps. |

## 5. Install and network parts

| Pri | Asset | Notes |
|---|---|---|
| A | Wall clip, eave bracket | Drop-wire fixings. |
| A | Junction box (open and closed) | Outside wall. |
| A | Wall socket (backplate and faceplate) | Indoors. |
| A | Cable staple, cable runs | Skirting cable; a drooping drop wire to the pole. |
| A | Telephone, rotary | The customer's own phone; lift the handset to test. |
| A | Frame terminal blocks, jumper wire, labels | For making the frame physical. |
| B | Sagging overhead line | Replace the straight wire between poles. |

## 6. Desk and room props (replacing the on-screen text)

| Pri | Asset | Notes |
|---|---|---|
| A | PIP's handset | The talking device: small screen, antenna, a light that pulses when PIP speaks. |
| A | Answering machine | Blinking light, cassette window. |
| A | Company notebook | Open book with ruled pages. |
| A | Wall calendar | Shows the date (replaces the date text). |
| A | Cash tin | Shows your money. |
| B | Pager, fax roll | Replace the "what next" line and the event feed. |
| B | Job sheet clipboard | By the customer's door. |

## 7. World and nature

| Pri | Asset | Notes |
|---|---|---|
| B | Trees (3 kinds), bushes, rocks | Replace the current gems-on-sticks; trees should block the van. |
| B | Road barrier, cones, council sign | The "road closed" gate. |
| B | Fences, hedges, garden gates, washing lines | Around houses. |
| C | Birds on the wire | Small touch for the trailer. |

## 8. Textures and materials

| Pri | Asset | Notes |
|---|---|---|
| A | Wall paints (6), brick, render, weatherboard | Tiling, 256 px. |
| A | Roof tiles, slate, corrugated iron | |
| A | Wallpaper (4 patterns), carpet (3), floorboards, lino | Very 1983. |
| A | Asphalt with centre line, pavement, grass, dirt track | |
| A | Wood (pole, ladder, drum), painted metal (van, toolbox), beige plastic (modem, phone) | |
| A | Signs and labels | Shop sign, van lettering, house numbers, nameplates, "ROAD CLOSED", modem carton, job sheet print. |
| B | Sky gradient, clouds | |
| B | Window glass with a hint of interior | So houses don't look empty from outside. |

## 9. Icons and bubbles

| Pri | Asset | Notes |
|---|---|---|
| A | Speech bubble | Done in code for PIP; reuse above characters' heads. |
| A | World markers | The ▼, $, ! and work-point markers as proper icons (a pin, a coin, a warning bell, a glowing outline on the part). |
| B | Key prompts | E / R / G as small key caps rather than "[E]". |

## 10. Sound (synthesised in code)

Relay click, dial tone, dead line, modem handshake, staple gun, drill, clip snap, ladder rattle, van engine and door, door knock, door open and close, footsteps (grass, road, carpet), answering-machine beep, PIP's voice blips, cash tin, phone ring, room tone, birds.

## Suggested order

1. **House exterior with nameplate + van + the five carried items.** These are on screen for most of the first hour.
2. **Townsperson base + residents + speech bubbles + walking.** The town stops being empty.
3. **House interior kit + install parts + telephone.**
4. **Your room and desk props** (also removes the last on-screen text).
5. **Shop, street dressing, trees.**
6. **Farm and overhead line.**

---

# Gameplay trailer plan

**When.** After steps 1–4 above: textured houses, the van, at least two walking, talking residents, and sound. Length 45–60 seconds.

**How it gets made.** A `--trailer` mode in the game (like `--shot`): scripted camera moves and scripted player actions, rendered frame by frame to images, joined into a video with the synthesised soundtrack. It can be regenerated whenever the art improves.

**Shot list (draft).**

| # | Seconds | Shot | On-screen line |
|---|---|---|---|
| 1 | 0–5 | From orbit, the small planet turns; push in toward one town. | "1983." |
| 2 | 5–9 | Your rented room at dawn. The answering machine blinks. PIP's bubble: "…signal. Year reads 1983. That can't be right." | |
| 3 | 9–14 | First person: out of the door, residents walking past, one waves with a speech bubble. Into the van. | |
| 4 | 14–22 | The install, cut fast: ladder against the wall, clip, clip, clip, junction box, indoors, staples along the skirting, modem lights. | "Do the job by hand." |
| 5 | 22–26 | Lifting the customer's telephone. Dial tone. The resident's bubble: "It works!" | |
| 6 | 26–31 | The frame: jumper wires going on, a line going live. | |
| 7 | 31–36 | The technician arrives; the player watches them carry the ladder instead. | "Then hire someone to do it for you." |
| 8 | 36–40 | A `!` over a house. The technician shrugs. PIP's bubble: "I've made a note of the date." | "Every improvement creates a new problem." |
| 9 | 40–50 | Driving the farm road at sunset, a row of new poles behind the van, the last span going up to Hollow Farm, Ruth at the gate. | "Reach the people nobody else will." |
| 10 | 50–58 | Time-lapse hint: paper notebook becomes a green terminal becomes a dashboard; pull back to orbit, a few lights now on the dark side of the planet. | "TELO: Network Empire. 1983–2035." |

**Needed before filming that doesn't exist yet:** walking residents with bubbles, the technician as a visible person, sound, sagging wires, a sunset pass on the sky, and a way to record frames.
