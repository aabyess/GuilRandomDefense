"""BLP1(JPEG 내용) 올바른 디코드(2026-10-09, blender). PIL은 이 BLP의 4채널 JPEG을 CMYK로 읽어 알파(4번째 채널)를 K로 곱해 버려,
투명 자리가 검은 RGB가 되고 알파는 0이었다(→ 알파 255로 덮으면 검은 네모·얼룩: 캐럿 꼬리 털·눈가). 여기선 채널을 직접 풀어
RGB = 255 - (성분 2,1,0), 알파 = 255 - 성분 3 로 만든다. JPEG이 아니거나(팔레트) 알파비트 0이면 PIL 그대로.
  from blp_decode import decode ; decode(bytes) -> PIL RGBA"""
import io, struct
import numpy as np
from PIL import Image


def decode(d):
    if d[:4] == b"BLP1":
        content, alpha_bits = struct.unpack("<II", d[4:12])
        if content == 0:
            offs = struct.unpack("<16I", d[28:92]); sizes = struct.unpack("<16I", d[92:156]); hl = struct.unpack("<I", d[156:160])[0]
            im = Image.open(io.BytesIO(d[160:160 + hl] + d[offs[0]:offs[0] + sizes[0]])); im.load()
            if im.mode == "CMYK":
                a = np.array(im).astype(np.uint8)
                rgb = 255 - a[..., [2, 1, 0]]
                al = (255 - a[..., 3]) if alpha_bits else np.full(a.shape[:2], 255, np.uint8)
                return Image.fromarray(np.dstack([rgb, al]).astype(np.uint8))
    im = Image.open(io.BytesIO(d)).convert("RGBA")
    if im.getchannel("A").getextrema()[1] == 0: im.putalpha(255)
    return im
