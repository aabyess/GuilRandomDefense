#!/usr/bin/python3
"""0.3.26 기능 설명 카드: 기능 폴더 하나 = 카드 한 장(제목·사진·하는 법).
폴더(~/grd/GRD_patch_0326/<기능>/)에 사진 png + 하는법.txt(또는 설명.txt)가 있으면 된다.
사진 파일 이름 `1_초월_양재모.png` → 설명 「초월 양재모」. 글 줄바꿈 = 문단.
쓰기: render_feature_cards.py [기능폴더이름…]  (없으면 전부) → <루트>/_cards/<기능>.png
스타일은 patch_notes.html과 같은 돌·금색 틀. 크롬 헤드리스로 1080폭 렌더."""
import sys, os, re, html, subprocess, tempfile, shutil
from PIL import Image
ROOT = os.path.expanduser(os.environ.get('CARD_ROOT', '~/grd/GRD_patch_0326'))
VER = os.environ.get('CARD_VER', '0.3.26')
CHROME = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
CSS = """
:root{--stone:#1b1d21;--stone2:#24272c;--edge:#3a3e45;--gold:#e3b54f;--gold2:#a87b22;--ink:#ece6d6;--muted:#a39c8b;
--display:"Black Han Sans","Nanum Myeongjo",serif;--body:"Gowun Dodum",sans-serif}
*{box-sizing:border-box}body{margin:0;background:var(--stone);color:var(--ink);font-family:var(--body);width:1080px}
.page{padding:48px 64px 52px;background:radial-gradient(ellipse at 50% -10%,rgba(227,181,79,.18),transparent 60%),
repeating-linear-gradient(45deg,rgba(255,255,255,.012) 0 2px,transparent 2px 6px),var(--stone)}
.top{display:flex;align-items:baseline;justify-content:space-between;border-bottom:2px solid var(--gold2);padding-bottom:14px;margin-bottom:30px}
.logo{font-family:var(--display);font-size:44px;color:var(--gold);letter-spacing:.06em;text-shadow:0 2px 0 #6b4a10}
.ver{color:var(--muted);font-size:22px}
h1{font-family:var(--display);font-weight:400;font-size:64px;margin:0 0 26px;color:var(--gold);text-shadow:0 3px 0 #6b4a10,0 6px 18px rgba(0,0,0,.5)}
.shots{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-bottom:30px}
figure{margin:0;background:var(--stone2);border:2px solid var(--edge);border-radius:6px;overflow:hidden;
box-shadow:inset 0 0 0 1px rgba(227,181,79,.15),0 8px 24px rgba(0,0,0,.35)}
figure:first-child{grid-column:1/3}
figure img{display:block;width:100%}
figcaption{padding:8px 14px;font-size:20px;color:var(--muted);border-top:2px solid var(--edge)}
.how{background:var(--stone2);border:2px solid var(--gold2);border-radius:6px;padding:20px 28px 22px}
.how h2{margin:0 0 10px;font-family:var(--display);font-weight:400;font-size:30px;color:var(--gold)}
.how p{margin:0;padding:8px 0;font-size:27px;line-height:1.5;border-top:1px dashed rgba(255,255,255,.1)}
.how h2+p{border-top:0}
"""
def build(d):
    name = os.path.basename(d)
    txt = next((os.path.join(d, f) for f in ('하는법.txt', '설명.txt') if os.path.exists(os.path.join(d, f))), None)
    pics = sorted(f for f in os.listdir(d) if f.lower().endswith('.png'))
    if not pics or not txt: return None
    lines = [l.strip() for l in open(txt, encoding='utf-8').read().splitlines() if l.strip()]
    figs = ''.join('<figure><img src="file://%s"><figcaption>%s</figcaption></figure>' % (
        os.path.join(d, p), html.escape(re.sub(r'^\d+_', '', p[:-4]).replace('_', ' '))) for p in pics)
    paras = ''.join('<p>%s</p>' % html.escape(l) for l in lines)
    return name, ('<!doctype html><html lang="ko"><head><meta charset="utf-8">'
        '<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Black+Han+Sans&family=Gowun+Dodum&family=Nanum+Myeongjo:wght@800&display=swap">'
        '<style>%s</style></head><body><div class="page"><div class="top"><span class="logo">G.R.D</span><span class="ver">%s 기능 설명</span></div>'
        '<h1>%s</h1><div class="shots">%s</div><div class="how"><h2>하는 법</h2>%s</div></div></body></html>' % (CSS, VER, html.escape(name), figs, paras))
def main():
    want = sys.argv[1:]
    out = os.path.join(ROOT, '_cards'); os.makedirs(out, exist_ok=True)
    dirs = [os.path.join(ROOT, n) for n in sorted(os.listdir(ROOT)) if not n.startswith('_') and os.path.isdir(os.path.join(ROOT, n))]
    tmp = tempfile.mkdtemp()
    for d in dirs:
        if want and os.path.basename(d) not in want: continue
        r = build(d)
        if not r: print('건너뜀(사진/글 없음):', d); continue
        name, doc = r
        hp = os.path.join(tmp, name + '.html'); open(hp, 'w', encoding='utf-8').write(doc)
        shot = os.path.join(tmp, name + '.png')
        subprocess.run([CHROME, '--headless=new', '--disable-gpu', '--hide-scrollbars', '--allow-file-access-from-files',
            '--force-device-scale-factor=1', '--virtual-time-budget=8000', '--window-size=1080,5000',
            '--screenshot=' + shot, 'file://' + hp], stderr=subprocess.DEVNULL)
        im = Image.open(shot).convert('RGB'); w, h = im.size; px = im.load()
        bg = px[w - 2, h - 2]; y = h - 1   # 아래 빈 바탕 잘라 내기
        while y > 0 and all(abs(a - b) < 4 for a, b in zip(px[w // 2, y], bg)) and all(abs(a - b) < 4 for a, b in zip(px[60, y], bg)): y -= 1
        dst = os.path.join(out, name + '.png'); im.crop((0, 0, w, min(h, y + 52))).save(dst); print(dst, w, y + 52)
    shutil.rmtree(tmp)
main()
