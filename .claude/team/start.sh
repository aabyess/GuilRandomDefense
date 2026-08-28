#!/bin/zsh
# 사용법: .claude/team/start.sh <역할이름> [모델]
#   역할: 리서치담당 | 구현담당1 | 구현담당2
#   모델 기본값: claude-sonnet-5  (Fable은 PM 세션에만 사용)
ROLE="$1"
MODEL="${2:-claude-sonnet-5}"
DIR="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$DIR" || exit 1
exec claude -n "$ROLE" --model "$MODEL" "$(cat ".claude/team/$ROLE.md")"
