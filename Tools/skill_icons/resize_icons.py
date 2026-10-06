#!/usr/bin/env python3
"""스킬 아이콘 PNG를 128×128로 줄여 Assets/Art/SkillIcons/에 둔다(10-06 구현담당2).

표(Tools/skill_icons/skill_icon_map.csv)의 「png」 열에 적힌 것만, ~/GRD_skill_icons/ 에서 읽어
SkillIconLinker.Safe()와 같은 규칙(# & ! [ ] → _)으로 파일 이름을 바꿔 쓴다. 다시 돌려도 안전(덮어씀).
실행: /usr/bin/python3 Tools/skill_icons/resize_icons.py   (Pillow 필요)
"""
import csv, os, sys
from PIL import Image

root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
src = os.path.expanduser("~/GRD_skill_icons")
dst = os.path.join(root, "Assets/Art/SkillIcons")
os.makedirs(dst, exist_ok=True)

def safe(name):
    return "".join("_" if c in "#&![]" else c for c in name)

rows = list(csv.reader(open(os.path.join(root, "Tools/skill_icons/skill_icon_map.csv"), encoding="utf-8-sig")))
wanted = sorted({r[7].strip() for r in rows[1:] if len(r) >= 11 and r[7].strip()})
done = missing = 0
for png in wanted:
    p = os.path.join(src, png)
    if not os.path.exists(p):
        missing += 1
        continue
    im = Image.open(p).convert("RGBA").resize((128, 128), Image.LANCZOS)
    im.save(os.path.join(dst, safe(png)), optimize=True)
    done += 1
print(f"줄인 PNG {done}개 · 원본 없음 {missing}개 · 폴더 {dst}")
