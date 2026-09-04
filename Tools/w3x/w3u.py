"""war3map.w3u (커스텀 유닛 오브젝트 데이터) 파서.

형식: [버전][원본유닛수][원본유닛들][커스텀유닛수][커스텀유닛들]
유닛 하나 = [원본ID 4][새ID 4][수정개수 4][수정들]
수정 하나 = [필드ID 4][자료형 4][값][끝표시 4]
"""
import struct

TYPES = {0: 'int', 1: 'real', 2: 'unreal', 3: 'string'}


def _read_units(d, i, count):
    units = []
    for _ in range(count):
        base = d[i:i+4].decode('ascii', 'replace'); i += 4
        new  = d[i:i+4].decode('ascii', 'replace'); i += 4
        nmod, = struct.unpack('<I', d[i:i+4]); i += 4
        mods = {}
        for _ in range(nmod):
            fid = d[i:i+4].decode('ascii', 'replace'); i += 4
            vtype, = struct.unpack('<I', d[i:i+4]); i += 4
            if vtype == 0:
                v, = struct.unpack('<i', d[i:i+4]); i += 4
            elif vtype in (1, 2):
                v, = struct.unpack('<f', d[i:i+4]); i += 4
            else:
                end = d.index(b'\0', i)
                v = d[i:end].decode('utf-8', 'replace'); i = end + 1
            i += 4                       # 끝 표시
            mods[fid] = v
        units.append({'base': base, 'id': new or base, 'mods': mods})
    return units, i


def parse(path):
    d = open(path, 'rb').read()
    i = 4
    n, = struct.unpack('<I', d[i:i+4]); i += 4
    orig, i = _read_units(d, i, n)
    n, = struct.unpack('<I', d[i:i+4]); i += 4
    custom, i = _read_units(d, i, n)
    return orig + custom
