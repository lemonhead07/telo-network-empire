# Locked Decisions

These settle the ten conflicts in `TELO_GAMEPLAY_HANDOFF.md` §7. Change one only on purpose, and record why.

| # | Question | Decision | Why |
|---|---|---|---|
| D1 | Which codebase? | **New single codebase: Godot 4.7 .NET (C#)**, pure sim in `src/Telo.Sim`. The old Unity/web repo (`~/oldNetworkNez`) is reference only. Port logic from it into `Telo.Sim` when useful. | Already installed; no royalties or runtime fees; C# lets the sim be tested with plain `dotnet test`; exports to Windows, Linux and Steam Deck. |
| D2 | Start year | **3 Jan 1983.** Act 0 is short (the first 1–2 hours) and teaches the basics. | Matches canon. Starting small is the point. |
| D3 | Perspective | **First-person** for physical work. Strategy views (notebook, PIP, dashboards) are objects you open. | Canon pillar 1; matches the hands-on job-sim genre. |
| D4 | Tech gating | **Announcement queue + act milestones.** The calendar only matters through announcement dates the player can shift. | Canon. Implemented in `ActRules` / `AlmanacRules`. |
| D5 | Archive pacing | 0.2/day base + 0.05 per subscriber + 0.3 per staff member. Entries cost 8–30 (100 for Exchange 0). Tune in playtests. | With about 10 subscribers you recall roughly one entry every 2–3 in-game weeks. |
| D6 | Outages | Defects are **latent and surface later** (built). A diagnosis minigame comes in milestone M3. | Fits "chaos persists". |
| D7 | Installations | **Physical, at the frame** (built). Delegated once you've hired someone. | The core fantasy. |
| D8 | Scope | **Early Access = 1983–1995 (Acts 0–2)** on the whole planet: the full world is explorable from day one, but your network only reaches the first continent. Target 15–25 hours. 1.0 adds 1995–2007 (boom, broadband, undersea cables to other continents). Later updates add mobile, platform, and the satellite/space era. | Ship something people can buy; each later era gives the planet a new use. |
| D9 | Test truth | `dotnet test Telo.slnx` + `--smoke` are the measure of a *release* being sound. Day to day, see D16. | One number, reproducible. |
| D10 | Time | One in-game day = 8 real minutes (a full 24-hour sun cycle). The bed skips to morning. Days pause while menus are open. | Long enough to drive somewhere and back. Tune with `GameRoot.DayLengthSeconds`. |
| D11 | Monetisation | **Premium price, no in-game ads, no microtransactions.** See `BUSINESS_PLAN.md`. | Steam players punish ads in paid games. |
| D12 | World | **One small open-world planet, 2.5 km radius.** A lap is ~16 km: about 5 minutes by light aircraft and 12–20 minutes by van once roads wind around terrain. ~78 km² of surface for towns, roads, towers and cables. Terrain is a pure function in `Telo.Sim/Planet` (the sim can reason about distance, coverage and line-of-sight); Godot only draws it. | The curve of the world is the telecom game: horizons limit radio, oceans need cables, and the last era launches satellites. |
| D13 | Planet seed | **One fixed world (seed 1983)** for now, so towns and content can be hand-placed on known terrain. `--seed=N` exists for experiments. | A designed world beats a random one for a story game. |
| D14 | Travel | Walking is for the last few metres. Steep ground stops you on foot (46°) and in the van (36°); the sea stops the van. Roads, bridges, boats and aircraft are *earned* ways past terrain. | Terrain is the consistent, simple challenge. |
| D15 | Assets | **Everything is made in code**: models with `LowPoly` (flat-shaded, vertex-coloured), sounds synthesised, UI drawn by the engine. No imported art until there is money for an artist. | Solo project; one consistent low-poly look; nothing to license. |
| D16 | Testing | **Develop lots, test little.** Tests and the smoke run are *not* run after every change — only before a commit that changes game rules, before a release, or when asked. GitHub Actions is manual-trigger only. | The owner's preference; keeps iteration fast and CI free. |
