#!/usr/bin/env python3
"""Blender 세션의 effects.json → 유니티 빌더가 읽는 납작한 JSON + 재료 복사.

쓰는 법:  python3 Tools/sphere_art/flatten_effects.py <폴더(~/GRD_motion_trial/초월_부가이펙트/<로스터>)> <아스키 별칭> [로스터 이름]
  · 읽음:   <폴더>/effects.json (스키마는 ../_effects_schema.md)
  · 씀:     Tools/sphere_art/effects/<로스터>.json  — Unity JsonUtility가 읽을 수 있는 고정 꼴(Newtonsoft 없음)
  · 복사:   FBX·텍스처 → Assets/Resources/Effects/Sphere/Src/<로스터>/ (Textures/ 하위 구조 유지)
이어서 유니티에서 `call SphereArtBuilder.BuildAll` → 프리팹·재질 + Tools/sphere_art/extra.tsv → `python3 Tools/sphere_art/build_table.py`.

변환 규칙
  · 좌표·크기는 미터(원작 모델 몸 키 기준). 유니티에서 (게임 키 30 ÷ 그 모델 몸 키 m)를 곱해 게임 단위로 만든다 — 모델마다 몸 키가 달라 부품마다 bodyHeightM을 싣는다.
  · 방출량 0 + 슬롯이 항상이 아닌 방출기는 KP2E 키의 최댓값을 방출량으로 쓴다(원작이 그 동작 때 켜는 값).
  · 보이는 때 = ours_states(대기·이동·공격·스킬 중 해당하는 것, | 로 이음) — 전부면 항상. 붙는 뼈 = humanoid_hint(? 는 body = 유닛 루트).
  · 슬롯 「없음」(원작에서도 안 켜짐)·팀색 판(skip)은 뺀다.
"""
import json, os, shutil, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(HERE, "effects")
SRC_DIR = os.path.join(ROOT, "Assets", "Resources", "Effects", "Sphere", "Src")


HINTS = {"RightHand": "hand,right", "LeftHand": "hand,left", "Spine1(가슴)": "chest", "Hips": "hips", "Head": "head"}
# 모델 통째 붙는 곳 지정(원작 힌트가 ?일 때 PM 지시): 마르코 날개 = 가슴
ATTACH_OVERRIDE = {"mrk7.mdx": "chest", "tashigi17.mdx": "hand,right"}   # 타시기 검 궤적 → 오른손(검을 쥔 손)
HELD_PARTS = {"lb_kz_g8"}   # 손에 쥐는 메시(쿠잔 얼음 칼날) — 손 뼈에 쥐는 점을 맞춘다
# 자리·기울기 덮어쓰기(메시): euler 도, offset = 몸 키 비율(유니티 x·y·z, +z = 앞). 원작 대응이 없어 PM이 배정한 판만.
POSE = {"BlSkill04A_g0": {"euler": [90.0, 0.0, 0.0], "offset": [0.0, 0.55, 0.45]}}   # 땅에 눕는 초승달 판 → 가슴 높이 앞 허공에 세워 휘두름(10-01 PM)
LIMB_PARTS = {"AkainuBW7_g2", "AkainuBW7_g5"}   # 마그마 소매(김만경_AD)
AURA = bool(os.environ.get("AURA"))   # 상시 오라 모델(몸 없음) — 맵 단위 그대로(1m = 100 wc3 = 24 게임 단위), 통째 한 프리팹 <별칭>.prefab
FORCE_STATES = [x for x in os.environ.get("FORCE_STATES", "").split("|") if x]   # 예: FORCE_STATES=스킬
ORDER = ["대기", "이동", "공격", "스킬"]


def opaque_alpha(path):
    try:
        import sys as _s
        _s.path.insert(0, os.path.expanduser("~/Library/Python/3.9/lib/python/site-packages"))   # 시스템 파이썬(/usr/bin/python3)의 PIL 위치
        from PIL import Image
        lo, _ = Image.open(path).convert("RGBA").getchannel("A").getextrema()
        return lo >= 250
    except Exception:
        return False


def vec(v, n=3, d=0.0):
    v = list(v or [])
    return (v + [d] * n)[:n]


def main():
    folder = os.path.abspath(os.path.expanduser(sys.argv[1]))
    alias = sys.argv[2]                       # Resources 키에 쓸 아스키 이름(한글 NFC/NFD 함정 피함) — 예: juho_ad
    roster = sys.argv[3] if len(sys.argv) > 3 else os.path.basename(folder).replace("초월_", "초월_")
    data = json.load(open(os.path.join(folder, "effects.json"), encoding="utf-8"))
    heights = {m["model"]: m["body_height_m"] for m in data["models"]}
    parts, files = [], set()
    for p in data["parts"]:
        slot = p["visible"]["ours_slot"]
        if slot == "없음" or p["visible"].get("never_active"):
            continue
        h = 30.0 / 24.0 if AURA else heights.get(p["model"], 1.8)   # 오라: 키 30 ÷ 1.25 = 24 단위/m (WorldScale 4.167 ← 100 wc3/m)
        states = [x for x in ORDER if x in p["visible"]["ours_states"]]
        if AURA:
            states = list(ORDER)   # 상시 오라는 늘 켜져 있다
        if FORCE_STATES:
            states = FORCE_STATES   # 단독 이펙트 모델(초승달 베기 등)은 원작 시퀀스가 비어 있다 — 쓰는 쪽이 정한 때로
        if not states and p["kind"] != "ribbon":
            continue
        out = {"kind": p["kind"], "name": p["name"], "slot": slot, "model": p["model"], "bodyHeightM": h,
               "states": "|".join(states), "attach": HINTS.get(p["attach"]["humanoid_hint"], "body")}
        if p["model"] in ATTACH_OVERRIDE:
            out["attach"] = ATTACH_OVERRIDE[p["model"]]
        if p["kind"] == "mesh":
            # 메시 pivot은 원작 팔 뼈의 자리(팔을 벌린 자세 기준, 몸에서 ~0.9m) — 우리 스킨 손에 붙이면 팔 흔들림에 크게 휘둘린다(10-01 구주호 날개) → 가슴 뼈에.
            if out["attach"] in ("hand,left", "hand,right"):
                # 예외 표(PM 10-01): 팔에 씌우는 소매는 우리 팔에 맞춰 위팔 뼈에(SphereArtLimb), 그 밖(날개 등 몸에서 뻗은 것)은 가슴 뼈에.
                out["attach"] = ("limb," + out["attach"].split(",")[1]) if p["name"] in LIMB_PARTS else (out["attach"] if p["name"] in HELD_PARTS else "chest")
            live = [l for l in p["layers"] if not l.get("skip")]
            layer = next((l for l in live if l["additive"]), live[0] if live else None)   # 층이 여럿이면(혼합+가산 같은 그림) 가산 층 하나
            if layer is None:
                continue
            if max(p.get("size_m") or [0]) < 0.001:
                continue   # 정점 1개짜리 닻 메시(리본이 매달리는 자리) — 그릴 게 없다(a_blueeff·BlackFlame·rib_sinobu)
            tex = layer["texture"]["file"]
            if p["name"] in POSE:
                out["euler"], out["offset"] = POSE[p["name"]]["euler"], POSE[p["name"]]["offset"]
            spins = [r for r in p["motion"]["bone_rotations"] if abs(r["deg_per_s"]) > 20]
            if AURA and spins:
                r = max(spins, key=lambda r: abs(r["deg_per_s"]))
                out["spinAxis"], out["spinDeg"] = r["axis_unity"], r["deg_per_s"]
            if AURA:
                out["unlit"] = True   # 오라 메시는 빛나는 효과 — 조명 받는 재질이면 뒤쪽이 검게 죽는다(10-01 Ora_siki 바위)
            if AURA and layer["texture"].get("approx") and not layer["additive"]:
                out["flat"] = [0.55, 0.45, 0.38]   # 근사 바위 텍스처는 UV 자리에 따라 검게 나온다 — 근사가 거짓 그림이 되느니 갈색 회색 단색(10-01 Ora_siki)
            out.update(pos=vec(p["attach"]["unity_pos_m"]), fbx=p["fbx"], fbxObject=layer["fbx_object"], texture=tex, additive=bool(layer["additive"]),
                       cutout=layer["fbx_object"].endswith("_cut"), blend=layer["fbx_object"].endswith("_blend"))
            if out["blend"] and opaque_alpha(os.path.join(folder, tex)):
                # 알파가 전부 255인 텍스처(검정 바탕)를 알파혼합으로 그리면 검은 판이 된다(10-01 박기찬 baozha) → 가산으로(검정 = 투명).
                out["blend"], out["additive"] = False, True
            files.update([p["fbx"], tex])
            # 같은 FBX의 다른 오브젝트(g5·g6)는 부품마다 따로 — 슬롯이 다르므로
        elif p["kind"] == "particle":
            u = p["unity"]
            rate = float(u["emission_rate_per_s"])
            if rate <= 0 and ("대기" not in states or AURA):
                keys = (p["raw"].get("tracks", {}).get("KP2E") or {}).get("keys", [])
                rate = max([k[1][0] for k in keys] or [0.0])
            if rate <= 0:
                continue
            fb = p.get("flipbook") or {}
            tex = p["texture"]["file"]
            files.add(tex)
            rgb = [c for tri in u["color_rgb"] for c in tri]
            blend_part = not bool(p.get("additive", True))
            if blend_part and opaque_alpha(os.path.join(folder, tex)):
                p = dict(p, additive=True)   # 알파가 불투명인 텍스처(검정 바탕)를 블렌드로 그리면 사각 판이 된다 → 가산
            out.update(pos=vec(p["attach"]["unity_pos_m"]), texture=tex, additive=bool(p.get("additive", True)),
                       rate=rate, life=u["lifetime_s"], speed=u["start_speed_mps"], speedVar=u["speed_variation_fraction"],
                       shape=u["shape"], cone=u.get("cone_angle_deg", 25.0), box=vec(u["box_size_m"]),
                       gravity=u["gravity_modifier"], size=vec(u["size_m"]), mid=u["mid_time_fraction"],
                       rgb=rgb, alpha=vec(u["alpha"]), rows=1 if p["texture"].get("approx") else fb.get("rows", 1), cols=1 if p["texture"].get("approx") else fb.get("cols", 1))   # 근사 텍스처(맵에 없는 워크3 기본 그림)는 낱장 — 시트로 자르면 사각 조각이 된다
        elif p["kind"] == "ribbon":
            u = p["unity"]
            if u["width_ratio_to_body"] < 0.1 and not AURA:
                continue   # 가는 실 리본(도플라밍고 32개, 몸 키의 5%)은 거의 안 보이고 TrailRenderer 32개는 값만 든다 — 뺀다(PM 보고)
            if (not states or FORCE_STATES == []) and not AURA:
                out["states"] = "|".join(["이동", "공격"])   # 리본 가시 트랙 정보 없음 — 궤적은 움직일 때만 그려진다
            tex = p["texture"]["file"]
            files.add(tex)
            out.update(pos=vec(p["attach"]["unity_pos_m"]), texture=tex, additive=bool(p.get("additive", True)),
                       trail=u["trail_time_s"], width=u["width_start_m"], widthEnd=u["width_end_m"],
                       alpha=[u["alpha"], u["alpha"], u["alpha"]], rgb=list(u["color_rgb"]) * 3)
        else:
            continue
        parts.append(out)

    if AURA:   # 리본·파티클은 모델의 가장 빠른 메시 회전을 따라 돈다(원작: 같은 Dummy 뼈에 붙은 궤적 — Ora_siki 리본이 원을 그린다)
        top = max([q for q in parts if q.get("spinDeg")], key=lambda q: abs(q["spinDeg"]), default=None)
        for q in parts:
            if q["kind"] != "ribbon":
                continue
            if top is not None:
                q["spinAxis"], q["spinDeg"] = top["spinAxis"], top["spinDeg"]
            else:
                # 원작은 Dummy 뼈 애니(회전·이동)가 리본 끝을 휘저어 궤적을 그린다 — 뼈 애니를 안 옮기므로 근사: 몸 둘레를 빠르게 도는 궤적(10-01 가정)
                q["spinAxis"], q["spinDeg"] = "Y", 360.0
                if abs(q["pos"][0]) + abs(q["pos"][2]) < 0.05:
                    q["pos"][0] = 0.35
    os.makedirs(OUT_DIR, exist_ok=True)
    old = os.path.join(OUT_DIR, roster + ".json")
    if os.path.exists(old) and alias + ".json" != roster + ".json":
        os.remove(old)   # 옛 이름(로스터 이름)으로 쓰던 파일 — 지금은 별칭이 파일 이름(한 로스터가 여러 재료 폴더를 받을 수 있다)
    with open(os.path.join(OUT_DIR, alias + ".json"), "w", encoding="utf-8") as f:
        json.dump({"roster": roster, "alias": alias, "single": AURA, "parts": parts}, f, ensure_ascii=False, indent=1)
    for rel in ([] if os.environ.get("NOCOPY") else sorted(files)):
        src = os.path.join(folder, rel)
        dst = os.path.join(SRC_DIR, roster, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
    print(f"{roster}({alias}): 부품 {len(parts)}개 · 재료 {len(files)}개 → {os.path.join(OUT_DIR, alias + '.json')}")


if __name__ == "__main__":
    main()
