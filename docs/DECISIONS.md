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
| D8 | Scope | **Early Access = Acts 0–2 (1983–1995).** Full release adds Acts 3–4. Acts 5–7 are expansions/updates. | Ship something people can buy. |
| D9 | Test truth | `dotnet test Telo.slnx` + `--smoke` are the only measures of "done". | One number, reproducible. |
| D10 | Time | One in-game day = 90 real seconds (08:00–20:00). The bed skips to morning. Days pause while menus are open. | Easy to tune: `GameRoot.DayLengthSeconds`. |
| D11 | Monetisation | **Premium price, no in-game ads, no microtransactions.** See `BUSINESS_PLAN.md`. | Steam players punish ads in paid games. |
