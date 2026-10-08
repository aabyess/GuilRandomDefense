"""모델 폴더 Textures/에 입자(PRE2) 텍스처 PNG가 빠진 것을 작업 폴더 _tex에서 복사(2026-10-09, blender). 메시용 텍스처만 변환돼 있던 폴더를 채운다."""
import glob, json, os, shutil, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mdx_anim, mdx_geo
H = os.path.expanduser("~"); T = H + "/GRD_orig_vfx_trial"
W = [H + f"/GRD_motion_trial/{w}/work" for w in ("original_vfx", "transcend_vfx", "grade_extra", "original_skin")]
cp = miss = 0
for jp in glob.glob(T + "/*/*.json"):
    d = json.load(open(jp)); t = os.path.basename(os.path.dirname(jp))
    if not d.get("pre2"): continue
    w = next((x for x in W if os.path.exists(f"{x}/{d['model']}")), None)
    if not w: continue
    raw = open(f"{w}/{d['model']}", "rb").read(); geo = mdx_geo.parse(raw); info = mdx_anim.describe(raw)
    for p in info["pre2"]:
        if p["tex"] >= len(geo["textures"]): continue
        tp = geo["textures"][p["tex"]]["path"]
        if not tp: continue
        png = tp.replace("\\", "_").replace("/", "_") + ".png"
        dst = f"{T}/{t}/Textures/{png}"
        if os.path.exists(dst): continue
        if os.path.exists(f"{w}/_tex/{png}"):
            os.makedirs(f"{T}/{t}/Textures", exist_ok=True); shutil.copy(f"{w}/_tex/{png}", dst); cp += 1
        else: miss += 1
print("복사", cp, "작업 폴더에도 없음", miss)
