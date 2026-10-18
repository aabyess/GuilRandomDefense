# apply_0327_skills.py가 exec로 읽는 변경 목록(0.3.27 유지 56기 정밀 대조 — 값은 S2 2.323 툴팁, ~/grd/GRD_rematch/유지143_정밀대조.xlsx 「0.3.27 적용 후보」 + 옛 크리 8개 N→N−1)
import rematch_s2 as R
import rematch_s2_data as D
_U, _AB, _AS, _US = D.load_s2()
def aname(aid): return R.ability_view(aid, _AB, _AS)['name']
GROUP = {}   # 파일 경로 → 커밋 묶음
def grp(p, g): GROUP[p] = g

def E(unit, skill, g, **kw):
    p, t = edit(unit, skill, **kw); grp(p, g)

def N(unit, aid, g, blk, caster=False, atk_type=1, idx=1):
    sname = aname(aid)
    desc = 'S2 2.323 원작 능력 %s(%s) — 툴팁에 있는데 우리에 없던 효과(0.3.27 유지 대조에서 추가)' % (sname, aid)
    name, body, meta = new_skill(unit, aid, sname, desc, blk, atk_type, caster, idx)
    pa = os.path.join(SK, name + '.asset'); NEWFILES[pa] = body; NEWFILES[pa + '.meta'] = meta
    grp(pa, g); grp(pa + '.meta', g)
    pr, _ = add_to_roster(unit, A.guid_of(name)); grp(pr, g)
    LOG.append(dict(unit=unit, skill=name, before='(없음)', after=summary(body), kind='새 스킬 「%s」' % sname))

def RM(unit, skill, g):
    pr, _ = remove_from_roster(unit, skill); grp(pr, g)
    sp = os.path.join(SK, 'SkillData_%s.asset' % skill)
    LOG.append(dict(unit=unit, skill=skill, before=summary(STORE.get(sp) or rd(sp)), after='(제거 — S2 이 유닛 툴팁에 없음)', kind='제거'))
    DELETE.append(sp); DELETE.append(sp + '.meta')

D_, M_, F_ = 2, 1, 1   # damageType 마법 2 · 물리 1
# ── 안흔함·특별함
g = '안흔함·특별함'
E('안흔함_이호준', '원작능력_안흔함_이호준', g, effects={1: {'duration': 0.3}})
E('특별함_양재모', '원작능력_특별함_양재모', g, level={'triggerChance': 1}, effects={0: {'basis': 0, 'multiplier': 150, 'damageType': 2}})
E('특별함_유재헌', '원작능력_특별함_유재헌', g, level={'triggerChance': 0.7}, effects={0: {'multiplier': 0.5}})
E('특별함_임장혁', '원작능력_특별함_임장혁', g, effects={0: {'multiplier': 1}})
E('특별함_정승준', '원작능력_특별함_정승준', g, effects={0: {'multiplier': 1}, 1: {'duration': 0.25}})
# ── 희귀함
g = '희귀함'
E('희귀함_강주혁', '원작오라_희귀함_강주혁_A064', g, level={'range': 600}, effects={0: {'multiplier': 0.8}})
E('희귀함_고어진', '원작능력_희귀함_고어진_A0D9', g, level={'range': 700})
E('희귀함_김만경', '원작능력_희귀함_김만경', g, effects={0: {'multiplier': 1}, 1: {'duration': 2.5}})
N('희귀함_김청운', 'A12F', g, block(0, 1.0, 400.0, effects=[eff(0, 2, 1500.0, damageType=2)]))
E('희귀함_노수신', '원작능력_희귀함_노수신', g, level={'triggerChance': 0.145})
E('희귀함_노수신', '원작오라_희귀함_노수신_A0N4', g, level={'range': 900})
E('희귀함_두유찬', '원작오라_희귀함_두유찬_A0K5', g, effects={0: {'multiplier': 0.85}})
N('희귀함_두유찬', 'A03U', g, block(0, 1.0, 215.0, effects=[eff(0, 2, 2500.0, damageType=2)]))
E('희귀함_박기찬', '원작능력_희귀함_박기찬', g, level={'triggerChance': 0.2}, effects={0: {'bonus': 60000, 'damageType': 2}})
N('희귀함_박민수', 'A0F0', g, block(0, 0.08, 600.0, effects=[eff(0, 2, 3000.0, damageType=1)]), caster=True)
E('희귀함_박수찬', '더미채널_희귀함_박수찬_1', g, level={'triggerChance': 0.1, 'range': 700}, effects={0: {'multiplier': 8000, 'damageType': 2}})
N('희귀함_박수찬', 'A02E', g, block(0, 0.12, 0.0, effects=[eff(0, 3, 10000.0, damageType=2)]))
E('희귀함_박은석', '더미채널_희귀함_박은석_1', g, level={'triggerChance': 0.15})
E('희귀함_배성령', '원작능력_희귀함_배성령', g, level={'triggerChance': 0.1, 'range': 450})
E('희귀함_서민성', '원작능력_희귀함_서민성', g, level={'triggerChance': 0.13})
E('희귀함_선효진', '원작능력_희귀함_선효진', g, level={'triggerChance': 0.07}, effects={0: {'multiplier': 10000, 'randMin': 0.5, 'randMax': 2}})
E('희귀함_엄태웅', '원작능력_희귀함_엄태웅', g, level={'triggerChance': 0.15}, effects={1: {'multiplier': 5000, 'randMin': 1, 'randMax': 20}})
E('희귀함_유재헌', '더미채널_희귀함_유재헌_1', g, effects={0: {'multiplier': 7500}})
E('희귀함_이승우', '더미채널_희귀함_이승우_1', g, effects={0: {'multiplier': 10000, 'maxTargets': 10}})
E('희귀함_이재윤', '더미채널_희귀함_이재윤_1', g, level={'triggerChance': 0.07}, effects={1: {'duration': 1.4}})
E('희귀함_정내연', '더미채널_희귀함_정내연_1', g, level={'triggerChance': 0.08})
N('희귀함_정내연', 'A0EV', g, block(0, 0.08, 450.0, effects=[eff(0, 2, 10000.0, damageType=2)]))
# ── 히든·변화됨
g = '히든·변화됨'
E('히든_최윤서', '게이트_히든_최윤서_3c5f8bd9', g, level={'hitCountThreshold': 95},
  effects={0: {'basis': 13, 'multiplier': 0.04, 'bonus': 3000000, 'randMin': 1, 'randMax': 1}, 1: {'duration': 1.5}, 2: {'duration': 1.5}})
E('히든_최윤서', '게이트_히든_최윤서_b8d2fd85', g, effects={0: {'multiplier': 300000}})
N('히든_최윤서', 'A12K', g, block(0, 1.0, 450.0, effects=[eff(0, 2, 8500.0, damageType=2)]))
E('히든_호치킨', '게이트_히든_호치킨_Hidden5_1of7', g, level={'triggerChance': 0.16},
  replace=[eff(0, 3, 250000.0, damageType=2), eff(0, 3, 2500000.0, damageType=2, targetCondition=3, targetConditionValue=200),
           eff(0, 3, 0.18, basis=1, damageType=2, fixedDamage=True, requiredTargetBuffId='B06B'), eff(1, 3, 0, duration=2.75)])
E('히든_황정기', '게이트_히든_황정기_2251ce1a', g, level={'triggerChance': 0.15},
  effects={0: {'basis': 0, 'multiplier': 625000, 'bonus': 0, 'randMin': 1, 'randMax': 1, 'damageType': 2}, 1: {'multiplier': 625000, 'fixedDamage': 1}})
E('히든_황정기', '게이트_히든_황정기_2a646778', g,
  replace=[eff(0, 2, 1000000.0, damageType=2), eff(0, 2, 3600000.0, damageType=2, fixedDamage=True), eff(1, 2, 0, duration=1.25)])
E('히든_황정기', '원작능력_히든_황정기_A0II', g, level={'triggerChance': 0.5}, effects={0: {'duration': 0.75}})
E('변화됨_강재규', '원작능력_변화됨_강재규', g, level={'range': 425})
# ── 전설
g = '전설'
for s in ('원작오라_전설적인_김용태_A0SX', '원작오라_전설적인_김용태_A0ST', '원작오라_전설적인_김용태_A04K'): RM('전설적인_김용태', s, g)
N('전설적인_김용태', 'A09K', g, block(2, 1.0, 0.0, effects=[eff(KIND['AttackPowerBuffFlat'], 0, 30000.0, buffId='A09KF')]))
E('전설적인_노태현', '원작능력_전설적인_노태현_A0DG', g, effects={0: {'multiplier': 3333}})
E('전설적인_박민석', '원작능력_전설적인_박민석', g, effects={0: {'multiplier': 9}})
E('전설적인_박민석', '원작오라_전설적인_박민석_A0YO', g, effects={0: {'multiplier': -20}})
E('전설적인_박민석', '원작능력_전설적인_박민석_A0VW', g, effects={0: {'duration': 20}})
E('전설적인_박성호', '원작능력_전설적인_박성호', g, level={'triggerChance': 0.3, 'range': 500}, effects={0: {'multiplier': 450000, 'randMin': 1, 'randMax': 1, 'damageType': 2}})
N('전설적인_박성호', 'A0DU', g, block(0, 1.0, 375.0, effects=[eff(0, 2, 0.5, basis=3, damageType=2, explosive=True)]))
E('전설적인_박성호', '원작능력_전설적인_박성호_A052', g, effects={1: {'duration': 1.0}}, remove=[0])
E('전설적인_박은석', '원작능력_전설적인_박은석', g, effects={1: {'multiplier': 0.02, 'randMin': 1, 'randMax': 1, 'fixedDamage': 1}, 2: {'multiplier': 0.12, 'fixedDamage': 1}})
E('전설적인_박은석', '원작능력_전설적인_박은석_A0GH', g, effects={1: {'duration': 0.8}})
E('전설적인_백기현', '원작능력_전설적인_백기현', g, level={'triggerChance': 0.11}, effects={1: {'duration': 2.2}})
E('전설적인_백기현', '원작능력_전설적인_백기현_A03X', g, effects={0: {'multiplier': 9}})
E('전설적인_신지우', '원작능력_전설적인_신지우', g, effects={0: {'bonus': 0}, 1: {'bonus': 175000}, 2: {'bonus': 87500}})
E('전설적인_신지우', '원작트리거_전설적인_신지우_Legend16sc_분해', g, level={'triggerChance': 0.045}, effects={0: {'bonus': 285000}, 1: {'bonus': 142500}})
E('전설적인_이유선', '원작능력_전설적인_이유선', g, level={'range': 600}, effects={0: {'multiplier': 245000}, 1: {'multiplier': 551250, 'randMin': 1, 'randMax': 1, 'damageType': 1}})
E('전설적인_이유선', '원작트리거_전설적인_이유선_Legend8_인의', g, level={'range': 900}, effects={0: {'multiplier': 0.3}, 1: {'multiplier': 0.3}},
  add=[eff(KIND['ArmorBonus'], 2, -20.0, duration=6.0, buffId='A074D')])
N('전설적인_이유선', 'A072', g, block(2, 1.0, 800.0, effects=[eff(KIND['Slow'], 2, 0.8)]))
E('전설적인_이재윤', '원작능력_전설적인_진연서', g, level={'range': 600}, effects={0: {'randMin': 1, 'randMax': 1, 'damageType': 2}},
  add=[eff(0, 2, 1000000.0, damageType=1, randMin=1.0, randMax=1.5), eff(1, 2, 0, duration=3.0)])
RM('전설적인_이재윤', '원작능력_전설적인_진연서_Legend7_stomp', g)
E('전설적인_이재윤', '원작능력_전설적인_진연서_A0EN', g, level={'range': 325}, effects={0: {'basis': 0, 'target': 2, 'multiplier': 70000, 'bonus': 0, 'damageType': 1}})
RM('전설적인_이재윤', '원작오라_전설적인_진연서_A03O', g)
E('전설적인_이재윤', '원작오라_전설적인_진연서_A0ER', g, effects={0: {'multiplier': -20}})
# ── 초월·랜덤·특수함
g = '초월·랜덤·특수함'
N('초월_김경현_AP', 'A0F2', g, block(0, 0.35, 400.0, effects=[eff(0, 2, 70000.0, damageType=1, randMin=1.0, randMax=2.0)]))
E('랜덤_야사카_카나코', '원작능력_랜덤_야사카_카나코', g, effects={0: {'multiplier': 2}})
E('특수함_장진희', '원작능력_특수함_장진희', g, effects={0: {'multiplier': 7, 'bonus': 0}},
  add=[eff(1, 3, 0, duration=0.8), eff(0, 3, 20000.0, damageType=1, fixedDamage=True, targetCondition=1, targetConditionValue=200),
       eff(0, 3, 0.009, basis=2, damageType=1, fixedDamage=True, targetCondition=3, targetConditionValue=200)])

if mode == 'apply':
    for p, t in list(STORE.items()): wr(p, t)
    for p, t in NEWFILES.items(): wr(p, t)
    import subprocess
    for p in DELETE:
        if os.path.exists(p): os.remove(p)
json.dump(dict(log=LOG, group=GROUP, delete=DELETE), open(os.path.expanduser('~/grd/GRD_rematch/적용_0327_로그.json'), 'w'), ensure_ascii=False, indent=1)
print(mode, '수정/신규/제거', len(LOG), '파일', len(STORE), '새 파일', len(NEWFILES) // 2, '삭제', len(DELETE) // 2)
