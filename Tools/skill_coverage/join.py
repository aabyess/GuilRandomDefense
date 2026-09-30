import sys,re,json,csv,collections
SD=sys.argv[1]; sys.path.insert(0,SD); sys.path.insert(0,'Tools/w3x')
from jparse import HASH,closure,FN
import ours
SK,UN=ours.SK,ours.UN
O=json.load(open(SD+'/orig.json')); OB={o['id']:o for o in O}
DU=json.load(open(SD+'/dummies.json'))
MAP=list(csv.DictReader(open('Docs/reference/MASTER_UID_ROSTER_MAP.csv',encoding='utf-8-sig')))
U2R={m['유닛ID']:(m['로스터'],m['채널']) for m in MAP}
R2U=collections.defaultdict(list)
for m in MAP: R2U[m['로스터']].append(m['유닛ID'])
held=collections.Counter(a['code'] for o in O for a in o['abils'])
PUID={o['id'] for o in O}
# uid mention count per function for "others" generic filter handled by name blacklist
LINK=collections.defaultdict(set)   # guid -> rosters
for r,u in UN.items():
    for g in u['skills']: LINK[g].add(r)
def text(s): return s['file']+' '+s['skillName']+' '+s['desc']
TXT={g:text(s) for g,s in SK.items()}
def has(tok,t): return re.search(r'(?<![A-Za-z0-9_])'+re.escape(tok)+r'(?![A-Za-z0-9])',t) is not None
# pull orig.py helpers for summon triggers
import importlib.util
def items_of(o):
    its=[]
    for a in o['abils']+o['added']:
        if a['cls']=='실효' and held[a['code']]<20:
            its.append(dict(key='A:'+a['code'],label='%s(%s %s %s)'%(a['code'],a['name'],a['base'],json.dumps(a['data'],ensure_ascii=False)),toks=[a['code']],consts=[],kind='능력',vals=[abs(v) for v in a['data'].values() if abs(v)>=5]))
    for t in o['trigs']:
        eff=t['dmg']>0 or t['dummies'] or t['add']
        if not eff: continue
        dab=[x['code'] for d in t['dummies'] for x in DU.get(d,{}).get('abils',[]) if x['cls'] not in('스톡','시각이펙트')]
        its.append(dict(key='T:'+t['t'],label='%s[피해호출%d·더미%d%s%s]'%(t['t'],t['dmg'],len(t['dummies']),'·평타등록' if t['root'] else '','·소환체 '+t['summon']+' 평타' if t.get('summon') else ''),
                        toks=[t['t'],'Trig_'+t['t']],sec=t['dummies']+dab,consts=t['consts'],kind='평타트리거' if t['root'] else '호출트리거'))
    for s in o['spells']:
        a=next((x for x in o['abils']+o['added'] if x['code']==s['code']),None)
        if a and a['cls'] in ('UI버튼','강화·변화버튼','시각이펙트','스톡'): continue
        if s['dmg']>0 or s['dummies']:
            its.append(dict(key='S:'+s['t'],label='%s←시전 %s[피해호출%d·더미%d]'%(s['t'],s['code'],s['dmg'],len(s['dummies'])),toks=[s['t'],'Trig_'+s['t'],s['code']],sec=s['dummies'],consts=s['consts'],kind='시전트리거'))
    seen=set(); return [i for i in its if not (i['key'] in seen or seen.add(i['key']))]
rows=[]; extra_by_roster={}
for o in O:
    uid=o['id']; ros,ch=U2R.get(uid,('',''))
    its=items_of(o)
    shells=[a for a in o['abils'] if a['cls'] in('꼬리표(j참조)','껍데기(수치0·j미참조)','수치미수정(스톡기본값?)')]
    ui=[a for a in o['abils'] if a['cls'] in('UI버튼','강화·변화버튼','시각이펙트','스톡','이동기')]
    common=[a for a in o['abils'] if a['cls']=='실효' and held[a['code']]>=20]
    linked=[g for g in UN.get(ros,{}).get('skills',[]) if g in SK] if ros else []
    live=[g for g in linked if SK[g]['neff']>0]; dead=[g for g in linked if SK[g]['neff']==0]
    got=[];miss=[];shell=[];else_=[]
    used_assets=set()
    for it in its:
        def find(gs,toks): return [g for g in gs if any(has(t,TXT[g]) for t in toks)]
        a=find(live,it['toks']) or find(live,[x for x in it.get('sec',[])])
        if a: got.append(it['label']); used_assets|=set(a); continue
        b=find(dead,it['toks'])
        if b: shell.append(it['label']+'→'+SK[b[0]]['file']); used_assets|=set(b); continue
        c=find(SK.keys(),it['toks'])
        if c:
            where=sorted({r for g in c for r in LINK.get(g,())}) or ['미연결 에셋 '+SK[c[0]]['file']]
            else_.append(it['label']+'→'+'/'.join(where)); miss.append(it['label']); continue
        vals=it.get('vals') or []
        nums={round(abs(x),2) for g in live for x in SK[g]['mult']}
        if vals and all(round(v,2) in nums for v in vals): miss.append(it['label']+'※값은 우리 효과에 있음(표기만 없음?)')
        else: miss.append(it['label'])
    oc=sorted(set(x for it in its for x in it['consts'])); ours_nums=set(round(x) for g in live for x in SK[g]['mult'])
    cm=sum(1 for x in oc if round(x) in ours_nums)
    # assets on roster not explained by this roster's uids
    extra=[]
    if ros:
        allu=R2U[ros]; alltoks=[t for u in allu for it in items_of(OB[u]) for t in it['toks']+it.get('sec',[])] if all(u in OB for u in allu) else []
        for g in live:
            t=TXT[g]
            if any(has(u,t) for u in allu): continue
            if any(has(x,t) for x in alltoks): continue
            src=sorted(set(re.findall(r'(?<![A-Za-z0-9_])([hH][0-9A-Z]{3})(?![A-Za-z0-9])',t))&PUID)
            extra.append(SK[g]['file']+('(출처 '+','.join(src)+')' if src else '(출처 uid 표기 없음)'))
        extra_by_roster[ros]=extra
    others=[x for x in o['others'] if not x['t'].startswith('fn:') and x['dmg']>0]
    has_skill='있음' if its else ('꼬리표·툴팁만' if shells else '없음')
    conf=[]
    if not its and shells: conf.append('툴팁 능력 %d개가 있는데 효과 배선을 네 그물로 못 찾음'%len(shells))
    if others: conf.append('평타 등록 밖에서 이 uid를 언급하는 피해 트리거: '+','.join(x['t'] for x in others[:5]))
    if its and ros and got and oc and cm==0: conf.append('이름으론 반영인데 원작 피해상수 %d개 중 우리 값 일치 0'%len(oc))
    if uid in HASH and len(HASH[uid])>1: conf.append('HashAttack 같은 키에 둘 등록(뒤엣것만 남음)')
    if o['hero'] and not o['roots']: conf.append('영웅(H)인데 평타 등록 없음')
    rows.append(dict(uid=uid,원작이름=o['name'],영웅실명=o['upro'] if o['hero'] else '',원작등급=o['grade']+('[%s]'%o['sub'] if o['sub'] else ''),
        우리로스터=ros,배정채널=ch,원작스킬=has_skill,원작항목수=len(its),
        그물1_uabi실효=' | '.join(i['label'] for i in its if i['kind']=='능력'),
        그물1_꼬리표·툴팁=' | '.join('%s(%s:%s)'%(a['code'],a['name'],a['cls']) for a in shells),
        그물1_공용·UI·시각=' | '.join('%s(%s)'%(a['code'],a['name'] or a['cls']) for a in common)+(' + UI/시각/스톡 %d'%len(ui)),
        그물2_런타임부여=' | '.join('%s(%s:%s)'%(a['code'],a['name'],a['cls']) for a in o['added']),
        그물3_평타트리거=' | '.join(o['roots']),
        그물3_호출트리거=' | '.join(i['label'] for i in its if i['kind'] in('평타트리거','호출트리거')),
        그물4_시전트리거=' | '.join(i['label'] for i in its if i['kind']=='시전트리거'),
        그물5_uid참조_피해트리거=' | '.join('%s[피해%d]'%(x['t'],x['dmg']) for x in others),
        우리_연결SkillData='%d(effects있음 %d·빈 %d)'%(len(linked),len(live),len(dead)) if ros else '',
        우리_반영=' | '.join(got),우리_빈껍데기=' | '.join(shell),빠짐=' | '.join(miss),다른곳에있음=' | '.join(else_),
        피해상수일치='%d/%d'%(cm,len(oc)) if oc and ros else '',
        원작엔없는데우리엔있음=' | '.join(extra_by_roster.get(ros,[])) if ros else '',
        확신도='낮음' if conf else '보통',비고=' ; '.join(conf),_n=(len(its),len(got),len(miss),len(shell))))
json.dump(rows,open(SD+'/rows.json','w'),ensure_ascii=False)
json.dump(extra_by_roster,open(SD+'/extra.json','w'),ensure_ascii=False)
GO=['영원한','불멸의','초월함','제한됨','랜덤전용','히든','변화된','특수함','전설적인','희귀함','특별함','안흔함','흔함']
rows.sort(key=lambda r:(GO.index(re.sub(r'\[.*','',r['원작등급'])),r['uid']))
cols=[c for c in rows[0] if c!='_n']
with open('Docs/research/SKILL_COVERAGE_BY_GRADE.csv','w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,cols,extrasaction='ignore'); w.writeheader(); w.writerows(rows)
print('grade | 유닛 | 스킬있음 | 툴팁만 | 없음 | 대응로스터있음 | (대응+스킬있음) 전부반영 | 일부 | 0반영 | 항목 합계 | 반영 | 빠짐 | 빈껍데기')
for g in GO:
    rs=[r for r in rows if r['원작등급'].startswith(g)]
    sk=[r for r in rs if r['원작스킬']=='있음']; mp=[r for r in sk if r['우리로스터']]
    full=sum(1 for r in mp if r['_n'][2]==0 and r['_n'][3]==0); zero=sum(1 for r in mp if r['_n'][1]==0)
    print(g,len(rs),len(sk),sum(1 for r in rs if r['원작스킬']=='꼬리표·툴팁만'),sum(1 for r in rs if r['원작스킬']=='없음'),sum(1 for r in rs if r['우리로스터']),full,len(mp)-full-zero,zero,
          sum(r['_n'][0] for r in rs),sum(r['_n'][1] for r in rs),sum(r['_n'][2] for r in rs),sum(r['_n'][3] for r in rs),sep=' | ')
print('low conf',sum(1 for r in rows if r['확신도']=='낮음'))
