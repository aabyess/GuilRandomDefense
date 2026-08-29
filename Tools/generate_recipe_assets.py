# -*- coding: utf-8 -*-
import os, sys, uuid, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import recipes_data as D

ROOT = "/Users/sang/Documents/GitHub/GuilRandomDefense"
UNITDATA_GUID   = "e1426f971d9174ed9b3e5519d787e74b"
ITEMDATA_GUID   = "287e5f1f1efa421eab118d0dda3fdd4c"
RECIPE_GUID     = "d9e360a1ab0e48608e0b08ef0e946ccf"

GRADE = {"Common":0,"Uncommon":1,"Special":2,"Rare":3,"Hidden":4,"Legendary":5,
         "Limited":6,"Transcendent":7,"Immortal":8,"Eternal":9,"RandomUnit":10,"OtherWorld":11,
         "Superior":12}   # Superior(특수함)는 기존 에셋 값 보존을 위해 enum 맨 끝에 추가됨
DMG = {"":0,"AD":1,"AP":2,"AD+AP":3}
RES = {"Wood":0,"Token":1,"LuckyToken":2}
# UnitGradeExtensions.Tier()와 동일. 동급은 같은 값, RandomUnit은 조합 라인 밖(-1).
# 강함 순서는 enum 순서가 아니라 이 표가 결정한다 (특수함이 희귀함과 전설적인 사이에 들어감).
TIER = {"Common":0,"Uncommon":1,"Special":2,"Rare":3,"Hidden":3,"Superior":4,"Legendary":5,
        "Limited":6,"Transcendent":7,"Immortal":7,"Eternal":7,"RandomUnit":-1,"OtherWorld":7}

def new_guid(): return uuid.uuid4().hex
def safe(name):
    return re.sub(r'[^0-9A-Za-z가-힣_]', '_', name).strip('_')

def meta(guid):
    return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)

def folder_meta(guid):
    return ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)

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

# ── 1) 유닛 수집 ──────────────────────────────────────────
# 결과 유닛(등급 확정) / 재료 전용 유닛(등급 미확정)
results = {}   # (캐릭터, 등급) -> {dmg, asset_name}
materials = set()

def add_result(name, grade, dmg, suffix=""):
    key = (name, grade, suffix)
    results[key] = dmg
    return key

def add_mats(lst):
    for m in lst:
        if m.startswith("아이템["): continue
        materials.add(m)

for r in D.TRANSCENDENT:
    add_result(r[0], "Transcendent", r[1], r[1]); add_mats(r[2])
for r in D.HIDDEN_PERSON + D.HIDDEN_PLACE:
    add_result(r[0], "Hidden", r[1]); add_mats(r[2])
for n, d in D.HIDDEN_DOGS:
    add_result(n, "Hidden", d)
for r in D.IMMORTAL:
    add_result(r[0], "Immortal", r[1]); add_mats(r[2])
for r in D.ETERNAL:
    add_result(r[0], "Eternal", r[1]); add_mats(r[2])
for r in D.LIMITED:
    add_result(r[0], "Limited", r[1]); add_mats(r[2])
for n, d in D.RANDOM_UNITS:
    add_result(n, "RandomUnit", d)
for r in D.OTHERWORLD:
    add_result(r[0], "OtherWorld", r[1]); add_mats(r[2])

# 결과로 등록된 캐릭터명 집합
result_names = {k[0] for k in results}
# 재료 전용(어느 등급인지 모름)
material_only = sorted(materials)

# ── 2) 에셋 이름/GUID 배정 ────────────────────────────────
unit_guid = {}       # asset_name -> guid
unit_asset = {}      # key -> asset_name

GRADE_KR = {"Transcendent":"초월","Hidden":"히든","Immortal":"불멸","Eternal":"영원",
            "Limited":"제한","RandomUnit":"랜덤","OtherWorld":"다른세계","Superior":"특수함"}

for (name, grade, suffix), dmg in results.items():
    base = f"{GRADE_KR[grade]}_{name}"
    if suffix: base += f"_{suffix.replace('+','')}"
    an = safe(base)
    unit_asset[(name, grade, suffix)] = an
    unit_guid.setdefault(an, new_guid())

# 재료 전용 유닛 (기본 등급 미확정 → Common 임시)
mat_asset = {}
for m in material_only:
    an = safe(f"기본_{m}")
    mat_asset[m] = an
    unit_guid.setdefault(an, new_guid())

item_guid = {}
for it in D.ITEMS:
    item_guid[it] = new_guid()

# ── 3) 파일 쓰기 ─────────────────────────────────────────
def write(path, content):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f: f.write(content)

def unit_asset_body(asset_name, unit_name, grade, dmg):
    return head(UNITDATA_GUID, asset_name) + f"""  unitName: {unit_name}
  grade: {GRADE[grade]}
  damageType: {DMG.get(dmg,0)}
  hp: 0
  attackPower: 0
  attackRange: 0
  attackSpeed: 0
  moveSpeed: 0
  skill: {{fileID: 0}}
  prefab: {{fileID: 1000000000000000001, guid: 4e2d5e92014e4a2e9ec20862d26bf3b8, type: 3}}
"""

UNITS_DIR = f"{ROOT}/Assets/Data/Units/Roster"
ITEMS_DIR = f"{ROOT}/Assets/Data/Items"
REC_DIR   = f"{ROOT}/Assets/Data/Recipes"

for d, g in ((UNITS_DIR, new_guid()), (ITEMS_DIR, new_guid()), (REC_DIR, new_guid())):
    os.makedirs(d, exist_ok=True)
    if not os.path.exists(d + ".meta"):
        write(d + ".meta", folder_meta(g))

count_u = 0
for (name, grade, suffix), dmg in results.items():
    an = unit_asset[(name, grade, suffix)]
    write(f"{UNITS_DIR}/{an}.asset", unit_asset_body(an, name, grade, dmg))
    write(f"{UNITS_DIR}/{an}.asset.meta", meta(unit_guid[an]))
    count_u += 1

for m, an in mat_asset.items():
    write(f"{UNITS_DIR}/{an}.asset", unit_asset_body(an, m, "Common", ""))
    write(f"{UNITS_DIR}/{an}.asset.meta", meta(unit_guid[an]))
    count_u += 1

for it, g in item_guid.items():
    an = safe(f"아이템_{it}")
    write(f"{ITEMS_DIR}/{an}.asset", head(ITEMDATA_GUID, an) + f"  itemName: {it}\n")
    write(f"{ITEMS_DIR}/{an}.asset.meta", meta(g))

print(f"유닛 {count_u}개, 아이템 {len(item_guid)}개 생성")

# ── 4) 레시피 ────────────────────────────────────────────
def unit_ref(material_name, recipe_tier):
    """재료 이름 → UnitData 참조.

    재료가 '더 낮은 티어의 조합 결과물'이면 그 결과 에셋을 가리킨다 (다단 조합).
    예) 초월(6) 레시피의 재료 '감탄떡볶이'는 히든(3) 결과물이므로 히든_감탄떡볶이를 참조.
    같은 티어 이상이면 기본(하위) 유닛을 가리킨다 — 자기 자신을 재료로 삼는 순환 방지.
    예) 초월 최상호의 재료 '최상호'는 기본_최상호.
    """
    lower = [k for k in unit_asset
             if k[0] == material_name and TIER[k[1]] < recipe_tier]
    if lower:
        lower.sort(key=lambda k: -TIER[k[1]])   # 가장 높은 하위 티어 선택
        an = unit_asset[lower[0]]
    elif material_name in mat_asset:
        an = mat_asset[material_name]
    else:
        cands = [k for k in unit_asset if k[0] == material_name]
        if not cands: return None
        an = unit_asset[cands[0]]
    return f"{{fileID: 11400000, guid: {unit_guid[an]}, type: 2}}"

def ingredients_yaml(mats, recipe_tier, item_mats=(), wildcard=0):
    from collections import Counter
    lines = []
    for m, c in Counter(mats).items():
        ref = unit_ref(m, recipe_tier)
        if ref is None:
            print("  ⚠️ 재료 참조 실패:", m); continue
        lines.append(f"""  - kind: 0
    unit: {ref}
    item: {{fileID: 0}}
    wildcardGrade: 0
    count: {c}""")
    for it in item_mats:
        lines.append(f"""  - kind: 1
    unit: {{fileID: 0}}
    item: {{fileID: 11400000, guid: {item_guid[it]}, type: 2}}
    wildcardGrade: 0
    count: 1""")
    if wildcard:
        lines.append(f"""  - kind: 2
    unit: {{fileID: 0}}
    item: {{fileID: 0}}
    wildcardGrade: {GRADE['RandomUnit']}
    count: {wildcard}""")
    return "\n".join(lines) if lines else " []"

def resources_yaml(res):
    if not res: return " []"
    return "\n" + "\n".join(f"  - type: {RES[t]}\n    amount: {a}" for t, a in res.items())

def write_recipe(asset_name, command_id, result_key, mats, gold=0, res=None,
                 items=(), wildcard=0, min_r=0, max_r=0, save=0):
    recipe_tier = TIER[result_key[1]]
    g = new_guid()
    ran = unit_asset[result_key]
    ing = ingredients_yaml(mats, recipe_tier, items, wildcard)
    body = head(RECIPE_GUID, asset_name) + f"""  commandId: {command_id}
  result: {{fileID: 11400000, guid: {unit_guid[ran]}, type: 2}}
  ingredients:
{ing}
  goldCost: {gold}
  resourceCosts:{resources_yaml(res)}
  minRound: {min_r}
  maxRound: {max_r}
  requiredSaveCount: {save}
"""
    write(f"{REC_DIR}/{asset_name}.asset", body)
    write(f"{REC_DIR}/{asset_name}.asset.meta", meta(g))

n = 0
for name, dmg, mats, kr, cmd in D.TRANSCENDENT:
    write_recipe(safe(f"초월_{name}_{dmg.replace('+','')}"), cmd, (name,"Transcendent",dmg), mats); n+=1
for name, dmg, mats, kr, cmd in D.HIDDEN_PERSON + D.HIDDEN_PLACE:
    write_recipe(safe(f"히든_{name}"), cmd, (name,"Hidden",""), mats); n+=1
for name, dmg, mats, wood, kr, cmd in D.IMMORTAL:
    write_recipe(safe(f"불멸_{name}"), cmd, (name,"Immortal",""), mats,
                 res={"Wood":wood} if wood else None); n+=1
for name, dmg, mats, items, wood, save, kr, cmd in D.ETERNAL:
    write_recipe(safe(f"영원_{name}"), cmd, (name,"Eternal",""), mats,
                 res={"Wood":wood} if wood else None, items=items, save=save); n+=1
for name, dmg, mats, items, extra_gold, lucky, min_r, max_r in D.LIMITED:
    if name == "김강민":
        gold, res = extra_gold, None                     # 공통비용 제외 대상
    else:
        gold, res = 5000 + extra_gold, {"Wood":5}
        if lucky: res["LuckyToken"] = lucky
    write_recipe(safe(f"제한_{name}"), f"{name} limited", (name,"Limited",""), mats,
                 gold=gold, res=res, items=items, min_r=min_r, max_r=max_r); n+=1
for name, dmg, mats, wildcard, extra_gold in D.OTHERWORLD:
    res = {"Wood":7, "LuckyToken":1}
    write_recipe(safe(f"다른세계_{name}"), f"{name} otherworld", (name,"OtherWorld",""), mats,
                 gold=10000+extra_gold, res=res, wildcard=wildcard); n+=1

print(f"레시피 {n}개 생성")
