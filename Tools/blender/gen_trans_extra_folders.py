"""초월 부가 이펙트 로스터별 폴더 생성(2026-10-01) — 시스템 python으로 실행(Blender 아님; FBX는 blender를 하위 호출).

  SCR=<analysis.json·_tex가 있는 작업폴더> python3 Tools/blender/gen_trans_extra_folders.py
산출: ~/GRD_motion_trial/초월_부가이펙트/<로스터>/{설명.md, *.fbx, Textures/}
"""
import json, os, re, shutil, subprocess, sys
HOME = os.path.expanduser("~/GRD_motion_trial/초월_부가이펙트")
SC = os.environ["SCR"]
A = json.load(open(os.path.join(SC, "analysis.json")))
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
by_file = {v["file"]: v for v in A.values()}

# 사람이 렌더를 보고 내린 판정: 모델 → (한 줄 판정, FBX로 뜯을 지오셋, 붙이는 때)
VERDICT = {
    "lb_jimbe.mdx": ("흰 별빛 플레어 1장(85정점, 가산) — 가슴 부착", [0], "항상(가슴 앞, 작게 깜박)"),
    "AkainuBW7.mdx": ("마그마 양팔(용암 텍스처 팔뚝 둘, 알파컷) — 양손에 씌우는 부품", [2, 5], "공격|스킬 (팔에 씌움)"),
    "Mr.War3_Flanqi4.mdx": ("빛나는 푸른 타원 셋(baozha, 가산) — 몸 둘레 폭발 잔광", [1, 2, 10], "공격|스킬 (번쩍)"),
    "nikaluffy011.mdx": ("검은 거대 주먹 1개(g14, 2549정점) + 파티클 4(번개·불 플립북) — 공격 상태용", [14], "공격 순간"),
    "Snakeman2a9.mdx": ("스네이크맨 변신 부품 11조각(붉은 거대 주먹·다리·몸통) — 상시 아님, 공격 상태 때만 몸을 바꿔 끼우는 것", [1, 3, 5, 9, 10, 11, 13, 15, 16, 17, 19], "공격 상태(변신) — 상시용 아님"),
    "mr.war3_dflmg5.mdx": ("FBX 없음 — 리본 32개(실 궤적: 흰색·폭 2~3·수명 0.4~0.5초·초당 100마디·알파 0.5~0.6). 도플라밍고 실 느낌", [], "이동·공격 때 궤적"),
    "tashigi17.mdx": ("FBX 없음 — 리본 1개(타시기 검 궤적 TashigiRibbon, 폭 88·수명 0.06초·초당 52마디)", [], "공격 때 검 궤적"),
    "luffy010_pf6.mdx": ("FBX 없음 — 파티클 1개(붉은→흰 큰 불꽃 플립북 4×4, 크기 300→70) — 기어 포스 폭발", [], "공격 순간"),
    "bigmom7.mdx": ("FBX 없음 — 파티클 6(과자 구름·알갱이 mm01~07, 몸이 1715라 거대) — 빅맘 몸이 17m라 비율로만 참고", [], "상시(비율로 줄여서)"),
}
NONE = "부가 이펙트 없음(몸·옷·소품뿐 — 무기·옷 지오셋은 버림)"

def run_fbx(model, idx, out):
    env = dict(os.environ, ANALYSIS=os.path.join(SC, "analysis.json"))
    subprocess.run(["blender", "-b", "--factory-startup", "--python", os.path.join(REPO, "Tools/blender/export_mdx_parts.py"), "--",
                    SC, out, model, ",".join(map(str, idx))], env=env, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

roster_models = {}
for name, v in A.items():
    for r in v["rosters"]:
        roster_models.setdefault(r, []).append(v)
done_fbx = {}
for r, models in sorted(roster_models.items()):
    if r == "초월_구주호_AD":
        continue                                            # 손으로 만든 파일럿 폴더
    out = os.path.join(HOME, r)
    os.makedirs(os.path.join(out, "Textures"), exist_ok=True)
    md = [f"# {r} — 원작 부가 이펙트\n", "단위: 워크3 1 = 0.01m, 앞 = 워크3 +X(FBX는 유니티 규약으로 돌려 둠). **FBX 오브젝트 원점 = 붙는 뼈의 pivot**. 키는 몸 키 대비 **비율**로 환산해 쓸 것.", "재질 끝 `_add`=가산(검정=투명) `_cut`=알파컷 `_blend`=알파혼합.\n"]
    for v in models:
        f = v["file"]
        verdict = VERDICT.get(f)
        md.append(f"## {v['model']}  (원작 유닛 {', '.join(v['uids'])} · 몸 키 {v['body_height']})")
        if not verdict:
            md.append(f"- {NONE}\n")
            continue
        text, idx, when = verdict
        md.append(f"- 판정: {text}\n- 붙이는 때(어림): {when}")
        if idx:
            key = (f, tuple(idx))
            if key not in done_fbx:
                tmp = os.path.join(SC, "_fbxout", os.path.splitext(f)[0]); shutil.rmtree(tmp, ignore_errors=True); os.makedirs(tmp)
                run_fbx(f, idx, tmp); done_fbx[key] = tmp
            tmp = done_fbx[key]
            for fn in os.listdir(tmp):
                if fn.endswith(".fbx"): shutil.copy(os.path.join(tmp, fn), out); md.append(f"- **FBX**: `{fn}`")
            for fn in os.listdir(os.path.join(tmp, "Textures")): shutil.copy(os.path.join(tmp, "Textures", fn), os.path.join(out, "Textures"))
            for gi in idx:
                g = next(x for x in v["geosets"] if x["index"] == gi)
                md.append(f"  - g{gi}: 정점 {g['verts']} · 뼈 pivot(워크3) {g['bone_pivot']} · 몸 대응 {g['humanoid']} · 층 {g['layers']}")
        lines = [t for t in v["anim_text"] if t.startswith(("파티클", "리본"))]
        if lines:
            md.append("- 파티클·리본(FBX에 못 담음 — 값으로 자작):")
            for t in lines: md.append(f"  - {t}")
            used = sorted({int(m) for t in lines for m in re.findall(r"텍스처 #(\d+)", t)})
            for ti in used:
                tp = v["textures"][ti]; fn = tp.replace("\\", "_").replace("/", "_") + ".png"
                src = os.path.join(SC, "_tex", fn)
                if os.path.exists(src): shutil.copy(src, os.path.join(out, "Textures", fn)); md.append(f"  - 텍스처 #{ti} = `Textures/{fn}`")
                else: md.append(f"  - 텍스처 #{ti} = {tp} (맵에 없는 워크3 기본 — 근사로 대체)")
            rb = [(x["name"], x["chain"]) for x in v["ribbons"]]
            if rb: md.append(f"  - 리본 붙는 뼈 예: {rb[:3]}")
        md.append("")
    open(os.path.join(out, "설명.md"), "w").write("\n".join(md))
    print(r, [m["file"] for m in models])
