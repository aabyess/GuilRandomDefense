#!/bin/zsh
# 팀원 세션 셋(구현담당1·구현담당2·blender)을 Orca 탭으로 띄운다 — 새 PM 세션이 이어받을 때 PM이 부른다(사장님 10-03).
#   사용법: .claude/team/spawn_team.sh [--fresh] [역할 ...]     (역할을 안 주면 셋 다)
#   기본: 같은 이름으로 연결된 Orca 탭이 이미 있으면 그 역할은 건너뛴다(같은 이름 세션이 둘이면 SendMessage가 헷갈린다).
#   --fresh: 같은 이름의 옛 탭을 닫고 새로 띄운다 — 사장님이 「next.md 읽고 진행해」로 하루치 세션을 갈아 끼울 때(토큰 비용).
#   각 탭은 start.sh <역할>을 실행한다 → claude -n <역할> --model claude-sonnet-5-5 + 역할 지시문(.claude/team/<역할>.md).
#   ⚠️ 측정 체인(nohup)은 세션과 따로 돌아 탭을 닫아도 안 죽는다. 에디터 판(gameshot)은 에디터 안에서 돌아 역시 안 죽는다.
DIR="$(cd "$(dirname "$0")/../.." && pwd)"
FRESH=0
if [[ "$1" == "--fresh" ]]; then FRESH=1; shift; fi
ROLES=("$@")
(( ${#ROLES} == 0 )) && ROLES=(구현담당1 구현담당2 blender)

if ! command -v orca >/dev/null 2>&1; then
  echo "❌ orca CLI가 없다 — Orca 안의 터미널에서 실행할 것"; exit 1
fi

# 연결된 탭: 「이름<탭>핸들」 줄. 제목 앞 상태 기호(✳ ◐ 등)는 뗀다.
LIVE=$(orca terminal list --json 2>/dev/null | python3 -c '
import json, sys, unicodedata
d = json.load(sys.stdin)
for t in d.get("result", {}).get("terminals", []):
    if not t.get("connected"): continue
    title = (t.get("title") or "").strip()
    parts = title.split(" ", 1)
    if len(parts) == 2 and not parts[0].isalnum(): title = parts[1]
    print(unicodedata.normalize("NFC", title) + "\t" + t["handle"])
')

for ROLE in "${ROLES[@]}"; do
  if [[ ! -f "$DIR/.claude/team/$ROLE.md" ]]; then
    echo "❌ $ROLE: 역할 지시문 .claude/team/$ROLE.md 없음 — 건너뜀"; continue
  fi
  NFC_ROLE=$(python3 -c 'import sys,unicodedata;print(unicodedata.normalize("NFC",sys.argv[1]))' "$ROLE")
  HANDLES=($(print -r -- "$LIVE" | awk -F'\t' -v r="$NFC_ROLE" '$1 == r {print $2}'))
  if (( ${#HANDLES} > 0 )); then
    if (( FRESH )); then
      for H in "${HANDLES[@]}"; do
        orca terminal close --terminal "$H" --tab >/dev/null 2>&1 && echo "🗑  $ROLE: 옛 탭 닫음($H)"
      done
    else
      echo "⏭  $ROLE: 이미 떠 있음 — 건너뜀(새로 띄우려면 --fresh)"; continue
    fi
  fi
  if orca terminal create --worktree "path:$DIR" --title "$ROLE" --command "$DIR/.claude/team/start.sh $ROLE" >/dev/null 2>&1; then
    echo "✅ $ROLE: Orca 탭 생성(claude -n $ROLE · Sonnet 5.5)"
  else
    echo "❌ $ROLE: orca terminal create 실패"
  fi
done
