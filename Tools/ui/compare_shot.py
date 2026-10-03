#!/usr/bin/env python3
"""우리 gameshot 사진과 원작 사진을 같은 크기로 나란히 붙인다 — 사용: compare_shot.py <우리.png> <원작.png> <출력.png> [제목]
우리 사진(예: 1920×1080)은 원작 사진 크기(1720×960)로 줄여 맞춘다(둘 다 16:9 근방). 왼쪽 원작 · 오른쪽 우리."""
import sys
from PIL import Image, ImageDraw
ours, orig, out = sys.argv[1:4]
title = sys.argv[4] if len(sys.argv) > 4 else ''
o = Image.open(orig).convert('RGB')
u = Image.open(ours).convert('RGB').resize(o.size, Image.LANCZOS)
w, h = o.size
sheet = Image.new('RGB', (w * 2 + 30, h + 50), (24, 28, 38))
sheet.paste(o, (10, 40)); sheet.paste(u, (w + 20, 40))
d = ImageDraw.Draw(sheet)
d.text((12, 12), '원작 (사장님 사진)  ' + title, fill=(240, 220, 120)); d.text((w + 22, 12), '우리 (gameshot)', fill=(120, 220, 240))
sheet.save(out, quality=85) if out.lower().endswith('.jpg') else sheet.save(out)
print(out, sheet.size)
