# Launch, Marketing and Keeping It Going

*Idea session, 2026-11-26. Decisions D52–D55 (proposed, waiting on the owner). Ready-to-paste text: `docs/marketing/KOFI.md` (the Ko-fi page) and `docs/marketing/STEAM_PAGE.md` (the store page). Money background: `docs/BUSINESS_PLAN.md`. Dates checked against Valve's published 2027 calendar on 2026-11-26; check them again in Steamworks before relying on them.*

## 1. Where we are

The whole of early access plays start to finish: Acts 0–5, about 26 hours for the test bot (a perfect player), ending when you light the data centre and find the thank-you card on the night-shift desk. There's a title screen, saves, settings and a release workflow that builds Windows and Linux zips. **What players would notice first:** there's no sound at all, some screens still freeze you while you lean in, and the floating ▼ $ ! markers and text strip are still on screen.

## 2. What's left before early access

### In the game (the launch path in `BUILD_PLAN.md`)

| # | Item | What it fixes | Size |
|---|---|---|---|
| 1 | **MK1** store pictures (new) | Steam needs capsule art and at least 5 screenshots before the page can go up, and today's pictures are test shots (flat midday light, debug text) | 1 session |
| 2 | **EA7** sound | A silent game feels unfinished in the first ten seconds | 1 session |
| 3 | **IW1** screens under the crosshair | The new feel (D51): no leaning in, no freezing | 1–2 sessions |
| 4 | **Owner playtest** + fix pass | The bot plays perfectly; people don't. One evening of Acts 0–2 | 1 evening + 1 session |
| 5 | **LK1** launch kit (new) | Saves in a plain `TELO` folder (Steam Cloud can sync it), "where are my saves and logs" for bug reports, the credits as a 1983 phone directory | 1 session |
| 6 | **DEMO1** the demo (new) | Steam Next Fest needs a free demo: Act 0, ending on a card; the save carries into the full game | 1 session |
| 7 | **MK2** clips and trailer (new) | 20–40 second clips for posting and a 60–90 second trailer, recorded from the real game | 1–2 sessions |

Small things for the fix pass: "Sol's Electronics's" on the "what next" line (engineering backlog), a confirm before a save overwrites a slot, PIP's bubble covering the switchboard.

### Around the game (only the owner can do these)

| When | What | Time |
|---|---|---|
| This week | **Steamworks account:** sign up at partner.steamgames.com, pay the $100 Steam Direct fee (you get it back once the game earns $1,000), do the tax interview and add bank details. Valve checks your identity, which can take days, and a new game has to wait about 30 days after the fee before it can release. Start now. | 1 hour, then waiting |
| This week | **Ko-fi:** paste in the page from `docs/marketing/KOFI.md`, set up the tiers, post the first post | 1 hour |
| Before the page goes up | **Name check:** search "TELO" in the Steam store and a trademark search. Also know that in Argentine Spanish "telo" is slang for a love hotel. "TELO: Network Empire" is probably fine, but decide before the page is public, because renaming later loses momentum | 20 min |
| By 11 Dec | **Store page:** paste in `docs/marketing/STEAM_PAGE.md`, upload MK1's pictures, fill in the content survey and the AI disclosure (D54), submit for review | 1–2 hours |
| Before launch | **Run the Windows build** on a Windows PC (nobody has yet), or ask a friend to | 30 min |
| Before 10 Jan | **Register for Next Fest** in Steamworks (from the game's page, not the demo's) | 15 min |

## 3. When: three ways to launch

Steam Next Fest is the biggest free way for a small game to be seen: a week where Steam puts thousands of demos in front of players. **A game can only be in it once, and only before it's released.** So the choice of launch date is really a choice about Next Fest.

| | A. As soon as Steam allows | **B. February Next Fest, launch in March** (recommended) | C. June Next Fest, launch in July |
|---|---|---|---|
| Store page live | mid-December | mid-December | mid-December |
| Demo | none | Next Fest, 22 Feb – 1 Mar 2027 | Next Fest, 14–21 Jun 2027 |
| Early access | **Thu 21 Jan 2027** | **Thu 11 Mar 2027** | **Thu 15 Jul 2027** (after the Summer Sale, 24 Jun – 8 Jul) |
| Wishlists at launch | very few (about five weeks of page) | a few months of page plus the Next Fest bump | seven months of page plus Next Fest |
| Next Fest | lost for good | used | used, with a much more polished game |
| Good | Fastest money and feedback | Seven weeks later than A, keeps the one free festival, still in the first quarter | Models, IW2, crews (EA4) and the data-centre games (DC3–DC8) could all be in |
| Bad | Launching to nobody; Steam shows new games more when they sell well in the first days, and that moment only comes once | A tight run of sessions: sound, IW1, playtest, demo and trailer by early February | Four months later; others in the genre get there first |

**Recommendation: B (D52).** It's the race-to-market choice that doesn't throw away the launch. A buys seven weeks and costs the festival forever; C buys polish the owner has said can wait.

## 4. The calendar for B

| Week of | Claude (sessions) | Owner |
|---|---|---|
| 30 Nov | **MK1** store pictures | Steamworks account and fee; Ko-fi page and first post |
| 7 Dec | **EA7** sound | Store page filled in; **submit for review by Fri 11 Dec** (Valve takes a few working days and slows down over the holidays) |
| 14 Dec | **IW1** | Page goes live as "Coming soon". Post it everywhere (§6) |
| 21 Dec | IW1 continued, if needed | **Playtest:** one evening, Acts 0–2, with a notebook (what confused you, when you got bored) |
| 4 Jan | Fix pass from the playtest | **Register for Next Fest by Sun 10 Jan, 11:59pm Pacific** |
| 11 Jan | **LK1** + **DEMO1** | Make the demo's app in Steamworks |
| 25 Jan | **MK2** clips and trailer | Upload the demo; **submit it for review by Mon 1 Feb** (Valve asks for three weeks) |
| 8 Feb | Fixes from demo feedback | Next Fest materials due **Mon 8 Feb**; trailer on the store page |
| **22 Feb** | Small fixes only | **Next Fest** (to 1 Mar): answer every forum post, post a clip a day |
| 1 Mar | The launch build | Set the release date; submit the full build for review |
| **Thu 11 Mar** | Watch the forums; fix what breaks | **Early access.** A 10% launch discount for the week, which ends as the Spring Sale starts (18–25 Mar) |
| Apr–Jun | A named free update every 4–6 weeks (§8) | One post per update |

## 5. Steam settings worth knowing

- **Steam Cloud without code:** Steamworks' "Auto-Cloud" can sync a folder of files. Once LK1 moves the saves into a plain `TELO` folder, it's two paths in Steamworks: Windows `%APPDATA%/TELO/saves`, Linux `~/.local/share/TELO/saves`.
- **The demo is its own app** on Steam, attached to the main game. It uses the same save folder, so a demo save carries straight on in the full game.
- **AI disclosure** (D54): Steam's content survey asks about AI-made content that players see or hear. TELO's code, in-game text and the scripts that build its models were written with Claude, so the honest answer is "yes, pre-generated". Wording in `STEAM_PAGE.md`. Nothing runs AI while you play, so the extra rules for live AI don't apply.
- **Keys for Ko-fi supporters:** Steam lets you request keys to give away. Keep within Valve's key rules (don't sell keys for less than the Steam price). The Ko-fi plan only gives keys to people who have already paid more than the price (D53).

## 6. Marketing: what to say, and where

**The hook, in one line:** *wire your first phone line by hand in 1983; end up running the data centre a planet depends on; the job itself changes as you play.* Every picture and clip should show one side of that: hands on a screwdriver, then a ticket handed to Jory, then a terminal, then a lit rack.

**Who it's for, in order:**
1. **Job-sim and management players.** They buy on Steam, watch YouTubers, and follow tags like Simulation, Management and Automation.
2. **People who've done the work:** telecom engineers, network admins, anyone who has punched down a cable. They spread the word and they'll laugh at the right things (NezTel's "Nez Ensures Zero-downtime"). Communities: networking, sysadmin and homelab forums. They're strict about self-promotion, so post once, as a person, with something worth seeing.
3. **Retro-tech fans:** BBS, modems, the 80s. A modem handshake (after EA7) is a ready-made clip.

**Where, ranked by return on the owner's time:**
1. **The Steam page.** Every wishlist is a person Steam will tell when the game launches. Nothing else comes close.
2. **Short clips** (YouTube Shorts, TikTok, Bluesky/X, Reddit). Claude makes them (MK2); the owner posts them. One a week is plenty. Captions that ask a question ("would you bodge it?") get replies.
3. **Reddit, a few times, done well:** indie and sim game communities, then the telecom and retro ones ("Who it's for" above). Read each one's self-promotion rule first.
4. **YouTubers and streamers:** at Next Fest, email the demo link to small and mid-sized sim and retro-tech channels (a short personal note, one picture, the link). At launch, offer keys. Keymailer-style services can help; never pay for coverage.
5. **Ko-fi:** the home for people who want to follow closely and chip in (§7).
6. **Later:** a Discord server once people ask for one (Ko-fi can give members a role there automatically), a press kit page, small online sim showcases that are free to enter.

**Avoid:** paid wishlist or "guaranteed exposure" services, paid reviews, and publishers who charge you up front. All three are common traps for first games (`BUSINESS_PLAN.md`).

**The weekly routine (D55, about an hour of the owner's time a week):**
- Every dev session that builds something you can see ends with a 20–30 second clip (once MK2 exists) and a two-line caption, added to `docs/marketing/POSTS.md` and sent to the owner.
- The owner posts the week's best clip in two or three places, and replies to comments.
- Once a month, Claude drafts the Ko-fi "letter from the switchboard" from the month's `NEXT_SESSION.md` notes; the owner posts it.

**Clip list for MK2** (each one moment, 20–40 seconds, no explaining):
1. First customer: jumper in, throw the TEST switch, the lamp lights, the modem screeches.
2. "Bodge it": press R, cut to three weeks later, a ! appears over their house.
3. The town at dusk: windows light up, people walk home.
4. Handing Jory a ticket at the door.
5. Lines in: the evening rush, lamps on the rack, engaged tones.
6. A pole line marching out to the farm (time-lapse).
7. The ferry to Farland.
8. One small planet: take off from town, fly once round, land at your door.
9. The same desk in 1983 and in Act 5.
10. The data centre lights up at night and the routes home glow across the land (the finale; keep it short).

## 7. Ko-fi

Full text to paste: `docs/marketing/KOFI.md`. In short (D53):
- **Tips** renamed "a line card" ($3).
- **Three memberships:** *Subscriber* $3 (a monthly letter, your name in the game's phone directory), *Line crew* $7 (test builds before Steam, a vote on what's next), *Exchange* $15, limited to 25 (name a file on the Board, a Steam key at launch).
- **A goal:** proper capsule art from a real artist, $500.
- **Don't sell the game on Ko-fi before Steam.** Sales in the first days on Steam decide how much Steam shows the game; every early buyer moved to Ko-fi is one fewer there.
- **Stay on Ko-fi's free plan** until memberships and shop sales pass about $240 a month. Below that, its 5% fee costs less than the Gold plan (about $12 a month; check Ko-fi's current price). One-off tips carry no Ko-fi fee on any plan; PayPal or Stripe take their own cut either way.

## 8. Keeping it going after launch

Most small games sell the bulk of their copies at launch and in sales. What keeps one going is a run of reasons for people to look again. Ranked by what they return for the effort:

1. **A named free update every 4–6 weeks.** Each is a Steam event, a clip, a Ko-fi post and a reason for a YouTuber to come back. The queue already exists: "The Core" update (DC3–DC8: the rack, the pin board, crossed patches, follow the lights, Dana and the night shift), crews (EA4), project sites (EA5), Shop days (SD1–SD10), then Act 6 (mobile masts) as the big one.
2. **Seasonal sales,** starting with the Summer Sale (24 Jun – 8 Jul 2027). Small discounts (10–20%) in early access.
3. **Ko-fi memberships:** small, but steady and every month.
4. **Translation** (later in early access): German and Simplified Chinese are big markets for sims. The words have to be moved out of the code first (an engineering backlog item), so plan it as one dev item, then pay for translation from sales.
5. **Steam Deck:** controller support earns the "Verified" badge, and Deck owners buy a lot of sims. Godot exports to it cleanly; the work is controls and readable text.
6. **A Supporter Pack** on Steam ($4.99): wallpapers, the Almanac as a printable PDF, the soundtrack once there is one. Almost free to make; lets fans pay more without leaving Steam.
7. **Mods later:** the game's content is already data files (JSON). Steam Workshop support would keep a long tail of players.
8. **Grants:** many countries fund games with a playable build (the UK Games Fund, Creative Europe MEDIA, the Canada Media Fund, arts councils). Tell Claude which country you're in and it can find the ones you qualify for.
9. **A publisher,** only if Next Fest numbers are strong and someone offers. Read every contract with a lawyer; never give away the IP.

**Price (open, Q2):** the business plan says $19.99. With box-like buildings and no music yet, **$14.99 in early access, rising to $19.99 at 1.0,** would make "worth it" reviews easier to get. Reviews are what Steam uses to decide what to show, and early buyers like a price that only goes up.

## 9. Questions for the owner

- **Q1** Launch plan: A, **B** or C (§3)?
- **Q2** Price: **$14.99** or $19.99 in early access?
- **Q3** Ko-fi: are the three tiers and the "nothing for sale before Steam" rule right (§7)? Can you run a monthly members' post?
- **Q4** How it's made: say it plainly on Steam and Ko-fi (**recommended, D54**), or only where Steam requires it?
- **Q5** Which country are you in (for grants, tax and the business setup)?

## Sources

Steam Next Fest and sale dates: [Steamworks upcoming events](https://partner.steamgames.com/doc/marketing/upcoming_events), [February 2027 Next Fest](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest/feb_2027), [June 2027 Next Fest](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest/june_2027), [PC Gamer on the 2027 calendar](https://www.pcgamer.com/games/steam-reveals-all-the-sale-dates-and-themed-events-for-the-first-half-of-2027/). Coming-soon rules: [Steamworks coming soon](https://partner.steamgames.com/doc/store/coming_soon), [release dates](https://partner.steamgames.com/doc/store/release_dates), [Next Fest rules](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest). AI disclosure: [PC Gamer on the January 2026 change](https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/). Ko-fi fees: [Ko-fi help: does Ko-fi take a fee?](https://help.ko-fi.com/hc/en-us/articles/360002506494).
