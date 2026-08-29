# -*- coding: utf-8 -*-
"""하위 등급(흔함~전설적인) UnitData + CombineRecipe 에셋 생성.

재료의 (이름, 별칭) 쌍에서 별칭으로 등급을 역추적한다.
같은 이름이 여러 등급에 존재하므로 별칭이 유일한 식별 수단이다.
"""
import os, re, sys, uuid
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import low_grade_data as D

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
UNITDATA_GUID = "e1426f971d9174ed9b3e5519d787e74b"
RECIPE_GUID   = "d9e360a1ab0e48608e0b08ef0e946ccf"
PREFAB = "{fileID: 1000000000000000001, guid: 4e2d5e92014e4a2e9ec20862d26bf3b8, type: 3}"

GRADE_VAL = {"흔함":0, "안흔함":1, "특별함":2, "희귀함":3, "전설적인":5, "특수함":12}

UNITS_DIR = os.path.join(ROOT, "Assets/Data/Units/Roster")
REC_DIR   = os.path.join(ROOT, "Assets/Data/Recipes")

def safe(n): return re.sub(r'[^0-9A-Za-z가-힣_]', '_', n).strip('_')
def new_guid(): return uuid.uuid4().hex

def meta(g):
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

def head(script_guid, name):
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: 
"""

# ── 1) 모든 유닛을 (이름, 등급, 별칭) 으로 수집 ──────────────────
# 같은 이름·같은 등급인데 별칭이 다른 유닛이 존재한다 (예: 희귀함 최상호가
# '윤식파의두뇌'와 '오타쿠의길' 두 종류). 따라서 별칭까지 키에 넣어야 한다.
units = set()       # (name, grade, alias)
def reg(name, grade, alias=""):
    units.add((name, grade, alias))

for n in D.COMMON:
    reg(n, "흔함")
for n, a, _ in D.UNCOMMON:  reg(n, "안흔함", a)
for n, a in D.UNCOMMON_DRAW: reg(n, "안흔함", a)
for n, a, _ in D.SPECIAL:   reg(n, "특별함", a)
for n, a, _ in D.RARE:      reg(n, "희귀함", a)
for n, a in D.RARE_DRAW:    reg(n, "희귀함", a)
for n, a, _ in D.LEGENDARY: reg(n, "전설적인", a)
for n in D.SUPERIOR_DRAW:   reg(n, "특수함")

by_name_grade = {}
for (name, grade, alias) in units:
    by_name_grade.setdefault((name, grade), []).append(alias)

# 별칭 → (이름, 등급) 역인덱스
alias_index = {}
for (name, grade, alias) in units:
    if alias:
        alias_index[(name, alias)] = grade

def resolve(name, alias):
    """(이름, 별칭) → 등급. 별칭이 없으면 흔함 → 안흔함 순으로 본다."""
    if not alias:
        if (name, "흔함") in by_name_grade: return "흔함"
        if (name, "안흔함") in by_name_grade: return "안흔함"
        return None
    return alias_index.get((name, alias))

def asset_name(name, grade, alias=""):
    """같은 이름·등급에 별칭이 여럿이면 별칭까지 붙여 구분한다."""
    if alias and len(by_name_grade.get((name, grade), [])) > 1:
        return safe(f"{grade}_{name}_{alias}")
    return safe(f"{grade}_{name}")

# ── 2) UnitData 에셋 ─────────────────────────────────────────
unit_guid = {}   # (name, grade, alias) -> guid
os.makedirs(UNITS_DIR, exist_ok=True)
for (name, grade, alias) in sorted(units):
    an = asset_name(name, grade, alias)
    g = new_guid()
    unit_guid[(name, grade, alias)] = g
    display = alias if alias else name
    body = head(UNITDATA_GUID, an) + f"""  unitName: {display}
  grade: {GRADE_VAL[grade]}
  damageType: 0
  movementAbility: 0
  hp: 0
  attackPower: 0
  attackRange: 0
  attackSpeed: 0
  moveSpeed: 0
  skill: {{fileID: 0}}
  prefab: {PREFAB}
"""
    open(f"{UNITS_DIR}/{an}.asset","w",encoding="utf-8").write(body)
    open(f"{UNITS_DIR}/{an}.asset.meta","w",encoding="utf-8").write(meta(g))

print(f"유닛 {len(units)}개 생성")

# ── 3) CombineRecipe 에셋 ────────────────────────────────────
from collections import Counter
os.makedirs(REC_DIR, exist_ok=True)
made, failed = 0, []

def write_recipe(result_name, result_grade, result_alias, mats):
    global made
    an = safe(f"{asset_name(result_name, result_grade, result_alias)}_조합")
    counts = Counter()
    for mn, ma in mats:
        mg = resolve(mn, ma)
        if mg is None:
            failed.append((result_name, result_grade, mn, ma)); return
        counts[(mn, mg, ma if (mn, mg, ma) in unit_guid else "")] += 1

    lines = []
    for (mn, mg, malias), c in counts.items():
        lines.append(f"""  - kind: 0
    unit: {{fileID: 11400000, guid: {unit_guid[(mn,mg,malias)]}, type: 2}}
    item: {{fileID: 0}}
    wildcardGrade: 0
    count: {c}""")

    g = new_guid()
    body = head(RECIPE_GUID, an) + f"""  commandId: {result_alias or result_name}
  result: {{fileID: 11400000, guid: {unit_guid[(result_name,result_grade,result_alias)]}, type: 2}}
  ingredients:
{chr(10).join(lines)}
  goldCost: 0
  resourceCosts: []
  minRound: 0
  maxRound: 0
  requiredSaveCount: 0
"""
    open(f"{REC_DIR}/{an}.asset","w",encoding="utf-8").write(body)
    open(f"{REC_DIR}/{an}.asset.meta","w",encoding="utf-8").write(meta(g))
    made += 1

for n,a,mats in D.UNCOMMON:  write_recipe(n,"안흔함",a,mats)
for n,a,mats in D.SPECIAL:   write_recipe(n,"특별함",a,mats)
for n,a,mats in D.RARE:      write_recipe(n,"희귀함",a,mats)
for n,a,mats in D.LEGENDARY: write_recipe(n,"전설적인",a,mats)

print(f"레시피 {made}개 생성")
if failed:
    print(f"\n⚠️ 재료 해석 실패 {len(failed)}건:")
    for r,g,mn,ma in failed:
        print(f"   {g} {r} ← 재료 '{mn}'(별칭 '{ma}')")
