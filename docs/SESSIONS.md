# Session modes: Idea vs Dev

Every session is **one** of these two. A dev session also names its **lane**, geometry or gameplay, and works on that branch (`docs/BRANCHES.md`). Say which at the start (`/idea` or `/dev` in Claude Code), or Claude asks. Mixing them is how sessions sprawl and burn tokens.

| | Idea session | Dev session |
|---|---|---|
| **Goal** | Explore, decide, write it down | Ship one playable slice |
| **Touches** | `docs/` only (plus design canvases) | `src/`, `game/`, `tests/`, and the docs listed below |
| **Never touches** | `src/`, `game/`, `tests/` | `TELO_GAMEPLAY_HANDOFF.md`, `docs/TECH_TREE.md` design sections (except ticking status) |
| **New idea mid-way** | Write it up properly | One line in `docs/IDEAS.md`, then back to the task |
| **Ends with** | Decisions recorded + items marked **Ready** in `BUILD_PLAN.md` | Builds, screenshot checked, `NEXT_SESSION.md` updated, committed and pushed |

---

## Idea session

**Use when:** brainstorming mechanics, branches, story, tone, balance numbers, "what if" questions.

1. Read only what the topic needs: `docs/IDEAS.md` (inbox), the relevant section of `docs/TECH_TREE.md` or `TELO_GAMEPLAY_HANDOFF.md`, and `docs/DECISIONS.md` to avoid re-arguing locked calls.
2. Talk it through with the owner. Offer 2–3 concrete options with trade-offs; let the owner pick.
3. Write results where they belong:
   - A locked choice → a new row in `docs/DECISIONS.md` (D17, D18…), with the why.
   - Tech tree changes → edit `docs/TECH_TREE.md` (nodes, tasks, numbers, open questions).
   - Anything else worth keeping → `docs/IDEAS.md` under **Shaped**.
4. **Promote** anything ready to build into `docs/BUILD_PLAN.md` as a task that meets the *Definition of Ready* below, under **Up next**.
5. Commit docs only: `docs: <what was decided>`.

## Dev session

**Use when:** writing code, content JSON, models, sounds, fixing bugs.

1. Read `docs/NEXT_SESSION.md` first. Then `CLAUDE.md` if not already loaded. Run `tools/branches.sh start <lane>` and read its list of stranded work (`docs/BRANCHES.md` rule 6). Open other docs **only for the section you need** (grep for the node id, e.g. `lines.frame`), not whole files.
2. Pick **one** item: the top **[Dev]** item of **Up next** in `BUILD_PLAN.md` unless the owner names another. State it back in one line: *"This session: X. Done when: Y."*
3. **Scope fence.** Work only on that item. If you or the owner think of something else:
   - a bug that blocks the item → fix it;
   - anything else → one line in `docs/IDEAS.md` **Inbox** (or the engineering backlog for bugs), then carry on.
4. Build the smallest slice that's playable, following CLAUDE.md (pure sim, data-driven, develop lots / test little):
   - `dotnet build game/Telo.Game.csproj` after each step;
   - `--shot` screenshot for anything visual and look at it;
   - `dotnet test` + `--smoke` only before a commit that changes sim rules.
5. If the item turns out bigger than one session, stop at a playable point, split the rest into new **Up next** items, and say so.
6. End: tick the item in `BUILD_PLAN.md`, rewrite `docs/NEXT_SESSION.md` (where things stand, the next item, rough edges), commit with a plain message, then `tools/branches.sh finish <lane>` (pushes the session branch into its lane, never master).

### Definition of Ready (an item a dev session can take)

- **Goal:** one sentence, player-facing ("You can hand the drop wire to Dave and he does it at 85%").
- **Where:** the sim files / content files / scenes it touches, and the TECH_TREE ids involved.
- **Done when:** what the screenshot or play-through shows, plus any rule a test should pin.
- **Not in scope:** the tempting next thing, named so it stays out.
- **Props:** every visible thing it adds, as a prop id with a brief (size, pivot, anchors, states, three takes, one charm detail), shipped as a labelled box. See `docs/MODELS_AND_VARIETY.md`.
- **Size:** fits one session. If not, split it.

### Token-saving habits (both modes)

- Don't re-read `TELO_GAMEPLAY_HANDOFF.md` in full; it's the vision, not the task list. Grep it.
- Prefer `grep -n` + reading a range over reading whole files.
- Generate repetitive content (JSON for a branch) with a short script from the spec tables, then review it, rather than typing it out.
- One commit per finished slice; don't hold a session's work uncommitted.
