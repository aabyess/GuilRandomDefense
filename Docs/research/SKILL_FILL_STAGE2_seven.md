# 스킬 살리기 2단계 — 미발동 7종 원작 값 (2026-09-30, Blender 세션, 읽기 전용 조사)

근거는 `Tools/w3x/원본/war3map_new.j`(줄 번호 = `grep -n` 기준)와 `war3map_new.w3a`·`w3u` 직접 디코드뿐이다. Docs는 안 봤다.
지시서의 힌트는 검증했다. **힌트 셋이 틀렸다**(아래 ⚠️ 표시).

## 먼저 읽을 것 — 공통 발견 5건

1. **발동 판정은 능력이 아니라 유닛 종류로 붙는다.** `Trig_AttackHashTable_Actions`(j:3392~)가 `SaveTriggerHandle(udg_HashAttack,'유닛ID',0,gg_trg_…)`로 유닛마다 공격 트리거를 건다. 초월 4종의 ACbh 능력(A0HG·A069·A0EQ·A0GR)은 `atar=none`·`Hbh1=0`인 **UI 버튼**이다. 이 능력 레벨을 읽는 곳은 트리거뿐이다.
2. **⚠️ 이상혁 = 도플라밍고(h01L)이지 상디가 아니다.** 에셋의 skillName은 `A06D !디아블 잠브`(상디 h02I의 능력)인데, description과 더미채널 에셋은 둘 다 `Trig_Unique12`(도플라밍고)다. 상디의 실제 트리거 `Trig_Unique1`은 이미 **배현진** 에셋에 옮겨져 있다. 그래서 이상혁은 A06D가 아니라 Unique12로 채워야 한다.
3. **원작 유닛 하나가 우리 유닛 둘로 쪼개져 있다.** 후지토라(H08X)는 A0GR→이태훈, Huji01/03→양재모로 갈렸다. 핸콕(h05C)은 A0J7·A0IN·A0OK→조세민, skill_9/10/arrow_h2→김영원으로 갈렸다. 쪼개진 쪽 게이지(마나)나 버프 게이트(B03M)는 **다른 유닛 인스턴스끼리 공유되지 않는다.** 핸콕 B03M 게이트는 opener(김영원)와 소비자(조세민 A0OK)가 서로 다른 유닛이라 조세민 쪽에서는 영영 안 열린다.
4. **우리 `Enemies` 범위의 중심은 시전자다**(`UnitAttacker.cs:1280`, `transform.position`). 원작 7종은 전부 **대상(맞은 적) 지점 중심**이다. 또 `range`는 원작 WC3 단위 그대로(예 450) 넣는 게 기존 에셋 관례인데, 코드는 이 값을 변환 없이 월드 거리로 쓴다. 두 문제 모두 이번 7종만이 아니라 전체에 걸려 있다. 아래 제안의 range는 관례대로 WC3 값으로 적는다.
5. **effect.chance는 대상마다 따로 굴린다.** 그래서 「레벨2에서 25% 확률로 운석」 같은 **시전 단위 확률**을 effect.chance에 넣으면, 적마다 25%씩 맞는 다른 동작이 된다. 레벨별 `triggerChance`를 쓰는 별도 SkillData로 떼는 쪽을 제안한다(A0GR).

표기: `NORMAL/UNIVERSAL` → 우리 `damageType: 2(AP)`·`attackType: 7(Spells)`(ATTACK_TYPE_NORMAL=Spells 행, UNIVERSAL=방어무시). `CHAOS/NORMAL` → `damageType: 1(AD)`·`attackType: 5(Chaos)`. `HERO/NORMAL` → `AD`·`attackType: 4(Hero)`.
RRD(a,b,c,min,max,…) = `c × GetRandomReal(min,max)`(j:3308) → 우리 `multiplier: c, randMin: min, randMax: max`.

---

## 1. A095 — 희귀함_박민수 (원작 제프 h02G, `Trig_Unique2`)

- **원작 동작**: 평타마다 `GetRandomInt(1,10)==3`(**10%**)이면 더미 e031(A095 보유)을 만들어 공격자에게 `bloodlust`를 건다(j:14172). A095 = Ablo: `Blo1=4.0`(**공격속도 +400%**), `Blo2=0`(이동속도 +0), `Blo3=0`(크기), `adur=ahdu=3.0`초, 대상 self/friend.
  - WC3 공속 배율 상한은 5.0(+400%)이다. 이 버프 하나만으로 상한에 닿는다(엔진 규칙, 확신도 likely).
  - WC3 블러드러스트는 **다시 걸면 시간만 갱신되고 겹치지 않는다.**
- **우리 에셋**: `Assets/Data/UnitSkills/SkillData_원작능력_희귀함_박민수.asset` → 희귀함_박민수
- **현재**: triggerType 0(OnHitChance), triggerChance 1.0, `effects: []`. A095(4.0)는 「피해 필드 아님」이라 2026-09-06에 지워졌다.
- **제안** (레벨 1개):
  - `triggerChance: 0.1`
  - effect `{kind: 11 AttackSpeedBuffPercent, target: 0 Self, multiplier: 4.0, duration: 3, buffId: "B03E"}`
- **⚠️ 구현 위험**: `AddAttackSpeedBuffPercent`(UnitAttacker.cs:317)는 걸 때마다 `activeBuffs`에 **새 항목을 추가**한다. 공속 5배면 3초에 평타가 ~15번이고, 그 사이 10%가 또 뜰 확률은 약 80%다. 그러면 5×5=25배로 곱해져 쌓인다. 원작처럼 「같은 buffId면 갱신」이나 공속 상한 5.0이 없으면 이 에셋은 넣지 말 것.
- **근거**: w3a A095 `Blo1=4.0 Blo2=0 Blo3=0 adur=3 ahdu=3 acdn=0.5 abuf=B03E`, w3u e031 `uabi=A095,Aloc,Avul`, j:14172 `if GetRandomInt(1,10)==3 … IssueTargetOrderById(…"bloodlust",GetAttacker())`, 해시 j:3394 `'h02G'→gg_trg_Unique2`
- **확신도**: confirmed(값·확률). 상한 5.0은 likely(엔진 기본값).

## 2. A06D — 희귀함_이상혁 → 실제로는 도플라밍고 `Trig_Unique12`

- ⚠️ 힌트의 「A06D 피해가 Unique12_Func006A에 있다」는 **반만 맞다**. Func006A는 도플라밍고(h01L)의 것이고, A06D(상디)의 것이 아니다. 해시 j:3394 `'h01L'→Unique12`, `'h02I'→Unique1`.
- **원작 동작 (Unique12, j:14195)**:
  - 평타마다 `GetRandomInt(1,11)==3`(**1/11 ≈ 9.09%**)이면 맞은 적 위치 **반경 450** 안의 적 전원에게 `RRD(…,6000,1.0,1.0,NORMAL,UNIVERSAL)`(j:14194 Func006A)를 준다.
  - e059 더미는 오더 없이 만들어지기만 한다(시각용 — 이전 감사 c00f472와 같은 결론).
  - 스턴·둔화는 없다.
- **우리 에셋**: `SkillData_원작능력_희귀함_이상혁.asset` → 희귀함_이상혁 (더미채널 `…_이상혁_1.asset`도 같은 유닛이고 비어 있음)
- **현재**: triggerChance 0.0(잠금), effects 비어 있음. 도플라밍고의 실제 피해가 **어디에도 없다**(Unique12 문자열로 grep하면 이 두 파일뿐).
- **제안** (레벨 1개, 원작능력 에셋 쪽에만. 더미채널 쪽은 빈 채로 둘 것 — 이중 계상 방지):
  - `triggerChance: 1/11 = 0.0909`, `range: 450`
  - effect `{kind: 0 Damage, basis: 0 Flat, target: 2 Enemies, damageType: 2 AP, attackType: 7 Spells, multiplier: 6000, randMin: 1, randMax: 1}`
  - skillName을 `Trig_Unique12`로 바로잡을 것(A06D는 상디 것).
- **덤 — 배현진(상디 Unique1)이 틀려 있다**: 원작은 `GetRandomInt(1,5)==3`(**20%**), **단일 대상**, `RRD(…,5000,0.8,1.5,NORMAL,UNIVERSAL)`(j:14171)이다. 우리 에셋은 triggerChance 1.0(매 타), AD·Normal, 배율 범위 없음이라 5배 과다 발동이다. → `triggerChance 0.2`, `target 3 SingleTarget`, `AP/Spells`, `randMin 0.8 randMax 1.5`.
- **A06D 자체**는 w3a `Hbh1=0 Hbh3=15000 atar=none`인 UI 껍데기다. 툴팁(20%·5000·단일)은 Unique1을 설명한다.
- **확신도**: confirmed

## 3. A0HG — 초월_김만경_AD (원작 아카이누 H095, `Trig_Akainu_Attack` → `Akainu_01`)

- **원작 동작**:
  - **게이트**: 평타마다 마나==135면 발동하고 마나를 0으로 되돌린다. 아니면 마나+1(j:16088). 0에서 시작하면 **136타째마다** 발동한다.
  - 예외: 공격자 체력==60(다른 스킬의 상태 플래그) **이면서** 대상 PointValue==200(보스)이면 그 타는 건너뛰고 마나 135를 유지한다(Func040C·Func040Func004C). 우리에겐 대응 상태가 없으니 무시해도 된다.
  - **유성 수** = `5 + A0HG레벨`(j:16125 `integerB < 5+레벨`, 0부터). Lv1=**6**, Lv2=7, Lv3=8, Lv4=9. ⚠️ 툴팁은 7/8/9/10이라 코드보다 하나 많다. **코드가 맞다.**
  - 각 유성은 0.1초 간격으로 떨어진다. 낙하점은 대상 위치에서 **0~700 무작위 거리·무작위 방향**이다. 낙하 1초 뒤 `UnitDamagePointLoc(…,465,지점,1000000,NORMAL,UNIVERSAL)`(j:16151 Shoot)이 들어간다. 즉 **반경 465 · 1,000,000 · 방어무시**다(툴팁 999999).
  - 스턴·둔화는 없다. 시전 중 0.02+… 동안 본체를 멈춘다(PauseUnit)(연출).
  - A0HG는 `alev=4`로 4레벨까지 있다(T_Ability_hero j:20568/20570에서 +1). 우리 에셋은 2레벨이다.
- **우리 에셋**: `SkillData_원작017_H095.asset` → 초월_김만경_AD
- **현재**: triggerType 0(OnHitChance), 두 레벨 모두 triggerChance 1·`effects: []`.
- **제안**:
  - `triggerType: 3 OnHitCount`, `gaugeKind: 0 Mana`, `hitCountThreshold: 135`, `resetTo: 0`
    - 기존 관례는 「MANA게이지N」→threshold N이다(예 `게이트_초월_최상호_AD_3c5f8bd9` 115). 우리 코드는 먼저 ++하고 `>=`로 비교하므로, 원작과 똑같은 136타 주기를 원하면 136이다. 차이는 0.7%. PM 판단.
  - Lv1: `range: 465`, effect `{Damage, Flat, Enemies, AP, Spells, multiplier: 1000000, hitCount: 6}`
  - Lv2: 같고 `hitCount: 7`
  - ⚠️ 원작 유성은 700 안 무작위 지점에 떨어지므로, 처음 맞은 적이 유성 하나에 맞을 확률은 약 465/700 ≈ 0.66(거리가 [0,700] 균등이라서)이다. hitCount 6을 전부 한 범위에 넣으면 **과대**다. 기댓값을 맞추려면 hitCount를 그대로 두고 multiplier를 ×0.66(=660,000)으로 하는 근사가 있다. PM 판단.
- **⚠️ 게이지 공유 충돌**: 같은 유닛의 `SkillData_회수_초월_김만경_AD_0ac0451e.asset`도 마나 threshold 135다. 우리는 같은 타에 둘 다 발동한다. 원작 Attack 트리거에서 마나==135 분기는 **Akainu_01만** 부른다. Akainu_02(체력 최대치 비례)는 마나≠135일 때 B06B/7.5%(`<76/1000`) 분기에서 불린다. 그 회수 에셋의 「MANA게이지135 AND B06B」 라벨은 재확인이 필요해 보인다(이번 범위 밖, 줄: j:16088).
- **근거**: w3u H095 `umpm=135`, j:16088, j:16125, j:16128 Stage4 `PolarProjectionBJ(locE,GetRandomReal(-700,700),GetRandomDirectionDeg())`, j:16151 `UnitDamagePointLoc(…,0,465.00,…,1000000.00,ATTACK_TYPE_NORMAL,DAMAGE_TYPE_UNIVERSAL)`, 해시 j:3415
- **확신도**: confirmed(게이트·수·피해). 유성 분산 보정만 근사.

## 4. A069 — 초월_박기찬_AD (원작 프랑키 H08Y, `Trig_Franky_Attack` → `Franky_Skill_1` 「꾸드방」)

- **원작 동작**:
  - 평타마다 `GetRandomInt(1,10)==2`(**10%**, j:16645)다. ⚠️ 툴팁 5%는 틀렸다.
  - **레벨 판정은 A069 레벨이 아니라** `udg_bool_franky_tr[플레이어]`다. 이 값은 A069를 올릴 때(T_Ability_hero, FOOD_USED≥1 소모, `IncUnitAbilityLevelSwapped('A069')` 직후, j:20508) true가 된다. 그래서 A069 Lv2 ⇔ true이고, 1:1이다.
  - Lv1 (false): 맞은 적 위치 **반경 400** 적 전원에게 `RRD(400000,0.8,1.5,NORMAL,UNIVERSAL)`, 그리고 **맞은 대상 하나에 추가로** `RRD(200000,0.8,1.5,…)`(j:16666)
  - Lv2 (true): **반경 500** · `500000` 전원, 대상에 추가 `250000`(같은 배율 범위)
  - 스턴·둔화는 없다.
- **우리 에셋**: `SkillData_원작003_H08Y.asset` → 초월_박기찬_AD. 같은 유닛의 `게이트_초월_박기찬_AD_a8343962`(Franky_Skill_Mana1, 마나100)와 `_b3948c74`(Franky_misiile_re, 1/14)는 **다른 트리거**라 중복이 아니다.
- **현재**: 두 레벨 모두 triggerChance 1·`effects: []`.
- **제안**:
  - Lv1: `triggerChance: 0.1`, `range: 400`, effects
    - `{Damage, Flat, Enemies, AP, Spells, multiplier: 400000, randMin 0.8, randMax 1.5}`
    - `{Damage, Flat, SingleTarget, AP, Spells, multiplier: 200000, randMin 0.8, randMax 1.5}`
  - Lv2: `triggerChance: 0.1`, `range: 500`, 위와 같은 둘에서 `500000` / `250000`
- **근거**: j:16645 `GetRandomInt(1,10)==2 … Franky_Skill_1`, j:16666 두 분기(`GetUnitsInRangeOfLocMatching(500.00|400.00,…)`, RRD 500000/400000, 대상 RRD 250000/200000), j:20508 `IncUnitAbilityLevelSwapped('A069') … set udg_bool_franky_tr[…]=true`, w3a A069 `alev=2 Hbh1=0 Hbh3=0 atar=none`
- **확신도**: confirmed

## 5. A0EQ — 초월_유재헌_ADAP (원작 시라호시 H08U, `Trig_Sirahoshi_Attack` 「해류조정」)

- **원작 동작**: 평타마다 `GetRandomInt(1,9)==5`(**1/9 ≈ 11.1%**, j:16229)다. 툴팁은 12%. 분기는 `GetUnitAbilityLevelSwapped('A0EQ',공격자)==2`다.
  - Lv1: 대상 위치에 e04V(Player 8, 아군 측)가 `stomp` → **A06W**(AOws): `Wrs1=380000` 피해, `aare=600`, **스턴 adur 2.15초**(ahdu 1.18).
  - Lv2: e0KA(Player 4) 3개 중 첫 번째만 `stomp` → **A0WW**(AOws): `Wrs1=450000`, `aare=600`, **스턴 adur 2.35초**(ahdu 2.35). e04V는 만들기만 하고 오더가 없다(시각).
  - Player(4)·(8)은 InitStart에서 적 웨이브 쪽(Player 5·6)과 UNALLIED가 된다. 적 웨이브는 Player(5)가 만든다(n00x). → **적에게 실제로 걸린다.**
  - 워 스톰프 피해는 엔진 주문 피해(spells 행·방어 무시)다(엔진 기본, likely).
- **우리 에셋**: `SkillData_원작008_H08U.asset` → 초월_유재헌_ADAP. Sirahoshi 트리거는 다른 에셋 어디에도 없다(중복 없음).
- **현재**: 두 레벨 모두 triggerChance 1·`effects: []`.
- **제안**:
  - Lv1: `triggerChance: 0.1111`, `range: 600`, effects
    - `{Damage, Flat, Enemies, AP, Spells, multiplier: 380000}`
    - `{kind: 1 Stun, target: 2 Enemies, duration: 2.15}`
  - Lv2: `triggerChance: 0.1111`, `range: 600`, `450000` + `Stun 2.35`
  - 적이 일반 유닛이므로 adur을 쓴다(보스도 영웅이 아니라 Large 방어라 adur).
- **근거**: j:16229 전문(분기·e04V/e0KA·`stomp` 오더 위치), w3a A06W `Wrs1=380000 aare=600 adur=2.15 ahdu=1.18`, A0WW `Wrs1=450000 aare=600 adur=2.35 ahdu=2.35`, w3u e04V `uabi=A06W`, e0KA `uabi=A0WW`, alliance j:3482 부근 `SetPlayerAllianceStateBJ(Player(4)|Player(8),Player(5),bj_ALLIANCE_UNALLIED)`
- **확신도**: confirmed(값). 피해 행(Spells)은 likely.

## 6. A0GR — 초월_이태훈_AP (원작 후지토라 H08X, `Trig_Huji_Attack` → `Huji_02` 「중력장」)

- **원작 동작**: 평타마다 `GetRandomInt(1,24)==10`(**1/24 ≈ 4.17%**, j:16532)다(툴팁 4.15%와 일치). 마나 게이지(140)와는 별개로 매 타 굴린다.
  - `Huji_02`(j:16561)는 **맞은 적에 일회용 피해 이벤트 트리거(Huji02DM)를 건다.** 그 적이 H08X에게 다음 피해를 받는 순간(= 이 평타의 피해) 다음이 일어난다.
    - 그 적 위치 **반경 485** 적 전원에게 `RRD(GetEventDamage()*5 + 450000, CHAOS, NORMAL)`(j:16571). 평타 피해의 5배 + 45만, 물리.
    - 같은 적들에게 `AId1` 레벨 +3(j:16571). w3a AId1 「방어력 감소」는 `Idef` Lv L = −(L−1)이고 alev 76이라 −75에서 멈춘다. → **방어 −3, 영구 누적, 상한 −75**
    - e07D(Player 4)가 `stomp` → **A0OX**(AOws): `Wrs1=280000`, `aare=475`, 스턴 `adur` **비어 있음 → 스톡 AOws Lv1 기본값**. 스톡 War Stomp Lv1 일반 지속은 3.0초로 알고 있다(likely). 운석 쪽 A11L도 adur이 비어 있는데 툴팁이 「3초 스턴」이라 이 추정을 받쳐 준다. 본체 툴팁 「2.5초」와는 맞지 않는다.
    - e07B도 A0OX를 갖고 만들어지지만 **오더가 없다**(시각).
  - **Lv2 추가**: 발동 시 `GetRandomInt(1,4)==3`(**25%**)이면 `Huji_meteor`(j:16636)가 돈다. 1초 뒤 0.28초 간격으로 **5타** × 반경 535 `RRD(315000, CHAOS, NORMAL)`(j:16630)가 들어간다. 이어서 e079 `stomp` → **A11L**: `Wrs1=1,500,000`, `aare=535`, 스턴 adur 비어 있음(→ 스톡, 3.0초 추정).
    - ⚠️ 툴팁의 「운석 +3 아머브레이크」는 **코드에 없다**(meteor에 AId1 없음).
- **우리 에셋**: `SkillData_원작005_H08X.asset` → 초월_이태훈_AP. Huji_02는 다른 에셋 어디에도 없다. Huji01/Huji_03(마나140)은 **양재모**에 있다(공통 발견 3).
- **현재**: 두 레벨 모두 triggerChance 1·`effects: []`.
- **제안**:
  - Lv1: `triggerChance: 0.04167`, `range: 485`, effects
    - `{Damage, basis: 5 ReceivedDamage, Enemies, AD, Chaos, multiplier: 5, bonus: 450000}`
    - `{kind: 2 ArmorBreak, Enemies, multiplier: 3, duration: 0}` (영구. 우리 AddArmorShred의 상한이 75인지는 확인 필요)
    - `{Damage, Flat, Enemies, AP, Spells, multiplier: 280000}` (워 스톰프)
    - `{Stun, Enemies, duration: 3.0}` (스톡 추정 — 확신도 likely)
    - 스톰프 반경은 475로 485와 거의 같아서 485 하나로 묶었다.
  - Lv2: Lv1 효과 전부 그대로.
  - 운석은 **별도 SkillData**(같은 유닛에 하나 더 배정)로 뗀다. 이유는 공통 발견 5(effect.chance는 대상별이라 「25% 확률 운석」을 못 담는다).
    - `triggerType 0`, levels[0] `triggerChance 0, effects []`
    - levels[1] `triggerChance: 1/96 = 0.01042`, `range: 535`, effects
      - `{Damage, Flat, Enemies, AD, Chaos, multiplier: 315000, hitCount: 5}`
      - `{Damage, Flat, Enemies, AP, Spells, multiplier: 1500000}`
      - `{Stun, Enemies, duration: 3.0}`
    - 기댓값은 원작과 같다. 「중력장과 같은 타에만 뜬다」는 동시성은 잃는다.
- **근거**: j:16532 `GetRandomInt(1,24)==10 → Huji_02`, j:16561, j:16570 `GetUnitTypeId(GetEventDamageSource())=='H08X'`, j:16571 `RRD(…,((GetEventDamage()*5.00)+450000.00),1.00,1.00,ATTACK_TYPE_CHAOS,DAMAGE_TYPE_NORMAL)` + `SetUnitAbilityLevelSwapped('AId1',…,+3)` + 485 범위 + e07D stomp, j:16546~16547 `A0GR==2 AND GetRandomInt(1,4)==3`, j:16630 `RRD(…,315000.00,…CHAOS,NORMAL)` 535 범위 Stage 1~5, w3a A0OX `Wrs1=280000 aare=475 ahdu=0.45 adur=없음`, A11L `Wrs1=1500000 aare=535 adur=없음`, AId1 `Idef 0,-1,…` `alev=76`, w3u e07D/e07B `uabi=A0OX`, e079 `uabi=A11L`
- **확신도**: 피해·확률·방깎은 confirmed. 스턴 3.0초는 likely(스톡 기본값 추정).

## 7. A0J7 — 영원_조세민 (원작 보아 핸콕 h05C, 엔진 Bash 「쿠사의 패기」)

- **원작 동작**: A0J7은 h05C 자신의 `uabi`에 든 **엔진 Bash**(ACbh)다. `atar=air,enemies,ground`라 실제로 돈다. 트리거가 이 능력을 건드리지는 않는다(j에서 'A0J7' 참조 0건).
  - `Hbh1` 비어 있음 → **스톡 ACbh 기본 확률**. 툴팁 「15%」와 맞는다. 스톡값 15%는 likely.
  - `Hbh2=8.0`(Damage Multiplier), `Hbh3=500000`(추가 피해), `adur=3.0`(ahdu 1.5). 적은 일반 유닛이므로 **3초 스턴**.
  - Hbh2가 Bash에서 실제로 곱해지는지는 **unknown**이다. 툴팁은 「8배 크리티컬 + 50만」이라고 한다.
  - Bash 추가 피해는 평타와 같은 공격타입(h05C `ua1t=hero`)·물리다.
  - 이 능력은 핸콕 마나 175 게이지와 **무관하게** 매 타 굴린다.
- **툴팁 두 번째 줄**(「7.5%·460범위 85만 물리 + 15만 마법 + 이속 60% 감소」)은 A0J7이 아니라 **`hancock_skill_9`** 다(j:19560: 마나≠175일 때 `GetRandomInt(1,15)==3` = 6.67%). 반경 450 `RRD(850000,1.0,1.5,HERO,NORMAL)`(j:19585)이고, e08F `creepthunderclap` → **A0C6**(ACt2): `Ctc1=150000`, `aare=600`, **`Ctc3=0.6`(이속 −60%)**, `Ctc4=0`(공속 감소 없음), `adur=2.5`(ahdu 1.25)다.
  - 이 트리거는 이미 **김영원** 에셋 `게이트_영원_김영원_5a6f48de`(850000, 0.0667, 450)로 옮겨져 있다. **빠진 것은 선더클랩 15만과 둔화뿐**이다.
- **우리 에셋**: `SkillData_원작능력_영원_조세민.asset` → 영원_조세민.
  - `SkillData_원작능력_영원_최상호.asset`의 skillName도 「A0J7」이지만 내용은 카벤딧슈 트리거다(A0J7 Bash 값은 없다. 이름만 남은 것).
  - 조세민·최상호 유닛의 crit 필드는 전부 0이다.
- **현재**: 조세민 에셋은 triggerChance 1.0·`effects: []`다(「대표를 최상호로」 하며 회수했다는데, 최상호에도 Bash 값은 없다 → **A0J7은 지금 어디에도 없다**).
- **제안 (조세민)**: `triggerChance: 0.15`, effects
  - `{Damage, basis: 3 CasterAttackPower, SingleTarget, AD, Hero, multiplier: 7, bonus: 500000}`
    - 총 8배를 뜻한다. 이 피해는 평타 **뒤에 추가로** 들어가서 1배가 이미 나가 있으므로 7이다. Hbh2를 무시하는 해석이면 `multiplier: 0`.
  - `{Stun, SingleTarget, duration: 3.0}`
  - 대안: 유닛 crit 필드(`critChance 0.15, critDamageMultiplier 7, critBonusDamage 500000, critStunDuration 3`)도 원작 Bash 경로(평타 판정·isAbilityDamage false)와 정확히 같다. 어느 쪽으로 넣을지는 PM 판단.
- **제안 (김영원 skill_9 보완, 선택)**: `게이트_영원_김영원_5a6f48de` 레벨에 다음을 추가한다.
  - `{Damage, Flat, Enemies, AP, Spells, multiplier: 150000}`
  - `{kind: 13 Slow, Enemies, multiplier: 0.4, duration: 2.5}`
  - 기존 850000 효과에 `randMin 1.0 randMax 1.5`
  - 선더클랩 반경은 600, RRD는 450이다. 에셋 range는 하나라 450을 유지하는 게 보수적이다.
- **A0OK 진입 게이트(요청분, 간단히)**: `Trig_Hancock_Attack`(j:19560)에서 마나≠175 **이면서** 공격자에게 버프 **B03M**이 있으면 **매 타 확률 없이** `arrow_h2`(j:19567)가 돈다. e0CZ가 `carrionswarm`(A0OK: `Ucs1=400000`/유닛, 길이 `Ucs3=1150`, 폭 220→395, `Ucs2=7억` 상한)을 쏘고, 대상에게 추가로 `RRD(250000,1.0,1.5,NORMAL,UNIVERSAL)`를 준다.
  - B03M은 **A0OP**(e0E3 bloodlust, 매 타 `1/15`, 0.7초, `Blo1=−0.15`)나 **A0RO**(액티브, 쿨 9초, 0.7초)가 건다.
  - 즉 잠금 에셋 `더미채널_영원_조세민_79행_lock`의 정확한 게이트는 `triggerChance 1.0 + requiredBuffId "B03M"`이다.
  - 다만 B03M opener(`버프게이트_영원_김영원_B03M`)가 **김영원**에 있어서, 조세민에게 게이트를 걸면 영영 안 열린다(공통 발견 3). 풀려면 opener를 조세민에도 두거나 핸콕 몫을 한 유닛으로 모아야 한다.
- **근거**: w3u h05C `uabi=A0RN,A0J8,A0J7,A0IN,A0IM,A0RO,Avul ua1t=hero umpm=175`, w3a A0J7 `Hbh2=8 Hbh3=500000 adur=3 ahdu=1.5 atar=air,enemies,ground Hbh1=없음`, j:19560, j:19585, w3a A0C6 `Ctc1=150000 Ctc3=0.6 Ctc4=0 aare=600 adur=2.5`, A0OK `Ucs1=400000 Ucs2=7e8 Ucs3=1150 Ucs4=395 aare=220`, A0OP `abuf=B03M Blo1=-0.15 adur=0.7`, j:19567 `IssuePointOrderByIdLoc(…"carrionswarm"…)` + RRD 250000
- **확신도**:
  - 스턴 3초·추가 50만은 confirmed.
  - 15%는 likely(스톡 기본값, 툴팁과 일치).
  - 8배 곱셈은 unknown.
  - skill_9 둔화 0.4·2.5초는 confirmed.

---

## 요약 표

| 능력 | 우리 유닛 | 발동 | 핵심 값 | 스턴/둔화 | 확신도 |
|---|---|---|---|---|---|
| A095 | 희귀함_박민수 | 10% | 자기 공속 +400%, 3초 | — | confirmed(겹침 버그 주의) |
| (A06D→Unique12) | 희귀함_이상혁 | 1/11 | 450 범위 6000 AP | — | confirmed |
| A0HG | 초월_김만경_AD | 마나 135 | 유성 5+Lv개 × 465 범위 100만 AP | — | confirmed |
| A069 | 초월_박기찬_AD | 10% | 400/500 범위 40만/50만(×0.8~1.5) + 대상 20만/25만 | — | confirmed |
| A0EQ | 초월_유재헌_ADAP | 1/9 | 600 범위 38만/45만 | 스턴 2.15/2.35 | confirmed |
| A0GR | 초월_이태훈_AP | 1/24 | 485 범위 평타×5+45만 AD·방깎3·스톰프 28만 (+Lv2 운석 1/4) | 스턴 3.0(스톡) | confirmed / 스턴 likely |
| A0J7 | 영원_조세민 | 15%(스톡) | 단일 8배(?)+50만 | 스턴 3.0 | likely |

## 조사 범위 메모

- j는 `function … endfunction`을 통째로 떼어(\r 무시) 함수 8,156개로 색인한 뒤 읽었다. 각 유닛은 해시테이블 줄로 공격 트리거를 특정했다.
- 능력 ID를 j에서 grep한 결과: A095·A06D·A0J7·A0OK는 0건이다(유닛 트리거·엔진 경유). A0HG·A069·A0EQ·A0GR은 레벨 판정·승급에서만 나온다.
- 다른 에셋과의 중복은 트리거 이름·유닛 ID로 grep해서 확인했다. Akainu_01·Franky_Skill_1·Sirahoshi_Attack·Huji_02·Unique12(Func006A)는 기존 에셋에 없다. Unique1은 배현진에, hancock_skill_9/10과 arrow_h2는 김영원에 있다.
- 스톡 기본값에 기댄 곳: AOws adur(A0OX·A11L)와 ACbh Hbh1(A0J7). 둘 다 likely로 표시했다.
- 스크래치 스크립트만 썼고 repo는 이 파일 외에 건드리지 않았다.
