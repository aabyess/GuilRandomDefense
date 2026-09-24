"""저장된 설정이 **오늘 실제로 도는지** 항목마다 한 번씩 돌려 보는 관문(PM 요청 2026-09-24).

왜 필요한가 — 2026-09-24에 돌려 봤더니 **9종이 그 자리에서 죽었습니다.**
가장 지독한 것은 히든_석성례였습니다: 저장된 설정으로는 아예 안 도는데 커밋된 FBX는 멀쩡히 있었고,
그 FBX는 **골반 뼈가 바닥 1.17m 아래**인 채였습니다. 「설정이 그 파일을 만든 설정이 아니다」 —
값을 봤지만 그 값이 쓰였는지는 아무도 안 봤던 것입니다.
👉 **산출할 때마다 돌리십시오.** 안 돌리면 또 쌓입니다.

🔴 **목록을 손으로 쓰지 않습니다**(PM 지시 2026-09-24). 손으로 쓴 목록이 그날 두 번 거짓이었습니다:
```
「클립 전부 봤다」 → 셋 중 idle 하나였다
「항목 181개 전부 돌렸다」 → 213개 중 181개였다(생성기 넷이 목록 밖)
```
두 번 다 **도구가 거짓말한 게 아니라 사람이 도구에 덜 준 것**입니다. 그래서 이 관문은
① `gen_*.py`·`fix_unit_fbx.py`를 **파일 목록에서 훑어** 유닛 생성기를 찾고(본문에 `Art/Units`가 있으면 유닛 생성기),
② 유닛 생성기인데 **항목 표를 못 찾으면 그 이름을 찍고 실패**합니다(조용히 안 건너뜁니다),
③ 모은 이름과 **Assets의 유닛 FBX 이름**을 견주어 **양쪽에만 있는 이름을 찍습니다.**
👉 그래서 `gen_*.py`를 새로 만들어도 **아무 데도 등록할 필요가 없습니다.** 안 맞으면 관문이 먼저 압니다.

🔴 **「돌았다」가 아니라 「그 파일이 나왔다」로 판정합니다**(2026-09-24에 데었습니다).
   옛 판은 출력에서 예외 이름을 문자열로 찾았는데, `subprocess.CalledProcessError`가 그 목록에 없어서
   **초월_양재모_AD(gen_biped_skin)가 죽었는데 OK로 찍혔습니다.** 지금은 돌리기 전에 산출 FBX를 지우고
   **끝난 뒤 그 파일이 생겼는지** 봅니다. 예외 이름 목록은 사람이 관리하는 자리라 또 빠집니다.

읽는 법 — 실패는 세 종류이고 **뜻이 다릅니다. 안 가르고 「N종이 죽는다」로 보고하지 마십시오.**
  ③ 진짜 썩음    설정이 오늘 못 돈다(예: no_nulls=True인데 drop_bones에 Null 이름). **고칠 것.**
  ② 구조상 못 돎  source가 자기 산출물이라 두 번째 실행이 죽는다. `rebuild="원본에서만"` 표식으로 걸러진다.
  ① 검사가 늦음   assert가 그 항목보다 나중에 들어왔다. **커밋본을 열어 재 보면 산출물과 같은 상태다.**
                  (2026-09-24 실측: 특별함_박진웅·희귀함_양재모·희귀함_박기찬 등 열한 종이 여기였다)

🔴 `rebuild` 표식 — **기계가 읽는다.** 사람이 머리말을 읽어야만 아는 사실로 두지 않는다.
  rebuild="원본에서만"  source가 자기 산출물. 다시 뽑으려면 원본을 source로 바꿔 놓고 돌릴 것.
  rebuild="금지"        이 설정으로는 **커밋본이 안 나온다**. 다시 뽑으면 **다른 모델이 들어간다.**
                        (희귀함_박도진: 커밋본 정점 20,562 ≠ 이 설정 25,857)
                        ⚠️ **OK로 도는 항목도 금지일 수 있다** — 초월_양재모_AD의 gen_biped_skin 항목은
                        2026-09-22 스킨 맞바꿈 **전**의 가프를 가리킨다. 돌면 **다른 사람이 들어간다.**

쓰는 법:
  python3 Tools/blender/check_entries.py              # 전부(200종 남짓, 1~2시간)
  python3 Tools/blender/check_entries.py 유닛이름 ...   # 몇 개만
  python3 Tools/blender/check_entries.py --list       # 안 돌리고 목록·대조만 본다(몇 초)
"""

import ast
import glob
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(PROJECT, ".check_entries_out")
UNITS_DIR = os.path.join(PROJECT, "Assets", "Art", "Units")
DICT_NAMES = ("UNITS", "SKINS", "ANIMALS")            # 항목 표로 인정하는 이름

# 유닛을 만들지만 **키가 유닛 이름이 아니라** 이 관문에 못 넣는 것. 비워 두지 말고 이유를 적을 것 —
# 빈칸으로 두면 다음 사람이 「없다」로 읽는다.
KNOWN_ODD = {
    "gen_creatures.py": "적(물범·노루·양)을 만든다. `--out <폴더> <이름>` 꼴이 아니다. 따로 볼 것",
}


def unit_generators():
    """유닛을 만드는 스크립트 = 본문에 `Art/Units`가 있는 gen_*.py·fix_unit_fbx.py.
    ⚠️ **읽다가 터지면 조용히 넘기지 않고 그 이름을 돌려준다**(아래 bad_files)."""
    found, bad_files = [], []
    paths = sorted(glob.glob(os.path.join(HERE, "gen_*.py"))) + [os.path.join(HERE, "fix_unit_fbx.py")]
    for path in paths:
        base = os.path.basename(path)
        try:
            src = open(path, encoding="utf-8").read()
        except OSError as e:
            bad_files.append((base, "못 읽음: %s" % e))
            continue
        if "Art/Units" not in src:
            continue                                   # 건물·자연·구조물 생성기 — 유닛이 아니다
        try:
            tree = ast.parse(src)
        except SyntaxError as e:
            bad_files.append((base, "구문 오류: %s" % e))
            continue
        named = [n for n in tree.body if isinstance(n, ast.Assign)
                 and any(getattr(t, "id", None) in DICT_NAMES for t in n.targets)]
        dicts = [n for n in named if isinstance(n.value, ast.Dict)]
        if named and not dicts:
            # 🔴 이름은 맞는데 **글자 그대로의 dict가 아니다**(내포 표기·함수 반환 등). 관문은 못 읽는다.
            #    조용히 「표가 없다」로 넘기면 그 생성기가 통째로 빠진다 — gen_ships에서 실제로 그랬다.
            bad_files.append((base, "🔴 %s는 있는데 **글자 그대로의 dict가 아니라** 못 읽는다(내포 표기?) — 리터럴로 바꿀 것"
                              % "·".join(sorted({t.id for n in named for t in n.targets if getattr(t, "id", None) in DICT_NAMES}))))
            continue
        if not dicts:
            bad_files.append((base, KNOWN_ODD.get(base, "🔴 항목 표(%s)를 못 찾았다 — 이름이 다르거나 새 꼴이다"
                                                   % "·".join(DICT_NAMES))))
            continue
        found.append((path, dicts))
    return found, bad_files


def entries():
    """(생성기 경로, 유닛 이름, rebuild 표식, 선언된 path) 목록 + 훑다 걸린 파일."""
    out, bad_files = [], []
    found, bad_files = unit_generators()
    for path, dicts in found:
        for node in dicts:
            for k, v in zip(node.value.keys, node.value.values):
                if not isinstance(k, ast.Constant):
                    continue
                mark = decl = None
                if isinstance(v, ast.Call):
                    for kw in v.keywords:
                        if not isinstance(kw.value, ast.Constant):
                            continue
                        if kw.arg == "rebuild":
                            mark = kw.value.value
                        elif kw.arg == "path":
                            decl = kw.value.value
                out.append((path, k.value, mark, decl))
    return out, bad_files


def cross_check(rows):
    """③ 모은 항목 ↔ Assets의 실제 파일. 다르면 **양쪽에만 있는 이름을 찍는다.**

    ⚠️ 「Assets/Art/Units/<이름>/<이름>.fbx」로 **넘겨짚지 않는다** — 항목이 `path=`를 선언하면 그걸 본다.
       안흔함_상붕카는 `Assets/Art/Characters/안흔함_상붕카.glb`라, 넘겨짚으면 없는 결함이 생긴다.
    """
    have = {os.path.basename(os.path.dirname(f))
            for f in glob.glob(os.path.join(UNITS_DIR, "*", "*.fbx"))}
    mine, missing, noted = set(), [], []
    for _, name, mark, decl in rows:
        mine.add(name)
        want = decl or os.path.join("Assets", "Art", "Units", name, name + ".fbx")
        if os.path.exists(os.path.join(PROJECT, want)):
            continue
        # 표식으로 이유를 적어 둔 것은 🔴이 아니라 **적힌 대로** 보여 준다 — 안 그러면 경고가 무뎌진다.
        (noted if mark else missing).append((name, mark or want))
    print("── ③ 대조: 설정 %d이름 · Art/Units FBX %d이름" % (len(mine), len(have)))
    only_fbx = sorted(have - mine)
    if noted:
        print("   · 파일이 없지만 **표식에 이유가 적힌 것**(%d):" % len(noted))
        for n, w in sorted(set(noted)):
            print("      %-22s %s" % (n, w))
    if missing:
        print("   🔴 설정은 있는데 **그 파일이 없고 이유도 안 적혔다**(%d):" % len(missing))
        for n, w in sorted(set(missing)):
            print("      %-22s %s" % (n, w))
    if only_fbx:
        print("   🔴 파일은 있는데 **설정이 없다**(%d) — 이건 아무도 다시 못 만든다:" % len(only_fbx))
        print("      " + ", ".join(only_fbx))
    if not missing and not only_fbx:
        print("   ✅ 설명 안 되는 차이 없음 (수가 달라도 위 두 줄이 비었으면 맞는 것 — "
              "설정 쪽엔 Art/Units 밖에 나가는 항목도 있다)")
    return missing, only_fbx


def main():
    args = [a for a in sys.argv[1:] if a != "--list"]
    list_only = "--list" in sys.argv
    rows, bad_files = entries()
    print("── ① 유닛 생성기 %d개에서 항목 %d개를 모았다" % (len({p for p, _, _, _ in rows}), len(rows)))
    for base, why in sorted(bad_files):
        print("── ② %-24s %s" % (base, why))
    missing, only_fbx = cross_check(rows)
    hard = [b for b in bad_files if b[0] not in KNOWN_ODD]
    if list_only:
        return 1 if (hard or only_fbx or missing) else 0

    want = set(args)
    rows = [r for r in rows if not want or r[1] in want]
    os.makedirs(OUT, exist_ok=True)
    print()
    ok = skipped = 0
    bad = []
    for path, name, mark, _ in rows:
        base = os.path.basename(path)
        if mark:
            print("SKIP\t%-24s %s\t%s" % (name, base, mark))
            skipped += 1
            continue
        fbx = os.path.join(OUT, name + ".fbx")
        if os.path.exists(fbx):
            os.remove(fbx)                             # 🔴 지난 실행의 산출물에 속지 않는다
        p = subprocess.run(["blender", "-b", "--factory-startup", "--python", path, "--", "--out", OUT, name],
                           capture_output=True, text=True)
        if os.path.exists(fbx):
            ok += 1
            print("OK\t%-24s %s" % (name, base))
            continue
        # 왜 안 나왔는지는 마지막 예외 줄에 거의 다 있다. 예외 **이름 목록은 두지 않는다**(빠진다).
        why = next((L.strip() for L in reversed((p.stdout + p.stderr).splitlines())
                    if "Error" in L and ":" in L), "산출 FBX가 안 생겼다(종료코드 %s)" % p.returncode)
        bad.append((name, base, why[:160]))
        print("FAIL\t%-24s %s\t%s" % (name, base, why[:160]))
    print()
    print("항목 %d · 통과 %d · 실패 %d · 표식으로 건너뜀 %d" % (len(rows), ok, len(bad), skipped))
    if bad:
        print("⚠️ 실패를 그대로 보고하지 말 것 — 위 「읽는 법」의 ①②③으로 가른 뒤에 올릴 것.")
    return 1 if (bad or hard or only_fbx or missing) else 0


sys.exit(main())
