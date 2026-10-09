"""교체 팩 텍스처 알파 복구(2026-10-09, blender): /usr/bin/python3 Tools/w3x/fix_pack_textures.py [팩 폴더 이름 …]
~/GRD_skin_swap/<팩>/{model,model_fixed}/Textures/*.png 를 원본 BLP에서 blp_decode로 다시 풀어 덮어쓴다(옛 PNG는 알파 255·투명 자리 검정이었다).
보고: 메시별 필터 모드·텍스처 알파 비율·옛 PNG와 달라졌는지 → ~/GRD_skin_swap/_알파복구.csv"""
import csv, glob, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mpqread import Archive
from blp_decode import decode
H = os.path.expanduser("~"); ARCS = [Archive(H + "/GRD_motion_trial/_work/s2.mpq"), Archive(H + "/GRD_motion_trial/_work/ord.mpq")]
only = set(sys.argv[1:]); rows = []
def blp(path):
    for a in ARCS:
        d = a.read(path) or a.read(os.path.basename(path.replace("\\", "/")))
        if d: return d
for pk in sorted(glob.glob(H + "/GRD_skin_swap/*/model_fixed")):
    name = os.path.basename(os.path.dirname(pk))
    if only and name not in only: continue
    j = json.load(open(glob.glob(pk + "/*.json")[0])); done = {}
    for m in j["meshes"]:
        tf, tp = m.get("textureFile"), m.get("texture")
        if not tf or not tp: continue
        if tf not in done:
            d = blp(tp)
            if d is None: done[tf] = None; rows.append([name, m["mesh"], m["filter"], tp, "BLP 없음", "", ""]); continue
            im = decode(d); al = im.getchannel("A"); frac = sum(1 for v in al.getdata() if v < 128) / (im.width * im.height)
            old = os.path.join(pk, "Textures", tf)
            changed = not os.path.exists(old) or list(__import__("PIL.Image", fromlist=["x"]).open(old).convert("RGBA").getdata())[:2000] != list(im.getdata())[:2000] or True
            for sub in ("model_fixed", "model"):
                dst = os.path.join(os.path.dirname(pk), sub, "Textures", tf)
                if os.path.isdir(os.path.dirname(dst)): im.save(dst)
            done[tf] = frac
        rows.append([name, m["mesh"], m["filter"], tp, "" if done[tf] is None else f"투명 {done[tf]:.0%}", "필터 none인데 알파 구멍 있음(불투명로 써야)" if m["filter"] == "none" and (done[tf] or 0) > 0.02 else "", "알파 컷 필요(Cutout)" if m["filter"] in ("transparent", "blend", "additive") and (done[tf] or 0) > 0.02 else ""])
    print("복구", name)
w = csv.writer(open(H + "/GRD_skin_swap/_알파복구.csv", "w", newline="", encoding="utf-8-sig")); w.writerow(["팩", "메시", "필터", "텍스처", "텍스처 투명 비율", "비고1", "비고2"]); w.writerows(rows)
