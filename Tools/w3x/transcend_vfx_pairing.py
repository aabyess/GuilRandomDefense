"""초월 스킬 ↔ 원작 이펙트 모델 대응표 (2026-10-08, PM 첫 지시 ①).

  /usr/bin/python3 Tools/w3x/transcend_vfx_pairing.py [작업폴더=~/GRD_motion_trial/transcend_vfx]

입력: ~/GRD_cast_vfx_trial/survey.json(cast_vfx_survey.py 산출 — 우리 초월 스킬·원작 트리거 사슬·이펙트 출처) + 원작 war3map.j·w3a·w3u 직접 디코드.
짝짓기 근거 3종(강한 순):
  [이름]      우리 스킬 에셋·원작트리거 칸·설명의 `Trig_X`가 원작 트리거 이름과 같음(survey의 pair_skill)
  [능력코드]  우리 스킬 이름이 원작 능력 코드(A0GZ 같은)로 시작 → w3a 그 능력의 아트 필드(acat·atat·asat·aeat·amat·alig)의 .mdl/.mdx 값만
              (🔴 조합 능력 아트필드는 레시피일 수 있어 .md[lx] 끝나는 값만 센다 · 번들 능력은 uabi 소유 유닛으로 재확인)
  [문턱 일치] 발동 문턱(체력/마나 게이지·1÷확률)이 같은 우리 스킬 — 약한 근거(사장님 신규 사양이라 이름이 다름)
짝이 없는 우리 스킬은 「(짝 없음 — 신규 사양)」로 남긴다(어떤 원작 이펙트를 붙일지는 사장님/PM 결정).
근거 칸: war3map.j 줄 번호(AddSpecialEffect/더미 유닛 코드가 처음 나오는 줄) · w3u/w3a 필드(umdl·ua1m·uabi·acat…).
산출: <작업폴더>/pairing.csv · pairing.md · ability_models.txt(능력코드로 새로 알게 된 모델 이름들 — 변환 대상에 추가)
"""
import csv
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import w3a                                                           # noqa: E402
import w3u                                                           # noqa: E402
from mpqread import Archive                                          # noqa: E402

GRADE = os.environ.get("GRADE", "초월")                                    # 초월·불멸·영원
TAG = {"초월": "transcend", "불멸": "immortal", "영원": "eternal"}[GRADE]
W = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else f"~/GRD_motion_trial/{TAG}_vfx")      # 불멸·영원은 W/fbx → ~/GRD_orig_vfx_trial 심볼릭 링크(평평한 모델 폴더)
SURVEY = json.load(open(os.path.expanduser("~/GRD_cast_vfx_trial/survey.json" if GRADE == "초월" else f"~/GRD_cast_vfx_{GRADE}/survey.json")))
JL = open(os.path.join(HERE, "원본/풀린것/war3map.j"), encoding="utf8", errors="replace").read().split("\n")
ABIL = {a["id"]: {m["field"]: m["value"] for m in a["mods"] if m["level"] <= 1 or m["field"] not in ()} for a in w3a.parse(os.path.join(HERE, "원본/풀린것/war3map.w3a"))}
ARC = Archive(os.path.expanduser("~/GRD_motion_trial/_work/ord.mpq"))
ART = [("acat", "시전자"), ("atat", "대상"), ("asat", "특수"), ("aeat", "효과"), ("amat", "투사체"), ("alig", "번개")]
MODEL = re.compile(r"\.(mdl|mdx)$", re.I)
JUNK = re.compile(r"dummy|spawnmodels|^\.mdx?$", re.I)


def jline(needle):
    """war3map.j에서 needle이 처음 나오는 줄 번호(1부터). 없으면 ''."""
    n = needle.replace("\\\\", "\\").lower()
    for i, ln in enumerate(JL, 1):
        if needle.lower() in ln.lower() or n in ln.replace("\\\\", "\\").lower():
            return i
    return ""


def mx(name):
    n = name.replace("\\\\", "\\")
    return re.sub(r"\.mdl$", ".mdx", n, flags=re.I)


def in_map(name):
    n = mx(name)
    for c in (n, n.split("\\")[-1]):
        try:
            if ARC.read(c):
                return True
        except Exception:                                            # noqa: BLE001
            pass
    return False


def conv_status(name):
    """변환 산출 상태: (상태, FBX 경로, 비고)."""
    tag = os.path.splitext(mx(name).split("\\")[-1])[0]
    j = os.path.join(W, "fbx", tag, tag + ".json")
    f = os.path.join(W, "fbx", tag, tag + ".fbx")
    if not os.path.exists(f):
        return "미변환", "", ""
    d = json.load(open(j))
    parts = []
    if d["pre2"]:
        parts.append(f"파티클 {len(d['pre2'])}줄기(JSON만)")
    if d["ribbons"]:
        parts.append(f"리본 {d['ribbons']}(미변환)")
    if not d["meshes"]:
        return "입자 전용(메시 없음)", f, "; ".join(parts)
    return "변환됨", f, "; ".join(parts)


def pv(tag):
    p = os.path.join(W, "preview", tag + "_orig_s0.png")
    return os.path.join(W, "preview_sheets", tag + ".png") if os.path.exists(os.path.join(W, "preview_sheets", tag + ".png")) else (p if os.path.exists(p) else "")


rows, ability_models = [], []
for u in SURVEY:
    skills = u["ourSkills"]
    matched = {}                                                     # 스킬 asset → [(근거, 트리거, 이펙트 e)]
    for f in u["families"]:
        for t in f["triggers"]:
            for o in t["ours"]:
                asset, _, how = o.partition(" ｜ ")
                how = how[how.rfind("["):] if "[" in how else ""
                for e in t["effects"]:
                    matched.setdefault(asset, []).append((how.strip("[]") or "이름", f"트리거 {t['trigger']}(깊이 {t['depth']}) · 공격 {f['attack']}({f['code']})", e))
    for s in skills:
        m = re.match(r"([A-Z][0-9A-Z]{3})\b", s["skillName"]) or re.search(r"_([A-Z][0-9A-Z]{3})$", s["asset"])
        code = m.group(1) if m and m.group(1) in ABIL else ""
        got = list(matched.get(s["asset"], []))
        if code:
            a = ABIL[code]
            for fld, role in ART:
                v = a.get(fld)
                if isinstance(v, str) and v:
                    for p in re.split(r"[,;]", v):
                        p = p.strip()
                        if MODEL.search(p) and not JUNK.search(p):
                            stock = bool(re.match(r"(abilities|units|objects|buildings|doodads|environment|ui|spells)\\", p.replace("\\\\", "\\"), re.I))
                            ok = in_map(p)
                            got.append(("능력코드", f"능력 {code} {a.get('anam', '')}", dict(model=p, src=f"w3a {code}.{fld}({role})", inmap=ok, stock=stock and not ok)))
                            if ok:
                                ability_models.append(mx(p).split("\\")[-1])
        if not got:
            rows.append([u["roster"], s["asset"], s["skillName"], "(짝 없음 — 신규 사양)", "", "", "", "", "", "", "", "", ""])
            continue
        for how, trig, e in got:
            model = e["model"]
            tag = os.path.splitext(mx(model).split("\\")[-1])[0]
            src = e.get("src", "")
            ev = []
            mm = re.search(r"더미 (\w{4})", src)
            if mm:
                ev.append(f"w3u {mm.group(1)}.umdl/ua1m/uabi · war3map.j {jline(chr(39) + mm.group(1) + chr(39))}행")
            elif "AddSpecialEffect" in src:
                ev.append(f"war3map.j {jline(os.path.basename(mx(model).replace(chr(92), '/')).replace('.mdx', ''))}행 {src}")
            else:
                ev.append(src)
            if how == "능력코드":
                ev = [src]
            inmap = e.get("inmap")
            if not inmap:
                st, fbx, note = ("변환불가", "", "워크3 기본 모델(맵에 없음) — 우리 쪽 Kenney/자체 메시로 근사" if e.get("stock") else "맵에 없음(보호맵 이름 변경 추정)")
            else:
                st, fbx, note = conv_status(model)
            rows.append([u["roster"], s["asset"], s["skillName"], how, trig, model, "; ".join(ev), "맵 안" if inmap else "맵 밖", st, fbx, pv(tag), note,
                         e.get("src", "")])

HEAD = ["초월 유닛(로스터)", "우리 스킬 에셋", "우리 스킬 이름", "짝 근거", "원작 트리거/능력", "원작 이펙트 모델", "근거(줄·필드)", "맵", "변환 상태", "FBX", "미리보기", "비고", "조사 원문(src)"]
with open(os.path.join(W, "pairing.csv"), "w", newline="", encoding="utf-8-sig") as f:
    wr = csv.writer(f)
    wr.writerow(HEAD)
    wr.writerows(rows)
open(os.path.join(W, "ability_models.txt"), "w").write(" ".join(sorted(set(ability_models))))
paired = [r for r in rows if r[3] != "(짝 없음 — 신규 사양)"]
print("rows", len(rows), "paired", len(paired), "skills", len({(r[0], r[1]) for r in rows}), "paired skills", len({(r[0], r[1]) for r in paired}),
      "models", len({r[5] for r in paired}), "ability models", len(set(ability_models)))
