"""스턴 효과의 영웅 지속(SkillEffect.heroDuration = 원작 ahdu)을 그 스턴을 낸 원작 능력에서 채운다 — 2026-09-30 구현담당1(PM 지시).

저항 피부 적(EnemyData.resistantSkin)은 duration 대신 heroDuration으로 스턴이 걸린다(UnitAttacker.StunDurationOn).
찾는 법: SkillData의 Stun 효과(kind 1, duration > 0)마다, 그 에셋의 이름·설명에 적힌 능력 코드 가운데
  스턴을 내는 기반(AOws stomp · ACbh/AHbh 강타 · AHtb/ANfb/ACtb 볼트 · AHtc/ACt2 천둥박수는 제외 — 이감)이고
  일반 지속(adur — AOws 빈칸은 스톡 3.0, apply_immortal_blocks.STOCK_AOWS_ADUR)이 효과의 duration과 같은 능력을 찾아 그 ahdu를 쓴다.
  - 후보가 여럿이고 ahdu가 갈리면 가장 짧은 것(보스 쪽으로 보수적)과 함께 「모호」로 적는다.
  - 못 찾으면 0으로 둔다 → 런타임이 duration × UnitAttacker.HeroDurationFallbackRatio(0.5)로.
  - ahdu가 0으로 명시된 능력(영웅엔 안 걸림)은 0.01로 적는다(0은 「모름」 센티널이라).
사용: python3 Tools/sync_stun_hero_duration_from_w3a.py [--dry]
"""
import glob
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'w3x'))
import skill_asset_tool as sat  # noqa: E402
import w3a  # noqa: E402

STUN_BASES = ('AOws', 'ACbh', 'AHbh', 'AHtb', 'ANfb', 'ACtb', 'ANsb', 'ACfb')
STOCK_AOWS_ADUR = 3.0


def main(dry):
    AB = {}
    for a in w3a.parse(os.path.join(sat.ROOT, 'Tools/w3x/원본/war3map_new.w3a')):
        if a['base'] not in STUN_BASES:
            continue
        f = {}
        for m in a['mods']:
            if m.get('level') in (0, 1):
                f.setdefault(m['field'], m['value'])
        adur = float(f['adur']) if 'adur' in f else (STOCK_AOWS_ADUR if a['base'] == 'AOws' else None)
        if adur is None or 'ahdu' not in f:
            continue
        AB[a['id']] = (round(adur, 3), round(float(f['ahdu']), 3))
    total = filled = same = ambiguous = 0
    changed, unknown = [], []
    for p in sorted(glob.glob(os.path.join(sat.SKILL_DIR, '*.asset'))):
        a = sat.load(p)
        codes = {c for c in re.findall(r'(?<![A-Za-z0-9])A[0-9A-Z]{3}(?![A-Za-z0-9])', a.head) if c in AB}
        touched = False
        for i in range(len(a.levels)):
            blocks = sat.get_effect_blocks(a, i)
            out = []
            for b in blocks:
                if re.search(r'^\s*-? ?kind: 1$', b, re.M):
                    d = float((re.search(r'^\s*duration: (\S+)', b, re.M) or [0, 0])[1])
                    if d > 0:
                        total += 1
                        cands = sorted({AB[c][1] for c in codes if abs(AB[c][0] - d) < 0.011})
                        if cands:
                            h = max(0.01, cands[0])
                            if len(cands) > 1:
                                ambiguous += 1
                            cur = re.search(r'^\s*heroDuration: (\S+)', b, re.M)
                            if cur and abs(float(cur.group(1)) - h) < 1e-4:
                                same += 1
                            else:
                                b = (re.sub(r'^(\s*heroDuration:) .*$', lambda m: m.group(1) + ' ' + sat.num(h), b, count=1, flags=re.M) if cur
                                     else b.rstrip('\n') + '\n      heroDuration: %s\n' % sat.num(h))
                                touched = True
                            filled += 1
                        else:
                            unknown.append((os.path.basename(p)[10:-6], d))
                out.append(b)
            if touched:
                sat.set_effects(a, i, out)
        if touched:
            changed.append(p)
            if not dry:
                sat.save(a)
    print('스턴 효과 %d · 영웅 지속 찾음 %d(그중 이미 같음 %d · 모호 %d) · 못 찾음 %d(런타임 ×0.5) · 바꾼 에셋 %d' % (total, filled, same, ambiguous, len(unknown), len(changed)))
    for n, d in unknown:
        print('  못 찾음 %s (스턴 %g초)' % (n, d))


if __name__ == '__main__':
    main('--dry' in sys.argv)
