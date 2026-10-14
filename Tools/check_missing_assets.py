#!/usr/bin/env python3 -I
"""씬·프리팹·FBX 재질 매핑이 가리키는 에셋(guid)이 실제로 있는지 검사한다 — 「형광 분홍」 사전 차단(10-14).

사고 둘(10-14 사장님 제보 사진):
 · 조합판 「조합표_흔함」 띠 — 씬이 가리키는 combine_NxM.mat가 부분 수리 뒤 지워져 저장소에 없음 → 분홍.
 · 특별함_박민수(조로) — FBX .meta externalObjects가 가리키는 .mat들을 커밋 안 함(작업트리 미추적) → main에서 분홍.
check_prefab_refs.py는 Assets/Data의 prefab 참조만 봐서 둘 다 못 잡았다.

보는 곳: Assets/**/*.unity · *.prefab · *.fbx.meta(externalObjects) · *.mat(텍스처) · *.controller · *.asset 안의 `guid: X`.
있는 것: Assets·Packages·Library/PackageCache의 .meta guid + 유니티 내장(0000000000000000[ef]000000000000000).
`--tracked`: git에 추적되는 .meta만 「있는 것」으로 친다(커밋 빠짐까지 잡는다 — 푸시 관문용).
인자: [--tracked] [프로젝트 루트(기본 현재 폴더)]. 종료 코드: 없는 참조가 있으면 1.
"""
import glob, os, re, subprocess, sys

args = [a for a in sys.argv[1:] if not a.startswith('--')]
tracked_only = '--tracked' in sys.argv
root = os.path.abspath(args[0] if args else '.')
GUID = re.compile(r'guid: ([0-9a-f]{32})')
BUILTIN = re.compile(r'^0{16}[0-9a-f]0{15}$')

def meta_guid(path):
    try:
        with open(path, encoding='utf-8', errors='replace') as f:
            for line in f:
                if line.startswith('guid: '): return line[6:].strip()
    except OSError: pass
    return None

known = {}
if tracked_only:
    out = subprocess.run(['git', '-C', root, 'ls-files', '-z', '--', '*.meta'], capture_output=True).stdout.decode('utf-8', 'replace')
    metas = [os.path.join(root, p) for p in out.split('\0') if p]
else:
    metas = glob.glob(os.path.join(root, 'Assets/**/*.meta'), recursive=True)
for extra in ('Packages', 'Library/PackageCache'):
    metas += glob.glob(os.path.join(root, extra, '**/*.meta'), recursive=True)
for m in metas:
    g = meta_guid(m)
    if g: known[g] = m

patterns = ['**/*.unity', '**/*.prefab', '**/*.fbx.meta', '**/*.FBX.meta', '**/*.mat', '**/*.controller', '**/*.overrideController']
sources = []
for p in patterns:
    sources += glob.glob(os.path.join(root, 'Assets', p), recursive=True)
if tracked_only:
    out = subprocess.run(['git', '-C', root, 'ls-files', '-z', '--', 'Assets'], capture_output=True).stdout.decode('utf-8', 'replace')
    tracked = {os.path.join(root, p) for p in out.split('\0') if p}
    sources = [s for s in sources if s in tracked]
sources = [s for s in sources if '/TextMesh Pro/Examples & Extras/' not in s]   # 패키지 예제(빌드에 안 들어감) — 원래부터 깨져 있다

missing = {}
for s in sources:
    try:
        text = open(s, encoding='utf-8', errors='replace').read()
    except OSError: continue
    for g in set(GUID.findall(text)):
        if g in known or BUILTIN.match(g): continue
        if s.endswith('.meta') and meta_guid(s) == g: continue
        missing.setdefault(g, []).append(os.path.relpath(s, root))

print(f'파일 {len(sources)}개 검사 · 없는 에셋 guid {len(missing)}개' + (' (git 추적 기준)' if tracked_only else ''))
for g, where in sorted(missing.items(), key=lambda kv: -len(kv[1])):
    print(f'  {g} ← {len(where)}곳: ' + ', '.join(where[:4]) + (' …' if len(where) > 4 else ''))
sys.exit(1 if missing else 0)
