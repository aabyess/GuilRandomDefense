"""보호된 MPQ(w3x)에서 암호화된 파일까지 꺼낸다.

mpyq는 (listfile)을 요구하고 암호화 블록을 못 읽는다. 보호된 맵은 그 둘을
정확히 없애고 켜두므로 mpyq만으로는 아무것도 못 꺼낸다. 해시·복호는 공개된
알고리즘이라 여기서 직접 구현한다.
"""
import struct, zlib, bz2
from io import BytesIO
import mpyq

MPQ_FILE_IMPLODE        = 0x00000100
MPQ_FILE_COMPRESS       = 0x00000200
MPQ_FILE_ENCRYPTED      = 0x00010000
MPQ_FILE_FIX_KEY        = 0x00020000
MPQ_FILE_SINGLE_UNIT    = 0x01000000
MPQ_FILE_SECTOR_CRC     = 0x04000000
MPQ_FILE_EXISTS         = 0x80000000


# 스톰 암호화 표. mpyq는 이걸 인스턴스 안에서 만드는데, 우리는 __init__을
# 건너뛰므로 여기서 직접 만든다(공개된 표준 알고리즘이다).
def _make_table():
    seed = 0x00100001
    table = {}
    for i in range(256):
        index = i
        for _ in range(5):
            seed = (seed * 125 + 3) % 0x2AAAAB
            temp1 = (seed & 0xFFFF) << 0x10
            seed = (seed * 125 + 3) % 0x2AAAAB
            temp2 = (seed & 0xFFFF)
            table[index] = (temp1 | temp2)
            index += 0x100
    return table

ENC = _make_table()
_HASH_TYPES = {'TABLE_OFFSET': 0, 'HASH_A': 1, 'HASH_B': 2, 'TABLE': 3}


def _hash(string, hash_type):
    seed1, seed2 = 0x7FED7FED, 0xEEEEEEEE
    for ch in string.upper():
        value = ord(ch)
        seed1 = ENC[(_HASH_TYPES[hash_type] << 8) + value] ^ ((seed1 + seed2) & 0xFFFFFFFF)
        seed2 = (value + seed1 + seed2 + (seed2 << 5) + 3) & 0xFFFFFFFF
    return seed1


def _decrypt(data, key):
    """MPQ 블록 복호. 4바이트 단위로 돌며 키가 매 워드마다 갱신된다."""
    seed1 = key
    seed2 = 0xEEEEEEEE
    out = bytearray()
    for i in range(len(data) // 4):
        seed2 += ENC[0x400 + (seed1 & 0xFF)]
        seed2 &= 0xFFFFFFFF
        value = struct.unpack('<I', data[i*4:i*4+4])[0] ^ ((seed1 + seed2) & 0xFFFFFFFF)
        value &= 0xFFFFFFFF
        seed1 = ((~seed1 << 0x15) + 0x11111111) | (seed1 >> 0x0B)
        seed1 &= 0xFFFFFFFF
        seed2 = (value + seed2 + (seed2 << 5) + 3) & 0xFFFFFFFF
        out += struct.pack('<I', value)
    out += data[len(data) - len(data) % 4:]
    return bytes(out)


def _decompress(data):
    t = data[0]
    if t == 0:   return data
    if t == 2:   return zlib.decompress(data[1:], 15)
    if t == 16:  return bz2.decompress(data[1:])
    if t == 8:                      # PKWARE implode
        import pkware
        return pkware.explode(data[1:])
    raise RuntimeError(f"압축 방식 {t} 미지원")


class Archive:
    def __init__(self, path, offset=0):
        self.f = open(path, 'rb')
        self.f.seek(offset)
        a = mpyq.MPQArchive.__new__(mpyq.MPQArchive)
        a.file = self.f
        self.f.seek(offset)
        a.header = a.read_header()
        a.hash_table = a.read_table('hash')
        a.block_table = a.read_table('block')
        a.files = None
        self.a = a

    def read(self, filename):
        a = self.a
        he = a.get_hash_table_entry(filename)
        if he is None: return None
        be = a.block_table[he.block_table_index]
        if not (be.flags & MPQ_FILE_EXISTS) or be.archived_size == 0: return None

        off = be.offset + a.header['offset']
        self.f.seek(off)
        raw = self.f.read(be.archived_size)

        key = None
        if be.flags & MPQ_FILE_ENCRYPTED:
            base = filename.rsplit('\\', 1)[-1]
            key = _hash(base, 'TABLE')
            if be.flags & MPQ_FILE_FIX_KEY:
                key = (key + be.offset) ^ be.size
                key &= 0xFFFFFFFF

        if be.flags & MPQ_FILE_SINGLE_UNIT:
            if key is not None: raw = _decrypt(raw, key)
            if be.flags & (MPQ_FILE_COMPRESS | MPQ_FILE_IMPLODE) and be.size > be.archived_size:
                raw = _decompress(raw)
            return raw[:be.size]

        sector_size = 512 << a.header['sector_size_shift']
        sectors = be.size // sector_size + 1
        if be.flags & MPQ_FILE_SECTOR_CRC: sectors += 1

        head = raw[:4 * (sectors + 1)]
        if key is not None:
            head = _decrypt(head, (key - 1) & 0xFFFFFFFF)
        positions = struct.unpack('<%dI' % (sectors + 1), head)

        out = BytesIO(); left = be.size
        for i in range(len(positions) - 1):
            sec = raw[positions[i]:positions[i+1]]
            if not sec: continue
            if key is not None:
                sec = _decrypt(sec, (key + i) & 0xFFFFFFFF)
            if be.flags & (MPQ_FILE_COMPRESS | MPQ_FILE_IMPLODE) and left > len(sec):
                sec = _decompress(sec)
            left -= len(sec)
            out.write(sec)
            if left <= 0: break
        return out.getvalue()[:be.size]
