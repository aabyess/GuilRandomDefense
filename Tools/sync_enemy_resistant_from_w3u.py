"""적 에셋의 저항 피부 표시(EnemyData.resistantSkin)를 원작 w3u uabi(ACrk)에서 유도한다 — 2026-09-30 구현담당1(PM 지시).

원작에서 ACrk(Resistant Skin)를 가진 적 32종에게는 스턴·둔화가 능력의 영웅 지속(ahdu)으로 걸린다.
우리 적 ↔ 원작 uid 대응(이름 매핑이 아니라 자리로):
  - 라운드 적 Enemy_R<N>_*: j의 udg_Round_UnitType[N]. 65·70·75의 「라운드 보스」(이승우·신지우·이이삭)는 인덱스 95·100·105(Trig_Enemy_Boss_sinsekai), 라인몹은 [N].
  - 스토리 Enemy_Story<NN>_*: 원작 이름이 「NN. …」인 nfgo 유닛.
  - 신세계 사이드보스 Enemy_SideBoss62/66/71_*: 인덱스 82·86·91(라운드 + 20).
  - 해적단 미니보스 Miniboss_*: 체력이 같은 원작 [퀘스트] 유닛(o02F 피카 3270만 = 이영용 · o02L 모리아 1400만 = 배고픈황정기 …).
  - 그 밖(물범·크립·거대해왕류)은 대응 uid에 ACrk가 없거나 대응을 못 세워 false.
사용: python3 Tools/sync_enemy_resistant_from_w3u.py [--dry]
"""
import glob
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
import w3u  # noqa: E402

BOSS_INDEX = {65: 95, 70: 100, 75: 105}
SIDE_INDEX = {62: 82, 66: 86, 71: 91}


def main(dry):
    U = {u['id']: u for u in w3u.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3u'))}
    J = open(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.j'), encoding='utf-8', errors='replace').read()
    round_type = {int(n): uid for n, uid in re.findall(r"udg_Round_UnitType\[(\d+)\]='(\w{4})'", J)}
    story = {}
    for u in U.values():
        m = re.search(r'\|cffff0000(\d+)\. ', str(u['mods'].get('unam', '')))
        if m and u['base'] == 'nfgo':
            story[int(m.group(1))] = u['id']
    quest_by_hp = {int(float(u['mods']['uhpm'])): u['id'] for u in U.values() if '[퀘스트]' in str(u['mods'].get('unam', '')) and u['mods'].get('uhpm')}
    resist = {uid for uid, u in U.items() if 'ACrk' in str(u['mods'].get('uabi', '')).split(',')}
    rows, changed = [], []
    for p in sorted(glob.glob(os.path.join(ROOT, 'Assets/Data/Enemies/*.asset')) + glob.glob(os.path.join(ROOT, 'Assets/Data/PirateQuests/Miniboss_*.asset'))):
        name = os.path.basename(p)[:-6]
        text = open(p, encoding='utf-8').read()
        uid = None
        m = re.match(r'Enemy_R(\d+)_(.*)', name)
        if m:
            n = int(m.group(1))
            is_boss = re.search(r'^  isBoss: 1', text, re.M) is not None
            uid = round_type.get(BOSS_INDEX[n] if (n in BOSS_INDEX and is_boss) else n)
        m = re.match(r'Enemy_Story(\d+)_', name)
        if m:
            uid = story.get(int(m.group(1)))
        m = re.match(r'Enemy_SideBoss(\d+)_', name)
        if m:
            uid = round_type.get(SIDE_INDEX.get(int(m.group(1)), -1))
        if name.startswith('Miniboss_'):
            hp = re.search(r'^  hp: (\S+)', text, re.M)
            uid = quest_by_hp.get(int(float(hp.group(1)))) if hp else None
        want = 1 if uid in resist else 0
        rows.append((name, uid or '-', want))
        cur = re.search(r'^  resistantSkin: (\d)', text, re.M)
        if (int(cur.group(1)) if cur else 0) == want:
            continue
        changed.append(p)
        if dry:
            continue
        if cur:
            text = re.sub(r'^(  resistantSkin:) \d', r'\1 %d' % want, text, count=1, flags=re.M)
        else:
            text = text.rstrip('\n') + '\n  resistantSkin: %d\n' % want
        open(p, 'w', encoding='utf-8').write(text)
    on = [r for r in rows if r[2]]
    print('적 에셋 %d · 저항 피부 %d · 대응 uid 없음 %d · 바꿈 %d' % (len(rows), len(on), sum(1 for r in rows if r[1] == '-'), len(changed)))
    print('  저항 피부:', ' · '.join('%s(%s)' % (r[0].replace('Enemy_', ''), r[1]) for r in on))
    print('  대응 없음:', ' · '.join(r[0].replace('Enemy_', '') for r in rows if r[1] == '-'))
    for c in changed:
        print(os.path.relpath(c, ROOT))


if __name__ == '__main__':
    main('--dry' in sys.argv)
