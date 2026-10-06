#!/usr/bin/python3
# 소환체 UnitData(Assets/Data/Units/Summons/Summon_*.asset)의 prefab 참조를 흔함_강재규의 현재 prefab 참조로 다시 잇는다(2026-10-06 구현담당1).
# 이유: 모델 배선(ArtBinder)을 다시 돌리면 흔함_강재규 프리팹 루트 fileID가 바뀌어 소환체 prefab 참조가 끊긴다("UnitSpawner: UnitData 또는 prefab이 비어있어 소환할 수 없습니다").
# 소환체는 흔함_강재규를 본떠 만들었으니 같은 prefab을 쓴다. 모델 배선 뒤마다 한 번 돌릴 것. 다시 돌려도 안전.
import glob, os, re
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
base = open(os.path.join(ROOT, 'Assets/Data/Units/Roster/흔함_강재규.asset'), encoding='utf-8').read()
ref = re.search(r'^  prefab: (\{.*\})$', base, re.M).group(1)
n = 0
for p in sorted(glob.glob(os.path.join(ROOT, 'Assets/Data/Units/Summons/Summon_*.asset'))):
    t = open(p, encoding='utf-8').read()
    new = re.sub(r'^  prefab: \{.*\}$', '  prefab: ' + ref, t, count=1, flags=re.M)
    if new != t:
        open(p, 'w', encoding='utf-8').write(new); n += 1; print('다시 이음', os.path.basename(p))
print('끝 — 바뀐 소환체 %d개 · prefab %s' % (n, ref))
