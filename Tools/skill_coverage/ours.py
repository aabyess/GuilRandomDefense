import os,re,json,unicodedata,collections
N=lambda s:unicodedata.normalize('NFC',s)
SK={}
D='Assets/Data/UnitSkills'
for f in os.listdir(D):
    if not f.endswith('.asset'): continue
    t=open(os.path.join(D,f),encoding='utf-8').read()
    g=re.search(r'guid: (\w+)',open(os.path.join(D,f+'.meta')).read()).group(1)
    m=re.search(r'^  skillName: (.*?)\n  description: (.*?)\n  triggerType: (\d+)',t,re.S|re.M)
    sn,desc,tt=(m.group(1),m.group(2),int(m.group(3))) if m else ('','',-1)
    lv=t[t.find('levels:'):]
    neff=len(re.findall(r'^\s+- kind: ',lv,re.M))
    kinds=re.findall(r'^\s+- kind: (\d+)',lv,re.M)
    mult=[float(x) for x in re.findall(r'(?:multiplier|bonus): ([-\d.e+]+)',lv)]
    SK[g]=dict(file=N(f[:-6]),skillName=sn,desc=desc,trig=tt,neff=neff,kinds=kinds,mult=mult,
               empty_levels='levels: []' in t, parsed=bool(m))
UN={}
R='Assets/Data/Units/Roster'
GR=['흔함','안흔함','특별함','희귀함','히든','전설적인','제한됨','초월함','불멸의','영원함','랜덤유닛','다른세계','특수함','초월위습','변화됨']
for f in os.listdir(R):
    if not f.endswith('.asset'): continue
    t=open(os.path.join(R,f),encoding='utf-8').read()
    gr=int(re.search(r'^  grade: (\d+)',t,re.M).group(1))
    i=t.find('\n  skills:'); j=t.find('\n  trait:',i)
    gs=re.findall(r'guid: (\w+)',t[i:j]) if i>=0 else []
    # 🔴 UnitData.SkillAt: skills가 비면 옛 단일 필드 skill로 폴백한다 — 같이 봐야 한다(09-30 본 세션 검토에서 발견, 52 로스터가 skill만 씀)
    if not gs:
        one=re.search(r'^  skill: \{fileID: 11400000, guid: (\w+)',t,re.M)
        if one: gs=[one.group(1)]
    tr=re.search(r'^  trait: \{fileID: (\d+)(?:, guid: (\w+))?',t,re.M)
    UN[N(f[:-6])]=dict(grade=gr,skills=gs,trait=tr.group(2) if tr else None)
if __name__=='__main__':
    print(len(SK),len(UN)); print(collections.Counter(u['grade'] for u in UN.values()))
    used=set(g for u in UN.values() for g in u['skills'])
    print('skills linked',len(used),'unlinked',len(set(SK)-used),'dangling',len(used-set(SK)))
    print('unparsed',[s['file'] for s in SK.values() if not s['parsed']][:5])
    print('no effects',sum(1 for s in SK.values() if s['neff']==0), 'linked&noeff',sum(1 for g in used if g in SK and SK[g]['neff']==0))
    c=collections.Counter(s['file'].split('_')[1] for s in SK.values()); print(c)
    for cat in c:
        ex=[s for s in SK.values() if s['file'].split('_')[1]==cat][:2]
        for s in ex: print('\n##',s['file'],'|',s['skillName'][:120],'|',s['desc'][:700],'| neff',s['neff'])
    print([s['file'] for g,s in SK.items() if g not in used][:40])
