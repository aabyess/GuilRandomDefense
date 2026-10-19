"""성 테마 로딩 화면 배경 → Assets/Resources/UI/LoadingScreenCastle.jpg (2560x1440, 옛 LoadingScreen.jpg와 같은 크기).
시작 화면 성 대전 배경을 재료로, 위쪽(제목 「G.R.D」 자리)과 아래쪽(진행 막대 자리)을 어둡게 눌러 글자가 읽히게 한다.
사용: /usr/bin/python3 Tools/ui/gen_castle_loading.py"""
import os, sys
import numpy as np
from PIL import Image
sys.argv = sys.argv[:1] + [os.path.join(os.path.dirname(__file__), "_unused_lobby_out")]
sys.path.insert(0, os.path.dirname(__file__))
import gen_castle_lobby as L
from castle_art import *
import shutil
shutil.rmtree(sys.argv[1], ignore_errors=True)

W, H = 2560, 1440
bg = np.asarray(L.background(W, H, seed=53), np.float32)
ys = np.linspace(0, 1, H)[:, None]
shade = 1 - 0.55 * np.clip(1 - ys / 0.30, 0, 1) ** 1.4 - 0.5 * np.clip((ys - 0.78) / 0.22, 0, 1)
bg = bg * shade[..., None]
out = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "UI", "LoadingScreenCastle.jpg")
Image.fromarray(np.clip(bg, 0, 255).astype(np.uint8)).save(out, quality=90)
print("ok", out)
