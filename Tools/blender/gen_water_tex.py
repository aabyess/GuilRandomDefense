"""원작 원랜디 톤 바다 물 텍스처(blender 세션 2026-10-07, 사장님 「바다 물 색상 이렇게 가능해?」, 참고 /tmp/claude-501/ref_original_water.png 의 색만 재서 새로 만듦 — 블리자드 텍스처 추출 없음).
PIL·numpy(주기 소음: FFT 필터 → 가장자리 이음새 0). /usr/bin/python3 Tools/blender/gen_water_tex.py [~/GRD_water]
산출: water_color_2048.png(RGB, 이음새 없음) · water_normal_2048.png(잔물결 노멀, 이음새 없음) · water_shallow_band.png(512×256 RGBA, 가로 이음, 아래→위 알파 0→불투명 밝은 청록) · spec.txt(색 값 규격) · compare_water.png
기준색(참고 사진 실측): 깊은 물 RGB(32,73,93) · 얕은 물 띠 RGB(79,104,111)~(151,162,143, 모래 비침) · 채도 낮고 어두움."""
import os, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
D = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/GRD_water'); os.makedirs(D, exist_ok=True)
N = 2048; rng = np.random.RandomState(1007)

def pnoise(n, beta, seed):
    """주기 소음: 흰 소음을 FFT에서 1/f^beta로 걸러 → 가장자리가 자동으로 이어진다. 0..1 정규화."""
    r = np.random.RandomState(seed); w = r.randn(n, n); F = np.fft.fft2(w)
    fy = np.fft.fftfreq(n)[:, None]; fx = np.fft.fftfreq(n)[None, :]; f = np.sqrt(fx ** 2 + fy ** 2); f[0, 0] = 1
    F *= 1.0 / f ** beta; F[0, 0] = 0; a = np.real(np.fft.ifft2(F)); a -= a.min(); a /= a.max(); return a
def bandpass(n, lo, hi, seed):
    """주파수 대역만 남긴 소음(잔물결 크기 고르기). lo~hi = 사이클/텍스처."""
    r = np.random.RandomState(seed); w = r.randn(n, n); F = np.fft.fft2(w)
    fy = np.fft.fftfreq(n)[:, None] * n; fx = np.fft.fftfreq(n)[None, :] * n; f = np.sqrt(fx ** 2 + fy ** 2)
    mask = np.exp(-((np.log(np.maximum(f, 1e-3)) - np.log((lo * hi) ** .5)) ** 2) / (2 * (np.log(hi / lo) / 2.2) ** 2)); mask[0, 0] = 0
    a = np.real(np.fft.ifft2(F * mask)); a /= np.abs(a).max(); return a            # −1..1

# 높이장: 큰 일렁임(8~16 주기) + 잔물결(60~140 주기), 방향성 살짝(가로로 길게) — 모두 주기적
big = bandpass(N, 6, 14, 1); mid = bandpass(N, 30, 70, 2); fine = bandpass(N, 110, 220, 3)
yy, xx = np.mgrid[0:N, 0:N] / N
ripple = (np.sin(2 * math.pi * (xx * 38 + yy * 12 + big * .9)) * .5 + np.sin(2 * math.pi * (xx * 61 - yy * 19 + mid * 1.2)) * .5)      # 이음새 맞는 정수 주기
h = big * .6 + mid * .3 + fine * .05 + ripple * .22
h = (h - h.min()) / (h.max() - h.min())

# 노멀맵(Unity 규약: +Y 위, 정규화) — 주기 래핑 차분
STR = 1.7
dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * .5 * N / 64; dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * .5 * N / 64
nx, ny, nz = -dx * STR, -dy * STR, np.ones_like(h); L = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
nrm = np.dstack([(nx / L * .5 + .5), (ny / L * .5 + .5), (nz / L * .5 + .5)])
Image.fromarray((nrm * 255).astype(np.uint8)).save(D + '/water_normal_2048.png')

# 색: 깊은 물 (32,73,93) 중심, 높이에 따라 어둡게/밝게 ±10%, 큰 얼룩으로 청록↔회청 살짝(채도 낮게), 잔물결 마루에 옅은 하이라이트
base = np.array([32, 73, 93], np.float32)
tone = (h - .5) * .5 + (big - .0) * .12
tint = np.dstack([np.zeros_like(h) - 3 * mid, np.zeros_like(h) + 5 * big, np.zeros_like(h) + 4 * big])           # 청록 쪽 미세 이동
col = base[None, None, :] * (1 + tone[..., None] * .55) + tint
crest = np.clip((h - .68) / .3, 0, 1) ** 1.6                                                                       # 마루
col += crest[..., None] * np.array([22, 28, 26])
col = np.clip(col, 0, 255)
Image.fromarray(col.astype(np.uint8)).save(D + '/water_color_2048.png')

# 얕은 가장자리 띠 512×256 (가로 이음): 아래(물)=투명 → 위(해안선)=밝은 청록(79,104,111)→(120,150,140), 잔물결 얼룩
W, H = 512, 256; r = np.random.RandomState(9)
g = bandpass(512, 4, 10, 5)[:H, :W]; g2 = bandpass(512, 20, 50, 6)[:H, :W]
v = np.linspace(0, 1, H)[:, None] * np.ones((1, W))                                                               # 0 = 물쪽, 1 = 해안쪽
a = np.clip((v - .08) / .9, 0, 1) ** 1.3 * (.8 + .3 * g + .15 * g2); a = np.clip(a, 0, 1)
c0 = np.array([62, 98, 110], np.float32); c1 = np.array([130, 158, 146], np.float32)
bc = c0[None, None] * (1 - v[..., None]) + c1[None, None] * v[..., None]; bc = bc * (1 + .12 * g2[..., None])
band = np.dstack([np.clip(bc, 0, 255), a * 255]).astype(np.uint8)
Image.fromarray(band, 'RGBA').save(D + '/water_shallow_band.png')

open(D + '/spec.txt', 'w', encoding='utf-8').write('''# 바다 색 규격 (참고 사진 실측 → 우리 URP Lit 머티리얼 제안)
깊은 물 평균색            RGB (32, 73, 93)   #20495D   — water_color_2048.png 의 평균 ≈ 이 색
얕은 물 띠 (물쪽)         RGB (62, 98, 110)  #3E626E
얕은 물 띠 (해안쪽)       RGB (130, 158, 146) #829E92  — 모래 비침(원작 얕은 곳은 (151,162,143)까지)
머티리얼 제안 (sea.mat · sea_128x128.mat, URP Lit)
  _BaseColor  = (1, 1, 1, 1)  ← 지금 (0.78, 0.9, 1)의 푸른 곱을 없앤다(텍스처가 이미 색을 가짐)
  _BaseMap    = water_color_2048.png (Repeat, 타일링: 지금 sea.mat의 타일링 그대로 쓰되 텍스처가 8배 커졌으니 타일 수를 1/8 ≈ 지금의 1/4~1/8로)
  _BumpMap    = water_normal_2048.png  _BumpScale = 0.6~0.8 (1은 과함: 원작은 부드러운 잔물결)
  _Smoothness = 0.35   ← 지금 0.92(거울처럼 하늘색 반사)가 밝은 하늘색의 큰 원인. 원작은 반사가 거의 없음.
  _Metallic   = 0       _SpecularHighlights 켬(해 반짝임은 약하게)
흐름 속도 제안: 노멀·색 UV를 (0.012, 0.006)/초 정도로 천천히 흘림(두 번 겹쳐 서로 다른 속도 (0.012,0.006)·(−0.008,0.010)이면 반복티가 줄어든다). 원작은 잔물결이 제자리에서 일렁이는 느낌.
얕은 띠: water_shallow_band.png 를 섬 해안선 따라 늘어선 얇은 평면(알파 블렌드)에 가로 반복(타일 폭 ≈ 해안 길이/반복 수), 아래 가장자리가 물 쪽.
''')
# ----- 비교: 참고 | 지금(스크린샷의 바다 + 지금 텍스처 타일) | 새 타일(노멀 조명 흉내)
ref = Image.open('/tmp/claude-501/ref_original_water.png').convert('RGB').crop((0, 40, 330, 300)).resize((660, 520))
def lit(colimg, nrmimg, tint=(1, 1, 1), gain=1.0):
    c = np.asarray(colimg, np.float32) * np.array(tint)[None, None]; n = np.asarray(nrmimg, np.float32) / 255 * 2 - 1
    ld = np.array([-.35, -.45, .82]); ld /= np.linalg.norm(ld); d = np.clip((n * ld[None, None]).sum(-1), 0, 1)
    return np.clip(c * (.8 + .3 * d[..., None]) * gain, 0, 255).astype(np.uint8)
old_c = Image.open('Assets/Textures/Map/water.png').convert('RGB'); old_n = Image.open('Assets/Textures/Map/water_normal.png').convert('RGB')
tile = lambda im, k: Image.fromarray(np.tile(np.asarray(im), (k, k, 1)))
old = Image.fromarray(lit(tile(old_c, 3).resize((660, 520), Image.LANCZOS), tile(old_n, 3).resize((660, 520), Image.LANCZOS), (.78, .9, 1.0), 1.55))
new = Image.fromarray(lit(Image.open(D + '/water_color_2048.png').resize((1320, 1040), Image.LANCZOS).crop((0, 0, 660, 520)), Image.open(D + '/water_normal_2048.png').resize((1320, 1040), Image.LANCZOS).crop((0, 0, 660, 520))))
out = Image.new('RGB', (660 * 3 + 40, 560), (20, 20, 24)); dd = ImageDraw.Draw(out)
for i, (im, t) in enumerate(((ref, 'REF (원작)'), (old, 'NOW (지금 텍스처+밝은 반사 보정 없이 단순 조명)'), (new, 'NEW (새 텍스처, 같은 단순 조명)'))):
    out.paste(im, (i * 680, 40)); dd.text((i * 680 + 6, 10), t, fill=(255, 255, 0))
mean = lambda im: [int(v) for v in np.asarray(im).reshape(-1, 3).mean(0)]
dd.text((6, 548), f'mean RGB  ref {mean(ref)}  now(광택 제외) {mean(old)}  new {mean(new)}', fill=(200, 200, 200))
out.save(D + '/compare_water.png'); print('saved', D, 'new mean', mean(Image.open(D + '/water_color_2048.png')))
