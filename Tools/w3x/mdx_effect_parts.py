"""원작 초월 유닛 모델에서 **몸 메시와 부가 이펙트 부품을 가른다**(2026-10-01, 사장님 「스킨에 든 부가 이펙트를 뜯어 우리 초월에 붙여라」).

  python3 Tools/w3x/mdx_effect_parts.py <ord.mpq> <작업폴더> <models.json> [모델.mdl …]

models.json = {모델이름: {uids, rosters, inmap}} (조사 단계에서 만든 표). 이름을 주면 그것만, 안 주면 inmap 전부.
작업폴더/<모델>.mdx · _tex/ (mdx_extract와 같은 규칙: BLP→PNG, 맵에 없으면 근사) · analysis.json.

가르는 규칙(지오셋 단위):
  body        : 첫 층이 none/transparent(알파 컷)이고 이펙트 텍스처 이름이 아닌 것 — 옷·피부·머리·망토·무기 본체
  body+glow   : body인데 둘째 층부터 가산/알파가산이 얹힌 것(몸 위에 덧칠한 발광 무늬) — 따로 뜯지 않고 표시만
  effect      : 모든 층이 가산·알파가산·곱하기이거나, 텍스처 이름이 glow/flare/star/aura/ring/wave/flame/fire/smoke/light/beam/zap …
  teamglow    : 팀 색(replaceable 1·2) 판
  blendplane  : 알파 혼합(blend) 한 층짜리 판(그림자·번짐 등)
파티클(PRE2/PREM)·리본(RIBB)은 몸에 안 속하므로 전부 effect로 따로 적는다.
붙은 뼈: 지오셋 정점의 행렬 그룹(GNDX/MTGC/MATS)에서 가장 많은 뼈 → 이름·조상 → Humanoid 대응 어림.
보이는 때: 지오셋 알파 애니(GEOA KGAO)가 있으면 시퀀스 구간별 값, 없으면 정적 알파.
"""
import collections
import io
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mdx_anim                                                      # noqa: E402
import mdx_extract                                                   # noqa: E402
import mdx_geo                                                       # noqa: E402
from mpqread import Archive                                          # noqa: E402

# 파일 이름이 이펙트처럼 보이는지 — 낱말 경계로(Smoker·Ball!·Nice 같은 우연 일치를 피한다)
EFFECT_NAME = re.compile(r"glow|flare|aura|halo|beam|zap|lightning|flash|shine|trail|ribbon|spark|swoosh|slash|baozha|dust|funnel|(?<![a-z])(ring|wave|star\d*|skill|shock)(?![a-z])", re.I)
HUMAN_PATTERNS = [
    ("RightHand", r"(\br\b|right|_r\b|^r[ _]?hand|rhand|hand[ _]?r\b|rweapon|weapon[ _]?r\b|wp[_ ]?r\b)"),
    ("LeftHand", r"(\bl\b|left|_l\b|^l[ _]?hand|lhand|hand[ _]?l\b|lweapon|weapon[ _]?l\b|wp[_ ]?l\b)"),
]


def humanoid_of(chain):
    """뼈 이름 + 조상 목록(가까운 쪽부터) → (Humanoid 대응 이름, 근거 이름). 못 정하면 ('?', 이름)."""
    for name in chain:
        low = name.lower()
        side = None
        if re.search(r"(^|[ _\-])r([ _\-]|$)|right|rhand|r_hand|rweapon|hand_r|wp[_ ]?r", low):
            side = "Right"
        if re.search(r"(^|[ _\-])l([ _\-]|$)|left|lhand|l_hand|lweapon|hand_l|wp[_ ]?l", low):
            side = "Left"
        if re.search(r"weapon|sword|gun|katana|blade|knife|axe|staff|spear|hammer|wp\d?$|wp[_ ]|cannon|shield", low):
            return (f"{side or 'Right'}Hand(무기)", name)
        if re.search(r"hand|palm|finger|thumb|wrist|forearm|fore[ _]?arm", low):
            return (f"{side or 'Right'}Hand", name)
        if re.search(r"head|face|hair|neck|jaw|eye|hat", low):
            return ("Head", name)
        if re.search(r"foot|toe|calf|shin|ankle", low):
            return (f"{side or 'Left'}Foot", name)
        if re.search(r"thigh|leg|knee", low):
            return (f"{side or 'Left'}UpLeg", name)
        if re.search(r"chest|spine|clavicle|shoulder|upperarm|upper[ _]?arm|breast|back|cape|cloak", low):
            return ("Spine1(가슴)", name)
        if re.search(r"pelvis|hip|root|body|waist|bip0?1$", low):
            return ("Hips", name)
    return ("?", chain[0] if chain else "")


def geoset_bones(geo, nodes_by_id):
    """정점 그룹에서 가장 많이 쓰인 뼈 id → (id, 이름, 조상 이름 목록)."""
    cnt = collections.Counter()
    offs, o = [], 0
    for c in geo["mtgc"]:
        offs.append((o, c))
        o += c
    for gi in geo["gndx"]:
        if gi < len(offs):
            s, c = offs[gi]
            if c:
                cnt[geo["mats"][s]] += 1
    if not cnt:
        return None, "", []
    bid = cnt.most_common(1)[0][0]
    chain, cur, seen = [], bid, set()
    while cur is not None and cur in nodes_by_id and cur not in seen:
        seen.add(cur)
        chain.append(nodes_by_id[cur]["name"])
        p = nodes_by_id[cur]["parent"]
        cur = p if p != -1 and p != 0xFFFFFFFF and p in nodes_by_id else None
    return bid, (chain[0] if chain else ""), chain


def classify(geo, mat, textures):
    layers = mat["layers"]
    filters = [l["filter"] for l in layers]
    names = []
    team = False
    for l in layers:
        t = textures[l["tex"]]
        if t["replaceable"] in (1, 2) or "teamcolor" in t["path"].lower() or "teamglow" in t["path"].lower():
            team = True
        names.append(t["path"].lower())
    eff_name = any(EFFECT_NAME.search(os.path.basename(n.replace("\\", "/"))) for n in names)
    glow_only = all(f in ("additive", "addalpha") for f in filters)
    if team and all(f in ("additive", "addalpha", "blend") for f in filters):
        return "teamglow"
    if glow_only:
        return "effect"
    if filters[0] in ("none", "transparent"):
        if eff_name and not any(f in ("none", "transparent") for f in filters[1:]) and len(filters) == 1:
            return "effect"
        if any(f in ("additive", "addalpha") for f in filters[1:]):
            return "body+glow"
        return "body"
    if filters[0] == "blend":
        return "effect" if eff_name else "blendplane"
    return "body"                                                          # modulate(곱하기)만인 층은 구운 그림자라 몸


def visible_by_sequence(info, geoset_index):
    """GEOA 알파 키로 시퀀스마다 보이는지(없으면 None = 항상)."""
    ga = [g for g in info["geoa"] if g["geoset"] == geoset_index]
    if not ga:
        return None
    g = ga[0]
    tr = g["tracks"].get("KGAO")
    if not tr or not tr["keys"]:
        return [("항상", round(g["alpha"], 2))]
    out = []
    for s in info["sequences"]:
        ks = [v[0] for t, v in tr["keys"] if s["start"] <= t <= s["end"]]
        if not ks:
            # 구간 안에 키가 없으면 직전 키 값
            prev = [v[0] for t, v in tr["keys"] if t < s["start"]]
            ks = [prev[-1]] if prev else [g["alpha"]]
        out.append((s["name"], round(max(ks), 2)))
    return out


def analyze(name, data):
    geo = mdx_geo.parse(data)
    info = mdx_anim.describe(data)
    nodes_by_id = {n["id"]: n for n in info["nodes"]}
    # PRE2 등은 노드 id가 info['nodes']에 없다 → 파티클 노드는 따로 넣는다
    for lst in ("pre2", "prem", "ribbons"):
        for e in info[lst]:
            nodes_by_id.setdefault(e["node"]["id"], e["node"])
    res = dict(model=name, textures=[t["path"] or f"repl{t['replaceable']}" for t in geo["textures"]], geosets=[], particles=[], ribbons=[], counts=geo["counts"])
    allv = []
    body_idx = []
    for gi, g in enumerate(geo["geosets"]):
        mat = geo["materials"][g["material"]]
        cls = classify(g, mat, geo["textures"])
        bid, bname, chain = geoset_bones(g, nodes_by_id)
        hum = humanoid_of(chain)
        vs = g["verts"]
        lo = [min(v[i] for v in vs) for i in range(3)]
        hi = [max(v[i] for v in vs) for i in range(3)]
        pivot = info["pivots"][bid] if bid is not None and bid < len(info["pivots"]) else (0, 0, 0)
        vis = visible_by_sequence(info, gi)
        if cls == "body" and vis and len(g["verts"]) >= 8:
            stand = next((v for s, v in vis if s.lower().startswith("stand")), None)
            if stand is not None and stand <= 0.01:
                cls = "conditional"                                       # Stand에선 숨고 다른 동작에서만 보임(변신 날개·스킬 부속 등)
        row = dict(index=gi, cls=cls, verts=len(vs), tris=len(g["tris"]), bbox_lo=[round(x, 1) for x in lo], bbox_hi=[round(x, 1) for x in hi],
                   layers=[(l["filter"], geo["textures"][l["tex"]]["path"] or f"repl{geo['textures'][l['tex']]['replaceable']}") for l in mat["layers"]],
                   bone=bname, chain=chain[:5], humanoid=hum[0], bone_pivot=[round(x, 1) for x in pivot],
                   center=[round((lo[i] + hi[i]) / 2, 1) for i in range(3)], visible=vis)
        res["geosets"].append(row)
        if cls in ("body", "body+glow"):
            allv += vs
            body_idx.append(gi)
    if not body_idx and res["geosets"]:
        # 몸 지오셋이 하나도 없다 = 모델 전체가 이펙트형(구름·더미) — 부품이 아니라 그 자체가 모델
        for r_ in res["geosets"]:
            if r_["cls"] == "effect":
                r_["cls"] = "all-effect-model"
        res["note"] = "모델 전체가 이펙트형 지오셋뿐(몸 없음)"
    if allv:
        res["body_height"] = round(max(v[2] for v in allv) - min(v[2] for v in allv), 1)
        res["body_bbox"] = [[round(min(v[i] for v in allv), 1) for i in range(3)], [round(max(v[i] for v in allv), 1) for i in range(3)]]
    for p in info["pre2"] + info["prem"]:
        node = p["node"]
        cur, chain, seen = node["parent"], [], set()
        while cur not in (-1, 0xFFFFFFFF) and cur in nodes_by_id and cur not in seen:
            seen.add(cur)
            chain.append(nodes_by_id[cur]["name"])
            cur = nodes_by_id[cur]["parent"]
        pv = info["pivots"][node["id"]] if node["id"] < len(info["pivots"]) else (0, 0, 0)
        res["particles"].append(dict(name=node["name"], chain=chain[:4], humanoid=humanoid_of(chain)[0], pivot=[round(x, 1) for x in pv]))
    for r in info["ribbons"]:
        node = r["node"]
        cur, chain, seen = node["parent"], [], set()
        while cur not in (-1, 0xFFFFFFFF) and cur in nodes_by_id and cur not in seen:
            seen.add(cur)
            chain.append(nodes_by_id[cur]["name"])
            cur = nodes_by_id[cur]["parent"]
        res["ribbons"].append(dict(name=node["name"], chain=chain[:4], humanoid=humanoid_of(chain)[0]))
    res["anim_text"] = mdx_anim.text(info)
    return res


def main():
    mpq, work, models_json = sys.argv[1], sys.argv[2], sys.argv[3]
    only = set(sys.argv[4:])
    os.makedirs(os.path.join(work, "_tex"), exist_ok=True)
    arc = Archive(mpq)
    models = json.load(open(models_json))
    out = {}
    approx = set(json.load(open(os.path.join(work, "_approx.json")))) if os.path.exists(os.path.join(work, "_approx.json")) else set()
    for name, d in models.items():
        if only and name not in only:
            continue
        if not d.get("inmap"):
            continue
        mx = name.replace(".mdl", ".mdx").replace(".MDL", ".mdx")
        data = arc.read(mx) or arc.read(mx.split("\\")[-1])
        key = mx.replace("\\", "_")
        open(os.path.join(work, key), "wb").write(data)
        geo = mdx_geo.parse(data)
        for t in geo["textures"]:
            path = t["path"]
            if not path or t["replaceable"] in (1, 2) or "teamcolor" in path.lower() or "teamglow" in path.lower():
                continue
            png = os.path.join(work, "_tex", path.replace("\\", "_").replace("/", "_") + ".png")
            if os.path.exists(png):
                continue
            blp = arc.read(path) or arc.read(os.path.basename(path.replace("\\", "/")))
            from PIL import Image
            if blp is not None:
                Image.open(io.BytesIO(blp)).convert("RGBA").save(png)
            else:
                approx.add(path)
                mdx_extract.placeholder(path).save(png)
        try:
            r = analyze(name, data)
        except Exception as e:                                        # 하나가 깨져도 나머지는 계속
            r = dict(model=name, error=str(e))
        r.update(file=key, uids=d["uids"], rosters=d["rosters"])
        out[name] = r
        print(name, "ok" if "error" not in r else "ERR " + r["error"][:80], collections.Counter(g["cls"] for g in r.get("geosets", [])), "particles", len(r.get("particles", [])), "ribbons", len(r.get("ribbons", [])))
    json.dump(out, open(os.path.join(work, "analysis.json"), "w"), ensure_ascii=False, indent=1)
    json.dump(sorted(approx), open(os.path.join(work, "_approx.json"), "w"), ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
