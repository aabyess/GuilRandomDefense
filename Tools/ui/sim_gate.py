"""성문 열림(Portcullis)을 DoorTransition과 같은 식으로 PIL에서 흉내 낸 연속 프레임 → 가로 띠(에디터 없이 모양·타이밍 미리보기). 뒤 화면은 시작 화면 배경."""
import os, sys
from PIL import Image
D = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "UI")
fr = Image.open(f"{D}/DoorCastle/gate_frame.png").convert("RGBA"); gr = Image.open(f"{D}/DoorCastle/gate_grate.png").convert("RGBA")
game = Image.open(f"{D}/LobbyCastle/bg_1920x1080.png").convert("RGBA")
seg = lambda k, a, b: min(1, max(0, (k - a) / (b - a)))
ein = lambda k, p: min(1, max(0, k)) ** p
def frame(k):
    gy = 0 if k < 0.08 else 1120 * ein(seg(k, 0.08, 0.68), 2)
    zs = 1 + 0.7 * ein(seg(k, 0.7, 1), 2)
    fa = 1 - seg(k, 0.72, 1); ba = 1 - seg(k, 0.2, 0.7)
    tri = max(0, 1 - abs(k - 0.16) / 0.05); fl = 0.28 * tri
    base = game.copy()
    black = Image.new("RGBA", base.size, (10, 11, 15, int(255 * ba))); base.alpha_composite(black)
    z = Image.new("RGBA", base.size, (0, 0, 0, 0))
    z.alpha_composite(gr, (560, int(-gy)) if -gy > -1040 else (560, -2000))
    z.alpha_composite(fr)
    a = z.split()[3].point(lambda v: int(v * fa)); z.putalpha(a)
    pw, ph = 960, int(1080 * 0.352)
    z = z.resize((int(1920 * zs), int(1080 * zs)), Image.LANCZOS)
    ox, oy = int(960 - 960 * zs), int(380 - 380 * zs)
    out = base.copy(); out.alpha_composite(z, (max(ox, 0), max(oy, 0)), (max(-ox, 0), max(-oy, 0)))
    if fl > 0: out.alpha_composite(Image.new("RGBA", out.size, (184, 209, 255, int(255 * fl))))
    return out.convert("RGB")
ks = [0.05, 0.16, 0.25, 0.35, 0.45, 0.55, 0.65, 0.75, 0.88, 1.0]
fs = [frame(k).resize((480, 270)) for k in ks]
S = Image.new("RGB", (480 * 5, 270 * 2))
for i, f in enumerate(fs): S.paste(f, ((i % 5) * 480, (i // 5) * 270))
o = os.path.expanduser("~/grd/GRD_title_castle/gate_open_sim.png"); S.save(o); print(o)
