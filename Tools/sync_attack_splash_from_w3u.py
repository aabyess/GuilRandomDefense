"""로스터 평타 광역(스플래시·클리브)을 원작 w3u·w3a 대응 유닛 값으로 맞춘다 — 2026-09-30 구현담당1(PM 결정, Docs/research/SPLASH_ATTACK_DESIGN.md §7).

UnitData에 쓰는 값(없으면 0 = 평타가 한 마리만):
  attackSplashRadius  — 원작 무기 종류 ua1w = msplash인 대응 유닛의 전체 피해 반경 ua1f(원작 단위).
                        절반·1/4 반경(ua1h·ua1q)은 버린다(피해 계수가 빈칸 = hrif 스톡, 미확인 → 0으로 봄. 엔진 지식, 맵 밖 근거).
                        카이도 둘(ua1f 250 · ua1h 500)은 250만. 카벤딧슈 h05B(ua1f 빈칸 · ua1h 1 · ua1q 325)는 ua1f가 없어 자연히 빠진다.
  attackCleaveFactor·attackCleaveRadius — 대응 유닛 uabi 중 기반 ACce(클리브)이고 nca1 > 0인 능력의 nca1·aare.
                        ⚠️ 클리브 소유 유닛의 무기 종류는 빈칸(hrif 스톡)이거나 missile(우타 h067)이다 — 워크3 클리브가 그 무기 종류에서
                        도는지는 미확정(엔진 지식으론 근접 전용). 제작자 툴팁(「기본공격 415범위 85%」)·능력 이름(「방무뎀」)을 따라 넣는다.
한 로스터에 대응 유닛이 여럿일 때(§7 (가)):
  ① 로스터의 지금 평타 스탯(attackSpeed = 1/ua1c · attackPower = ua1b, 주사위 평균만큼 오차 허용)이 대응 uid 하나와 맞으면 그 uid를 따른다
     — 그 uid가 msplash가 아니면 광역 없음(초월_황준석_ADAP = h04Z).
  ② 아무것과도 안 맞으면 msplash인 uid 중 최댓값, 출력에 「모호」. (로스터 평타 스탯은 순위 배정이라 대응 uid와 안 맞는 로스터가 많다.)
  클리브가 여럿이면 비율이 큰 것 하나(경고).
사용: python3 Tools/sync_attack_splash_from_w3u.py [--dry] [--list <바꾼 파일 목록을 쓸 경로>]
"""
import collections
import csv
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
import w3u  # noqa: E402
import w3a  # noqa: E402

FIELDS = ['attackSplashRadius', 'attackCleaveFactor', 'attackCleaveRadius']


def main(dry, list_path=None):
    U = {u['id']: u for u in w3u.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3u'))}
    CLEAVE = {}
    for a in w3a.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3a')):
        if a['base'] != 'ACce':
            continue
        f = {}
        for m in a['mods']:
            f.setdefault(m['field'], m['value'])
        if float(f.get('nca1') or 0) > 0 and float(f.get('aare') or 0) > 0:
            CLEAVE[a['id']] = (round(float(f['nca1']), 4), float(f['aare']))
    rmap = collections.defaultdict(list)
    for r in csv.DictReader(open(os.path.join(ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
        if r['유닛ID'] in U and r['유닛ID'] not in rmap[r['로스터']]:
            rmap[r['로스터']].append(r['유닛ID'])

    changed, notes, same = [], [], 0
    for ro, uids in sorted(rmap.items()):
        p = os.path.join(ROOT, 'Assets/Data/Units/Roster', ro + '.asset')
        if not os.path.exists(p):
            continue
        text = open(p, encoding='utf-8').read()

        def get(k):
            m = re.search(r'^  %s: (\S+)' % k, text, re.M)
            return round(float(m.group(1)), 4) if m else 0.0

        splash_of = {uid: float(U[uid]['mods'].get('ua1f') or 0) for uid in uids if U[uid]['mods'].get('ua1w') == 'msplash'}
        for uid in uids:
            mo = U[uid]['mods']
            if mo.get('ua1w') == 'msplash' and not splash_of[uid]:
                notes.append('%s(%s): msplash인데 ua1f 빈칸(ua1h %s · ua1q %s) — 제외' % (ro, uid, mo.get('ua1h'), mo.get('ua1q')))
            if mo.get('ua1w') == 'msplash' and (mo.get('ua1h') or mo.get('ua1q')) and splash_of[uid]:
                notes.append('%s(%s): 절반 %s · 1/4 %s 반경은 버림(hrif 스톡 계수 미확인) — ua1f %g만' % (ro, uid, mo.get('ua1h'), mo.get('ua1q'), splash_of[uid]))
        splash_of = {k: v for k, v in splash_of.items() if v > 0}
        splash = 0.0
        if splash_of:
            if len(uids) == 1:
                splash = splash_of[uids[0]]
            else:
                ap, spd = get('attackPower'), get('attackSpeed')
                match = [uid for uid in uids
                         if U[uid]['mods'].get('ua1c') and abs(1.0 / U[uid]['mods']['ua1c'] - spd) < 0.01
                         and U[uid]['mods'].get('ua1b') is not None and abs(ap - U[uid]['mods']['ua1b']) <= 10]
                if len(match) == 1:
                    splash = splash_of.get(match[0], 0.0)
                    notes.append('%s: 평타 스탯이 %s와 맞음 → %s' % (ro, match[0], ('광역 %g' % splash) if splash else '광역 없음(그 유닛은 msplash 아님)'))
                else:
                    splash = max(splash_of.values())
                    notes.append('⚠️ 모호 %s: 평타 스탯(공격력 %g · 주기 %.2f)이 대응 uid %d개 중 %s — msplash %s 중 최댓값 %g' % (
                        ro, ap, 1.0 / spd if spd else 0, len(uids), '여럿과 맞음' if match else '아무것과도 안 맞음',
                        ' '.join('%s=%g' % kv for kv in sorted(splash_of.items())), splash))
        cleaves = sorted({CLEAVE[ab] + (ab, uid) for uid in uids for ab in str(U[uid]['mods'].get('uabi', '')).split(',') if ab in CLEAVE},
                         reverse=True)
        if len({c[:2] for c in cleaves}) > 1:
            notes.append('⚠️ %s: 클리브가 여럿 %s — 비율 큰 것 하나만' % (ro, cleaves))
        for c in cleaves[:1]:
            notes.append('%s: 클리브 %s(%s) %g × 반경 %g · 그 유닛 무기 종류 %s' % (ro, c[2], c[3], c[0], c[1], U[c[3]]['mods'].get('ua1w') or '빈칸(hrif 스톡)'))
        target = [round(splash, 4)] + ([cleaves[0][0], cleaves[0][1]] if cleaves else [0.0, 0.0])
        if [get(k) for k in FIELDS] == target:
            same += 1
            continue
        changed.append((ro, target))
        if dry:
            continue
        for k, v in zip(FIELDS, target):
            s = repr(float(v))
            if re.search(r'^  %s: ' % k, text, re.M):
                text = re.sub(r'^(  %s:) .*$' % k, lambda m: m.group(1) + ' ' + s, text, count=1, flags=re.M)
            else:
                text = text.rstrip('\n') + '\n  %s: %s\n' % (k, s)
        open(p, 'w', encoding='utf-8').write(text)
    print('같음 %d · 바꿈 %d' % (same, len(changed)))
    for ro, t in changed:
        print('  %-28s 스플래시 %g · 클리브 %g × 반경 %g' % (ro, *t))
    if list_path and not dry:
        open(list_path, 'w', encoding='utf-8').write(''.join('Assets/Data/Units/Roster/%s.asset\n' % ro for ro, _ in changed))
        print('바꾼 파일 목록 → %s' % list_path)
    for n in notes:
        print('  참고', n)


if __name__ == '__main__':
    main('--dry' in sys.argv, sys.argv[sys.argv.index('--list') + 1] if '--list' in sys.argv else None)
