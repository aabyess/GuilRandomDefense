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

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
UNIT_DIR = os.path.join(PROJECT, "Assets", "Art", "Units")
PER_ROW = 8                      # 한 줄에 여덟 — 25종짜리 초월이 가로로 너무 길어지지 않게

# 🔴 판에서 빼는 유닛(2026-09-23 blender 전수 실측 — 커밋된 FBX를 전부 재수입해 세계 좌표로 잼).
#   유닛 규약은 「키(z) 1.8m」인데 이들은 자릿수가 다르다 — 한 판에 같이 놓으면 칸 너비가 이들 기준으로
#   잡혀 나머지가 점으로 보인다(1차 렌더에서 실제로 그랬다). 판에서만 빼고, 목록은 리포트로 올린다.
#   ⚠️ 이건 전시 문제가 아니라 **유닛 자체의 결함**이다 — 게임 안에서도 ArtBinder가 키로 맞추면서
#   말도 안 되는 배율이 나온다(PM이 유니티에서 본 희귀함_김정래 0.16배와 같은 부류).
SKIP = {
    "안흔함_김용태": "키 22.99m(규약의 12.8배)",
    "안흔함_엄태웅": "키 18.83m(10.5배)",
    "안흔함_이재윤": "키 4.47m(2.5배)",
    "안흔함_박준희": "크기가 0 — 메시 55개가 전부 정점 범위 0(완전 퇴화)",
}

# 등급 순서(흔함 → … → 히든)와 판 제목. 폴더 접두어가 곧 등급이다.
GRADES = [
    ("흔함", "COMMON"), ("안흔함", "UNCOMMON"), ("특별함", "SPECIAL"), ("희귀함", "RARE"),
    ("전설적인", "LEGENDARY"), ("제한", "LIMITED"),
    ("초월", "TRANSCENDENT"), ("불멸", "IMMORTAL"), ("영원", "ETERNAL"), ("히든", "HIDDEN"),
]

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
    parts = unit.split("_")[1:]
    words = []
    for p in parts:
        if not p or ord(p[0]) < 0xAC00:                      # AD·AP·ADAP 같은 영문 꼬리
            words.append(p)
        elif len(p) >= 2 and p[0] in SURNAME:
            words += [SURNAME[p[0]], romanize(p[1:])]
        else:
            words.append(romanize(p))
    return " ".join(w for w in words if w)


def import_unit(collection, showcase, path):
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
    world = {o: o.matrix_world.copy() for o in meshes}
    for o in new:
        if o not in meshes:                             # 뼈대·빈 껍데기(정점 0짜리 Cube 따위)는 전시에서 뺀다
            bpy.data.objects.remove(o, do_unlink=True)
    for o in meshes:
        o.parent = None
        o.matrix_basis = Matrix.Identity(4)
        o.data.transform(world[o])                      # 배율·회전을 메시에 구워 둔다(자리는 lay_out이 잡는다)
        showcase.relink_textures(o, os.path.join(os.path.dirname(path), "Textures"))   # 유닛은 <유닛>/Textures/ (자연물과 층이 다르다)
    return meshes


def units_of(grade):
    return sorted(n for n in os.listdir(UNIT_DIR)
                  if n.startswith(grade + "_") and n not in SKIP
                  and os.path.isfile(os.path.join(UNIT_DIR, n, n + ".fbx")))


def show_units(grades=None):
    import importlib
    import showcase
    importlib.reload(showcase)

    picked = [(g, t) for g, t in GRADES if grades is None or g in grades]
    boards, report = {}, {}
    for grade, title in picked:
        names = units_of(grade)
        col_name = "판_유닛_" + grade
        col = showcase.collection(col_name)
        rows = []
        for start in range(0, len(names), PER_ROW):
            groups = []
            for unit in names[start:start + PER_ROW]:
                meshes = import_unit(col, showcase, os.path.join(UNIT_DIR, unit, unit + ".fbx"))
                assert meshes, f"{unit}: 정점 있는 메시가 없다"
                meshes[0].name = unit
                groups.append(showcase.group(label_of(unit), [(o, 0.0, 0.0) for o in meshes]))
            rows.append((f"{grade} {start + 1}~{start + len(groups)}", groups))
        boards[col_name] = showcase.lay_out(col, rows, title=title)
        report[col_name] = [label_of(n) for n in names]
    # 등급 순서가 보이게 한 줄에 넷씩(앞줄이 낮은 등급) — 판이 커서 한 줄에 다 놓으면 가로 수백 m가 된다.
    order = ["판_유닛_" + g for g, _ in picked]
    showcase.arrange([[boards[n] for n in order[i:i + 4]] for i in range(0, len(order), 4)])
    showcase.frame_all(list(boards.values()))
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
