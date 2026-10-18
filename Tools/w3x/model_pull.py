"""S2(또는 옛 판) w3x에서 MDX(+텍스처)를 이름으로 꺼내 뼈 애니 FBX로 변환(스킬 이펙트·유닛·구조물 공용).

  /usr/bin/python3 Tools/w3x/model_pull.py <s2|old> <출력루트> <모델> [<모델> …]
    모델: 'war3mapImported\\!0720_Tashigi.mdx' 처럼 MPQ 안 경로(.mdl 로 써도 .mdx 로 바꿔 읽음)
    s2  = ~/grd/GRD_motion_trial/_work/s2.mpq   old = .../ord.mpq
    산출: <출력루트>/<모델 파일명>/<이름>.fbx · Textures/ · <이름>.json  (export_mdx_anim_fbx.py 규칙)
          <출력루트>/_work/  (mdx_extract 결과: mdx + _tex + _approx.json)
  - 스킬 이펙트 → ~/grd/GRD_skill_vfx/   유닛·구조물 → ~/grd/GRD_s2_models/
  - 텍스처 BLP 는 blp_decode.decode 로 풀린다(JPEG 알파 함정 회피). 맵 밖 워크3 기본 텍스처는 approx 표시.
  - 후속 검수: Tools/blender 의 skin 검수 도구(check_entries 등) — 유닛은 swap_refix_all 와 같은 규칙.
  - 🔴 원작 저작물 — Assets 반입은 PM·사장님 판단.
"""
import os, re, subprocess, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
H = os.path.expanduser("~")
MPQ = {"s2": H + "/grd/GRD_motion_trial/_work/s2.mpq", "old": H + "/grd/GRD_motion_trial/_work/ord.mpq"}
BLENDER = "/opt/homebrew/bin/blender"


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
        print("OK" if os.path.exists(os.path.join(out, base, base + ".fbx")) else "FBX 없음", n)
    for n in miss:
        print("맵에 없음", n)


if __name__ == "__main__":
    main()
