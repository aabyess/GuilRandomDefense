# 로스터 스탯 출처(값 일치 원작 유닛) ↔ 스킬 원작 주인 대조표 생성. 저장소 루트에서: python3 -I Tools/skill_stat_source_audit.py
import sys,re,glob,os,csv,collections
sys.path.insert(0,'Tools/w3x'); import w3u,w3a,wts
def un(s): return re.sub(r'\\u([0-9A-Fa-f]{4})',lambda m:chr(int(m.group(1),16)),s)
strip=lambda s:re.sub(r'\|c[0-9a-fA-F]{8}|\|r','',s or '').replace('\r\n',' ').strip()
U={u['id'].upper():u for u in w3u.parse('Tools/w3x/원본/war3map_new.w3u')}
A={a['id'].upper():a for a in w3a.parse('Tools/w3x/원본/war3map_new.w3a')}
def aname(i):
    a=A.get(i)
    if not a: return ''
    d={}
    for m in a['mods']: d.setdefault(m['field'],m['value'])
    return strip(d.get('anam') or d.get('atp1') or '')
def stat(m):
    b=m.get('ua1b')
    if b is None or not m.get('ua1c'): return None
    d=m.get('ua1d'); s=m.get('ua1s'); d=1 if d is None else d; s=1 if s is None else s
    return b+d*(s+1)/2, 1/m['ua1c']
S={}
for i,u in U.items():
    if i[0] in 'H' and stat(u['mods']): S[i]=stat(u['mods'])
uname=lambda i:strip(U[i]['mods'].get('unam'))[:24] if i in U else ''
owners=collections.defaultdict(set)
for i,u in U.items():
    ab=u['mods'].get('uabi') or ''
    for a in ab.split(','):
        if a: owners[a.upper()].add(i)
SKIP_NAME=('조합','판매','창고','아이템','도움소','[')
def combat_abs(i):
    out=[]
    for a in (U[i]['mods'].get('uabi') or '').split(','):
        a=a.upper()
        if not a or not re.match(r'A[0-9A-Z]{3}$',a) or a in('A07Y','A0B8','A0B9','A0JE'): continue
        n=aname(a)
        if any(k in n for k in SKIP_NAME) and not n.startswith('!'): continue
        if n.startswith('[조합]') or '조합' in n: continue
        base=A[a].get('base') if a in A else ''
        out.append(f'{a} {n}')
    return out
# csv map
cm=collections.defaultdict(list)
for r in csv.DictReader(open('Docs/reference/MASTER_UID_ROSTER_MAP.csv',encoding='utf-8')): cm[r['로스터']].append(r['유닛ID'].upper())
# skills
SK={}
guid2={}
for p in glob.glob('Assets/Data/UnitSkills/*.asset'):
    mg=re.search(r'^guid: (\w+)',open(p+'.meta').read(),re.M).group(1)
    t=open(p).read(); n=os.path.basename(p)[:-6]
    sn=re.search(r'^  skillName: (.*)$',t,re.M); d=re.search(r'^  description: (.*?)\n  triggerType',t,re.M|re.S)
    txt=n+' '+un(sn.group(1) if sn else '')+' '+un(' '.join(d.group(1).split()) if d else '')
    uids=set(x.upper() for x in re.findall(r'(?<![0-9A-Za-z])[hH][0-9A-Za-z]{3}(?![0-9A-Za-z])',txt) if x.upper() in U)
    abs_=set(re.findall(r'(?<![0-9A-Za-z])A[0-9A-Z]{3}(?![0-9A-Za-z])',txt))
    own=set(uids)
    for a in abs_:
        if a in owners and len(owners[a])<=6: own|=owners[a]
    SK[mg]=(n,own,n.startswith('SkillData_사장님_'))
def val(t,k): 
    m=re.search(r'^  %s: (\S+)'%k,t,re.M); return float(m.group(1)) if m else None
rows=[];byid=collections.defaultdict(list)
for p in sorted(glob.glob('Assets/Data/Units/Roster/*.asset')):
    t=open(p).read(); n=os.path.basename(p)[:-6]
    grade=n.split('_')[0]
    ap,asp=val(t,'attackPower'),val(t,'attackSpeed')
    vm=[i for i,(a,s) in S.items() if ap and abs(a-ap)<=max(2,ap*0.002) and abs(s-asp)<=0.006]
    cids=[c for c in cm.get(n,[]) if c in U]
    OVR={'히든_감탄떡볶이':['H03X'],'히든_맥주만땅':['H03T'],'히든_이삭토스트':['H03Q'],'히든_여은서':['H03O']}
    note0=''
    if n in OVR: vm=OVR[n]; note0='값 상향(40ca04c97) 전 원작값 기준 출처. '
    src=vm or cids
    srckind='값일치' if vm else ('csv만(값 불일치·없음)' if cids else '불명')
    if vm and cids and set(vm)&set(cids): srckind='값일치=csv'
    elif vm and cids: srckind='값일치(csv와 다름)'
    if n in OVR: srckind='값일치(상향 전)'
    sg=[]
    m1=re.search(r'^  skill: \{fileID: 11400000, guid: (\w+)',t,re.M)
    if m1: sg.append(m1.group(1))
    if 'skills:' in t:
        blk=t.split('skills:')[1].split('\n  trait:')[0]
        sg+=re.findall(r'guid: (\w+), type: 2',blk)
    sk=[SK[g] for g in sg if g in SK]
    fixed = any(s[2] for s in sk) or n.startswith('초월_최상호') or n.startswith('초월_노태현')
    skown=set().union(*[s[1] for s in sk]) if sk else set()
    ns=[x for x in sk if not x[2]]
    nsown=set().union(*[x[1] for x in ns]) if ns else set()
    extra=note0
    if fixed and ns: extra='사양 외 스킬: '+('✅' if set(src)&nsown else '❌' if nsown and src else '?')+' '+'+'.join(x[0][10:] for x in ns)
    if fixed: v='사양 고정'
    elif not sk: v='스킬없음'
    elif not src: v='출처불명'
    elif set(src)&skown: v='✅'
    elif not skown: v='소유자불명'
    else: v='❌'
    comb=[]
    if v in('❌','스킬없음','소유자불명'):
        for i in src: comb.append(f'{i}:'+'; '.join(combat_abs(i)))
    for i in src: byid[i].append(n)
    rows.append([grade,n,ap,asp,srckind,'/'.join(f'{i} {uname(i)}' for i in src),'/'.join(f'{i}' for i in cids),' | '.join(s[0][10:] for s in sk),'/'.join(sorted(skown)),v,' || '.join(comb),extra])
for r in rows:
    ids=[x.split()[0] for x in r[5].split('/') if x]
    sh=[(i,byid[i]) for i in ids if len(byid[i])>1]
    if sh: r[11]=(r[11]+' ' if r[11] else '')+'공유: '+'; '.join(f'{i}←'+','.join(x for x in l if x!=r[1]) for i,l in sh)
w=csv.writer(open('Docs/reference/SKILL_STAT_SOURCE_AUDIT_2026-10-15.csv','w',encoding='utf-8-sig',newline=''))
w.writerow(['등급','로스터','공격력','공속','스탯출처방식','스탯 원작유닛(ID 이름)','csv 매핑ID','붙은 스킬','스킬 원작주인ID','판정','❌/없음이면 원작 전투능력','비고'])
w.writerows(rows)
c=collections.defaultdict(collections.Counter)
for r in rows: c[r[0]][r[9]]+=1
for g,k in c.items(): print(g,dict(k))
print(len(rows))
