"""perf_lod.py 뒤처리(시스템 파이썬 PIL): 텍스처를 최대변 N으로 줄여 <out>/Textures에 넣고, 전후 그림을 한 장(전후.png)으로 묶는다.
  /usr/bin/python3 Tools/blender/perf_lod_post.py <out 폴더>"""
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
out = sys.argv[1]; J = json.load(open(out + "/lod.json")); src = os.path.dirname(J["src"]) + "/Textures"
os.makedirs(out + "/Textures", exist_ok=True); info = []
for n in J["textures"]:
    im = Image.open(f"{src}/{n}"); w, h = im.size; k = min(1.0, J["texMax"] / max(w, h))
    if k < 1: im = im.resize((max(1, int(w * k)), max(1, int(h * k))), Image.LANCZOS)
    im.save(f"{out}/Textures/{n}"); info.append(f"{n} {w}x{h}->{im.size[0]}x{im.size[1]}")
J["textureResize"] = info; json.dump(J, open(out + "/lod.json", "w"), ensure_ascii=False, indent=1)
a, b = Image.open(out + "/_before.png").convert("RGB"), Image.open(out + "/_after.png").convert("RGB")
sh = Image.new("RGB", (a.width * 2, a.height + 44), (30, 32, 38)); sh.paste(a, (0, 44)); sh.paste(b, (a.width, 44)); d = ImageDraw.Draw(sh)
f = ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", 24)
d.text((10, 8), f"{J['model']}  전 {J['trisBefore']:,}면", fill=(255, 255, 255), font=f); d.text((a.width + 10, 8), f"후 {J['trisAfter']:,}면 ({J['trisAfter'] * 100 // J['trisBefore']}%)  텍스처 최대 {J['texMax']}", fill=(255, 220, 120), font=f)
sh.save(out + "/전후.png"); os.remove(out + "/_before.png"); os.remove(out + "/_after.png"); print("OK", out)
