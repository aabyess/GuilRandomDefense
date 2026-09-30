"""md.py가 읽는 roster_side.json(우리 로스터 쪽에서 본 대응·스킬 유무)을 만든다 — 2026-09-30 구현담당1이 다시 짬(원래 스크립트는 스크래치와 함께 사라짐).

등급(= 로스터 파일 이름의 첫 토막)별 세 목록:
  nomap       — 대응표(Docs/reference/MASTER_UID_ROSTER_MAP.csv)에 없는 로스터 전부
  nomap_skill — 그중 「효과가 든 스킬」이 달린 로스터(nomap의 부분집합)
  map_noskill — 대응표에 있는데 「효과가 든 스킬」이 하나도 없는 로스터
「효과가 든 스킬」 = ours.py의 UN(skills가 비면 옛 단일 필드 skill로 폴백)에 걸린 에셋 중 효과 수(neff) > 0인 것
— 효과 0짜리 껍데기 에셋(더미채널_*_1 등)만 달린 로스터는 스킬 없음으로 센다.
nomap_skill은 여기에 더해 원작능력_* 번들을 빼고 센다(대응 없는 로스터의 번들은 순위 배정 때 남은 것 — 안흔함 여섯이 이걸로 갈린다).
⚠️ 두 기준은 원래 스크립트가 없어 기대 출력에 맞춰 되짚은 것이다: 이 기준으로 남는 차이는 「기대에만 있는 map_noskill」(그 뒤 스킬을 채운 로스터)뿐. 읽기 전용.
사용(저장소 루트에서): python3 Tools/skill_coverage/roster_side.py <출력 json>
      python3 Tools/skill_coverage/roster_side.py --diff Tools/skill_coverage/expected/roster_side.json   # 저장해 둔 기대 출력과 비교
⚠️ expected/roster_side.json은 09-30 21:01 시점 출력이다 — 그 뒤 스킬을 채웠으면 map_noskill이 줄어드는 게 정상.
"""
import csv
import json
import os
import sys
import unicodedata

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ours  # noqa: E402

N = lambda s: unicodedata.normalize('NFC', s)


def build():
    mapped = {N(r['로스터']) for r in csv.DictReader(open('Docs/reference/MASTER_UID_ROSTER_MAP.csv', encoding='utf-8'))}
    out = {}
    for name, u in sorted(ours.UN.items()):
        side = out.setdefault(name.split('_')[0], {'nomap_skill': [], 'map_noskill': [], 'nomap': []})
        effective = [ours.SK[g]['file'] for g in u['skills'] if ours.SK.get(g, {}).get('neff', 0) > 0]
        if name not in mapped:
            side['nomap'].append(name)
            if any('_원작능력_' not in f for f in effective):
                side['nomap_skill'].append(name)
        elif not effective:
            side['map_noskill'].append(name)
    return out


if __name__ == '__main__':
    out = build()
    if sys.argv[1] == '--diff':
        exp = json.load(open(sys.argv[2], encoding='utf-8'))
        for g in sorted(set(out) | set(exp)):
            for k in ('nomap_skill', 'map_noskill', 'nomap'):
                a, b = set(out.get(g, {}).get(k, [])), set(map(N, exp.get(g, {}).get(k, [])))
                if a != b:
                    print('%s %s: 지금만 %s · 기대만 %s' % (g, k, sorted(a - b), sorted(b - a)))
        print('비교 끝')
    else:
        json.dump(out, open(sys.argv[1], 'w', encoding='utf-8'), ensure_ascii=False)
        print({g: {k: len(v) for k, v in s.items()} for g, s in out.items()})
