"""움직일 때 **팔·손이 몸통을 뚫는지** 잰다(2026-09-24, PM 승인). 경계 상자로는 원리적으로 안 보이는 층이다.

앞서 만든 자들(clip_check·clip_table·clip_spread·bone_bind)은 **경계 상자와 뼈 위치**만 본다.
그래서 「팔이 몸을 관통한다」는 표에 안 나온다 — 없어서가 아니라 **안 본 것**이었다. 이 자가 그 자리를 본다.

재는 법
  ① 공용 Idle을 옮겨 입힌다(retarget_idle.py — 세계 변화량 방식).
  ② **몸통 면**으로 BVH를 짓는다: 정점의 우세 가중치 뼈가 Hips·Spine·Spine1·Spine2·Neck인 면.
  ③ **손·아래팔 정점**(우세 뼈가 ForeArm·Hand·손가락)마다 **광선이 몸통 껍데기를 몇 번 지나는지 센다**
     (홀수면 안쪽). 열린 껍데기에 안 속도록 **세 방향으로 쏴 다수결**. 깊이는 가장 가까운 면까지의 거리다.
     ⚠️ 1차에는 「가장 가까운 면의 법선」으로 안팎을 봤는데 **크게 틀렸다** — T자 손오공이 「32% 파고듦」으로 나왔다
     (손이 몸에서 50cm 떨어져 있어도 가장 가까운 면 하나의 방향이 엉뚱하면 안쪽으로 읽힌다).
  ④ 깊이를 키로 나누고, **쉬는 자세에서 이미 겹친 만큼을 뺀다**(증가분만 본다).
  ⑤ 대조군(랜덤_한마_바키·랜덤_손오공)을 같은 판에서 같이 잰다 — 새 지표는 혼자서는 못 읽는다.

🔴 **이 자로 못 보는 것**(만들면서 아는 사각지대. 나중에 찾는 것보다 훨씬 싸다)
  1. **몸통이 열린 껍데기면 안팎 판정이 샌다.** 자켓처럼 아래가 뚫린 메시는 광선이 그냥 빠져나가
     홀짝이 틀린다. 세 방향 다수결로 많이 줄였지만 **없앤 건 아니다.**
  2. **노멀이 뒤집힌 면이 섞이면 안팎이 통째로 뒤집힌다.** gen_objrip_skin 산물에서 그런 일이 있었다.
  3. **원래 몸에 닿은 손**(팔짱·허리에 손)은 쉬는 자세에서도 겹친다 → ④의 증가분으로 걸러지지만,
     클립이 손을 더 밀어 넣으면 정상인데도 잡힌다. **숫자가 크면 렌더로 볼 것.**
  4. 🔴 **쏘는 쪽에서만 옷을 뺐지, 맞는 쪽에는 옷이 들어 있다.** 손·아래팔 정점만 쏘지만
     **몸통 BVH는 척추 가중치 면 전부**라 코트·망토·판초가 거기 들어간다.
     그래서 **「손이 옷 안으로 들어간 것」과 「팔이 가슴을 뚫은 것」을 이 자는 못 가른다.**
     2026-09-24 전수에서 상위권이 전부 그랬다 — 희귀함_서민성(+19.0%)은 통 넓은 판초에 손이 들어간 것이고,
     히든_성탄(+14.7%)은 짧은 팔이 둥근 만두 배에 들어간 것이다. **둘 다 결함이 아니다.**
     가르려면 몸과 옷을 갈라야 하는데 가중치로는 못 가른다(둘 다 척추에 실린다). **재질 이름으로도 못 믿는다.**
     → 숫자가 크면 **반드시 렌더로 볼 것.** 이 자만으로는 판정하지 말 것.
  4-b. 소매가 몸을 뚫는 것은 안 걸린다(쏘는 쪽이 손·아래팔뿐이라).
  5. **Generic·소품·매핑 못 한 유닛은 대상이 아니다**(공용 클립을 안 받는다).
     ⚠️ 그런데 **이름이 mixamorig면 매핑이 되어 버려서 잰다** — 영원_김영원이 그랬다.
     읽을 때 `UnitModelPostprocessor.GenericRigUnits`에 있는 이름은 **빼고 볼 것.**
  6. ⚠️ **팔을 내린 Idle에서는 손이 엉덩이·허벅지에 닿는 게 정상이다.** 대조군도 +2~5%가 나온다 —
     **문턱은 분포를 보고 정할 것.** 숫자 하나로 「관통」이라 부르지 말 것.

쓰는 법:
  blender -b --factory-startup --python Tools/blender/mesh_clash.py -- <유닛.fbx> [...]
  인자를 하나만 주면 대조군 둘을 자동으로 같이 잰다.
"""

import os
import sys

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import retarget_idle as R                                                  # noqa: E402

PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
CONTROLS = ["랜덤_한마_바키", "랜덤_손오공"]
FRACS = (0.0, 0.25, 0.5, 0.75, 1.0)
TORSO = ("Hips", "Spine", "Spine1", "Spine2", "Chest", "Neck")
ARMY = ("ForeArm", "Hand")                                                 # 손가락 이름도 Hand로 시작한다
LIMIT = 0.03                                                               # 키의 3% 넘게 더 파고들면 본다


def dominant(o):
    """정점마다 우세 가중치 뼈 이름."""
    idx = {g.index: g.name.split(":")[-1] for g in o.vertex_groups}
    out = []
    for v in o.data.vertices:
        best, bw = None, 0.0
        for ge in v.groups:
            if ge.weight > bw:
                best, bw = idx.get(ge.group), ge.weight
        out.append(best)
    return out


def shells(ms):
    """(몸통 면을 가진 메시, 그 면 인덱스) · (손·아래팔 정점을 가진 메시, 그 정점 인덱스)."""
    torso, arms = [], []
    for o in ms:
        dom = dominant(o)
        tf = [p.index for p in o.data.polygons
              if sum(1 for vi in p.vertices if dom[vi] in TORSO) * 2 >= len(p.vertices)]
        av = [i for i, d in enumerate(dom) if d and any(d.endswith(a) or a in d for a in ARMY)]
        if tf:
            torso.append((o, tf))
        if av:
            arms.append((o, av))
    return torso, arms


def deepest(torso, arms, dg):
    """지금 자세에서 손·아래팔 정점이 몸통 안으로 파고든 **최대 깊이**(미터)."""
    verts, polys, base = [], [], 0
    for o, tf in torso:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        vs = [ev.matrix_world @ v.co for v in me.vertices]
        verts.extend(vs)
        for fi in tf:
            polys.append([vi + base for vi in me.polygons[fi].vertices])
        base += len(vs)
        ev.to_mesh_clear()
    if len(polys) < 4:
        return None
    bvh = BVHTree.FromPolygons(verts, polys, all_triangles=False, epsilon=0.0)

    def inside(pt):
        """🔴 「가장 가까운 면의 법선」으로 안팎을 보면 **크게 틀린다**(1차 시험: T자 손오공이 「32% 파고듦」).
        손이 몸에서 50cm 떨어져 있어도 가장 가까운 면 하나의 방향이 엉뚱하면 안쪽으로 읽힌다.
        → **광선이 껍데기를 몇 번 지나는지 센다**(홀수면 안). 열린 껍데기에 속지 않도록 **세 방향**으로 쏴 다수결."""
        votes = 0
        for d in (Vector((1.0, 0.03, 0.02)), Vector((0.02, 1.0, 0.03)), Vector((0.03, 0.02, 1.0))):
            d.normalize()
            n, o2 = 0, pt.copy()
            for _ in range(24):
                hit = bvh.ray_cast(o2, d)
                if hit[0] is None:
                    break
                n += 1
                o2 = hit[0] + d * 1e-4
            votes += n % 2
        return votes >= 2

    worst = 0.0
    for o, av in arms:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        for vi in av[::3]:                                                 # 3개마다 하나씩 — 관통은 면 단위라 충분하다
            if vi >= len(me.vertices):
                continue
            p = ev.matrix_world @ me.vertices[vi].co
            hit = bvh.find_nearest(p)
            if hit[0] is None or hit[3] is None:
                continue
            if float(hit[3]) > worst and inside(p):
                worst = float(hit[3])
        ev.to_mesh_clear()
    return worst


def measure(fbx, idles):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    scn = bpy.context.scene
    arm = next((o for o in scn.objects if o.type == "ARMATURE"), None)
    ms = [o for o in scn.objects if o.type == "MESH" and len(o.data.vertices)]
    name = os.path.splitext(os.path.basename(fbx))[0]
    mapping = R.bone_map(arm, idles) if arm else None
    if mapping is None or not ms:
        print("%-22s 공용 Idle을 못 입힌다(Generic·소품) — 이 자의 대상이 아니다" % name)
        return
    V = np.concatenate([np.array([(o.matrix_world @ v.co)[:] for v in o.data.vertices]) for o in ms])
    H = float(V[:, 2].max() - V[:, 2].min())
    torso, arms = shells(ms)
    if not torso or not arms:
        print("%-22s 몸통/팔 정점을 못 갈랐다(가중치 이름 확인)" % name)
        return
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()
    rest = deepest(torso, arms, bpy.context.evaluated_depsgraph_get()) or 0.0
    worst, wf = 0.0, None
    for idle in idles:
        for pb in arm.pose.bones:
            pb.matrix_basis.identity()
        bpy.context.view_layer.update()
        R.apply_idle(arm, idle, mapping)
        d = deepest(torso, arms, bpy.context.evaluated_depsgraph_get()) or 0.0
        if d > worst:
            worst, wf = d, idle["frame"]
    inc = (worst - rest) / H
    flag = "  🔴 렌더로 볼 것" if inc > LIMIT else ""
    print("%-22s 키 %.2f · 쉬는 자세 %4.1f%% → 움직일 때 %4.1f%% · **증가분 %+5.1f%%**(프레임 %s)%s"
          % (name, H, 100 * rest / H, 100 * worst / H, 100 * inc, wf, flag))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    assert args, "잴 FBX를 하나 이상 주세요"
    targets = list(args)
    if len(args) == 1:
        targets += [os.path.join(PROJECT, "Assets/Art/Units", n, n + ".fbx") for n in CONTROLS]
    # 🔴 공용 클립 **셋 다** 돌린다 — idle만 보면 팔이 몸을 가로지르는 동작을 못 본다(그건 걷기·공격에서 난다)
    print("문턱: 쉬는 자세 대비 **증가분**이 키의 %.0f%% 넘으면 표시(절대량이 아니라 증가분이다)" % (LIMIT * 100))
    for clip in ("idle", "walk", "attack"):
        idles = R.load_idle(frames=FRACS, clip=clip)
        if not idles:
            print("[%s] 공용 클립이 없다 — 건너뜀" % clip)
            continue
        print("── 공용 %s" % clip)
        for t in targets:
            measure(t, idles)


main()
