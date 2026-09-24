"""저장된 설정이 **오늘 실제로 도는지** 항목마다 한 번씩 돌려 보는 관문(PM 요청 2026-09-24).

왜 필요한가 — 2026-09-24에 181개를 한 번씩 돌려 봤더니 **9종이 그 자리에서 죽었습니다.**
가장 지독한 것은 히든_석성례였습니다: 저장된 설정으로는 아예 안 도는데 커밋된 FBX는 멀쩡히 있었고,
그 FBX는 **골반 뼈가 바닥 1.17m 아래**인 채였습니다. 「설정이 그 파일을 만든 설정이 아니다」 —
값을 봤지만 그 값이 쓰였는지는 아무도 안 봤던 것입니다.
👉 **산출할 때마다 돌리십시오.** 안 돌리면 또 쌓입니다.

읽는 법 — 실패는 세 종류이고 **뜻이 다릅니다. 안 가르고 「N종이 죽는다」로 보고하지 마십시오.**
  ③ 진짜 썩음    설정이 오늘 못 돈다(예: no_nulls=True인데 drop_bones에 Null 이름). **고칠 것.**
  ② 구조상 못 돎  source가 자기 산출물이라 두 번째 실행이 죽는다. `rebuild="원본에서만"` 표식으로 걸러진다.
  ① 검사가 늦음   assert가 그 항목보다 나중에 들어왔다. **커밋본을 열어 재 보면 산출물과 같은 상태다.**
                  (2026-09-24 실측: 특별함_박진웅·희귀함_양재모·희귀함_박기찬 등 열한 종이 여기였다)

🔴 `rebuild` 표식 — **기계가 읽는다.** 사람이 머리말을 읽어야만 아는 사실로 두지 않는다.
  rebuild="원본에서만"  source가 자기 산출물. 다시 뽑으려면 원본을 source로 바꿔 놓고 돌릴 것.
  rebuild="금지"        이 설정으로는 **커밋본이 안 나온다**. 다시 뽑으면 **다른 모델이 들어간다.**
                        (희귀함_박도진: 커밋본 정점 20,562 ≠ 이 설정 25,857)

쓰는 법:
  python3 Tools/blender/check_entries.py              # 전부(181종, 1~2시간)
  python3 Tools/blender/check_entries.py 유닛이름 ...   # 몇 개만
"""

import ast
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(PROJECT, ".check_entries_out")
GENERATORS = [("fix_unit_fbx.py", "UNITS"), ("gen_scan_rig.py", "UNITS"),
              ("gen_objrip_skin.py", "SKINS"), ("gen_rigify_skin.py", "SKINS"),
              ("gen_prop_unit.py", "UNITS")]
ERR = re.compile(r"(AssertionError|KeyError|ValueError|TypeError|RuntimeError|FileNotFoundError|"
                 r"IndexError|AttributeError|OSError): .*")


def entries():
    """(생성기 파일, 유닛 이름, rebuild 표식) 목록. 표식은 소스에서 ast로 읽는다."""
    out = []
    for fname, var in GENERATORS:
        path = os.path.join(HERE, fname)
        tree = ast.parse(open(path, encoding="utf-8").read())
        for node in tree.body:
            if not (isinstance(node, ast.Assign) and isinstance(node.value, ast.Dict)):
                continue
            if not any(getattr(t, "id", None) == var for t in node.targets):
                continue
            for k, v in zip(node.value.keys, node.value.values):
                if not isinstance(k, ast.Constant):
                    continue
                mark = None
                if isinstance(v, ast.Call):
                    for kw in v.keywords:
                        if kw.arg == "rebuild" and isinstance(kw.value, ast.Constant):
                            mark = kw.value.value
                out.append((path, k.value, mark))
    return out


def main():
    want = set(sys.argv[1:])
    rows = [r for r in entries() if not want or r[1] in want]
    os.makedirs(OUT, exist_ok=True)
    ok = skipped = 0
    bad = []
    for path, name, mark in rows:
        if mark:
            print("SKIP\t%-24s %s\t%s" % (name, os.path.basename(path), mark))
            skipped += 1
            continue
        p = subprocess.run(["blender", "-b", "--factory-startup", "--python", path, "--", "--out", OUT, name],
                           capture_output=True, text=True)
        m = ERR.search(p.stdout + p.stderr)
        if m:
            bad.append((name, os.path.basename(path), m.group(0)[:160]))
            print("FAIL\t%-24s %s\t%s" % (name, os.path.basename(path), m.group(0)[:160]))
        else:
            ok += 1
            print("OK\t%-24s %s" % (name, os.path.basename(path)))
    print()
    print("항목 %d · 통과 %d · 실패 %d · 표식으로 건너뜀 %d" % (len(rows), ok, len(bad), skipped))
    if bad:
        print("⚠️ 실패를 그대로 보고하지 말 것 — 위 「읽는 법」의 ①②③으로 가른 뒤에 올릴 것.")
    return 1 if bad else 0


sys.exit(main())
