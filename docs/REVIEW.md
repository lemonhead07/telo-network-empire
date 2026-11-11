# Design Review — TELO: Network Empire

*Reviewed 2026-11-11 against `TELO_GAMEPLAY_HANDOFF.md`.*

## Verdict

**The idea is strong and commercially distinctive. The scope is not survivable as written.** Cut the first release down to three eras, ship that, and let its sales fund the rest.

## What's genuinely good

1. **"The job changes as you play" is a real hook, not a slogan.** The handoff already has the mechanism: *earned automation* (manual → delegate → automate) plus *earned interface* (paper → terminal → database → dashboard). Most job sims (cleaning, flipping, mechanic) stay the same verb for 40 hours. TELO's verbs move: in hour 1 you hold the jumper wire, and by hour 30 you're watching a dashboard of jumpers you'll never touch. That fills the "what's left after hour 10?" gap those games have, and it's the line to put on the Steam page.
2. **Tactile first-person work is proven on Steam.** The cleaning, flipping, mechanic and supermarket simulators all sell on "satisfying hands-on job + slow growth". TELO starts from that same base and adds a strategy layer on top.
3. **Research is recall (the Almanac, Archive and Echo)** is an original tech-tree twist with built-in tension and a story reason for it. Recalling tech early is powerful, but it gets you noticed.
4. **Data-driven, pure-sim architecture** lets a small team add eras as content rather than code, which is how a long game gets made by few people.
5. **Tone and theme have heart.** The Promise Ledger (named people you promised to connect) and the 1980s analogue texture give it identity. Nostalgia for 80s and 90s tech is a strong marketing angle.

## Risks (in order of danger)

| # | Risk | Mitigation |
|---|---|---|
| 1 | **Scope.** Eight acts, nine loops, sixteen minigames, crime, rivals, an office designer. That's 5–10 years of work for a small team. | Early Access = Acts 0–2 (1983–1995) only. Every other system is post-launch. |
| 2 | **The previous attempt split into two codebases** (Unity + web) and stalled. | Locked: one codebase (Godot 4 + C#). The old repo stays reference-only. |
| 3 | **Telecom is niche.** "Wire a frame" isn't instantly fun to most people. | The capsule art and trailer must sell the *feeling* (a cosy 80s room, a CRT glow, first dial tone) and the transformation (room → national network). Test the hook with a demo before building more. |
| 4 | **Strategy depth vs. sim feel.** Spreadsheet creep can kill the tactile appeal. | The rule from the handoff: dashboards are *earned*, and the hands-on verb always stays available. |
| 5 | **Story/time-travel layer is expensive** (authored dialogue, endings). | Keep it in short notes: PIP lines, fax headlines, ledger entries. Fully authored story only after there's revenue. |
| 6 | **"A-rated" expectations.** AAA is out of reach for one person. | Aim for a *polished indie*: a coherent low-poly 80s art style, great audio feedback (relay clicks, modem shrieks), and 10–20 hours of Early Access content. |
| 7 | **Real-world trademarks** (phone companies, product names). | Fictionalise everything ("the touchscreen phone"). Already done in the content. |

## What the scaffold already proves

- A pure, deterministic simulation with 35 passing tests: Almanac spine, earned automation, act progression gated by milestones *and* announcements, Promise Ledger, latent defects, and versioned save/load.
- The Frame minigame end-to-end: probe, test, jumper, reroute, party line, the hot-wire chaos option, delegation and a soft clock.
- A first-person Godot room you can walk around: answering machine → enquiry → physical install at the frame → monthly billing.
- The interface visibly changes with the company's systems (`TierStyle`).

## What the scaffold deliberately does not do yet

Counter-trade (Act 0 income), the town/van, door-knocking, rivals, staff as characters, contracts, outages with diagnosis, art, audio, Steam integration, or economy balance. See `BUILD_PLAN.md`.
