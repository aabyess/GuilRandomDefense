"""파칭코 숫자 v3(10-14, blender): 사장님 확정 웹 시안(digits.html, claude.ai/artifact/VFAuaknTYmQN2uHaZRmmhp)의 SVG를 그대로 단독 페이지로 렌더.
  /usr/bin/python3 Tools/pachinko/symbol_v3.py [출력폴더=~/GRD_pachinko_art/symbols_v3]
폰트: Bodoni Moda(OFL, 가변 wght 900) · Abril Fatface(OFL, 7) — <출력>/fonts/ 에 TTF+OFL.txt. 렌더: 헤드리스 Chrome 투명 PNG 1024.
산출: symbol_N.png(홀수·7 = f0) · symbol_N_f0~f5.png(불꽃 6프레임, feTurbulence seed 바꿈) · 크리스털(짝수) symbol_N.png + symbol_N_s0/s1(별빛 반짝) · symbol_N_blur.png(세로 모션블러)"""
import os, re, subprocess, sys, json
OUT = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/GRD_pachinko_art/symbols_v3")
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
SRC = open(OUT + "/src/digits.html", encoding="utf8").read()
defs = re.search(r"<defs>(.*?)</defs>", SRC, re.S).group(1)
FIRE_SEEDS = [3, 9, 14, 5, 11, 17]
def fonts_css():
    return (f"@font-face{{font-family:'Bodoni Moda';src:url('file://{OUT}/fonts/BodoniModa.ttf');font-weight:400 900}}"
            f"@font-face{{font-family:'Abril Fatface';src:url('file://{OUT}/fonts/AbrilFatface-Regular.ttf')}}")
def scaled_defs(k, seed):
    """시안은 size 400 기준 필터 수치 — 렌더 크기(size)에 비례해 키운다. 불꽃 seed 고정(animate 제거)."""
    d = defs
    d = re.sub(r'<animate[^>]*/>', '', d)
    d = re.sub(r'(<feTurbulence[^>]*id="n"[^>]*)seed="\d+"', lambda m: m.group(1) + f'seed="{seed}"', d) if False else d
    # flame 필터의 seed
    d = re.sub(r'(<filter id="flame".*?<feTurbulence[^>]*?)seed="\d+"', lambda m: m.group(1) + f'seed="{seed}"', d, flags=re.S)
    d = re.sub(r'scale="(\d+)"', lambda m: f'scale="{int(m.group(1)) * k:.1f}"', d)
    d = re.sub(r'stdDeviation="([\d.]+)"', lambda m: f'stdDeviation="{float(m.group(1)) * k:.2f}"', d)
    d = re.sub(r'surfaceScale="([\d.]+)"', lambda m: f'surfaceScale="{float(m.group(1)) * k:.2f}"', d)
    d = d.replace('width="16" height="16"', f'width="{16 * k:.0f}" height="{16 * k:.0f}"')
    return d
def page(n, seed, sparkle_seed=None, size=640):
    k = size / 400
    kind = "gold" if n == 7 else ("fire" if n % 2 else "crystal")
    fam = "Abril Fatface, Bodoni Moda, serif" if n == 7 else "Bodoni Moda, Didot, serif"
    gsz = size * (1.05 if n == 7 else 1.0)
    T = lambda **a: "<text x='0' y='0' text-anchor='middle' font-family=\"%s\" font-weight='900' font-size='%s' %s>%d</text>" % (fam, gsz, " ".join(f'{kk.replace("_", "-")}="{v}"' for kk, v in a.items()), n)
    L = []
    if kind == "crystal":
        L += [T(fill="#7fe9ff", opacity=.28, filter="url(#glowC)"), T(fill="none", stroke="url(#aqua)", stroke_width=gsz * .035, opacity=.55, filter="url(#feather)", transform="translate(%s %s)" % (-6 * k, 4 * k)),
              T(fill="url(#crystal)", filter="url(#bevel)"), T(fill="url(#streak)", opacity=.35), T(fill="none", stroke="#ffffff", stroke_width=gsz * .006, opacity=.9),
              T(fill="none", stroke="#2ff0ff", stroke_width=gsz * .012, opacity=.75, filter="url(#feather)", transform="translate(%s %s)" % (8 * k, -6 * k))]
    elif kind == "gold":
        L += [T(fill="#ff2a10", opacity=.55, filter="url(#glowF)", transform="scale(1.08)"), T(fill="#ffd000", opacity=.95, filter="url(#flame)", transform="translate(%s %s) scale(1.1)" % (10 * k, -30 * k)),
              T(fill="#ff5a10", opacity=.95, filter="url(#flame)", transform="translate(%s %s) scale(1.05)" % (4 * k, -14 * k)),
              T(fill="url(#gold)", stroke="url(#gold)", stroke_width=gsz * .03, stroke_linejoin="round", filter="url(#bevel)"), T(fill="url(#streak)", opacity=.35),
              T(fill="none", stroke="#fff3b0", stroke_width=gsz * .008, opacity=.95), T(fill="none", stroke="#ffffff", stroke_width=gsz * .003, opacity=.8, transform="translate(-3 -3)")]
    else:
        L += [T(fill="#ff2a10", opacity=.45, filter="url(#glowF)"), T(fill="#ffb000", opacity=.9, filter="url(#flame)", transform="translate(%s %s) scale(1.05)" % (4 * k, -14 * k)),
              T(fill="#ff3a10", opacity=.9, filter="url(#flame)", transform="translate(%s %s) scale(1.02)" % (-2 * k, -6 * k)),
              T(fill="url(#fire)", filter="url(#bevel)"), T(fill="url(#streak)", opacity=.3), T(fill="none", stroke="#ffd36a", stroke_width=gsz * .005, opacity=.7)]
    if sparkle_seed is not None:
        import random; rng = random.Random(sparkle_seed * 31 + n)
        for _ in range(3 if kind != "crystal" else 2):
            sx = (rng.random() - .5) * gsz * .45; sy = -gsz * (.15 + rng.random() * .6); r = gsz * (.016 + rng.random() * .012)
            L.append(f"<path d='M{sx} {sy - r * 3}L{sx + r * .4} {sy - r * .4}L{sx + r * 3} {sy}L{sx + r * .4} {sy + r * .4}L{sx} {sy + r * 3}L{sx - r * .4} {sy + r * .4}L{sx - r * 3} {sy}L{sx - r * .4} {sy - r * .4}Z' fill='#fff' opacity='.9'/>")
    body = "".join(L)
    return (f"<!doctype html><meta charset=utf8><style>{fonts_css()}html,body{{margin:0;background:transparent;overflow:hidden}}svg{{display:block}}</style>"
            f"<svg xmlns='http://www.w3.org/2000/svg' width='1024' height='1024' viewBox='0 0 1024 1024'><defs>{scaled_defs(k, seed)}</defs>"
            f"<g transform='translate(512 {size * 1.18}) skewX(-12)'>{body}</g></svg>")
def shoot(html, png):
    f = OUT + "/pages/" + os.path.basename(png)[:-4] + ".html"; open(f, "w", encoding="utf8").write(html)
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000", "--window-size=1024,1024",
                    "--virtual-time-budget=4000", f"--screenshot={png}", "file://" + f], capture_output=True, timeout=90)
    return os.path.exists(png)
if __name__ == "__main__":
    os.makedirs(OUT + "/pages", exist_ok=True)
    only = [int(x) for x in os.environ.get("ONLY", "").split(",") if x]
    for n in range(1, 10):
        if only and n not in only: continue
        crystal = n % 2 == 0
        if crystal:
            shoot(page(n, 3, None), f"{OUT}/symbol_{n}.png")
            for s in range(2): shoot(page(n, 3, s + 1), f"{OUT}/symbol_{n}_s{s}.png")
        else:
            for f, sd in enumerate(FIRE_SEEDS): shoot(page(n, sd, 1), f"{OUT}/symbol_{n}_f{f}.png")
            import shutil; shutil.copy(f"{OUT}/symbol_{n}_f0.png", f"{OUT}/symbol_{n}.png")
        print("done", n, flush=True)
