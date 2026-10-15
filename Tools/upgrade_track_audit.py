# 로스터 유닛 ↔ 원작 연구(upgr) 대조표: 등급 트랙·공격타입 자식·유닛 전용 연구가 우리 UnitUpgrades와 같은지. 저장소 루트에서: python3 -I Tools/upgrade_track_audit.py
import sys,csv,re,collections
sys.path.insert(0,'Tools/w3x'); import w3u,w3q
U={u['id'].upper():u for u in w3u.parse('Tools/w3x/원본/war3map_new.w3u')}
Q={}
for q in w3q.parse('Tools/w3x/원본/war3map.w3q'):
    m={}
    for f,l,v in q['mods']: m.setdefault(f,v)
    Q[q['id']]=m
TYPE_CHILD={'R01K':('일반',1,1500),'R01L':('일반',1,2000),'R01M':('일반',1,3000),'R01N':('공성',3,3000),'R01O':('공성',3,1500),'R01P':('공성',3,2000),
            'R01Q':('관통',2,3000),'R01R':('관통',2,2000),'R01S':('관통',2,1500),'R01T':('패기',4,4000),'R01U':('패기',4,3000),'R01W':('패기',4,2000)}
GRADE_RES={'희귀함':'R008','특별함':'R00E','전설적인':'R000','히든':'R000','제한':'R004','특수함':'R004','초월':'R003','불멸':'R002','랜덤':'R019','다른세계':'R019','흔함':'R00F','안흔함':'R00F'}
ENUM={0:'Common',1:'Uncommon',2:'Special',3:'Rare',4:'Hidden',5:'Legendary',6:'Limited',7:'Transcendent',8:'Immortal',9:'Eternal',10:'RandomUnit',11:'OtherWorld',12:'Superior'}
TIER={'Legendary':1500,'Hidden':1500,'RandomUnit':1500,'OtherWorld':1500,'Limited':2000,'Superior':2000,'Transformed':2000,'Transcendent':3000,'Immortal':3000,'Eternal':3000}
def total(r,L):  # (공속 증가율, 공격 가산) 레벨 L
    if L<=0 or r is None: return 0,0
    return (r.get('gba1',0)+r.get('gmo1',0)*(L-1)), (r.get('gba2',0)+r.get('gmo2',0)*(L-1))
rows=list(csv.DictReader(open('Docs/reference/SKILL_STAT_SOURCE_AUDIT_2026-10-15.csv',encoding='utf-8-sig')))
out=[]
for r in rows:
    g=r['등급']; src=(r['스탯 원작유닛(ID 이름)'].split(' ')[0].upper() if r['스탯 원작유닛(ID 이름)'] else '') or r['csv 매핑ID']
    u=U.get(src)
    if not u: continue
    up=[x for x in (u['mods'].get('upgr') or '').split(',') if x]
    try: t=open('Assets/Data/Units/Roster/%s.asset'%r['로스터'],encoding='utf-8').read()
    except: continue
    gr=ENUM.get(int(re.search(r'^  grade: (\d+)',t,re.M).group(1)),'?')
    at=int(re.search(r'^  attackType: (\d+)',t,re.M).group(1))
    child=[x for x in up if x in TYPE_CHILD]; unit_res=[x for x in up if x not in TYPE_CHILD and x!=GRADE_RES.get(g)]
    note=[]
    gres=[x for x in up if x not in TYPE_CHILD]
    if gres and GRADE_RES.get(g) and gres[0]!=GRADE_RES[g]:
        for x in gres:
            if x!=GRADE_RES[g]: note.append('전용연구 %s %s'%(x,Q.get(x,{}).get('gnam','')))
    for c in child:
        nm,ty,val=TYPE_CHILD[c]
        if ty!=at: note.append('타입불일치 원작 %s(%s) ↔ 우리 attackType %d'%(c,nm,at))
        if TIER.get(gr,0)!=val: note.append('가산 원작 %s=%d ↔ 우리 %s=%d'%(c,val,gr,TIER.get(gr,0)))
    # 레벨 21 비교(전용 연구가 있으면 등급 트랙 대신)
    orig=gres[0] if gres else None
    ours=GRADE_RES.get(g)
    row=dict(등급=g,로스터=r['로스터'],원작=src,upgr=','.join(up),우리등급=gr,우리타입=at,메모=' / '.join(note))
    if orig and ours and orig!=ours and orig in Q:
        for L in (1,10,21):
            sp0,d0=total(Q[ours],L); sp1,d1=total(Q[orig],L)
            row['L%d_공속원작/우리'%L]='%.2f/%.2f'%(sp1,sp0); row['L%d_가산원작/우리'%L]='%d/%d'%(d1,d0)
    out.append(row)
cols=['등급','로스터','원작','upgr','우리등급','우리타입','메모','L1_공속원작/우리','L1_가산원작/우리','L10_공속원작/우리','L10_가산원작/우리','L21_공속원작/우리','L21_가산원작/우리']
w=csv.DictWriter(open('Docs/reference/UPGRADE_TRACK_AUDIT_2026-10-16.csv','w',encoding='utf-8-sig',newline=''),fieldnames=cols); w.writeheader(); w.writerows(out)
c=collections.Counter(); 
for o in out:
    c[(o['등급'],'전용연구' if '전용연구' in o['메모'] else ('타입/가산' if o['메모'] else 'OK'))]+=1
for k,v in sorted(c.items()): print(k,v)
