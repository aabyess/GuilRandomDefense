"""새 테마 PNG에 .meta를 만든다: 옛(선술집) 같은 이름 그림의 .meta(9-slice 경계·가져오기 설정)를 복사하고 GUID·spriteID만 새로 뽑는다.
사용: make_metas.py <새 폴더> <옛 폴더>   — 새 폴더의 .meta가 이미 있으면 건드리지 않는다(GUID 유지)."""
import os, re, sys, uuid
new, old = sys.argv[1], sys.argv[2]
for f in sorted(os.listdir(new)):
    if not f.endswith(".png") or os.path.exists(os.path.join(new, f + ".meta")): continue
    src = os.path.join(old, f + ".meta")
    if not os.path.exists(src): print("옛 meta 없음", f); continue
    s = open(src, encoding="utf-8").read()
    s = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, s, count=1)
    s = re.sub(r"spriteID: [0-9a-f]{32}", "spriteID: " + uuid.uuid4().hex, s)
    open(os.path.join(new, f + ".meta"), "w", encoding="utf-8").write(s)
    print("meta", f)
