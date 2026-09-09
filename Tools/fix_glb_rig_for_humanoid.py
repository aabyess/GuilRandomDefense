"""IK 컨트롤 리그가 든 .glb를 유니티 Humanoid가 읽을 수 있게 고친다.

쓰는 법: SRC/DST와 아래 MOVES·RENAME 표를 이 모델에 맞게 고치고 돌린다.
        먼저 노드 이름과 계층을 눈으로 확인할 것 — 표를 추측으로 채우면 안 된다.

────────────────────────────────────────────────────────────────────
아래는 2026-09-09 luffy.glb(특별함_최상호)에 실제로 쓴 값이다.

원래 설명: luffy.glb의 IK 컨트롤 리그를 유니티 Humanoid가 읽을 수 있는 형태로 바꾼다.

원본 구조의 문제 (실측):
  다리  root hips → pelvis → thigh.L → knee.L        (여기서 끊긴다)
        root hips → ankle.L.004 → .003 → .002 → .001 → toes.L   ← 발이 별도 IK 사슬
  팔    shoulder1 → shoulder2 → elbow                (여기서 끊긴다)
        shoulder1 → arm.ctrl → wrist → 손가락        ← 손이 별도 컨트롤 밑

유니티 Humanoid는 발이 무릎의 자손, 손이 팔꿈치의 자손이어야 한다. 구조가 안 맞으면
이름을 아무리 고쳐도 아바타가 안 만들어진다.

🔧 노드를 옮기되 **월드 변환을 보존**한다: newLocal = inverse(newParentGlobal) × oldGlobal.
   glTF는 노드를 번호로 참조하고 스키닝은 globalTransform × inverseBindMatrix로 계산하므로,
   월드 변환만 같으면 메시는 한 점도 안 움직인다. 애니메이션(눈깜빡임)은 눈꺼풀만
   건드리므로 옮기는 노드와 겹치지 않는다.
"""
import json, struct, sys

SRC = "/Users/sang/Downloads/luffy.glb"
DST = "/tmp/luffy_probe/luffy_humanoid.glb"

def load(p):
    b = open(p, 'rb').read()
    off, js, bin_ = 12, None, None
    while off < len(b):
        ln, ty = struct.unpack_from('<II', b, off); off += 8
        chunk = b[off:off+ln]
        if ty == 0x4E4F534A: js = json.loads(chunk.decode('utf-8'))
        elif ty == 0x004E4942: bin_ = chunk
        off += ln
    return js, bin_

def ident(): return [[1.0 if i == j else 0.0 for j in range(4)] for i in range(4)]
def mm(a, b): return [[sum(a[i][k]*b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

def local(n):
    if 'matrix' in n:
        m = n['matrix']; return [[m[c*4+r] for c in range(4)] for r in range(4)]
    M = ident()
    if 'rotation' in n:
        x, y, z, w = n['rotation']
        M = [[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w), 0],
             [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w), 0],
             [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y), 0],
             [0, 0, 0, 1]]
    if 'scale' in n:
        s = n['scale']
        M = mm(M, [[s[0],0,0,0],[0,s[1],0,0],[0,0,s[2],0],[0,0,0,1]])
    if 'translation' in n:
        t = n['translation']; T = ident(); T[0][3], T[1][3], T[2][3] = t
        M = mm(T, M)
    return M

def inv(m):
    """일반 4x4 역행렬 — 가우스 소거. 리그에 비균등 스케일이 있어도 정확하다."""
    a = [row[:] + ident()[i][:] for i, row in enumerate(m)]
    for c in range(4):
        p = max(range(c, 4), key=lambda r: abs(a[r][c]))
        if abs(a[p][c]) < 1e-12: raise ValueError("역행렬 없음")
        a[c], a[p] = a[p], a[c]
        d = a[c][c]
        a[c] = [v / d for v in a[c]]
        for r in range(4):
            if r == c: continue
            f = a[r][c]
            if f: a[r] = [v - f*w for v, w in zip(a[r], a[c])]
    return [row[4:] for row in a]

js, bin_ = load(SRC)
nodes = js['nodes']; names = [n.get('name', '') for n in nodes]
kids = {i: n.get('children', []) for i, n in enumerate(nodes)}
byname = {n: i for i, n in enumerate(names)}
parent = {}
for i, ch in kids.items():
    for c in ch: parent[c] = i

glob = {}
def walk(i, M):
    m = mm(M, local(nodes[i])); glob[i] = m
    for c in kids.get(i, []): walk(c, m)
for s in js['scenes'][js.get('scene', 0)]['nodes']: walk(s, ident())

# ── ① 계층 교정 ────────────────────────────────────────────────
MOVES = [
    ('leg *side* ankle.L.001_434', 'leg *side* knee.L_11'),
    ('leg *side* ankle.R.001_445', 'leg *side* knee.R_29'),
    ('arm *side* wrist.L_243',     'arm *side* elbow.L_220'),
    ('arm *side* wrist.R_271',     'arm *side* elbow.R_248'),
]
for child, newparent in MOVES:
    if child not in byname or newparent not in byname:
        print(f"  🔴 못 찾음: {child} 또는 {newparent}"); sys.exit(1)
    ci, pi = byname[child], byname[newparent]
    old = parent.get(ci)
    if old is not None:
        nodes[old]['children'] = [c for c in nodes[old].get('children', []) if c != ci]
        if not nodes[old]['children']: nodes[old].pop('children')
    nodes[pi].setdefault('children', []).append(ci)
    newlocal = mm(inv(glob[pi]), glob[ci])
    for k in ('translation', 'rotation', 'scale', 'matrix'): nodes[ci].pop(k, None)
    nodes[ci]['matrix'] = [newlocal[r][c] for c in range(4) for r in range(4)]
    print(f"  옮김: {child}  →  {newparent} 밑")

# ── ② 이름을 유니티 규약으로 ───────────────────────────────────
RENAME = {
    'root hips_429': 'Hips',
    'spine lower_428': 'Spine', 'spine middle_427': 'Chest', 'spine upper_426': 'UpperChest',
    'head neck lower_217': 'Neck', 'head neck upper_216': 'Head',
    'arm *side* shoulder 1.L_245': 'LeftShoulder', 'arm *side* shoulder 2.L_224': 'LeftUpperArm',
    'arm *side* elbow.L_220': 'LeftLowerArm', 'arm *side* wrist.L_243': 'LeftHand',
    'arm *side* shoulder 1.R_273': 'RightShoulder', 'arm *side* shoulder 2.R_252': 'RightUpperArm',
    'arm *side* elbow.R_248': 'RightLowerArm', 'arm *side* wrist.R_271': 'RightHand',
    'leg *side* thigh.L_19': 'LeftUpperLeg', 'leg *side* knee.L_11': 'LeftLowerLeg',
    'leg *side* ankle.L.001_434': 'LeftFoot', 'leg *side* toes.L_433': 'LeftToes',
    'leg *side* thigh.R_37': 'RightUpperLeg', 'leg *side* knee.R_29': 'RightLowerLeg',
    'leg *side* ankle.R.001_445': 'RightFoot', 'leg *side* toes.R_444': 'RightToes',
}
missing = [k for k in RENAME if k not in byname]
if missing:
    print("  🔴 이름을 못 찾음:", missing); sys.exit(1)
for old, new in RENAME.items(): nodes[byname[old]]['name'] = new
print(f"  이름 {len(RENAME)}개 변경")

# ── 저장 ────────────────────────────────────────────────────────
jb = json.dumps(js, separators=(',', ':')).encode('utf-8')
jb += b' ' * ((4 - len(jb) % 4) % 4)
bb = bin_ + b'\x00' * ((4 - len(bin_) % 4) % 4)
out = b'glTF' + struct.pack('<II', 2, 12 + 8 + len(jb) + 8 + len(bb))
out += struct.pack('<II', len(jb), 0x4E4F534A) + jb
out += struct.pack('<II', len(bb), 0x004E4942) + bb
open(DST, 'wb').write(out)
print(f"  저장: {DST}  ({len(out)/1e6:.1f} MB)")
