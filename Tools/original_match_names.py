#!/usr/bin/python3
"""로스터 → 원작 캐릭터 한글 이름 표(사장님 10-07 「베타라 누구 매칭인지 보이게」) — 공격력 재이식 표와 같은 대응 규칙(attack_reinstall_table.py의 pick).
원작 이름 = war3map_new.w3u의 upro(고유 이름) 「|cffc0c0c0징베|r  - |cff00fa9a초월함|r」에서 색 코드를 떼고 「 - 등급」 앞 글자(예 징베). upro가 없으면 unam.
대응 uid가 여럿이면 「A/B」(중복 이름은 한 번), 없으면 표에서 뺀다.  사용: /usr/bin/python3 Tools/original_match_names.py > Docs/research/ORIGINAL_MATCH_NAMES_2026-10-07.tsv
"""
import sys,csv,collections,re,glob,os
R='/Users/sang/GitHub/GuilRandomDefense'
sys.path.insert(0,R+'/Tools/w3x'); import w3u
U={u['id']:u for u in w3u.parse(R+'/Tools/w3x/원본/war3map_new.w3u')}
j=open(R+'/Tools/w3x/원본/풀린것/war3map.j',encoding='utf-8',errors='replace').read()
created=collections.Counter(re.findall(r"CreateNUnitsAtLoc\(\d+,'([hH][0-9A-Z]{3})'",j))
rows=list(csv.DictReader(open(R+'/Docs/reference/MASTER_UID_ROSTER_MAP.csv',encoding='utf-8')))
by=collections.defaultdict(list)
for r in rows: by[r['로스터']].append((r['유닛ID'],r['채널']))
PRI={'게이트/회수':2,'06번①':3,'전설 대응 2026-10-06':3,'2채널':1,'1채널':0}
def pick(uids):
    uids=[(u,c) for u,c in uids if u in U]
    if not uids: return []
    ref=[u for u,c in uids if created.get(u,0)>0]
    if ref: return ref
    top=max(PRI.get(c,0) for u,c in uids); return [u for u,c in uids if PRI.get(c,0)==top]
def strip(s): return re.sub(r'\|c[0-9a-fA-F]{8}|\|r','',s or '').strip()
# 소문자 uid(h0xx)는 원작 upro가 「이름 + 수식어 - 등급」 한 덩어리라 이름만 손으로 뗀다(원작 지식 — 이름 두 단어짜리를 자동으로 못 가른다). 대문자 H는 upro가 깨끗해 자동.
SHORT={'h037':'슈가','h02w':'몽키.D.드래곤','h038':'시저 크라운','h03f':'카르가라','h02z':'바르토로메오','h02x':'몽키.D.루피','h03i':'제파','h035':'샹크스','h039':'시키','h03c':'트라팔가 로우','h03h':'샬롯 크래커','h0ah':'킹','h02p':'나미','h02q':'라분','h03e':'코비','h036':'센고쿠','h042':'시노부','h03k':'Dr.히루루크','h032':'보아 핸콕','h033':'빈스모크 레이쥬','h087':'아마츠키 토키','h02t':'마르코','h03s':'울티','h02n':'겟코 모리아','h02s':'롤로노아 조로','h09z':'네코마무시','h03b':'에드워드 뉴게이트','h02u':'마샬.D.티치',
 'h03r':'반 더 데켄','h03z':'아카이누','h03v':'코알라',
 'h04a':'에드워드 뉴게이트','h04b':'시키','h04j':'골.D.로져','h04g':'제트','h04f':'스코퍼 가반','h07m':'카이도','h04c':'몽키.D.거프','h04e':'센고쿠','h049':'실버즈 레일리','h04d':'몽키.D.드래곤','h04q':'빅 맘',
 'h05a':'버기','h067':'우타','h088':'토트 무지카','h057':'네펠타리 비비','h08r':'코즈키 오뎅','h059':'포트거스.D.에이스','h05c':'보아 핸콕','h058':'쥬라클 미호크','h05b':'카벤딧슈'}
def charname(uid):
    if uid.lower() in SHORT and uid[0]=='h': return SHORT[uid.lower()]
    m=U[uid]['mods']; p=strip(m.get('upro'))
    if p:
        n=re.split(r'\s+-\s+',p)[0].strip()
        if n: return re.sub(r'\s*-\s*(초월함|영원한|불멸의|전설적인)$','',n).strip()
    return strip(m.get('unam'))
w=csv.writer(sys.stdout,delimiter='\t'); w.writerow(['로스터','원작 유닛','원작 이름'])
for ro in sorted(by):
    if not re.match(r'(히든_|전설적인_|초월_|불멸_|영원_|제한됨_)',ro): continue
    cand=pick(by[ro])
    if not cand: continue
    names=[]
    for u in cand:
        n=charname(u)
        if n and n not in names: names.append(n)
    if names: w.writerow([ro,'+'.join(cand),'/'.join(names)])
