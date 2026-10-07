#!/bin/zsh
# 패치노트 HTML → 버전마다 한 장씩 PNG(바탕화면 구랜디_베타/패치노트/). 사장님 10-08: 세로로 긴 한 장 대신 한 장씩 넘기기.
# 각 장 = 머리글 + 그 버전 섹션 하나 + 꼬리글, 머리글 아래에 「n / N」. 높이는 내용에 맞춰 잘라 낸다.
# 파일 이름은 최신이 1번(1_0.3.15.png …) — 사진 앱에서 이름순으로 넘기면 최신부터 본다.
D=${0:A:h}; OUTDIR=~/Desktop/구랜디_베타/패치노트
C="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
TMP=$(mktemp -d)
rm -rf "$OUTDIR"; mkdir -p "$OUTDIR"
rm -f ~/Desktop/구랜디_베타/GRD_패치노트_0.3.11이후.png   # 옛 세로 한 장
/usr/bin/python3 - "$D/patch_notes.html" "$TMP" <<'PY'
import sys, re
src = open(sys.argv[1], encoding='utf-8').read()
head, rest = src.split('</header>', 1)
head += '</header>'
foot = rest[rest.index('<footer'):]
secs = re.findall(r'<section class="ver[^"]*">.*?</section>', rest, re.S)
n = len(secs)
for i, s in enumerate(secs, 1):
    ver = re.search(r'<span class="v">([^<]+)</span>', s).group(1)
    page = f'<div style="text-align:center;color:var(--muted);font-size:20px;margin:-24px 0 28px">{i} / {n}</div>'
    open(f'{sys.argv[2]}/{i}_{ver}.html', 'w', encoding='utf-8').write(head + page + s + foot)
PY
for f in "$TMP"/*.html; do
  b=${f:t:r}
  "$C" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --virtual-time-budget=8000 \
    --window-size=1080,4000 --screenshot="$TMP/$b.png" "file://$f" 2>/dev/null
  /usr/bin/python3 - "$TMP/$b.png" "$OUTDIR/$b.png" <<'PY'
import sys
from PIL import Image
im=Image.open(sys.argv[1]).convert('RGB'); w,h=im.size; px=im.load()
bg=px[w-2,h-2]; y=h-1
while y>0 and all(abs(a-b)<4 for a,b in zip(px[w//2,y],bg)) and all(abs(a-b)<4 for a,b in zip(px[60,y],bg)): y-=1
im.crop((0,0,w,min(h,y+56))).save(sys.argv[2]); print(sys.argv[2], w, y+56)
PY
done
rm -rf "$TMP"
