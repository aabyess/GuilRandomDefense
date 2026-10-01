"""원작 맵 MPQ에서 MDX와 그 텍스처를 꺼내 작업 폴더에 둔다(Blender 그림 render_mdx.py의 앞 단계).

  python3 Tools/w3x/mdx_extract.py <ord.mpq> <작업폴더> <이름.mdx> [...]

작업폴더/<이름.mdx> · 작업폴더/_tex/<경로를_밑줄로>.png · 작업폴더/_approx.json(맵 안에 없어 근사로 그린 텍스처 경로).
맵 안에 없는 워크3 기본 텍스처(Textures\\Blue_Glow2 등)는 이름에서 색·모양을 짐작해 비슷한 그림을 만든다 — 원작과 정확히 같지 않다.
"""
import io
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mpqread import Archive                                          # noqa: E402
import mdx_geo                                                       # noqa: E402
from PIL import Image, ImageDraw, ImageFilter                        # noqa: E402


def placeholder(name):
    low = name.lower()
    col = (255, 255, 255)
    for k, c in (("blue", (80, 140, 255)), ("yellow", (255, 220, 80)), ("purple", (190, 90, 255)), ("red", (255, 70, 60)),
                 ("frost", (150, 220, 255)), ("flame", (255, 150, 50)), ("green", (90, 255, 120)), ("gold", (255, 215, 90))):
        if k in low:
            col = c
            break
    S = 256
    im = Image.new("RGBA", (S, S), (0, 0, 0, 255))
    d = ImageDraw.Draw(im)
    if "runearrow" in low or "aurarune" in low:
        for k in range(16):
            a = k / 16 * 2 * math.pi
            cx, cy = S / 2 + math.cos(a) * S * 0.36, S / 2 + math.sin(a) * S * 0.36
            tip = (cx + math.cos(a) * 18, cy + math.sin(a) * 18)
            l = (cx + math.cos(a + 2.4) * 16, cy + math.sin(a + 2.4) * 16)
            r = (cx + math.cos(a - 2.4) * 16, cy + math.sin(a - 2.4) * 16)
            d.polygon([tip, l, r], fill=(200, 255, 200, 255))
        d.ellipse([S * 0.08, S * 0.08, S * 0.92, S * 0.92], outline=(120, 220, 120, 255), width=4)
    elif "star" in low:
        for w, h in ((14, S * 0.45), (S * 0.45, 14)):
            d.ellipse([S / 2 - w, S / 2 - h, S / 2 + w, S / 2 + h], fill=col + (255,))
    elif "zap" in low or "lightning" in low:
        pts = [(S * 0.5, 4)]
        for k in range(1, 10):
            pts.append((S * 0.5 + (-1) ** k * 26, 4 + k * S / 10))
        d.line(pts, fill=col + (255,), width=10)
    else:
        for r in range(int(S * 0.5), 0, -4):
            t = 1 - r / (S * 0.5)
            v = int(255 * t * t)
            d.ellipse([S / 2 - r, S / 2 - r, S / 2 + r, S / 2 + r], fill=(col[0] * v // 255, col[1] * v // 255, col[2] * v // 255, 255))
    return im.filter(ImageFilter.GaussianBlur(2))


def main():
    mpq, work, names = sys.argv[1], sys.argv[2], sys.argv[3:]
    os.makedirs(os.path.join(work, "_tex"), exist_ok=True)
    arc = Archive(mpq)
    approx = set(json.load(open(os.path.join(work, "_approx.json")))) if os.path.exists(os.path.join(work, "_approx.json")) else set()
    for name in names:
        data = arc.read(name)
        if data is None:
            print(name, "맵에 없음")
            continue
        open(os.path.join(work, name), "wb").write(data)
        model = mdx_geo.parse(data)
        for t in model["textures"]:
            path = t["path"]
            if not path or t["replaceable"] in (1, 2) or "teamcolor" in path.lower() or "teamglow" in path.lower():
                continue
            out = os.path.join(work, "_tex", path.replace("\\", "_").replace("/", "_") + ".png")
            blp = arc.read(path) or arc.read(os.path.basename(path.replace("\\", "/")))
            if blp is not None:
                Image.open(io.BytesIO(blp)).convert("RGBA").save(out)
            else:
                approx.add(path)
                placeholder(path).save(out)
        print(name, "ok", len(model["geosets"]), "지오셋", [t["path"] for t in model["textures"] if t["path"] in approx])
    json.dump(sorted(approx), open(os.path.join(work, "_approx.json"), "w"), ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
