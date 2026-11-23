---
description: Start a focused dev session on one build-plan item.
---
This is a **dev session** for TELO. Follow `docs/SESSIONS.md` → "Dev session" and `CLAUDE.md`.

1. Read `docs/NEXT_SESSION.md`. Name the lane (gameplay or geometry) and run `tools/branches.sh start <lane>`: it merges master and the lane in and lists stranded work on other branches. If your item is already done on one of them, merge that instead of rebuilding it.
2. Take the item named here, or else the top **[Dev]** item of "Up next" in `docs/BUILD_PLAN.md`: $ARGUMENTS
3. State it in one line: "This session: X. Done when: Y." Then start; don't wait for approval.
4. Scope fence: anything not needed for this item goes as one line into `docs/IDEAS.md` Inbox (bugs: BUILD_PLAN engineering backlog). Then carry on.
5. For tech tree work, read only the needed parts of `docs/TECH_TREE.md`: §1–§2, then grep the node ids you're touching.
6. Build after each step, screenshot anything visual, run tests + smoke only before committing sim-rule changes.
7. Finish: tick BUILD_PLAN, rewrite NEXT_SESSION.md, commit, then `tools/branches.sh finish <lane>` (pushes your branch and the lane; never master). If the item didn't fit, stop at a playable point and split the remainder into new "Up next" items.
