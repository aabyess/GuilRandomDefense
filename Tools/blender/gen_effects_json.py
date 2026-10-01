"""초월 부가 이펙트·상시 오라 폴더마다 effects.json(기계가 읽는 부품 목록)을 만든다(2026-10-01). 시스템 python으로:

  TRANS=<analysis.json·mdx·_tex가 있는 transwork> AURA=<mdxwork> python3 Tools/blender/gen_effects_json.py [폴더이름 …]
이름을 주면 그 폴더만(예: 초월_구주호_AD), 안 주면 전부. 읽는 폴더: ~/GRD_motion_trial/초월_부가이펙트/* · 초월_상시오라/*.
스키마·단위 환산은 각 루트의 _effects_schema.md. 변환 좌표 = 유니티(-wy, wz, wx)×0.01 m — FBX(export_mdx_parts.py)와 같은 규약.
"""
import json, math, os, re, shutil, sys, collections
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "w3x"))
import mdx_anim, mdx_geo, mdx_effect_parts as EP

HOME = os.path.expanduser("~/GRD_motion_trial")
ROOT_T, ROOT_A = HOME + "/초월_부가이펙트", HOME + "/초월_상시오라"
_WORK = os.path.expanduser("~/GRD_motion_trial/_work")
TRANS, AURA = os.environ.get("TRANS") or _WORK + "/transwork", os.environ.get("AURA") or _WORK + "/mdxwork"
SC = 0.01
FM = {0: "blend", 1: "additive", 2: "modulate", 3: "modulate2x", 4: "alphakey"}
MESH_FILTER = {"none": "cut", "transparent": "cut", "blend": "blend", "additive": "add", "addalpha": "add", "modulate": "blend"}
AXIS_U = {"X": "Z", "Y": "X", "Z": "Y"}                           # 워크3 축 → 유니티 축(부호 무시)
ASSUMED_BODY = 180.0                                              # 몸이 없는 오라 모델의 기준 키(워크3, 캐릭터 약 150~200)

# 대응 로스터 폴더 → [(mdx, 뜯은 지오셋)] — gen_trans_extra_folders.VERDICT와 같은 판정
TRANS_FOLDERS = {
    "초월_구주호_AD": [("yamato6.mdx", []), ("rokugu_tr4.mdx", [5, 6])],
    "초월_김만경_AD": [("AkainuBW7.mdx", [2, 5])],
    "초월_최상호_AD": [("lb_jimbe.mdx", [0])],
    "초월_박기찬_AD": [("Mr.War3_Flanqi4.mdx", [1, 2, 10])],
    "영원_이지원": [("nikaluffy011.mdx", [14])],
    "초월_김경현_AP": [("Snakeman2a9.mdx", [1, 3, 5, 9, 10, 11, 13, 15, 16, 17, 19])],
    "초월_최상호_AP": [("mr.war3_dflmg5.mdx", [])],
    "초월_김민준_AP": [("tashigi17.mdx", [])],
    "초월_신문철_AP": [("luffy010_pf6.mdx", [])],
    "초월_황준석_ADAP": [("lb_jimbe.mdx", [0]), ("mr.war3_dflmg5.mdx", []), ("luffy010_pf6.mdx", []), ("bigmom7.mdx", [])],
    "_미대응/마르코_날개": [("mrk7.mdx", [3, 4, 5, 6])],
    "_미대응/쿠잔_얼음칼날": [("lb_kz.mdx", [8])],
    "_미대응/초승달_베기": [("BlSkill04A.mdx", [0])],
    "_미대응/큰_베기_호": [("lufei1.mdx", [0, 1])],
    "_미대응/검은_초승달": [("!cro.mdx", [5, 6])],
    "_미대응/구름_모자": [("zuse2.mdx", [0, 1])],
}


def u(v):                                                          # 워크3 (x,y,z) → 유니티 미터
    return [round(-v[1] * SC, 4), round(v[2] * SC, 4), round(v[0] * SC, 4)]


def seq_group(name):
    n = name.lower()
    if n.startswith(("stand", "idle")) or "portrait" in n: return None if "portrait" in n else "대기"
    if n.startswith(("walk", "move", "run")): return "이동"
    if n.startswith("attack"): return "공격"
    if n.startswith(("spell", "channel")): return "스킬"
    return None                                                    # Death·Birth·Decay·Dissipate 등은 우리 칸 밖


def ours_slot(states):
    vis = {g for s, g in states if g}
    if not vis: return "항상", []
    if "대기" in vis: return "항상", sorted(vis)                       # Stand에서 보이면 상시
    if "공격" in vis or "이동" in vis: return "공격", sorted(vis)      # 대기엔 숨고 움직일 때만(이동 포함은 ours_states 참고)
    return "스킬", sorted(vis)


def val_at(keys, t, default):
    cur = default
    for kt, v in keys:
        if kt <= t: cur = v[0]
        else: break
    return cur


def particle_visibility(info, p, base_rate):
    """원작 시퀀스마다 이 방출기가 살아 있는지(KP2V 가시 + KP2E 방출 수)."""
    out = []
    tr = p["tracks"]
    for s in info["sequences"]:
        vis = 1.0
        if "KP2V" in tr and tr["KP2V"]["keys"]:
            ks = tr["KP2V"]["keys"]
            vis = max([val_at(ks, s["start"], ks[0][1][0])] + [v[0] for t, v in ks if s["start"] < t <= s["end"]])
        rate = base_rate
        if "KP2E" in tr and tr["KP2E"]["keys"]:
            ks = tr["KP2E"]["keys"]
            rate = max([val_at(ks, s["start"], 0.0)] + [v[0] for t, v in ks if s["start"] < t <= s["end"]])
        out.append((s["name"], bool(vis > 0.01 and rate > 0.01)))
    return out


def chain_of(nodes_by_id, node):
    cur, chain, seen = node["parent"], [], set()
    while cur not in (-1, 0xFFFFFFFF) and cur in nodes_by_id and cur not in seen:
        seen.add(cur); chain.append(nodes_by_id[cur]["name"]); cur = nodes_by_id[cur]["parent"]
    return chain


def motion_of(info, nodes_by_id, bid, mat_idx):
    mo = dict(bone_rotations=[], bone_translations=[], layer_alpha=[])
    cur, seen = bid, set()
    while cur is not None and cur in nodes_by_id and cur not in seen:
        seen.add(cur); n = nodes_by_id[cur]
        for tag, tr in n["tracks"].items():
            if tag == "KGRT":
                r = mdx_anim.rotation_rate(info, tr)
                if r and r[0] >= 1: mo["bone_rotations"].append(dict(node=n["name"], deg_per_s=round(r[0], 1), axis_wc3=r[1], axis_unity=AXIS_U[r[1]], period_s=round(mdx_anim._period(info, tr), 3)))
            elif tag == "KGTR":
                ks = mdx_anim._window_keys(info, tr)
                if len(ks) > 1:
                    rng = {a: [round(min(v[i] for _, v in ks), 1), round(max(v[i] for _, v in ks), 1)] for i, a in enumerate("xyz")}
                    if any(b - a >= 1 for a, b in rng.values()): mo["bone_translations"].append(dict(node=n["name"], range_wc3=rng, period_s=round(mdx_anim._period(info, tr), 3)))
        p = n["parent"]; cur = p if p not in (-1, 0xFFFFFFFF) else None
    for la in info["layer_anims"]:
        if la["material"] == mat_idx and "KMTA" in la["tracks"]:
            ks = mdx_anim._window_keys(info, la["tracks"]["KMTA"])
            if len(ks) > 1:
                al = [v[0] for _, v in ks]
                mo["layer_alpha"].append(dict(layer=la["layer"], min=round(min(al), 3), max=round(max(al), 3), period_s=round(mdx_anim._period(info, la["tracks"]["KMTA"]), 3)))
    return mo


def tex_ref(model, tex_idx, workdir, approx, folder, notes):
    t = model["textures"][tex_idx]
    p = t["path"]
    if not p or t["replaceable"] in (1, 2): return dict(file=None, team_color=True, approx=False, original=p)
    fn = p.replace("\\", "_").replace("/", "_") + ".png"
    src = os.path.join(workdir, "_tex", fn)
    rel = None
    if os.path.exists(src):
        os.makedirs(os.path.join(folder, "Textures"), exist_ok=True)
        dst = os.path.join(folder, "Textures", fn)
        if not os.path.exists(dst): shutil.copy(src, dst)
        rel = "Textures/" + fn
    else:
        notes.append("텍스처 파일 없음: " + p)
    return dict(file=rel, team_color=False, approx=p in approx, original=p)


def build(folder, models, workdir, analysis, is_aura):
    approx = set(json.load(open(os.path.join(workdir, "_approx.json")))) if os.path.exists(os.path.join(workdir, "_approx.json")) else set()
    out = dict(schema=1, folder=os.path.relpath(folder, HOME), unit_rule="unity = (-wy, wz, wx) x 0.01 m (FBX와 같은 규약)", models=[], parts=[], notes=[])
    for mdxname, idxs in models:
        path = os.path.join(workdir, mdxname)
        if not os.path.exists(path):
            path = os.path.join(workdir, mdxname.lower())
        data = open(path, "rb").read()
        geo = mdx_geo.parse(data); info = mdx_anim.describe(data)
        tag = os.path.splitext(mdxname)[0]
        nodes_by_id = {n["id"]: n for n in info["nodes"]}
        for lst in ("pre2", "prem", "ribbons"):
            for e in info[lst]: nodes_by_id.setdefault(e["node"]["id"], e["node"])
        an = next((v for v in (analysis or {}).values() if v.get("file") == mdxname), None)
        body = an.get("body_height") if an else None
        assumed = body is None
        bh = body or ASSUMED_BODY
        out["models"].append(dict(model=mdxname, body_height_wc3=bh, body_height_assumed=assumed, body_height_m=round(bh * SC, 3),
                                  textures=[t["path"] for t in geo["textures"]], approx_textures=[t["path"] for t in geo["textures"] if t["path"] in approx],
                                  notes_ko=mdx_anim.text(info, geo["materials"])))
        def attach(node_name, pivot, chain, is_mesh=False):
            hum = EP.humanoid_of(chain)[0] if chain else "?"
            return dict(node=node_name, attach_bone=(node_name if is_mesh else (chain[0] if chain else node_name)), parent_chain=chain[:4], humanoid_hint=hum, wc3_pos=[round(x, 2) for x in pivot], unity_pos_m=u(pivot),
                        ratio_to_body=[round(-pivot[1] / bh, 4), round(pivot[2] / bh, 4), round(pivot[0] / bh, 4)])
        # ── 메시
        for gi in idxs:
            g = geo["geosets"][gi]
            bid, bname, chain = EP.geoset_bones(g, nodes_by_id)
            pivot = tuple(info["pivots"][bid]) if (bid is not None and bid < len(info["pivots"]) and not is_aura) else (0, 0, 0)
            if is_aura: pivot = (0, 0, 0)
            vis = EP.visible_by_sequence(info, gi)
            states = [(s, seq_group(s)) for s, v in (vis or []) if v > 0.01 and isinstance(s, str) and s != "항상"]
            slot, vs = ours_slot(states if vis and vis[0][0] != "항상" else [])
            mat = geo["materials"][g["material"]]
            objs = []
            for li, layer in enumerate(mat["layers"]):
                tx = geo["textures"][layer["tex"]]
                team = tx["replaceable"] in (1, 2) or not tx["path"] or "team" in tx["path"].lower()
                suf = "team" if team else MESH_FILTER.get(layer["filter"], "blend")
                objs.append(dict(fbx_object=f"{tag}_g{gi}_L{li}_{suf}", mdx_filter=layer["filter"], additive=layer["filter"] in ("additive", "addalpha"),
                                 texture=tex_ref(geo, layer["tex"], workdir, approx, folder, out["notes"]), skip=team))
            fbx = next((f for f in os.listdir(folder) if f.startswith(tag + "_g") and f.endswith(".fbx") and gi in [int(x) for x in re.findall(r"g([\d\-]+)", f)[0].split("-")]), None)
            bbox_lo = [round(min(v[i] for v in g["verts"]), 1) for i in range(3)]; bbox_hi = [round(max(v[i] for v in g["verts"]), 1) for i in range(3)]
            out["parts"].append(dict(kind="mesh", name=f"{tag}_g{gi}", model=mdxname, geoset=gi, fbx=fbx, layers=objs,
                                     attach=attach(bname, pivot, chain, True), fbx_origin_is_attach_pivot=True,
                                     visible=dict(original_sequences=vis, ours_states=vs, ours_slot=slot),
                                     size_wc3=[round(b - a, 1) for a, b in zip(bbox_lo, bbox_hi)], size_m=[round((b - a) * SC, 3) for a, b in zip(bbox_lo, bbox_hi)],
                                     motion=motion_of(info, nodes_by_id, bid, g["material"])))
        # ── 파티클
        for pi, p in enumerate(info["pre2"]):
            node = p["node"]; pv = tuple(info["pivots"][node["id"]]) if node["id"] < len(info["pivots"]) else (0, 0, 0)
            chain = chain_of(nodes_by_id, node)
            act = particle_visibility(info, p, p["rate"])
            slot, vs = ours_slot([(s, seq_group(s)) for s, a in act if a])
            never = not any(a for s, a in act)
            if never: slot = "없음"
            tx = tex_ref(geo, p["tex"], workdir, approx, folder, out["notes"])
            lat = p["latitude"]
            shape = "cone" if lat <= 90 else ("hemisphere" if lat <= 180 else "sphere")
            raw = {k: (list(v) if isinstance(v, tuple) else v) for k, v in p.items() if k not in ("node", "tracks")}
            raw["tracks"] = {k: dict(interp=t["interp"], gseq=t["gseq"], keys=[[kt, list(v)] for kt, v in t["keys"][:24]]) for k, t in p["tracks"].items()}
            sc = p["scale"]
            out["parts"].append(dict(
                kind="particle", name=f"{tag}_pre2_{pi}_{node['name']}", model=mdxname, emitter_name=node["name"],
                attach=attach(node["name"], pv, chain),
                visible=dict(original_sequences=[[s, a] for s, a in act], ours_states=vs, ours_slot=slot, never_active=never),
                texture=tx, additive=p["filter"] == 1, blend=FM.get(p["filter"], p["filter"]),
                flipbook=dict(rows=p["rows"], cols=p["cols"], tiles=p["rows"] * p["cols"], head_tail=["head", "tail", "both"][p["head"]] if p["head"] < 3 else p["head"]),
                raw=raw,
                unity=dict(emission_rate_per_s=round(p["rate"], 3), lifetime_s=round(p["lifespan"], 3), start_speed_mps=round(p["speed"] * SC, 4), speed_variation_fraction=round(p["variation"], 3),
                           shape=shape, cone_angle_deg=round(min(lat, 90.0), 1), latitude_wc3_deg=lat, box_size_m=[round(p["length"] * SC, 3), 0.0, round(p["width"] * SC, 3)],
                           gravity_modifier=round(p["gravity"] * SC / 9.81, 4), gravity_note="양수=아래로 떨어짐·음수=위로 솟음(원작 부호 그대로)",
                           size_m=[round(x * SC, 4) for x in sc], size_ratio_to_body=[round(x / bh, 4) for x in sc], mid_time_fraction=round(p["mid"], 3),
                           color_rgb=[[round(c, 3) for c in col] for col in p["colors"]], alpha=[round(a / 255, 3) for a in p["alpha"]],
                           tail_length=round(p["tail_len"], 3), squirt=p["squirt"], simulation_space="world",
                           note="방출 방향 = 노드 로컬 위(+Z→유니티 +Y). 방출 개수(KP2E)가 0↔양수로 켜지는 방출기는 raw.tracks.KP2E와 visible.original_sequences 참고")))
        for pi, p in enumerate(info["prem"]):
            node = p["node"]; pv = tuple(info["pivots"][node["id"]]) if node["id"] < len(info["pivots"]) else (0, 0, 0)
            out["parts"].append(dict(kind="particle", name=f"{tag}_prem_{pi}_{node['name']}", model=mdxname, emitter_name=node["name"], old_style_prem=True,
                                     attach=attach(node["name"], pv, chain_of(nodes_by_id, node)), raw={k: v for k, v in p.items() if k not in ("node", "tracks")}))
        # ── 리본
        for ri, r in enumerate(info["ribbons"]):
            node = r["node"]; pv = tuple(info["pivots"][node["id"]]) if node["id"] < len(info["pivots"]) else (0, 0, 0)
            mt = geo["materials"][r["material"]] if r["material"] < len(geo["materials"]) else None
            layer = mt["layers"][0] if mt and mt["layers"] else None
            tx = tex_ref(geo, layer["tex"], workdir, approx, folder, out["notes"]) if layer else None
            chain = chain_of(nodes_by_id, node)
            ks = r["tracks"]
            out["parts"].append(dict(
                kind="ribbon", name=f"{tag}_ribbon_{ri}_{node['name']}", model=mdxname, emitter_name=node["name"],
                attach=attach(node["name"], pv, chain),
                visible=dict(original_sequences=None, ours_states=[], ours_slot="항상", note="리본 가시 트랙(KRVS)은 안 읽음 — 이동·공격 때 궤적으로 쓸 것"),
                texture=tx, additive=bool(layer and layer["filter"] in ("additive", "addalpha")), mdx_filter=layer["filter"] if layer else None,
                flipbook=dict(rows=r["rows"], cols=r["cols"]),
                raw={k: (list(v) if isinstance(v, tuple) else v) for k, v in r.items() if k not in ("node", "tracks")},
                unity=dict(trail_time_s=round(r["lifespan"], 3), width_start_m=round((r["above"] + r["below"]) * SC, 4), width_end_m=round((r["above"] + r["below"]) * SC, 4),
                           width_ratio_to_body=round((r["above"] + r["below"]) / bh, 4), vertices_per_s=r["edges_per_sec"], color_rgb=[round(c, 3) for c in r["color"]], alpha=round(r["alpha"], 3),
                           gravity_modifier=round(r["gravity"] * SC / 9.81, 4), note="TrailRenderer 또는 LineRenderer 자작. min_vertex_distance는 vertices_per_s와 이동 속도로 정할 것")))
    return out


def main():
    want = set(sys.argv[1:])
    analysis = json.load(open(os.path.join(TRANS, "analysis.json")))
    done = []
    for name, models in TRANS_FOLDERS.items():
        if want and name not in want and name.split("/")[-1] not in want: continue
        folder = os.path.join(ROOT_T, name)
        if not os.path.isdir(folder): print("폴더 없음", folder); continue
        j = build(folder, models, TRANS, analysis, False)
        json.dump(j, open(os.path.join(folder, "effects.json"), "w"), ensure_ascii=False, indent=1)
        done.append((name, len(j["parts"])))
    for name in sorted(os.listdir(ROOT_A)):
        folder = os.path.join(ROOT_A, name)
        if not os.path.isdir(folder) or (want and name not in want): continue
        mdx = next((f for f in os.listdir(AURA) if os.path.splitext(f)[0].lower() == name.lower() and f.lower().endswith(".mdx")), None)
        if not mdx: print("mdx 없음", name); continue
        geo = mdx_geo.parse(open(os.path.join(AURA, mdx), "rb").read())
        j = build(folder, [(mdx, list(range(len(geo["geosets"]))))], AURA, None, True)
        json.dump(j, open(os.path.join(folder, "effects.json"), "w"), ensure_ascii=False, indent=1)
        done.append((name, len(j["parts"])))
    for d in done: print(*d)


main()
