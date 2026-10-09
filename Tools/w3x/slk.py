"""SLK(W3x2lni 슬크 모드) 파서 → (헤더, {행키: {열: 값}}). 2026-10-09, blender."""
import re
def parse(data, key_col=1):
    txt = data.decode("utf-8", "replace") if isinstance(data, bytes) else data
    cells = {}; x = y = 0
    for ln in txt.split("\n"):
        ln = ln.strip("\r")
        if not ln.startswith("C;"): continue
        k = None; val = None
        for part in re.split(r";(?=[XYK])", ln[2:]):
            if part.startswith("X"): x = int(part[1:])
            elif part.startswith("Y"): y = int(part[1:])
            elif part.startswith("K"): val = part[1:]
        if val is None: continue
        if val.startswith('"') and val.endswith('"'): val = val[1:-1]
        cells[(y, x)] = val
    maxy = max(k[0] for k in cells); maxx = max(k[1] for k in cells)
    hdr = {x: cells.get((1, x), "") for x in range(1, maxx + 1)}
    rows = {}
    for y in range(2, maxy + 1):
        key = cells.get((y, key_col))
        if key is None: continue
        rows[key] = {hdr[x]: cells[(y, x)] for x in range(1, maxx + 1) if (y, x) in cells}
    return list(hdr.values()), rows
