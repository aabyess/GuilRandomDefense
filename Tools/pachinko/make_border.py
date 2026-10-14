#!/usr/bin/python3
"""machine_frame.png(blender 산출)의 가운데 어두운 판·릴 구멍을 투명하게 뚫어 「바깥 장식 테두리」만 남긴다(사장님 10-14: 숫자가 화면 가운데를 크게, 틀은 장식).
사용: /usr/bin/python3 Tools/pachinko/make_border.py  → Assets/Resources/Pachinko/machine_border.png"""
from PIL import Image
src = 'Assets/Resources/Pachinko/machine_frame.png'
im = Image.open(src).convert('RGBA')
px = im.load()
for y in range(318, 840):
    for x in range(386, 1536):
        px[x, y] = (0, 0, 0, 0)
im.save('Assets/Resources/Pachinko/machine_border.png')
print('ok')
