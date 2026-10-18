"""사장님 짝 한 건 처리(2026-10-18, blender): 교체목록.csv에 신작N 줄 → 팩 → 재수출·검증 → 전후 카드·목록 시트.
  /usr/bin/python3 Tools/w3x/swap_add_pair.py <구랜디 등급> <구랜디 이름> <S2 시트 폴더 번호_등급 예 03_특별함> <S2 시트 글자> <카드 폴더 이름 예 특별함>
예) swap_add_pair.py 특별함 왕승환 03_특별함 AC 특별함  → 팩 왕승환_스모커, 카드 ~/Desktop/구랜디스킨모음/교체_특별함_전후비교/(같은 등급 신작 줄 전부로 목록 시트).
같은 로스터에 이미 줄이 있으면 그 줄을 고친다(옛 팩은 _replaced_로)."""
import csv, os, re, shutil, subprocess, sys
H = os.path.expanduser("~"); ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
grade, name, sheetdir, letter, cardname = sys.argv[1:6]
idx = {r["글자"]: r for r in csv.DictReader(open(f"{H}/Desktop/구랜디스킨모음/원랜디_신작_스킨/{sheetdir}/index.csv", encoding="utf-8-sig"))}
e = idx[letter]; title = re.sub(r"[()]", "", e["캐릭터 이름(모델 기준)"]).split()[0]
p = f"{H}/grd/GRD_skin_swap/교체목록.csv"; rows = list(csv.reader(open(p, encoding="utf-8-sig"))); ro = f"{grade}_{name}"
cur = next((r for r in rows[1:] if r[1] == ro and r[0].startswith("신작")), None)
if cur:
    old = f"{name}_{cur[5].split()[0]}"
    if os.path.isdir(f"{H}/grd/GRD_skin_swap/{old}") and old != f"{name}_{title}":
        shutil.move(f"{H}/grd/GRD_skin_swap/{old}", f"{H}/grd/GRD_skin_swap/_replaced_{old}"); shutil.rmtree(f"{H}/grd/GRD_skin_swap/_model_fixed_prev/{old}", ignore_errors=True)
    cur[3:8] = [f"신작 {sheetdir[3:]}{letter}", e["ID"], title, e["S2 모델"], "가"]; num = cur[0]
else:
    num = f"신작{max(int(r[0][2:]) for r in rows[1:] if r[0].startswith('신작')) + 1}"
    rows.append([num, ro, "(현재 스킨)", f"신작 {sheetdir[3:]}{letter}", e["ID"], title, e["S2 모델"], "가"])
csv.writer(open(p, "w", newline="", encoding="utf-8-sig")).writerows(rows)
pk = f"{name}_{title}"; py = "/usr/bin/python3"
print(subprocess.run([py, ROOT + "/Tools/w3x/skin_swap_pack.py", pk], capture_output=True, text=True).stdout.strip().splitlines()[-1])
print(subprocess.run([py, ROOT + "/Tools/w3x/swap_refix_all.py", pk], capture_output=True, text=True).stdout.strip().splitlines()[0])
rows = list(csv.reader(open(p, encoding="utf-8-sig")))
nums = [r[0] for r in rows[1:] if r[0].startswith("신작") and r[1].startswith(grade + "_")]
for f in os.popen(f"ls {H}/grd/GRD_swap_before_1018/render/{ro}_* 2>/dev/null").read().split(): os.remove(f)
env = dict(os.environ, SWAP_NUMS=",".join(nums), SWAP_OLD_REF="HEAD", SWAP_CARD_OUT=f"{H}/Desktop/구랜디스킨모음/교체_{cardname}_전후비교", SWAP_BEFORE_TMP=f"{H}/grd/GRD_swap_before_1018")
print(subprocess.run([py, ROOT + "/Tools/w3x/swap_before_after_run.py"], env=env, capture_output=True, text=True).stdout.strip().splitlines()[-1])
print(f"카드 {env['SWAP_CARD_OUT']}/{ro}.png")
