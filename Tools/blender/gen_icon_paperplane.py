"""유물 「종이비행기」 아이콘 후보(blender 세션 2026-10-06) — 직접 그림(원작 대응 그림 없음, 자작).
/usr/bin/python3 Tools/blender/gen_icon_paperplane.py ~/GRD_item_icons → R002_종이비행기_후보.png(256) · _64.png
하늘 그라데이션 바탕 + 접힌 면마다 명암 다른 흰 종이비행기 + 테두리(원작 BTN 틀 느낌)."""
import os, sys
from PIL import Image, ImageDraw, ImageFilter
out = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/GRD_item_icons"); os.makedirs(out, exist_ok=True)
S = 1024
img = Image.new("RGB", (S, S))
px = img.load()
for y in range(S):
    t = y / S
    c = (int(40 + 70 * (1 - t)), int(90 + 90 * (1 - t)), int(170 + 70 * (1 - t)))
    for x in range(S): px[x, y] = c
d = ImageDraw.Draw(img, "RGBA")
for cx, cy, r in ((250, 760, 110), (360, 790, 90), (770, 250, 100), (690, 280, 70)):   # 구름 두 덩이
    d.ellipse((cx - r, cy - r * .6, cx + r, cy + r * .6), fill=(255, 255, 255, 90))
# 비행기(오른쪽 위를 향함): 코 N, 왼 날개 끝 L, 오른 날개 끝 R, 꼬리 T, 접힌 줄 K
N, L, R, T, K = (860, 170), (170, 520), (480, 860), (330, 560), (460, 540)
d.polygon([(N[0] + 25, N[1] + 70), (R[0] + 40, R[1] + 30), (T[0] + 40, T[1] + 80)], fill=(0, 0, 0, 90))  # 그림자
d.polygon([N, L, K], fill=(255, 255, 255, 255))            # 윗 날개(밝음)
d.polygon([N, K, R], fill=(205, 220, 240, 255))            # 아랫 날개(그늘)
d.polygon([K, R, T], fill=(150, 170, 205, 255))            # 몸통 접힌 밑면(어두움)
d.polygon([L, K, T], fill=(225, 235, 250, 255))
d.line([N, K], fill=(120, 140, 180, 255), width=7)
d.line([K, T], fill=(120, 140, 180, 255), width=5)
for y in range(S):  # 어둑한 테두리
    for x in range(S):
        e = min(x, y, S - 1 - x, S - 1 - y)
        if e < 28:
            r_, g_, b_ = px[x, y]; k = 0.35 + e / 28 * .65; px[x, y] = (int(r_ * k), int(g_ * k), int(b_ * k))
img.save(out + "/R002_종이비행기_후보.png")
img.resize((256, 256), Image.LANCZOS).save(out + "/R002_종이비행기_후보_256.png")
img.resize((64, 64), Image.LANCZOS).save(out + "/R002_종이비행기_후보_64.png")
print("DONE")
