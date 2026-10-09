"""BLP 알파 디코드 버그 전수 조사·복구(2026-10-09, blender). /usr/bin/python3 Tools/w3x/audit_blp_alpha.py [--fix] [루트 …]
루트 아래 모든 *.blp.png → 이름(basename)으로 s2.mpq·ord.mpq의 BLP를 찾아 blp_decode로 푼 알파와 비교.
 대상 = JPEG BLP + 알파비트>0 + 올바른 투명 비율 >3% + 지금 PNG 알파가 전부 255(=버그 서명).
--fix: Assets 밖 루트만 덮어쓴다. Assets는 목록만(~/GRD_blp_audit/assets_refix.txt). 표 ~/GRD_blp_audit/audit.csv"""
import csv, glob, os, struct, sys, io
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mpqread import Archive
from blp_decode import decode
from PIL import Image
H = os.path.expanduser("~"); OUT = H + "/GRD_blp_audit"; os.makedirs(OUT, exist_ok=True)
args = [a for a in sys.argv[1:] if not a.startswith("--")]; FIX = "--fix" in sys.argv
ROOTS = args or [H + "/GitHub/GuilRandomDefense/Assets", H + "/GRD_orig_vfx_trial", H + "/GRD_motion_trial", H + "/Desktop/구랜디스킨모음", H + "/GRD_skin_swap", H + "/GRD_scenes"]
ARCS = [Archive(H + "/GRD_motion_trial/_work/s2.mpq"), Archive(H + "/GRD_motion_trial/_work/ord.mpq")]
cache = {}
def info(base):                      # base = 'x.blp'
    if base in cache: return cache[base]
    r = None
    for a in ARCS:
        d = a.read(base)
        if d:
            im = decode(d); al = im.getchannel("A"); n = im.width * im.height
            frac = sum(1 for v in al.getdata() if v < 128) / n
            content, ab = (struct.unpack("<II", d[4:12]) if d[:4] == b"BLP1" else (-1, -1))
            r = dict(img=im, frac=frac, jpeg=(content == 0), alphabits=ab); break
    cache[base] = r; return r
rows = []; seen = set(); assets = []
for root in ROOTS:
    for p in glob.glob(root + "/**/*.blp.png", recursive=True):
        if p in seen or os.path.islink(p): continue
        seen.add(p); base = os.path.basename(p)[:-4]
        r = info(base)
        if not r:                                       # 'Textures_X.blp.png' = 경로 'Textures\\X.blp'(\\ → _ 로 납작하게 저장됨): 밑줄을 폴더 구분으로 되돌려 본다
            for i, ch in enumerate(base):
                if ch == "_":
                    r = info(base[:i] + "\\" + base[i + 1:])
                    if r: break
                    for j in range(i + 1, len(base)):
                        if base[j] == "_":
                            r = info(base[:i] + "\\" + base[i + 1:j] + "\\" + base[j + 1:])
                            if r: break
                    if r: break
        r = r or info(base.split("_")[-1])
        if not r: rows.append([p, "BLP 못 찾음", "", "", ""]); continue
        try: cur = Image.open(p).convert("RGBA")
        except Exception: continue
        amin = cur.getchannel("A").getextrema()[0]
        need = r["jpeg"] and r["alphabits"] > 0 and r["frac"] > 0.03 and amin == 255 and cur.size == r["img"].size
        rows.append([p, "JPEG" if r["jpeg"] else "팔레트", r["alphabits"], f"{r['frac']:.0%}", "재추출 필요" if need else ""])
        if need:
            if "/Assets/" in p: assets.append(p)
            elif FIX: r["img"].save(p)
w = csv.writer(open(OUT + "/audit.csv", "w", newline="", encoding="utf-8-sig")); w.writerow(["파일", "종류", "알파비트", "올바른 투명 비율", "판정"]); w.writerows(rows)
open(OUT + "/assets_refix.txt", "w").write("\n".join(assets) + "\n")
import collections
print("총", len(rows), collections.Counter(r[4] or r[1] for r in rows), "Assets 재추출", len(assets))
