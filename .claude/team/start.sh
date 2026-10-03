#!/bin/zsh
# 사용법: .claude/team/start.sh <역할이름> [모델]
#   역할: 구현담당1 | 구현담당2 | blender | pm   (역할 지시문 = .claude/team/<역할>.md)
#   모델 기본값: claude-sonnet-5-5  (사장님 10-03 — 팀원은 Sonnet 5.5 · PM 교체는 spawn_pm.sh가 claude-opus-5-5로)
#   권한: --dangerously-skip-permissions — 사장님이 띄우는 PM과 같은 권한 묶음이어야 세션 간 메시지가 승인 대기 없이 오간다.
# 보통은 직접 부르지 않고 spawn_team.sh가 Orca 탭마다 부른다.
ROLE="$1"
MODEL="${2:-claude-sonnet-5-5}"
DIR="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$DIR" || exit 1
exec claude --dangerously-skip-permissions -n "$ROLE" --model "$MODEL" "$(cat ".claude/team/$ROLE.md")"
