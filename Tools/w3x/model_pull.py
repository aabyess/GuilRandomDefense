"""S2(또는 옛 판) w3x에서 MDX(+텍스처)를 이름으로 꺼내 뼈 애니 FBX로 변환(스킬 이펙트·유닛·구조물 공용).

  /usr/bin/python3 Tools/w3x/model_pull.py <s2|old> <출력루트> <모델> [<모델> …]
    모델: 'war3mapImported\\!0720_Tashigi.mdx' 처럼 MPQ 안 경로(.mdl 로 써도 .mdx 로 바꿔 읽음)
    s2  = ~/grd/GRD_motion_trial/_work/s2.mpq   old = .../ord.mpq
    산출: <출력루트>/<모델 파일명>/<이름>.fbx · Textures/ · <이름>.json  (export_mdx_anim_fbx.py 규칙)
          <출력루트>/_work/  (mdx_extract 결과: mdx + _tex + _approx.json)
  - 스킬 이펙트 → ~/grd/GRD_skill_vfx/   유닛·구조물 → ~/grd/GRD_s2_models/
  - 텍스처 BLP 는 blp_decode.decode 로 풀린다(JPEG 알파 함정 회피). 맵 밖 워크3 기본 텍스처는 approx 표시.
  - 끝에 vfx_unity_block.py를 돌려 json에 unity(OriginalVfxBuilder)·pre2Unity(Pre2Builder) 블록을 붙이고 입자 텍스처를 Textures/로 복사한다.
  - 후속 검수: Tools/blender 의 skin 검수 도구(check_entries 등) — 유닛은 swap_refix_all 와 같은 규칙.
  - 🔴 원작 저작물 — Assets 반입은 PM·사장님 판단.
"""
import os, re, subprocess, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
H = os.path.expanduser("~")
MPQ = {"s2": H + "/grd/GRD_motion_trial/_work/s2.mpq", "old": H + "/grd/GRD_motion_trial/_work/ord.mpq"}
BLENDER = "/opt/homebrew/bin/blender"


def opaque_none_textures(mdir, work):
    """필터 none(불투명) 층만 쓰는 텍스처는 알파를 255로 채운다.
    워크3는 none 층에서 알파를 안 보는데, 중국 모델러 텍스처 일부는 알파에 워터마크만 있고 나머지가 0이다
    (10-18 S2 새턴: 알파 0 이 98% → Blender 그림이 검고, 우리 _cut 재질로는 몸이 사라진다)."""
    from PIL import Image
    js = [f for f in os.listdir(mdir) if f.endswith(".json")]
    if not js:
        return
    side = json.load(open(os.path.join(mdir, js[0])))
    use = {}
    for m in side.get("meshes", []):
        if m.get("textureFile"):
            use.setdefault(m["textureFile"], set()).add(m.get("filter"))
    for tf, filters in use.items():
        if filters != {"none"}:
            continue
        for p in (os.path.join(mdir, "Textures", tf), os.path.join(work, "_tex", tf)):
            if os.path.exists(p):
                im = Image.open(p).convert("RGBA")
                if im.getchannel("A").getextrema()[0] < 255:
                    im.putalpha(255)
                    im.save(p)
                    print("  알파 채움(none 층 전용):", tf)


def main():
    which, out, names = sys.argv[1], os.path.expanduser(sys.argv[2]), sys.argv[3:]
    names = [re.sub(r"\.mdl$", ".mdx", n, flags=re.I) for n in names]
    if not names[0].lower().endswith(".mdx"):
        names = [n + ".mdx" for n in names]
    sys.path.insert(0, HERE)
    from mpqread import Archive
    arc = Archive(MPQ[which])
    names = [n if arc.read(n) is not None or "\\" in n else ("war3mapImported\\" + n if arc.read("war3mapImported\\" + n) is not None else n) for n in names]
    work = os.path.join(out, "_work")
    os.makedirs(work, exist_ok=True)
    subprocess.run([sys.executable, HERE + "/mdx_extract.py", MPQ[which], work] + names, check=True)
    ok = [n for n in names if os.path.exists(os.path.join(work, n))]
    miss = [n for n in names if n not in ok]
    if ok:
        subprocess.run([BLENDER, "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/export_mdx_anim_fbx.py", "--", work, out] + ok,
                       check=True, stdout=subprocess.DEVNULL)
    for n in ok:
        base = os.path.splitext(n.replace("\\", "__").replace("/", "__"))[0]
        opaque_none_textures(os.path.join(out, base), work)
        print("OK" if os.path.exists(os.path.join(out, base, base + ".fbx")) else "FBX 없음", n)
    for n in miss:
        print("맵에 없음", n)
    if ok:   # 유니티 반입 블록(unity·pre2Unity) + 입자 텍스처 복사 — Tools/w3x/vfx_unity_block.py
        subprocess.run([sys.executable, HERE + "/vfx_unity_block.py", out], check=True, stdout=subprocess.DEVNULL)


if __name__ == "__main__":
    main()
