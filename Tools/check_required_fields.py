#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""필드가 없으면 조용히 위험한 기본값으로 켜지는 자리를 찾는다. 읽기 전용 — 아무것도 고치지 않는다.

"필드가 없다"와 "값이 없다(0/false)"는 다르다. Unity ScriptableObject는 직렬화된 키가
아예 빠져 있으면 C# 필드 선언의 기본값으로 조용히 채운다 — 그 기본값이 "안전한 무효과"면
괜찮지만, 아래 7개는 기본값이 "의도치 않게 무언가를 허용/해제하는 쪽"이라 위험하다.

이 목록은 2026-09-05 "필드 없음 = 조용한 기본값" 9클래스 전수 감사에서 나온 결과 그대로다
(구현담당1) — 감사에서 위험하다고 판단한 항목만 옮겼다. 새 항목을 추가하려면 같은 기준으로
판단할 것: 필드가 없을 때 그 클래스의 C# 기본값이 "안전한 무효과"가 아니라
"의도치 않게 뭔가를 허용/해제"하는가.

핵심은 "필드가 있는가"가 아니라 "없을 때 위험한 쪽으로 켜지는가"다 — 그래서 각 검사마다
왜 위험한지를 코드 옆에 바로 적어둔다.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def read(path):
    return path.read_text(encoding="utf-8")


def has_field(text, field):
    # Unity YAML로 직렬화된 MonoBehaviour/ScriptableObject 필드는
    # "  fieldName: value" 형태로 최상위 2칸 들여쓰기다.
    return re.search(rf"^  {re.escape(field)}:", text, re.MULTILINE) is not None


def glob(pattern):
    return sorted(ROOT.glob(pattern))


results = []  # (라벨, [필드...], 위험 이유, 대상 자산 수, 빠진 자산 목록)


def check(label, fields, danger, assets):
    missing = [p for p in assets if any(not has_field(read(p), f) for f in fields)]
    results.append((label, fields, danger, len(assets), missing))


# ── 1. UnitUpgradeTrackData.hasOriginalResearch — 기본값 true ───────────────
# 원작 연구소 8종(특별함·희귀함·히든·제한됨·전설적인·불멸·초월함·랜덤전용)에 대응하는
# 트랙만 true여야 하고, 대응이 없는 3종(흔함·안흔함 묶음·다른세계·영원함)은 명시적으로
# false를 박아 잠가둔다(UnitUpgradeTrackData.cs 주석). 이 필드가 빠지면 "대응 없음" 트랙도
# 조용히 true로 열려 UnitUpgradeShop의 잠금이 풀린다.
check(
    "UnitUpgradeTrackData.hasOriginalResearch",
    ["hasOriginalResearch"],
    "기본값 true — 원작 대응이 없는 트랙(흔함·안흔함·다른세계·영원함)이 이 필드를 빠뜨리면 "
    "조용히 '원작 연구 있음'으로 열려 UnitUpgradeShop 잠금이 풀린다.",
    glob("Assets/Data/UnitUpgrades/*.asset"),
)

# ── 2. CombineRecipe.requiredSaveCount — 기본값 0 ───────────────────────────
# "세이브 누적 N회" 진행 게이트. 빠지면 0회(조건 없음)로 읽혀 처음부터 조합 가능해진다.
check(
    "CombineRecipe.requiredSaveCount",
    ["requiredSaveCount"],
    "기본값 0 — 세이브 누적 게이트가 걸려 있어야 할 레시피가 이 필드를 빠뜨리면 조건 없이 "
    "(0회) 조용히 풀려 진행 게이트가 사라진다.",
    glob("Assets/Data/Recipes/*.asset"),
)

# ── 3. CombineRecipe.minRound / maxRound — 기본값 0 ─────────────────────────
# CombineRecipe.cs 주석: "0이면 제한 없음". 라운드 창(예: "41라운드 이전만")이 있어야 할
# 레시피가 필드를 빠뜨리면 두 값 다 0이 되어 "제한 없음"으로 읽힌다 — minRound 하나만
# 빠져도 하한이 사라지고, maxRound 하나만 빠져도 상한이 사라진다.
check(
    "CombineRecipe.minRound/maxRound",
    ["minRound", "maxRound"],
    "기본값 0(=제한 없음) — 라운드 창이 있어야 할 레시피가 이 필드를 빠뜨리면 조용히 "
    "'아무 라운드에서나 가능'으로 풀린다.",
    glob("Assets/Data/Recipes/*.asset"),
)

# ── 4. PirateQuestData.minRound / maxRound — 기본값 0 ───────────────────────
# 3번과 같은 이유, 같은 필드 이름, 다른 클래스(해적단 퀘스트).
check(
    "PirateQuestData.minRound/maxRound",
    ["minRound", "maxRound"],
    "기본값 0(=제한 없음) — 라운드 창이 있어야 할 퀘스트가 이 필드를 빠뜨리면 조용히 "
    "'아무 라운드에서나 등장'으로 풀린다.",
    glob("Assets/Data/PirateQuests/Quest_*.asset"),
)

# ── 5. ChatUnlockData.requiredSaveCount — 기본값 0 ──────────────────────────
# 2번과 같은 위험(세이브 누적 게이트가 0으로 풀림). 지금은 자산이 0개라 항상 통과하지만,
# 사장님이 콘텐츠를 배정해 자산이 생기는 순간부터 이 검사가 의미를 갖는다 — 미리 넣어둔다
# (MapGenerator.ChatUnlockFolder = "Assets/Data/ChatUnlocks").
check(
    "ChatUnlockData.requiredSaveCount",
    ["requiredSaveCount"],
    "기본값 0 — 세이브 누적 게이트가 걸려 있어야 할 채팅 해금이 이 필드를 빠뜨리면 조건 없이"
    " 풀린다. (현재 자산 0개 — 나중에 생길 것에 대비한 감시)",
    glob("Assets/Data/ChatUnlocks/*.asset"),
)

# ── 6. EnemyData.takesPercentDamage — 기본값 true, 조건부 검사 ──────────────
# ⚠️ 모든 EnemyData가 아니라 "보스급 카테고리"만 검사한다 — 일반 몹은 이 필드가 없어도
# 맞다(default true = %체력 비례 스킬피해를 그대로 받는다, 일반 몹의 정상 상태).
# "보스급"을 isBoss==1로만 판정하면 부족하다 — 우리 isBoss는 라운드보스 보상·도박소 해금
# 경로를 타는 스위치라 뜻이 좁고(PM 확인, 2026-09-05), 그 경로를 안 타는 퀘스트류 보스급
# (해적단 미니보스·물범류·상위 크립·거대 해왕류)은 isBoss=false인 채로 원작 upoi(포인트값)
# 기준으로만 보스다. 이 카테고리들을 구조적으로 유도할 필드가 없어서(리서치담당도 확인
# 못함) 2026-09-05 감사에서 확정된 이름 패턴을 그대로 curated list로 박는다 — 새 보스급
# 카테고리가 생기면 아래 is_boss_tier에 패턴을 추가할 것.
def is_boss_tier(path, text):
    if re.search(r"^  isBoss: 1\b", text, re.MULTILINE):
        return True
    name = path.name
    if name.startswith("Miniboss_"):
        return True
    if name == "Enemy_Seal.asset":
        return True
    if re.match(r"^Enemy_Creep[23]_", name):
        return True
    if name == "Enemy_거대해왕류.asset":
        return True
    return False


enemy_assets = glob("Assets/Data/Enemies/*.asset") + glob("Assets/Data/PirateQuests/Miniboss_*.asset")
boss_tier_assets = [p for p in enemy_assets if is_boss_tier(p, read(p))]

check(
    "EnemyData.takesPercentDamage (보스급 카테고리만)",
    ["takesPercentDamage"],
    "기본값 true — 원작 '보스는 %체력 비례 스킬피해 면역' 게이트를 옮긴 필드다. 보스급"
    "(isBoss=1, 또는 Miniboss_*·Enemy_Seal·Enemy_Creep2_*·Enemy_Creep3_*·거대해왕류)인데 "
    "이 필드가 없으면 조용히 true(면역 없음)로 켜져 상위 스킬 대미지가 원작보다 세진다.",
    boss_tier_assets,
)

# ── 7. UnitTraitData.isTransformType — 기본값 false, 조건부 검사 ────────────
# ⚠️ 폴더 전체(Assets/Data/Traits/, 239개)가 아니라 "06번③ 변신형" 원작 26분기 중 2개만
# 검사한다 — 이 목록은 이 스크립트를 작성하는 도중 실제로 부딪힌 문제라 남긴다: 폴더
# 전체를 검사하면 방금 새로 생긴 Trait_변화됨_*.asset 4개가 즉시 "누락"으로 걸리는데,
# UnitTraitData.cs의 isTransformType 주석을 확인해보면 이 필드는 "변화됨" 등급과 전혀
# 무관하다(이름이 우연히 "변신/변화"로 겹칠 뿐, 06번③ 변신형 트레잇을 가리키는 필드다) —
# 이름 패턴으로도 걸러지지 않는다. transformIntoUnit(변신 대상)으로 구분하려 해도 실제
# 변신형 두 자산 모두 대상이 아직 미배정(null)이라 값으로도 안 갈린다. 구조적으로 유도할
# 방법이 없어 이름을 직접 박는다 — 사장님이 새 변신형 대상을 배정하면 여기 추가할 것
# (Docs/reference/ORIGINAL_TRAIT_BRANCHES.md 참고).
TRANSFORM_BRANCH_TRAIT_NAMES = {
    "Trait_초월_양재모_AD.asset",
    "Trait_초월_배성령_AD.asset",
}
transform_branch_assets = [p for p in glob("Assets/Data/Traits/*.asset") if p.name in TRANSFORM_BRANCH_TRAIT_NAMES]

check(
    "UnitTraitData.isTransformType (변신형 2개만)",
    ["isTransformType"],
    "기본값 false — 변신형 트레잇인데 이 필드가 없으면 조용히 false로 켜져 GameHud가 "
    "'변신 대상 미정' 구매 차단을 안 건다. 언락은 되돌릴 수 없어 대상 미배정 상태로 포인트를"
    " 쓰면 그 포인트를 영구히 잃는다.",
    transform_branch_assets,
)

# ── 리포트 ───────────────────────────────────────────────────────────────
any_problem = False
for label, fields, danger, total, missing in results:
    field_desc = "/".join(fields)
    print(f"[{label}] 대상 {total}개, 필드({field_desc}) 누락 {len(missing)}개")
    print(f"  ⚠️  {danger}")
    for path in missing:
        print("  ❌", path.relative_to(ROOT))
        any_problem = True
    print()

sys.exit(1 if any_problem else 0)
