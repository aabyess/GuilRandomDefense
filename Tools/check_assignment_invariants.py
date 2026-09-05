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
많아 일부를 못 담는 건 정상일 수 있다) — 실패에 안 넣는다.
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

    sys.exit(1 if any_problem else 0)


if __name__ == "__main__":
    main()
