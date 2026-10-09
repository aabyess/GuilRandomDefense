"""워크래프트 기본 텍스처(맵 MPQ에 없음) 절차적 재현(2026-10-09, blender). /usr/bin/python3 Tools/w3x/gen_wc3_textures.py [--all]
이름이 뜻하는 모양을 따른다: 글로=방사 그라데이션, 충격파=링, 번개=가지 있는 번쩍임, 별=뾰족 별, 구름·연기·먼지=부드러운 노이즈, 거품, 불 고리.
RGB는 색×알파(=가산에서 검정 바탕), 알파=모양(블렌드에서 가장자리가 부드럽다). 출력: ~/GRD_blp_audit/placeholder_v2/<Assets 기준 상대 경로>.
기본: 글로·충격파·번개 / --all: + 별·구름·연기·먼지·거품·불고리. Assets에는 쓰지 않는다."""
import csv, math, os, random, re, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
H = os.path.expanduser("~"); OUT = H + "/GRD_blp_audit/placeholder_v2"
ALL = "--all" in sys.argv


def color_of(n):
    l = n.lower()
    for k, c in (("black", (0.05, 0.05, 0.08)), ("blue", (0.35, 0.55, 1.0)), ("ice", (0.6, 0.9, 1.0)), ("frost", (0.65, 0.9, 1.0)), ("water", (0.4, 0.7, 1.0)),
                 ("red", (1.0, 0.25, 0.2)), ("yellow", (1.0, 0.85, 0.3)), ("purple", (0.75, 0.35, 1.0)), ("green", (0.4, 1.0, 0.45)), ("fire", (1.0, 0.55, 0.2)),
                 ("flame", (1.0, 0.55, 0.2)), ("lava", (1.0, 0.4, 0.15)), ("white", (1.0, 1.0, 1.0))):
        if k in l: return np.array(c)
    return np.array((1.0, 1.0, 1.0))


def grid(S):
    y, x = np.mgrid[0:S, 0:S].astype(np.float32); x = (x + 0.5) / S * 2 - 1; y = (y + 0.5) / S * 2 - 1
    return x, y, np.sqrt(x * x + y * y), np.arctan2(y, x)


def pack(alpha, col, S, tint_core=0.0):
    a = np.clip(alpha, 0, 1)
    rgb = col[None, None, :] * (1 - tint_core * a[..., None]) + tint_core * a[..., None]       # 중심은 하양쪽으로(tint_core)
    out = np.dstack([np.clip(rgb * a[..., None], 0, 1), a])
    return Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA")


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0 + 1e-9), 0, 1); return t * t * (3 - 2 * t)


def noise(S, seed, octaves=5, base=4, persist=0.55):
    rnd = np.random.RandomState(seed); tot = np.zeros((S, S), np.float32); amp = 1.0; norm = 0
    for o in range(octaves):
        n = base * (2 ** o); g = (rnd.rand(n + 1, n + 1) * 255).astype(np.uint8)
        tot += np.asarray(Image.fromarray(g).resize((S, S), Image.BICUBIC), np.float32) / 255 * amp; norm += amp; amp *= persist
    return tot / norm


# ── 글로 ──
def glow(n, S=256):
    x, y, r, th = grid(S); l = n.lower(); col = color_of(n)
    if "faded" in l or "dim" in l: a = np.exp(-(r / 0.42) ** 2 * 2.2) * 0.6
    elif "glow2" in l or "glow3" in l or "magic" in l: a = 0.8 * np.exp(-(r / 0.26) ** 2 * 2.0) + 0.22 * np.exp(-(r / 0.5) ** 2 * 3.0)       # 밝은 심 + 좁은 후광
    elif "glowx" in l or "glow5" in l or "glow1" in l: a = 0.8 * np.exp(-(r / 0.35) ** 2 * 2.0) + 0.35 * (np.exp(-(x / 0.05) ** 2) * np.exp(-(y / 0.75) ** 2) + np.exp(-(y / 0.05) ** 2) * np.exp(-(x / 0.75) ** 2))
    elif "crescent" in l: a = np.exp(-(((r - 0.6) / 0.14) ** 2)) * smooth(-0.4, 0.5, x * 0.6 + 0.2)
    elif "barglow" in l: a = np.exp(-(y / 0.35) ** 2) * smooth(1.0, 0.7, np.abs(x))
    elif "rune" in l: a = np.exp(-(((r - 0.7) / 0.08) ** 2)) * (0.5 + 0.5 * np.cos(th * 8))
    else: a = np.exp(-(r / 0.4) ** 2 * 2.4)
    a = a * smooth(0.85, 0.5, r)                                                               # 가장자리 일찍 0
    return pack(a, col, S, tint_core=0.55)


# ── 충격파 ──
def shock(n, S=256):
    x, y, r, th = grid(S); l = n.lower(); col = color_of(n)
    side = np.where(r < 0.78, 0.04, 0.13)                                                       # 안쪽은 날카롭게, 바깥은 길게 번진다
    ring = np.exp(-(((r - 0.78) / side) ** 2)) + 0.12 * np.exp(-(((r - 0.55) / 0.2) ** 2))
    inner = smooth(0.1, 0.78, r) ** 2 * 0.2
    a = (ring + inner) * smooth(1.0, 0.7, r) * 0.45                                             # 반투명(원작 느낌): 알파 45%
    if "9" in l:                                                                                # 삼중 링: 간격 넓게, 안쪽일수록 흐리게
        a = (0.9 * np.exp(-(((r - 0.88) / 0.035) ** 2)) + 0.45 * np.exp(-(((r - 0.6) / 0.035) ** 2)) + 0.2 * np.exp(-(((r - 0.32) / 0.035) ** 2))) * 0.5
        a = a + 0.2 * np.exp(-(((r - 0.88) / 0.12) ** 2)) * (r > 0.88)
        a = a * smooth(1.0, 0.92, r)
    if "black" in l: col = np.array((0.1, 0.1, 0.14))
    return pack(a, col, S, tint_core=0.12)


# ── 번개 ──
def bolt_points(rnd, x0, y0, x1, y1, jag, depth):
    pts = [(x0, y0), (x1, y1)]
    for _ in range(depth):
        new = [pts[0]]
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            mx, my = (ax + bx) / 2, (ay + by) / 2; dx, dy = bx - ax, by - ay; ln = math.hypot(dx, dy) + 1e-6
            off = rnd.uniform(-1, 1) * jag * ln; new += [(mx - dy / ln * off, my + dx / ln * off), (bx, by)]
        pts = new
    return pts


def lightning(n, S=256):
    l = n.lower(); col = color_of(n); rnd = random.Random(abs(hash(n)) % 99991 if False else sum(map(ord, n)))
    SS = S * 2; core = Image.new("L", (SS, SS), 0); d = ImageDraw.Draw(core)
    if "ball" in l:
        for k in range(9):                                                                     # 공 + 사방 호
            a0 = rnd.uniform(0, 6.28); pts = bolt_points(rnd, SS / 2, SS / 2, SS / 2 + math.cos(a0) * SS * 0.42, SS / 2 + math.sin(a0) * SS * 0.42, 0.22, 5); d.line(pts, fill=255, width=3)
        glow_a = None
    elif "drain" in l or "spirit" in l:                                                        # 일렁이는 빛줄기
        xs = np.linspace(0, SS - 1, 200); ph = rnd.uniform(0, 6.28)
        d.line([(float(x), SS / 2 + math.sin(x / SS * 14 + ph) * SS * 0.07 * (1 + 0.5 * math.sin(x / SS * 5))) for x in xs], fill=255, width=4)
    else:                                                                                      # Zap/Lightning: 가로 번개(타일 길이 방향) + 가지
        main = bolt_points(rnd, 0, SS / 2, SS - 1, SS / 2, 0.16, 6); d.line(main, fill=255, width=4)
        for _ in range(5 if "fork" in l or "weather" in l else 3):
            i = rnd.randrange(len(main) // 5, len(main) * 4 // 5); bx, by = main[i]; ang = rnd.uniform(-0.9, 0.9) + (0 if rnd.random() < 0.5 else math.pi) * 0
            ex, ey = bx + SS * rnd.uniform(0.12, 0.28), by + math.sin(ang) * SS * 0.3; d.line(bolt_points(rnd, bx, by, ex, ey, 0.2, 4), fill=255, width=2)
    core = core.resize((S, S), Image.LANCZOS); c = np.asarray(core, np.float32) / 255
    halo = np.asarray(core.filter(ImageFilter.GaussianBlur(S * 0.018)), np.float32) / 255 * 1.6 + np.asarray(core.filter(ImageFilter.GaussianBlur(S * 0.05)), np.float32) / 255 * 1.2
    a = np.clip(np.maximum(c, halo), 0, 1)
    if "ball" in l:
        x, y, r, th = grid(S); a = np.clip(a + 0.8 * np.exp(-(r / 0.22) ** 2 * 2), 0, 1) * smooth(1.0, 0.8, r)
    if not any(k in l for k in ("red", "blue", "green", "purple", "yellow", "black")): col = np.array((0.62, 0.78, 1.0))   # 번개 기본 하늘빛 흰색
    return pack(a, col, S, tint_core=0.6 * (c.max() > 0))


# ── 별 ──
def star(n, S=256):
    x, y, r, th = grid(S); l = n.lower(); col = color_of(n); m = re.search(r"star(\d+)", l); k = int(m.group(1)) if m else 4
    pts = {1: 4, 2: 4, 3: 4, 4: 4, 6: 6, 7: 8, 8: 8, 10: 4, 11: 4, 32: 4}.get(k, 4)
    spikes = np.abs(np.cos(th * pts / 2)) ** 9
    a = np.exp(-(r / 0.5) ** 2 * 3.5) * 1.0 + spikes * np.exp(-(r / 0.8) ** 2 * 3.5) * 0.95
    return pack(a * smooth(1.0, 0.85, r), col, S, tint_core=0.7)


def sparkle(n, S=256):
    x, y, r, th = grid(S); col = color_of(n); a = np.exp(-(np.abs(x) / 0.09) ** 1.2) * np.exp(-(np.abs(y) / 0.7) ** 2 * 2) + np.exp(-(np.abs(y) / 0.09) ** 1.2) * np.exp(-(np.abs(x) / 0.7) ** 2 * 2) + np.exp(-(r / 0.28) ** 2 * 3)
    return pack(a * smooth(1.0, 0.85, r), col, S, tint_core=0.7)


# ── 구름·연기·먼지 ──
def puff(n, S=256, seed=0, contrast=1.0, dark=False, tint=None):
    x, y, r, th = grid(S); nz = noise(S, seed or sum(map(ord, n)), 6, 3, 0.55)
    a = smooth(0.12, 0.62, (nz - 0.5) * contrast * 1.6 + 0.5) * smooth(1.0, 0.3, r + (nz - 0.5) * 0.45)
    l = n.lower(); col = np.array((0.08, 0.08, 0.09)) if (dark or "black" in l) else (tint if tint is not None else color_of(n) * 0.0 + np.array((0.92, 0.92, 0.94)))
    shade = 0.75 + 0.25 * noise(S, seed + 7, 4, 4)
    img = pack(np.clip(a * 1.1, 0, 0.95), col, S); arr = np.asarray(img, np.float32) / 255; arr[..., :3] *= shade[..., None]; return Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")


def atlas(n, cells, S=512, **kw):
    c = S // cells; im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for i in range(cells * cells): im.paste(puff(n, c, seed=1000 + i, **kw), ((i % cells) * c, (i // cells) * c))
    return im


def dust(n, S=256):
    l = n.lower(); tint = np.array((0.75, 0.62, 0.45)) if "color" not in l or True else None
    if "red" in l: tint = np.array((0.9, 0.45, 0.3))
    if "black" in l: tint = np.array((0.1, 0.1, 0.1))
    return puff(n, S, contrast=0.8, tint=tint)


def foam(n, S=256):
    x, y, r, th = grid(S); rnd = np.random.RandomState(5); a = np.zeros((S, S), np.float32)
    for _ in range(90):
        cx, cy, rr = rnd.uniform(-0.85, 0.85, 2).tolist() + [rnd.uniform(0.03, 0.12)]; rr = rr; d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2)
        a = np.maximum(a, (smooth(rr, rr * 0.8, d) * 0.8 + np.exp(-(((d - rr) / (rr * 0.18)) ** 2)) * 0.5) * smooth(1.0, 0.6, np.sqrt(cx * cx + cy * cy)))
    return pack(np.clip(a, 0, 1) * 0.9, np.array((1.0, 1.0, 1.0)), S)


def firering(n, S=256):
    x, y, r, th = grid(S); nz = noise(S, 3, 5, 4); rr = 0.72 + (nz - 0.5) * 0.22
    a = np.exp(-(((r - rr) / 0.12) ** 2)) * (0.6 + 0.8 * nz) + 0.3 * np.exp(-(((r - rr) / 0.3) ** 2)); a = np.clip(a, 0, 1) * smooth(1.0, 0.85, r)
    col = np.array((1.0, 0.45, 0.12)); return pack(a, col, S, tint_core=0.55)


def classify(n):
    b = n.replace("textures_", "Textures_"); l = b.lower().split("textures_", 1)[-1]
    if "glow" in l and "star" not in l: return "glow", glow
    if "shockwave" in l: return "shock", shock
    if any(k in l for k in ("lightning", "zap")): return "light", lightning
    if "star" in l: return "star", star
    if l.startswith("sparkle"): return "star", sparkle
    if "cloud" in l:
        m = re.search(r"(\d)x(\d)", l)
        return "cloud", (lambda nn: atlas(nn, int(m.group(1)))) if m else (lambda nn: puff(nn))
    if "smoke" in l: return "smoke", (lambda nn: puff(nn, dark=True, contrast=0.9))
    if "dust" in l: return "dust", dust
    if "foam" in l: return "foam", foam
    if "firering" in l: return "firering", firering
    return None, None


if __name__ == "__main__":
    base = [x for x in csv.reader(open(H + "/GRD_blp_audit/audit.csv", encoding="utf-8-sig"))][1:]
    want = {"glow", "shock", "light"} | ({"star", "cloud", "smoke", "dust", "foam", "firering"} if ALL else set())
    made = {}; n_files = 0
    for p, kind, *_ in base:
        if kind != "BLP 못 찾음" or "/Assets/" not in p: continue
        nm = os.path.basename(p)[:-8]; fam, fn = classify(nm)
        if fam not in want: continue
        if nm not in made: made[nm] = fn(nm)
        rel = p.split("/Assets/")[1]; dst = os.path.join(OUT, rel); os.makedirs(os.path.dirname(dst), exist_ok=True); made[nm].save(dst); n_files += 1
        os.makedirs(OUT + "/_원본이름", exist_ok=True); made[nm].save(OUT + f"/_원본이름/{nm}.blp.png")
    print("텍스처 종", len(made), "Assets 파일", n_files)
