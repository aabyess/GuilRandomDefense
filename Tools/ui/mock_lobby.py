"""시작 화면 합성 시안(에디터 없이 레이아웃 근사): 배경 + 제목 판 + 메뉴 틀 + 단추 4. 사용: mock_lobby.py <Lobby 폴더> <출력 png> <글자색 r,g,b>"""
import sys, os
from PIL import Image, ImageDraw, ImageFont
d, out, col = sys.argv[1], sys.argv[2], tuple(int(x) for x in sys.argv[3].split(","))
L = lambda n: Image.open(os.path.join(d, n)).convert("RGBA")
bg = L("bg_1920x1080.png")
plate = L("title_plate.png").resize((984, 211))
bg.alpha_composite(plate, (636 - 492, 136 - 105))
fr = L("menu_frame.png").resize((480, 912))
fx, fy = 1920 - 324 - 240, 540 + 24 - 456
bg.alpha_composite(fr, (fx, fy))
btn = L("btn_normal.png").resize((348, 84))
try: f = ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", 38, index=6)
except Exception: f = ImageFont.load_default()
for i, t in enumerate(["혼자 하기", "같이 하기", "세이브 코드 불러오기", "설정"]):
    y = fy + 250 + i * 110
    bg.alpha_composite(btn, (fx + 66, y))
    dr = ImageDraw.Draw(bg); w = dr.textlength(t, font=f)
    dr.text((fx + 66 + (348 - w) / 2, y + 20), t, font=f, fill=col)
dr = ImageDraw.Draw(bg)
try: tf = ImageFont.truetype("/System/Library/Fonts/Supplemental/Papyrus.ttc", 96)
except Exception: tf = ImageFont.load_default()
dr.text((636 - dr.textlength("G.R.D", font=tf) / 2, 136 - 58), "G.R.D", font=tf, fill=col)
bg.convert("RGB").save(out)
