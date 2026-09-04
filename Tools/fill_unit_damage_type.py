#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""로스터의 `damageType`을 **조합표에 적힌 대로** 채운다.

근거는 두 곳이고 둘 다 저장소 안에 있다:
  1. `Docs/reference/RECIPES.md`의 **「타입」 열** — 초월·히든·불멸·영원·제한·다른세계
  2. **로스터 파일명의 접미사** `_AD` / `_AP` / `_ADAP` — 초월 25종

⚠️ **하위 등급(흔함·안흔함·특별함·희귀함·특수함·전설적인·변화됨)은 근거가 없다.**
우리가 가진 조합표 이미지 4장이 전부 상위 등급이고, `RECIPES_LOW.md`엔 타입 열이 없다.
그 유닛들은 **건드리지 않는다** — 지금 값(`AD`)은 `generate_unit_stats.py`가 일괄로 박은
것이지 조합표에서 온 게 아니다. 근거 있는 값과 섞이지 않게 그대로 둔다.

⚠️ 이 파일을 고칠 땐 `generate_unit_stats.py`도 같이 보라. 예전엔 그쪽이 `damageType`을
1(AD)로 덮어써서, 조합표가 `(AP)`라고 적어둔 유닛까지 물리로 되돌아갔다.
"""
import re, os, sys, collections

AD, AP = 1, 2
CODE = {'AD': AD, 'AP': AP, 'AD+AP': AD | AP, 'ADAP': AD | AP}

ROSTER = 'Assets/Data/Units/Roster'
# RECIPES.md의 절 제목 → 로스터 파일명 접두
SECTION = {'초월': '초월', '히든': '히든', '불멸': '불멸', '영원': '영원',
           '제한': '제한', '다른세계': '다른세계', '랜덤유닛': '랜덤'}


def read_doc():
    """(등급접두, 이름) -> 타입 문자열."""
    out = collections.defaultdict(list)
    section = None
    for line in open('Docs/reference/RECIPES.md', encoding='utf-8'):
        if line.startswith('## '):
            section = line[3:].split('(')[0].strip()
        m = re.match(r'^\|\s*([^|]+?)\s*\|\s*(AD\+AP|AD|AP)\s*\|', line)
        if m and section in SECTION:
            out[(SECTION[section], m.group(1).strip().replace(' ', '_'))].append(m.group(2))

    # 랜덤유닛은 표가 아니라 줄글이다 — "성탄(AD)"처럼 이름 뒤에 괄호로 붙는다.
    # 「히든 > 개」의 솔·성탄·뻬꼼도 같은 꼴이라 함께 훑는다.
    text = open('Docs/reference/RECIPES.md', encoding='utf-8').read()
    for prefix, start, end in [('랜덤', '## 랜덤유닛', '## 다른세계'),
                               ('히든', '### 개', '---')]:
        i = text.index(start)
        block = text[i:text.index(end, i)]
        for m in re.finditer(r'([가-힣A-Za-z0-9 ]+?)\((AD\+AP|AD|AP)\)', block):
            out[(prefix, m.group(1).strip().replace(' ', '_'))].append(m.group(2))
    return out


def split(stem):
    """파일명을 (등급접두, 이름, 접미사)로 가른다."""
    parts = stem.split('_')
    suffix = None
    if parts[-1] in ('AD', 'AP', 'ADAP'):
        suffix, parts = parts[-1], parts[:-1]
    return parts[0], '_'.join(parts[1:]), suffix


def main():
    doc = read_doc()
    written = skipped = 0
    conflicts, unresolved = [], []

    for name in sorted(os.listdir(ROSTER)):
        if not name.endswith('.asset'):
            continue
        stem = name[:-len('.asset')]
        grade, unit, suffix = split(stem)
        found = doc.get((grade, unit))

        # 파일명 접미사가 문서보다 구체적이다 — 같은 이름의 AD판·AP판이 따로 있는 경우
        # (초월_최상호_AD / 초월_최상호_AP) 문서 쪽은 둘을 구분하지 못한다.
        if suffix:
            kind = suffix
            if found and suffix.replace('ADAP', 'AD+AP') not in found:
                conflicts.append((stem, suffix, found))
        elif found and len(set(found)) == 1:
            kind = found[0]
        else:
            if found:
                conflicts.append((stem, None, found))
            else:
                unresolved.append(stem)
            skipped += 1
            continue

        path = os.path.join(ROSTER, name)
        text = open(path, encoding='utf-8').read()
        new, n = re.subn(r'^  damageType: .*$', f'  damageType: {CODE[kind]}', text,
                         count=1, flags=re.M)
        if n != 1:
            print(f'  ⚠ {stem}: damageType 줄을 못 찾았다', file=sys.stderr)
            skipped += 1
            continue
        if new != text:
            open(path, 'w', encoding='utf-8').write(new)
        written += 1

    if conflicts:
        print('⚠️ 충돌 — 사람이 봐야 한다:')
        for c in conflicts:
            print('   ', c)
    print(f'\n근거 있음 {written}개 / 근거 없음 {skipped}개')
    print('근거 없는 등급별:',
          dict(collections.Counter(u.split('_')[0] for u in unresolved)))
    print('\n상위 등급인데 조합표에도 타입이 안 적힌 것:')
    for u in unresolved:
        if u.split('_')[0] in SECTION.values():
            print('   ', u)


if __name__ == '__main__':
    main()
