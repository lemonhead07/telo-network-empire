# Business Plan: Funding and Selling on Steam

*Not financial or legal advice. Figures are rough industry rules of thumb as of late 2026. Check the current Steamworks terms before relying on any number.*

## 1. Product

| | |
|---|---|
| Genre | First-person job sim + light business strategy |
| Hook | "Start by wiring one phone line in 1983. End up running the network a country depends on. The job changes as you play." |
| Comparable games | The hands-on job simulators (cleaning, flipping, mechanic, shop sims) and long-form management sims |
| Platforms | Windows + Linux at launch; Steam Deck verified as a target (Godot exports cleanly) |
| Price | **$19.99 in Early Access**, rising to $24.99 at 1.0. Launch discount 10–15%. Use Steam's regional pricing suggestions. |
| Model | **Premium, one-time purchase. No in-game ads, no microtransactions.** Paid DLC eras possible after 1.0 |

### Why no in-game advertising
Ads inside a paid PC game draw negative Steam reviews, and reviews drive Steam's visibility algorithm. The ad income from a niche PC audience would be tiny. "Advertising" for this project should mean **advertising the game**, not ads *in* it. A free mobile spin-off with ads could come later, but it's a different product.

## 2. How money arrives

**Steam basics.** Steam Direct charges **$100 per game**, recouped once the game earns $1,000. Valve takes **30%** (falling to 25% above $10M and 20% above $50M). Sales tax/VAT and refunds come out too. Plan on roughly **55–65% of the sticker price reaching you**: about $11–13 per $19.99 sale.

**Wishlists are what you're really building.** They're the best predictor of launch sales. A very rough rule of thumb is that first-week sales land at about 10–20% of launch-day wishlists, though it varies a lot by genre and quality. Rough targets:

| Launch wishlists | Likely first-year outcome |
|---|---|
| < 5,000 | Hobby income. Keep your day job. |
| 10,000–30,000 | Funds the next 6–18 months of part-time development |
| 50,000+ | A real business. Hire an artist or sound designer. |

## 3. The funding ladder (cheapest money first)

1. **Your time + AI-assisted development (now).** The scaffold costs nothing but hours. Keep cash spending near zero until the demo proves the hook.
2. **Free audience-building (from M1).** Short devlog clips of satisfying moments (wiring the frame, the modem screech, the interface turning from paper into a terminal). Post on YouTube Shorts, TikTok, Reddit (r/IndieDev, r/BaseBuildingGames, r/retrobattlestations) and Bluesky/X. 80s/90s tech nostalgia is a strong angle.
3. **Steam page + demo (M2).** Put the store page up as early as you have decent art. Wishlists build from that day. **Steam Next Fest** (three times a year) is the biggest free wishlist event for a demo.
4. **Crowdfunding (optional, after the demo).** Kickstarter works when you *already* have an audience: a demo, a few thousand followers or wishlists. Use it to fund art and audio, not to find an audience. Backer rewards: your name in the Promise Ledger, a BBS file area named by you.
5. **Patreon / Ko-fi (from M1).** A small, steady amount from the community, plus a private playtest Discord.
6. **Grants.** Many countries run games funds (for example the UK Games Fund, Creative Europe MEDIA, the Canada Media Fund, and national arts councils). They usually want a playable prototype and a budget. Check which ones you're eligible for where you live.
7. **Publisher deal (after the demo shows traction).** Some publishers specialise in simulators and fund development, marketing and ports in exchange for a revenue share. Typical asks: 50–70% of revenue until they recoup, then a lower share. **Read every contract with a lawyer.** Avoid deals that take your IP or bind future games.
8. **Early Access revenue (M3).** Sales fund Acts 3–7. This is the main plan; everything above just gets you here faster.

**Avoid:** "guaranteed wishlist"/bot marketing services, paying for reviews, and up-front-fee "publishers". These are common scams aimed at first-time developers.

## 4. A lean budget to Early Access

| Item | Low | Comfortable |
|---|---|---|
| Steam Direct fee | $100 | $100 |
| Art (asset packs + a few commissioned hero props + capsule art) | $500 | $4,000 |
| Audio (SFX libraries + a composer for 3–4 tracks) | $200 | $2,500 |
| Trailer editing | $0 (DIY) | $1,500 |
| Company setup, accounting, legal review | $300 | $1,500 |
| Hardware/test devices (e.g. a Steam Deck) | $0 | $600 |
| **Total** | **≈ $1,100** | **≈ $10,200** |

Engine (Godot) and tools are free. Spending more than this before you see wishlist traction is the most common way indie projects run out of money.

## 5. Timeline

| When | Milestone | Business action |
|---|---|---|
| Month 0–1.5 | M1: first hour fun | Start devlog posts; private playtest with 10–20 people |
| Month 2–3.5 | M2: demo | **Steam page live**, trailer, mailing list, Patreon |
| Next Next Fest | Demo in festival | Push for wishlists; pitch to publishers if the numbers are good |
| Month 6–9 | M3: Early Access | Launch at $19.99 |
| +6–12 months | 1.0 (Acts 3–4) | Price to $24.99, launch sale, press push |

## 6. Legal and admin checklist

- [ ] Register a business (sole trader/LLC/Ltd depending on country) before money arrives.
- [ ] Steamworks partner account: tax interview and bank details.
- [ ] Check that the name "TELO" isn't trademarked in games; register a domain and social handles.
- [ ] Keep all content fictional (no real carriers, brands or people). This is already a project rule.
- [ ] Keep licences for every asset pack, sound and font in `docs/licences/`.
- [ ] Privacy: no telemetry leaves the player's machine without opt-in.

## 7. What to measure

Wishlists per week, demo players vs. demo completions, the median minute where playtesters quit, Next Fest wishlist uplift, and devlog post → store page click-through. Decide go/no-go on a bigger budget from these numbers, not from hope.
