"""gameshot autoloop 결과(ClaudeBridge/outbox/<id>.txt)를 밸런스 표 한 줄씩으로 줄인다 — 2026-09-30 구현담당1(BALANCE_2026-09-30.md).

라운드 줄(🏁·💀)에서: 도달 라운드 · 등급별 유닛 수 · 스토리 깬 수 · 조합 누계(🧪) · ⚖️ 줄(평타 광역 타당 맞은 수 · 피해 비중 · 보스 스턴) · 보스 처치 시간(👑).
사용: python3 Tools/balance_digest.py ClaudeBridge/outbox/g1_181.txt [...]
"""
import re
import sys

GRADE = {'Common': '흔함', 'Uncommon': '안흔함', 'Special': '특별함', 'Rare': '희귀함', 'Legendary': '전설', 'Hidden': '히든', 'Limited': '제한',
         'Transcendent': '초월', 'Immortal': '불멸', 'Eternal': '영원', 'Superior': '특수함', 'TranscendentWisp': '초월위습', 'Changed': '변화됨',
         'RandomOnly': '랜덤', 'OtherWorld': '다른세계'}


def digest(path):
    text = open(path, encoding='utf-8').read()
    lines = text.split('\n')
    out = {'file': path.split('/')[-1], 'rounds': [], 'boss': [], 'end': None}
    for i, l in enumerate(lines):
        m = re.search(r'(🏁 라운드 (-?\d+)→(\d+)|💀 끝)[^:]*: (.*)', l)
        if m:
            body = m.group(4)
            r = {'to': int(m.group(3)) if m.group(3) else None, 'over': m.group(1).startswith('💀')}
            u = re.search(r'내 유닛 (.*?) · 위습', body)
            r['units'] = ' '.join('%s %s' % (GRADE.get(k, k), v) for k, v in re.findall(r'(\w+) (\d+)', u.group(1))) if u else ''
            s = re.search(r'깸 (\d+)', body)
            r['story'] = int(s.group(1)) if s else -1
            g = re.search(r'골드 (-?\d+)', body)
            r['gold'] = int(g.group(1)) if g else -1
            for j in range(i + 1, min(i + 14, len(lines))):
                x = lines[j]
                if '🏁' in x or '💀' in x:
                    break
                c = re.search(r'판 누계 \[(.*?)\]', x)
                if '🧪 희귀함' in x and c:
                    r['combined'] = c.group(1)
                b = re.search(r'⚖️ 판 누계 평타 (\d+)타 · 광역 든 유닛 (\d+)타에 주변 (\d+)마리\(타당 ([\d.]+)\) · 피해 비중 (.*)', x)
                if b:
                    r['hits'], r['splashUnitHits'], r['splashHits'], r['perHit'], r['share'] = int(b.group(1)), int(b.group(2)), int(b.group(3)), b.group(4), b.group(5)
                if '⚖️ 광역·다중 유닛' in x:
                    r['areaUnits'] = x.split(':', 1)[1].strip()
                st = re.search(r'⚖️ 이번 라운드 0번 레인 보스: 살아 있던 ([\d.]+)초 중 스턴 ([\d.]+)초.*이감 ([\d.]+)초', x)
                if st:
                    r['bossAlive'], r['bossStun'], r['bossSlow'] = float(st.group(1)), float(st.group(2)), float(st.group(3))
                if '⚖️ 「영구 스턴」' in x:
                    r['six'] = x.split(':', 1)[1].strip()
                if '⚖️ 피해 상위' in x:
                    r['top'] = x.split(':', 1)[1].strip()
            out['rounds'].append(r)
        k = re.search(r'👑 R(\d+) 보스 (\S+) \*\*(처치|사라짐)\*\*.*?등장 뒤 ([\d.]+)초', l)
        if k:
            out['boss'].append((int(k.group(1)), k.group(3), float(k.group(4))))
        if '제한 초과, 패배' in l or '보스제한' in l:
            out['end'] = l.strip()[:160]
    return out


def main(paths):
    for p in paths:
        d = digest(p)
        last = d['rounds'][-1] if d['rounds'] else {}
        reached = max([r['to'] for r in d['rounds'] if r['to']] or [0])
        print('## %s — %s · 도달 R%d · 스토리 %s · 끝 유닛 [%s]' % (d['file'], '패배' if last.get('over') else '끝까지', reached, last.get('story'), last.get('units')))
        print('   조합 누계 [%s]' % last.get('combined', '-'))
        print('   평타 %s타 · 광역 든 유닛 %s타에 주변 %s마리(타당 %s) · 피해 비중 %s' % (last.get('hits'), last.get('splashUnitHits'), last.get('splashHits'), last.get('perHit'), last.get('share')))
        if last.get('areaUnits'):
            print('   광역·다중 유닛: %s' % last['areaUnits'])
        if last.get('top'):
            print('   피해 상위: %s' % last['top'])
        print('   보스: ' + ' · '.join('R%d %s %.1f초' % b for b in d['boss']))
        stun = [(r['to'], r['bossAlive'], r['bossStun'], r['bossSlow']) for r in d['rounds'] if r.get('bossAlive', 0) > 1]
        if stun:
            print('   보스 스턴(살아 있던 1초 넘는 라운드): ' + ' · '.join('→R%s %.0f초 중 스턴 %.0f%%·이감 %.0f%%' % (t, a, 100 * s / a, 100 * w / a) for t, a, s, w in stun))
        six = sorted({r['six'] for r in d['rounds'] if r.get('six')})
        print('   「영구 스턴」 후보 보유: %s' % (', '.join(six) if six else '없음'))
        if d['end']:
            print('   끝: %s' % d['end'])
        print('   라운드별 유닛: ' + ' | '.join('R%s [%s]' % (r['to'], r['units']) for r in d['rounds'] if r['to'] and r['to'] % 5 == 0))
        print()


if __name__ == '__main__':
    main(sys.argv[1:])
