#!/bin/zsh
# PM 세션을 새로 띄우고 옛 PM 탭을 닫는다 — PM 대화가 길어졌을 때 PM이 스스로 판단해서 부른다(사장님 10-03).
#   사용법: .claude/team/spawn_pm.sh
#   순서: ① 옛 「pm」 탭 핸들을 기억 → ② 새 탭(claude -n pm · Opus 5.5 · 지시문 pm.md) → ③ 20초 뒤 옛 탭을 닫는다(nohup — 이 스크립트를 부른 옛 PM 자신이 닫혀도 끝까지 돈다).
#   ⚠️ 부르기 전에: NEXT_SESSION.md 갱신·커밋·푸시, 사장님께 한 줄 알림. 부른 뒤 옛 PM은 아무것도 하지 않는다(곧 닫힌다).
DIR="$(cd "$(dirname "$0")/../.." && pwd)"
command -v orca >/dev/null 2>&1 || { echo "❌ orca CLI가 없다 — Orca 안의 터미널에서 실행할 것"; exit 1; }

OLD=($(orca terminal list --json 2>/dev/null | python3 -c '
import json, sys
for t in json.load(sys.stdin).get("result", {}).get("terminals", []):
    if not t.get("connected"): continue
    title = (t.get("title") or "").strip()
    parts = title.split(" ", 1)
    if len(parts) == 2 and not parts[0].isalnum(): title = parts[1]
    if title.lower() == "pm": print(t["handle"])
'))

if ! orca terminal create --worktree "path:$DIR" --title "pm" --command "$DIR/.claude/team/start.sh pm claude-opus-5-5" >/dev/null 2>&1; then
  echo "❌ pm: orca terminal create 실패 — 옛 탭은 그대로 둔다"; exit 1
fi
echo "✅ pm: 새 Orca 탭 생성(claude -n pm · Opus 5.5)"

if (( ${#OLD} > 0 )); then
  CMD="sleep 20"
  for H in "${OLD[@]}"; do CMD="$CMD; orca terminal close --terminal $H --tab"; done
  nohup zsh -c "$CMD" >/dev/null 2>&1 &!
  echo "🗑  옛 pm 탭 ${#OLD}개를 20초 뒤 닫는다(${OLD[*]})"
fi
