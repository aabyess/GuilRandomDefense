"""뼈가 **자기가 움직이는 살**과 같은 자리에 있는지 잰다(2026-09-24). 정지 렌더로는 안 보이는 층이다.

왜 필요한가 — 안흔함_박준희(사이렌헤드)는 쉬는 자세 렌더가 멀쩡했는데 클립을 먹이자 유니티에서 ×44로 터졌다.
히든_석성례는 골반 뼈가 **바닥 1.17m 아래**인 채 판정을 통과해 왔다. 둘 다 「뼈와 살이 딴 자리」였다.

🔴 **자를 셋 쓴다. 하나로 합치지 말 것** — 서로 못 보는 걸 본다(2026-09-24 실측):
```
① 뼈 머리 ↔ 살 무게중심   그 뼈가 도는 **축**이 살에서 얼마나 떨어졌나
                          → 히든_석성례 145%로 잡음.  ⚠️ **긴 뼈는 원래 크게 나온다**(212종 중앙값 15.9%).
                             혼자 읽으면 안 된다 — 이 값만 보고 이호준을 병으로 단정했다가 정정했다.
② 뼈 선분 ↔ 살 무게중심   그 뼈가 **엉뚱한 살**을 물고 있나. 길이 편향이 없다
                          → 안흔함_이호준(중앙 31.1%)을 잡음. 정상 유닛은 0.5~0.9%, 201종 중앙 2.0% · 99분위 9.8%
                             ⚠️ 석성례는 **못 잡는다**(13%) — 뼈가 길어 선분이 가슴을 지나가기 때문
③ 뼈가 메시 경계 밖       축이 아예 몸 밖인가
                          → 석성례 65% · 초월_엄태웅_AD 26%로 잡음
```

⚠️ **「살이 붙었다」의 기준**(세 번 바뀌었다. 지금이 셋째다):
  정점 그룹이 있다 ❌ → 무게 > 0 ❌ → **그 모델 무게 중앙값의 1% 이상** ✅
  유니티 휴머노이드는 필수 15뼈를 **존재**로 요구하므로, 팔 없는 모델(외팔 샹크스·원추 로브)도 팔 뼈를 갖는다.
  그 뼈들은 몸 밖 허공에 있지만 가중치가 씨앗(무게합 0.003 = 중앙값의 0.002%)뿐이라 **아무것도 안 움직인다.**

⚠️ **어깨 ② 15~22%는 결함이 아니다 — 처리 방식의 성질이다**(2026-09-25, 적 스킨 배치에서 확정):
  바운티러시 pl_ 계열은 코트 어깨 사슬(`l/r_coat_shoulder`·`l/r_coat_01~04`·소매 `sode`)을
  `merge_bones`로 **Shoulder에 합친다.** 그러면 그 뼈가 물게 되는 살이 어깨에서 옷자락 쪽으로 넓게 퍼져
  무게중심이 뼈 선분에서 멀어진다. 류마·김만경·이시원·이재윤·인홍진·조도연이 **전부 15~22%**로 같았다.
  👉 **여러 종에서 같은 값이 나오면 그건 그 종의 값이 아니다.** 코트 입은 pl_ 유닛의 이 값은 안 판다.
  (Icosphere가 네 종에서 `x 1.902 · y 2.000`으로 똑같이 나온 것과 같은 규칙이다.)

⚠️ **이 자로 못 보는 것 — 자기 클립만 쓰는 Generic 유닛**(2026-09-24 안흔함_이호준):
  원본 자산의 결합 자세가 애초에 어긋나 있어도, **그 결합 자세로 만든 클립**을 재생하면 화면은 멀쩡하다
  (서로 맞기 때문이다). 실제로 이호준은 ② 31.1%인데 자기 Idle·Attack은 렌더로 봐도 정상이다.
  → 숫자가 크다고 바로 고치지 말고 **그 유닛이 어떤 클립을 쓰는지** 먼저 볼 것.
    공용 클립을 받는 사람형이면 진짜 문제이고, 자기 클립만 쓰면 안 드러난다.

쓰는 법:
  blender -b --factory-startup --python Tools/blender/bone_bind.py -- <유닛.fbx|원본.glb> [...]
"""

import os
import sys

import bpy
import numpy as np


def weighted_bones(ms, frac=0.01):
    """무게합이 그 모델 중앙값의 frac 이상인 뼈 이름(위 ⚠️ 참고)."""
    tot = {}
    for o in ms:
        idx = {g.index: g.name for g in o.vertex_groups}
        for v in o.data.vertices:
            for ge in v.groups:
                if ge.weight > 0:
                    tot[idx[ge.group]] = tot.get(idx[ge.group], 0.0) + ge.weight
    if not tot:
        return set(), {}
    med = float(np.median(list(tot.values())))
    return {k for k, v in tot.items() if v >= med * frac}, tot


def measure(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.lower().endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=path)
    else:
        bpy.ops.import_scene.fbx(filepath=path)
    arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    ms = [o for o in bpy.data.objects if o.type == "MESH" and len(o.data.vertices)]
    name = os.path.basename(path)
    if arm is None or not ms:
        print("%-28s 뼈대나 메시가 없다" % name)
        return
    V = np.concatenate([np.array([(o.matrix_world @ v.co)[:] for v in o.data.vertices]) for o in ms])
    H = float(V[:, 2].max() - V[:, 2].min())
    lo_m, hi_m = V.min(0), V.max(0)
    skin, _ = weighted_bones(ms)
    cent = {}
    for o in ms:
        idx = {g.index: g.name for g in o.vertex_groups}
        for v in o.data.vertices:
            q = np.array((o.matrix_world @ v.co)[:])
            for ge in v.groups:
                if ge.weight > 0.01 and idx[ge.group] in skin:
                    a = cent.setdefault(idx[ge.group], [0.0, np.zeros(3)])
                    a[0] += ge.weight
                    a[1] += ge.weight * q
    heads, segs, out = [], [], []
    for pb in arm.pose.bones:
        a = cent.get(pb.name)
        if not a or a[0] <= 0:
            continue
        c = a[1] / a[0]
        h = np.array((arm.matrix_world @ pb.head)[:])
        t = np.array((arm.matrix_world @ pb.tail)[:])
        ab = t - h
        L = float(np.dot(ab, ab))
        u = 0.0 if L < 1e-12 else max(0.0, min(1.0, float(np.dot(c - h, ab) / L)))
        heads.append((float(np.linalg.norm(h - c)) / H, pb.name))
        segs.append((float(np.linalg.norm(c - (h + u * ab))) / H, pb.name))
        d = max((lo_m - h).max(), (h - hi_m).max()) / H
        if d > 0:
            out.append((d, pb.name))
    hm, sm = max(heads), max(segs)
    om = max(out) if out else (0.0, "-")
    print("%-28s 키 %.3f · 살 붙은 뼈 %d/%d" % (name, H, len(skin), len(arm.pose.bones)))
    print("   ① 머리 거리  최대 %5.1f%% (%s) · 중앙 %4.1f%%   ⚠️ 긴 뼈 편향 있음(212종 중앙 15.9%%)"
          % (hm[0] * 100, hm[1], 100 * float(np.median([d for d, _ in heads]))))
    print("   ② 선분 거리  최대 %5.1f%% (%s) · 중앙 %4.1f%%   정상 0.5~0.9%% · 99분위 9.8%%"
          % (sm[0] * 100, sm[1], 100 * float(np.median([d for d, _ in segs]))))
    print("   ③ 메시 밖    최대 %5.1f%% (%s)                 5%% 넘으면 볼 것"
          % (om[0] * 100, om[1]))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    assert args, "잴 파일을 하나 이상 주세요"
    for a in args:
        measure(os.path.expanduser(a))


main()
