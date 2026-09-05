#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""d6499ef가 만든 더미 채널 SkillData 40개(description: 텍스트만 덧붙인다.

⚠️ 뿌리 ⑱ 대응: fill_dummy_channel_damage.py는 에셋 트리를 스캔해 원작→로스터
대응표를 매번 새로 만들기 때문에, 그 사이 다른 세션의 커밋이 들어오면 배정이
흔들린다(실제로 겪음 — h023·h04F 등이 사라지고 h01X가 중복됐었다, 되돌림).
이 스크립트는 **배정 로직을 전혀 안 탄다** — d6499ef가 실제로 건드린 파일
목록(`git show --name-only d6499ef`)을 그대로 읽어 그 파일들의 description
끝에 문구만 추가한다.
"""
import subprocess

TAG = (
    " [근사: 원작 게이트 미확인] 부모 평타 트리거의 게이트를 찾지 못해 매 타 발동"
    "(triggerChance=1.0)으로 뒀다. 원작 미수록 196건의 게이트 분포가 게이지 86·"
    "확률 86·없음 27이라 매 타 발동은 소수다 — 과다 계상 가능. 부분 매치 후보: "
    "Dragon_Attack · Kaido_Attack · Sengoku_Attack · Shiki_Attack · LaillySkill · "
    "Gaban_Attack · Ace_Attack(한 부모 밑에 게이트가 다른 자식이 여럿이라 자식 "
    "능력을 하나씩 봐야 판정된다)."
)

MARKER = "[근사: 원작 게이트 미확인]"


def main():
    out = subprocess.run(
        ['git', '-c', 'core.quotepath=false', 'show', '--name-only', 'd6499ef'],
        cwd='.', capture_output=True, text=True, check=True,
    ).stdout
    files = [
        line for line in out.splitlines()
        if line.startswith('Assets/Data/UnitSkills/SkillData_더미채널_') and line.endswith('.asset')
    ]
    print(f'대상 파일 {len(files)}개')

    patched = 0
    skipped_already = 0
    for path in files:
        text = open(path, encoding='utf-8').read()
        if MARKER in text:
            skipped_already += 1
            continue
        lines = text.split('\n')
        for i, line in enumerate(lines):
            if line.startswith('  description: '):
                lines[i] = line + TAG
                break
        else:
            print('⚠️ description 줄을 못 찾음:', path)
            continue
        open(path, 'w', encoding='utf-8').write('\n'.join(lines))
        patched += 1

    print(f'패치: {patched}개, 이미 있어 건너뜀: {skipped_already}개')


if __name__ == '__main__':
    main()
