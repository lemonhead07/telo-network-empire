# TELO: Network Empire — Gameplay Handoff

**Date:** 2026-11-11
**Purpose:** A single-file, high-level handoff of every gameplay idea in the repository, with an honest implementation audit and the decisions the next team must make before building.
**Audience:** A stronger model or new team taking over design and implementation.
**Status legend:** ✅ verified in code · 🟡 prototyped/partial · 📄 design-only

---

## 0. One-page brief

TELO is a first-person telecommunications business simulator set across 1983–2035. The player is a burned-out network engineer from 2035 who wakes in 1983 with a corrupted phone archive of the company's own future. They build a local ISP from a rented room into national — or criminal — infrastructure.

- **The fantasy:** crawl behind a rack to connect one customer; end up operating infrastructure millions depend on.
- **The core rule:** every improvement creates more demand, and more demand creates a new problem.
- **The spine:** research is *recall*. The world announces technology on a fixed queue. Ship it early and pay in **Archive** and **Echo**; ship on time and be safe but late.
- **The two lights:** piracy as folk culture, and the Last Mile as named people rather than a satisfaction score.
- **The three-layer build:** pure simulation (`sim`), physical world, diegetic presentation. Automation and dashboards are earned, never granted.
- **The intended first proof:** one building minigame end-to-end, with a contract obligation and a Promise Ledger entry attached.
- **The biggest risk:** the repository currently contains two parallel implementations and several unresolved canon conflicts. Pick one codebase and one start year before adding content.

**Read order for the next team:** this file → `Assets/_Project/Documentation/STORY_BIBLE.md` → `ERAS.md` → `web/AGENTS.md` → `web/DEV_STATUS.md`.

---

## 1. What the game is

A first-person telecommunications business simulator about building a network across four decades. You start alone, carrying modems, wiring racks, knocking on doors, and personally fixing outages. You end by deciding whether millions of people remain connected.

**The emotional ladder:**

1. I connected my first customer.
2. I can barely keep this network operating.
3. I hired someone to handle installations.
4. My company now covers the town.
5. Competitors are trying to stop me.
6. I can cooperate, compete or cheat.
7. My decisions are changing the entire region.
8. The company has become more powerful than I am.
9. I must decide what kind of empire I have built.

**Tone:** wry, warm, bureaucratic comedy. The 1980s are analogue: fax paper, telex, pagers, carbon copies, cigarette smoke in the exchange, a fan louder than the server. Dread arrives quietly: a page that should not exist, a name in a document not typed yet, a clock that disagrees with itself. Cynicism is reserved for institutions, incumbents, and whoever approved the budget — never for people who need the network.

---

## 2. Design pillars

1. **First-person physical work.** Carry, wire, drive, install, inspect, repair. The player performs important tasks personally.
2. **Time pressure.** Every action consumes time. An outage continues while the player travels, diagnoses, and searches for parts.
3. **Earned automation.** Tasks only become automated after investment in people, software, procedures, monitoring, spares, vehicles, and management.
4. **Network optimisation.** Build a functioning network with visible traffic and identifiable bottlenecks.
5. **Corporate competition.** Rivals build, compete, undercut, lobby, acquire, and retaliate.
6. **Legal and illegal choice.** Honest, exploitative, or criminal play are all viable. Evidence accumulates; consequences persist.
7. **Diegetic information.** Paper first, dashboards later. The interface improves because the company built the infrastructure behind it.
8. **Every improvement creates a new problem.** Products, staff, redundancy, and automation all create their own failure modes.
9. **Data-driven content.** Definitions are immutable records; instances are small; rules are functions over data; stable string IDs only.

---

## 3. The nine gameplay loops

### Loop 1 — Physical work ✅🟡

- Carry equipment with weight, both hands occupied, and movement slowdown.
- Rack equipment: 42RU occupancy, snap placement, typed ports, cable compatibility.
- Functional equipment chain: **buy → transport → place → power → cable → configure → test → integrate**. Nothing works just because it was purchased.
- Repair chain: detect → locate → gather tools → travel → diagnose → repair or bypass → restore → communicate → find root cause.
- Travel by service van: fuel, cargo slots, maintenance, later upgrades and delegation.
- Information starts physical: paper maps, notebooks, wall calendars, invoices, answering machines, filing cabinets, handwritten network diagrams, equipment labels, indicator lights, CRT terminals, pagers, fax machines.
- Interface is earned: paper → customer database → billing software → dispatch → remote status → monitoring → NOC dashboards.
- Outages keep running while the player travels, diagnoses, and searches for replacement equipment.

### Loop 2 — Sell & serve ✅🟡

- Eight customer archetypes: curious home user, student, family, gamer, small business, tech-inexperienced, price-sensitive, reliability-sensitive.
- Door-knocking: 30% nobody home, interest and budget rolls, pitch plus price negotiation, archetype-specific rejection lines.
- Appointments 1–3 days out; word-of-mouth boost from satisfied customers within 150 m.
- Satisfaction driven by uptime and delivered speed; per-archetype churn thresholds.
- Customers can subscribe, upgrade, downgrade, complain, recommend neighbours, refuse payment, cancel, sue, report suspicious behaviour, accept illegal arrangements, or become loyal long-term clients.
- Pricing, plans, overage, business accounts, paper invoicing.
- Reputation split into household, business, technical, government, employee, investor, underworld.
- Underground customers pay well but generate legal and reputational risk.
- Customer needs: price, speed, reliability, latency, support, installation delay, privacy, company reputation, contract terms.

### Loop 3 — Build & operate the network ✅🟡

- Visible traffic flows; bottlenecks are identifiable.
- Capacity chain: customer lines → modem banks → servers → backhaul → routing → storage → support.
- Power and heat: breakers, cooling, PUE, generators, batteries, thermal throttling.
- Coverage: masts, quadratic signal falloff, premises in/out of coverage, tower azimuth and tilt.
- Routing: BFS shortest path, per-hop bottleneck, congestion, overload states, later redundancy and failover.
- Outage types: equipment failure, power loss, upstream down, overload; later storms, flooding, cable damage, software errors, sabotage, supplier failure, employee mistakes.
- Monitoring is earned, never granted.
- Hex cells as the canonical spatial unit for coverage, rural routes, and sensor meshes.
- Scale plan: local site → region chunks → cube-sphere planet, with off-screen customers still ticking.

### Loop 4 — Time, technology & the Almanac ✅

- Worldline clock; eras advance through a **public announcement queue**, not a calendar timer.
- **Research is recall** from PIP's corrupted Almanac.
- **Archive** grows from the network the player built — traffic, data, compute, staff, labs — not from time alone.
- **Early recall** costs Archive and adds **Echo**. On-time recall costs Archive only. Late is safe but behind rivals.
- Entry states: `sealed` → `readable` → `recalled` → `drifting`.
- Echo is attention: press, regulators, suppliers, rivals, correction events. Bands: quiet → noticed → watched → investigated → correction.
- The calendar is a lever: fund standards bodies, trade shows, and ecosystem subsidies to pull events forward; suppress or discredit to push them back.
- The last Almanac entry is always **Exchange 0**, with the player's name on the work order. The date is unknown.
- Products become obsolete: old gear still runs, costs more, fails more, and cannot be sold to new customers. Removing it is a physical choice.
- Some entries are sealed until the player has the people, infrastructure, or trust to understand them.

### Loop 5 — Hire & automate 📄

- Earned automation: **manual → delegate (80–90% optimal, defects discoverable later) → automate**.
- Roles: installer, receptionist, bookkeeper, support, sales, driver; network engineer, server admin, tower/fibre technician, security analyst, developer; operations/finance/sales director, legal counsel, regulatory specialist, CTO.
- Questionable roles: private investigator, political fixer, corrupt contractor, corporate spy, underworld intermediary, evidence cleaner.
- Employee attributes: skill, salary expectations, loyalty, ambition, stress, ethics, criminal tolerance, relationships, knowledge of company activities.
- Illegal orders can be accepted, refused, leveraged, reported, blackmailed, or become loyal complicity.
- Automation creates maintenance and security risk; redundancy increases cost; payroll creates pressure; complex software creates vendor lock-in.

### Loop 6 — Compete & negotiate 📄

- Six rival archetypes: incumbent carrier, discount provider, technology startup, regional cooperative, corporate specialist, shadow provider.
- Rivals canvass, build infrastructure, buy property, hire, advertise, undercut, lobby, sue, acquire, and retaliate.
- Rivals remember hostility: poaching, customer targeting, reporting, refusing interconnection, negative media, property purchase, lawsuits, sabotage.
- Diplomacy levers: peering, wholesale access, tower/fibre sharing, roaming, joint construction, non-aggression, customer transfer, licensing, emergency capacity, mergers, acquisitions.
- Defeated rivals retreat, specialise, merge, seek government protection, turn criminal, or attempt a comeback.

### Loop 7 — Cheat, if you choose 📄

- Illegal systems: grey-market equipment, bribery, corporate espionage, sabotage, data exploitation, billing fraud, unlicensed infrastructure, contract manipulation, competitor collusion, covert underworld network.
- Consequence model separates **suspicion** (fades) from **evidence** (persists until handled).
- Evidence lives with employees, rivals, police, regulators, journalists, partners, customers.
- Investigations: inspections, interviews, audits, equipment seizures, court orders, public inquiries.
- Consequences: fines, lawsuits, refunds, licence loss, lost contracts, resignations, investor panic, scandal, confiscation, forced breakup, prison, permanent record.
- Recovery: pay penalties, replace management, cooperate, expose a rival, compensate customers, compliance, rebrand, sell divisions, relocate illegal operations.
- No morality meter. Fictional devices and abstract interactions only. No real hacking, interception, frequencies, credentials, or evasion techniques.

### Loop 8 — Design & express (office designer) 📄

- Cosmetic only: no stat bonuses, no optimal layout, no hidden “correct” design.
- Modes: place, paint, blueprint, walk, share.
- 0.5 m grid, props, rooms, layers, deterministic share codes.
- Plausibility warnings in an in-world voice: overlap, reachability, egress width, extinguisher access, ceiling height, floor load, rack clearance, power reach, door swing, accessibility.
- Chaos catalogue with delayed discovery: blocked fire exit, rack in the kitchen, cable across a walkway, desks under a leak, maze reception, rival merchandise, overcrowding, server room as break room, boss gets the only window.
- Tool progression: notebook sketch → graph paper → CAD terminal → drag-and-drop → VR walkthrough.
- Consequences are narrative and emergent: fine, resignation, trip, heat pocket, insurance premium, customer impression.

### Loop 9 — The two lights 📄

- **The Last Mile:** a Promise Ledger of named unserved premises, each with a reason (distance, cost, terrain, landlord, disability, poverty, language, age, institution, policy), cost to serve, promised date, and status.
- Access-shelf products: lifeline tariff, library terminals, school line, clinic line, remote/satellite link, community co-op shares, second-hand equipment, accessibility kit, translation line.
- Universal Service Obligation contracts: subsidy paid after build-out; “served” defined by someone who has never driven the road.
- Community access contracts: low margin, high visibility, signed by someone who knows every customer by name.
- No morality score. The game remembers in the ledger and in who appears at the end.
- **The Borrowed Light (piracy):** BBS file-area curation, copied school software, a rival reporting you, a raid you can warn Tobi about, a corporate customer asking for “the copied version.” Social comedy and folk redistribution, never instructions.
- Act 7 payoff: the people who were never connected are the ones the Blackout kills.

---

## 4. The eight-act ladder

| Act | Years | Business model | Binding constraint | Signature minigame | Exit milestone |
|---|---|---|---|---|---|
| 0 The Cover | 1983–85 | Labour and favours | Cash, identity, hours | Counter trade | Lease on 1 Main Street |
| 1 Bulletin Board | 1985–89 | Hardware + BBS subscription | Phone lines and your hours | The Frame | Recurring revenue pays wages |
| 2 Dial-Up Frontier | 1989–95 | Subscription + overage | Modem bank + upstream | Modem bank | First profitable quarter with staff |
| 3 The Boom | 1995–2001 | Hosting, colo, roll-ups, IPO | Servers, staff, fraud | Cold aisle | Survive the crash with control |
| 4 Broadband Wars | 2001–07 | Always-on, triple-play, SLAs | Copper distance, construction, backlog | The Trench | Profitable broadband in three districts |
| 5 Mobile Revolution | 2007–13 | Plans, spectrum, towers, roaming | Spectrum, backhaul, rooftop leases | Aim | Fixed + mobile in one region |
| 6 The Platform | 2013–20 | Cloud, CDN, streaming, IoT | Power, privacy, content cost | Alarm storm | Become a national platform — or refuse |
| 7 Intelligent Infrastructure | 2020–35 | 5G/edge/LEO, sovereign cloud | Systemic risk and regulation | Sensor mesh | Decide what institution you became |

Each act also specifies: announcements, rival move, regulation weather, illegal temptation, piracy weather, who gets left out, signature minigame, and exit milestone. The years are a guide; the announcement queue is the real gate.

**Act detail at a glance:**

- **Act 0 — The Cover (1983–85).** Fix telex machines, install extensions, sell a modem at cost, sleep in the shop. The 1984 monopoly breakup plays on a television. First customer is a shop needing a second phone line.
- **Act 1 — The Bulletin Board (1985–89).** First subscribers, first rival, first self-inflicted outage. Tobi's file area makes the BBS famous for the wrong reason. Ruth Adeyemi asks for a line to a farm. The company gets its name when PIP's broken voice says “TE-LO.”
- **Act 2 — Dial-Up Frontier (1989–95).** The web arrives and almost nobody notices. Busy signals, modem banks, upstream bills, first employee, first proper office. Mr. Pell's house is just past the limit.
- **Act 3 — The Boom (1995–2001).** Hosting, colocation, business accounts, IPO, crash. An investor asks where the technology comes from. Wren Halloway writes her first story about TELO.
- **Act 4 — Broadband Wars (2001–07).** Trenches, cabinets, copper distance, install backlogs. Schools, clinics, council, hospital. Overbuild a rich street or connect a poor one. The ledger remembers.
- **Act 5 — Mobile Revolution (2007–13).** Spectrum auction, tower on the hill, rooftop lease, first smartphone. Coverage becomes a public argument. The blackout that is not the player's fault but is the player's problem.
- **Act 6 — The Platform (2013–20).** Cloud, CDN, streaming, IoT, and the data the player should not have. Content deals and privacy scandals. The first automated failure is small, embarrassing, and a warning.
- **Act 7 — Intelligent Infrastructure (2020–35).** 5G, edge, LEO, AI ops, smart cities, sovereign cloud. The Almanac's last entry comes due. The 2034 Blackout happens — or is prevented. The loop closes at Exchange 0.

---

## 5. Cross-cutting content systems

### 5.1 Building minigames — one verb per building

**Rules:**

1. One verb per building; later acts add pressure, not new controls.
2. 30–90 seconds, soft clocks, no hard fail screens or reloads.
3. Reads real simulation state — customer, contract, fault, power budget, map — never a random roll.
4. Failure costs time, money, or satisfaction. Never progress, never a life.
5. Delegate always exists once staffed: 80–90% optimal, defects discoverable later.
6. Chaos always exists: a fast, cheap, wrong option that persists in the world.
7. Diegetic: the puzzle is the object, not a modal overlay.
8. Accessible: colour-blind labels, no-timer mode, one-handed mode, auto-solve, subtitles.
9. Piracy content is about curation and community, never copying technique.
10. Every minigame names the product or contract it affects.

| Building | Verb | Core decision | Chaos option |
|---|---|---|---|
| Electronics shop | Counter trade | Price vs lead time; spot grey market | Buy stolen stock |
| Rented office | Patch it | Cross-connect ports | Hot-wire a pair |
| Exchange | The Frame | Jumper wiring + tone test | Reuse a dirty pair |
| Datacentre | Cold aisle | Rack placement, airflow, phase balance | Boxes in the aisle |
| Tower/rooftop | Aim | Azimuth/tilt vs coverage/interference | Unlicensed power |
| Tower compound | Power | Generator/battery/fuel/solar | Bypass the breaker |
| Rural | The Trench | Hex route, terrain, permits | Night trench through a reserve |
| Park | Not In My Park | Conceal small cells | Hide one in the clock tower |
| Corporate HQ | The Boardroom | Quarterly allocation + earnings Q&A | Lie to analysts |
| Call centre | Queue | Triage by severity, ARPU, SLA | Close tickets unread |
| NOC | Alarm storm | Correlate 30 alarms to one cause | Silence the alarm |
| Bank/investor | Term sheet | Debt vs equity, covenants | Creative accounting |
| Regulator | The Audit | Assemble evidence; choose transparency | Destroy evidence |
| Smart city | Sensor mesh | Battery vs coverage vs privacy | Sell the data |
| Warehouse/van | Load | Bin-pack the day's jobs | Overload the van |
| Cable landing | Splice | Fibre pairing and loss budget | Skip the OTDR |

**Light-touch story minigames:**

- **The Borrowed Library** — curate a BBS file area: keep useful software, hide obvious warez, or shut it down.
- **The Promise Meeting** — present a coverage plan to a rural community; fail and they sign with a co-op or rival.
- **The Queue** — allocate library terminals and time slots to people who need them.
- **The Bench** — place a small cell in park furniture without ruining the park.

**Worked example — The Frame (exchange, 1989):** six customer lines to six exchange ports. Two labels unreadable (use tone probe), one pair faulty (reroute), one party line (do not disconnect the neighbour). Soft 60-second clock. Success: all lines live, reputation up. Failure: privacy incident or dead line. Chaos: reuse an existing jumper, save 20 seconds, latent defect surfaces weeks later. Delegate: junior technician at 85% accuracy.

### 5.2 Products — an answer to a visible problem that creates the next problem

Ten problem axes: **reach, capacity, speed, reliability, cost, trust, compliance, staff, power/heat, security.**

Every product names exactly one primary problem solved and one secondary problem created. Roughly 8 products per act, plus an access shelf of deliberately lower-margin Last Mile products. Examples:

- **Act 0–1:** 1200/2400-baud modem, multi-line hunt group, leased line, paging gateway, long-distance resale, fax/telex gateway, BBS file area, offline mail reader.
- **Act 2:** 14.4k modem bank, RADIUS/AAA, mail gateway, file archive, upstream T1, web server, automated billing, network monitoring.
- **Act 3:** 56k, DSLAM, colocation rack, cache/proxy, VoIP gateway, OSS/BSS, Wi-Fi AP, first datacentre.
- **Act 4:** ADSL2+, street cabinet, fibre backhaul, IPTV headend, FTTP, universal-service line, community broadband co-op.
- **Act 5:** 3G base station, 4G/LTE, small cell, spectrum licence, rooftop lease, mobile plan, MVNO.
- **Act 6:** 100G optical, CDN node, cloud IaaS, SDN controller, IoT gateway, security suite, content bundle, public Wi-Fi/library terminals.
- **Act 7:** 5G standalone, edge node, LEO gateway, private 5G, AI ops, smart-city platform, sovereign cloud, emergency services network.

**Obsolescence:** every product has an introduction announcement and an obsolete-after window (typically 4–8 years). Old equipment still works, costs more to run, fails more often, and cannot be sold to new customers.

**Access shelf:** lifeline tariff, library public terminals, school connection, clinic/hospital line, remote/satellite link, community co-op share, second-hand equipment programme, accessibility kit, translation/language line. Each act must offer at least one access product and one premium product. The choice is the game.

### 5.3 Contracts — promises with clocks

**Fourteen families:** residential, SME, enterprise, wholesale/transit, infrastructure access, government, emergency services, spectrum, smart city/IoT, media/content, equipment supply, construction, employment, covert.

**Typed clause anatomy:**

1. Parties
2. Term
3. Scope
4. SLA (availability, latency, MTTR, measurement window, exclusions)
5. Install deadline
6. Price and escalation
7. Exclusivity
8. Penalties and credits
9. Audit rights
10. Termination
11. Data and privacy
12. Security
13. Most-favoured-customer
14. Change control
15. Force majeure
16. Governing law
17. **The catch** — one hidden clause, discoverable through due diligence, legal staff, or an expensive mistake

**Negotiation levers:** price, term, penalty cap, build-out commitment, exclusivity, service credits, payment terms, data rights, MFN, equity. No lever is free; every concession creates a visible future problem.

**Breach cascade:** SLA miss → service credit → escalation → penalty → termination threat → lawsuit → regulator interest → licence risk.

### 5.4 Rival archetypes, company paths, endings

**Rivals:**

- **Incumbent carrier** — legacy infrastructure, political influence, slow, strong business relationships, poor service, large reserves. First supplier and first enemy.
- **Discount provider** — low prices, aggressive advertising, cheap equipment, weak support, overloaded network. Grows fast, vulnerable to service failures.
- **Technology startup** — new technology, automation, software, business customers, investor funding. Expands fast or collapses under debt.
- **Regional cooperative** — rural areas, community trust, local employment, reliable service, grants. Hard to displace without damaging public reputation.
- **Corporate specialist** — banks, hospitals, government, industrial clients. Fewer customers, stable revenue.
- **Shadow provider** — legitimate facade with underworld connections. Smuggled hardware, covert hosting, sabotage services, stolen information, unregistered infrastructure.

**Company paths:** trusted public provider, technology leader, discount giant, corporate carrier, media/data empire, predatory monopoly, criminal network. Paths can combine; conflicting strategies create internal tension.

**Endings:**

| Ending | Trigger | What it says |
|---|---|---|
| The Institution | Universal service honoured, trust high, empire intact | You became the thing everyone depends on. |
| The Ghost | Empire intact, trust low, Echo high | The company survives; the founder disappears into the archive. |
| The Better Future | Break the loop by giving up central control | The Blackout is prevented by making the network smaller and more human. |
| The Breakup | Monopoly + regulator pressure | Regulators divide the company; the pieces are healthier than the whole. |
| The Quiet Retirement | Sell the business, Last Mile mostly served | You leave with a town that can reach you. |
| The Loop | Bootstrap loop revealed and accepted | You become the founder whose disappearance you caused. |

Plus: hostile takeover, public collapse, imprisoned founder, predatory monopoly, criminal communications empire.

---

## 6. Current implementation audit

There are **two parallel codebases**, and they are not the same game yet.

### 6.1 Unity project (`Telo.*`, first-person)

| Area | State | Notes |
|---|---|---|
| FPS controller, interaction, pickup/carry, placement | ✅ | WASD, mouse look, sprint, crouch, jump; E/G; snap placement |
| Rack/ports/cables/state machine | ✅ | 42RU rack, typed ports, 22-transition state machine, 7 equipment definitions |
| BFS network graph, bottleneck routing, overload | ✅ | Per-hop capacity, load ratio, overload state |
| Customers, satisfaction, churn, economy, daily tick | ✅ | 8 archetypes, uptime + speed satisfaction |
| World clock, 3 outage types, save model | ✅ | 1993 start; no disk serialization yet |
| Town, van fuel/cargo/travel, door-knocking, 9-node tech tree | ✅ simulation | 8 locations; 40 L tank, 10 km/L, 80 km/h, 6 cargo slots |
| Scene wiring, customer UI, install flow, tech UI | 🟡 | Simulation exists; gameplay wiring incomplete |
| Rival, employee, crime systems | 📄 | Stages 9–11 not started |
| Start year | 1993 only | Canon says 1983/1985 |

**Known Unity gaps:** `Bootstrap.cs` scene attachment, runtime tick driver, network bridge wiring, cable disconnect, interaction prompt UI, houses/office interiors, door-knocking UI, installation at customer site, paper records, save-to-disk, prefabs.

### 6.2 Browser prototype (`web/`, three.js + TypeScript + pure `sim`)

| Area | State | Notes |
|---|---|---|
| Spherical planet, walker, building enter/exit, task camera | ✅ | R = 320 m, faceted low-poly, visible horizon curvature |
| 3 devices / 12 ports cable puzzle, boot states, objectives | ✅ | Router, switch, server, PDU; click-to-connect cables |
| 2 masts, 7 premises, coverage-driven “served” count | ✅ | Quadratic falloff, great-circle distance |
| PIP guidance derived from state, mail, phone UI | ✅ | Guidance is never scripted separately from rules |
| Inventory: weight, hands, lead time, vertical integration | ✅ | Heavy gear uses both hands and slows movement |
| Almanac core: 6 announcements, Archive, Echo, recall, bands | ✅ | Deterministic, pure simulation |
| Almanac on the handset | ✅ | Era-gated entries, recall buttons, Archive/Echo status |
| Customers as sim entities, vehicles, hiring, save/load | 📄 | Not built |
| Frame, contracts, ledger, designer, sensor mesh | 📄 | Next slices, defined in `DESIGN_HANDOFF.md` |

**Verified last known state:** 39 unit tests + 8 headless browser tests, `npm run verify` green as of 2026-11-02.

### 6.3 Documentation-only systems

- Full contract clause model, SLA credits, breach cascades.
- Promise Ledger and access-shelf products.
- Office designer, plausibility checks, chaos catalogue.
- Hex sensor mesh, rural trench routing, tower aiming, datacentre cold aisle, alarm storm, boardroom, audit, term sheet, queue triage, van loading, cable splicing.
- Rival AI, market share, diplomacy, retaliation.
- Employee hiring, skills, morale, loyalty, whistleblowing.
- Crime, evidence, investigations, trials, recovery.
- Character arcs, authored dialogue, endings, art direction, audio, accessibility specifics.

---

## 7. Conflicts and decisions to lock before building

1. **Which codebase is the product?** Unity is the intended game; `web` is the design lab and canon reference. Pick one. Do not keep implementing the same systems twice.
2. **Start year.** Unity is 1993; canon is 1983 (playable 1985, Acts 0–1 earned).
3. **Perspective.** Canon says first-person; the web prototype uses a third-person follow avatar plus a task camera.
4. **Tech gating.** Unity uses calendar years; canon uses the announcement queue.
5. **Archive pacing.** Live web play earns 1 Archive/day while recalls cost 8–30. Decide the intended session pacing.
6. **Outages.** Unity auto-resolves 1–3 day timers. The design wants a diagnostic verb with travel, clues, and root cause.
7. **Installations.** Unity auto-completes appointments. The core fantasy is doing the install physically.
8. **Scope.** Roughly 8,000 lines of design docs describe far more than either codebase. A new team needs a small, locked vertical slice, not the whole bible.
9. **Test truth.** Unity test counts disagree across documents (95 vs 102; 89 vs 82 EditMode). Run the suite and publish one number.
10. **Simulation depth.** Time ratio, day length, traffic units, latency formulas, session concurrency, power/cooling depth, and repair-vs-replace rules are all unresolved.

---

## 8. Recommended handoff structure

Do not copy all 8,000 lines. Rewrite into a focused directory:

```text
handoff/
  00-START-HERE.md          one page, what to build first
  01-VISION.md              pitch, pillars, emotional arc
  02-CANON.md               story, tone, PIP, Almanac, two lights
  03-ERA-LADDER.md          eight acts, constraints, rivals, exits
  04-GAMEPLAY-LOOPS.md      the nine loops
  05-CONTENT-CATALOGUE.md   products, contracts, minigames, ledger
  06-IMPLEMENTATION.md      two codebases, tests, gaps, traps
  07-BUILD-PLAN.md          slices, acceptance, verification
  08-OPEN-DECISIONS.md      the ten conflicts above
```

### Recommended build order

1. **Lock decisions** in `08-OPEN-DECISIONS.md`: one codebase, one start year, one camera perspective, one tech-gating model, one Archive pacing model.
2. **Almanac spine** — deterministic worldline, announcements, Archive, Echo, sealed/readable/recalled/drifting.
3. **One building loop end-to-end** — The Frame, with probe, connect, reroute, party-line privacy, hot-wire latent defect, seeded delegate, soft clock, accessibility.
4. **Contracts + Promise Ledger** — two templates, one hidden clause, SLA clock, service credit, breach event, five named ledger entries.
5. **One customer vertical slice** — knock → pitch → appointment → physical install → activation → traffic → bill → collect.
6. **One outage** — clue-driven diagnosis, travel, repair, customer communication, root cause.
7. **One employee** — completes a task the player used to do manually, at 85% accuracy with discoverable defects.
8. **One rival** — changes price or coverage in response to the player, without cheating.
9. **Save/load** — versioned wrapper, stable IDs, recompute derived state.
10. **Then** crime, office designer, mobile/IoT, and later acts.

**Shortest useful brief:** build the Frame end-to-end, with contract consequences and a ledger promise attached, inside one codebase — not more parallel prototypes.

---

## 9. Source map

### Canon and design

| File | What it owns |
|---|---|
| `README.md` | Project overview, controls, current status |
| `End goal` | Original full vision: pillars, world, customers, employees, rivals, crime, eras, paths, endings |
| `inital build` | Master development prompt: scope hierarchy, non-negotiables, data-driven rules |
| `Assets/_Project/Documentation/STORY_BIBLE.md` | Premise, cast, tone, Almanac rules, the two lights, endings, voice |
| `Assets/_Project/Documentation/ERAS.md` | Eight-act ladder, constraints, rival moves, signature minigames |
| `Assets/_Project/Documentation/PRODUCTS.md` | Product catalogue, ten problem axes, obsolescence, access shelf |
| `Assets/_Project/Documentation/CONTRACTS.md` | Fourteen contract families, clause anatomy, negotiation, breach cascade |
| `Assets/_Project/Documentation/MINIGAMES.md` | One verb per building, rules, worked storyboards |
| `Assets/_Project/Documentation/OFFICE_DESIGNER.md` | Cosmetic designer, plausibility checks, chaos catalogue |
| `Assets/_Project/Documentation/CONTENT_SCHEMA.md` | Stable IDs, record shapes, validation rules, save rules |
| `Assets/_Project/Documentation/DESIGN_HANDOFF.md` | Current web slices, acceptance criteria, guardrails, copy bank |
| `Assets/_Project/Documentation/GDD.md` | Condensed reference and canon summary |
| `Assets/_Project/Documentation/DEMO_TO_FULL.md` | Demo vs full-game work breakdown |
| `Assets/_Project/Documentation/OPEN_DESIGN_QUESTIONS.md` | Unresolved design questions |
| `Assets/_Project/Documentation/ROADMAP.md` | Unity stage plan |
| `Assets/_Project/Documentation/DEV_STATUS.md` | Unity stage status and known issues |
| `Assets/_Project/Documentation/SESSION_HANDOFF.md` | Last Unity handoff |

### Code

| Path | What it contains |
|---|---|
| `web/src/sim/` | Pure simulation: Almanac, guidance, inventory, content records |
| `web/src/world/` | Planet, cells, premises, building geometry |
| `web/src/net/lab.ts` | Cable puzzle, devices, ports, objectives |
| `web/src/ui/` | HUD and phone |
| `web/src/main.ts` | Composition root, loop, debug surface `window.__telo` |
| `Assets/_Project/Scripts/Simulation/` | Pure C#: network, customers, economy, outages, town, van, tech tree |
| `Assets/_Project/Scripts/Gameplay/` | MonoBehaviours: controller, interaction, equipment, cables, rack |
| `Assets/_Project/Scripts/Presentation/` | Status lights, cable rendering, prompts, wall plates |
| `Assets/_Project/Scripts/Core/` | Enums, seeded random, service locator, colors |

### Non-negotiables for any implementation

- `web/src/sim/**` stays pure: no `three`, no DOM, no `Math.random`, no `Date.now`.
- Positions are directions on a sphere; hex cells are the canonical spatial unit.
- No new dependencies, textures, or external assets in the prototype.
- Small files; one idea each.
- Every player-facing number has a home on screen.
- Notifications are for milestones and errors only.
- Never serialise `Object3D`; serialise the simulation and rebuild the view.
- Piracy is fictional social comedy, never instruction.
- The Last Mile is people, not a score.
- `npm run verify` green is the only definition of done in `web/`.

---

*End of handoff.*
