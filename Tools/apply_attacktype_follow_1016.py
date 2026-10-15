"""attackType을 원작 공격타입 연구 종류(upgr 자식 R01K~R01W: 일반·공성·관통·패기)에 맞춘다(사장님 10-16 「원작대로 바꾸기」) — 구현담당1.
대상: SKILL_STAT_SOURCE_AUDIT에서 스탯 출처가 값일치(=csv 포함)인 유닛. 제외: 값일치(csv와 다름)·상향 전·csv만·불명(짝 불일치/짝 없음).
사용: python3 -I Tools/apply_attacktype_follow_1016.py [--apply]  → Docs/reference/ATTACKTYPE_FOLLOW_2026-10-16.csv"""
import sys,csv,re,collections
sys.path.insert(0,'Tools/w3x'); import w3u
U={u['id'].upper():u for u in w3u.parse('Tools/w3x/원본/war3map_new.w3u')}
TC={'R01K':1,'R01L':1,'R01M':1,'R01N':3,'R01O':3,'R01P':3,'R01Q':2,'R01R':2,'R01S':2,'R01T':4,'R01U':4,'R01W':4}
NAME={1:'일반',2:'관통',3:'공성',4:'패기'}
DT={1:(1.0,1.25),2:(1.25,0.75),3:(0.75,1.0),4:(1.05,1.05)}   # (vsLarge 보스, vsNormal 일반몹) — DamageTable.asset
apply='--apply' in sys.argv
out=[]; excl=[]
for r in csv.DictReader(open('Docs/reference/SKILL_STAT_SOURCE_AUDIT_2026-10-15.csv',encoding='utf-8-sig')):
    s=r['스탯 원작유닛(ID 이름)'].split(' ')[0].upper() if r['스탯 원작유닛(ID 이름)'] else ''
    u=U.get(s)
    if not u: continue
    ch=[TC[x] for x in (u['mods'].get('upgr') or '').split(',') if x in TC]
    if not ch: continue
    p='Assets/Data/Units/Roster/%s.asset'%r['로스터']
    try: t=open(p,encoding='utf-8').read()
    except: continue
    cur=int(re.search(r'^  attackType: (\d+)',t,re.M).group(1))
    if ch[0]==cur: continue
    how=r['스탯출처방식']
    if how not in('값일치','값일치=csv'):
        excl.append((r['등급'],r['로스터'],s,how)); continue
    out.append((r['등급'],r['로스터'],s,cur,ch[0]))
    if apply:
        t=re.sub(r'(?m)^  attackType: \d+$','  attackType: %d'%ch[0],t,count=1); open(p,'w',encoding='utf-8').write(t)
w=csv.writer(open('Docs/reference/ATTACKTYPE_FOLLOW_2026-10-16.csv','w',encoding='utf-8-sig',newline=''))
w.writerow(['등급','로스터','원작ID','전 타입','후 타입(원작 연구 종류)','보스 배율 전','보스 배율 후','일반몹 배율 전','일반몹 배율 후','구분'])
for g,ro,s,a,b in out: w.writerow([g,ro,s,NAME[a],NAME[b],DT[a][0],DT[b][0],DT[a][1],DT[b][1],'변경'])
for g,ro,s,how in excl: w.writerow([g,ro,s,'','','','','','','제외: 스탯출처 '+how])
c=collections.Counter(g for g,*_ in out); print(len(out),dict(c),'제외',len(excl))
for e in excl: print('제외',e)
