"""로스터 공격력·공속을 MASTER_UID_ROSTER_MAP 원작 짝의 w3u 값으로 되돌린다(사장님 10-15 「능력치를 스킬 쪽으로」, 전설적인 때 AttackReinstall과 같은 규칙).
규칙: 공격력 = ua1b + ua1d×(ua1s+1)/2 · 공속 = 1/ua1c · 짝이 여럿이면 조합결과(CreateNUnitsAtLoc) > 채널우선순위(게이트/회수·06번①·전설대응 > 2채널 > 1채널), 같으면 평균.
바꾸는 필드: attackPower·attackSpeed 둘뿐(사거리·크리·스플·체력은 건드리지 않음 — 전설적인 때와 같음).
대상: Docs/reference/SKILL_STAT_SOURCE_AUDIT_*.csv 판정 ❌ 이면서 csv 짝이 있는 로스터. 짝 없는 로스터는 목록만 낸다.
사용(저장소 루트): python3 -I Tools/stat_follow_csv.py 특별함 랜덤        # 미리보기
                    python3 -I Tools/stat_follow_csv.py --apply 특별함 랜덤  # 에셋 수정(커밋은 직접)
"""
import sys,csv,collections,re,os
R=os.path.abspath(os.path.join(os.path.dirname(__file__),'..'))
sys.path.insert(0,R+'/Tools/w3x'); import w3u
args=sys.argv[1:]; APPLY='--apply' in args; grades=[a for a in args if not a.startswith('--')]
U={u['id'].upper():u for u in w3u.parse(R+'/Tools/w3x/원본/war3map_new.w3u')}
j=open(R+'/Tools/w3x/원본/풀린것/war3map.j',encoding='utf-8',errors='replace').read()
created=collections.Counter(x.upper() for x in re.findall(r"CreateNUnitsAtLoc\(\d+,'([hH][0-9A-Z]{3})'",j))
by=collections.defaultdict(list)
for r in csv.DictReader(open(R+'/Docs/reference/MASTER_UID_ROSTER_MAP.csv',encoding='utf-8')): by[r['로스터']].append((r['유닛ID'].upper(),r['채널']))
def stat(uid):
    m=U[uid]['mods']; b=m.get('ua1b') or 0; d=m.get('ua1d'); s=m.get('ua1s'); d=1 if d is None else d; s=1 if s is None else s
    return b+d*(s+1)/2, m.get('ua1c') or 1.0
PRI={'게이트/회수':2,'06번①':3,'전설 대응 2026-10-06':3,'2채널':1,'1채널':0}
def pick(uids):
    uids=[(u,c) for u,c in uids if u in U and U[u]['mods'].get('ua1b') is not None]
    if not uids: return None
    ref=[u for u,c in uids if created.get(u,0)>0]
    if ref: return ref
    top=max(PRI.get(c,0) for u,c in uids); return [u for u,c in uids if PRI.get(c,0)==top]
fmt=lambda v:('%.4f'%v).rstrip('0').rstrip('.')
audit=sorted(__import__('glob').glob(R+'/Docs/reference/SKILL_STAT_SOURCE_AUDIT_*.csv'))[-1]
rows=list(csv.reader(open(audit,encoding='utf-8-sig')))[1:]
done=0;none=[]
for x in rows:
    g,ro,v=x[0],x[1],x[9]
    if g not in grades or v!='❌': continue
    cand=pick(by.get(ro,[]))
    if not cand: none.append(ro); continue
    a=sum(stat(u)[0] for u in cand)/len(cand); c=sum(stat(u)[1] for u in cand)/len(cand)
    p=R+'/Assets/Data/Units/Roster/%s.asset'%ro
    t=open(p,encoding='utf-8').read()
    ap=float(re.search(r'^  attackPower: (\S+)',t,re.M).group(1)); sp=float(re.search(r'^  attackSpeed: (\S+)',t,re.M).group(1))
    print('%s %s -> %.1f/%.4f (DPS %.0f -> %.0f)'%(ro,'+'.join(cand),a,1/c,ap*sp,a/c))
    if APPLY:
        t=re.sub(r'(?m)^  attackPower: \S+$',lambda m:'  attackPower: '+fmt(a),t,count=1)
        t=re.sub(r'(?m)^  attackSpeed: \S+$',lambda m:'  attackSpeed: '+fmt(round(1/c,4)),t,count=1)
        open(p,'w',encoding='utf-8').write(t); done+=1
print('csv 짝 없음(손대지 않음):',none)
if APPLY: print('수정',done)
