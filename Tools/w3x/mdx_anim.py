"""워크3 MDX(v800) — 노드(뼈·헬퍼)·파티클(PRE2/PREM)·리본(RIBB)·지오셋 애니메이션·텍스처 애니메이션·층 알파 애니메이션 요약.

mdx_geo.py가 **모양**(지오셋)을 읽는다면 여기는 **움직임과 파티클**을 읽는다. 유니티로 옮길 때 FBX에 안 실리는 것들
(파티클·UV 스크롤·알파 맥동·뼈 회전)을 설명표로 옮기려는 용도다(2026-10-01 원작 상시 오라).

  describe(bytes) → dict(
      pivots, nodes[{name, id, parent, kind, pos, tracks{T|R|S:{keys, interp, gseq}}}],
      pre2[{node, fields…}], prem[...], ribbons[...], geoa[...], txan[...], layer_anims[{material, layer, alpha_keys}],
      sequences[{name, start, end, looping}], global_seqs[ms…])
  text(dict) → 사람이 읽는 한국어 줄 목록.

한계: 보간(Hermite/Bezier 접선)은 값만 읽고 곡선은 안 만든다. 회전 속도는 키 사이 각도 합 ÷ 시간으로 어림한다.
"""
import math
import struct

import mdx_geo

U32 = struct.Struct("<I")


def _u32(b, p):
    return U32.unpack_from(b, p)[0]


def _f(b, p, n=1):
    return struct.unpack_from("<%df" % n, b, p)


def _cstr(x):
    return x.split(b"\0")[0].decode("latin1")


TRACK_DIM = {b"KGTR": 3, b"KGRT": 4, b"KGSC": 3, b"KGAO": 1, b"KGAC": 3, b"KMTA": 1, b"KMTF": 1, b"KTAT": 3, b"KTAR": 4, b"KTAS": 3,
             b"KPEE": 1, b"KPEG": 1, b"KPLN": 1, b"KPLT": 1, b"KPEL": 1, b"KPES": 1, b"KPEV": 1, b"KP2E": 1, b"KP2G": 1, b"KP2L": 1, b"KP2S": 1,
             b"KP2V": 1, b"KP2R": 1, b"KP2N": 1, b"KP2W": 1, b"KP2N": 1, b"KRHA": 1, b"KRHB": 1, b"KRAL": 1, b"KRCO": 3, b"KRTX": 1, b"KRVS": 1,
             b"KVIS": 1}
INT_TRACK = {b"KMTF", b"KRTX"}


def read_tracks(b, p, end):
    """b[p:end]에서 K*** 트랙들을 읽는다 → {tag: dict(interp, gseq, keys[(ms, tuple)])}."""
    out = {}
    while p + 4 <= end:
        tag = bytes(b[p:p + 4])
        if tag not in TRACK_DIM:
            break
        n, interp, gseq = struct.unpack_from("<IIi", b, p + 4)
        dim = TRACK_DIM[tag]
        p += 16
        keys = []
        for _ in range(n):
            t, = struct.unpack_from("<i", b, p)
            p += 4
            if tag in INT_TRACK:
                v = (struct.unpack_from("<I", b, p)[0],)
                p += 4
            else:
                v = _f(b, p, dim)
                p += 4 * dim
            if interp > 1 and tag not in INT_TRACK:
                p += 8 * dim                                   # in/out 접선
            keys.append((t, v))
        out[tag.decode()] = dict(interp=interp, gseq=gseq, keys=keys)
    return out


def read_node(b, p):
    size = _u32(b, p)
    name = _cstr(b[p + 4:p + 84])
    oid, parent, flags = struct.unpack_from("<IiI", b, p + 84)
    tr = read_tracks(b, p + 96, p + size)
    return dict(name=name, id=oid, parent=parent, flags=flags, tracks=tr), size


def entries(d):
    """[size][...] 연속 레코드를 (오프셋, 크기)로 돈다."""
    i = 0
    while i + 4 <= len(d):
        sz = _u32(d, i)
        if sz <= 0 or i + sz > len(d):
            break
        yield i, sz
        i += sz


def describe(data):
    c = mdx_geo.chunks(data)
    out = dict(nodes=[], pre2=[], prem=[], ribbons=[], geoa=[], txan=[], layer_anims=[], sequences=[], global_seqs=[], pivots=[])
    for d in c.get("PIVT", []):
        out["pivots"] += [tuple(struct.unpack_from("<3f", d, k * 12)) for k in range(len(d) // 12)]
    for d in c.get("GLBS", []):
        out["global_seqs"] += [_u32(d, k * 4) for k in range(len(d) // 4)]
    for d in c.get("SEQS", []):
        for k in range(len(d) // 132):
            e = d[k * 132:(k + 1) * 132]
            s, t, ms, fl = struct.unpack("<IIfI", e[80:96])
            out["sequences"].append(dict(name=_cstr(e[:80]), start=s, end=t, looping=not (fl & 1)))
    for kind, extra in (("BONE", 8), ("HELP", 0)):                 # BONE = 노드 + geosetId + geosetAnimId
        for d in c.get(kind, []):
            i = 0
            while i + 4 <= len(d):
                n, nsz = read_node(d, i)
                n["kind"] = kind
                if extra:
                    n["geoset"], n["geoset_anim"] = struct.unpack_from("<ii", d, i + nsz)
                out["nodes"].append(n)
                i += nsz + extra
    # PRE2
    for d in c.get("PRE2", []):
        for i, sz in entries(d):
            node, nsz = read_node(d, i + 4)                      # [레코드 크기][노드…]
            p = i + 4 + nsz
            speed, var, lat, grav, life, rate, width, length = _f(d, p, 8); p += 32
            filt, rows, cols, head = struct.unpack_from("<4I", d, p); p += 16
            tail_len, mid = _f(d, p, 2); p += 8
            col = [_f(d, p + k * 12, 3) for k in range(3)]; p += 36
            alpha = list(d[p:p + 3]); p += 3
            scale = _f(d, p, 3); p += 12
            p += 48
            tex, squirt, prio, repl = struct.unpack_from("<IIiI", d, p); p += 16
            tr = read_tracks(d, p, i + sz)
            out["pre2"].append(dict(node=node, speed=speed, variation=var, latitude=lat, gravity=grav, lifespan=life, rate=rate, width=width, length=length,
                                    filter=filt, rows=rows, cols=cols, head=head, tail_len=tail_len, mid=mid, colors=col, alpha=alpha, scale=scale,
                                    tex=tex, squirt=bool(squirt), tracks=tr))
    # PREM(옛 파티클)
    for d in c.get("PREM", []):
        for i, sz in entries(d):
            node, nsz = read_node(d, i + 4)
            p = i + 4 + nsz
            rate, grav, lon, lat = _f(d, p, 4); p += 16
            path = _cstr(d[p:p + 260]); p += 260
            life, vel = _f(d, p, 2); p += 8
            out["prem"].append(dict(node=node, rate=rate, gravity=grav, longitude=lon, latitude=lat, model=path, lifespan=life, speed=vel,
                                    tracks=read_tracks(d, p, i + sz)))
    # RIBB
    for d in c.get("RIBB", []):
        for i, sz in entries(d):
            node, nsz = read_node(d, i + 4)
            p = i + 4 + nsz
            above, below, alpha = _f(d, p, 3); p += 12
            color = _f(d, p, 3); p += 12
            life, = _f(d, p, 1); p += 4
            tslot, eps, rows, cols, mat = struct.unpack_from("<5I", d, p); p += 20
            grav, = _f(d, p, 1); p += 4
            out["ribbons"].append(dict(node=node, above=above, below=below, alpha=alpha, color=color, lifespan=life, edges_per_sec=eps, rows=rows, cols=cols,
                                       material=mat, gravity=grav, tracks=read_tracks(d, p, i + sz)))
    # GEOA
    for d in c.get("GEOA", []):
        for i, sz in entries(d):
            alpha, flags = struct.unpack_from("<fI", d, i + 4)
            color = _f(d, i + 12, 3)
            gid = _u32(d, i + 24)
            out["geoa"].append(dict(alpha=alpha, flags=flags, color=color, geoset=gid, tracks=read_tracks(d, i + 28, i + sz)))
    # TXAN
    for d in c.get("TXAN", []):
        for i, sz in entries(d):
            out["txan"].append(dict(tracks=read_tracks(d, i + 4, i + sz)))
    # 층 알파·텍스처 번호 애니메이션
    for mi, d in enumerate(c.get("MTLS", [])):
        pass
    for d in c.get("MTLS", []):
        mi = 0
        for i, sz in entries(d):
            m = d[i:i + sz]
            nl = _u32(m, 16)
            p = 20
            for li in range(nl):
                lsz = _u32(m, p)
                tr = read_tracks(m, p + 28, p + lsz)
                if tr:
                    out["layer_anims"].append(dict(material=mi, layer=li, tracks=tr))
                p += lsz
            mi += 1
    return out


# ───────── 사람이 읽는 줄 ─────────
def _stand_window(info):
    for s in info["sequences"]:
        if s["name"].lower().startswith("stand"):
            return s["start"], s["end"]
    return (info["sequences"][0]["start"], info["sequences"][0]["end"]) if info["sequences"] else (0, 0)


def _period(info, tr):
    """트랙의 반복 주기(초): 전역 시퀀스면 그 길이, 아니면 Stand 구간 길이."""
    if tr["gseq"] >= 0 and tr["gseq"] < len(info["global_seqs"]):
        return info["global_seqs"][tr["gseq"]] / 1000.0
    s, e = _stand_window(info)
    return (e - s) / 1000.0 if e > s else 0.0


def _window_keys(info, tr):
    if tr["gseq"] >= 0:
        return tr["keys"]
    s, e = _stand_window(info)
    return [k for k in tr["keys"] if s <= k[0] <= e] or tr["keys"]


def _quat_angle(q1, q2):
    d = abs(sum(a * b for a, b in zip(q1, q2)))
    return 2 * math.acos(min(1.0, d))


def rotation_rate(info, tr):
    keys = _window_keys(info, tr)
    if len(keys) < 2:
        return None
    tot = 0.0
    axes = [0.0, 0.0, 0.0]
    for (t1, q1), (t2, q2) in zip(keys, keys[1:]):
        a = _quat_angle(q1, q2)
        tot += a
        # 델타 축 = q2 * conj(q1) 의 벡터부
        x1, y1, z1, w1 = q1
        x2, y2, z2, w2 = q2
        dx = w2 * -x1 + x2 * w1 + y2 * -z1 - z2 * -y1
        dy = w2 * -y1 - x2 * -z1 + y2 * w1 + z2 * -x1
        dz = w2 * -z1 + x2 * -y1 - y2 * -x1 + z2 * w1
        n = math.sqrt(dx * dx + dy * dy + dz * dz) or 1
        for k, v in enumerate((dx / n, dy / n, dz / n)):
            axes[k] += abs(v) * a
    span = (keys[-1][0] - keys[0][0]) / 1000.0
    if span <= 0:
        return None
    ax = "XYZ"[axes.index(max(axes))]
    return math.degrees(tot) / span, ax, span


def text(info, geo_materials=None):
    """한 모델의 움직임·파티클 줄 목록."""
    L = []
    stand = [s for s in info["sequences"]]
    if stand:
        L.append("시퀀스: " + ", ".join(f"{s['name']} {s['end'] - s['start']}ms{'' if s['looping'] else '(1회)'}" for s in stand[:4]))
    if info["global_seqs"]:
        L.append("전역 시퀀스(반복 주기): " + ", ".join(f"{g / 1000:.2f}초" for g in info["global_seqs"]))
    for n in info["nodes"]:
        for tag, tr in n["tracks"].items():
            if tag == "KGRT":
                r = rotation_rate(info, tr)
                if r:
                    L.append(f"회전: 뼈 {n['name']} — {r[0]:.0f}°/초 (주 축 {r[1]}, 구간 {r[2]:.2f}초, 키 {len(tr['keys'])}개)")
            elif tag == "KGSC":
                ks = _window_keys(info, tr)
                if len(ks) > 1:
                    xs = [v[0] for _, v in ks]
                    L.append(f"스케일 맥동: 뼈 {n['name']} — x {min(xs):.2f}~{max(xs):.2f} (주기 {_period(info, tr):.2f}초)")
            elif tag == "KGTR":
                ks = _window_keys(info, tr)
                if len(ks) > 1:
                    zs = [v[2] for _, v in ks]; xs = [v[0] for _, v in ks]; ys = [v[1] for _, v in ks]
                    L.append(f"이동: 뼈 {n['name']} — x {min(xs):.0f}~{max(xs):.0f} y {min(ys):.0f}~{max(ys):.0f} z {min(zs):.0f}~{max(zs):.0f} (주기 {_period(info, tr):.2f}초)")
    for g in info["geoa"]:
        for tag, tr in g["tracks"].items():
            ks = _window_keys(info, tr)
            if tag == "KGAO" and len(ks) > 1:
                al = [v[0] for _, v in ks]
                L.append(f"지오셋 {g['geoset']} 알파 변화: {min(al):.2f}~{max(al):.2f} (주기 {_period(info, tr):.2f}초)")
    for i, t in enumerate(info["txan"]):
        for tag, tr in t["tracks"].items():
            ks = _window_keys(info, tr)
            if len(ks) < 2:
                continue
            if tag == "KTAT":
                dx = ks[-1][1][0] - ks[0][1][0]; dy = ks[-1][1][1] - ks[0][1][1]
                span = (ks[-1][0] - ks[0][0]) / 1000.0 or 1
                L.append(f"텍스처 {i} UV 스크롤: Δu {dx:.2f} Δv {dy:.2f} / {span:.2f}초 (주기 {_period(info, tr):.2f}초)")
            elif tag == "KTAR":
                r = rotation_rate(info, tr)
                if r:
                    L.append(f"텍스처 {i} UV 회전: {r[0]:.0f}°/초 (주기 {_period(info, tr):.2f}초)")
            elif tag == "KTAS":
                L.append(f"텍스처 {i} UV 스케일 변화 {ks[0][1][0]:.2f}→{ks[-1][1][0]:.2f}")
    for la in info["layer_anims"]:
        for tag, tr in la["tracks"].items():
            ks = _window_keys(info, tr)
            if tag == "KMTA" and len(ks) > 1:
                al = [v[0] for _, v in ks]
                L.append(f"재질 {la['material']} 층 {la['layer']} 알파 맥동: {min(al):.2f}~{max(al):.2f} (주기 {_period(info, tr):.2f}초)")
    FM = {0: "블렌드", 1: "가산", 2: "곱하기", 3: "곱하기2배", 4: "알파키"}
    for p in info["pre2"]:
        c0, c1, c2 = p["colors"]
        rate_anim = " · 방출 개수 애니 있음" if any(t in p["tracks"] for t in ("KP2E",)) else ""
        L.append(f"파티클(PRE2) {p['node']['name']}: 필터 {FM.get(p['filter'], p['filter'])} · 초당 {p['rate']:.1f}개(수명 {p['lifespan']:.2f}초) · 속도 {p['speed']:.0f}±{p['variation']:.2f} · "
                 f"방출 {p['width']:.0f}×{p['length']:.0f} · 위도 {p['latitude']:.0f}° · 중력 {p['gravity']:.0f} · "
                 f"색 {tuple(int(v * 255) for v in c0)}→{tuple(int(v * 255) for v in c1)}→{tuple(int(v * 255) for v in c2)} · 알파 {p['alpha']} · 크기 {p['scale'][0]:.0f}/{p['scale'][1]:.0f}/{p['scale'][2]:.0f} · "
                 f"텍스처 #{p['tex']}{'(플립북 %d×%d)' % (p['rows'], p['cols']) if p['rows'] * p['cols'] > 1 else ''} · 위치 pivot {info['pivots'][p['node']['id']] if p['node']['id'] < len(info['pivots']) else '?'}{rate_anim}")
    for p in info["prem"]:
        L.append(f"파티클(PREM 옛 방식) {p['node']['name']}: 모델 {p['model']} · 초당 {p['rate']:.1f} · 수명 {p['lifespan']:.2f} · 속도 {p['speed']:.0f}")
    for r in info["ribbons"]:
        L.append(f"리본 {r['node']['name']}: 높이 {r['above']:.0f}/{r['below']:.0f} · 수명 {r['lifespan']:.2f}초 · 초당 {r['edges_per_sec']}마디 · 색 {tuple(int(v * 255) for v in r['color'])} 알파 {r['alpha']:.2f}")
    return L
