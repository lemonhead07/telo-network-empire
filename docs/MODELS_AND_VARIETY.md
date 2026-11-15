# Model Requests and Job Variety

*Idea session, 2026-11-15. Two linked questions from the owner:*
1. *Every feature should carry its own geometry/model requests, so a later pass can make or update the models automatically.*
2. *A way to develop and ideate models for in-world interactions, for fun, with different tasks for the same type of job or tech.*

*Owner's constraint: **tasks stay straightforward.***

Builds on D15/D21 (scripted Blender models, labelled boxes first), D28 (boxes on this branch), and spine step 6 (assemblies as data) in `NEZ_AND_THE_SPINE.md`.

**Decided by the owner, 2026-11-15 (D30–D33):**
- **One list of props** (`game/content/props.json`); features refer to props by id. (§1 option A)
- **Twists now, kits later:** one-step twists on today's install first; the full Kit × Place × Twist × Mode model arrives with spine step 6. (§3)
- **The props helper is built right after spine steps 1–2**; until then, new features write briefs in their build-plan item.
- **First batch to ideate: the switchboard.** Done in this session: `docs/props/switchboard.json` (§6).

---

## 1. Model requests: every box knows what it will become

### The problem today
Placeholder boxes are made inline in each script. `InstallSite.cs` alone builds about 15 (`Box(p1, new Vector3(0.08f, …), Steel)`). Nothing records *what* each box is, so a model pass would have to read the code, guess, and then edit every script to swap the model in.

### The idea: model slots
Every visible thing gets a **slot id** (`install.junction-box`, `kit.ladder`, `room.switchboard`), stable forever like other content ids. Code asks for a slot, not a box:

```csharp
var jb = Props.Make("install.junction-box");   // a .glb if one exists, else the labelled box from its brief
```

The slot's **brief** lives in content, so the box and the future model come from the same description:

```json
{ "id": "install.junction-box",
  "name": "Junction box",
  "what": "Where the drop wire meets the house wiring. Screwed to the outside wall by the front door.",
  "size": [0.20, 0.26, 0.06],
  "pivot": "back-centre",
  "tris": 150,
  "colours": ["world-trim", "world-insulator"],
  "charm": "A raised company logo moulded into the lid, half worn away.",
  "anchors": { "wire-in": [0, 0.13, 0.03], "wire-out": [0, -0.13, 0.03], "screw-1": [-0.07, 0.1, 0], "screw-2": [0.07, -0.1, 0] },
  "states": ["open", "closed"],
  "takes": { "plain": "Your beige plastic box.", "old": "The monopoly's grey cast-metal box, painted over twice.", "bodge": "A biscuit tin, taped shut." },
  "usedBy": ["lines.drop", "prod-secondline"],
  "status": "box" }
```

**The contract a model must keep:** the same `size` (it fits where the box was), the same `pivot`, and the same named `anchors` (where wires attach and where you click). Keep those three and a model drops in with no code change.

**`status`** moves `box → brief-ok → modelled → approved`. A model pass picks up everything at `box` or `brief-ok`, and anything whose brief changed after it was modelled.

### What "automatic" means, honestly
- **Automatic:** finding what needs a model (status and changed briefs), swapping it in when `game/assets/models/<id>.glb` appears, falling back to the box when it doesn't, and catching broken ids (`ContentDb.Validate`).
- **Done by Claude in a model session:** writing each Blender script (`tools/models/<id>.py`) from its brief. A session can work down the list unattended, but someone still looks at the result.

### Options for where the requests live

| | **A. One registry** (`game/content/props.json`) + `Props.Make(id)` | **B. Inside each feature's JSON** (a `model` block on each product step or tree task) | **C. A docs list only** (grow `ASSET_LIST.md`) |
|---|---|---|---|
| Model pass | Reads one file | Has to gather from every file | Reads prose; no auto-swap |
| Shared parts (one junction box, many jobs) | Written once, referenced by id | Copied into each feature, so they drift | Written once |
| Code change | Small: a `Props` helper; scripts migrate when touched | Same helper, plus a reader for each file | None |
| Keeps up to date | Validation fails on a missing id | Validation fails on a missing id | Only by discipline |

**Recommended: A.** Features reference slots by id (`"props": ["install.junction-box"]`), which gives B's "the feature carries its request" without copies.

---

## 2. Ideating models: three takes, one charm detail

Each brief is written as a small design exercise, not just a size:

1. **Three takes, and they are gameplay.** *Plain* (the working version you fit), *Old* (what the monopoly left there), *Bodge* (what the chaos option leaves behind). The silhouettes must differ, so a bodge is visible from the street: a taped biscuit tin, a cable stapled across a doorway, a jumper wire in the wrong colour. This makes D23's "mistakes that compound" readable without a text label.
2. **One charm detail.** A nameplate, a sticker, a dent, a chalk mark. Small and readable, never fine geometry (ASSET_LIST style target).
3. **Anchors first.** Decide where hands go before the shape: a clip point, a screw head, a plug socket. The interaction defines the model, not the reverse.
4. **States, not animations.** Open/closed, lit/dark, neat/crooked. Two or three meshes or a hinge, so the box placeholder can show them too.
5. **Contact sheet.** A model session renders every new prop's three takes side by side (one PNG per batch). The owner picks from the picture, which fits "show me a screenshot".

Budget and palette come from the design system (world tokens, ASSET_LIST budgets: props 50–400 triangles).

---

## 3. Job variety: many jobs from a few parts

### The rule that keeps it straightforward
- **Eight verbs, ever:** *carry, climb, fasten, run (cable), connect, test, remove, clean.* New content never adds a verb; it adds parts and places.
- **A job is at most five steps.** A twist adds **exactly one** step.
- One idea per job. If you need to explain it, it is two jobs.

### A job = Kit × Place × Twist × Mode

| Axis | What it is | Examples for "connect a house" |
|---|---|---|
| **Kit** | The parts being assembled (each part is a slot from §1) | drop wire + clips + junction box + socket |
| **Place** | Where it happens: changes which fastener and which tool | brick (drill and plug), weatherboard (screws), stone cottage (long drill), shop front (sign in the way), farmhouse (long span over the yard) |
| **Twist** | One small complication, one extra step | dog in the garden (carry the gate shut first), ivy on the wall (clear it), wasp nest under the eave (come back at dusk), the monopoly's old box already there (remove it first), "not on the new wallpaper" (run it behind the skirting), customer wants it in the kitchen (longer run) |
| **Mode** | What you are doing to the kit | **install** (build it), **repair** (one part is faulty: find it, replace it), **upgrade** (swap one part for a newer one), **remove** (they're leaving: take it out, keep the parts) |

**Same tech, different tasks:** one kit gives 5 places × 6 twists × 4 modes = **120 distinct jobs**. They all use the same few verbs and props, and none is longer than five steps.

**Repair comes for free:** a fault is a part in a bad state (`chewed`, `wet`, `loose`, `bodged`). The repair job is "test → find the bad part → replace it → test". The bad states are the *old* and *bodge* takes from §2, so they cost no extra models.

### The same job across eras
The kit grows, the verbs don't. "Connect a house":

| Era | Kit adds | New step |
|---|---|---|
| 1983 | drop wire, junction box, socket | none |
| 1984+ | modem (beige, three lights) | connect + test (listen for the shriek) |
| 1999 | microfilter on every socket | fasten a filter per phone (the twist: someone forgot the upstairs one) |
| 2010 | fibre box on the wall, router | run the fibre (it can't bend sharply: the one new rule) |

Each era is new slots in `props.json` and new kit lists. No new code.

### Other job families, in the same shape
- **Pole line:** kit = pole, crossarm, insulators, span. Places = verge, field, hillside, road crossing. Twists = a tree in the way, a farmer who wants the gate left shut, soft ground. Modes = plant, string, replace a rotten pole, take down the monopoly's old line.
- **Switchboard:** kit = line card, jumper, label. Twists = smudged label, party line, a card that's warm. Modes = jumper, relabel, swap a card, clear dead jumpers.
- **Odd jobs** (spine step 6, "odd jobs first"): put up a shelf, fix a gate, hang a TV aerial. The same verbs, which teaches them before the telecom jobs use them.

### Options for variety

| | **A. Kit × Place × Twist × Mode** (above) | **B. Hand-written variants** (each product lists 2–3 versions of its steps) | **C. Twists only** (today's jobs + a random one-step complication) |
|---|---|---|---|
| Variety | Very high, from little content | Medium; each variant is writing | Low–medium |
| Effort | One sim system (assemblies, already spine step 6) | No new system, lots of JSON | Smallest |
| Keeps tasks simple | Yes, by the 5-step / 1-twist rule | Up to the writer | Yes |
| Model requests | Each part and twist prop is a slot | Each variant lists its props | Each twist lists one prop |

**Recommended: A, built in that order: C first** (twists on today's install, a week's work), **then the full kit model** when spine step 6 arrives. C is a strict subset of A, so nothing is thrown away.

---

## 4. What a feature must include from now on

When an item is promoted to `BUILD_PLAN.md`, its Definition of Ready gains one line:

- **Props:** the slot ids it shows, new ones with a brief (size, pivot, anchors, three takes, one charm detail). Status `box`.

A dev session ships the labelled box from the brief. A later model session works down `props.json` by status.

## 5. Answered questions
- M1. Where requests live → **A, one list** (D30).
- M2. Variety model → **C now, then A** (D31).
- M3. When the `Props` helper is built → **after spine steps 1–2** (D32).
- M4. First batch → **the switchboard** (D33, §6).

---

## 6. First batch: the switchboard

Thirteen briefs in `docs/props/switchboard.json`, written against what `SwitchboardStation.cs`, `LineRack.cs` and `Cargo.cs` draw today. Sizes and anchor points are copied from the code, so each model can replace its box exactly. Each brief names the code it replaces (`replaces`).

| Slot | What | The three takes (plain / old / bodge) |
|---|---|---|
| `room.switchboard` | The plywood backboard, shelf and stencils | varnished ply / a telex shop's old backboard / panel bolted on crooked, gaffer tape |
| `switch.terminal` | Subscriber's brass screw posts | polished brass / verdigris / a bent nail |
| `switch.port` | Board port jack (spares in nickel) | brass collar / cracked and scorched / held in with solder |
| `switch.port-lamp` | Jewelled lamp per port | faceted jewel / yellowed / bare bulb |
| `switch.tag` | Paper name tag per row | neat capitals / water-stained ▒▒ / envelope corner in pencil |
| `switch.jumper-plug` | Ends of a jumper (the wire stays code-drawn) | coloured sleeve / grey cloth old wire / bare twisted copper |
| `switch.jumper-hook` | New and old wire hooks | brass cup hooks / coat hook / a nail |
| `tool.tone-probe` | Yellow probe for smudged tags | moulded yellow / worn to cream / elastic band |
| `tool.test-set` | Lineman's handset with clips | black with coiled cord / taped earpiece / a phone handset with clips soldered on |
| `switch.throw-switch` | The TEST knife switch | slate and brass / from a fuse board / a light switch sideways |
| `switch.upgrade-panel` | The 8 → 16 panel | matching ply / different wood / bare hardboard |
| `room.line-rack` | Lines-in rack with lamps and tally | grey relay rack / missing slot cover / cutlery drawer |
| `kit.line-card` | The card you carry in and slot | green board, gold edge / burnt resistor / part held on with putty |

**Rules found while writing them:**
- The board's rows change (8, then 16), so the backboard model holds no rows. Terminals, ports, tags and lamps are small props placed per row.
- Jumper wires stay drawn in code because they sag between any two points. Only their ends and the hanging coils are models.
- Lamp colour is set by the game at run time. The lamp model is the jewel cap and bezel only.
- The *old* and *bodge* takes are already game states: hot-wiring uses the old grey wire, the bad port is the scorched jack, a smudged tag is the water-stained one. Each needs a model, but no new rule.

**Switchboard twists (shaped, for the full kit model later; each is one step):** a mouse nest behind the board (*clean*); a tag in someone else's handwriting (*test* it before trusting it); the new-jumper hook is empty (*carry* a coil from the van); a lamp bulb has gone (*fasten* a new one); a customer rings the Board while you're mid-jumper, so their lamp lights under your hand (wait, or pull and annoy them).

## 7. First twists on the house install (build item)

One-step complications on today's `InstallJob`. Each is picked by the sim (`DetRng`, seeded per job), adds **exactly one step** using an existing verb, shows **one prop**, and never blocks the job. Skipping or bodging it leaves a latent defect, as any step does.

| Twist | The extra step (verb) | Prop slot (brief to write in the build item) | If bodged |
|---|---|---|---|
| The monopoly's old box is on the wall | *remove* it first | `install.old-junction-box` (the *old* take of the junction box) | Two boxes on the wall; the line crackles |
| Ivy over the spot | *clean* the wall | `twist.ivy` | Clip pulls out in the first wind |
| Wasp nest under the eave | *clean* (smoke it out: a longer hold) | `twist.wasp-nest` | You clip round it; a faulty clip later |
| "Not on the new wallpaper" | *run* the cable behind the skirting (longer hold) | `twist.wallpaper` (a strip of bright 1983 paper) | Staples across the wallpaper; satisfaction −5 |
| Dog in the garden | *carry* the gate shut before the ladder goes up | `twist.garden-gate` | The dog chews the drop wire: a fault in a week |
| Socket wanted in the kitchen | *run* a longer cable | `twist.kitchen-socket` (socket position moves) | A cable across the kitchen doorway |

About one install in three gets a twist. Never on the first install of a new game (onboarding).
