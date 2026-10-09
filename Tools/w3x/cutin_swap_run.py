"""스킨 교체 유닛(초월·불멸·영원) 컷인 C안 재제작(2026-10-09, blender): 원작 스킨 FBX로 gen_cutin_char(C) → cutin_pick → gen_cutin_comp.
  /usr/bin/python3 Tools/w3x/cutin_swap_run.py [로스터 …]   → ~/GRD_cutin_swap/{render, C_all, layers}
옛 컷인(~/GRD_cutin)은 그대로 두고 별도 폴더에 만든다(구현담당2 반입용). 포즈 = 원작 Attack 시퀀스 프레임 후보 c1~c3(+ 믹사모 프리셋 없음), 사람이 고른 옛 포즈 목록(cutin_picks.json)은 쓰지 않는다.
공격 시퀀스 시작에 알파 0인 메시는 HIDE_MESHES로 뺀다(clip_map.json hiddenMeshes)."""
import csv, glob, json, os, re, shutil, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE))
H = os.path.expanduser("~"); HOME = H + "/GRD_cutin_swap"; os.makedirs(HOME + "/render", exist_ok=True)
if not os.path.exists(HOME + "/layers"): shutil.copytree(H + "/GRD_cutin/layers", HOME + "/layers")
if not os.path.exists(HOME + "/fonts"): os.symlink(H + "/GRD_cutin/fonts", HOME + "/fonts")
EXTRA_HIDE = {"초월_이태훈_AP": ["rokugu_tr4_g5", "rokugu_tr4_g6"]}      # 덩굴 메시(몸이 작아 보이던 원인, 10-09 PM)
PICKS = {"불멸_신지우": {"pick": "c1", "zoom": 1.45}, "불멸_정윤식": {"pick": "c1", "zoom": 1.4}}   # 작게 보이는 둘은 확대 자르기
json.dump(dict({"_설명": "교체 스킨 컷인: 옛 포즈 목록 대신 필요한 유닛만 확대(zoom)"}, **PICKS), open(HOME + "/_no_picks.json", "w"), ensure_ascii=False)
env = dict(os.environ, CUTIN_HOME=HOME, CUTIN_PICKS=HOME + "/_no_picks.json", ONLY="C")
rows = list(csv.DictReader(open(H + "/GRD_skin_swap/교체목록.csv", encoding="utf-8-sig")))
want = set(sys.argv[1:]); units = []
for r in rows:
    ro = r["우리 로스터"]
    if ro.split("_")[0] not in ("초월", "불멸", "영원") or (want and ro not in want): continue
    nm = ro.split("_")[1] + "_" + r["원작 이름"].split()[0]; d = f"{H}/GRD_skin_swap/{nm}"
    fb = glob.glob(d + "/model/*.fbx")
    if not fb: print("폴더 없음", ro, nm); continue
    cm = json.load(open(d + "/clip_map.json")); atk = next((c for c in cm["clips"] if c["ourClip"] == "Attack"), None)
    units.append((ro, fb[0], ",".join((atk["hiddenMeshes"] if atk else []) + EXTRA_HIDE.get(ro, []))))
for ro, fb, hid in units:
    e = dict(env, HIDE_MESHES=hid)
    r = subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/gen_cutin_char.py", "--", fb, HOME + "/render", ro], env=e, capture_output=True, text=True)
    print(ro, [l for l in (r.stdout + r.stderr).split("\n") if l.startswith(("C ", "Traceback", "Error"))][:2], flush=True)
us = [u[0] for u in units]
subprocess.run(["/usr/bin/python3", ROOT + "/Tools/blender/cutin_pick.py"] + us, env=env)
subprocess.run(["/usr/bin/python3", ROOT + "/Tools/blender/gen_cutin_comp.py", "C", "all"] + us, env=env)
subprocess.run(["/usr/bin/python3", ROOT + "/Tools/blender/cutin_sheets.py"], env=env)
print("완료", HOME)
