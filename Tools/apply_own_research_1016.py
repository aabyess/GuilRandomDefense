"""강화소 원작 대조(10-16) ② 유닛 전용 등급 연구 트랙 + 랜덤·다른세계 공격타입 자식 가산 버킷 — 구현담당1.
전용 연구: 원작 w3u upgr에 등급 연구 대신 개별 연구(R02S 등)가 붙은 유닛 → UnitUpgradeTrackData(구매 칸 없음) + UnitData.ownGradeResearch.
  레벨은 등급 트랙을 따라간다(원작 Trig_upgrade_u가 R000/R002/R003/R004를 살 때 같이 +1). 값은 w3q gba1/gmo1(공속)·gba2/gmo2(ratx 가산).
버킷: 랜덤·다른세계 중 원작 자식 연구(R01K~R01W) 가산이 우리 등급 묶음(1500)과 다른 유닛에 attackTypeBonusPerLevel = 원작 gba2.
짝 판정: 스탯 출처가 값일치 또는 csv 확정(SKILL_STAT_SOURCE_AUDIT). 제외: R017 우솝 공학1(JASS 어디서도 안 올라 레벨 0), R02T 이치고 공격(수치 없는 플래그).
"""
import sys,re,csv,hashlib,os
sys.path.insert(0,'Tools/w3x'); import w3q,w3u
Q={}
for q in w3q.parse('Tools/w3x/원본/war3map.w3q'):
    m={}
    for f,l,v in q['mods']: m.setdefault(f,v)
    Q[q['id']]=m
PAIRS=[('전설적인_김건','R02S','울티'),('전설적인_임채현','R02S','울티'),('전설적인_이승우','R01J','라분'),('전설적인_정윤식','R02V','모리아'),('전설적인_정준영','R01Z','조로'),
 ('제한_이충민','R022','카타쿠리'),('제한_임준성','R02Q','킹'),('제한_최영민','R006','레베카'),
 ('초월_김건_AP','R02W','야마토'),('초월_김민준_AP','R005','타시기'),('초월_두유찬_AD','R00D','사보'),('초월_이태훈_AP','R00C','후지토라'),('초월_최상호_AD','R02J','징베'),
 ('히든_이요한','R01Y','미호크')]
HEAD='''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: af9413764dc9047d893ae0a4d3fd02fa, type: 3}
'''
def yq(s): return '"'+''.join(c if ord(c)<128 else '\\u%04X'%ord(c) for c in s)+'"'
def track(rid,who):
    q=Q[rid]; name='UnitUpgrade_전용_%s_%s'%(rid,who)
    guid=hashlib.md5(('guilrd/전용연구1016/'+name).encode()).hexdigest()
    desc='원작 연구 %s(%s) — %s 한 종 전용 등급 연구. 레벨은 등급 연구를 따라가고(원작 Trig_upgrade_u) 값만 이 연구 것: 공속 gba1 %.2f·gmo1 %.2f · 가산 gba2 %d·gmo2 %d. 2026-10-16 구현담당1.'%(rid,q.get('gnam'),who,q.get('gba1',0),q.get('gmo1',0),q.get('gba2',0),q.get('gmo2',0))
    body=HEAD+('  m_Name: %s\n  m_EditorClassIdentifier: \n  trackName: %s\n  description: %s\n  targetGrades: \n  maxLevel: %d\n  costBase: 0\n  costGrowthPerLevel: 0\n'
          '  statLevel1Multiplier: %s\n  statGrowthPerLevel: %s\n  slotColor: {r: 1, g: 1, b: 1, a: 1}\n  targetUnit: {fileID: 0}\n  hasOriginalResearch: 1\n  statLevel1Bonus: %s\n  statBonusGrowthPerLevel: %s\n')%(
        yq(name),yq('%s %s(전용)'%(who,rid)),yq(desc),int(q.get('glvl',21)),
        repr(round(q.get('gba1',0),4)),repr(round(q.get('gmo1',0),4)),repr(float(q.get('gba2',0))),repr(float(q.get('gmo2',0))))
    p='Assets/Data/UnitUpgrades/%s.asset'%name
    open(p,'w',encoding='utf-8').write(body)
    open(p+'.meta','w',encoding='utf-8').write('fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'%guid)
    return guid
def set_tail(ro,k,v):
    p='Assets/Data/Units/Roster/%s.asset'%ro; t=open(p,encoding='utf-8').read()
    if re.search(r'(?m)^  %s: '%k,t): t=re.sub(r'(?m)^  %s: .*$'%k,lambda m:'  %s: %s'%(k,v),t,count=1)
    else: t=t.rstrip('\n')+'\n  %s: %s\n'%(k,v)
    open(p,'w',encoding='utf-8').write(t)
guids={}
for ro,rid,who in PAIRS:
    if (rid,who) not in guids: guids[(rid,who)]=track(rid,who)
    set_tail(ro,'ownGradeResearch','{fileID: 11400000, guid: %s, type: 2}'%guids[(rid,who)])
# 버킷
U={u['id'].upper():u for u in w3u.parse('Tools/w3x/원본/war3map_new.w3u')}
CH={'R01K':1500,'R01L':2000,'R01M':3000,'R01N':3000,'R01O':1500,'R01P':2000,'R01Q':3000,'R01R':2000,'R01S':1500,'R01T':4000,'R01U':3000,'R01W':2000}
n=0
for r in csv.DictReader(open('Docs/reference/SKILL_STAT_SOURCE_AUDIT_2026-10-15.csv',encoding='utf-8-sig')):
    if r['등급'] not in('랜덤','다른세계'): continue
    s=r['스탯 원작유닛(ID 이름)'].split(' ')[0].upper()
    u=U.get(s)
    if not u or not r['스탯출처방식'].startswith('값일치'): continue
    ch=[CH[x] for x in (u['mods'].get('upgr') or '').split(',') if x in CH]
    if ch and ch[0]!=1500:
        set_tail(r['로스터'],'attackTypeBonusPerLevel',ch[0]); n+=1; print('버킷',r['로스터'],s,ch[0])
print('전용 트랙',len(guids),'버킷',n)
