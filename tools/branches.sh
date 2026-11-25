#!/usr/bin/env bash
# Branch helper for AI sessions (docs/BRANCHES.md). Only AI sessions work on this repo, so this does the bookkeeping.
#
#   tools/branches.sh                 how far each lane is ahead of master, and every branch holding work in neither lane nor master
#   tools/branches.sh start <lane>    session start: merge master and the lane into the current branch
#   tools/branches.sh finish <lane>   session end: merge the current branch into the lane and push the lane
#
# <lane> is "gameplay" or "geometry" (feature/gameplay, feature/geometry). Nothing here ever pushes to master.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

fetch() {
  for i in 1 2 3 4; do git fetch -q origin '+refs/heads/*:refs/remotes/origin/*' && return 0; sleep $((i * 2)); done
  echo "git fetch failed" >&2; return 1
}

lane_ref() {
  case "${1:-}" in
    gameplay|feature/gameplay) echo feature/gameplay ;;
    geometry|feature/geometry) echo feature/geometry ;;
    *) echo "lane must be gameplay or geometry" >&2; exit 2 ;;
  esac
}

stranded() {
  # work not reachable from master or either lane, newest first
  local found=0
  while read -r b; do
    case "$b" in origin/HEAD|origin/master|origin/main|origin/feature/gameplay|origin/feature/geometry) continue ;; esac
    local n
    n=$(git rev-list --count "$b" --not origin/master origin/feature/gameplay origin/feature/geometry 2>/dev/null || echo 0)
    [ "$n" -gt 0 ] || continue
    found=1
    printf '%3s commit(s)  %-45s %s\n' "$n" "${b#origin/}" "$(git log -1 --format='%cs  %s' "$b" | cut -c1-80)"
  done < <(git for-each-ref --sort=-committerdate --format='%(refname:short)' refs/remotes/origin)
  [ "$found" = 1 ] || echo "nothing stranded: every branch's work is in master or a lane"
}

cmd=${1:-list}
case "$cmd" in
  list)
    fetch
    for l in feature/gameplay feature/geometry; do
      echo "$l: $(git rev-list --count origin/master..origin/$l) ahead of master, $(git rev-list --count origin/$l..origin/master) behind"
    done
    echo "Branches with work in neither lane nor master:"
    stranded
    ;;
  start)
    lane=$(lane_ref "${2:-}")
    fetch
    git merge --no-edit origin/master
    git merge --no-edit "origin/$lane"
    echo
    echo "Up to date with master and $lane. Stranded work elsewhere (check before building it again):"
    stranded
    ;;
  finish)
    lane=$(lane_ref "${2:-}")
    here=$(git rev-parse --abbrev-ref HEAD)
    [ -z "$(git status --porcelain)" ] || { echo "commit your work first" >&2; exit 1; }
    fetch
    git merge --no-edit "origin/$lane"          # bring in anything the lane got meanwhile
    for i in 1 2 3 4; do git push -q origin "HEAD:$here" && break; sleep $((i * 2)); done
    for i in 1 2 3 4; do git push -q origin "HEAD:refs/heads/$lane" && break; sleep $((i * 2)); done
    echo "$here is merged into $lane and pushed."
    ahead=$(git rev-list --count "origin/master..HEAD")
    if [ "$ahead" -gt 0 ]; then
      echo "$lane is $ahead commit(s) ahead of master: make sure the standing PR '$lane -> master' is open (BRANCHES.md, 'Pull requests into master') and tell the owner to merge it."
    else
      echo "$lane is level with master: nothing for the owner to merge."
    fi
    ;;
  *) sed -n '2,9p' "$0"; exit 2 ;;
esac
