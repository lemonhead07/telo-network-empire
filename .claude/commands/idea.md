---
description: Start an idea (design) session. Docs only, no code.
---
This is an **idea session** for TELO. Follow `docs/SESSIONS.md` → "Idea session".

- Do not edit anything in `src/`, `game/` or `tests/`.
- Read only what the topic needs: `docs/IDEAS.md`, `docs/DECISIONS.md`, and the relevant section of `docs/TECH_TREE.md` or `TELO_GAMEPLAY_HANDOFF.md` (grep, don't read whole files).
- Give 2–3 concrete options with trade-offs and let the owner choose.
- Record locked choices in `docs/DECISIONS.md`, tree changes in `docs/TECH_TREE.md`, everything else in `docs/IDEAS.md`.
- Promote anything build-ready into `docs/BUILD_PLAN.md` "Up next", written to the Definition of Ready.
- Start with `tools/branches.sh start gameplay` (design docs ride the gameplay lane). Commit docs only, then `tools/branches.sh finish gameplay`.

Topic: $ARGUMENTS
