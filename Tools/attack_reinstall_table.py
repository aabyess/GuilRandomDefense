import sys,csv,collections,re,glob,os,statistics,json
R='/Users/sang/GitHub/GuilRandomDefense'
sys.path.insert(0,R+'/Tools/w3x'); import w3u,wts
U={u['id']:u for u in w3u.parse(R+'/Tools/w3x/원본/war3map_new.w3u')}
j=open(R+'/Tools/w3x/원본/풀린것/war3map.j',encoding='utf-8',errors='replace').read()
created=collections.Counter(re.findall(r"CreateNUnitsAtLoc\(\d+,'([hH][0-9A-Z]{3})'",j))
rows=list(csv.DictReader(open(R+'/Docs/reference/MASTER_UID_ROSTER_MAP.csv',encoding='utf-8')))
by=collections.defaultdict(list)
for r in rows: by[r['로스터']].append((r['유닛ID'],r['채널']))
def name(u): return (U[u]['mods'].get('unam') or '')
def stat(uid):
    m=U[uid]['mods']; b=m.get('ua1b') or 0; d=m.get('ua1d'); s=m.get('ua1s'); d=1 if d is None else d; s=1 if s is None else s
    cd=m.get('ua1c') or 1.0
    return b+d*(s+1)/2, cd
# 등급별 원작 모집단(이름 색 표식)
def grade_of(uid):
    n=name(uid)
    if '초월함' in n and 'cff00fa9a' in n: return '초월'
    if '불멸의' in n and 'bc4346' in n: return '불멸'
    if '히든조합' in n: return '히든'
    if '전설적인' in n and 'cffFF0000' in n: return '전설'
    return None
pop=collections.defaultdict(list)
for uid in U:
    g=grade_of(uid)
    if g and U[uid]['mods'].get('ua1b') is not None: pop[g].append(uid)
med={}
for g,l in pop.items():
    a=[stat(u)[0] for u in l]; c=[stat(u)[1] for u in l]
    med[g]=(statistics.median(a),statistics.median(c),len(l))
PRI={'게이트/회수':2,'06번①':3,'전설 대응 2026-10-06':3,'2채널':1,'1채널':0}
def pick(uids):
    # 규칙: ① 조합 결과로 CreateNUnitsAtLoc에 나오는 uid ② 채널 우선순위(게이트/회수·06번①·전설 대응 > 2채널 > 1채널) ③ 같으면 평균(합친 채 유지)
    uids=[(u,c) for u,c in uids if u in U and U[u]['mods'].get('ua1b') is not None]
    if not uids: return None,'원작 없음'
    ref=[u for u,c in uids if created.get(u,0)>0]
    if ref: cand=ref; why='조합 결과(CreateNUnits)'
    else:
        top=max(PRI.get(c,0) for u,c in uids); cand=[u for u,c in uids if PRI.get(c,0)==top]; why='채널우선'
    return cand,why
def ours(p):
    t=open(p,encoding='utf-8').read()
    g=lambda k:(re.search(r'^  %s: (.*)$'%k,t,re.M) or [None,'0'])[1].strip()
    return float(g('attackPower')),float(g('attackSpeed'))
out=[]
for gname,pref in (('초월','초월_'),('전설','전설적인_'),('불멸','불멸_'),('히든','히든_')):
    for p in sorted(glob.glob(R+'/Assets/Data/Units/Roster/%s*.asset'%pref)):
        ro=os.path.basename(p)[:-6]; ap,sp=ours(p)
        cand,why=pick(by.get(ro,[]))
        if cand:
            a=sum(stat(u)[0] for u in cand)/len(cand); c=sum(stat(u)[1] for u in cand)/len(cand)
            src='+'.join(cand)+('(평균)' if len(cand)>1 else '')+' '+why
        else:
            a,c,_=med[gname]; src='대응 없음 → %s 원작 중앙값(%d종)'%(gname,med[gname][2])
        nap=a; nsp=1.0/c
        out.append((gname,ro,src,ap,sp,nap,nsp))
w=csv.writer(sys.stdout,delimiter='\t')
w.writerow(['등급','로스터','원작 대응','지금 공격력','지금 공속','새 공격력','새 공속','공격력 배율','DPS 지금','DPS 새'])
for g,ro,src,ap,sp,nap,nsp in out:
    w.writerow([g,ro,src,'%.0f'%ap,'%.3f'%sp,'%.0f'%nap,'%.3f'%nsp,'%.2f'%(nap/ap if ap else 0),'%.0f'%(ap*sp),'%.0f'%(nap*nsp)])
print('모집단 중앙값:',{g:(round(v[0]),round(1/v[1],3),v[2]) for g,v in med.items()},file=sys.stderr)
