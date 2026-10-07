#!/usr/bin/python3
"""blender 컷인 레이어(1920×1080 투명 PNG) → 게임 반입 형식(투명 여백 잘라 낸 작은 PNG + 배치 json).

  /usr/bin/python3 Tools/cutin/import_cutin.py [--src ~/GRD_cutin/layers] [--out Assets/Resources/Cutin] [--dry]

규칙(PM 10-08 승인):
  · 등급(초월·불멸·영원)마다 같은 레이어(flat·halftone·ring·streak·stripes·band)는 해시로 확인해 한 벌만
    <out>/_common/<등급>/ 에 둔다(등급 안에서 해시가 갈리면 에러로 멈춘다 — 이 가정이 깨졌다는 뜻).
    flat은 단색이라 PNG를 안 쓰고 layout.json에 색만 적는다.
  · 유닛마다 다른 레이어(char·shadow·band_name·band_gray·title)는 투명 여백을 잘라 내고 긴 변을 줄인다(MAX_SIDE).
  · 각 폴더의 layout.json = {"layers":[{"name","file","x","y","w","h"}]} — x,y,w,h는 1920×1080 기준 화면 픽셀(왼쪽 위 원점)에서
    그 레이어 그림이 놓일 사각형(줄이기 전 원래 크기 기준이라 그대로 배치하면 원본과 같은 자리·크기가 된다).
  · 마지막에 예상 빌드 증가량(GPU 압축 텍스처 기준 DXT5 1B/px · ASTC6x6 0.44B/px)과 PNG 디스크 용량을 출력한다.
blender 산출 폴더 구조는 건드리지 않는다(읽기만).
"""
import argparse, collections, hashlib, json, os, sys, unicodedata
from PIL import Image

COMMON = ["flat", "halftone", "ring", "streak", "stripes", "band"]
PER_UNIT = ["band_gray", "band_name", "shadow", "char", "title"]
MAX_SIDE = {"char": 832, "shadow": 256, "title": 1024, "band_name": 1024, "band_gray": 1024,
            "halftone": 1920, "ring": 1024, "streak": 1024, "stripes": 1024, "band": 1920}
LAYERS = COMMON + PER_UNIT
GRADE_ASCII = {"초월": "transcend", "불멸": "immortal", "영원": "eternal"}


def unit_key(unit):
    """한글 폴더명은 맥(NFD)·윈도(NFC)에서 Resources.Load가 다르게 읽는다 — 폴더는 ASCII 키(c_ + NFC 이름 SHA1 앞 8자리)로. CutinOverlay.UnitKey와 같은 식."""
    return "c_" + hashlib.sha1(unicodedata.normalize("NFC", unit).encode("utf8")).hexdigest()[:8]


def grade_key(grade):
    return GRADE_ASCII[unicodedata.normalize("NFC", grade)]


def split_name(stem):
    """C_초월_최상호_AD_band_name → (초월, 초월_최상호_AD, band_name)"""
    body = stem[2:]
    for layer in sorted(LAYERS, key=len, reverse=True):
        if body.endswith("_" + layer):
            unit = body[: -len(layer) - 1]
            return unit.split("_")[0], unit, layer
    return None


def trim_and_scale(path, max_side):
    im = Image.open(path).convert("RGBA")
    box = im.getchannel("A").getbbox()
    if box is None:
        return None
    cropped = im.crop(box)
    w, h = cropped.size
    scale = min(1.0, max_side / max(w, h))
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    nw, nh = (nw + 3) // 4 * 4, (nh + 3) // 4 * 4   # 압축 텍스처(DXT/Crunch)는 4의 배수여야 한다 — 최대 3px 늘림(배치는 layout.json 사각형이라 그대로)
    if (nw, nh) != (w, h):
        cropped = cropped.resize((nw, nh), Image.LANCZOS)
    return cropped, box  # box = 원본 1920×1080에서의 (x0, y0, x1, y1)


def flat_color(path):
    im = Image.open(path).convert("RGBA")
    px = im.getpixel((im.width // 2, im.height // 2))
    return [px[0], px[1], px[2], px[3]]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=os.path.expanduser("~/GRD_cutin/layers"))
    ap.add_argument("--out", default="Assets/Resources/Cutin")
    ap.add_argument("--dry", action="store_true", help="파일을 쓰지 않고 용량만 계산")
    args = ap.parse_args()

    groups = collections.defaultdict(lambda: collections.defaultdict(dict))  # grade → unit → layer → path
    for f in sorted(os.listdir(args.src)):
        if not f.startswith("C_") or not f.endswith(".png"):
            continue
        parsed = split_name(f[:-4])
        if parsed:
            grade, unit, layer = parsed
            groups[grade][unit][layer] = os.path.join(args.src, f)

    total_px = 0
    total_png = 0
    per_unit_px = []

    def emit(folder, name, img_box, layout):
        nonlocal total_px, total_png
        img, box = img_box
        file = name + ".png"
        if not args.dry:
            os.makedirs(folder, exist_ok=True)
            img.save(os.path.join(folder, file), optimize=True)
            total_png += os.path.getsize(os.path.join(folder, file))
        else:
            import io
            b = io.BytesIO(); img.save(b, "PNG", optimize=True); total_png += b.tell()
        total_px += img.width * img.height
        layout.append({"name": name, "file": name, "x": box[0], "y": box[1], "w": box[2] - box[0], "h": box[3] - box[1]})
        return img.width * img.height

    for grade, units in sorted(groups.items()):
        # 공통 레이어: 등급 안에서 해시가 하나여야 한다.
        common_layout = []
        common_json = {"flat": None}
        for layer in COMMON:
            hashes = {}
            for unit, layers in units.items():
                if layer in layers:
                    hashes.setdefault(hashlib.md5(open(layers[layer], "rb").read()).hexdigest(), []).append(unit)
            if len(hashes) > 1:
                print(f"🔴 {grade}/{layer}: 해시가 {len(hashes)}가지 — 등급 공통이 아니다. 유닛별로 두려면 이 스크립트의 COMMON에서 PER_UNIT으로 옮길 것.", file=sys.stderr)
                for h, us in hashes.items(): print("   ", h[:8], us[:4], file=sys.stderr)
                sys.exit(2)
            if not hashes:
                continue
            any_unit = next(iter(next(iter(hashes.values()))))
            path = units[any_unit][layer]
            if layer == "flat":
                common_json["flat"] = flat_color(path)
                continue
            r = trim_and_scale(path, MAX_SIDE[layer])
            if r: emit(os.path.join(args.out, "_common", grade_key(grade)), layer, r, common_layout)
        folder = os.path.join(args.out, "_common", grade_key(grade))
        if not args.dry:
            os.makedirs(folder, exist_ok=True)
            json.dump({"flat": common_json["flat"], "layers": common_layout}, open(os.path.join(folder, "layout.json"), "w"), ensure_ascii=False)

        # 유닛별 레이어
        for unit, layers in sorted(units.items()):
            layout = []
            unit_px = 0
            for layer in PER_UNIT:
                if layer not in layers:
                    continue
                r = trim_and_scale(layers[layer], MAX_SIDE[layer])
                if r: unit_px += emit(os.path.join(args.out, unit_key(unit)), layer, r, layout)
            if not args.dry and layout:
                json.dump({"grade": grade, "unit": unit, "layers": layout}, open(os.path.join(args.out, unit_key(unit), "layout.json"), "w"), ensure_ascii=False)
            per_unit_px.append(unit_px)

    n = len(per_unit_px)
    print(f"유닛 {n}기 · 등급 {len(groups)}종")
    print(f"텍스처 픽셀 합 {total_px/1e6:.1f} Mpx")
    print(f"예상 빌드 증가 — DXT5(1B/px) {total_px/1e6:.1f} MB · ASTC6x6(0.44B/px) {total_px*0.444/1e6:.1f} MB · (밉맵 안 씀 가정)")
    print(f"PNG 디스크 {total_png/1e6:.1f} MB (유닛당 평균 텍스처 {sum(per_unit_px)/max(1,n)/1e6:.2f} Mpx)")


if __name__ == "__main__":
    main()
