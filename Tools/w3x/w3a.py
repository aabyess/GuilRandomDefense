"""war3map.w3a (능력/스킬 오브젝트 데이터) 파서. w3q.py와 완전히 같은 형식(w3o 계열
공용 포맷)이다 — 능력은 레벨별로 값이 붙어서 w3u.py(레벨 없음)가 아니라 이쪽을 따랐다.

형식: [버전][원본능력수][원본능력들][커스텀능력수][커스텀능력들]
능력 하나 = [원본ID 4][새ID 4][수정개수 4][수정들]
수정 하나 = [필드ID 4][자료형 4][레벨 4][데이터포인터 4][값][끝표시 4]
"""
import struct


def _read(d, i, count):
    out = []
    for _ in range(count):
        base = d[i:i+4].decode('ascii', 'replace'); i += 4
        new = d[i:i+4].decode('ascii', 'replace'); i += 4
        nmod, = struct.unpack('<I', d[i:i+4]); i += 4
        mods = []
        for _ in range(nmod):
            fid = d[i:i+4].decode('ascii', 'replace'); i += 4
            vtype, = struct.unpack('<I', d[i:i+4]); i += 4
            lvl, = struct.unpack('<I', d[i:i+4]); i += 4
            dataid, = struct.unpack('<I', d[i:i+4]); i += 4
            if vtype == 0:
                v, = struct.unpack('<i', d[i:i+4]); i += 4
            elif vtype in (1, 2):
                v, = struct.unpack('<f', d[i:i+4]); i += 4
            else:
                e = d.index(b'\0', i); v = d[i:e].decode('utf-8', 'replace'); i = e + 1
            i += 4  # 끝 표시
            mods.append({'field': fid, 'level': lvl, 'dataId': dataid, 'value': v})
        out.append({'base': base, 'id': new or base, 'mods': mods})
    return out, i


def parse(path):
    d = open(path, 'rb').read()
    i = 4
    n, = struct.unpack('<I', d[i:i+4]); i += 4
    orig, i = _read(d, i, n)
    n, = struct.unpack('<I', d[i:i+4]); i += 4
    custom, i = _read(d, i, n)
    return orig + custom


def fields_by_level(ability):
    """{필드ID: {레벨: 값}} 형태로 다시 묶는다 — 레벨마다 값이 다른 필드를 한눈에 보기 위함."""
    out = {}
    for m in ability['mods']:
        out.setdefault(m['field'], {})[m['level']] = m['value']
    return out
