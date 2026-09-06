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

# ── 8. SkillData: CooldownAutoCast인데 cooldown이 0 — 값 자체가 위험한 기본값 ─
# ⚠️ 앞의 7개는 "필드가 없다"였지만 이건 "필드는 있는데 값이 0"이다 — cooldown 미기재
# 시의 C# 기본값(0f)과 값을 0으로 채운 경우가 구분이 안 돼서 같은 위험을 공유한다.
# UnitAttacker.cs의 `Mathf.Max(0.01f, level.cooldown)`이 cooldown=0을 0.01초로
# 클램프해 사실상 무제한(초당 최대 100회) 발동이 된다(2026-09-05 구현담당3 발견 —
# effects가 비어 있는 동안은 TryCastOnHitSkill의 "effects 비면 return" 가드에 막혀
# 잠들어 있다가, levels[i].effects를 채우는 순간 이 게이트가 풀린다. 실제로 이번
# 06번① 6종 배정에서 하마터면 그대로 커밋될 뻔했다). 클램프 값(0.01) 자체는 안
# 건드린다 — 의도적으로 짧게 쓰는 스킬까지 늦춰진다, 검사로 막는 게 맞다(PM 지시).
def cooldown_danger(text):
    if not re.search(r"^  triggerType: 1\b", text, re.MULTILINE):
        return False  # CooldownAutoCast가 아니면 이 클램프 경로를 안 탄다.

    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        cooldown_match = re.match(r"([\d.]+)", level_body)
        cooldown = float(cooldown_match.group(1)) if cooldown_match else 0.0
        if cooldown != 0.0:
            continue
        effects_match = re.search(r"    effects:(.*?)(?=\n  - cooldown: |\Z)", level_body, re.S)
        effects_body = effects_match.group(1) if effects_match else ""
        if re.search(r"^\s*- kind:", effects_body, re.MULTILINE):
            return True
    return False


skill_assets = glob("Assets/Data/UnitSkills/*.asset") + glob("Assets/Data/EnemySkills/*.asset")
dangerous_cooldown_assets = [p for p in skill_assets if cooldown_danger(read(p))]

results.append((
    "SkillData: CooldownAutoCast인데 cooldown=0 (초당 최대 100회)",
    ["cooldown"],
    "값 0 — CooldownAutoCast인 레벨의 cooldown이 0(필드 없을 때 기본값과 동일)이면 "
    "UnitAttacker.Mathf.Max(0.01f, cooldown) 클램프에 걸려 사실상 무제한(초당 최대 100회) "
    "발동한다. effects가 비어 있는 동안은 무해하지만 채우는 순간 위험해진다.",
    len(skill_assets),
    dangerous_cooldown_assets,
))

# ── 9. SkillData: OnHitCount인데 hitCountThreshold가 0 — 매 타 발동 ─────────
# ⚠️ 8번과 같은 종류의 함정, 다른 발동방식(OnHitCount, 2026-09-05 신설 — 원작 특성
# 24건이 "확률"이 아니라 "정확히 N타째"라 OnHitChance로 근사하지 않고 따로 뗐다).
# UnitAttacker.TryCastOnHitSkill이 카운터를 먼저 올리고(`onHitCountCounter++`) 그
# 다음 임계값과 비교한다(`< hitCountThreshold`면 return) — 임계값이 0(필드 없을 때
# 기본값과 동일)이면 카운터가 1로 오른 순간 항상 "안 작다"가 되어 "N타째마다 1회"가
# 아니라 매 타 발동한다. 8번과 마찬가지로 effects가 비어 있는 동안은 그 가드가 먼저
# 걸려 무해하다 — 채우는 순간 위험해진다.
#
# ⚠️ 2026-09-06 예외 추가(SkillLevel.hitCountFloor 신설, B045/카타쿠리) — "바닥 조건"
# 모드는 hitCountThreshold를 아예 안 쓰고(항상 0) hitCountFloor로 판정하므로, 이
# 모드에선 threshold=0이 "채우는 걸 깜빡함"이 아니라 **정상 설계**다. hitCountFloor>0인
# 레벨은 이 검사에서 뺀다.
def onhitcount_danger(text):
    if not re.search(r"^  triggerType: 3\b", text, re.MULTILINE):
        return False  # OnHitCount가 아니면 이 카운터 경로를 안 탄다.

    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        floor_match = re.search(r"\n {4}hitCountFloor: (-?\d+)", level_body)
        if floor_match and int(floor_match.group(1)) > 0:
            continue  # 바닥 조건 모드 — threshold=0이 정상이다.
        threshold_match = re.search(r"\n {4}hitCountThreshold: (-?\d+)", level_body)
        threshold = int(threshold_match.group(1)) if threshold_match else 0
        if threshold != 0:
            continue
        effects_match = re.search(r"    effects:(.*?)(?=\n  - cooldown: |\Z)", level_body, re.S)
        effects_body = effects_match.group(1) if effects_match else ""
        if re.search(r"^\s*- kind:", effects_body, re.MULTILINE):
            return True
    return False


dangerous_onhitcount_assets = [p for p in skill_assets if onhitcount_danger(read(p))]

results.append((
    "SkillData: OnHitCount인데 hitCountThreshold=0 (매 타 발동)",
    ["hitCountThreshold"],
    "값 0 — OnHitCount인 레벨의 hitCountThreshold가 0(필드 없을 때 기본값과 동일)이면 "
    "UnitAttacker의 카운터 비교(증가 후 `< threshold`)가 항상 거짓이 되어 '정확히 N타째'가 "
    "아니라 매 타 발동한다. effects가 비어 있는 동안은 무해하지만 채우는 순간 위험해진다.",
    len(skill_assets),
    dangerous_onhitcount_assets,
))

# ── 10. SkillData: OnHitCount인데 cooldown>0 — 경고(오류 아님) ─────────────
# ⚠️ 절대쿨(SkillLevel.cooldown 주석 참고, 2026-09-05 신설)은 지금 OnHitChance
# 경로에서만 읽는다 — UnitAttacker.TryCastOnHitSkill의 OnHitCount 분기는 이 값을
# 아예 안 본다. 원작에 게이지(OnHitCount)+절대쿨이 동시에 걸린 사례가 있는지
# 리서치담당이 아직 전수 확인 중이라(PM 지시), 지금 값이 있어도 조용히 무시될
# 뿐 위험하게 켜지는 게 아니다 — 그래서 #8/#9처럼 오류(❌)로 잡지 않고 경고로만
# 남긴다. 동시 사례가 확인되면 그때 실제 처리를 만들 것.
def onhitcount_with_cooldown(text):
    if not re.search(r"^  triggerType: 3\b", text, re.MULTILINE):
        return False  # OnHitCount가 아니면 해당 없음.

    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        cooldown_match = re.match(r"([\d.]+)", level_body)
        cooldown = float(cooldown_match.group(1)) if cooldown_match else 0.0
        if cooldown > 0.0:
            return True
    return False


onhitcount_cooldown_warnings = [p for p in skill_assets if onhitcount_with_cooldown(read(p))]

# ── 11. UnitData: skill과 skills가 둘 다 채워짐 — skill이 조용히 무시됨 ────
# ⚠️ UnitData.SkillCount/SkillAt(2026-09-05 다중 스킬 확장, MULTI_SKILL_IMPACT.md)이
# "skills가 하나라도 있으면 skill은 아예 안 본다"로 정했다(UnitData.cs의 SkillCount 주석
# 참고) — 하위호환을 위해 옛 skill 필드를 안 지웠을 뿐이다. 그래서 두 필드가 동시에
# 채워진 에셋은 skill 쪽 값이 통째로 죽은 배선이 된다 — "값은 있는데 아무도 안 읽는다"는
# 오늘 하루 종일 잡아온 바로 그 패턴이다. skill만 있거나 skills만 있는 건 정상이라 대상이
# 아니다(그래서 fields 목록엔 이름만 적고, 대상 자체는 "둘 다 있는 것"만 잡는다).
def has_both_skill_fields(text):
    skill_m = re.search(r"^  skill: \{fileID: (\d+)", text, re.MULTILINE)
    has_skill = skill_m is not None and skill_m.group(1) != "0"
    if not has_skill:
        return False

    # 리스트 필드 판독은 audit_data.py의 top_level_field_status와 같은 패턴을 쓴다 —
    # "skills:"가 같은 줄에 "- "로 시작하거나("skills:  - {...}"), 다음 줄이 "  - "로
    # 시작하면 항목이 있는 것이다("[]"거나 다음 줄이 다른 필드면 빈 리스트).
    skills_m = re.search(r"^  skills:(.*)$", text, re.MULTILINE)
    if skills_m is None:
        return False
    rest = skills_m.group(1).strip()
    if rest == "[]":
        return False
    if rest.startswith("- "):
        return True
    if rest == "":
        next_line_start = skills_m.end() + 1  # "\n" 다음
        return text[next_line_start:next_line_start + 4] == "  - "
    return False


roster_assets = glob("Assets/Data/Units/Roster/*.asset")
both_skill_fields = [p for p in roster_assets if has_both_skill_fields(read(p))]

results.append((
    "UnitData: skill과 skills가 둘 다 채워짐 (skill 쪽이 조용히 무시됨)",
    ["skill", "skills"],
    "skills가 하나라도 있으면 UnitData.SkillCount/SkillAt이 skill을 아예 안 본다 — 둘 다 "
    "채우면 skill 쪽 스킬이 죽은 배선이 된다. skill만 있거나 skills만 있는 건 정상이다.",
    len(roster_assets),
    both_skill_fields,
))

# ── 12. UnitData: 로스터 유닛의 damageType이 순수 AP(2) ─────────────────────
# ⚠️ "필드 없음" 계열이 아니라 "값 자체가 원작에 없는 조합"이다 — damageType=AP는
# EnemyDummy.MitigatedDamage에서 isAbilityDamage=true(스킬 효과 등)일 때 방어·마법저항을
# 통째로 무시하는 신호다. 원작 플레이어 유닛 431종 평타 공격타입 전수조사 결과 magic이
# 0건이다(UnitData.damageType 필드 주석 참고) — **원작에 평타가 UNIVERSAL(순수 AP)인 유닛이
# 없다.** 순수 AP(damageType=2)가 로스터에 나타나면 그 자체가 데이터 오류다. AD+AP 혼합
# (damageType=3, 9종)은 원작에도 있는 정상 값이라 대상이 아니다 — "화력이 스킬에서도
# 나온다"는 표시일 뿐, 평타의 순수 방어 무시로 새지 않는다(EnemyDummy가 isAbilityDamage로
# 평타 경로를 이미 막는다). 2026-09-05 PM 지적 — "지금 0종이라 안전"이 아니라 "0종이어야
# 맞다"를 검사로 고정한다.
def has_pure_ap_damage_type(text):
    return re.search(r"^  damageType: 2\b", text, re.MULTILINE) is not None


pure_ap_roster = [p for p in roster_assets if has_pure_ap_damage_type(read(p))]

results.append((
    "UnitData: 로스터 damageType이 순수 AP(2)",
    ["damageType"],
    "원작 플레이어 유닛 평타에 마법(UNIVERSAL)이 0건이다 — 순수 AP는 그 자체가 데이터 오류. "
    "이 값이면 평타 경로는 isAbilityDamage=false라 안 새지만(EnemyDummy), 애초에 원작에 없는 "
    "조합이라 조합표 반영 과정에서 실수로 들어간 값일 가능성이 높다. AD+AP 혼합(3)은 정상.",
    len(roster_assets),
    pure_ap_roster,
))

# ── 13. SkillEffect: basis=Flat인데 bonus≠0 — 상수항이 조용히 버려짐 ────────
# ⚠️ 2026-09-05 버그 발견(구현담당1, PM 확인): UnitAttacker.ResolveSkillEffectValue의
# `Flat` 케이스는 `effect.multiplier`만 돌려주고 `effect.bonus`는 안 본다 — SkillData.cs의
# 필드 주석("비례식의 +상수항")과 다른 basis(CasterAttackPower 등)의 관례와 안 맞는다.
# 코드를 안 고친 이유(PM 지시): "고정값 그 자체"인 Flat에 bonus를 더하면 basis 의미가
# 흐려진다 — 대신 **basis=Flat인데 bonus가 채워진 데이터 자체를 오류로 잡는다.** 지금
# 자산 전부 Flat+bonus=0이라 안전하지만, 누가 실수로 Flat 효과에 bonus를 채우면 그 값이
# 조용히 사라진다(피해가 안 나가는 게 아니라 "일부만" 나가서 더 늦게 발견된다).
def flat_with_bonus_danger(text):
    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        effects_match = re.search(r"    effects:(.*?)(?=\n  - cooldown: |\Z)", level_body, re.S)
        effects_body = effects_match.group(1) if effects_match else ""

        for effect_body in re.split(r"\n    - kind: ", effects_body)[1:]:
            basis_match = re.search(r"\n {6}basis: (\d+)", effect_body)
            basis = basis_match.group(1) if basis_match else "0"  # 기본값 Flat(0)
            if basis != "0":
                continue
            bonus_match = re.search(r"\n {6}bonus: (-?[\d.]+)", effect_body)
            bonus = float(bonus_match.group(1)) if bonus_match else 0.0
            if bonus != 0.0:
                return True
    return False


flat_with_bonus_assets = [p for p in skill_assets if flat_with_bonus_danger(read(p))]

results.append((
    "SkillEffect: basis=Flat인데 bonus≠0 (상수항이 조용히 버려짐)",
    ["basis", "bonus"],
    "UnitAttacker.ResolveSkillEffectValue의 Flat 케이스는 multiplier만 돌려주고 bonus는 "
    "안 본다(의도적 설계, PM 지시) — Flat 효과에 bonus를 채우면 그 값이 조용히 사라진다.",
    len(skill_assets),
    flat_with_bonus_assets,
))

# ── 14. SkillEffect: target이 Enemies/Allies인데 range<=0 — 무제한 범위 사고 ─
# ⚠️ range는 SkillLevel(스킬 레벨) 필드고 target은 그 안 SkillEffect(효과) 필드라 계층이
# 다르다 — 레벨 하나의 range<=0인데 그 레벨의 어느 effect라도 target이 Enemies(2)/Allies(1)면
# 위험하다. UnitAttacker.ApplySkillEffect가 Enemies 분기에서 `range > 0f`일 때만 거리를
# 재고, range<=0이면 그 조건 자체가 거짓이 되어 **거리 검사를 건너뛰고 EnemyDummy.Active
# 전체를 때린다**(맵 전체 범위) — Allies도 UnitIdentity.AlliesOf(identity, range)로 같은
# 함정을 공유한다. 구현담당1이 핸콕에서 실제로 760을 0으로 비워둔 채 커밋할 뻔했다
# (2026-09-05, PM). SingleTarget은 range<=0이면 FindClosestEnemyWithin이 그냥 "못 찾음"으로
# 안전하게 실패하고(무제한이 아니라 무효), Self는 range를 아예 안 본다 — 그래서 이 둘은
# 대상이 아니다(PM 지시).
#
# ⚠️ target 필드가 아예 없는 effect는 SkillEffect.target의 C# 기본값(Enemies)으로 읽힌다 —
# 지금 실제 자산 164개 전부 target을 명시하지만(2026-09-05 확인), 이 검사는 미래의 누락도
# 같은 위험(기본값이 Enemies)으로 잡는다.
def unbounded_range_danger(text):
    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        range_match = re.search(r"\n {4}range: (-?[\d.]+)", level_body)
        level_range = float(range_match.group(1)) if range_match else 0.0
        if level_range > 0.0:
            continue

        effects_match = re.search(r"    effects:(.*?)(?=\n  - cooldown: |\Z)", level_body, re.S)
        effects_body = effects_match.group(1) if effects_match else ""

        for effect_body in re.split(r"\n    - kind: ", effects_body)[1:]:
            target_match = re.search(r"\n {6}target: (\d+)", effect_body)
            target = target_match.group(1) if target_match else "2"  # 없으면 기본값 Enemies(2)
            if target in ("1", "2"):  # Allies, Enemies
                return True
    return False


dangerous_range_assets = [p for p in skill_assets if unbounded_range_danger(read(p))]

results.append((
    "SkillEffect: target=Enemies/Allies인데 range<=0 (맵 전체 무제한 범위)",
    ["range", "target"],
    "값 0(또는 미기재) — UnitAttacker.ApplySkillEffect가 Enemies/Allies 분기에서 range<=0이면 "
    "거리 검사 자체를 건너뛰어 EnemyDummy.Active/AlliesOf 전체를 때린다. SingleTarget·Self는 "
    "range<=0이어도 안전하게 실패할 뿐이라 대상이 아니다.",
    len(skill_assets),
    dangerous_range_assets,
))

# ── 15. SkillData: OnHitCount인데 gaugeKind가 파일에 없음 — 경고(오류 아님) ─
# ⚠️ SkillGaugeKind 기본값은 Mana(0)다(SkillData.cs) — 지금까지 있는 OnHitCount 자산
# 전부 원래 마나 게이지라 이 기본값이 우연히 맞았다(2026-09-05 밤 발견). 그런데
# **어느 자산도 gaugeKind를 명시적으로 직렬화한 적이 없다** — "체력(Life) 게이지를 쓰는
# 스킬이라 gaugeKind=1을 채웠다"와 "그냥 기본값을 안 건드렸다"가 파일만 봐서는 구분이
# 안 된다. 구현담당1이 지금 유닛당 2~6개 스킬을 배정하면서 체력 게이지 자산이 곧
# 생기는데, 실수로 그 필드를 안 채우면 조용히 Mana로 읽혀 원작과 다른 게이지를
# 공유하게 된다(예: 원래 체력 게이지 스킬이 마나 게이지 스킬과 카운터를 섞어 씀).
# → 값을 강제할 방법이 없어(0이 "명시적 Mana"인지 "미기재"인지 텍스트로는 구분되는데,
# "미기재가 항상 틀렸다"고 단정할 근거는 없다 — 실제로 마나 게이지인 스킬은 안 채워도
# 맞다) **오류(❌)가 아니라 경고**로만 남긴다(#10과 같은 급).
def onhitcount_missing_gaugekind(text):
    if not re.search(r"^  triggerType: 3\b", text, re.MULTILINE):
        return False  # OnHitCount가 아니면 해당 없음.

    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        effects_match = re.search(r"    effects:(.*?)(?=\n  - cooldown: |\Z)", level_body, re.S)
        effects_body = effects_match.group(1) if effects_match else ""
        if not re.search(r"^\s*- kind:", effects_body, re.MULTILINE):
            continue  # 효과가 비어 있으면 아직 안 도는 스킬이라 무해하다(#8/#9와 같은 게이트).

        if not re.search(r"\n {4}gaugeKind: \d+", level_body):
            return True
    return False


onhitcount_missing_gaugekind_warnings = [p for p in skill_assets if onhitcount_missing_gaugekind(read(p))]

# ── 16. SkillEffect: randMin > randMax — 난수 범위가 뒤집힘 ─────────────────
# ⚠️ 2026-09-06 신설(원작 RRD 난수 배율 연결, PM 지시) — 원작 RRD(c, min, max, …)의
# "실제 피해 = c × GetRandomReal(min, max)"에서 min/max를 그대로 옮기다 순서가 뒤집히면
# UnitAttacker.RandomDamageMultiplier의 Random.Range(min, max)가 이상한 범위를 굴린다.
# 필드 자체가 없으면 C# 기본값 1f/1f라(SkillData.cs) 이 검사에 안 걸린다 — 회귀 없음.
def rand_min_gt_max(text):
    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        effects_match = re.search(r"    effects:(.*?)(?=\n  - cooldown: |\Z)", level_body, re.S)
        effects_body = effects_match.group(1) if effects_match else ""

        for effect_body in re.split(r"\n    - kind: ", effects_body)[1:]:
            min_m = re.search(r"\n {6}randMin: (-?[\d.]+)", effect_body)
            max_m = re.search(r"\n {6}randMax: (-?[\d.]+)", effect_body)
            if min_m is None or max_m is None:
                continue  # 둘 다(혹은 한쪽) 없으면 기본값 1이라 뒤집힐 수가 없다.
            if float(min_m.group(1)) > float(max_m.group(1)):
                return True
    return False


rand_min_gt_max_assets = [p for p in skill_assets if rand_min_gt_max(read(p))]

results.append((
    "SkillEffect: randMin > randMax (난수 범위가 뒤집힘)",
    ["randMin", "randMax"],
    "UnitAttacker.RandomDamageMultiplier가 Random.Range(randMin, randMax)를 그대로 돌린다 "
    "— min>max로 뒤집히면 원작 RRD의 min/max 순서를 잘못 옮긴 것이다.",
    len(skill_assets),
    rand_min_gt_max_assets,
))

# ── 17. SkillEffect: randMin < 0 — 음수 난수 배율 하한 ──────────────────────
# ⚠️ 리서치담당 RRD 649건 전수에서 음수 배율은 0건이었다 — 있으면 데이터 오타일 가능성이
# 높다(원작 그대로 옮겼다면 절대 안 나올 값).
def rand_min_negative(text):
    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        effects_match = re.search(r"    effects:(.*?)(?=\n  - cooldown: |\Z)", level_body, re.S)
        effects_body = effects_match.group(1) if effects_match else ""

        for effect_body in re.split(r"\n    - kind: ", effects_body)[1:]:
            min_m = re.search(r"\n {6}randMin: (-?[\d.]+)", effect_body)
            if min_m is not None and float(min_m.group(1)) < 0:
                return True
    return False


rand_min_negative_assets = [p for p in skill_assets if rand_min_negative(read(p))]

results.append((
    "SkillEffect: randMin < 0 (음수 난수 배율)",
    ["randMin"],
    "난수 배율 하한이 음수면 피해 부호가 뒤집힐 수 있다 — 리서치담당 RRD 649건 전수에 "
    "음수 사례가 0건이라, 있으면 원작을 잘못 옮긴 데이터일 가능성이 높다.",
    len(skill_assets),
    rand_min_negative_assets,
))

# ── 18. SkillEffect: triggerChance=0인데 게이트 미확인 꼬리표가 없음 ────────
# PM 지시(2026-09-06, DUMMY_CHANNEL_MISSING.csv 79행 이식) — 게이트를 못 찾은 새 더미
# 채널 스킬은 실수로 매 타 발동(145배 과다 사고 재현)하지 않도록 triggerChance를 0으로
# 잠근다(UnitAttacker.cs:734, Random.value >= 0은 항상 참이라 확실히 안 쏜다). 그런데
# 0은 "게이트가 극도로 빡빡하다"와 "피해 자체가 0이다"를 코드만 봐서는 구분 못 한다
# (뿌리 ⑯, 오늘 세 번째 재발). description에 "[게이트 미확인"이 없는 채로 triggerChance:
# 0.0이 있으면 잠금인지 진짜 무효과인지 알 길이 없어진다 — 반드시 꼬리표와 짝이어야 한다.
def zero_trigger_without_unconfirmed_tag(text):
    tag_present = "[게이트 미확인" in text
    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        tc_m = re.search(r"^    triggerChance: ([-+0-9.eE]+)", "  - cooldown: " + level_body, re.M)
        if tc_m and float(tc_m.group(1)) == 0.0 and not tag_present:
            return True
    return False


zero_trigger_assets = [p for p in skill_assets if zero_trigger_without_unconfirmed_tag(read(p))]

results.append((
    "SkillEffect: triggerChance=0인데 [게이트 미확인] 꼬리표 없음",
    ["triggerChance"],
    "0은 '게이트를 몰라서 잠갔다'와 '원작 자체가 무효과다'를 코드만 보고 구분 못 한다 — "
    "description에 [게이트 미확인] 표시가 없는 0은 다음 사람이 '피해 없음'으로 잘못 읽는다"
    "(뿌리 ⑯).",
    len(skill_assets),
    zero_trigger_assets,
))

# ── 19. SkillEffect: buffHitCharges>0인데 kind가 ApplyBuff가 아님 ───────────
# ⚠️ 2026-09-06 신설(제트 B00M 타수형 만료, PM 지시) — buffHitCharges는 "이 버프가
# 평타 N번 뒤에 만료된다"는 뜻이라 ApplyBuff(kind=6) 효과에만 의미가 있다(SkillData.cs
# buffHitCharges 주석 참고). UnitAttacker.ApplyToAlly/ApplyToEnemy가 ApplyBuff가 아닌
# kind는 buffId·buffHitCharges 필드 자체를 안 읽으므로 값을 채워도 조용히 무시된다 —
# "채웠는데 아무 일도 안 일어난다"는 오늘 하루 종일 잡아온 바로 그 패턴(값은 있는데
# 아무도 안 읽는다)이다.
def buff_hit_charges_without_apply_buff(text):
    for level_body in re.split(r"\n  - cooldown: ", text)[1:]:
        effects_match = re.search(r"    effects:(.*?)(?=\n  - cooldown: |\Z)", level_body, re.S)
        effects_body = effects_match.group(1) if effects_match else ""

        for effect_body in re.split(r"\n    - kind: ", effects_body)[1:]:
            kind_match = re.match(r"(\d+)", effect_body)
            kind = kind_match.group(1) if kind_match else "0"
            charges_match = re.search(r"\n {6}buffHitCharges: (-?\d+)", effect_body)
            charges = int(charges_match.group(1)) if charges_match else 0
            if charges > 0 and kind != "6":  # 6 = SkillEffectKind.ApplyBuff
                return True
    return False


buff_hit_charges_mismatch_assets = [p for p in skill_assets if buff_hit_charges_without_apply_buff(read(p))]

results.append((
    "SkillEffect: buffHitCharges>0인데 kind가 ApplyBuff가 아님",
    ["kind", "buffHitCharges"],
    "buffHitCharges는 ApplyBuff(kind=6) 효과 전용이다 — 다른 kind에 채우면 UnitAttacker가 "
    "그 필드를 아예 안 읽어 조용히 무시된다(값은 있는데 아무 효과가 없다).",
    len(skill_assets),
    buff_hit_charges_mismatch_assets,
))

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

# 경고는 exit code에 안 반영한다 — 지금은 무해하다고 확정됐기 때문이다(위 주석 참고).
if onhitcount_cooldown_warnings:
    print(f"[경고] SkillData: OnHitCount인데 cooldown>0인 레벨이 있음 (오류 아님, "
          f"{len(onhitcount_cooldown_warnings)}개)")
    print("  ⚠️  절대쿨은 지금 OnHitChance 경로에서만 읽는다 — OnHitCount는 이 값을 무시한다.")
    for path in onhitcount_cooldown_warnings:
        print("  ⚠️ ", path.relative_to(ROOT))
    print()

if onhitcount_missing_gaugekind_warnings:
    print(f"[경고] SkillData: OnHitCount인데 gaugeKind가 파일에 없는 레벨이 있음 (오류 아님, "
          f"{len(onhitcount_missing_gaugekind_warnings)}개)")
    print("  ⚠️  기본값 Mana(0)로 읽힌다 — 마나 게이지 스킬이면 무해하지만, 체력(Life) 게이지 "
          "스킬인데 gaugeKind=1을 안 채웠다면 다른 게이지 스킬과 카운터가 섞인다. 배정할 때 확인할 것.")
    for path in onhitcount_missing_gaugekind_warnings:
        print("  ⚠️ ", path.relative_to(ROOT))
    print()

sys.exit(1 if any_problem else 0)
