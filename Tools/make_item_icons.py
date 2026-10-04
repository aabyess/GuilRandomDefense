#!/usr/bin/python3
"""아이템 아이콘 35개(워크3 기본 아이콘 자리)를 game-icons.net(CC BY 3.0 / CC0) 그림으로 만든다 — 2026-10-04 사장님 「무료 아이콘으로 채운다」.

돌리기:  /usr/bin/python3 Tools/make_item_icons.py          (Pillow는 /usr/bin/python3에만 있다. cairosvg 없이 macOS qlmanage로 SVG→PNG)
입력:    https://raw.githubusercontent.com/game-icons/icons/master/<작가>/<이름>.svg  (내려받아 ~/GRD_motion_trial/아이템아이콘/_svg/에 둔다)
출력:    ~/GRD_motion_trial/아이템아이콘/I0xx_<이름>.png (64×64) · 128/I0xx_<이름>.png (128×128) · _sheet.png(35개 한 장) · credits.md
         맵에서 꺼낸 원작 3개(I00H·I00T·I00Y)는 건드리지 않는다.
LinkItemIcons(ItemIconLinker)가 이 폴더의 I*.png를 코드로 매칭해 Assets/Art/Items/로 복사·연결한다.
워크3 버튼 느낌: 어두운 색조 바탕 + 아이템 성격별 색의 그림 + 그림자 + 베벨 테두리.
"""
import os, subprocess, sys, urllib.request, tempfile, shutil
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageFont

OUT = os.path.expanduser('~/GRD_motion_trial/아이템아이콘')
SVG = os.path.join(OUT, '_svg')
BASE = 'https://raw.githubusercontent.com/game-icons/icons/master/'

# 성격별 색(그림 색) — 무기 은색 · 도박 금색 · 소모품 초록 · 마법 보라 · 배 파랑 · 가죽·목재 갈색 · 문서 양피지 · 도구 황동 · 자석 강청 · 근력 주황
COLORS = {
    'weapon': (205, 214, 228), 'gamble': (240, 196, 64), 'consume': (118, 214, 118), 'magic': (178, 128, 240),
    'ship': (96, 168, 232), 'leather': (200, 140, 84), 'wood': (196, 140, 80), 'paper': (226, 196, 140),
    'tool': (214, 168, 96), 'steel': (150, 190, 220), 'power': (240, 120, 70), 'storm': (120, 180, 240),
}

# (코드, 이름, 작가/그림, 색) — 이름은 ItemData 에셋 이름 그대로(ItemIconLinker가 코드 접두로 찾는다). 원작 iico 단서는 README 표에.
ICONS = [
    ('I000', '목재구입',            'delapouite/wood-pile',               'wood'),      # BTNBundleOfLumber
    ('I001', '힘-기본공격력증가',    'delapouite/biceps',                  'power'),     # (iico 비어 있음)
    ('I002', '찾은보물개수',        'lorc/treasure-map',                  'paper'),     # BTNGlyph
    ('I003', '명검-흑도슈스이',      'delapouite/katana',                  'weapon'),    # BTNAdvancedCreatureCarapace
    ('I004', '폐함대선',            'lorc/galleon',                       'ship'),      # BTNHumanTransport
    ('I005', '포식한영혼',          'delapouite/soul-vessel',             'magic'),     # BTNUrnOfKelThuzad
    ('I006', '돈도박초급',          'skoll/open-treasure-chest',          'gamble'),    # BTNChestOfGold
    ('I007', '태양신의흔적',        'lorc/sunbeams',                      'gamble'),    # BTNPeriapt
    ('I008', '저지-거프',           'lorc/gavel',                         'weapon'),    # BTNNecromancer
    ('I009', '유닛군업글',          'delapouite/armor-upgrade',           'weapon'),    # BTNManual3
    ('I00A', '고급유닛생성',        'lorc/magic-swirl',                   'magic'),     # BTNSpellShieldAmulet
    ('I00B', '퇴치-해적단퇴치',     'delapouite/pirate-flag',             'ship'),      # BTNHumanBattleShip
    ('I00C', '특성포인트구매',      'delapouite/upgrade',                 'consume'),   # BTNStatUp
    ('I00D', '돈도박중급리메이크',   'delapouite/coins-pile',              'gamble'),    # BTNChestOfGold
    ('I00E', '랜덤특수도박',        'delapouite/perspective-dice-six-faces-random', 'gamble'),   # BTNChestOfGold
    ('I00F', '다른세계도박',        'lorc/portal',                        'magic'),     # BTNMechanicalCritter
    ('I00G', '자성물체-키드초월',    'lorc/magnet',                        'steel'),     # BTNTinyCastle
    ('I00I', '흡수한양분',          'zeromancer/heart-plus',              'consume'),   # BTNReplenishHealth
    ('I00J', '황금빛전보벌레',      'lorc/snail',                         'gamble'),    # BTNSpellShieldAmulet
    ('I00K', '탐사도구',            'delapouite/telescope',               'tool'),      # BTNTelescope
    ('I00L', '버스터콜지령서',      'lorc/scroll-unfurled',               'paper'),     # BTNFlare
    ('I00M', '약주',                'seregacthtuf/sake-bottle',           'consume'),   # BTNPotionOfRestoration
    ('I00N', '무장도끼',            'lorc/battle-axe',                    'weapon'),    # BTNOrcMeleeUpOne
    ('I00O', '표식새김',            'delapouite/crosshair',               'weapon'),    # BTNMarksmanship
    ('I00P', '둔화의지팡이',        'lorc/wizard-staff',                  'storm'),     # BTNEntrapmentWard
    ('I00Q', '하늘섬전사의창',      'lorc/spear-hook',                    'weapon'),    # BTNAdvancedStrengthOfTheMoon
    ('I00R', '가죽장갑',            'delapouite/gloves',                  'leather'),   # BTNGlove
    ('I00S', '고대의배',            'delapouite/sailboat',                'ship'),      # BTNShip
    ('I00U', '비구름생성기',        'lorc/lightning-storm',               'storm'),     # BTNLionHorn
    ('I00V', '거인족의술잔',        'lorc/jeweled-chalice',               'gamble'),    # BTNBronzeBowlfull
    ('I00W', '보급의료품',          'delapouite/first-aid-kit',           'consume'),   # BTNPotionGreenSmall
    ('I00X', '초급공학도구',        'delapouite/toolbox',                 'tool'),      # BTNNecromancerAdept
    ('I00Z', '수배서',              'delapouite/wanted-reward',           'paper'),     # BTNBansheeMaster
    ('I010', '초대형자성물질',      'lorc/magnet-blast',                  'steel'),     # BTNAdvancedFlameTower
    ('I011', '위습꾸러미',          'lorc/swap-bag',                      'magic'),     # BTNPackBeast
]

# 작가 표기(game-icons.net license.txt) — CC BY 3.0, 단 Zeromancer는 CC0
AUTHORS = {
    'lorc': ('Lorc', 'http://lorcblog.blogspot.com', 'CC BY 3.0'),
    'delapouite': ('Delapouite', 'http://delapouite.com', 'CC BY 3.0'),
    'skoll': ('Skoll', 'https://game-icons.net/tags/skoll.html', 'CC BY 3.0'),
    'zeromancer': ('Zeromancer', 'https://game-icons.net', 'CC0 1.0'),
    'seregacthtuf': ('SeregaCthtuf', 'https://game-icons.net', 'CC BY 3.0'),
}


def fetch_svg(path):
    local = os.path.join(SVG, path.replace('/', '__') + '.svg')
    if not os.path.exists(local):
        os.makedirs(SVG, exist_ok=True)
        with urllib.request.urlopen(BASE + path + '.svg', timeout=60) as r:
            open(local, 'wb').write(r.read())
    return local


def render_mask(svg_path, size=512):
    """SVG(검은 바탕 + 흰 그림)를 qlmanage로 그려 밝기를 알파로 쓴다 → 'L' 마스크."""
    tmp = tempfile.mkdtemp()
    try:
        subprocess.run(['qlmanage', '-t', '-s', str(size), '-o', tmp, svg_path], check=True, capture_output=True)
        png = os.path.join(tmp, os.path.basename(svg_path) + '.png')
        return Image.open(png).convert('L')
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def compose(mask, color, size=128):
    """워크3 버튼 느낌 한 장 — 어두운 색조 바탕·그림자·색 그림·베벨 테두리."""
    S = size
    img = Image.new('RGBA', (S, S))
    top, bottom = lerp((12, 12, 14), color, 0.30), lerp((6, 6, 8), color, 0.10)
    px = img.load()
    for y in range(S):
        c = lerp(top, bottom, y / (S - 1))
        for x in range(S):
            px[x, y] = c + (255,)
    # 가운데 은은한 빛
    glow = Image.new('L', (S, S), 0)
    ImageDraw.Draw(glow).ellipse((S * 0.18, S * 0.12, S * 0.82, S * 0.76), fill=70)
    glow = glow.filter(ImageFilter.GaussianBlur(S * 0.12))
    img = Image.composite(Image.new('RGBA', (S, S), lerp(color, (255, 255, 255), 0.2) + (255,)), img, glow.point(lambda v: int(v * 0.28)))

    # 그림 — 마스크를 안쪽 여백만 남기고 줄여 얹는다. 위쪽은 밝게, 아래쪽은 색 그대로(살짝 입체감)
    inner = int(S * 0.78)
    m = mask.resize((inner, inner), Image.LANCZOS)
    full = Image.new('L', (S, S), 0)
    off = (S - inner) // 2
    full.paste(m, (off, off))
    shadow = ImageChops.offset(full, int(S * 0.025), int(S * 0.03)).filter(ImageFilter.GaussianBlur(S * 0.018)).point(lambda v: int(v * 0.85))
    img.paste(Image.new('RGBA', (S, S), (0, 0, 0, 255)), (0, 0), shadow)
    glyph = Image.new('RGBA', (S, S))
    gp = glyph.load()
    light, dark = lerp(color, (255, 255, 255), 0.55), lerp(color, (0, 0, 0), 0.18)
    for y in range(S):
        c = lerp(light, dark, y / (S - 1))
        for x in range(S):
            gp[x, y] = c + (255,)
    img.paste(glyph, (0, 0), full)

    # 베벨 테두리: 바깥 검은선 · 위·왼 밝게 · 아래·오른 어둡게
    d = ImageDraw.Draw(img)
    t = max(2, S // 40)
    hi, lo = lerp(color, (255, 255, 255), 0.45) + (200,), (0, 0, 0, 170)
    for i in range(t):
        d.line([(i, i), (S - 1 - i, i)], fill=hi)
        d.line([(i, i), (i, S - 1 - i)], fill=hi)
        d.line([(i, S - 1 - i), (S - 1 - i, S - 1 - i)], fill=lo)
        d.line([(S - 1 - i, i), (S - 1 - i, S - 1 - i)], fill=lo)
    d.rectangle((0, 0, S - 1, S - 1), outline=(0, 0, 0, 255), width=max(1, S // 64))
    return img


def main():
    os.makedirs(os.path.join(OUT, '128'), exist_ok=True)
    made = []
    for code, name, path, cat in ICONS:
        svg = fetch_svg(path)
        icon = compose(render_mask(svg), COLORS[cat], 128)
        icon.save(os.path.join(OUT, '128', f'{code}_{name}.png'))
        icon.resize((64, 64), Image.LANCZOS).save(os.path.join(OUT, f'{code}_{name}.png'))
        made.append((code, name, path, cat))
        print('ok', code, name, path)

    # 한 장 시트(+ 원작 3개 — 맵에서 꺼낸 것 그대로 — 도 같이 보여 준다)
    originals = [('I00H', '우타의헤드셋'), ('I00T', '전용아이템-엔마'), ('I00Y', '불사조의깃털')]
    cells = [(c, n, os.path.join(OUT, '128', f'{c}_{n}.png')) for c, n, _, _ in made] + \
            [(c, n + ' (원작)', os.path.join(OUT, f'{c}_{n}.png')) for c, n in originals]
    cols = 8
    rows = (len(cells) + cols - 1) // cols
    cw, ch = 168, 168
    sheet = Image.new('RGB', (cols * cw, rows * ch), (26, 24, 22))
    try:
        font = ImageFont.truetype('/System/Library/Fonts/AppleSDGothicNeo.ttc', 15)
    except Exception:
        font = ImageFont.load_default()
    dr = ImageDraw.Draw(sheet)
    for i, (c, n, p) in enumerate(cells):
        im = Image.open(p).convert('RGBA')
        if im.size[0] != 128: im = im.resize((128, 128), Image.NEAREST)
        x, y = (i % cols) * cw + 20, (i // cols) * ch + 8
        sheet.paste(im, (x, y), im)
        dr.text((x - 12, y + 132), f'{c} {n}', fill=(230, 220, 200), font=font)
    sheet.save(os.path.join(OUT, '_sheet.png'))

    # 출처 표(CC BY 필수 표기) — Docs/CREDITS.md에 옮길 내용
    lines = ['| 아이템 | 그림 | 작가 | 라이선스 | URL |', '|---|---|---|---|---|']
    for code, name, path, cat in made:
        a = path.split('/')[0]
        an, au, lic = AUTHORS[a]
        lines.append(f'| {code} {name} | {path.split("/")[1]} | {an} | {lic} | https://game-icons.net/1x1/{a}/{path.split("/")[1]}.html |')
    open(os.path.join(OUT, 'credits.md'), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print('시트', os.path.join(OUT, '_sheet.png'), '· 아이콘', len(made))


if __name__ == '__main__':
    main()
