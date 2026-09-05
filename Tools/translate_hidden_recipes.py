#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""④ 히든 레시피 23종 → 우리 히든 유닛 22종 (PM 지시, 2026-09-06).

PM 지시 원문 요약 — 순위 매칭이 아니라 "번역":
1단계: 원작 히든 23종을 등급 안 순위(원작=레시피 총 등급투자, 우리=DPS)로 우리 22종에
       매칭. 하나(가장 약한 것)는 드롭하고 목록에 남긴다.
2단계: 재료(원작 유닛ID)를 이미 있는 원작→로스터 대응표로 "번역"한다. 대응 없으면
       그 재료의 원작 등급 안에서 순위로 배정(같은 원칙). 둘 다 안 되면 레시피를 비우고
       목록에 남긴다 — 추측 금지.
3단계: commandId를 원작 채팅 코드로 전부 교체.

## 원작→로스터 대응표 구성 (구현담당1, 2026-09-06)
스킬 배정에 쓴 4채널을 전부 뒤져 우선순위로 병합했다:
  06번①(SkillData_원작0NN_{uid}.asset, 파일명에 uid 직접) >
  게이트/회수(SkillData_게이트_*·SkillData_회수_*, description에 "(uid)" 직접) >
  1채널(generate_unit_ability_skills.py 재시뮬레이션, uid 직접) >
  2채널(regroup_skill_damage_by_unit.py의 스냅샷, uid 직접)
⚠️ 알려진 한계: 채널 간 분산(뿌리 ⑮, ⑤로 다음 세션 이월)이 실제로 걸린 사례 1건 —
h02E·h01M·h021 세 원작 유닛이 전부 희귀함_황준석으로 번역된다(서로 다른 채널이 각자
독립으로 매칭한 결과). 세 레시피(Aokiji/Mihawk/Mobidic 대 Bonkure/Tiger 대 Dekken)가
겹치지 않아 당장 깨지진 않지만, 한 카피만 있으면 세 조합 중 하나만 완성할 수 있다 —
채널 통합(⑤)이 끝나면 자동으로 갈라진다. 추측으로 다른 채널을 강제로 고르지 않았다.
"""
import csv
import glob
import hashlib
import json
import re
import sys
from collections import defaultdict

sys.path.insert(0, 'Tools')
import generate_unit_ability_skills as gen1
import generate_unit_skills_by_gate as gen_gate
import regroup_skill_damage_by_unit as gen_regroup

RECIPE_DIR = 'Assets/Data/Recipes'
ROSTER_DIR = 'Assets/Data/Units/Roster'
RECIPE_SCRIPT_GUID = 'd9e360a1ab0e48608e0b08ef0e946ccf'  # CombineRecipe.cs (히든_감탄떡볶이.asset에서 확인)

TIER = {'흔함': 0, '안흔함': 1, '특별함': 2, '희귀함': 3, '히든': 4, '특수함': 4,
        '전설적인': 5, '변화된': 5, '영원한': 6, '랜덤전용': 7, '다른세계': 8,
        '초월함': 9, '제한됨': 10, '불멸의': 11}

GRADE_NAME_TO_ENUM = {'흔함': 0, '안흔함': 1, '특별함': 2, '희귀함': 3, '히든': 4,
                       '전설적인': 5, '제한됨': 6, '초월함': 7, '불멸의': 8, '영원한': 9,
                       '랜덤전용': 10, '특수함': 12, '변화된': 14}


def guid_for(name):
    return hashlib.md5(('guilrd/hiddenrecipe/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def load_roster():
    roster = {}
    for f in glob.glob(f'{ROSTER_DIR}/*.asset'):
        text = open(f, encoding='utf-8').read()
        gm = re.search(r'^  grade: (\d+)', text, re.M)
        if not gm:
            continue
        ap = float(re.search(r'^  attackPower: ([\d.]+)', text, re.M).group(1))
        aspd = float(re.search(r'^  attackSpeed: ([\d.]+)', text, re.M).group(1))
        base = f.split('/')[-1][:-len('.asset')]
        meta = open(f + '.meta', encoding='utf-8').read()
        guid = re.search(r'guid: (\w+)', meta).group(1)
        roster[base] = dict(base=base, grade=int(gm.group(1)), dps=ap * aspd,
                             claimed=base in gen_gate.CLAIMED_BASES, guid=guid, path=f)
    return roster


def build_master_uid_map():
    """PRIORITY: 06번① > 게이트/회수 > 1채널 > 2채널. returns uid -> roster_base."""
    entries = defaultdict(list)

    roster_texts = {f: open(f, encoding='utf-8').read() for f in glob.glob(f'{ROSTER_DIR}/*.asset')}

    for f in glob.glob('Assets/Data/UnitSkills/SkillData_원작0*.asset'):
        fname = f.split('/')[-1]
        m = re.match(r'SkillData_원작\d+_([Hh]0[0-9A-Za-z]{2})\.asset$', fname)
        if not m:
            continue
        uid = m.group(1)
        meta = open(f + '.meta', encoding='utf-8').read()
        guid = re.search(r'guid: (\w+)', meta).group(1)
        for rf, rtext in roster_texts.items():
            if guid in rtext:
                entries[uid].append((rf.split('/')[-1][:-6], '06번①'))

    header_re = re.compile(r'원작\s+\S[^()]{0,60}?\(([Hh]0[0-9A-Za-z]{2})\)')
    for f in glob.glob('Assets/Data/UnitSkills/SkillData_게이트_*.asset') + \
             glob.glob('Assets/Data/UnitSkills/SkillData_회수_*.asset'):
        text = open(f, encoding='utf-8').read()
        desc = re.search(r'^  description: (.*)$', text, re.M)
        if not desc:
            continue
        base_fname = f.split('/')[-1]
        m = re.match(r'SkillData_(?:게이트|회수)_(.+)_[0-9a-f]{8}\.asset$', base_fname)
        if not m:
            continue
        roster_base = m.group(1)
        for hm in header_re.finditer(desc.group(1)):
            entries[hm.group(1)].append((roster_base, '게이트/회수'))

    by_grade1, _, _ = gen1.load_csv_rows()
    roster1 = gen1.load_roster()
    for gname, genum in gen1.GRADE_ENUM.items():
        ability_rows = by_grade1.get(gname, [])
        eligible = sorted([u for u in roster1 if u['grade'] == genum and not u['claimed']],
                           key=lambda u: -u['dps'])
        n = min(len(ability_rows), len(eligible))
        for i in range(n):
            uid = ability_rows[i]['유닛ID']
            base = eligible[i]['path'].split('/')[-1][:-6]
            entries[uid].append((base, '1채널'))

    mapping2 = gen_regroup.snapshot_current_mapping()
    for uid, bases in mapping2.items():
        for b in bases:
            entries[uid].append((b, '2채널'))

    PRIORITY = ['06번①', '게이트/회수', '1채널', '2채널']
    resolved = {}
    for uid, lst in entries.items():
        for ch in PRIORITY:
            hit = next((b for b, c in lst if c == ch), None)
            if hit:
                resolved[uid] = (hit, ch)
                break
    return resolved


def parse_original_recipes():
    rows = list(csv.DictReader(open('Docs/reference/ORIGINAL_HIDDEN_RECIPES.csv', encoding='utf-8-sig')))
    recipes = []
    for r in rows:
        # ⚠️ "재료" 열은 대부분 uid×1이지만 Hidden_carrot의 h010×2처럼 같은 유닛을
        # 2개 요구하는 예외가 1건 있다 — count를 그대로 살려서 옮긴다(하드코딩 1 금지).
        uid_counts = [(u.split('×')[0], int(u.split('×')[1])) for u in r['재료'].split()]
        names = r['재료이름'].split('  ')
        materials = []
        for (uid, cnt), nm in zip(uid_counts, names):
            grade = nm.rsplit(' - ', 1)[1].split('×')[0] if ' - ' in nm else None
            cname = nm.rsplit(' - ', 1)[0] if ' - ' in nm else nm
            materials.append(dict(uid=uid, name=cname, grade=grade, count=cnt))
        power = sum(TIER.get(m['grade'], 0) * m['count'] for m in materials)
        recipes.append(dict(trigger=r['트리거'], chat=r['채팅문구'], materials=materials, power=power))
    return recipes


def effect_check(recipe_path):
    pass


def main():
    roster = load_roster()
    uid_map = build_master_uid_map()

    original_recipes = parse_original_recipes()
    original_recipes.sort(key=lambda r: -r['power'])

    hidden_units = sorted([b for b, u in roster.items() if u['grade'] == 4],
                           key=lambda b: -roster[b]['dps'])
    print(f'원작 히든 레시피 {len(original_recipes)}종, 우리 히든 유닛 {len(hidden_units)}종')

    n = min(len(original_recipes), len(hidden_units))
    dropped_originals = original_recipes[n:]
    pairs = list(zip(original_recipes[:n], hidden_units[:n]))

    # 재료 번역 — 등급별 fallback 배정용 유닛 풀(claimed 제외, 이미 다른 재료로 쓰인 것 제외)
    used_bases = set(uid_map[u] for u in uid_map)  # 이미 확보된 대응(스킬 배정 채널)들
    grade_pool = defaultdict(list)
    for b, u in roster.items():
        if u['claimed']:
            continue
        grade_pool[u['grade']].append(b)
    for g in grade_pool:
        grade_pool[g].sort(key=lambda b: -roster[b]['dps'])

    unmapped_report = []
    used_material_bases = set()
    # ⚠️ 원작 재료 uid 하나가 레시피 여러 개에 나올 수 있다(h010 압살롬 — Hidden_carrot·
    # Hidden_perona 둘 다 필요). fallback으로 배정한 결과를 uid별로 캐시해두지 않으면
    # 같은 원작 유닛이 레시피마다 다른 로스터 유닛으로 번역돼(칼 때마다 pool에서 새로
    # 뽑으니) 정합성이 깨진다 — uid_map처럼 "같은 원작 유닛 = 같은 로스터 유닛"을
    # fallback에도 강제한다.
    fallback_cache = {}

    def resolve_material(uid, grade_name, char_name):
        if uid == 'h060':
            return '해적선', '원작 그대로(재화형 재료)'
        if uid == 'h08B':
            return None, '확장팩 자리 — 실존 캐릭터 아님, 못 채움'
        if uid in uid_map:
            base, ch = uid_map[uid]
            return base, ch
        if uid in fallback_cache:
            return fallback_cache[uid]
        # 등급 안 순위 fallback — 이미 골라진 후보는 건너뛴다
        genum = GRADE_NAME_TO_ENUM.get(grade_name)
        pool = [b for b in grade_pool.get(genum, []) if b not in used_material_bases]
        if not pool:
            result = (None, f'{grade_name} 등급 가용 로스터 소진')
            fallback_cache[uid] = result
            return result
        chosen = pool[0]
        result = (chosen, '등급 안 순위 배정(신호 없음, 원작 표 등장순)')
        fallback_cache[uid] = result
        return result

    recipe_files_written = []
    translation_report = []

    for orig, result_base in pairs:
        ingredients = []
        row_report = []
        incomplete = False
        for m in orig['materials']:
            base, source = resolve_material(m['uid'], m['grade'], m['name'])
            row_report.append((m['uid'], m['name'], m['grade'], base, source, m['count']))
            if base is None:
                incomplete = True
                continue
            used_material_bases.add(base)
            ingredients.append((base, m['count']))

        translation_report.append(dict(trigger=orig['trigger'], chat=orig['chat'],
                                        result=result_base, materials=row_report,
                                        incomplete=incomplete))

        if incomplete:
            continue  # 비워두고 목록에만 남긴다(추측 금지)

        # CombineRecipe 자산 작성
        result_guid = roster[result_base]['guid']
        ing_blocks = []
        for ib, cnt in ingredients:
            ig = roster[ib]['guid']
            ing_blocks.append(
                "  - kind: 0\n"
                f"    unit: {{fileID: 11400000, guid: {ig}, type: 2}}\n"
                "    item: {fileID: 0}\n"
                "    wildcardGrade: 0\n"
                f"    count: {cnt}\n"
            )
        name = f'히든_{result_base[len("히든_"):]}' if result_base.startswith('히든_') else f'히든_{result_base}'
        path = f'{RECIPE_DIR}/{name}.asset'
        guid = guid_for(name)
        body = (
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n"
            "MonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {RECIPE_SCRIPT_GUID}, type: 3}}\n"
            f"  m_Name: {name}\n  m_EditorClassIdentifier:\n"
            f"  commandId: {orig['chat']}\n"
            f"  result: {{fileID: 11400000, guid: {result_guid}, type: 2}}\n"
            "  ingredients:\n" + ''.join(ing_blocks) +
            "  goldCost: 0\n  resourceCosts: []\n  minRound: 0\n  maxRound: 0\n  requiredSaveCount: 0\n"
        )
        open(path, 'w', encoding='utf-8').write(body)
        write_meta(path, guid)
        recipe_files_written.append(name)

    # ── 보고 ──────────────────────────────────────────────────────────────
    print(f'\n레시피 작성/갱신: {len(recipe_files_written)}종')
    print(f'드롭된 원작 레시피(로스터 부족): {[r["trigger"] for r in dropped_originals]}')

    incomplete_recipes = [r for r in translation_report if r['incomplete']]
    print(f'재료 번역 실패로 비워둔 레시피: {len(incomplete_recipes)}종')
    for r in incomplete_recipes:
        missing = [(u, n, g) for u, n, g, b, s, c in r['materials'] if b is None]
        print(f"  {r['trigger']} ({r['chat']}) -> {r['result']} : 못 채운 재료 {missing}")

    fallback_used = [r for rp in translation_report for r in rp['materials'] if r[4] and '등급 안 순위' in r[4]]
    print(f'\n등급 안 순위로 fallback 배정된 재료: {len(fallback_used)}건')

    with open('/tmp/hidden_recipe_translation_report.json', 'w', encoding='utf-8') as fh:
        json.dump(translation_report, fh, ensure_ascii=False, indent=1)
    with open('/tmp/hidden_recipe_dropped.json', 'w', encoding='utf-8') as fh:
        json.dump([r['trigger'] for r in dropped_originals], fh, ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()
