#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
이 스크립트는 아무것도 쓰지 않는다 — Assets/Scripts/의 ScriptableObject 클래스 정의와
Assets/의 그 에셋 인스턴스들을 읽기만 한다. `Tools/generate_*.py`(파괴적)와 헷갈리지
않도록 동사를 "generate"가 아니라 "audit"로 뒀다.

무엇을 하나: 오늘(2026-09-05) 도움소·적·유닛·조합식 필드 감사를 세 번 손으로 돌렸는데
매번 스크립트를 새로 짜고 지웠다 — 그중 한 번은 리스트 안쪽 필드(`count:`)를 최상위로
잘못 세는 실수를 했다(구현담당3, 라운드당 마릿수). 같은 실수를 막으려고 **리스트 내부
필드와 최상위 필드를 들여쓰기 깊이로 명확히 가른다.**

판정 셋만 자동으로 낸다:
  읽힘 — 코드가 이 필드를 읽고, 에셋 전 인스턴스에 값이 있다
  누락 — 코드는 읽는데 일부/전체 인스턴스에 필드 자체가 없다(오래된 에셋이 필드 도입
         전에 만들어진 경우 등 — 폭우 waveCount가 그랬다)
  미사용 — 에셋엔 있는데 읽는 코드가 없다(전 인스턴스 필드가 있어도 미사용일 수 있다)

⚠️ **"해당없음"은 이 스크립트가 못 낸다.** "이 필드는 특정 enum 값(effect 등)일 때만
쓰인다"는 판단은 코드의 조건 분기를 읽어야 하는 사람 몫이다 — 자동으로 내면 오답 위험이
크다. 이 스크립트의 "누락"에는 그런 "해당없음인데 값이 없어 보이는" 경우가 섞여 있을 수
있다. 사람이 결과표를 보고 걸러야 한다(SUPPORT_SHOP.md/UNIT_DATA 감사 때 했던 방식).

사용법: `python3 Tools/audit_data.py` (프로젝트 루트에서 실행). 클래스 하나만 보려면
`python3 Tools/audit_data.py EnemyData`.
"""
import re
import glob
import os
import sys
from collections import defaultdict, Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPTS_DIR = os.path.join(ROOT, "Assets", "Scripts")

VALUE_TYPES = {
    "int", "float", "bool", "double", "long", "short", "byte", "string", "char", "uint",
    "Vector2", "Vector3", "Vector4", "Color", "Quaternion", "Rect", "Bounds",
    "Vector2Int", "Vector3Int", "LayerMask", "AnimationCurve",
}


def read_all_cs():
    files = glob.glob(os.path.join(SCRIPTS_DIR, "**", "*.cs"), recursive=True)
    return {f: open(f, encoding="utf-8").read() for f in files}


def collect_enum_names(cs_texts):
    names = set()
    for text in cs_texts.values():
        for m in re.finditer(r'\benum\s+([A-Za-z_]\w*)', text):
            names.add(m.group(1))
    return names


def collect_classes(cs_texts):
    """returns {classname: {"base": str|None, "path": str, "is_serializable": bool, "body": str}}"""
    classes = {}
    for path, text in cs_texts.items():
        for m in re.finditer(r'class\s+(\w+)\s*(?::\s*([\w.]+))?\s*\{', text):
            cname, base = m.group(1), m.group(2)
            start = m.end()
            depth = 1
            i = start
            while i < len(text) and depth > 0:
                if text[i] == '{':
                    depth += 1
                elif text[i] == '}':
                    depth -= 1
                i += 1
            body = text[start:i - 1]
            preceding = text[max(0, m.start() - 120):m.start()]
            is_serializable = "[System.Serializable]" in preceding or "[Serializable]" in preceding
            classes[cname] = {"base": base, "path": path, "is_serializable": is_serializable, "body": body}
    return classes


def strip_nested_classes(body):
    """중첩 클래스(예: DamageTable 안의 Row)의 몸통을 지워서, 그 안의 필드가 바깥 클래스
    자신의 필드로 잘못 섞이지 않게 한다 — 실제로 이 문제로 DamageTable.vsLarge 같은 Row의
    필드가 DamageTable 자신의 필드처럼 나온 적이 있다."""
    out = []
    i = 0
    for m in re.finditer(r'class\s+\w+\s*(?::\s*[\w.]+)?\s*\{', body):
        if m.start() < i:
            continue  # 이미 지운 범위 안에 있는 중첩의 중첩
        out.append(body[i:m.start()])
        depth = 1
        j = m.end()
        while j < len(body) and depth > 0:
            if body[j] == '{':
                depth += 1
            elif body[j] == '}':
                depth -= 1
            j += 1
        i = j
    out.append(body[i:])
    return "".join(out)


def extract_fields(body):
    """(type, name) pairs for public fields or [SerializeField] fields, skipping properties/consts/statics."""
    body = strip_nested_classes(body)
    lines = body.split("\n")
    fields = []
    pending_serialize = False
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("[SerializeField]"):
            pending_serialize = True
            rest = stripped[len("[SerializeField]"):].strip()
            stripped = rest if rest else None
            if stripped is None:
                continue
        if re.match(r'^\[.*\]$', stripped):
            continue
        if '=>' in stripped or '{' in stripped:
            pending_serialize = False
            continue
        m = re.match(r'^(?:public|private|protected|internal|static|readonly|\s)*'
                     r'([A-Za-z_][\w<>\[\],\s.]*?)\s+(\w+)\s*(=.*)?;', stripped)
        if m:
            ftype, fname = m.group(1).strip(), m.group(2)
            is_public = stripped.startswith("public")
            is_sf = pending_serialize
            pending_serialize = False
            if not (is_public or is_sf):
                continue
            if "const" in line or "static" in line or "readonly" in line:
                continue
            if ftype in ("event", "class", "void"):
                continue
            fields.append((ftype, fname))
        else:
            pending_serialize = False
    return fields


def classify_field(ftype, enum_names, classes):
    """returns (is_list, element_type_str)"""
    m = re.match(r'List<([\w.]+)>$', ftype)
    if m:
        return True, m.group(1)
    if ftype.endswith("[]"):
        return True, ftype[:-2]
    return False, ftype


def get_guid(cs_path):
    meta_path = cs_path + ".meta"
    if not os.path.exists(meta_path):
        return None
    m = re.search(r"guid:\s*(\w+)", open(meta_path, encoding="utf-8").read())
    return m.group(1) if m else None


def code_reads_field(fieldname, cs_texts, own_class_body=None):
    """`.fieldname` 형태(다른 변수를 거친 접근)는 전 파일에서 찾는다 — **선언 파일도 포함한다.**
    처음엔 선언 파일을 빼고 찾았는데, 그러면 같은 파일 안에서 자기 필드를 다른 메서드가 읽는
    경우(예: DamageTable.Multiplier()가 DamageTable 자신의 Row 필드를 읽는 것)를 전부
    놓친다 — 실제로 이 버그로 DamageTable 전체가 "미사용"으로 잘못 나온 적이 있다.

    `own_class_body`를 주면, **점 없이 자기 필드를 바로 쓰는 경우**(`pierce`처럼 `this.` 없이
    쓰는 것)도 그 클래스 자신의 메서드 본문 안에서만 단어 경계로 찾는다 — 파일 전체에서
    점 없는 단어를 찾으면 오탐이 너무 많아서, 범위를 그 클래스 자신으로 좁혔다."""
    pattern = re.compile(rf'\.{re.escape(fieldname)}\b')
    for text in cs_texts.values():
        if pattern.search(text):
            return True
    if own_class_body:
        bare_pattern = re.compile(rf'(?<![.\w]){re.escape(fieldname)}\b')
        if bare_pattern.search(own_class_body):
            return True
    return False


def load_asset_instances(guid):
    """Scan Assets/**/*.asset for MonoBehaviour blocks whose m_Script guid matches.
    Returns list of (filename, body_text)."""
    results = []
    for path in glob.glob(os.path.join(ROOT, "Assets", "**", "*.asset"), recursive=True):
        if "/Photon/" in path.replace("\\", "/"):
            continue
        try:
            text = open(path, encoding="utf-8").read()
        except (UnicodeDecodeError, OSError):
            continue
        if f"guid: {guid}," not in text:
            continue
        for seg in re.split(r'\n(?=--- !u!)', text):
            if f"guid: {guid}," in seg and "m_Script:" in seg:
                results.append((os.path.basename(path), seg))
    return results


def top_level_field_status(body, fieldname):
    """returns ("scalar_present"|"scalar_absent"|"list", value_or_length)"""
    m = re.search(rf'^  {re.escape(fieldname)}:(.*)$', body, re.MULTILINE)
    if not m:
        return "absent", None
    rest = m.group(1).strip()
    if rest == "[]":
        return "list", 0
    if rest == "" or rest.startswith("- "):
        # 여러 줄짜리 리스트. 항목 하나가 "  - key: val"(시작) + "    key: val"(이어지는 필드들
        # — 대시 없이 4칸)로 여러 줄에 걸치므로, **항목 시작줄("  - ")의 개수만** 센다.
        # 이어지는 4칸 줄에서 멈추면 항목이 2개 이상일 때 1개로 잘못 세게 된다 — 실제로 겪은 버그.
        # ⚠️ 드물게 "fieldname:  - kind: 0"처럼 첫 항목이 콜론과 같은 줄에 붙은 에셋이 있다
        # (줄바꿈 없이 저장된 것) — rest.startswith("- ")로 그 경우도 항목 1개로 잡는다.
        cnt = 1 if rest.startswith("- ") else 0
        start_idx = body[:m.start()].count("\n") + 1
        lines = body.split("\n")
        j = start_idx
        while j < len(lines) and (lines[j].startswith("  - ") or lines[j].startswith("    ")):
            if lines[j].startswith("  - "):
                cnt += 1
            j += 1
        return "list", cnt
    return "scalar", rest


def list_item_blocks(body, fieldname):
    """returns list of item text-blocks (dash line + following 4-space lines)."""
    m = re.search(rf'^  {re.escape(fieldname)}:(.*)$', body, re.MULTILINE)
    if not m:
        return []
    # m.end()는 그 줄의 끝(개행 문자 바로 앞)이라 split("\n")의 첫 조각은 항상 빈 문자열이다
    # — [1:]로 건너뛴다. 안 건너뛰면 그 빈 줄에서 바로 break해서 항목을 하나도 못 잡는다(버그였음).
    lines = body[m.end():].split("\n")[1:]
    items = []
    cur = []
    # ⚠️ 드물게 "fieldname:  - kind: 0"처럼 첫 항목이 콜론과 같은 줄에 붙는다 — 그 줄도
    # 첫 항목으로 잡는다. 안 잡으면 이 4개 에셋의 첫 항목 서브필드가 통째로 빠진다.
    inline_rest = m.group(1).strip()
    if inline_rest.startswith("- "):
        cur = [inline_rest[2:]]
    for line in lines:
        if line.startswith("  - "):
            if cur:
                items.append("\n".join(cur))
            cur = [line[4:]]  # strip "  - "
        elif line.startswith("    "):
            if cur:
                cur.append(line[4:])
        else:
            break
    if cur:
        items.append("\n".join(cur))
    return items


def nested_field_present(item_block, fieldname):
    return re.search(rf'^{re.escape(fieldname)}:', item_block, re.MULTILINE) is not None


def audit_class(cname, classes, enum_names, cs_texts):
    info = classes[cname]
    guid = get_guid(info["path"])
    if guid is None:
        print(f"  (⚠️ {cname}: .cs.meta 없음 — 건너뜀)")
        return

    fields = extract_fields(info["body"])
    instances = load_asset_instances(guid)
    total = len(instances)
    print(f"\n=== {cname} ({info['path']}) — 인스턴스 {total}개 ===")
    if total == 0:
        print("  (에셋 인스턴스 0개 — 필드 감사 생략)")
        return

    for ftype, fname in fields:
        is_list, elem = classify_field(ftype, enum_names, classes)
        reads = code_reads_field(fname, cs_texts, own_class_body=info["body"])

        if not is_list:
            present = 0
            for _, body in instances:
                status, _ = top_level_field_status(body, fname)
                if status != "absent":
                    present += 1
            verdict = "미사용" if not reads else ("읽힘" if present == total else "누락")
            print(f"  {fname:28s} [{ftype}] {verdict:6s}  {present}/{total}")
        else:
            elem_class = classes.get(elem)
            if elem_class and elem_class["is_serializable"]:
                # nested class inside a list — audit its own fields separately, at deeper indent
                lengths = []
                for _, body in instances:
                    status, val = top_level_field_status(body, fname)
                    lengths.append(val if status == "list" else 0)
                nonempty = sum(1 for l in lengths if l and l > 0)
                verdict = "미사용" if not reads else ("읽힘" if nonempty > 0 else "누락(전부 빈 리스트)")
                print(f"  {fname:28s} [List<{elem}>] {verdict:6s}  길이분포={dict(Counter(lengths))}")
                for sub_ftype, sub_fname in extract_fields(elem_class["body"]):
                    sub_reads = code_reads_field(sub_fname, cs_texts, own_class_body=elem_class["body"])
                    sub_present, sub_total_items = 0, 0
                    for _, body in instances:
                        for item in list_item_blocks(body, fname):
                            sub_total_items += 1
                            if nested_field_present(item, sub_fname):
                                sub_present += 1
                    if sub_total_items == 0:
                        continue
                    sub_verdict = "미사용" if not sub_reads else ("읽힘" if sub_present == sub_total_items else "누락")
                    print(f"    └ {sub_fname:24s} [{sub_ftype}] {sub_verdict:6s}  {sub_present}/{sub_total_items}(리스트 항목 기준)")
            else:
                # list of primitives/enums/UnityEngine.Object refs — just presence + length
                lengths = []
                for _, body in instances:
                    status, val = top_level_field_status(body, fname)
                    lengths.append(val if status == "list" else (0 if status == "absent" else None))
                present_insts = sum(1 for l in lengths if l is not None)
                verdict = "미사용" if not reads else ("읽힘" if present_insts == total else "누락")
                print(f"  {fname:28s} [List<{elem}>] {verdict:6s}  {present_insts}/{total} 존재, 길이분포={dict(Counter(l for l in lengths if l is not None))}")


def main():
    cs_texts = read_all_cs()
    enum_names = collect_enum_names(cs_texts)
    classes = collect_classes(cs_texts)

    targets = [c for c, info in classes.items() if info["base"] == "ScriptableObject"]
    if len(sys.argv) > 1:
        targets = [c for c in targets if c in sys.argv[1:]]
        if not targets:
            sys.exit(f"FATAL: {sys.argv[1:]} 중 ScriptableObject 클래스가 없다.")

    print(f"ScriptableObject 클래스 {len(targets)}개 감사: {', '.join(sorted(targets))}")
    for cname in sorted(targets):
        audit_class(cname, classes, enum_names, cs_texts)


if __name__ == "__main__":
    main()
