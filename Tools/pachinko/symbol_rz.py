"""리제로 테마 「보석 숫자」(10-14, blender): 굵은 이탤릭 세리프 + 은색 금속 베벨 테 + 각진 보석 면(패싯). SVG를 헤드리스 Chrome으로 투명 PNG 1024.
  /usr/bin/python3 Tools/pachinko/symbol_rz.py [숫자 ...]   (없으면 1~9)   출력 ~/GRD_pachinko_art/symbols_rz/
홀수 루비(빨강) · 짝수 사파이어(파랑) · 7 금 + 「強」. 산출: symbol_N.png · symbol_N_s0/s1.png(반짝임 2프레임, 별빛 위치만 다름) · symbol_N_blur.png(세로 모션블러)
폰트: Bodoni Moda(OFL, 가변 wght 900) · Dela Gothic One(OFL, 「強」) — <출력>/fonts/ 에 TTF+OFL.txt"""
import os, sys, math, random, subprocess
import numpy as np
from PIL import Image, ImageFilter
OUT = os.path.expanduser("~/GRD_pachinko_art/symbols_rz"); os.makedirs(OUT + "/_html", exist_ok=True)
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
PAL = {  # 어두움 → 밝음
    "ruby": ["#5a0010", "#9a0a1c", "#d6122a", "#ff3a3a", "#ff7a6a", "#ffd0c8"],
    "sapphire": ["#06185e", "#0d3bb5", "#1f6bff", "#4fa6ff", "#8fd8ff", "#e4f6ff"],
    "gold": ["#6a3d00", "#a8680a", "#e0a020", "#ffd54a", "#fff0a0", "#fffbe8"],
}
INF = 30   # 글자 윤곽 부풀림(보도니의 가는 획이 보석 면을 못 담아서)
def hx(c): return tuple(int(c[i:i + 2], 16) for i in (1, 3, 5))
def ramp(pal, t):
    t = min(max(t, 0), 0.9999) * (len(pal) - 1); i = int(t); f = t - i; a, b = hx(pal[i]), hx(pal[i + 1])
    return "#%02x%02x%02x" % tuple(int(a[k] + (b[k] - a[k]) * f) for k in range(3))
def facets(pal, seed):
    """글자 영역을 덮는 지터 격자 삼각 패싯. 빛은 왼쪽 위 — 방향 그라데이션 + 면마다 무작위 밝기."""
    rnd = random.Random(seed); x0, x1, y0, y1 = 10, 600, 50, 560; cols, rows = 20, 16
    P = [[(x0 + (x1 - x0) * i / cols + (rnd.uniform(-1, 1) * 13 if 0 < i < cols else 0), y0 + (y1 - y0) * j / rows + (rnd.uniform(-1, 1) * 13 if 0 < j < rows else 0)) for i in range(cols + 1)] for j in range(rows + 1)]
    out = []
    for j in range(rows):
        for i in range(cols):
            a, b, c, d = P[j][i], P[j][i + 1], P[j + 1][i + 1], P[j + 1][i]
            tris = [(a, b, c), (a, c, d)] if rnd.random() < .5 else [(a, b, d), (b, c, d)]
            for tri in tris:
                cx = sum(p[0] for p in tri) / 3; cy = sum(p[1] for p in tri) / 3
                dirl = 1 - ((cx - x0) / (x1 - x0) * 0.5 + (cy - y0) / (y1 - y0) * 0.5)      # 왼쪽 위가 밝음
                t = 0.18 + 0.50 * dirl + rnd.uniform(-0.34, 0.36)
                if rnd.random() < 0.12: t = 0.97                                              # 번쩍이는 면
                pts = " ".join(f"{p[0]:.1f},{p[1]:.1f}" for p in tri)
                out.append(f'<polygon points="{pts}" fill="{ramp(pal, t)}" stroke="{ramp(pal, min(1, t + .25))}" stroke-opacity=".55" stroke-width="1.1" stroke-linejoin="bevel"/>')
    return "".join(out)
def star(x, y, r, op=1):
    return (f'<path transform="translate({x} {y})" opacity="{op}" fill="#fff" d="M0,{-r} L{r*.14},{-r*.14} L{r},0 L{r*.14},{r*.14} L0,{r} L{-r*.14},{r*.14} L{-r},0 L{-r*.14},{-r*.14}Z"/>'
            f'<circle cx="{x}" cy="{y}" r="{r*.22}" fill="#fff" opacity="{op}"/>')
SPARK = {0: [(215, 215, 30), (400, 300, 18), (300, 420, 14)], 1: [(430, 205, 28), (200, 340, 17), (320, 250, 12)]}
def page(n, frame=0):
    kind = "gold" if n == 7 else ("ruby" if n % 2 else "sapphire"); pal = PAL[kind]
    fam = "Bodoni Moda, Didot, serif"
    G = "transform='translate(300 440) skewX(-12)' text-anchor='middle' font-family=\"%s\" font-weight='900' font-size='370'" % fam
    T = lambda extra="": f"<text x='0' y='0' {G} {extra}>{n}</text>"
    ext = ""
    for i in range(18, 0, -1):                     # 눌린 두께(오른쪽 아래로) — 어두운 강철빛
        t = i / 18; col = "#%02x%02x%02x" % (int(70 + 40 * (1 - t)), int(86 + 50 * (1 - t)), int(130 + 50 * (1 - t)))
        ext += f"<g transform='translate({i * 0.9:.1f} {i * 1.15:.1f})'><text x='0' y='0' transform='translate(300 440) skewX(-12)' text-anchor='middle' font-family=\"{fam}\" font-weight='900' font-size='370' fill='{col}' stroke='{col}' stroke-width='{28 + INF}' stroke-linejoin='round'>{n}</text></g>"
    kanji = ""
    if n == 7:
        kanji = ("<g transform='translate(105 180) rotate(-10)'><text x='0' y='0' text-anchor='middle' font-family=\"Dela Gothic One\" font-size='118' "
                 "fill='url(#gold)' stroke='#5a1000' stroke-width='9' paint-order='stroke' stroke-linejoin='round'>強</text>"
                 "<text x='0' y='0' text-anchor='middle' font-family=\"Dela Gothic One\" font-size='118' fill='none' stroke='#fff6c8' stroke-width='2'>強</text></g>")
    stars = "".join(star(x, y, r) for x, y, r in SPARK[frame % 2]) if frame >= 0 else ""
    return f"""<!doctype html><meta charset=utf8><style>
@font-face{{font-family:'Bodoni Moda';src:url('file://{OUT}/fonts/BodoniModa.ttf');font-weight:400 900}}
@font-face{{font-family:'Dela Gothic One';src:url('file://{OUT}/fonts/DelaGothicOne-Regular.ttf')}}
html,body{{margin:0;background:transparent}}</style>
<svg xmlns='http://www.w3.org/2000/svg' width='1024' height='1024' viewBox='0 0 600 600'><defs>
<linearGradient id='silver' x1='0' y1='90' x2='0' y2='500' gradientUnits='userSpaceOnUse'><stop offset='0' stop-color='#eef4ff'/><stop offset='.22' stop-color='#6f8fd0'/><stop offset='.45' stop-color='#dfe9ff'/><stop offset='.7' stop-color='#4a66a8'/><stop offset='1' stop-color='#b9ccf5'/></linearGradient>
<linearGradient id='gold' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#fff6c0'/><stop offset='.5' stop-color='#f0b020'/><stop offset='1' stop-color='#a85a00'/></linearGradient>
<clipPath id='face'>{T("stroke='#000' stroke-width='%d' stroke-linejoin='round'" % INF)}</clipPath></defs>
{ext}
{T("fill='url(#silver)' stroke='url(#silver)' stroke-width='%d' stroke-linejoin='round'" % (30 + INF))}
{T("fill='#0b1030' stroke='#0b1030' stroke-width='%d' stroke-linejoin='round'" % (10 + INF))}
{T("fill='%s' stroke='%s' stroke-width='%d' stroke-linejoin='round'" % (pal[2], pal[2], INF))}
<g clip-path='url(#face)'>{facets(pal, n * 17 + 3)}
<path d='M60,90 L560,90 L560,200 L60,270Z' fill='#fff' opacity='.10'/></g>
{kanji}{stars}
</svg>"""
def render(n, frame=0, name=None):
    html = f"{OUT}/_html/{name or f'symbol_{n}'}.html"; open(html, "w").write(page(n, frame))
    png = f"{OUT}/{name or f'symbol_{n}'}.png"
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000", "--window-size=1024,1024", "--virtual-time-budget=3000", f"--screenshot={png}", f"file://{html}"], capture_output=True)
    return png
def motion_blur(im, px=90, n=41):
    arr = np.asarray(im.convert("RGBA")).astype("float32"); acc = np.zeros(arr.shape[:2] + (3,), "float32"); ws = np.zeros(arr.shape[:2] + (1,), "float32")
    for k in range(n):
        sh = np.roll(arr, int(-px + 2 * px * k / (n - 1)), axis=0); a = sh[..., 3:4] / 255; acc += sh[..., :3] * a; ws += a
    rgb = acc / np.maximum(ws, 1e-3); al = (ws / n * 255 * 1.25).clip(0, 255)
    return Image.fromarray(np.concatenate([rgb, al], -1).clip(0, 255).astype("uint8")).convert("RGBA").filter(ImageFilter.GaussianBlur(2))
if __name__ == "__main__":
    ns = [int(a) for a in sys.argv[1:]] or list(range(1, 10))
    for n in ns:
        p = render(n, -1)                                            # 기본판: 별빛 없음
        for f in (0, 1): render(n, f, f"symbol_{n}_s{f}")
        im = Image.open(p).convert("RGBA"); bb = im.getchannel("A").getbbox()
        motion_blur(im).save(f"{OUT}/symbol_{n}_blur.png"); print(n, "bbox", bb, "여백 위", bb[1], "좌", bb[0], "우", 1024 - bb[2])
