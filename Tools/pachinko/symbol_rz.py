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
    "pink": ["#4a0036", "#9a0f78", "#e0309e", "#ff5cbc", "#ff9ed8", "#ffe4f4"],
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
                t = 0.12 + 0.62 * dirl + rnd.uniform(-0.42, 0.44)
                if rnd.random() < 0.15: t = 0.98                                              # 번쩍이는 면
                pts = " ".join(f"{p[0]:.1f},{p[1]:.1f}" for p in tri)
                out.append(f'<polygon points="{pts}" fill="{ramp(pal, t)}" stroke="{ramp(pal, min(1, t + .25))}" stroke-opacity=".55" stroke-width="1.1" stroke-linejoin="bevel"/>')
    return "".join(out)
def star(x, y, r, op=1):
    return (f'<path transform="translate({x} {y})" opacity="{op}" fill="#fff" d="M0,{-r} L{r*.14},{-r*.14} L{r},0 L{r*.14},{r*.14} L0,{r} L{-r*.14},{r*.14} L{-r},0 L{-r*.14},{-r*.14}Z"/>'
            f'<circle cx="{x}" cy="{y}" r="{r*.22}" fill="#fff" opacity="{op}"/>')
SPARK = {0: [(215, 215, 30), (400, 300, 18), (300, 420, 14)], 1: [(430, 205, 28), (200, 340, 17), (320, 250, 12)]}
SIDE = {"ruby": ("#3a0008", "#8a1020"), "sapphire": ("#06103e", "#1a3a96"), "gold": ("#4a2a00", "#a8680a"), "pink": ("#3a0028", "#9a1a78")}
def page(n, frame=0, pink=False):
    kind = "pink" if pink else ("gold" if n == 7 else ("ruby" if n % 2 else "sapphire")); pal = PAL[kind]
    fam = "Abril Fatface, Bodoni Moda, serif" if n == 7 else "Bodoni Moda, Didot, serif"
    wt = 400 if n == 7 else 900
    sk = SIDE[kind]
    G = "transform='translate(300 430) skewX(-12)' text-anchor='middle' font-family=\"%s\" font-weight='%d' font-size='350'" % (fam, wt)
    T = lambda extra="": f"<text x='0' y='0' {G} {extra}>{n}</text>"
    ext = ""; DEP = 44
    for i in range(DEP, 0, -1):                    # 눌린 두께: 보석색 어두운 톤(오른쪽 아래), 맨 끝은 은 테 반사
        t = i / DEP; c0, c1 = hx(sk[0]), hx(sk[1])
        col = "#%02x%02x%02x" % tuple(int(c1[k] + (c0[k] - c1[k]) * t) for k in range(3))
        if i == DEP: col = "#c9d8f5"
        ext += f"<g transform='translate({i * 0.95:.1f} {i * 1.3:.1f})'><text x='0' y='0' transform='translate(300 430) skewX(-12)' text-anchor='middle' font-family=\"{fam}\" font-weight='{wt}' font-size='350' fill='{col}' stroke='{col}' stroke-width='{30 + INF}' stroke-linejoin='round'>{n}</text></g>"
    kanji = ""
    if n == 7:
        kanji = ("<g transform='translate(430 232) rotate(8)'>"
                 "<text x='0' y='0' text-anchor='middle' font-family=\"Dela Gothic One\" font-size='135' fill='#3a0a00' stroke='#3a0a00' stroke-width='26' stroke-linejoin='round'>強</text>"
                 "<text x='0' y='0' text-anchor='middle' font-family=\"Dela Gothic One\" font-size='135' fill='url(#silver)' stroke='url(#silver)' stroke-width='16' stroke-linejoin='round'>強</text>"
                 "<text x='0' y='0' text-anchor='middle' font-family=\"Dela Gothic One\" font-size='135' fill='url(#gold)' stroke='#5a1000' stroke-width='5' paint-order='stroke' stroke-linejoin='round'>強</text>"
                 "<text x='0' y='0' text-anchor='middle' font-family=\"Dela Gothic One\" font-size='135' fill='none' stroke='#fff6c8' stroke-width='1.6'>強</text></g>")
    stars = "".join(star(x, y, r) for x, y, r in SPARK[frame % 2]) if frame >= 0 else ""
    return f"""<!doctype html><meta charset=utf8><style>
@font-face{{font-family:'Bodoni Moda';src:url('file://{OUT}/fonts/BodoniModa.ttf');font-weight:400 900}}
@font-face{{font-family:'Abril Fatface';src:url('file://{OUT}/fonts/AbrilFatface-Regular.ttf')}}
@font-face{{font-family:'Dela Gothic One';src:url('file://{OUT}/fonts/DelaGothicOne-Regular.ttf')}}
html,body{{margin:0;background:transparent}}</style>
<svg xmlns='http://www.w3.org/2000/svg' width='1024' height='1024' viewBox='0 0 600 600'><defs>
<linearGradient id='silver' x1='0' y1='90' x2='0' y2='500' gradientUnits='userSpaceOnUse'><stop offset='0' stop-color='#d8e4ff'/><stop offset='.18' stop-color='#5f7fc4'/><stop offset='.4' stop-color='#c4d4f8'/><stop offset='.62' stop-color='#33508f'/><stop offset='.82' stop-color='#8fa8e0'/><stop offset='1' stop-color='#eef4ff'/></linearGradient>
<linearGradient id='gold' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#fff6c0'/><stop offset='.5' stop-color='#f0b020'/><stop offset='1' stop-color='#a85a00'/></linearGradient>
<linearGradient id='hi' x1='60' y1='60' x2='560' y2='520' gradientUnits='userSpaceOnUse'><stop offset='0' stop-color='#fff'/><stop offset='.35' stop-color='#fff' stop-opacity='0'/><stop offset='.7' stop-color='#fff' stop-opacity='0'/><stop offset='1' stop-color='#fff' stop-opacity='.85'/></linearGradient><clipPath id='face'>{T("stroke='#000' stroke-width='%d' stroke-linejoin='round'" % INF)}</clipPath></defs>
{ext}
{T("fill='url(#silver)' stroke='url(#silver)' stroke-width='%d' stroke-linejoin='round'" % (30 + INF))}
{T("fill='none' stroke='url(#hi)' stroke-width='%d' stroke-linejoin='round'" % (30 + INF - 14))}
{T("fill='#0b1030' stroke='#0b1030' stroke-width='%d' stroke-linejoin='round'" % (10 + INF))}
{T("fill='%s' stroke='%s' stroke-width='%d' stroke-linejoin='round'" % (pal[2], pal[2], INF))}
<g clip-path='url(#face)'>{facets(pal, n * 17 + 3)}
<path d='M60,90 L560,90 L560,200 L60,270Z' fill='#fff' opacity='.10'/></g>
{kanji}{stars}
</svg>"""
def render(n, frame=0, name=None, pink=False):
    html = f"{OUT}/_html/{name or f'symbol_{n}'}.html"; open(html, "w").write(page(n, frame, pink))
    png = f"{OUT}/{name or f'symbol_{n}'}.png"
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000", "--window-size=1024,1024", "--virtual-time-budget=3000", f"--screenshot={png}", f"file://{html}"], capture_output=True)
    return png
def motion_blur(im, px=90, n=41):
    arr = np.asarray(im.convert("RGBA")).astype("float32"); acc = np.zeros(arr.shape[:2] + (3,), "float32"); ws = np.zeros(arr.shape[:2] + (1,), "float32")
    for k in range(n):
        sh = np.roll(arr, int(-px + 2 * px * k / (n - 1)), axis=0); a = sh[..., 3:4] / 255; acc += sh[..., :3] * a; ws += a
    rgb = acc / np.maximum(ws, 1e-3); al = (ws / n * 255 * 1.25).clip(0, 255)
    return Image.fromarray(np.concatenate([rgb, al], -1).clip(0, 255).astype("uint8")).convert("RGBA").filter(ImageFilter.GaussianBlur(2))
def fit_all(files_by_n, top=205, side=154, bottom=1024 - 100):
    """모든 숫자·프레임에 같은 배율을 줘(캔버스 중앙 기준) 여백 기준(위 20%·좌우 15%)을 맞춘다."""
    s = 1.0
    for fs in files_by_n.values():
        for f in fs:
            b = Image.open(f).getchannel("A").getbbox()
            s = min(s, (512 - side) / max(1, 512 - b[0]), (1024 - side - 512) / max(1, b[2] - 512), (512 - top) / max(1, 512 - b[1]))
    if s < 0.999:
        for fs in files_by_n.values():
            for f in fs:
                im = Image.open(f).convert("RGBA"); k = int(1024 * s); sm = im.resize((k, k), Image.LANCZOS)
                o = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0)); o.paste(sm, ((1024 - k) // 2, (1024 - k) // 2)); o.save(f)
    return s
def make(ns, pink=False):
    suf = "_pink" if pink else ""; files = {}
    for n in ns:
        base = f"symbol_{n}{suf}"
        render(n, -1, base, pink)
        fr = [f"{OUT}/{base}.png"]
        if not pink:
            for f in (0, 1): render(n, f, f"{base}_s{f}", pink); fr.append(f"{OUT}/{base}_s{f}.png")
        files[n] = fr
    sc = fit_all(files)
    for n in ns:
        f0 = files[n][0]; im = Image.open(f0).convert("RGBA"); bb = im.getchannel("A").getbbox()
        if not pink: motion_blur(im).save(f"{OUT}/symbol_{n}_blur.png")
        print(n, suf, "bbox", bb, "여백 위", bb[1], "좌", bb[0], "우", 1024 - bb[2], "배율", round(sc, 3))
if __name__ == "__main__":
    args = sys.argv[1:]; pink = "--pink" in args; ns = [int(a) for a in args if a.isdigit()] or list(range(1, 10))
    make(ns, pink)

def sheet():
    """contact sheet: 1~9 · 분홍 변형 · 블러 · 반짝임 + 참고 프레임 나란히 → symbols_rz/contact_sheet_rz.png"""
    from PIL import ImageDraw, ImageFont
    F = ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", 22, index=0); C = 230
    bg = Image.new("RGB", (9 * C, 4 * (C + 30) + 380), (30, 30, 44)); d = ImageDraw.Draw(bg)
    for r, (suf, lab) in enumerate((("", "기본(홀수 루비·짝수 사파이어·7 금)"), ("_pink", "분홍 리치 변형"), ("_blur", "세로 모션블러"), ("_s1", "반짝임 프레임 s1"))):
        d.text((8, r * (C + 30) + 4), lab, font=F, fill=(255, 235, 170))
        for n in range(1, 10):
            f = f"{OUT}/symbol_{n}{suf}.png"
            if os.path.exists(f):
                im = Image.open(f).convert("RGBA").resize((C, C)); bg.paste(im, ((n - 1) * C, r * (C + 30) + 30), im)
    y = 4 * (C + 30); d.text((8, y + 2), "참고 프레임(원본 영상 캡처)", font=F, fill=(255, 235, 170))
    ref = Image.open(os.path.expanduser("~/GRD_pachinko_art/rezero_ref/06_numbers_closeup.jpg"))
    x = 0
    for box in ((640, 0, 1920, 360), (640, 720, 1920, 1080), (0, 360, 640, 720)):
        c = ref.crop(box); k = 330 / c.height; c = c.resize((int(c.width * k), 330)); bg.paste(c, (x, y + 34)); x += c.width + 10
    bg.save(OUT + "/contact_sheet_rz.png")
if __name__ == "__main__" and len(sys.argv) == 1: sheet()
