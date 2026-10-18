"""원작 MDX PRE2(입자) → 유니티 CinematicImporter/Pre2Builder가 읽는 입자 목록 — 한 곳에서만 만든다(2026-10-18).

scene_scripts.model_block(연출 대본 json models[이름].pre2)과 model_pull/vfx_unity_block(모델 json "pre2Unity")이
같은 함수를 쓴다. 두 군데서 따로 유지하면 필드 이름·단위가 어긋난다(10-18 대조: lifespan↔lifespanSec 등).
"""
import os
import shutil

FILTER = {0: "blend", 1: "additive", 2: "modulate", 3: "modulate2x", 4: "alphakey"}
HEAD = {0: "head", 1: "tail", 2: "both"}
NOTE = ("keysMsAbs = MDX 전체 시간 ms(시퀀스 start 기준으로 빼서 쓸 것). KP2E=방출률 KP2V=가시(0/1) KP2S=속도 KP2G=중력 KP2L=위도 KP2W/N=폭/길이 …. "
        "모델 공간/월드 공간 구분 비트는 nodeFlags에 있다(원작은 거의 월드 공간 = 부모를 따라가지 않음)")


def stand_seq(info):
    """spawn에 anim이 없을 때 도는 시퀀스: 이름에 stand, 없으면 첫 시퀀스."""
    for sq in info["sequences"]:
        if "stand" in sq["name"].lower():
            return sq
    return info["sequences"][0] if info["sequences"] else dict(name="(없음)", start=0, end=1000)


def pre2_entries(info, geo, mdir, workd, seq_start, copied=None, missing=None, label=""):
    """info = mdx_anim.describe(raw) · geo = mdx_geo.parse(raw) · mdir = 모델 폴더(<mdir>/Textures/에 입자 PNG를 복사)
    · workd = mdx_extract 작업 폴더(_tex/ 원본 PNG) · seq_start = 기준 시퀀스 start(ms)."""
    pre = []
    for p in info["pre2"]:
        n = p["node"]
        piv = info["pivots"][n["id"]] if n["id"] < len(info["pivots"]) else [0, 0, 0]
        tp = geo["textures"][p["tex"]]["path"] if p["tex"] < len(geo["textures"]) else ""
        png = tp.replace("\\", "_").replace("/", "_") + ".png" if tp else ""
        if png and not os.path.exists(f"{mdir}/Textures/{png}") and os.path.exists(f"{workd}/_tex/{png}"):
            os.makedirs(f"{mdir}/Textures", exist_ok=True)
            shutil.copy(f"{workd}/_tex/{png}", f"{mdir}/Textures/{png}")
            if copied is not None:
                copied.append(f"{label}/{png}")
        if png and not os.path.exists(f"{mdir}/Textures/{png}") and missing is not None:
            missing.append(f"{label}/{png}")
        d = dict(textureFile=png or None, textureSourcePath=tp, sequenceStartMs=seq_start, node=n["name"], nodeFlags=n.get("flags"), parentId=n.get("parent"), pivot=list(piv),
                 speed=p["speed"], variation=p["variation"], latitude=p["latitude"], gravity=p["gravity"], lifespanSec=p["lifespan"],
                 emissionRate=p["rate"], width=p["width"], length=p["length"], filterMode=FILTER.get(p["filter"], p["filter"]), rows=p["rows"], cols=p["cols"],
                 headOrTail=HEAD.get(p["head"], p["head"]), tailLength=p["tail_len"], midTime=p["mid"], colorsRGB01=p["colors"], alpha255=p["alpha"], scale3=p["scale"],
                 squirt=p["squirt"], textureIndex=p["tex"],
                 tracks={k: dict(interp=v.get("interp"), keysMsAbs=[[x[0], list(x[1])] for x in v["keys"]]) for k, v in p["tracks"].items()})
        pre.append(d)
    return pre
