"""유닛 스킨을 등급별 판으로 전시한다(사장님 Blender 창용 + 헤드리스 저장·렌더).

사장님 지시(2026-09-23, PM 경유): 「등급별 전시판을 만들어 띄워 줘 — 이번에 끝난 등급(초월·불멸·
영원·히든)을 포함해서」. 09-17~18에 등급마다 따로 만들어 두던 ~/Desktop/구랜디스킨모음/전시판/
판_유닛_<등급>.blend를 이 파일 하나로 다시 지을 수 있게 정리했다(그때는 스크립트가 저장소에
안 남았다 — 그래서 매번 새로 짰다. 이제 여기가 정본).

🔴 커밋된 FBX(Assets/Art/Units/<유닛>/<유닛>.fbx)를 **그대로** 들여올 뿐 새로 짓지 않는다.
🔴 크기도 실제 그대로다(비교가 이 전시의 목적) — FBX는 게임 단위로 읽히므로 1/11.4만 곱한다.
🔴 이름표는 로마자(한글은 블렌더 기본 폰트에서 깨진다) — 유닛 이름을 국어의 로마자 표기법으로
   옮기고, 흔한 성씨는 통용 표기(김=Kim, 이=Lee, 최=Choi …)를 쓴다. AD/AP/ADAP 꼬리는 그대로.

쓰는 법:
  헤드리스(판 저장 + 정면 렌더):
    blender -b --factory-startup --python Tools/blender/show_units.py -- [등급 ...] [--save DIR] [--render DIR]
      등급을 안 주면 열 등급 전부를 한 장면에 등급 순서로 깐다.
  창에서(MCP):
    ns = {"__name__": "show_units", "__file__": ".../Tools/blender/show_units.py"}
    exec(compile(open(ns["__file__"], encoding="utf-8").read(), ns["__file__"], "exec"), ns)
    boards = ns["show_units"]()        # {"판_유닛_히든": board, ...}
"""

import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
UNIT_DIR = os.path.join(PROJECT, "Assets", "Art", "Units")
PER_ROW = 8                      # 한 줄에 여덟 — 25종짜리 초월이 가로로 너무 길어지지 않게
IDLE = os.path.expanduser("~/Desktop/구랜디스킨모음/99_공용_애니메이션/idle.fbx")

# 🔴 사장님 지시(2026-09-23): 「**서있는 모습으로** 배치」 — 쉬는 자세(T자) 그대로 세우면 안 된다.
#   게임 안 조합판 인형이 공용 Idle을 먹고 자연스럽게 서 있으므로, 판도 같은 클립의 한 프레임으로 세운다.
#   프레임은 **클립에서 가장 얌전한 자세**를 자동으로 고른다(뼈 회전량 합이 가장 작은 프레임) — 손을 든
#   순간을 잡으면 비교가 안 되기 때문이다. 리타겟이 안 먹는 유닛(Generic·네발·소품)은 쉬는 자세 그대로 둔다.
HUMAN_MIN = ["Hips", "Spine", "Head", "LeftArm", "LeftForeArm", "LeftHand",
             "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot",
             "RightUpLeg", "RightLeg", "RightFoot"]

# 🔴 전시용으로만 줄이는 것(실측치는 결함 목록에 그대로 남긴다) — 배 둘이 22m라 다른 판을 전부 덮는다.
DISPLAY_SCALE = {"해적선": 1.8 / 22.0, "고대의배": 1.8 / 22.0}

# 🔴 판에서 빼는 유닛(2026-09-23 blender 전수 실측 — 커밋된 FBX를 전부 재수입해 세계 좌표로 잼).
#   유닛 규약은 「키(z) 1.8m」인데 이들은 자릿수가 다르다 — 한 판에 같이 놓으면 칸 너비가 이들 기준으로
#   잡혀 나머지가 점으로 보인다(1차 렌더에서 실제로 그랬다). 판에서만 빼고, 목록은 리포트로 올린다.
#   ⚠️ 이건 전시 문제가 아니라 **유닛 자체의 결함**이다 — 게임 안에서도 ArtBinder가 키로 맞추면서
#   말도 안 되는 배율이 나온다(PM이 유니티에서 본 희귀함_김정래 0.16배와 같은 부류).
SKIP = {
    # 키가 자릿수로 어긋나 판에 같이 놓으면 칸 너비가 이들 기준이 되어 나머지가 점으로 보인다(2026-09-23 재실측).
    "안흔함_김용태": "키 22.99m(규약 1.8m의 12.8배)",
    "안흔함_엄태웅": "키 18.83m(10.5배)",
    "안흔함_이재윤": "키 4.47m(2.5배)",
    "흔함_임장혁": "키 0.113m(1/16)",
    "흔함_박민수": "키 0.027m(1/67)",
    "흔함_양재모": "키 0.024m(1/75)",
    "특별함_임장혁": "키 0.022m(1/82)",
    # ※ 안흔함_박준희는 09-23 재출력으로 1.8m가 되어 목록에서 빠졌다.
    # ※ 소품 유닛(특별함_김정래 노트북 · 히든_호치킨/이삭토스트/감탄떡볶이)은 「가장 긴 변 0.6m」가 규약이라 정상이다 — 안 뺀다.
}

# 등급 순서(흔함 → … → 히든)와 판 제목. 폴더 접두어가 곧 등급이다.
GRADES = [
    ("흔함", "COMMON"), ("안흔함", "UNCOMMON"), ("특별함", "SPECIAL"), ("희귀함", "RARE"),
    ("전설적인", "LEGENDARY"), ("제한", "LIMITED"),
    ("초월", "TRANSCENDENT"), ("불멸", "IMMORTAL"), ("영원", "ETERNAL"), ("히든", "HIDDEN"),
    ("랜덤", "RANDOM"), ("기타", "OTHER"),          # 기타 = 등급 접두어가 없는 폴더(해적선·고대의배 등)
]
PREFIXES = [g for g, _ in GRADES if g != "기타"]      # 「기타」 판정에 쓴다

# ── 로마자 표기(국어의 로마자 표기법 — 자음동화 같은 뒤섞임 규칙은 안 넣는다. 이름표용이라 이걸로 충분).
CHO = ["g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h"]
JUNG = ["a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i"]
JONG = ["", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m", "p", "t", "t", "p", "l", "m", "p", "p", "t", "t", "ng", "t", "t", "k", "t", "p", "t"]
# 흔한 성씨는 사람들이 실제로 쓰는 표기로(로마자 표기법대로면 Gim·I·Choe가 되어 알아보기 어렵다).
SURNAME = {
    "김": "Kim", "이": "Lee", "박": "Park", "최": "Choi", "정": "Jung", "강": "Kang", "조": "Cho",
    "윤": "Yoon", "장": "Jang", "임": "Lim", "한": "Han", "신": "Shin", "서": "Seo", "권": "Kwon",
    "황": "Hwang", "안": "Ahn", "송": "Song", "류": "Ryu", "전": "Jeon", "홍": "Hong", "고": "Ko",
    "문": "Moon", "손": "Son", "양": "Yang", "배": "Bae", "백": "Baek", "허": "Heo", "유": "Yoo",
    "남": "Nam", "심": "Shim", "노": "Noh", "하": "Ha", "곽": "Kwak", "성": "Sung", "차": "Cha",
    "주": "Joo", "우": "Woo", "구": "Koo", "민": "Min", "지": "Ji", "엄": "Eom", "채": "Chae",
    "원": "Won", "천": "Cheon", "방": "Bang", "공": "Kong", "현": "Hyun", "함": "Ham", "변": "Byun",
    "염": "Yeom", "여": "Yeo", "추": "Chu", "도": "Do", "석": "Seok", "선": "Sun", "설": "Seol",
    "명": "Myung", "기": "Ki", "진": "Jin", "태": "Tae", "은": "Eun", "용": "Yong", "두": "Doo",
}


def romanize(word):
    """한글 낱말 하나를 로마자로. 한글이 아닌 글자는 그대로 둔다(AD·AP 같은 꼬리)."""
    out = []
    for ch in word:
        code = ord(ch) - 0xAC00
        if 0 <= code < 11172:
            out.append(CHO[code // 588] + JUNG[(code % 588) // 28] + JONG[code % 28])
        else:
            out.append(ch)
    return "".join(out).capitalize() if out else ""


def label_of(unit):
    """유닛 폴더 이름 → 이름표. 「등급_이름[_꼬리]」에서 등급을 떼고, 이름은 성+이름으로 띄운다."""
    parts = unit.split("_")[1:] if any(unit.startswith(g + "_") for g in PREFIXES) else [unit]   # 기타 판(해적선·고대의배)은 이름 전체가 이름이다
    words = []
    for p in parts:
        if not p or ord(p[0]) < 0xAC00:                      # AD·AP·ADAP 같은 영문 꼬리
            words.append(p)
        elif len(p) >= 2 and p[0] in SURNAME:
            words += [SURNAME[p[0]], romanize(p[1:])]
        else:
            words.append(romanize(p))
    return " ".join(w for w in words if w)


def import_unit(collection, showcase, path, idle=None):
    """유닛 FBX 하나를 들여와 메시만 남기고 무리로 돌려준다.

    🔴 showcase.import_fbx를 안 쓰는 이유 둘(2026-09-23 실측):
      ① 그 함수는 1/11.4(게임 단위 → 미터)를 곱하는데, **유닛 FBX는 이미 미터다**(키 1.8로 들어온다 —
         영원 여섯·히든 둘·초월·불멸을 재 봐서 전부 1.800 확인). 곱하면 15cm짜리가 된다.
      ② 그 함수는 뼈대 오브젝트를 먼저 지우고 메시만 남기는데, 메시가 뼈대의 자식이라 부모를 지우는
         순간 세계 변환(뼈대가 쥐고 있던 배율·회전)이 날아간다 — 영원 여섯 중 김영원만 12배로 커 보인
         1차 렌더의 원인이었다. 여기서는 **부모를 지우기 전에 matrix_world를 제 자리에 구워** 넣는다.
    """
    from mathutils import Matrix
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        for owner in list(o.users_collection):
            owner.objects.unlink(o)
        collection.objects.link(o)
    bpy.context.view_layer.update()
    meshes = [o for o in new if o.type == "MESH" and len(o.data.vertices)]
    arm = next((o for o in new if o.type == "ARMATURE"), None)
    posed = False
    if idle is not None and arm is not None:
        names = {b.name.split(":")[-1] for b in arm.pose.bones}
        if all(h in names for h in HUMAN_MIN) and any(b.name in idle["delta"] for b in arm.pose.bones):
            apply_idle(arm, idle)                       # 사람 필수 뼈가 다 있을 때만 옮겨 입힌다
            posed = True
    dg = bpy.context.evaluated_depsgraph_get()
    world = {o: o.matrix_world.copy() for o in meshes}
    baked = {}
    if posed:                                           # 포즈가 들어간 모양을 정점에 굳힌다(뼈대는 곧 지운다)
        for o in meshes:
            baked[o] = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    for o in new:
        if o not in meshes:                             # 뼈대·빈 껍데기(정점 0짜리 Cube 따위)는 전시에서 뺀다
            bpy.data.objects.remove(o, do_unlink=True)
    scale = DISPLAY_SCALE.get(os.path.splitext(os.path.basename(path))[0], 1.0)
    for o in meshes:
        if o in baked:
            old_me = o.data
            o.data = baked[o]
            o.data.name = old_me.name
        for md in [m for m in o.modifiers if m.type == "ARMATURE"]:
            o.modifiers.remove(md)
        o.parent = None
        o.matrix_basis = Matrix.Identity(4)
        M = world[o]
        if scale != 1.0:
            M = Matrix.Scale(scale, 4) @ M              # 전시용 축소(배 둘) — 실측치는 결함 목록에 그대로 남긴다
        o.data.transform(M)                             # 배율·회전을 메시에 구워 둔다(자리는 lay_out이 잡는다)
        showcase.relink_textures(o, os.path.join(os.path.dirname(path), "Textures"))   # 유닛은 <유닛>/Textures/ (자연물과 층이 다르다)
    return meshes, posed


def load_idle():
    """공용 idle.fbx에서 **가장 얌전한 프레임**의 자세를 읽어 둔다(뼈 회전량 합이 최소인 프레임).

    🔴 뼈마다 「쿼터니언을 그대로 복사」하면 안 된다(1차에서 팔이 만세를 했다) — pose_bone.rotation_quaternion은
    **그 뼈의 쉬는 방향(roll) 기준 로컬 회전**이라, 원본과 대상 리그의 뼈 방향이 다르면 엉뚱하게 돈다.
    우리 유닛은 파이프라인마다 뼈 방향이 제각각이다. 그래서 **세계 좌표의 변화량**만 옮긴다:
        Rd = (자세 때 세계 회전) × (쉬는 때 세계 회전)⁻¹
    대상에서는 그 Rd를 제 쉬는 방향에 곱해 세계 회전을 만든다 — 리그가 달라도 같은 자세가 나온다.
    파일이 없으면 None(그러면 판은 쉬는 자세 그대로).
    """
    if not os.path.exists(IDLE):
        return None
    from mathutils import Matrix
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=IDLE)
    new_objs = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new_objs if o.type == "ARMATURE")
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    scn = bpy.context.scene
    best, best_fr = None, f0
    for fr in range(f0, f1 + 1, 5):
        scn.frame_set(fr)
        tot = sum(abs(pb.matrix_basis.to_quaternion().angle) for pb in arm.pose.bones)
        if best is None or tot < best:
            best, best_fr = tot, fr
    scn.frame_set(best_fr)
    W = arm.matrix_world
    delta, hips_off = {}, None
    for pb in arm.pose.bones:
        rest_w = (W @ pb.bone.matrix_local).to_3x3()
        pose_w = (W @ pb.matrix).to_3x3()
        delta[pb.name] = pose_w @ rest_w.inverted()
        if pb.name.endswith("Hips"):
            hips_off = (W @ pb.matrix).translation - (W @ pb.bone.matrix_local).translation
    for o in new_objs:
        bpy.data.objects.remove(o, do_unlink=True)
    return {"delta": delta, "hips_off": hips_off, "frame": best_fr,
            "회전량": round(float(best), 3), "뼈": len(delta)}


def apply_idle(arm, idle):
    """load_idle이 읽어 둔 세계 변화량을 이 뼈대에 입힌다(부모부터 차례로 — 자식 머리 자리가 부모 자세에 딸려 온다)."""
    from mathutils import Matrix
    W = arm.matrix_world
    Wi = W.inverted()
    order = sorted(arm.pose.bones, key=lambda pb: len(pb.parent_recursive))
    for pb in order:
        Rd = idle["delta"].get(pb.name)
        if Rd is None:
            continue
        rest_w = W @ pb.bone.matrix_local
        rot_w = (Rd @ rest_w.to_3x3()).to_4x4()
        if pb.parent is not None:
            p_rest_w = W @ pb.parent.bone.matrix_local
            head_w = (W @ pb.parent.matrix) @ p_rest_w.inverted() @ rest_w.translation
        else:
            head_w = rest_w.translation + (idle["hips_off"] or Vector((0, 0, 0)))
        pb.matrix = Wi @ (Matrix.Translation(head_w) @ rot_w)
        bpy.context.view_layer.update()


def units_of(grade):
    def ok(n):
        return n not in SKIP and os.path.isfile(os.path.join(UNIT_DIR, n, n + ".fbx"))
    if grade == "기타":                                # 등급 접두어가 없는 폴더(해적선·고대의배 등)
        return sorted(n for n in os.listdir(UNIT_DIR)
                      if ok(n) and not any(n.startswith(g + "_") for g in PREFIXES))
    return sorted(n for n in os.listdir(UNIT_DIR) if n.startswith(grade + "_") and ok(n))


def show_units(grades=None):
    import importlib
    import showcase
    importlib.reload(showcase)

    idle = load_idle()
    picked = [(g, t) for g, t in GRADES if grades is None or g in grades]
    boards, report = {}, {}
    rest_only = []
    for grade, title in picked:
        names = units_of(grade)
        col_name = "판_유닛_" + grade
        col = showcase.collection(col_name)
        rows = []
        for start in range(0, len(names), PER_ROW):
            groups = []
            for unit in names[start:start + PER_ROW]:
                meshes, posed = import_unit(col, showcase, os.path.join(UNIT_DIR, unit, unit + ".fbx"), idle)
                assert meshes, f"{unit}: 정점 있는 메시가 없다"
                meshes[0].name = unit
                if not posed:
                    rest_only.append(unit)
                groups.append(showcase.group(label_of(unit), [(o, 0.0, 0.0) for o in meshes]))
            rows.append((f"{grade} {start + 1}~{start + len(groups)}", groups))
        boards[col_name] = showcase.lay_out(col, rows, title=title)
        report[col_name] = [label_of(n) for n in names]
    # 등급 순서가 보이게 한 줄에 넷씩(앞줄이 낮은 등급) — 판이 커서 한 줄에 다 놓으면 가로 수백 m가 된다.
    order = ["판_유닛_" + g for g, _ in picked]
    showcase.arrange([[boards[n] for n in order[i:i + 4]] for i in range(0, len(order), 4)], corridor=1.2)
    showcase.frame_all(list(boards.values()))
    report["__Idle__"] = idle and {k: v for k, v in idle.items() if k not in ("delta", "hips_off")}
    report["__쉬는자세로만 세운 유닛__"] = rest_only
    return boards, report


def front_render(boards, path, width=2600, height=1600, tilt=42.0):
    """판 전체를 정면(−Y)에서 내려다본 한 장 — 사장님 창을 못 쓸 때(MCP 끊김) 대신 보내는 그림.
    🔴 완전 정면(수평)으로 찍으면 **이름표가 안 보인다** — 이름표·제목은 판 위에 누워 있어서 정면에선
    모서리만 보인다. showcase.frame_all과 같은 42° 내려다보기를 쓴다."""
    import numpy as np
    from mathutils import Vector
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.world = bpy.data.worlds.new("전시") if scene.world is None else scene.world
    scene.world.color = (0.45, 0.45, 0.48)
    pts = []
    for board in boards.values():
        for obj in board.users_collection[0].objects:
            if obj.type == "MESH":
                pts += [obj.matrix_world @ v.co for v in obj.data.vertices]
    P = np.array([p[:] for p in pts])
    lo, hi = P.min(0), P.max(0)
    cen = (lo + hi) / 2
    cam = bpy.data.objects.new("전시_카메라", bpy.data.cameras.new("전시_카메라"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    t = math.radians(tilt)
    span = max(float(hi[0] - lo[0]), (float(hi[1] - lo[1]) * math.cos(t) + float(hi[2] - lo[2]) * math.sin(t)) * width / height)
    cam.data.ortho_scale = span * 1.03
    dist = span * 2 + 50
    cam.data.clip_end = dist * 3
    forward = Vector((0.0, math.cos(t), -math.sin(t)))
    cam.location = Vector((float(cen[0]), float(cen[1]), float(cen[2]))) - forward * dist
    cam.rotation_euler = (math.radians(90) - t, 0.0, 0.0)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    save_dir = render_dir = None
    grades = []
    it = iter(args)
    for a in it:
        if a == "--save":
            save_dir = next(it)
        elif a == "--render":
            render_dir = next(it)
        else:
            grades.append(a)
    for o in list(bpy.data.objects):                    # 헤드리스 기본 장면의 Cube·Camera·Light가 판 옆에 끼어든다(1차 렌더에서 확인). 창에서는 이 main()을 안 쓴다.
        bpy.data.objects.remove(o, do_unlink=True)
    boards, report = show_units(grades or None)
    import json
    print("전시판  " + json.dumps({k: len(v) for k, v in report.items()}, ensure_ascii=False))
    print("이름표  " + json.dumps(report, ensure_ascii=False))
    if render_dir:
        print("정면 렌더  " + front_render(boards, os.path.join(render_dir, "판_유닛_전체_정면.png")))
    if save_dir:
        os.makedirs(save_dir, exist_ok=True)
        name = "판_유닛_" + ("전체" if not grades else "_".join(grades)) + ".blend"
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(save_dir, name))
        print("저장  " + os.path.join(save_dir, name))


if __name__ == "__main__":
    main()
