"""24기 model_fixed 재수출 + 연속성·바인드 검증 일괄(2026-10-09, blender): /usr/bin/python3 Tools/w3x/swap_refix_all.py [팩 이름 …]
이전 판은 ~/GRD_skin_swap/_model_fixed_prev/<팩>/ 에 있어야 한다(없으면 현재 것을 복사). 결과 표 ~/GRD_skin_swap/_재수출검증.csv"""
import csv, glob, os, re, shutil, subprocess, sys
H = os.path.expanduser("~"); ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
only = set(sys.argv[1:]); rows = list(csv.DictReader(open(H + "/GRD_skin_swap/교체목록.csv", encoding="utf-8-sig"))); out = []
def run(py, *a): return subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/" + py, "--", *a], capture_output=True, text=True).stdout
for r in rows:
    ro = r["우리 로스터"]; nm = ro.split("_")[1] + "_" + r["원작 이름"].split()[0]; pk = f"{H}/GRD_skin_swap/{nm}"
    if not os.path.isdir(pk): pk = glob.glob(f"{H}/GRD_skin_swap/{ro.split('_')[1]}_*")[0]
    nm = os.path.basename(pk)
    if only and nm not in only: continue
    prev = f"{H}/GRD_skin_swap/_model_fixed_prev/{nm}"
    if not os.path.isdir(prev): shutil.copytree(pk + "/model_fixed", prev)
    o1 = run("swap_refix_b3.py", pk); ok = re.search(r"^OK .*", o1, re.M)
    new = glob.glob(pk + "/model_fixed/*.fbx")[0]; old = glob.glob(prev + "/*.fbx")[0]
    o2 = run("swap_verify_cont.py", new, old); c = re.search(r"CONT .*", o2)
    o3 = run("swap_verify_b3.py", pk + "/model_fixed"); rest = re.search(r"rest-vs-stand max diff ([\d.]+)", o3)
    out.append([nm, ro, "OK" if ok else "실패", (re.search(r"WARN (\[.*?\]) clipmap_diff (\[.*\])", ok.group(0)).groups() if ok and re.search(r"WARN", ok.group(0)) else ""), c.group(0) if c else "검증 실패", rest.group(1) if rest else "?"])
    print(nm, "OK" if ok else "실패", c.group(0) if c else "-", "rest-vs-stand", rest.group(1) if rest else "?", flush=True)
w = csv.writer(open(H + "/GRD_skin_swap/_재수출검증.csv", "w", newline="", encoding="utf-8-sig")); w.writerow(["팩", "로스터", "재수출", "WARN/clipmap_diff", "연속성 검증", "휴식-vs-Stand 최대 차(m)"]); w.writerows(out)
