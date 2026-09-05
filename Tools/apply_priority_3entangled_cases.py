#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""⑤-2 파트 B(PM 지시, 2026-09-06): check_assignment_invariants ①의 나머지 3건
— 2채널이 게이트/회수에 지는 경우(원칙 2: 06번①>게이트/회수>1채널>2채널).

⚠️ 단순 파일 이동이 아니다 — 재태깅(844210c)에서 확인했듯 패자(2채널) 파일은
그 자체가 이미 "2채널 원 내용 + ⑤-0이 나중에 붙인 무관한 1채널 내용"이 뒤섞인
entangled 파일이다(예: 불멸_정윤식 = 골.D.로져(2채널, 자기 것) + 실버즈 레일리
(1채널, 우연히 같은 파일에 실린 남의 것)). 승자(게이트/회수) 로스터는 이미
이 원작 키의 1채널 몫을 정확히 갖고 있다(⑤-0의 1단계가 올바르게 라우팅함) —
없는 건 패자 쪽의 "옛 2채널" 몫뿐이다. 그래서:
  1. 패자 파일에서 2채널 몫(설명 앞부분 + 맨 앞 N개 effects 블록, N=설명의
     "행 {N}개")만 잘라 승자 파일에 이어붙인다.
  2. 패자 파일은 나머지(1채널 몫)만 남겨 독립된 유효한 SkillData로 다시 쓴다
     (guid·로스터 참조는 그대로 — 파일이 사라지는 게 아니라 내용만 준다).

값 손실 검증: 이동 전/후 전체 effects 블록 수가 보존되는지 확인한다(승자
증가분 == 패자 감소분).
"""
import re

UNIT_SKILLS_DIR = 'Assets/Data/UnitSkills'
SUFFIX_MARKERS = [' | 1채널 추가 배정(', ' | 1채널 우선 배정(']

# (원작 키, 승자 base, 패자 base)
CASES = [
    ('골.D.로져', '불멸_김용태', '불멸_정윤식'),
    ('스코퍼 가반', '불멸_신지우', '불멸_이승우'),
    ('금사자 시키', '불멸_고도현', '불멸_박은석'),
]


def split_desc(desc):
    """설명을 (2채널 몫, 1채널 몫 원문) 로 자른다. 1채널 몫은 앞의 ' | ' 포함
    원문 그대로(재삽입은 별도 처리)."""
    pos = len(desc)
    for m in SUFFIX_MARKERS:
        idx = desc.find(m)
        if idx != -1:
            pos = min(pos, idx)
    return desc[:pos], desc[pos:]


def block_count_from_desc(ch2_desc):
    m = re.search(r'행 (\d+)개', ch2_desc)
    if not m:
        raise SystemExit(f'"행 N개"를 못 찾음: {ch2_desc[:200]!r}')
    return int(m.group(1))


def split_blocks(effects_text, n):
    starts = [m.start() for m in re.finditer(r'^    - kind: \d+\n', effects_text, re.M)]
    if len(starts) < n:
        raise SystemExit(f'블록 {len(starts)}개인데 {n}개를 떼려 함')
    cut = starts[n] if len(starts) > n else len(effects_text)
    return effects_text[:cut], effects_text[cut:]


def first_bullet_id(text):
    # ⚠️ bullet 줄("  · ...")은 description YAML 값의 raw 개행 연속선이라 우리 regex로
    # 캡처한 description 변수(첫 줄만)엔 안 담긴다 — 파일 전체 텍스트에서 찾아야 한다.
    m = re.search(r'^  · ([^:]+):', text, re.M)
    return m.group(1).strip() if m else 'unknown'


def main():
    total_moved_blocks = 0
    for key, winner_base, loser_base in CASES:
        winner_path = f'{UNIT_SKILLS_DIR}/SkillData_원작능력_{winner_base}.asset'
        loser_path = f'{UNIT_SKILLS_DIR}/SkillData_원작능력_{loser_base}.asset'

        winner_text = open(winner_path, encoding='utf-8').read()
        loser_text = open(loser_path, encoding='utf-8').read()

        loser_desc_m = re.search(r'^  description: (.*)$', loser_text, re.M)
        loser_desc = loser_desc_m.group(1)

        MARKER = f' | ⑤-2 이관 완료({key})'
        if MARKER in loser_desc:
            print(f'[{key}] 이미 처리됨 — 건너뜀')
            continue

        ch2_desc, ch1_desc_raw = split_desc(loser_desc)
        n = block_count_from_desc(ch2_desc)

        # effects 블록 잘라내기 — description 다음, 'triggerType:' 앞의 'effects:' 블록.
        effects_m = re.search(r'(    effects:\n)((?:    - kind: \d+\n(?:      .*\n)+)+)',
                               loser_text)
        if not effects_m:
            raise SystemExit(f'{loser_path}: effects 블록을 못 찾음')
        effects_text = effects_m.group(2)
        moved_blocks, kept_blocks = split_blocks(effects_text, n)

        before_total = len(re.findall(r'^    - kind: \d+\n', effects_text, re.M))

        # ── 승자 파일에 이관분 추가 ──
        winner_desc_m = re.search(r'^  description: (.*)$', winner_text, re.M)
        winner_desc = winner_desc_m.group(1)
        addition = f' | ⑤-2 이관(원작 {key}, 옛 2채널 중복 정리 — 게이트/회수가 채널우선순위 승자): {ch2_desc.strip()}'
        new_winner_desc = winner_desc + addition
        winner_text2 = winner_text.replace(f'  description: {winner_desc}\n',
                                            f'  description: {new_winner_desc}\n', 1)
        winner_text2 = winner_text2.rstrip('\n') + '\n' + moved_blocks
        if winner_text2 == winner_text:
            raise SystemExit(f'{winner_path}: 치환 실패(description 못 찾음)')
        open(winner_path, 'w', encoding='utf-8').write(winner_text2)

        # ── 패자 파일은 1채널 몫만 남겨 재작성 ──
        ch1_desc = ch1_desc_raw.lstrip()
        if ch1_desc.startswith('| '):
            ch1_desc = ch1_desc[2:].lstrip()
        new_loser_desc = ch1_desc + MARKER
        loser_text2 = loser_text.replace(f'  description: {loser_desc}\n',
                                          f'  description: {new_loser_desc}\n', 1)
        loser_text2 = loser_text2.replace(effects_text, kept_blocks, 1)
        new_skill_name = first_bullet_id(loser_text2)
        loser_text2 = re.sub(r'^  skillName: .*$', f'  skillName: {new_skill_name}',
                              loser_text2, count=1, flags=re.M)
        if loser_text2 == loser_text:
            raise SystemExit(f'{loser_path}: 치환 실패')
        open(loser_path, 'w', encoding='utf-8').write(loser_text2)

        after_total = (len(re.findall(r'^    - kind: \d+\n', moved_blocks, re.M))
                       + len(re.findall(r'^    - kind: \d+\n', kept_blocks, re.M)))
        assert before_total == after_total, (key, before_total, after_total)

        total_moved_blocks += n
        kept_count = len(re.findall(r'^    - kind: \d+\n', kept_blocks, re.M))
        print(f'[{key}] {loser_base} -> {winner_base}: 블록 {n}개 이관, {loser_base}엔 {kept_count}개 남김')

    print(f'총 이관 블록 {total_moved_blocks}개')


if __name__ == '__main__':
    main()
