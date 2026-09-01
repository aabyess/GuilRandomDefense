#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""맵용 타일링 텍스처를 만든다.

셰이더를 새로 짜는 대신 텍스처 + URP/Lit 조합으로 간다.
에디터를 못 여는 환경에서는 셰이더 컴파일 오류를 확인할 방법이 없어서,
검증 가능한 쪽(이미지 파일은 여기서 바로 확인된다)을 택했다.

노이즈는 격자를 감싸서(wrap) 만들기 때문에 이어 붙여도 이음매가 없다.
"""
import math, random
from PIL import Image

SIZE = 256
OUT = 'Assets/Textures/Map'


def smoothstep(t):
    return t * t * (3 - 2 * t)


def value_noise(size, period, seed):
    """주기 격자 기반 값 노이즈. 격자를 wrap 해서 타일링이 맞는다."""
    rnd = random.Random(seed)
    grid = [[rnd.random() for _ in range(period)] for _ in range(period)]
    step = size / period
    out = [[0.0] * size for _ in range(size)]

    for y in range(size):
        gy = y / step
        y0 = int(gy) % period
        y1 = (y0 + 1) % period
        fy = smoothstep(gy - int(gy))

        for x in range(size):
            gx = x / step
            x0 = int(gx) % period
            x1 = (x0 + 1) % period
            fx = smoothstep(gx - int(gx))

            top = grid[y0][x0] * (1 - fx) + grid[y0][x1] * fx
            bottom = grid[y1][x0] * (1 - fx) + grid[y1][x1] * fx
            out[y][x] = top * (1 - fy) + bottom * fy

    return out


def fbm(size, octaves, base_period, seed):
    """옥타브를 겹쳐 자잘한 결과 큰 얼룩을 함께 만든다."""
    total = [[0.0] * size for _ in range(size)]
    amplitude, norm = 1.0, 0.0

    for o in range(octaves):
        layer = value_noise(size, base_period * (2 ** o), seed + o * 977)
        for y in range(size):
            row, layer_row = total[y], layer[y]
            for x in range(size):
                row[x] += layer_row[x] * amplitude
        norm += amplitude
        amplitude *= 0.5

    return [[v / norm for v in row] for row in total]


def write(name, pixels):
    img = Image.new('RGB', (SIZE, SIZE))
    img.putdata(pixels)
    img.save(f'{OUT}/{name}.png')
    print(f"  {name}.png")


def lerp3(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def build_grass():
    n = fbm(SIZE, 5, 4, seed=11)
    patch = fbm(SIZE, 2, 2, seed=97)
    # 위에서 내려다보는 맵이라 큰 얼룩만 있으면 뭉개져 보인다. 픽셀 단위 잔결을 섞는다.
    speck = random.Random(1234)
    dark, light, dry = (44, 80, 36), (112, 156, 64), (132, 142, 70)
    pixels = []
    for y in range(SIZE):
        for x in range(SIZE):
            c = lerp3(dark, light, n[y][x])
            c = lerp3(c, dry, max(0.0, patch[y][x] - 0.55) * 0.9)
            j = speck.randint(-14, 14)
            pixels.append((max(0, min(255, c[0] + j)),
                           max(0, min(255, c[1] + j)),
                           max(0, min(255, c[2] + j // 2))))
    write('grass', pixels)


def build_water():
    """방향성 있는 잔물결. 얼룩 노이즈만으로는 흐릿한 판으로만 보인다.

    서로 다른 각도·주기의 파를 겹치고 노이즈로 위상을 흔들면 물결처럼 읽힌다.
    주기는 타일 크기(SIZE)의 정수배로 잡아야 이어 붙였을 때 이음매가 안 생긴다.
    """
    warp = fbm(SIZE, 4, 4, seed=23)
    swell = fbm(SIZE, 2, 2, seed=71)

    deep, shallow, foam = (16, 52, 88), (52, 126, 166), (196, 228, 238)
    pixels = []

    for y in range(SIZE):
        for x in range(SIZE):
            u = x / SIZE
            v = y / SIZE
            phase = (warp[y][x] - 0.5) * 2.2

            # 세 방향의 파를 겹친다 — 주기는 전부 정수라 타일 경계에서 맞아떨어진다
            wave = (math.sin((u * 6 + v * 2) * math.pi * 2 + phase) * 0.5
                    + math.sin((u * 2 - v * 5) * math.pi * 2 + phase * 1.7) * 0.3
                    + math.sin((u * 11 + v * 9) * math.pi * 2 + phase * 0.6) * 0.2)
            wave = wave * 0.5 + 0.5

            depth = wave * 0.72 + swell[y][x] * 0.28
            c = lerp3(deep, shallow, depth)
            # 마루 끝에만 흰기를 얹는다. 넓게 깔면 안개처럼 보인다.
            pixels.append(lerp3(c, foam, max(0.0, wave - 0.82) * 3.2))

    write('water', pixels)


def build_rock():
    n = fbm(SIZE, 5, 6, seed=41)
    strata = fbm(SIZE, 3, 2, seed=53)
    dark, light = (58, 52, 46), (126, 116, 102)
    pixels = []
    for y in range(SIZE):
        for x in range(SIZE):
            # 가로 줄무늬를 섞어 절벽 지층처럼 보이게 한다
            band = 0.5 + 0.5 * math.sin(y / SIZE * math.pi * 12 + strata[y][x] * 4)
            pixels.append(lerp3(dark, light, n[y][x] * 0.7 + band * 0.3))
    write('rock', pixels)


def build_normal(source_name, target_name, strength=2.5):
    """높이차에서 노멀맵을 만든다. 평평한 판이 빛을 받아 굴곡져 보이게 한다."""
    src = Image.open(f'{OUT}/{source_name}.png').convert('L')
    h = list(src.getdata())

    def at(x, y):
        return h[(y % SIZE) * SIZE + (x % SIZE)] / 255.0

    pixels = []
    for y in range(SIZE):
        for x in range(SIZE):
            dx = (at(x + 1, y) - at(x - 1, y)) * strength
            dy = (at(x, y + 1) - at(x, y - 1)) * strength
            length = math.sqrt(dx * dx + dy * dy + 1.0)
            pixels.append((
                int((-dx / length * 0.5 + 0.5) * 255),
                int((-dy / length * 0.5 + 0.5) * 255),
                int((1.0 / length * 0.5 + 0.5) * 255)))

    img = Image.new('RGB', (SIZE, SIZE))
    img.putdata(pixels)
    img.save(f'{OUT}/{target_name}.png')
    print(f"  {target_name}.png")


print("맵 텍스처 생성:")
build_grass()
build_water()
build_rock()
build_normal('water', 'water_normal', strength=5.0)
build_normal('grass', 'grass_normal', strength=1.5)
build_normal('rock', 'rock_normal', strength=3.5)
