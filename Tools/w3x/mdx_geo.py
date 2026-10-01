"""워크3 MDX(v800) 모델의 **모양**을 읽는다 — 지오셋(정점·UV·삼각형)·재질 층·텍스처 이름.

mdx.py(조사용 요약)는 파티클·시퀀스만 읽었다. 여기는 눈으로 보려고 메시를 꺼낸다.
읽는 것: TEXS · MTLS(층: 필터 모드·텍스처·알파) · GEOS(VRTX·NRMS·PTYP·PVTX·UVBS·재질 번호).
안 읽는 것: 뼈 애니메이션·파티클(PRE2/PREM/RIBB는 개수만)·카메라·충돌. 정점은 이미 모델 공간 위치라
스켈레톤 없이 그대로 그리면 기본(Stand 0프레임) 모양이다.

쓰는 법: python3로 불러 parse(bytes) → dict. Blender 쪽 그림은 Tools/blender/render_mdx.py.
"""
import struct

FILTER = {0: "none", 1: "transparent", 2: "blend", 3: "additive", 4: "addalpha", 5: "modulate", 6: "modulate2x"}


def chunks(b):
    i, out = 4, {}
    while i + 8 <= len(b):
        tag = b[i:i + 4].decode("latin1")
        sz, = struct.unpack("<I", b[i + 4:i + 8])
        out.setdefault(tag, []).append(b[i + 8:i + 8 + sz])
        i += 8 + sz
    return out


def _cstr(x):
    return x.split(b"\0")[0].decode("latin1")


def parse_textures(c):
    tex = []
    for d in c.get("TEXS", []):
        for j in range(len(d) // 268):
            e = d[j * 268:(j + 1) * 268]
            rid, = struct.unpack("<I", e[:4])
            tex.append(dict(replaceable=rid, path=_cstr(e[4:264])))
    return tex


def parse_materials(c):
    mats = []
    for d in c.get("MTLS", []):
        i = 0
        while i + 4 <= len(d):
            size, = struct.unpack("<I", d[i:i + 4])
            m = d[i:i + size]
            prio, flags = struct.unpack("<II", m[4:12])
            assert m[12:16] == b"LAYS", "MTLS 구조가 v800이 아니다"
            nl, = struct.unpack("<I", m[16:20])
            p, layers = 20, []
            for _ in range(nl):
                lsz, = struct.unpack("<I", m[p:p + 4])
                fm, sf, tid, tan, cid = struct.unpack("<5I", m[p + 4:p + 24])
                alpha, = struct.unpack("<f", m[p + 24:p + 28])
                layers.append(dict(filter=FILTER.get(fm, str(fm)), flags=sf, tex=tid, alpha=alpha, texanim=tan))
                p += lsz
            mats.append(dict(priority=prio, flags=flags, layers=layers))
            i += size
    return mats


def parse_geosets(c):
    out = []
    for d in c.get("GEOS", []):
        i = 0
        while i + 4 <= len(d):
            size, = struct.unpack("<I", d[i:i + 4])
            g = d[i + 4:i + size]
            p = 0
            geo = {}

            def tag(t):
                nonlocal p
                assert g[p:p + 4] == t, f"GEOS 구조 어긋남: {g[p:p+4]} != {t}"
                p += 4

            tag(b"VRTX")
            n, = struct.unpack("<I", g[p:p + 4]); p += 4
            geo["verts"] = [struct.unpack("<3f", g[p + k * 12:p + k * 12 + 12]) for k in range(n)]; p += 12 * n
            tag(b"NRMS")
            n2, = struct.unpack("<I", g[p:p + 4]); p += 4
            geo["normals"] = [struct.unpack("<3f", g[p + k * 12:p + k * 12 + 12]) for k in range(n2)]; p += 12 * n2
            tag(b"PTYP")
            n, = struct.unpack("<I", g[p:p + 4]); p += 4
            geo["ptypes"] = list(struct.unpack("<%dI" % n, g[p:p + 4 * n])); p += 4 * n
            tag(b"PCNT")
            n, = struct.unpack("<I", g[p:p + 4]); p += 4
            geo["pcounts"] = list(struct.unpack("<%dI" % n, g[p:p + 4 * n])); p += 4 * n
            tag(b"PVTX")
            n, = struct.unpack("<I", g[p:p + 4]); p += 4
            geo["indices"] = list(struct.unpack("<%dH" % n, g[p:p + 2 * n])); p += 2 * n
            tag(b"GNDX")
            n, = struct.unpack("<I", g[p:p + 4]); p += 4
            geo["gndx"] = list(g[p:p + n]); p += n                  # 정점 → 행렬 그룹 번호
            tag(b"MTGC")
            n, = struct.unpack("<I", g[p:p + 4]); p += 4
            geo["mtgc"] = list(struct.unpack("<%dI" % n, g[p:p + 4 * n])); p += 4 * n   # 그룹별 뼈 개수
            tag(b"MATS")
            n, = struct.unpack("<I", g[p:p + 4]); p += 4
            geo["mats"] = list(struct.unpack("<%dI" % n, g[p:p + 4 * n])); p += 4 * n   # 뼈(노드) 번호 나열
            mid, sg, sf = struct.unpack("<3I", g[p:p + 12]); p += 12
            geo["material"] = mid
            p += 4 + 12 + 12                                      # boundsRadius, min, max
            nseq, = struct.unpack("<I", g[p:p + 4]); p += 4 + 28 * nseq
            tag(b"UVAS")
            nuv, = struct.unpack("<I", g[p:p + 4]); p += 4
            tag(b"UVBS")
            n, = struct.unpack("<I", g[p:p + 4]); p += 4
            geo["uvs"] = [struct.unpack("<2f", g[p + k * 8:p + k * 8 + 8]) for k in range(n)]; p += 8 * n
            # 삼각형(타입 4)만 모은다 — 워크3 지오셋은 사실상 전부 삼각형 목록
            tris, q = [], 0
            idx = geo["indices"]
            for t, cnt in zip(geo["ptypes"], geo["pcounts"]):
                if t == 4:
                    tris += [tuple(idx[q + k:q + k + 3]) for k in range(0, cnt - 2, 3)]
                q += cnt
            geo["tris"] = tris
            out.append(geo)
            i += size
    return out


def parse_counts(c):
    """파티클·리본·조명 개수(그리지 않는 것)."""
    def count(tag):
        n = 0
        for d in c.get(tag, []):
            i = 0
            while i + 4 <= len(d):
                sz, = struct.unpack("<I", d[i:i + 4])
                if sz <= 0:
                    break
                n += 1
                i += sz
        return n
    return dict(pre2=count("PRE2"), prem=count("PREM"), ribbons=count("RIBB"), lights=count("LITE"), bones=count("BONE"))


def parse(b):
    assert b[:4] == b"MDLX", "MDX가 아니다"
    c = chunks(b)
    return dict(textures=parse_textures(c), materials=parse_materials(c), geosets=parse_geosets(c), counts=parse_counts(c))
