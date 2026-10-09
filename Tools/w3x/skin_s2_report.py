"""S2 신작 스킨 README·실패_목록.md·구버전 「맵에 없음」 42기 대조. /usr/bin/python3 Tools/w3x/skin_s2_report.py"""
import csv, glob, os, re
H = os.path.expanduser("~"); R = H + "/Desktop/구랜디스킨모음/원랜디_신작_스킨"; OLD = H + "/Desktop/구랜디스킨모음/원랜디_구버전_스킨/실패_목록.md"
BODY = {"h04K": "H09A(!0723_Jinbe)", "h04L": "H0B5(!0715_Borsalino)", "h04N": "H092(!0718_Sabo)", "h04W": "H09B·H09C(!0704_Usopp)", "h04X": "H09B·H09C(!0704_Usopp)", "h04Z": "H091·H093(!0706_Chopper)",
        "h050": "H08U(!0712_Shirahoshi)", "h053": "H08Y·H0BV(!0708_Franky)", "h05T": "h01S(!0217_Beckman, 희귀함)", "h07E": "H0CO·H0CP(!0729_Koby)·h03E(!0534_Koby)", "h07F": "h039·h04B(!0526/!0907_Shiki)",
        "h066": "S2에 몸 없음", "h07Q": "S2에 몸 없음(우루지 모델 못 찾음)", "h08K": "S2에 몸 없음(이타치 모델 못 찾음; Mission_SeaKings만)"}
rows = []
for f in sorted(glob.glob(R + "/*/index.csv")):
    g = os.path.basename(os.path.dirname(f))
    for r in csv.DictReader(open(f, encoding="utf-8-sig")): r["grade"] = g; rows.append(r)
byid = {r["ID"]: r for r in rows}
ok = [r for r in rows if r["결과"] == "성공"]; fail = [r for r in rows if r["결과"] != "성공"]
out = ["# 못 뽑은 S2 스킨 목록\n"]
for g in sorted({r["grade"] for r in fail}):
    fs = [r for r in fail if r["grade"] == g]; out.append(f"\n## {g} ({len(fs)})")
    for r in fs: out.append(f"- {r['이름(구버전 같은 ID, 추정)']} ({r['ID']}) · S2 모델 {r['S2 모델']} · 우리 {r['우리 대응 로스터'] or '-'} · {r['결과'][4:]}" + (f" → 같은 캐릭터 몸은 {BODY[r['ID']]}" if r['ID'] in BODY else ""))
open(R + "/실패_목록.md", "w", encoding="utf8").write("\n".join(out) + "\n")
old = open(OLD, encoding="utf8").read(); oldids = [m for m in re.findall(r"\(([hH][0-9A-Za-z]{3})\)[^\n]*(?:맵에 없음|Model\\)", old)]
chk = ["| ID | 이름 | 구버전 모델 | S2 결과 | S2 모델 |", "|---|---|---|---|---|"]; n_ok = 0
for k in oldids:
    r = byid.get(k)
    if not r: chk.append(f"| {k} | ? | | S2 표에 없음 | |"); continue
    good = r["결과"] == "성공"; n_ok += good
    chk.append(f"| {k} | {r['이름(구버전 같은 ID, 추정)']} | {r['구버전 모델']} | {'성공' if good else r['결과']} | {r['S2 모델']} |")
open(R + "/README.md", "w", encoding="utf8").write(f"""# 원랜디 최신(S2 2.323) 유닛 스킨 — 등급별 (Assets 밖)
유닛 {len(rows)}기 중 성공 {len(ok)} · 실패 {len(fail)} · 구버전 대비 NEW(성공 중) {sum(1 for r in ok if r['NEW'])}.
폴더: <NN_등급>/<ID_이름>/(FBX·Textures·json·preview.png) + 등급마다 <NN_등급>_격자.png(칸마다 글자 A…, 구버전과 다른 새 스킨은 NEW)·_격자.blend·index.csv·_cells/·_items.json. 못 뽑은 것은 실패_목록.md.
- S2는 슬크 저장본이라 이름·번역이 지워졌다 → 이름은 구버전 같은 ID 이름(「추정」), 신규 ID는 `(신규) 모델이름`. 등급은 구버전 ID 등급, 신규 ID는 같은 캐릭터 토큰을 쓰는 구버전 ID 등급(index 「등급 추정」 칸), 불명은 14_신규_등급미상.
- NEW 판정 = 구버전 모델과 S2 모델의 (지오셋 수·정점 합·삼각형 합)이 다르거나(새 모델) 구버전엔 맵에 없던 모델이거나 신규 ID. 같으면 「동일(이름만 바뀜)」. 서명 기준이라 같은 모델의 텍스처만 바뀐 경우는 못 가린다.
- 실패 사유: 워크3 기본 모델(맵에 없음) · 유닛 데이터에 모델 지정 없음 · 입자 전용(메시 없음) · 이펙트·장식 모델(Effect…·!effect·Mission·BlSkill·NPC_)이 유닛 모델로 지정됨(S2에서 몸이 다른 방식으로 붙는 유닛일 수 있음).
- 🔍 「이펙트 모델이 유닛 모델로 지정」 22기 추적(S2 war3map.j는 ID가 `$68303554`꼴 16진수): 이 ID들은 전부 스킬 시전 때 `CreateUnitAtLoc(…,$ID,…)`로 소환돼 `BST(슬롯, 10, 유닛)`에 저장되는 **스킬 더미**다(몸 모델을 붙이는 `AddSpecialEffect*` 호출은 맵 전체 33건뿐이고 몸 모델이 아님). 같은 캐릭터의 진짜 몸은 S2에서 **다른 ID(대문자 H09x·H08x 새 번호)** 로 옮겨져 이미 성공 목록에 있다(실패_목록.md 각 줄 「같은 캐릭터 몸은 …」). 우루지(h07Q)·이타치(h08K)는 S2에 몸 모델 자체를 못 찾았다. 나머지는 도움소·도전과제·강화소 같은 UI 더미.\n- 14번(등급 미상 65기)은 해체: 도박·강화소·도전과제·더미 이름 → 15_더미_상점_퀘스트, `!NNNN_` 모델 번호대로 등급 재분류(예 !07xx=초월·!09xx=불멸·!10xx=특수함·!12~13xx=랜덤/다른세계). index 「등급 근거」·「모델 번호대 등급(참고)」 칸 참고(번호대가 구버전 ID 이름 등급과 다른 유닛이 73기 — 같은 모델을 윗등급 변형이 재사용하는 정상 경우도 많아 14번이 아니면 안 바꿨다).\n- 검수는 구버전과 같은 기준(알파0 BLP 수정·Stand 알파0 지오셋 숨김·텍스처 누락·안쪽 면 비율 참고). 🔴 원작 저작물 — Assets 반입은 사장님 판단.
- 재현: Tools/w3x/s2_units.py → mdx_extract.py(s2.mpq) → Tools/blender/export_mdx_anim_fbx.py → Tools/w3x/skin_grade_build_s2.py(+render_skin_grid_s2.py·build_skin_grid_blend_s2.py·skin_grid_sheet_s2.py) → skin_s2_report.py.

## 구버전 「맵에 없음」이던 {len(oldids)}기가 S2에 있나 → 성공 {n_ok}
""" + "\n".join(chk) + "\n")
print(len(rows), len(ok), len(fail), len(oldids), n_ok)
