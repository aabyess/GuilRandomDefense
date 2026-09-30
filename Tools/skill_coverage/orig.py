import sys,re,json,collections
sys.path.insert(0,'Tools/w3x'); sys.path.insert(0,sys.argv[1] if len(sys.argv)>1 else '.')
import w3a,w3u,wts
from jparse import *
SD=sys.argv[1]
Sx=wts.parse('Tools/w3x/원본/war3map_new.wts')
A={a['id']:a for a in w3a.parse('Tools/w3x/원본/war3map_new.w3a')}
WU={u['id']:u for u in w3u.parse('Tools/w3x/원본/war3map_new.w3u')}
U=json.load(open(SD+'/units.json'))
def strip(n): return re.sub(r'\|[cC]\w{8}|\|r','',n).replace('\n',' ').strip()
def uname(uid):
    u=WU.get(uid); 
    if not u: return ''
    f=u['mods']; return strip(wts.resolve(str(f.get('unam','')),Sx)), strip(wts.resolve(str(f.get('upro','')),Sx))
def abil_info(code):
    a=A.get(code)
    if not a or code not in CUSTOM: return dict(code=code,cls='스톡',name='',base='STOCK',data={},inj=("'%s'"%code) in J,aord='')
    fl=w3a.fields_by_level(a)
    def g(k):
        v=fl.get(k,{}); return v.get(1,v.get(0)) if v else None
    name=strip(wts.resolve(str(g('anam') or ''),Sx))
    data={}
    for k,v in fl.items():
        if k[0].isupper():
            vals=[x for x in v.values() if isinstance(x,(int,float))]
            if vals: data[k]=max(vals,key=abs)
    nz={k:round(v,4) for k,v in data.items() if v}
    inj=("'%s'"%code) in J
    base=a['base']; aord=g('aord') or ''
    if base=='Asph': cls='시각이펙트'
    elif name.startswith('[조합]') or any(w in name for w in ('판매','창고','유닛모으기')): cls='UI버튼'
    elif base=='Arsg' or name.startswith('T'): cls='강화·변화버튼'
    elif base=='ANbl' or name.startswith('D'): cls='이동기'
    elif nz: cls='실효'
    elif inj: cls='꼬리표(j참조)'
    elif data: cls='껍데기(수치0·j미참조)'
    else: cls='수치미수정(스톡기본값?)'
    return dict(code=code,cls=cls,name=name,base=base,data=nz,inj=inj,aord=aord)
_raw=open('Tools/w3x/원본/war3map_new.w3a','rb').read()
import struct
n_orig=struct.unpack('<I',_raw[4:8])[0]
ALL=w3a.parse('Tools/w3x/원본/war3map_new.w3a'); CUSTOM={a['id'] for a in ALL[n_orig:]}|{a['id'] for a in ALL[:n_orig]}
DMG=re.compile(r'\b(RRD|UnitDamageTargetBJ|UnitDamagePointLoc|UnitDamageTarget)\(')
CRE=re.compile(r"Create\w*\([^']{0,60}'(\w{4})'")
def fx(body):
    d=len(DMG.findall(body))
    dum=sorted(set(x for x in CRE.findall(body)))
    ab=sorted(set(re.findall(r"'(A\w{3})'",body)))
    bf=sorted(set(re.findall(r"'(B\w{3})'",body)))
    add=sorted(set(re.findall(r"UnitAddAbilityBJ\('(\w{4})'",body))|set(re.findall(r"UnitAddAbility\([^,]+,'(\w{4})'",body)))
    orders=sorted(set(re.findall(r'"([a-z]+)"',body)))
    gates=sorted(set(re.findall(r'GetRandomInt\((\d+,\d+)\)',body)))
    consts=[]
    for m in re.finditer(r'call (?:RRD|UnitDamageTargetBJ|UnitDamagePointLoc|UnitDamageTarget)\((.*)',body):
        for x in re.findall(r'(?<![\w.])(\d+(?:\.\d+)?)',m.group(1)):
            if float(x)>=1000: consts.append(float(x))
    return dict(dmg=d,dummies=dum,abils=ab,buffs=bf,add=add,orders=orders,gates=gates,consts=consts)
def own_funcs(t):
    seen=set(); st=list(ACT.get(t,[])+COND.get(t,[]))
    while st:
        f=st.pop()
        if f in seen or f not in FN: continue
        seen.add(f); b=FN[f]
        for c in set(CALLRE.findall(b))|set(re.findall(r'function (\w+)',b)):
            if c in FN and c!='RRD': st.append(c)
    return seen
_own={}
def trig_fx(t):
    if t in _own: return _own[t]
    body='\n'.join(FN[f] for f in own_funcs(t) if f!='RRD')
    r=fx(body); r['nfunc']=len(own_funcs(t)); _own[t]=r; return r
# spell net
SPELL=collections.defaultdict(set)   # code -> funcs
for n,b in FN.items():
    for c in re.findall(r"GetSpellAbilityId\(\)==\s*'(\w{4})'",b): SPELL[c].add(n)
F2T=collections.defaultdict(set)
for t,fs in list(ACT.items())+list(COND.items()):
    for f in fs: F2T[f].add(t)
def func_trigs(f):
    # condition funcs like Trig_X_Conditions / Trig_X_Func003C -> trigger by name prefix
    if f in F2T: return F2T[f]
    m=re.match(r'Trig_(.+?)_(?:Conditions|Actions|Func\w+)$',f)
    return {'gg_trg_'+m.group(1)} if m and ('gg_trg_'+m.group(1)) in ACT else set()
# uid literal net
UIDREF=collections.defaultdict(set)
for n,b in FN.items():
    for c in set(re.findall(r"'([hH]\w{3})'",b)): UIDREF[c].add(n)
PU={r['id'] for r in U}
out=[]
for r in U:
    uid=r['id']; wu=WU[uid]['mods']
    codes=[c for c in (str(wu.get('uabi',''))+','+str(wu.get('uhab',''))).split(',') if c and c!='None']
    ab=[abil_info(c) for c in codes]
    roots=[t for _,t in HASH.get(uid,[])]
    Fc,Tc=closure(start_trgs=roots) if roots else (set(),set())
    trigs=[]; summon={}
    grew=True
    while grew:
        grew=False
        for t in list(Tc):
            for d in trig_fx(t)['dummies']:
                if d in HASH and d not in PU and d!=uid:
                    for _,st in HASH[d]:
                        if st not in Tc:
                            F2,T2=closure(start_trgs=[st]); Fc|=F2
                            for x in T2:
                                if x not in Tc: Tc.add(x); summon[x]=d; grew=True
    for t in sorted(Tc):
        e=trig_fx(t); trigs.append(dict(t=t[7:],root=t in roots,summon=summon.get(t,''),**e))
    added=sorted(set(x for t in trigs for x in t['add']))
    # spells on held/added codes
    sp=[]
    for c in set(codes)|set(added):
        for f in SPELL.get(c,()):
            for t in func_trigs(f):
                F2,T2=closure(start_trgs=[t])
                for t2 in sorted(T2):
                    e=trig_fx(t2); sp.append(dict(code=c,t=t2[7:],**e))
    # other functions mentioning uid
    oth=[]
    for f in UIDREF.get(uid,()):
        if f in Fc or f.startswith('Trig_AttackHashTable'): continue
        for t in func_trigs(f) or {f}:
            if t in Tc: continue
            if t.startswith('gg_trg_'):
                F2,T2=closure(start_trgs=[t]); d=sum(trig_fx(x)['dmg'] for x in T2); du=sorted(set(y for x in T2 for y in trig_fx(x)['dummies']))
                oth.append(dict(t=t[7:],dmg=d,dummies=du,nt=len(T2)))
            else:
                e=fx(FN[f]); oth.append(dict(t='fn:'+f,dmg=e['dmg'],dummies=e['dummies'],nt=0))
    seen=set(); oth=[o for o in oth if not (o['t'] in seen or seen.add(o['t']))]
    nm=uname(uid)
    out.append(dict(id=uid,name=r['name'],upro=nm[1],grade=r['grade'],sub=r['sub'],hero=r['hero'],abils=ab,roots=[t[7:] for t in roots],trigs=trigs,added=[abil_info(c) for c in added],spells=sp,others=oth,closure=len(Fc)))
json.dump(out,open(SD+'/orig.json','w'),ensure_ascii=False)
# dummy unit abilities
DU={}
for o in out:
    for t in o['trigs']+o['spells']:
        for d in t['dummies']:
            if d in WU and d not in DU:
                DU[d]=dict(name=uname(d)[0],abils=[abil_info(c) for c in str(WU[d]['mods'].get('uabi','')).split(',') if c and c!='None'])
json.dump(DU,open(SD+'/dummies.json','w'),ensure_ascii=False)
print(len(out),'units; with hash',sum(1 for o in out if o['roots']),'max closure',max(o['closure'] for o in out))
print('hash uids not in unit list',sorted(set(HASH)-{o['id'] for o in out}))
c=collections.Counter(a['cls'] for o in out for a in o['abils']); print(c)
print('ExecuteFunc',J.count('ExecuteFunc('),'LoadTriggerHandle',J.count('LoadTriggerHandle('))
big=[(o['id'],o['closure'],len(o['trigs'])) for o in out if o['closure']>150]; print(big[:20])
