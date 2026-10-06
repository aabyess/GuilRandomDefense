#!/usr/bin/python3
# 전설 스킬 「중복 5기」(Docs/research/LEGEND_SKILL_PLAN_2026-10-06.md §1) — 스탯 쌍둥이가 이미 다른 로스터에 반영해 둔 원작 몫을 복제해 붙인다.
# 복제 = SkillData 에셋 파일 복사(새 GUID·새 이름) + SkillVfxTable 항목 복사 + 유닛 필드(평타 타입·크리 0·스플래시·게이지) 정리. 다시 돌려도 안전(있으면 건너뜀).
import re, os, sys, uuid, glob, shutil

SK = 'Assets/Data/UnitSkills/'
RO = 'Assets/Data/Units/Roster/'
VFX = 'Assets/Resources/Effects/SkillVfxTable.asset'

def guid_of(path):
    return re.search(r'guid: (\w+)', open(path + '.meta', encoding='utf8').read()).group(1)

# 받는 유닛, 주는 유닛(키), 복제할 스킬(주는 유닛 에셋 이름 중 SkillData_ 뒤), 유닛 필드
JOBS = [
    ('김건', '임채현', ['게이트_전설적인_임채현_03847fe6', '원작오라_전설적인_임채현_A0FB', '원작트리거_전설적인_임채현_Legend35ulti_trg3'],
        {'attackType': 3, 'attackSplashRadius': 275, 'lifeGaugeMax': 35, 'lifeGaugeStart': 35, 'lifeGaugeRegenPerSecond': 0.5}),   # 울티 h03S: siege · ua1f 275 · LIFE 35
    ('김민규', '이일중', ['원작능력_전설적인_이일중', '원작트리거_전설적인_이일중_Legend30_스턴'],
        {'attackType': 1}),                                                                                                    # 시노부 h042: normal
    ('김민준', '진연서', ['게이트_전설적인_진연서_483e0464', '게이트_전설적인_진연서_Legend32_1of7', '게이트_전설적인_진연서_Legend32_every',
                         '원작오라_전설적인_진연서_A0Y1', '원작오라_전설적인_진연서_A0YT'],
        {'attackType': 1, 'attackSplashRadius': 325, 'lifeGaugeMax': 33, 'lifeGaugeStart': 33, 'lifeGaugeRegenPerSecond': 0.5, 'lifeGaugeCustomHitGain': 1, 'lifeGaugeHitGain': 0}),  # 네코마무시 h09Z: normal · LIFE 33
    ('김정래', '임장혁', ['게이트_전설적인_임장혁_3a13fd3d', '원작오라_전설적인_임장혁_A0UF'],
        {'attackType': 1}),                                                                                                    # 토키 h087: normal
    ('박병규', '임장혁', ['원작능력_전설적인_임장혁', '더미채널_전설적인_임장혁_1', '원작오라_전설적인_임장혁_A0DZ'],
        {'attackType': 1}),                                                                                                    # 에이스 h02O: normal
]
STRAY = ['더미채널_전설적인_김건_1', '더미채널_전설적인_박병규_1']

def set_field(text, key, value):
    pat = re.compile(r'^(  %s: ).*$' % re.escape(key), re.M)
    assert pat.search(text), 'no field ' + key
    return pat.sub(lambda m: m.group(1) + (('%g' % value) if not isinstance(value, int) else str(value)), text, count=1)

vfx = open(VFX, encoding='utf8').read()

def vfx_block(guid):
    m = re.search(r'^  - skill: \{fileID: 11400000, guid: %s, type: 2\}\n(?:    .*\n)+' % guid, vfx, re.M)
    return m.group(0) if m else None

report = []
# 남의 블록 찌꺼기 먼저 지운다(같은 이름의 복제가 뒤에 생길 수 있다)
for s in STRAY:
    p = SK + 'SkillData_' + s + '.asset'
    if os.path.exists(p):
        g = guid_of(p)
        blk = vfx_block(g)
        if blk: vfx = vfx.replace(blk, '', 1)
        os.remove(p); os.remove(p + '.meta')
        report.append('삭제 ' + s)
for recipient, donor, assets, fields in JOBS:
    new_guids = []
    for name in assets:
        src = SK + 'SkillData_' + name + '.asset'
        newname = name.replace('전설적인_' + donor, '전설적인_' + recipient)
        dst = SK + 'SkillData_' + newname + '.asset'
        if not os.path.exists(dst):
            text = open(src, encoding='utf8').read()
            text = re.sub(r'^  m_Name: .*$', '  m_Name: SkillData_' + newname, text, count=1, flags=re.M)
            open(dst, 'w', encoding='utf8').write(text)
            meta = open(src + '.meta', encoding='utf8').read()
            g_new = uuid.uuid4().hex
            meta = re.sub(r'guid: \w+', 'guid: ' + g_new, meta, count=1)
            open(dst + '.meta', 'w', encoding='utf8').write(meta)
            blk = vfx_block(guid_of(src))
            if blk:
                nb = blk.replace(guid_of(src), g_new, 1)
                vfx = vfx.replace(blk, blk + nb, 1) if 'guid: ' + g_new not in vfx else vfx
            report.append('복제 ' + newname)
        new_guids.append(guid_of(dst))
    up = RO + '전설적인_' + recipient + '.asset'
    u = open(up, encoding='utf8').read()
    skills = ''.join('  - {fileID: 11400000, guid: %s, type: 2}\n' % g for g in new_guids)
    u, n = re.subn(r'^  skills:.*\n(?:  - .*\n)*', '  skills:\n' + skills, u, count=1, flags=re.M)
    assert n == 1, recipient
    fields = dict(fields); fields.update({'critChance': 0, 'critBonusDamage': 0})
    for k, v in fields.items(): u = set_field(u, k, v)
    open(up, 'w', encoding='utf8').write(u)
    report.append('유닛 ' + recipient + ': 스킬 %d · 필드 %s' % (len(new_guids), sorted(fields)))

open(VFX, 'w', encoding='utf8').write(vfx)
print('\n'.join(report))
