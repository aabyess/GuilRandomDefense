#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""로스터 ↔ 스킬 에셋 배정 불변식 검사. 읽기 전용 — 아무것도 고치지 않는다.

배경(TEAM_RULES.md 뿌리 ⑮, 커밋 69536ec): 오늘 밤 두 생성기가 "매칭 단위를 원작 유닛
하나로 안 잡아서" 각각 다른 얼굴로 같은 실수를 냈다.
  - generate_unit_skills_by_gate.py: 등급 안에서 넘치는 원작 유닛을 경고 없이 버렸다
    (13유닛 19행 — 6505647로 회수됨).
  - generate_unit_skill_damage_effects.py: CSV를 행 단위로 순위매칭해서, 한 원작 유닛의
    계산식이 여러 줄이면 그 줄 수만큼 서로 다른 로스터 유닛에 쪼개졌다(32유닛 → 77슬롯,
    재생성 예정).
구현담당1이 데이터를 고치는 동안, 다음에 또 나지 않게 이 불변식들을 검사로 고정한다.

⚠️ 데이터는 안 고친다 — 검사만이다.

스코프: Assets/Data/Units/Roster/*.asset(로스터) + Assets/Data/UnitSkills/*.asset(스킬) —
보스 스킬(EnemySkills)은 로스터 배정과 무관해 대상이 아니다.

"원작 유닛" 키 뽑는 법: 스킬 에셋 description은 생성기 셋(게이트·절대쿨·스킬피해)이 형식이
다 다르다("원작 {이름}({ID})" / "원작 {ID} {이름}({트리거})" / "원작 {등급} {이름}({출처})")
— 그런데 셋 다 "원작 ... 이름(...)" 자리에 원작 유닛 이름은 공통으로 넣는다. 그래서 그
패턴에서 등급명·ID코드(H로 시작하는 4자)를 벗겨내고 남는 이름을 매칭 키로 쓴다. description
이 이 패턴을 안 따르면(수치 미상 자리 등) 매칭 키를 못 뽑는다 — 그런 에셋은 ①·⑤ 판정에서
빠지고 목록으로 따로 보고한다(조용히 무시하지 않는다).

check_required_fields.py #11(로스터 skill·skills 동시 채움)과 겹치는 항목은 여기서 뺐다
— PM 지시대로 그쪽에 맡긴다.

exit code: ①·②·③(실제 배선 사고)만 실패로 잡는다. ⑤는 정보다(원작이 로스터 등급 정원보다
많아 일부를 못 담는 건 정상일 수 있다) — 실패에 안 넣는다. ⑦도 실패로 잡는다(죽은 게이트는
실제 배선 사고다).

⚠️ ⑩(문서만, PM 지시 2026-09-06) — `forbiddenBuffId`/`forbiddenTargetBuffId`는 런타임과
시뮬이 서로 다른 파일에서 각자 판정한다(오늘 한 번 방향이 뒤집혔던 자리라 자동 검사가
아니라 위치만 못박아둔다 — 코드 로직 자체를 정적으로 비교할 방법이 없다):
  런타임: Assets/Scripts/Units/UnitAttacker.cs의 PassesBuffGate — forbidden(BuffId만 없어도
    통과) 있으면(HasBuff) 막는다(required와 정확히 반대 방향, 둘 다 "위반이면 return false").
  시뮬:   Tools/simulate_balance.py의 skill_dps_for_unit — required는 rate=0(보수적으로
    없다고 가정), forbidden은 아예 안 본다(그 게이트를 무시 = "대부분 없어서 통과"의 근사).
두 방향이 다시 어긋나면(예: 시뮬이 forbidden도 0으로 잠그게 "고치면") §22-6/22-7
(BALANCE_SIMULATION_2026-09-05.md)과 이 주석부터 다시 맞출 것.
"""
import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def read(p):
    return Path(p).read_text(encoding="utf-8")


def glob(pattern):
    return sorted(ROOT.glob(pattern))


# ── 로스터 skill/skills 판독 — check_required_fields.py·simulate_multiskill_gates.py와
# 같은 패턴(UnitData.SkillCount 규칙: skills가 하나라도 있으면 skill은 안 본다) ──
def resolve_skill_guids(roster_text):
    skill_m = re.search(r"^  skill: \{fileID: (\d+)(?:, guid: ([0-9a-f]+))?", roster_text, re.MULTILINE)
    has_skill = skill_m is not None and skill_m.group(1) != "0"
    skill_guid = skill_m.group(2) if has_skill else None

    skills_m = re.search(r"^  skills:(.*)$", roster_text, re.MULTILINE)
    guids = []
    if skills_m is not None:
        rest = skills_m.group(1).strip()
        tail = roster_text[skills_m.end():]
        block_lines = []
        if rest.startswith("- "):
            block_lines.append(rest)
        for line in tail.split("\n"):
            if line.startswith("  - "):
                block_lines.append(line[4:])
            elif line.startswith("  ") and not line.startswith("    ") and line.strip() != "":
                break
        for line in block_lines:
            g = re.search(r"guid: ([0-9a-f]+)", line)
            if g:
                guids.append(g.group(1))

    if guids:
        return guids  # skills가 하나라도 있으면 skill은 죽은 필드(UnitData.SkillCount 규칙)
    return [skill_guid] if skill_guid else []


def build_guid_index():
    index = {}
    for meta_path in glob("Assets/Data/UnitSkills/*.asset.meta"):
        guid_m = re.search(r"guid: ([0-9a-f]+)", read(meta_path))
        if guid_m:
            index[guid_m.group(1)] = str(meta_path)[:-len(".meta")]
    return index


GRADES = ["다른세계", "초월함", "초월", "불멸의", "불멸", "영원한", "영원", "전설적인", "전설",
          "제한됨", "제한", "히든", "랜덤전용", "랜덤", "변화됨", "희귀함", "흔함", "안흔함"]
_ID_CODE_RE = re.compile(r"^[Hh][0-9A-Za-z]{3}$")


def extract_original_key(description):
    # ⚠️ description이 종종 "원작 {source}.csv 인용"을 먼저 적고("원작 능력표
    # (ORIGINAL_UNIT_ABILITIES.csv) DPS 순위 배정 — 원작 {등급} {이름}({트리거})의…")
    # 실제 유닛 이름은 그 뒤에 나온다 — 첫 매치만 보면 CSV 파일명을 유닛 키로 잘못 뽑는다
    # (실전 발견: "트리거·더미 피해표"·"능력표"가 섞여 있었다). 게다가 "더미" 경로의 괄호
    # 안에 괄호가 중첩되기도 한다("(더미:Trig_X → e026(#설명) → A08O)") — 단순 정규식으론
    # 짝이 안 맞아 그 자리를 건너뛰고 그 다음 아무 문장("원작 유닛 하나(행 2개)를…")을
    # 잘못 집었다(실전 발견). 그래서 괄호는 깊이를 직접 세어 짝을 맞추고, 닫는 괄호 뒤
    # 문맥이 우리가 아는 마커("의 …" 또는 ", 버프…")로 시작할 때만 실제 유닛 자리로
    # 인정한다.
    for om in re.finditer(r"원작\s+", description):
        start = om.end()
        paren_open = description.find("(", start)
        if paren_open == -1 or paren_open - start > 120:
            continue
        name_part = description[start:paren_open].strip()
        if len(name_part) < 2:
            continue

        depth = 0
        close = -1
        i = paren_open
        while i < len(description):
            if description[i] == "(":
                depth += 1
            elif description[i] == ")":
                depth -= 1
                if depth == 0:
                    close = i
                    break
            i += 1
        if close == -1:
            continue

        paren_content = description[paren_open + 1:close]
        if ".csv" in paren_content.lower():
            continue

        after = description[close + 1:close + 3]
        if not (after.startswith("의 ") or after.startswith(",")):
            continue  # 알려진 마커가 아니면 실제 유닛 이름 자리가 아닐 수 있다 — 건너뛴다.

        tokens = name_part.split()
        while tokens and (tokens[0] in GRADES or _ID_CODE_RE.match(tokens[0])):
            tokens.pop(0)
        name = " ".join(tokens).strip()
        if name:
            return name
    return None


# ── 불변식 ⑦ — 버프 게이트 방향 불일치(PM 지시, 2026-09-06 06번 사후 + 2026-09-07 양방향
# 확장) ── 두 방향을 다 본다:
#   (a) 요구하는데 아무도 안 거는 버프 — requiredBuffId/requiredTargetBuffId가 채워진
#       게이트인데 그 버프를 실제로 거는 자산·코드가 하나도 없으면 그 효과는 영영 안
#       나간다. 이게 06번의 원래 문제였다(902dbeb, 2f36e6b로 처음 고침).
#   (b) 거는데 아무도 안 요구하는 버프 — opener가 ApplyBuff/AttackPowerBuffFlat으로
#       버프를 걸어두고 정작 그 버프를 게이트로 쓰는 소비 쪽이 없으면, 그 opener는
#       "걸어둬도 무해"가 아니라 **조용한 과다발동**이다(소비 스킬이 버프 유무와 무관하게
#       항상 통과한다는 뜻 — requiredBuffId 필드 자체가 비어있으니 (a) 방향으로는 절대
#       안 잡힌다). 2026-09-07 실제로 이 방향에서 5건(B05N·B03M·B00D·B045·B03Z)이
#       조용히 통과 중이었던 걸 발견하고서야 이 방향을 추가했다 — "별건"이라고 미루면
#       똑같은 사고가 다음에 또 난다(PM 지시, 이번 발견의 진짜 값어치).
#
# 캐스터(UnitAttacker.activeBuffs)와 대상(EnemyDummy.activeBuffs)은 서로 다른 레지스트리라
# (PassesBuffGate 참고) "누가 거는지"도 그 방향에 맞는 쪽만 인정해야 한다:
#   캐스터 버프 소스 = SkillLevel.selfBuffId(비어있지 않음, OnHitChance 절대쿨이 자기잠금을
#     등록) ∪ SkillEffect.buffId(kind∈{ApplyBuff,AttackPowerBuffFlat}, target∈{Self,Allies})
#     ∪ UnitAttacker.cs 하드코딩 캐스터 버프(AttackSpeedBuffId/AttackPowerBuffId — 소스에서
#     상수값을 직접 읽는다, SupportShop 버프 — 이 둘은 (b) 방향 검사에서 제외한다, 아래 참고)
#   대상 버프 소스 = SkillEffect.buffId(kind∈{ApplyBuff,AttackPowerBuffFlat}, target=Enemies)
#     ∪ C# 소스에서 `EnemyDummy` 인스턴스에 직접 문자열 리터럴로 AddBuff("...")를 부르는
#     자리(정규식 스캔 — 지금은 SideBossEncounter의 B06B 하나, 2f36e6b)
#
# ⚠️ (b) 방향에서 AttackSpeedBuffId/AttackPowerBuffId(하드코딩 상수)는 검사 대상에서
# 뺀다 — 이 둘은 이름으로 조회되는 게 아니라 attackSpeedBuffs/attackPowerBuffs 리스트의
# 존재 자체(배율곱)로만 쓰이는 구조적 버프라, "아무도 requiredBuffId로 안 부른다"가 항상
# 참이고 그게 정상이다(설계상 게이트 대상이 아님) — 여기까지 잡으면 매번 오탐 2건이 뜬다.
#
# ⚠️ 코드 쪽 리터럴 스캔은 휴리스틱이다 — 변수명에 "target"/"mob"이 있으면 대상 쪽,
# 그 외(bare AddBuff(...) 또는 "ally"가 들어간 변수)는 캐스터 쪽으로 가른다. 새 호출부가
# 다른 이름 관례를 쓰면 놓칠 수 있다 — 이 스크립트가 조용히 통과시키면 안 되니, 분류
# 못한 리터럴은 "미분류"로 따로 보고한다(있으면 사람이 봐야 한다).
_ADD_BUFF_KINDS = {6, 11, 12}  # ApplyBuff, AttackPowerBuffFlat, AttackSpeedBuffPercent
_TARGET_SELF, _TARGET_ALLIES, _TARGET_ENEMIES = "0", "1", "2"


def iter_effect_blocks(text):
    """레벨 경계를 안 가리고 파일 전체에서 효과 블록(각 "- kind:"부터 다음 "- kind:" 또는
    파일 끝까지)을 순서대로 낸다 — ⑦은 "이 파일 안 어딘가에 이런 효과가 있는가"만 보면
    되므로 check_required_fields.py처럼 레벨별로 안 갈라도 된다."""
    starts = [m.start() for m in re.finditer(r"\n {4}- kind: \d+", text)]
    for i, start in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else len(text)
        yield text[start:end]


def field_value(block, field):
    # 블록의 첫 필드(kind)는 리스트 항목 표시("- ")가 같은 줄에 붙어 있다("    - kind: 6")
    # — 나머지 필드는 순수 공백 들여쓰기뿐이다. 둘 다 받아야 한다.
    m = re.search(rf"\n\s*(?:- )?{re.escape(field)}: (\S+)", block)
    return m.group(1) if m else None


def find_caster_buff_hardcoded_ids():
    text = read(ROOT / "Assets/Scripts/Units/UnitAttacker.cs")
    ids = set()
    for m in re.finditer(r'static readonly string \w+Id = "([^"]+)";', text):
        ids.add(m.group(1))
    return ids


def find_code_granted_target_buff_ids():
    """Assets/Scripts/**/*.cs에서 EnemyDummy(대상) 쪽에 직접 문자열 리터럴로 거는
    AddBuff("...") 호출을 찾는다. 휴리스틱: 리시버 변수명에 target/mob/enemy가 있으면
    대상 쪽으로, 그 외는 캐스터 쪽(별도 집계, 미분류에는 안 넣는다 — 이미 selfBuffId/
    ApplyBuff로 캐스터 쪽은 자산에서 찾으므로 여기 캐스터 쪽 리터럴은 그냥 버린다)."""
    target_ids, caster_ids, unclassified = set(), set(), []
    for cs_path in sorted((ROOT / "Assets/Scripts").rglob("*.cs")):
        text = read(cs_path)
        for m in re.finditer(r'(\w*)\.AddBuff\(\s*"([^"]+)"', text):
            receiver, buff_id = m.group(1), m.group(2)
            lower = receiver.lower()
            if "target" in lower or "mob" in lower or "enemy" in lower:
                target_ids.add(buff_id)
            elif "ally" in lower or receiver == "":
                caster_ids.add(buff_id)
            else:
                unclassified.append((cs_path, receiver, buff_id))
    return target_ids, caster_ids, unclassified


def find_orphaned_buff_gates(skill_assets):
    hardcoded_ids = find_caster_buff_hardcoded_ids()
    caster_granted = set(hardcoded_ids)
    target_granted = set()
    # 자산이 준 버프의 출처(에셋 경로) — (b) 방향 보고용. 하드코딩·코드 리터럴 출처는
    # 문자열로 태그한다(자산이 아니라 파일 경로 형식이 다르므로 print 쪽에서 구분).
    caster_grant_sources = {}   # buff_id -> [str 또는 Path]
    target_grant_sources = {}

    code_target_ids, code_caster_ids, unclassified_calls = find_code_granted_target_buff_ids()
    for bid in code_target_ids:
        target_granted.add(bid)
        target_grant_sources.setdefault(bid, []).append("(C# 코드)")
    for bid in code_caster_ids:
        caster_granted.add(bid)
        caster_grant_sources.setdefault(bid, []).append("(C# 코드)")

    required_caster = {}   # buff_id -> [asset paths that require it]
    required_target = {}   # buff_id -> [asset paths that require it]
    # ⚠️ 2026-09-07 추가(PM 지시) — (b) 방향은 "거는데 아무도 안 요구하면 전부 의심"이
    # 원칙이지만, 원작에 정말로 게이트가 없는 순수 버프도 있다(예: B035 — 네이티브 스탯
    # 버프라 JASS 어디서도 조회 안 함, war3map_new.j 전수 검색으로 확인). 그런 경우
    # description에 `[버프게이트예외:B035]`처럼 원문 근거와 함께 명시적으로 표시하면
    # 이 검사기가 예외로 뺀다 — 예외를 남발하면 검사기가 무의미해지니 마커만 보고 믿지
    # 않는다, 이 마커를 붙인 자산의 description에 반드시 원문 확인 근거(예: JASS 전수
    # 검색 결과, 원본 필드 조사)가 같이 있어야 한다(사람이 리뷰로 강제).
    exempted_ids = set()

    for sp in skill_assets:
        text = read(sp)
        for m in re.finditer(r"\[버프게이트예외:([^\]]+)\]", text):
            exempted_ids.add(m.group(1))
        for m in re.finditer(r"\n {4}selfBuffId: (\S+)", text):
            caster_granted.add(m.group(1))
            caster_grant_sources.setdefault(m.group(1), []).append(sp)
        for m in re.finditer(r"\n {4}requiredBuffId: (\S+)", text):
            required_caster.setdefault(m.group(1), []).append(sp)

        for block in iter_effect_blocks(text):
            kind = field_value(block, "kind")
            target = field_value(block, "target")
            buff_id = field_value(block, "buffId")
            req_target_buff = field_value(block, "requiredTargetBuffId")

            if kind is not None and int(kind) in _ADD_BUFF_KINDS and buff_id:
                if target == _TARGET_ENEMIES:
                    target_granted.add(buff_id)
                    target_grant_sources.setdefault(buff_id, []).append(sp)
                elif target in (_TARGET_SELF, _TARGET_ALLIES):
                    caster_granted.add(buff_id)
                    caster_grant_sources.setdefault(buff_id, []).append(sp)
            if req_target_buff:
                required_target.setdefault(req_target_buff, []).append(sp)

    orphaned_caster = {b: ps for b, ps in required_caster.items() if b not in caster_granted}
    orphaned_target = {b: ps for b, ps in required_target.items() if b not in target_granted}

    # (b) 거는데 아무도 안 요구하는 버프 — 하드코딩 시스템 버프(AttackSpeedBuffId/
    # AttackPowerBuffId)는 이름으로 조회되는 게 아니라서 제외하고, `[버프게이트예외:ID]`
    # 마커로 원문 근거와 함께 명시적으로 예외 처리된 것도 뺀다(위 주석 참고).
    dead_grant_caster = {
        b: srcs for b, srcs in caster_grant_sources.items()
        if b not in hardcoded_ids and b not in required_caster and b not in exempted_ids
    }
    dead_grant_target = {
        b: srcs for b, srcs in target_grant_sources.items()
        if b not in required_target and b not in exempted_ids
    }

    return orphaned_caster, orphaned_target, unclassified_calls, dead_grant_caster, dead_grant_target


def main():
    roster_assets = glob("Assets/Data/Units/Roster/*.asset")
    skill_assets = glob("Assets/Data/UnitSkills/*.asset")
    guid_to_path = build_guid_index()
    path_to_guid = {v: k for k, v in guid_to_path.items()}

    roster_to_guids = {}
    guid_to_rosters = {}
    dup_within_roster = []  # (roster_path, guid) — 불변식 ③

    for rp in roster_assets:
        text = read(rp)
        guids = resolve_skill_guids(text)
        roster_to_guids[rp] = guids
        seen = set()
        for g in guids:
            if g in seen:
                dup_within_roster.append((rp, g))
            seen.add(g)
            guid_to_rosters.setdefault(g, set()).add(rp)

    # ── 불변식 ② — 같은 SkillData를 두 로스터 유닛이 참조 ──────────────────
    cross_roster_dup = {g: rs for g, rs in guid_to_rosters.items() if len(rs) > 1}

    # ── 불변식 ④ — 로스터 skill/skills에도, 트레잇의 replacementSkill에도 안 걸린
    # 스킬 에셋(PM 지시, 2026-09-06 — H0BL 사고: 만들어졌는데 아무도 안 쓰는 자산이
    # "데이터는 있는데 아무 일도 안 일어나는" 자리다). 트레잇(06번③ 능력교체형)은
    # 로스터가 아니라 트레잇 에셋을 통해 배선되므로 별도로 스캔해야 한다.
    trait_guids = set()
    for tp in glob("Assets/Data/Traits/*.asset"):
        m = re.search(r"^  replacementSkill: \{fileID: \d+(?:, guid: ([0-9a-f]+))?", read(tp), re.MULTILINE)
        if m and m.group(1):
            trait_guids.add(m.group(1))

    orphaned_skills = []
    for sp in skill_assets:
        guid = path_to_guid.get(str(sp))
        if guid is None:
            continue
        if guid in guid_to_rosters or guid in trait_guids:
            continue
        orphaned_skills.append(sp)

    # ── 불변식 ⑥ — ④의 거울: 로스터 skill/skills·트레잇 replacementSkill이 가리키는
    # guid인데 실제 SkillData 에셋이 없는 것(PM 지시, 2026-09-06 — 랜덤_미도리야_이즈쿠·
    # 손오공 사고: bd1b267이 만든 guid를 c6f6bf8이 지웠는데 f6fa527이 그 중간 시점
    # 로스터를 스캔해 이미 죽은 guid를 다시 참조로 남겼다, 뿌리 ⑱). ④는 "에셋은
    # 있는데 참조가 없다", ⑥은 "참조는 있는데 에셋이 없다" — 둘 다 있어야 배선이
    # 실제로 닫힌 것이다.
    dangling_refs = []  # (referrer_path, guid)
    for g, rs in guid_to_rosters.items():
        if g in guid_to_path:
            continue
        for rp in rs:
            dangling_refs.append((rp, g))
    for tp in glob("Assets/Data/Traits/*.asset"):
        m = re.search(r"^  replacementSkill: \{fileID: \d+(?:, guid: ([0-9a-f]+))?", read(tp), re.MULTILINE)
        if m and m.group(1) and m.group(1) not in guid_to_path:
            dangling_refs.append((tp, m.group(1)))

    # ── 불변식 ① — 원작 유닛 하나의 스킬이 두 로스터 유닛에 걸침 ────────────
    key_to_skill_paths = {}
    unparsed_desc = []
    for sp in skill_assets:
        text = read(sp)
        desc_m = re.search(r"^  description: (.*)$", text, re.MULTILINE)
        desc = desc_m.group(1) if desc_m else ""
        key = extract_original_key(desc)
        if key is None:
            unparsed_desc.append(sp)
            continue
        key_to_skill_paths.setdefault(key, []).append(sp)

    split_violations = []
    for key, sps in key_to_skill_paths.items():
        owners = set()
        for sp in sps:
            guid = path_to_guid.get(str(sp))
            if guid is None:
                continue
            owners |= guid_to_rosters.get(guid, set())
        if len(owners) > 1:
            split_violations.append((key, sps, owners))

    # ── 불변식 ⑤(정보) — 원작 표(확정/부분확정)에는 있는데 로스터 어디에도 안 붙음 ──
    csv_path = ROOT / "Docs/reference/ORIGINAL_UNLISTED_SKILL_EFFECTS.csv"
    csv_names = set()
    if csv_path.exists():
        with open(csv_path, encoding="utf-8") as f:
            for row in csv.DictReader(f):
                if row.get("판정") in ("확정", "부분확정"):
                    csv_names.add(row["유닛이름"].strip())

    assigned_keys = set()
    for key, sps in key_to_skill_paths.items():
        for sp in sps:
            guid = path_to_guid.get(str(sp))
            if guid and guid_to_rosters.get(guid):
                assigned_keys.add(key)
                break

    dropped_units = sorted(csv_names - assigned_keys)

    # ── 리포트 ───────────────────────────────────────────────────────────
    any_problem = False

    print(f"[① 원작 유닛 스킬 분산] 원작 키 {len(key_to_skill_paths)}개 중 두 로스터 유닛에 "
          f"걸친 것 {len(split_violations)}개")
    for key, sps, owners in split_violations:
        any_problem = True
        print(f"  ❌ 원작 '{key}' — 로스터 {len(owners)}곳에 걸쳐 있음:")
        for o in sorted(owners):
            print(f"      로스터: {o.relative_to(ROOT)}")
        for sp in sps:
            print(f"      스킬:   {sp.relative_to(ROOT)}")
    print()

    print(f"[② SkillData 중복 참조] 배선된 스킬 에셋 {len(guid_to_rosters)}개 중 두 로스터 "
          f"유닛이 같이 참조하는 것 {len(cross_roster_dup)}개")
    for g, rs in cross_roster_dup.items():
        any_problem = True
        path = guid_to_path.get(g)
        label = Path(path).relative_to(ROOT) if path else f"(경로 못 찾음, guid {g})"
        print(f"  ❌ {label} — 로스터 {len(rs)}곳:")
        for r in sorted(rs):
            print(f"      {r.relative_to(ROOT)}")
    print()

    print(f"[③ 로스터 skills 내부 중복] 로스터 {len(roster_assets)}개 중 같은 guid가 skills "
          f"리스트에 두 번 이상 들어간 것 {len(dup_within_roster)}개")
    for rp, g in dup_within_roster:
        any_problem = True
        print(f"  ❌ {rp.relative_to(ROOT)} — guid {g} 중복")
    print()

    print(f"[④ 고아 스킬 에셋] 스킬 에셋 {len(skill_assets)}개 중 로스터 skill/skills에도 "
          f"트레잇 replacementSkill에도 안 걸린 것 {len(orphaned_skills)}개")
    for sp in orphaned_skills:
        any_problem = True
        print(f"  ❌ {sp.relative_to(ROOT)} — 아무 데도 안 걸림(만들어졌는데 아무도 안 씀)")
    print()

    print(f"[⑥ 끊어진 배선] 로스터·트레잇이 참조하는 guid 중 실제 SkillData 에셋이 "
          f"없는 것 {len(dangling_refs)}개 (④의 거울 — 참조는 있는데 에셋이 없음)")
    for referrer, g in dangling_refs:
        any_problem = True
        print(f"  ❌ {referrer.relative_to(ROOT)} — guid {g}를 참조하지만 그런 SkillData가 없음")
    print()

    print(f"[⑤ 정보] {csv_path.name}(판정=확정/부분확정) 원작 유닛 {len(csv_names)}개 중 로스터 "
          f"어디에도 안 붙은 것 {len(dropped_units)}개 — 오류 아님(이 CSV 범위 밖 원작 유닛은 "
          f"안 셈, 로스터 등급 정원이 좁아 일부를 못 담는 것도 정상일 수 있다)")
    for u in dropped_units:
        print(f"  · {u}")
    if unparsed_desc:
        print(f"\n  ⚠️  description에서 원작 유닛 키를 못 뽑은 스킬 에셋 {len(unparsed_desc)}개 "
              f"(①·⑤ 판정에서 제외됨 — 조용히 무시한 게 아니라 여기 목록으로 남긴다):")
        for p in unparsed_desc:
            print(f"    {p.relative_to(ROOT)}")
    print()

    orphaned_caster, orphaned_target, unclassified_calls, dead_grant_caster, dead_grant_target = \
        find_orphaned_buff_gates(skill_assets)
    print(f"[⑦-a 죽은 버프 게이트 — 요구하는데 아무도 안 거는 버프] requiredBuffId(캐스터) "
          f"{len(orphaned_caster)}개 · requiredTargetBuffId(대상) {len(orphaned_target)}개 — "
          f"그 버프를 거는 자산·코드가 하나도 없음(그 효과는 영영 안 나간다)")
    for buff_id, ps in sorted(orphaned_caster.items()):
        any_problem = True
        print(f"  ❌ requiredBuffId={buff_id} — 아무도 안 검(캐스터 버프): {', '.join(str(p.relative_to(ROOT)) for p in ps)}")
    for buff_id, ps in sorted(orphaned_target.items()):
        any_problem = True
        print(f"  ❌ requiredTargetBuffId={buff_id} — 아무도 안 검(대상 버프): {', '.join(str(p.relative_to(ROOT)) for p in ps)}")
    if unclassified_calls:
        print(f"\n  ⚠️  캐스터/대상으로 못 가른 AddBuff(\"...\") 리터럴 호출 {len(unclassified_calls)}개 "
              f"(휴리스틱이 변수명을 못 알아봄 — 사람이 확인할 것):")
        for cs_path, receiver, buff_id in unclassified_calls:
            print(f"    {cs_path.relative_to(ROOT)} — {receiver}.AddBuff(\"{buff_id}\", ...)")
    print()

    print(f"[⑦-b 조용한 과다발동 — 거는데 아무도 안 요구하는 버프] 캐스터 {len(dead_grant_caster)}개 · "
          f"대상 {len(dead_grant_target)}개 — opener가 이 버프를 걸어도 그걸 게이트로 쓰는 소비 "
          f"쪽이 없으면, 소비 스킬이 requiredBuffId가 애초에 비어있는 것처럼(=항상 통과) "
          f"실행된다. ⚠️ 이 숫자가 반드시 0이어야 하는 건 아니다 — 원작에 정말 게이트가 "
          f"없는 순수 버프(B035·B06Y처럼 JASS 전수 검색으로 확인된 것)는 `[버프게이트예외:ID]` "
          f"마커로 뺀다. 마커도 없이 남아있는 항목만 진짜 의심 대상이다(2026-09-07 발견분 "
          f"B05N·B03M·B00D·B045·B03Z 5건은 이미 배선 완료, B00S는 SkillData_원작013_H099_A09S로 "
          f"소비 쪽을 새로 만들어 해소했다)")
    for buff_id, srcs in sorted(dead_grant_caster.items()):
        any_problem = True
        loc = ', '.join(s if isinstance(s, str) else str(s.relative_to(ROOT)) for s in srcs)
        print(f"  ❌ buffId={buff_id}(캐스터) — 아무도 requiredBuffId로 안 씀, 거는 곳: {loc}")
    for buff_id, srcs in sorted(dead_grant_target.items()):
        any_problem = True
        loc = ', '.join(s if isinstance(s, str) else str(s.relative_to(ROOT)) for s in srcs)
        print(f"  ❌ buffId={buff_id}(대상) — 아무도 requiredTargetBuffId로 안 씀, 거는 곳: {loc}")

    sys.exit(1 if any_problem else 0)


if __name__ == "__main__":
    main()
