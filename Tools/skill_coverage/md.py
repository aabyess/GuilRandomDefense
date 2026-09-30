import json,sys,re,collections
SD=sys.argv[1]; sys.path.insert(0,SD); import ours
R=json.load(open(SD+'/rows.json')); EX=json.load(open(SD+'/extra.json')); RS=json.load(open(SD+'/roster_side.json'))
GO=['영원한','불멸의','초월함','제한됨','랜덤전용','히든','변화된','특수함','전설적인','희귀함','특별함','안흔함','흔함']
g0=lambda r:re.sub(r'\[.*','',r['원작등급'])
def short(s): return re.sub(r'\((?:[^()]|\([^()]*\))*\)','',s)
L=[]; W=L.append
W('# 원작 스킬 전수 조사 — 등급별 반영 현황 (2026-09-30, Blender 세션)\n')
W('표 전체: `Docs/research/SKILL_COVERAGE_BY_GRADE.csv` (행 = 원작 플레이어 유닛 %d).\n'%len(R))
W('## 0. 읽기 전에 — 출발점과 판정 기준\n')
W('''- **「전수」의 출발점**: `war3map_new.w3u`에서 id가 h/H로 시작하고 이름·upoi로 등급이 읽히는 유닛 314(직전 세션 `cov/units.py` 기준 그대로 — 「필요/금화/노획/토큰/묘비/[아이템]/항해일지/퇴치-/???」 이름은 제외). 소환체(h05J 등 9종)는 행이 아니라 주인 유닛의 「소환체 평타」 항목으로 들어간다.
- **원작 그물 다섯**(전부 `war3map_new.j`·`.w3a`·`.w3u` 직접 디코드, 문서 미사용):
  1. `uabi`+`uhab` 보유 능력 → 실효(데이터 필드에 0 아닌 값) / 꼬리표(수치 0·j 참조) / 껍데기(수치 0·j 미참조 = 툴팁) / UI 버튼 / 시각 이펙트(Asph) / 이동기(D…) / 스톡으로 분류. 20유닛 이상이 같이 가진 실효 능력(A0WP 등)은 「공용」으로 빼고 항목에 안 넣었다.
  2. `UnitAddAbility*` — 그 유닛의 트리거 폐쇄 안에서 부여되는 능력.
  3. `udg_HashAttack` 등록 평타 트리거 + 거기서 `TriggerExecute`/`ConditionalTriggerExecute`·함수 호출로 닿는 트리거 전부(폐쇄). 폐쇄 안에서 만든 소환체가 HashAttack에 등록돼 있으면 그 평타 트리거도 이어 붙였다.
  4. `GetSpellAbilityId()=='Axxx'` 트리거 — 보유·부여 능력 코드로 연결(UI·강화 버튼 능력은 제외).
  5. 폐쇄 밖에서 그 uid를 글자로 언급하는 트리거 중 피해 호출이 있는 것(참고 칸, 항목엔 안 넣음).
- **「원작 스킬 항목」** = 실효 능력 1개 또는 효과가 있는 트리거 1개(피해 호출 RRD·UnitDamage* ≥1, 또는 더미 생성, 또는 능력 부여). 효과 없는 분배용 트리거(예: `Akainu_Attack`)는 항목이 아니다. **한 트리거 안의 여러 블록(확률 게이트별 스킬)은 한 항목으로 뭉쳐 있다** — 「반영」은 「그 트리거를 출처로 적은 에셋이 있다」까지이고 블록 전부가 들어갔다는 뜻이 아니다(CSV 「피해상수일치」 칸이 보조 지표).
- **「우리에 들어갔나」** = `MASTER_UID_ROSTER_MAP`의 대응 로스터 유닛 에셋 `skills:`(비었으면 `skill:`)에 **연결돼 있고**, `levels[].effects`가 **1개 이상**인 SkillData가 그 항목의 능력 코드·트리거 이름·더미 id를 파일명/skillName/description에 적고 있는가. effects가 0이면 「빈껍데기」, 연결 안 된 에셋·남의 로스터에만 있으면 「다른곳에있음」(빠짐으로 센다).
  - ⚠️ 출처 표기(코드·트리거 이름) 없이 값만 옮긴 에셋은 「빠짐」으로 보인다. 능력 수치가 우리 효과 값에 그대로 있으면 「※값은 우리 효과에 있음」을 붙였다.
  - ⚠️ 특성(`trait:` UnitTraitData)·유닛 스탯 쪽으로 옮긴 것(예: 공속 오라를 스탯으로)은 **안 봤다**.
''')
W('- ⚠️ **대응표는 여럿 대 하나다** — 로스터 30개가 원작 uid를 둘 이상 받는다(초월_황준석_ADAP 14 · 불멸_정준영 7 · 변화됨_박은석 4 …, 순위 배정의 결과). 그래서 원작 유닛 기준 「빠짐」에는 **한 로스터에 몰린 여러 원작 유닛의 스킬을 다 못 담은 것**이 섞여 있다. 빠짐 수를 곧바로 「채울 양」으로 읽지 말 것 — 로스터마다 어느 uid를 본체로 볼지 먼저 정해야 한다.\n')
W('## 1. 등급별 요약 (원작 유닛 기준)\n')
W('| 원작 등급 | 원작 유닛 | 스킬 있음 | 툴팁만 | 없음 | 대응 로스터 있음 | 대응+스킬 중 전부 반영 | 일부 | 0 | 원작 항목 | 반영 | 빠짐 | 빈껍데기 |')
W('|---|---|---|---|---|---|---|---|---|---|---|---|---|')
for g in GO:
    rs=[r for r in R if g0(r)==g]; sk=[r for r in rs if r['원작스킬']=='있음']; mp=[r for r in sk if r['우리로스터']]
    full=sum(1 for r in mp if r['_n'][2]==0 and r['_n'][3]==0); zero=sum(1 for r in mp if r['_n'][1]==0)
    W('| %s | %d | %d | %d | %d | %d | %d | %d | %d | %d | %d | %d | %d |'%(g,len(rs),len(sk),sum(1 for r in rs if r['원작스킬']=='꼬리표·툴팁만'),sum(1 for r in rs if r['원작스킬']=='없음'),sum(1 for r in rs if r['우리로스터']),full,len(mp)-full-zero,zero,sum(r['_n'][0] for r in rs),sum(r['_n'][1] for r in rs),sum(r['_n'][2] for r in rs),sum(r['_n'][3] for r in rs)))
W('\n- 랜덤전용 28 = 작품 밖 콜라보 카테고리(그중 10이 `랜덤전용[제한됨]`). 우리 「랜덤」·「다른세계」의 원천. 히든 = 이름에 `[히든조합]`/`[히든]`. 영원한은 11.089 맵에 11유닛 있다.')
W('- 원작 흔함 9는 능력 슬롯이 전부 UI 버튼이고 HashAttack 등록도 없다(그물 1·3·4로 0).\n')
W('## 2. 로스터 쪽에서 본 표 (우리 유닛 기준)\n')
W('| 우리 등급 | 로스터 | 원작 uid 대응 있음 | effects 있는 스킬이 연결된 로스터 | 대응 없는데 스킬 있음 | 대응 있는데 스킬 0 |\n|---|---|---|---|---|---|')
G=collections.defaultdict(list)
for n in ours.UN: G[n.split('_')[0]].append(n)
import csv
MAP=list(csv.DictReader(open('Docs/reference/MASTER_UID_ROSTER_MAP.csv',encoding='utf-8-sig'))); R2U=collections.defaultdict(list)
for m in MAP: R2U[m['로스터']].append(m['유닛ID'])
live=lambda n:[x for x in ours.UN[n]['skills'] if x in ours.SK and ours.SK[x]['neff']>0]
for g in ['영원','불멸','초월','제한','랜덤','다른세계','히든','변화됨','특수함','전설적인','희귀함','특별함','안흔함','흔함']:
    ns=G[g]; m=[n for n in ns if n in R2U]
    W('| %s | %d | %d | %d | %d | %d |'%(g,len(ns),len(m),sum(1 for n in ns if live(n)),sum(1 for n in ns if n not in R2U and live(n)),sum(1 for n in m if not live(n))))
W('''
**직전 어림 「히든 3/22 · 다른세계 0/9 · 특수함 2/6」 검증**: 「원작 uid가 대응된 로스터 수」로는 **셋 다 맞다**(히든 3/22 · 다른세계 0/9 · 특수함 2/6). 「실제로 effects 있는 스킬이 연결된 로스터」로 세도 위 표 §2의 값과 같다(특수함 둘 황길라←h05O 아이스버그·장진희←h05H 모건은 `skills:` 목록은 비었지만 옛 단일 필드 `skill:`에 `SkillData_원작능력_특수함_*`이 걸려 있고, 코드는 `UnitData.SkillAt`으로 그 필드까지 읽는다).
- 히든 대응 셋: 히든_최윤서←h03R 반 더 데켄 · 히든_호치킨←h03Z 아카이누 · 히든_황정기←h03V 코알라. 나머지 19 로스터는 대응표에 원작 uid가 없다 → 「원작에 있는 유닛에만」 규칙으로는 **먼저 대응(어느 원작 히든인지)을 정해야** 채울 수 있다. 원작 히든 28 중 대응 안 된 것 24(그중 스킬 있음 21).
- 다른세계 9 로스터는 대응 0. 원작 랜덤전용 28 중 대응 안 된 8이 후보다(아래 §3 「대응 로스터 없음」).
''')
W('## 3. 빠진 것 — 위 등급부터\n')
W('표기: `uid 원작이름 → 로스터`: 빠진 항목. `[피해호출n·더미m]`은 그 트리거의 크기. 「→…」는 그 항목을 적은 에셋이 있는 다른 곳.\n')
for g in GO:
    rs=[r for r in R if g0(r)==g and r['원작스킬']=='있음']
    a=[r for r in rs if r['우리로스터'] and (r['빠짐'] or r['우리_빈껍데기'])]; b=[r for r in rs if not r['우리로스터']]
    if not a and not b: continue
    W('### %s — 대응 있음·빠짐 %d유닛 / 대응 로스터 없음(스킬 있음) %d유닛\n'%(g,len(a),len(b)))
    for r in a:
        el={short(x.split('→')[0]).strip():x.split('→')[1] for x in r['다른곳에있음'].split(' | ') if '→' in x}
        ms=[]
        for x in r['빠짐'].split(' | '):
            if not x: continue
            k=short(x).strip(); nm=re.search(r'\(([^ ]+(?: [^ A-Z{]+)*)',x); 
            lab=x.split('(')[0]+('('+x.split('(')[1].split(' A')[0].split(' {')[0]+')' if x.startswith('A') and '(' in x else '') if x[0]=='A' and x[1:4].isalnum() and '[' not in x else x
            ms.append(lab+('※값있음' if '※' in x else '')+(' →'+el[k] if k in el else ''))
        sh=['(빈껍데기) '+x for x in r['우리_빈껍데기'].split(' | ') if x]
        W('- `%s` %s → **%s** (항목 %d 중 반영 %d): %s'%(r['uid'],r['원작이름'][:40],r['우리로스터'],r['_n'][0],r['_n'][1],' · '.join(ms+sh)))
    if b:
        W('\n대응 로스터 없음: '+' · '.join('`%s` %s(항목 %d)'%(r['uid'],r['원작이름'][:28],r['_n'][0]) for r in b)+'\n')
    W('')
W('## 4. 「원작엔 없는데 우리엔 있음」 (지우기 전 사장님 보고 대상)\n')
W('대응 로스터에 연결된 effects 있는 SkillData 중, 그 로스터에 대응된 어느 원작 uid의 코드·트리거·더미도 안 적혀 있는 것. 괄호는 description에 적힌 출처 uid.\n')
n=0
for ros,ex in sorted(EX.items()):
    if ex: n+=len(ex); W('- **%s** (대응 %s): %s'%(ros,','.join(R2U[ros]),' · '.join(x.replace('SkillData_','') for x in ex)))
W('\n대응 원작 uid가 아예 없는 로스터에 연결된 스킬:')
for nme in sorted(ours.UN):
    if nme not in R2U and live(nme):
        W('- **%s**: %s'%(nme,' · '.join(ours.SK[x]['file'].replace('SkillData_','') for x in live(nme))))
W('\n(에셋 %d개 + 위 무대응 로스터분. 출처 uid가 적힌 것은 「남의 유닛 스킬이 이 로스터에 와 있다」, 표기 없는 것은 출처를 이 조사로는 못 밝혔다.)\n'%n)
W('## 5. 확신도 낮은 행\n')
for r in R:
    if r['확신도']=='낮음': W('- `%s` %s (%s) → %s: %s'%(r['uid'],r['원작이름'][:36],r['원작등급'],r['우리로스터'] or '대응 없음',r['비고']))
W('''
## 6. 돌린 그물과 못 본 것

돌린 것: §0의 다섯 그물 전부(j 함수 8,156 · 트리거 784 · HashAttack 등록 204건/203 uid, 그중 플레이어 유닛 194). 우리 쪽은 SkillData 431 에셋(effects 0인 것 24) · 로스터 240 에셋의 `skills:` + 옛 단일 필드 `skill:` 폴백(`UnitData.SkillAt`과 같은 규칙 — 52 로스터가 `skill:`만 쓴다. 연결 425 · 미연결 7).

못 본 것 / 한계:
- **트리거 안 블록 단위 대조는 안 했다.** 한 평타 트리거에 확률 게이트가 여럿이어도 한 항목이다. 「반영」인 항목도 일부 블록이 빠졌을 수 있다(피해상수일치 칸 참고: 원작 피해 호출의 1000 이상 상수가 우리 multiplier/bonus에 있는지).
- **값의 정확성은 안 봤다**(확률·계수·반경·게이트). 이 표는 「있나/없나」만이다.
- **유닛 인스턴스 변수로만 이어진 트리거**(예: `TriggerRegisterUnitInRangeSimple` 5건·`TriggerRegisterUnitInRange` 1건, 주기 타이머가 `udg_Hero_X`를 읽는 것)는 HashAttack 폐쇄에 안 닿으면 그물 5(uid 글자 참조)에만 걸리고, 거기도 안 걸리면 빠진다. 샹크스·핸콕 「적이 근처에 오면」 계열이 여기다.
- **더미가 가진 능력의 효과 종류**(스턴·둔화·방깎)는 분류 안 했다 — 더미 생성이 있으면 효과 있는 항목으로만 셌다. 더미가 순수 시각 이펙트뿐인 트리거가 항목으로 세어졌을 수 있다(피해호출 0·더미만 있는 항목이 그 후보).
- **수치 수정이 없는 커스텀 능력 24건**(「수치미수정(스톡기본값?)」)은 스톡 기본값으로 실효일 수 있으나 스톡 표(SLK)가 없어 판정 못 하고 꼬리표·툴팁 칸에 뒀다.
- **`h07F`(시키 돌 소환체)는 HashAttack 같은 키에 트리거 둘이 저장**돼 뒤엣것(`Legend4_shickirock2`)만 남는다 — 앞엣것은 죽은 등록일 수 있다.
- **특성(UnitTraitData)·아이템·연구로 주는 능력**은 범위 밖. `T특성강화` 버튼(Arsg)으로 바뀌는 능력 레벨/교체(06번①)는 「강화·변화버튼」으로만 분류했다.
- 등급은 이름 글자·upoi로 읽었다(직전 세션 기준). 대응표는 `MASTER_UID_ROSTER_MAP.csv` 199행(uid 199 → 로스터 147)을 그대로 썼고 **그 대응이 맞는지는 검증하지 않았다**.
''')
open('Docs/research/SKILL_COVERAGE_BY_GRADE.md','w',encoding='utf-8').write('\n'.join(L))
print(len('\n'.join(L)),'chars; extras',n,'low',sum(1 for r in R if r['확신도']=='낮음'))
