"""원작 이펙트 모델 json(export_mdx_anim_fbx 산출)에 유니티 반입용 블록을 붙인다(2026-10-18).

  /usr/bin/python3 Tools/w3x/vfx_unity_block.py <모델폴더 루트> [--check]
    루트/<모델>/<모델>.json(+ 루트/_work/<원본 mdx>) → json에 "unity"(OriginalVfxBuilder가 읽는 블록)와
    "pre2Unity"(CinematicImporter 대본 models[이름].pre2 와 같은 원소 — Tools/w3x/pre2_unity.py 한 함수로 만듦) 추가,
    입자 텍스처 PNG를 <모델>/Textures/로 복사.
    --check : 옛 ~/grd/GRD_orig_vfx_trial 의 unity 블록과 이 도구로 다시 만든 것을 대조만(쓰지 않음).
unity 블록 형식(옛 GRD_orig_vfx_trial 그대로): durationSec·loop(첫 클립) · clips[name,sec,loop,mdxStartMs,mdxEndMs] · units ·
  textureBytes · particlesPRE2Excluded · layers[mesh,blend,mdxFilter,texture,approxTexture,staticAlpha,alphaCurveGlobalSec,(alphaCurveClip0Sec),(uvOffsetClip0)].
입자 전용(메시 0) 모델은 layers=[] (PM 10-18: 빈 루트+pre2Unity로 지어진다).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import mdx_anim                                                      # noqa: E402
import mdx_geo                                                       # noqa: E402
import pre2_unity as P2                                              # noqa: E402

BLEND = {"addalpha": "Additive(알파곱)", "additive": "Additive", "blend": "AlphaBlend", "transparent": "AlphaTest", "none": "Opaque",
         "modulate": "Multiply", "modulate2x": "Multiply2x"}
UNITS = "초·미터(FBX 크기 그대로; 워크3 1=0.01m)"
UVNOTE = "MDX UV 오프셋(워크3은 +, Unity는 v 반전: offsetV=-v). [초(첫 클립 기준),u,v]"


def r3(x):
    return round(x / 1000.0, 3)


def unity_block(side, info, mdir):
    seqs = info["sequences"]
    names = [q["name"] for q in side.get("sequences", [])]          # export가 정한 이름(공백 제거 등)을 그대로
    clips = [dict(name=names[i] if i < len(names) else f"s{i}_{s['name']}", sec=max(r3(s["end"] - s["start"]), 0.001), loop=bool(s["looping"]), mdxStartMs=s["start"], mdxEndMs=s["end"])
             for i, s in enumerate(seqs)]
    c0 = seqs[0] if seqs else dict(start=0, end=0)
    layers = []
    for m in side["meshes"]:
        keys = sorted(list(m.get("layerAlphaKeys") or []) + list(m.get("geosetAlphaKeys") or []), key=lambda k: (k[0], k[1][0]))   # 같은 시각이면 값 작은 것 먼저(옛 반입과 같은 순서)
        L = dict(mesh=m["mesh"], blend=BLEND.get(m["filter"], m["filter"]), mdxFilter=m["filter"], texture=m.get("textureFile"),
                 approxTexture=m.get("approxTexture", False), staticAlpha=m["staticAlpha"] * (m.get("geosetAlphaStatic") if m.get("geosetAlphaStatic") is not None else 1.0))
        if keys:
            L["alphaCurveClip0Sec"] = [[r3(k[0] - c0["start"]), k[1][0]] for k in keys if c0["start"] - 1 <= k[0] <= c0["end"] + 1]
            L["alphaCurveGlobalSec"] = [[r3(k[0]), k[1][0]] for k in keys]
        else:
            L["alphaCurveGlobalSec"] = None
        uv = (m.get("uvAnim") or {}).get("tracks", {}).get("KTAT") if m.get("uvAnim") else None
        if uv:
            L["uvOffsetClip0"] = dict(interp="STEP" if uv.get("interp") == 0 else "LINEAR", note=UVNOTE,
                                      keys=[[r3(k[0] - c0["start"]), k[1][0], k[1][1]] for k in uv["keys"] if c0["start"] <= k[0] <= c0["end"]])
        layers.append(L)
    texdir = os.path.join(mdir, "Textures")
    tb = sum(os.path.getsize(os.path.join(texdir, t)) for t in os.listdir(texdir)) if os.path.isdir(texdir) else 0   # Textures/ 폴더 전체(입자 텍스처 포함)
    return dict(durationSec=max((c["sec"] for c in clips), default=0.0), loop=any(c["loop"] for c in clips), clips=clips, units=UNITS,
                textureBytes=tb, particlesPRE2Excluded=len(side.get("pre2", [])), layers=layers)


def find_mdx(root, side):
    for p in (os.path.join(root, "_work", side["model"]),):
        if os.path.exists(p):
            return p
    H = os.path.expanduser("~")
    for w in ("original_vfx", "transcend_vfx", "grade_extra", "original_skin", "s2_skin"):
        p = H + f"/grd/GRD_motion_trial/{w}/work/" + side["model"]
        if os.path.exists(p):
            return p
    return None


def process(root, tag, write=True):
    mdir = os.path.join(root, tag)
    jp = os.path.join(mdir, tag + ".json")
    side = json.load(open(jp))
    src = find_mdx(root, side)
    raw = open(src, "rb").read()
    info = mdx_anim.describe(raw)
    geo = mdx_geo.parse(raw)
    ub = unity_block(side, info, mdir)
    if not write:
        return side, ub
    ss = P2.stand_seq(info)
    missing = []
    side["pre2Unity"] = P2.pre2_entries(info, geo, mdir, os.path.dirname(src), ss["start"], None, missing, tag)
    side["pre2UnityNote"] = P2.NOTE
    side["unity"] = ub
    json.dump(side, open(jp, "w"), ensure_ascii=False, indent=1, default=list)
    return side, missing


def main():
    root = os.path.expanduser(sys.argv[1])
    check = "--check" in sys.argv
    tags = sorted(d for d in os.listdir(root) if not d.startswith("_") and os.path.exists(os.path.join(root, d, d + ".json")))
    if check:
        same = diff = skip = 0
        bad = []
        for t in tags:
            side = json.load(open(os.path.join(root, t, t + ".json")))
            if "unity" not in side or not find_mdx(root, side):
                skip += 1
                continue
            _, ub = process(root, t, write=False)
            old = dict(side["unity"])
            old.pop("textureBytes", None)
            ub.pop("textureBytes", None)                          # 폴더 크기 — 옛 반입 뒤 대본 도구가 입자 텍스처를 더 복사해 커졌다
            if json.dumps(old, sort_keys=True, ensure_ascii=False) == json.dumps(ub, sort_keys=True, ensure_ascii=False):
                same += 1
            else:
                diff += 1
                ks = [k for k in set(old) | set(ub) if json.dumps(old.get(k), sort_keys=True) != json.dumps(ub.get(k), sort_keys=True)]
                bad.append((t, ks))
        print("같음", same, "다름", diff, "건너뜀", skip)
        for t, ks in bad[:15]:
            print("  다름", t, ks)
        return
    tot_missing = []
    for t in tags:
        side, missing = process(root, t)
        tot_missing += missing
        print(t, "메시층", len(side["unity"]["layers"]), "pre2Unity", len(side["pre2Unity"]), "없는 입자텍스처", len(missing))
    print("입자 텍스처 없음 합", len(tot_missing), tot_missing[:10])


if __name__ == "__main__":
    main()
