# In the world: one eye, one hand (D51)

*Idea session, 2026-11-26. The principle is locked (D51). **The owner answered Q1–Q4 the same day: Q1c pick it up, Q2a crosshair hand, Q3a words on the phone, Q4a IW1 before the playtest.** The answers are written into the rule and the items below.*

**The owner's words:** "I'd like EVERYTHING in world, so it never moves the player's camera away from its control. Controlling the game should feel like literally controlling the player character in the world to complete tasks." All story and gameplay is **event- and raycast-based**, and PIP's notes about objects show up in the world too. The reference is the arcade-cabinet shop in a fast first-person shooter: a real machine in the level, a textured pointer drawn on its glass that moves as you look, buttons you press with the same hand you fight with.

The game is already halfway there. Every screen is drawn on an object (`WorldScreen`), and the pointer is already drawn on the glass in the era's style (`ScreenCursor`: a pencil on paper, a blinking block on the terminal). What still feels like "opening a browser" is everything around that: the camera is tweened to a fixed pose until the screen fills the view, the walk is frozen, the system mouse comes back, and help text floats over the top.

## The rule

1. **The camera is always the player's.** No lean-in tweens, no fixed camera poses, no freezing the walk. Small paper things are picked up and read in your hands instead (Q1, "Held things" below).
2. **The crosshair is the only cursor.** A ray from the centre of the view finds the thing. Where it hits a fixed screen, the hit point on the screen's quad is the pointer, drawn there by `ScreenCursor`. Things in your hands have no pointer: the wheel turns their pages and number keys choose. The mouse only ever turns your head.
3. **The same hand everywhere.** E or left click uses what the ray hits; hold E for steps that take time (`Interactable.HoldSeconds`). Typed keys go to the screen you are looking at (the Board's number keys). Nothing opens or closes: you look away or walk away.
4. **The clock never stops for an interface** (D23 already). While you read the Almanac or patch the board, the phone can ring and Jory can knock.
5. **Things show what they are; PIP says what to do.** State lives on objects: lamps, tags, crooked parts, lit windows. PIP's words go on PIP's phone in your hand. No speech bubbles, no text under the crosshair, no floating markers.
6. **Story arrives as events, delivered by things.** Each sim event (`state.Emit`) comes through an object: the phone rings, a letter lands on the mat, the fax curls out, someone knocks, the radio says it. No pop-ups, and no cutscene takes the camera.
7. **One exception: the pause.** Esc stops the world and raises PIP's handset. While the world is stopped the mouse may work the handset (Save, Load, Settings, Quit). Accessibility options (larger text, hold-to-toggle) live there.

## Why it changes the game, not just the look

- **The desk becomes a workstation you turn around in.** Terminal, switchboard, the line rack's lamps and the ticket rail are all live at once. In the evening rush you patch the board while watching the lines-in lamps out of the corner of your eye.
- **Interruptions become gameplay.** The answering machine blinks while you are reading the Almanac; Jory knocks for a ticket while you are holding a jumper. Time is lived (D23), so a screen that freezes you was always a small cheat.
- **Information has a place.** To know something you go where it is kept: the ledger on the desk, the calendar on the wall, the pin board in the Planning Room. That is the game's hook made physical: paper on a desk, then a terminal, then a control room you stand in (Act 5's night-shift desk).
- **One way to act, for everyone.** Every action is "look at it, press". The sim's actions, the script player and `--autoplay` (which already aims a ray) speak the same language. One input path is less code than three.

## Held things and fixed screens (the owner's answer to Q1)

**Held things** are paper or pocket-sized: the Notebook, PIP's Almanac handset, the early-access card, and later letters, faxes and work orders.
- **E on it picks it up.** It comes up into the lower part of your view at reading distance, like PIP's phone (`WorldScreen` with `onTop`, carried by the camera, as `PhoneView` already is). You can walk, look round and talk to people while you read; the clock runs.
- **The wheel turns pages or scrolls; number keys choose** (the Almanac's Recall lines are numbered, like the Board's menu). There is no pointer on a held thing and no "Step back" button.
- **G puts it down** where you stand, like van kit. If you were carrying kit, picking up a page sets the kit down at your feet first (it still counts as on site within 6 m). Not saved: after a sleep or a load, paper is back on the desk.

**Fixed screens** are read where they stand, with the crosshair as the pointer: the Board's terminal, the title letter on the cork board, the switchboard, and after launch the rack and the pin board. They must read from standing at arm's length, so they get a type floor and fewer words. The Board's terminal goes to a 40-column screen with big letters, which is also true to the early 1980s.

## Review of today's screens (screenshots in `docs/img/`, 2026-11-26)

| Screen | Where | Good | Breaks the rule | Becomes |
|---|---|---|---|---|
| **Title** (typed letter on the cork board, `title.png`) | `FrontDoor.cs`, `UI/MenuView.cs` | A real letter above the real desk, world built behind it | Camera fixed full-frame; system mouse; footer of key hints | You stand in the room facing the board; crosshair on a line, click. The hints go |
| **Pause** (PIP's handset, `pause.png`) | `FrontDoor.cs` | The clock really stops | A monitor-sized slab across the middle of the view: this is the "browser window" feeling | The one exception. Held lower and smaller, so the frozen street stays in view |
| **Settings, How to play** (`settings.png`, `howto.png`) | `UI/SettingsView.cs`, `UI/HowToPlayView.cs` | Clear, readable | "Back [Esc]" and "Defaults" are web buttons | Reached from the pause, so exempt. Lose the bracketed key labels |
| **Almanac** (PIP's screen, 0.34 m), **Notebook** (0.48 m), **the Board's terminal**, **early-access card** (0.42 m) (`pointers-on-screens.png`, `halden-card.png`) | `World/WorldScreen.cs`, `GameRoot.LeanInto`/`LeanBack`, `AnyScreenOpen`, `Halden.cs` | Diegetic, era-styled, live when idle | Leaned in until the object fills the view; walk frozen; system mouse; "Step back [Esc]" buttons; small dense text that only reads leaned in | Almanac, Notebook and card: picked up and read in your hands (Q1c). Board terminal: crosshair pointer, 40 big columns. No lean, no freeze; fewer words (the Notebook is a wall of text); "Step back" goes |
| **Switchboard** (`switchboard.png`) | `World/SwitchboardStation.cs` | The best of them: a real board, real wires, tools on a shelf | Lean pose; the mouse is a free pointer (`MouseMode = Visible`); "Esc steps back" floats over the board | Crosshair hand (Q2) |
| **PIP's bubble** (bottom of `switchboard.png`) | `UI/Hud.cs` `Say`, the fallback in `PipTips.Say` | Short and warm | A screen overlay over the work | PIP's phone only (Q3) |
| **HUD strip** | `UI/Hud.cs`: date, cash, ▶ next, prompt under the crosshair, event feed, hold bar | Mostly moved to PIP's phone already (`PhoneView`, `SetReading`) | Text under the crosshair; feed | Only the crosshair dot stays, with the hold bar drawn as a ring round it |
| **▼ $ ! over houses and pole sites** | `GameRoot.cs` (~line 986), `TownBuilder.cs` (~344), `PoleLineView.cs` (~56): fixed-size `Label3D` | The first hour's main way to find a house from the street | Floating symbols, not things | Physical tells (IW3, after launch): letterbox flag and porch light for "here", a note pinned on the door for an odd job, a red tag on the drop wire for a fault; the phone names the street and number. Must still read from 100 m |

## Questions for the owner

**Q1. Reading small things without the lean.** Today the lean is what makes 0.34 m screens readable. **Owner: c, pick it up.**
- a. Look closely (was recommended). Hold right mouse: your view narrows and the look slows, like leaning your head in, but you still steer and can walk. Let go and it's back. Plus a type floor: every screen readable from arm's length while looking closely.
- b. Big screens only. Every screen at least monitor size, words cut to fit. No new control, but every view is rewritten.
- **c. Pick it up (chosen).** Small paper things (notebook, card, Almanac handset) are lifted into your hands like the phone and read while you walk; pages turn with the wheel, choices are number keys. Big fixed things are read where they stand. Most book-like; no pointer on held things.

**Q2. The switchboard (and the rack and pin board after launch).** **Owner: a.**
- **a. Crosshair hand (recommended).** Look at a terminal, click: the jumper's end is in your hand. Look at a port, click: plugged. Tools come off the shelf into your hands like van kit. You can step back with a wire held to see the whole board, or turn to the line rack mid-patch. The rack (DC3) and pin board (DC4) are built this way from the start.
- b. Keep the lean for stations only, as a named exception. Cheapest, but the first hour's centrepiece stays the old way, and the rack and pin board copy it.

**Q3. Where PIP's notes about an object show.** **Owner: a.**
- **a. Words on the phone, state on the object (recommended).** The thing you look at glows faintly; PIP's phone at the bottom of the view names it and says what E does; lamps, tags and crooked parts show its state. Half built already (`PhoneView`; BUILD_PLAN "Earlier list" item 7).
- b. Tags on the object. A paper luggage tag on the thing turns to face you when you look at it, in PIP's handwriting. Words at the place, but more props and more clutter.
- c. Both. A tag carries the object's name and state; the phone carries PIP's tips and the story.

**Q4. When (the race to market).** **Owner: a.**
- **a. IW1 before the playtest (recommended).** IW1 (the screens, look closely, PIP to the phone; about one session) goes in right after EA7 sound, so the owner's playtest judges the new feel. IW2 (the switchboard) right after launch, or before it if the playtest says the lean jars.
- b. IW1 and IW2 before launch (two to three sessions). Launch slips by that much.
- c. All after launch. The rule holds for everything new (rack, pin board), and the old screens are converted in an update.

## Build items (in `BUILD_PLAN.md`; IW1 and IW2 are Ready)

- **IW1. Screens under the crosshair, paper in your hands.** The Notebook, Almanac and early-access card are picked up and read in your hands; the Board's terminal and the title letter are worked by looking at them; you can walk and look the whole time; PIP's words go only to the phone; the HUD is just the crosshair and its hold ring. Folds in "the prompt moves to the phone" (Earlier list, item 7). Adds the guard script below.
- **IW2. The switchboard by eye and hand.** Q2a on `SwitchboardStation`.
- **IW3. Physical tells instead of floating marks** (after launch). ▼ $ ! become things you can see from the street.
- **IW4. Story events delivered by things** (after launch; waits on the owner's gameplay detail). Each `EventKind` gets a deliverer: phone text (default today), letter on the mat, fax roll, knock at the door, radio news. Takes over the old backlog lines "wall calendar, cash tin, pager, fax roll".

## How the rule stays true in later sessions

- **D51** in `DECISIONS.md`, a paragraph in `CLAUDE.md` ("No floating menus"), and this doc.
- **Definition of Ready** (`SESSIONS.md`) asks every new item how the player sees and works it under the crosshair.
- **The guard script** (built in IW1): `tools/inworld-check.sh` lists every line in `game/scripts` that breaks the rule: a free mouse (`MouseModeEnum.Visible`), a camera tween (`LeanTo`), a frozen walk (`IsFrozen`), a screen overlay (`CanvasLayer`, `Hud.Say`), a floating label (`Label3D` with `FixedSize`), against a short allow-list (the pause handset). Run before finishing any change that touches what the player sees. Not in CI (CI stays cheap). It starts life as the conversion to-do list and ends as a guard.
- Today's screens still lean in until IW1 (before the playtest) and IW2 (after launch, or before it if the playtest says the lean jars) land. **Anything new is built the new way.**
