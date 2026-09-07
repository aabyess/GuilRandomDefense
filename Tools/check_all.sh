#!/bin/zsh
# 커밋 전에 한 번 돌리는 전체 검사. Unity를 열지 않고 돈다.
#
# 왜 필요한가 — 검사기가 넷으로 흩어져 있어서 "어느 걸 돌려야 하나"가 매번 애매했고,
# 실제로 안 돌린 채 커밋한 적이 있다. 하나로 묶는다.
#
# 사용법:
#   Tools/check_all.sh          모두 돌린다
#   Tools/check_all.sh --fast   컴파일 검사는 건너뛴다(Unity 호출이 제일 느리다)
#
# 종료 코드: 0=전부 통과, 1=하나 이상 실패.
# ⚠️ 각 검사의 "실패"는 성격이 다르다 — 아래 출력에서 어느 검사가 걸렸는지 보고 판단할 것.

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

FAST=0
[[ "$1" == "--fast" ]] && FAST=1

fail=0
run() {
  local label="$1"; shift
  echo "\n── $label ──"
  if "$@"; then
    echo "  ✅ 통과"
  else
    echo "  🔴 실패 (종료코드 $?)"
    fail=1
  fi
}

if [[ $FAST -eq 0 ]]; then
  run "C# 컴파일" zsh Tools/compile_check.sh
else
  echo "\n── C# 컴파일 ── (--fast: 건너뜀)"
fi

run "수치 불변식 (이동속도·hitCount·확률·버프게이트)" python3 Tools/check_numeric_invariants.py
run "로스터↔스킬 배정 불변식"                        python3 Tools/check_assignment_invariants.py
run "필수 필드"                                      python3 Tools/check_required_fields.py
run ".meta 짝"                                       python3 Tools/check_meta.py

echo ""
if [[ $fail -eq 0 ]]; then
  echo "전부 통과."
else
  echo "🔴 실패한 검사가 있다. 위 출력을 보고 고칠 것."
fi
exit $fail
