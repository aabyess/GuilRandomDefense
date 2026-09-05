#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""⑤-1 (PM 지시, 2026-09-06): check_assignment_invariants.py ①(원작 유닛 스킬 분산)이
찾은 20건 각각의 스킬 파일이 실제로 어느 채널에서 왔는지 정확히 태깅한다.

⚠️ 어젯밤 낸 임시 보고("2차 배정 문자열이 있으면 2채널")는 부정확했다 — 미호크 건에서
확인된 것처럼 SkillData_원작능력_*.asset 한 파일 안에 1채널·2채널 내용이 같이 있을 수
있는데, "포함 여부"만 보면 실제로 check_assignment_invariants.extract_original_key()가
"집은"(반환한) 키가 어느 채널 것인지와 무관하게 태깅됐다. 이 스크립트는 그 대신
"추출기가 실제로 매칭한 위치 앞에 어느 채널의 서문이 있는지"로 정확히 가른다 —
파일명만으로 결정되는 채널(06번①·게이트/회수·더미채널·절대쿨 등)은 그대로 두고,
SkillData_원작능력_*.asset(1채널·2채널이 파일명 규칙을 공유한다, ⑤-0 설계 문서
참고)만 본문 매치 위치로 가른다.

⚠️ 읽기 전용 — 데이터를 안 고친다.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, 'Tools')
import check_assignment_invariants as cai

ROOT = cai.ROOT

FILENAME_CHANNEL = [
    (re.compile(r'^SkillData_원작\d+_'), '06번①'),
    (re.compile(r'^SkillData_게이트_'), '게이트/회수'),
    (re.compile(r'^SkillData_회수_'), '게이트/회수'),
    (re.compile(r'^SkillData_더미채널_'), '더미채널'),
    (re.compile(r'^SkillData_절대쿨_'), '절대쿨'),
    (re.compile(r'^SkillData_카이도_'), '카이도(게이트류)'),
]

CH1_PREAMBLE = 'ORIGINAL_UNIT_ABILITIES.csv'
CH2_PREAMBLE = 'ORIGINAL_SKILL_DAMAGE_TABLE.csv'
# ⚠️ 실행 중 발견 — 다른 세션(96유닛 미수록 스킬 작업, 2a8cb51/96b5e12/fa08337)도
# SkillData_원작능력_{base}.asset 파일명 규칙을 같이 쓴다. 애초에 4채널(06번①·게이트/
# 회수·1채널·2채널)만 있다고 가정했던 CHANNEL_UNIFICATION_DESIGN 문서에 없던 다섯 번째
# 채널이다 — PM 보고 필요.
CH96_PREAMBLE = '96유닛 배정(ORIGINAL_UNLISTED_SKILL_EFFECTS.csv)'
CH96G_PREAMBLE = '게이트별 배정(ORIGINAL_UNLISTED_SKILL_EFFECTS.csv'


def extract_original_key_with_pos(description):
    """cai.extract_original_key()와 완전히 같은 로직이지만 매칭 시작 위치도 같이
    돌려준다(cai는 읽기 전용 검사기라 손 안 댐 — 여기 별도로 복제)."""
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
            continue

        tokens = name_part.split()
        while tokens and (tokens[0] in cai.GRADES or cai._ID_CODE_RE.match(tokens[0])):
            tokens.pop(0)
        name = " ".join(tokens).strip()
        if name:
            return name, om.start()
    return None, -1


# ⚠️ 실행 중 두 번째 발견 — 위 4개 서문만 봐서 놓쳤다: ⑤-0의 1단계(직접배정)·
# 3단계(흡수)가 만드는 SUFFIX("| 1채널 추가/우선 배정(...): ")는 파일 맨 앞이 아니라
# 뒤에 붙는데, 그 뒤에 이어지는 "(uid)의 능력 N개" 자리가 checker의 첫 매치가 될 수
# 있다(앞쪽 세그먼트의 닫는 괄호 뒤 문맥이 "의 "/","로 안 이어지면 스킵되기 때문—
# 골.D.로져 건에서 실제로 이렇게 걸렸다: 1채널 서문 없이 시작하는 파일인데 실제
# 매칭은 뒤쪽 1채널 SUFFIX였다). "서문이 파일 맨 앞에 있다"가 아니라 "매칭 위치
# 바로 앞에 있는 가장 가까운 마커가 그 세그먼트의 소유자"로 정확히 잡아야 한다.
SEGMENT_MARKERS = [
    (CH1_PREAMBLE, '1채널'),
    (CH2_PREAMBLE, '2채널'),
    (CH96_PREAMBLE, '96번(미수록스킬)'),
    (CH96G_PREAMBLE, '게이트별(미수록스킬)'),
    ('1채널 추가 배정(', '1채널'),
    ('1채널 우선 배정(', '1채널'),
    ('2차 배정(트리거·더미 채널)', '2채널'),
]


def channel_of(path: Path, description: str, match_start: int) -> str:
    fname = path.name
    for pat, ch in FILENAME_CHANNEL:
        if pat.match(fname):
            return ch
    if fname.startswith('SkillData_원작능력_'):
        occurrences = []
        for marker, ch in SEGMENT_MARKERS:
            start = 0
            while True:
                idx = description.find(marker, start)
                if idx == -1:
                    break
                occurrences.append((idx, ch))
                start = idx + 1
        occurrences.sort()
        owner = None
        for pos, ch in occurrences:
            if pos <= match_start:
                owner = ch
            else:
                break
        if owner:
            return owner
        # 어떤 마커보다도 앞에서 매칭됐다 — revert()가 1채널 서문을 지운 stacked
        # 2채널 remainder(파일 맨 앞이 "원작 {등급} {이름}(...)."로 바로 시작,
        # ⑤-0에서 실제 파일 열어 STACK_MARKER로 확인된 패턴). 낮은 확신도 표시.
        return '2채널(서문 제거된 remainder로 추정)'
    return f'미분류({fname})'


def main():
    roster_assets = cai.glob("Assets/Data/Units/Roster/*.asset")
    skill_assets = cai.glob("Assets/Data/UnitSkills/*.asset")
    guid_to_path = cai.build_guid_index()
    path_to_guid = {v: k for k, v in guid_to_path.items()}

    roster_to_guids = {}
    guid_to_rosters = {}
    for rp in roster_assets:
        guids = cai.resolve_skill_guids(cai.read(rp))
        roster_to_guids[rp] = guids
        for g in guids:
            guid_to_rosters.setdefault(g, set()).add(rp)

    key_to_skill_paths = {}
    key_to_desc = {}
    for sp in skill_assets:
        text = cai.read(sp)
        desc_m = re.search(r"^  description: (.*)$", text, re.MULTILINE)
        desc = desc_m.group(1) if desc_m else ""
        key = cai.extract_original_key(desc)
        if key is None:
            continue
        key_to_skill_paths.setdefault(key, []).append(sp)
        key_to_desc[sp] = desc

    violations = []
    for key, sps in key_to_skill_paths.items():
        owners = set()
        for sp in sps:
            guid = path_to_guid.get(str(sp))
            if guid is None:
                continue
            owners |= guid_to_rosters.get(guid, set())
        if len(owners) > 1:
            violations.append((key, sps, owners))

    print(f"① 원작 유닛 스킬 분산 {len(violations)}건 — 채널 재태깅(⑤-1)\n")
    channel_pair_counter = {}
    for key, sps, owners in sorted(violations, key=lambda v: v[0]):
        print(f"❌ 원작 '{key}' — 로스터 {len(owners)}곳:")
        by_owner = {}
        for sp in sps:
            guid = path_to_guid.get(str(sp))
            owner_set = guid_to_rosters.get(guid, set()) if guid else set()
            desc = key_to_desc[sp]
            _, pos = extract_original_key_with_pos(desc)
            ch = channel_of(sp, desc, pos if pos >= 0 else 0)
            for owner in owner_set:
                by_owner.setdefault(owner, []).append((sp.name, ch))
        for owner in sorted(by_owner):
            print(f"    로스터 {owner.relative_to(ROOT)}:")
            for fname, ch in by_owner[owner]:
                print(f"        [{ch}] {fname}")
        # 이 키의 "동순위(같은 채널끼리 충돌)" 여부 — 소유자별 최고우선순위 채널만 비교.
        # ⚠️ 96번(미수록스킬)/게이트별(미수록스킬)은 CHANNEL_UNIFICATION_DESIGN 문서의
        # 4채널 우선순위 표에 없다 — 다른 세션이 이 문서 작성 이후 새로 만든 채널이라
        # 순위를 여기서 임의로 안 정한다(PM 판단 필요). 목록 맨 끝에 두되 서로 간의
        # 순서도 미정 표시.
        PRIORITY = ['06번①', '게이트/회수', '절대쿨', '카이도(게이트류)', '더미채널', '1채널', '2채널',
                    '96번(미수록스킬)', '게이트별(미수록스킬)']

        def best_channel(chs):
            for p in PRIORITY:
                if p in chs:
                    return p
            return sorted(chs)[0] if chs else '?'

        owner_best = {owner: best_channel([ch for _, ch in lst]) for owner, lst in by_owner.items()}
        chs = sorted(owner_best.values())
        pair_key = tuple(chs)
        channel_pair_counter[pair_key] = channel_pair_counter.get(pair_key, 0) + 1
        readable = {str(o.relative_to(ROOT)): ch for o, ch in owner_best.items()}
        print(f"    → 소유자별 최우선 채널: {readable}")
        print()

    print("=== 요약: 소유자 최우선 채널 조합별 건수 ===")
    for pair, count in sorted(channel_pair_counter.items(), key=lambda x: -x[1]):
        print(f"  {pair}: {count}건")


if __name__ == '__main__':
    main()
