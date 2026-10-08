#!/usr/bin/env python3 -I
"""UnitData·EnemyData(·퀘스트 미니보스 등 Assets/Data 전부)의 `prefab: {fileID, guid}`가 실제 프리팹 안에 있는 fileID인지 검사한다.
프리팹을 재생성(「Tools/아트/모델 배선」)하면 루트 GameObject fileID가 바뀌는데 참조 에셋이 안 따라가면 클린 빌드에서 null이 된다(10-08 6종).
읽기 전용. 인자: 프로젝트 루트(기본 현재 폴더). 특정 커밋 검사: git archive <ref> Assets/Data Assets/Prefabs | tar -x -C <빈 폴더> 뒤 그 폴더를 준다.
종료 코드: 깨진 참조가 있으면 1."""
import re, glob, os, sys
root = sys.argv[1] if len(sys.argv) > 1 else '.'
guid2path = {}
for m in glob.glob(os.path.join(root, 'Assets/**/*.prefab.meta'), recursive=True):
    g = re.search(r'guid: (\w+)', open(m, encoding='utf-8').read())
    if g: guid2path[g.group(1)] = m[:-5]
cache, bad, total = {}, [], 0
for a in glob.glob(os.path.join(root, 'Assets/Data/**/*.asset'), recursive=True):
    t = open(a, encoding='utf-8', errors='replace').read()
    for m in re.finditer(r'prefab: \{fileID: (\d+), guid: (\w+), type: 3\}', t):
        fid, g = m.groups(); total += 1
        p = guid2path.get(g)
        if not p: bad.append((os.path.relpath(a, root), '프리팹 파일 없음', g)); continue
        if p not in cache: cache[p] = open(p, encoding='utf-8', errors='replace').read()
        if f'&{fid}\n' not in cache[p]: bad.append((os.path.relpath(a, root), '프리팹 안에 없는 fileID', fid, os.path.basename(p)))
print(f'참조 {total}건 검사 · 깨짐 {len(bad)}건')
for b in bad: print('  ', *b)
sys.exit(1 if bad else 0)
