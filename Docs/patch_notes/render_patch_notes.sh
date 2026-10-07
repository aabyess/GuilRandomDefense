#!/bin/zsh
# 패치노트 HTML → PNG(바탕화면 구랜디_베타). 높이는 내용에 맞춰 잘라 낸다.
D=${0:A:h}; OUT=~/Desktop/구랜디_베타/GRD_패치노트_0.3.11이후.png
C="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
"$C" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --virtual-time-budget=8000 \
  --window-size=1080,6000 --screenshot="$D/_full.png" "file://$D/patch_notes.html" 2>/dev/null
/usr/bin/python3 - "$D/_full.png" "$OUT" <<'PY'
import sys
from PIL import Image
im=Image.open(sys.argv[1]).convert('RGB'); w,h=im.size; px=im.load()
bg=px[w-2,h-2]; y=h-1
while y>0 and all(abs(a-b)<4 for a,b in zip(px[w//2,y],bg)) and all(abs(a-b)<4 for a,b in zip(px[60,y],bg)): y-=1
im.crop((0,0,w,min(h,y+56))).save(sys.argv[2]); print(sys.argv[2], w, y+56)
PY
rm -f "$D/_full.png"
