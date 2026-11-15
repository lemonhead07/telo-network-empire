# Bookings, Service and NezTel

*Idea session, 2026-11-15, from the owner's direction. Decisions D35–D39. Builds on `NEZ_AND_THE_SPINE.md` (Nez, the planner) and `AGES.md`.*

## 1. Who Nez is (D35, D36)

**Nez is your previous run.** You built all this once already. Your memory was wiped and the world reset to 1983; PIP's Almanac is what survived of your notes. Nez is who you became, still running the company you built, on the far side of the planet. **Beating Nez means stopping yourself.** (Answers N2.)

**The company is NezTel.** Its motto is a recursive acronym, in the style of PHP:

> **NEZ: Nez Ensures Zero-downtime.**

It's printed on every van, bill and leaflet. It is a lie, and the game is about proving it: NezTel's service runs at 99%, never better. Its engineers are known in every town as **the Network Nerds**. Content that says "the national phone monopoly" becomes "NezTel" once the player has met the name (stage 1, the phone bill).

**Far away means hard to reach, not forbidden.** NezTel's exchange, Nadir, is at the antipode. Going there to talk to Nez is an expedition (boat, later aircraft), not a stroll. That is the "Contact" stage.

## 2. The booking loop (D37)

One easy loop that keeps growing:

```
 good service ─► reputation ─► word of mouth ─┐
                                               ├─► bookings on the calendar ─► more than you can do?
 ads (you pay) ────────────────────────────────┘          │                         │
                                                           ▼                         ▼
                                              jobs done by people (1–5 a day each)   hire, or they go to NezTel
```

- **Bookings come from two places.** *Word of mouth* is free and grows with your reputation (happy customers tell their neighbours). *Ads* cost money each week and bring more: leaflets first, then the newspaper, a shop sign, the radio, one town further each time. Word of mouth is the reward for good work; ads are how you push.
- **Bookings land on the calendar with a date.** The timesheet (spine step 5) is where you see them. A booking has a job, a place and a day the customer wants.
- **Each person does 1 to 5 jobs a day.** You start at 3. A new hire starts at 1–2 and gets faster with practice (mastery, `TECH_TREE.md` §4), up to 5. Nobody ever does more than 5.
- **Too many bookings?** They wait. Left too long, they give up and go to NezTel (this already happens to orders). So the pressure is always the same easy choice: **hire someone, or turn the ads down.**
- **Every turn of the loop is bigger:** more people means more bookings handled, which means more happy customers and more word of mouth, which means you need more people. Later the calendar zooms out (day, week, month) as `NEZ_AND_THE_SPINE.md` §7 describes.

**Keep it easy:** two numbers on the phone (bookings this week, jobs your people can do this week) and one lever (ads up or down). No spreadsheets.

## 3. Service is the weapon (D38)

- **Your service level is measured in nines:** the share of the time your customers' lines work, from faults, outages and waiting. 99% means a line is down for over three days a year. 99.999% means five minutes.
- **NezTel is unbeatable on size and money, and stuck at 99%.** It never improves, because nobody has ever made it.
- **People's standards rise with the eras.** In 1983, 99% is fine. By the dial-up years people notice, and by the internet years 99% is a scandal. Each era raises the bar a nine.
- **NezTel loses because it has no competition, and then it has you.** When your service is better than NezTel's by a nine, its customers start to switch: a few a week, more as the gap grows. Nez doesn't lose a fight; Nez loses its customers' patience.

## 4. VIP jobs (D39)

- Some bookings are **VIP**: the clinic, the bank, the radio station, the mayor, later a stock exchange and a launch site. They are marked on the calendar.
- A VIP job asks for **near-perfect service:** done on the day booked, no bodges, tested, and your network at or above the VIP's nines.
- **Pay-out: a big cash bonus plus reputation.** Reputation brings more word of mouth, so the bonus feeds the booking loop, and fewer people go to NezTel.
- Miss the standard and the VIP goes to NezTel, publicly. Nobody loses progress, only face.

## 5. What exists already that this uses

Tickets and staff (D25), the timesheet (spine 5), orders that go to NezTel when left too long (`OrderRules`), satisfaction per customer, faults and repairs, Nez stages and par times (spine 8). New: reputation, ads, bookings with dates, a per-person daily job cap, service level in nines, VIP bookings.

## 6. Open questions for the owner

- B1. Does reputation replace satisfaction, or sit on top of it (one town-wide number made from everyone's satisfaction)? *Leaning: on top of it.*
- B2. Should a skilled person ever reach 5 installs a day, or should installs count as 2 "slots" and quick jobs as 1? *Leaning: slots, so 5 is quick jobs only.*
- B3. When does the player learn Nez is them? *Leaning: Act 7, from PIP's last Almanac page ("Exchange 0").*
