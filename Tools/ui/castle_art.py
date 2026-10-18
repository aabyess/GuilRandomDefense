"""성(石造) 테마 UI 그림 공용 부품 — 회색 돌벽·쇠 테두리·리벳·횃불 빛. numpy + PIL만 쓴다(/usr/bin/python3).
정본 스크립트: gen_castle_lobby.py(시작 화면) · gen_castle_door.py(문) · gen_castle_settings.py(설정 창). 전부 결정적(seed 고정)."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def rng(seed):
    return np.random.default_rng(seed)


def fbm(h, w, seed, octaves=5, base=6, persistence=0.55):
    """0..1 값 노이즈(여러 해상도 격자를 쌓아 쌍삼차로 키운다)."""
    r = rng(seed)
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        gh, gw = max(2, int(base * 2 ** o * h / max(h, w))), max(2, int(base * 2 ** o * w / max(h, w)))
        g = r.random((gh + 1, gw + 1)).astype(np.float32)
        im = Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        out += np.asarray(im, np.float32) / 255.0 * amp
        tot += amp
        amp *= persistence
    out /= tot
    lo, hi = out.min(), out.max()
    return (out - lo) / (hi - lo + 1e-6)


def to_img(a, mode="RGB"):
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def stone_wall(w, h, seed=1, bh=64, bw=120, tone=(0.46, 0.49, 0.53), mortar=0.18, contrast=0.55):
    """층층이 엇갈려 쌓은 회색 돌벽(RGB float 0..255)."""
    r = rng(seed)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    row = (yy // bh).astype(np.int32)
    nrows = int(h // bh) + 2
    row_off = r.random(nrows).astype(np.float32) * bw
    row_w = (bw * (0.75 + 0.5 * r.random(nrows))).astype(np.float32)
    ro = row_off[row]
    rw = row_w[row]
    u = np.mod(xx + ro, rw)
    col = np.floor((xx + ro) / rw).astype(np.int32)
    v = np.mod(yy, bh)
    # 블록마다 다른 밝기
    key = (row.astype(np.int64) * 7919 + col.astype(np.int64) * 104729 + seed * 13) % 1000003
    bt = ((key * 2654435761) % 1000) / 1000.0
    bt = (bt - 0.5) * 2 * 0.22 * contrast + 1.0
    edge = np.minimum(np.minimum(u, rw - u), np.minimum(v, bh - v))
    m = max(2.0, bh * 0.045)
    in_mortar = edge < m
    bev = np.clip((edge - m) / (bh * 0.16), 0, 1)
    light = 1.0 - 0.28 * (1 - bev) + 0.10 * (1 - v / bh) - 0.10 * (v / bh) * (1 - bev)
    tex = 0.72 + 0.5 * fbm(h, w, seed + 5, 5, 10) + 0.12 * (fbm(h, w, seed + 6, 3, 90) - 0.5)
    base = np.array(tone, np.float32) * 255
    img = base[None, None, :] * (bt * light * tex)[..., None]
    mc = np.array(tone, np.float32) * 255 * mortar
    img = np.where(in_mortar[..., None], mc[None, None, :] * (0.7 + 0.6 * tex[..., None]), img)
    return img


def iron_texture(w, h, seed=3, tone=(0.20, 0.21, 0.24)):
    """어두운 쇠판 — 가로 결 + 얼룩."""
    t = fbm(h, w, seed, 4, 5)
    streak = fbm(h, w, seed + 1, 3, 3)
    stre = np.asarray(to_img(streak * 255, "L").resize((w, h)).filter(ImageFilter.GaussianBlur(0.5)), np.float32) / 255
    g = 0.7 + 0.45 * t + 0.1 * (stre - 0.5)
    return np.array(tone, np.float32)[None, None, :] * 255 * g[..., None]


def chamfer_poly(w, h, c, inset=0):
    i = inset
    return [(i + c, i), (w - i - c, i), (w - i, i + c), (w - i, h - i - c), (w - i - c, h - i), (i + c, h - i), (i, h - i - c), (i, i + c)]


def mask_poly(w, h, pts, blur=0.0, ss=2):
    big = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(big).polygon([(x * ss, y * ss) for x, y in pts], fill=255)
    m = big.resize((w, h), Image.LANCZOS)
    if blur: m = m.filter(ImageFilter.GaussianBlur(blur))
    return np.asarray(m, np.float32) / 255.0


def bevel_light(mask, depth, dx=-1, dy=-1, strength=1.0):
    """마스크(0..1)에서 돋을새김 조명: 높이=마스크를 depth만큼 흐린 것, 왼쪽 위 빛."""
    hm = np.asarray(to_img(mask * 255, "L").filter(ImageFilter.GaussianBlur(depth)), np.float32) / 255.0
    gy, gx = np.gradient(hm)
    return np.clip(-(gx * dx + gy * dy) * depth * 2.4 * strength, -1, 1)


def composite(rgb, alpha):
    h, w = alpha.shape
    out = np.zeros((h, w, 4), np.float32)
    out[..., :3] = rgb
    out[..., 3] = alpha * 255
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def rivet(img_arr, cx, cy, r, tone=(150, 156, 166)):
    """리벳 하나(둥근 쇠못 + 아래쪽 그림자)를 float RGB 배열에 직접 그린다."""
    h, w, _ = img_arr.shape
    y0, y1, x0, x1 = int(max(0, cy - r * 2)), int(min(h, cy + r * 2 + 1)), int(max(0, cx - r * 2)), int(min(w, cx + r * 2 + 1))
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    d = np.hypot(xx - cx, yy - cy)
    sh = np.clip(1 - np.hypot(xx - cx - r * 0.35, yy - cy - r * 0.5) / (r * 1.5), 0, 1) * 0.5
    img_arr[y0:y1, x0:x1] *= (1 - sh)[..., None]
    dome = np.clip(1 - d / r, 0, 1)
    spec = np.clip(1 - np.hypot(xx - cx + r * 0.3, yy - cy + r * 0.3) / (r * 0.7), 0, 1)
    col = np.array(tone, np.float32)[None, None, :] * (0.45 + 0.55 * dome[..., None]) + 90 * spec[..., None] ** 2
    a = np.clip((r - d) / 1.2 + 0.5, 0, 1)[..., None]
    img_arr[y0:y1, x0:x1] = img_arr[y0:y1, x0:x1] * (1 - a) + col * a


def iron_panel(w, h, border, chamfer, seed=7, interior="dark", rivets=True, rim_tone=(0.30, 0.32, 0.36), tint=1.0, glow=0.0, inset_dark=False):
    """쇠 테두리 + 안쪽 판 하나. interior='dark'(어두운 쇠판) 또는 'stone'(어두운 돌벽). 투명 모서리(챔퍼)."""
    outer = mask_poly(w, h, chamfer_poly(w, h, chamfer))
    inner_pts = chamfer_poly(w, h, max(2, chamfer - border * 0.6), inset=border)
    inner = mask_poly(w, h, inner_pts)
    rim_m = np.clip(outer - inner, 0, 1)
    rim = iron_texture(w, h, seed, rim_tone)
    rim *= (1 + bevel_light(outer, border * 0.35, strength=1.2)[..., None] * 0.9)
    rim *= (1 + bevel_light(1 - inner, border * 0.25, strength=1.0)[..., None] * 0.7 * -1)
    if interior == "stone":
        fill = stone_wall(w, h, seed + 2, bh=max(24, h // 7), bw=max(40, w // 5), tone=(0.30, 0.32, 0.36), contrast=0.5) * 0.55
    else:
        fill = iron_texture(w, h, seed + 4, (0.09, 0.10, 0.12)) * 0.9
    fill *= (1 + bevel_light(inner, border * 0.3, dx=1, dy=1, strength=0.8)[..., None] * 0.7)
    if glow:
        ys = np.linspace(-1, 1, h)[:, None]
        xs = np.linspace(-1, 1, w)[None, :]
        fill = fill + glow * 60 * np.clip(1 - (xs ** 2 + ys ** 2), 0, 1)[..., None] * np.array([0.7, 0.85, 1.0])
    rgb = rim * rim_m[..., None] + fill * inner[..., None]
    rgb = rgb / np.maximum(outer[..., None], 1e-3) * 1.0
    rgb = np.where(outer[..., None] > 0, rgb, 0) * tint
    # 안쪽 가장자리 어둡게(깊이감)
    inner_shadow = 1 - 0.35 * np.clip(np.asarray(to_img(inner * 255, "L").filter(ImageFilter.GaussianBlur(border * 0.5)), np.float32) / 255, 0, 1)
    rgb = np.where(inner[..., None] > 0.5, rgb * (0.65 + 0.35 * inner_shadow[..., None] ** 0.5 * 1.0), rgb)
    if rivets:
        rr = max(3.0, border * 0.22)
        for cx, cy in [(border * 0.5 + chamfer * 0.35, border * 0.5 + chamfer * 0.35), (w - border * 0.5 - chamfer * 0.35, border * 0.5 + chamfer * 0.35),
                       (border * 0.5 + chamfer * 0.35, h - border * 0.5 - chamfer * 0.35), (w - border * 0.5 - chamfer * 0.35, h - border * 0.5 - chamfer * 0.35)]:
            rivet(rgb, cx, cy, rr)
    return composite(rgb, outer)


def vignette(arr, strength=0.5, power=2.2):
    h, w, _ = arr.shape
    ys = np.linspace(-1, 1, h)[:, None]
    xs = np.linspace(-1, 1, w)[None, :]
    v = 1 - strength * np.clip((xs ** 2 + ys ** 2) / 2, 0, 1) ** (power / 2)
    return arr * v[..., None]


def radial_glow(h, w, cx, cy, radius, color, power=2.0):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.clip(1 - np.hypot(xx - cx, yy - cy) / radius, 0, 1) ** power
    return d[..., None] * np.array(color, np.float32)[None, None, :]


def sheet(images, path, cols=4, cell=420, bg=(40, 42, 48)):
    """검증용 한 장짜리 격자(이름 붙임)."""
    from PIL import ImageFont
    rows = (len(images) + cols - 1) // cols
    S = Image.new("RGB", (cols * cell, rows * (cell + 24)), bg)
    d = ImageDraw.Draw(S)
    for i, (name, im) in enumerate(images):
        im = im.convert("RGBA")
        sc = min((cell - 12) / im.width, (cell - 12) / im.height)
        t = im.resize((max(1, int(im.width * sc)), max(1, int(im.height * sc))), Image.LANCZOS)
        x, y = (i % cols) * cell, (i // cols) * (cell + 24)
        # 체커 바탕(투명 확인)
        chk = Image.new("RGB", t.size, (70, 70, 76))
        cd = ImageDraw.Draw(chk)
        for yy in range(0, t.height, 16):
            for xx in range(0, t.width, 16):
                if (xx // 16 + yy // 16) % 2: cd.rectangle([xx, yy, xx + 15, yy + 15], fill=(92, 92, 98))
        chk.paste(t, (0, 0), t)
        S.paste(chk, (x + (cell - t.width) // 2, y + 20 + (cell - t.height) // 2))
        d.text((x + 6, y + 4), f"{name} {im.width}x{im.height}", fill=(230, 230, 235))
    S.save(path)
