"""연속 프레임 → 가로 띠 한 장(+선택: gif). 사용: strip.py <출력.png> <프레임1.png> <프레임2.png> ... [--cols N] [--w 480]
문 전환 촬영(DoorPreview.Slow + gameshot snap 연속)에서 나온 프레임을 사장님이 움직임을 보기 좋게 한 장으로 붙인다."""
import sys
from PIL import Image, ImageDraw
args = sys.argv[1:]
cols, w = 0, 480
if "--cols" in args: i = args.index("--cols"); cols = int(args[i + 1]); del args[i:i + 2]
if "--w" in args: i = args.index("--w"); w = int(args[i + 1]); del args[i:i + 2]
out, files = args[0], args[1:]
ims = [Image.open(f).convert("RGB") for f in files]
h = int(ims[0].height * w / ims[0].width)
cols = cols or len(ims)
rows = (len(ims) + cols - 1) // cols
S = Image.new("RGB", (cols * w, rows * h), (10, 10, 14))
d = ImageDraw.Draw(S)
for i, im in enumerate(ims):
    x, y = (i % cols) * w, (i // cols) * h
    S.paste(im.resize((w, h), Image.LANCZOS), (x, y))
    d.text((x + 6, y + 4), str(i + 1), fill=(255, 255, 255))
S.save(out)
try:
    ims[0].resize((w, h)).save(out.replace(".png", ".gif"), save_all=True, append_images=[im.resize((w, h)) for im in ims[1:]], duration=100, loop=0)
except Exception as e:
    print("gif 실패", e)
print(out)
