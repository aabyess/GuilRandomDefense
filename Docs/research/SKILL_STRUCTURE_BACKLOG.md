# 구조 칸 백로그 — 원작 스킬 중 지금 축으로 안 담기는 구조 (2026-09-30 밤, 구현담당1)

집계: `python3 Tools/skill_coverage/structure_backlog.py --md` — 문서가 아니라 `war3map_new.j·w3a·w3u`에서 센다.
대응표에 있는 원작 유닛 199(뿌리 트리거 없는 32 포함)의 평타 트리거·시전 트리거에서 호출 폐쇄를 따라가 함수 본문의 표식을 찾았다.

⚠️ 읽는 법
- **표식 수 = 상한**이다. 표식이 있어도 실효 없는 줄일 수 있고(atar=none · 값 0), 이미 근사해 넣은 것도 섞여 있다. 「우리에 없다」를 뜻하지 않는다.
- 한 트리거가 여러 구조에 걸린다(아카이누 지대 = 지대 + 더미 오라 + 움직이는 더미).
- 합친 로스터는 걸린 원작 유닛 중 하나만 있어도 센다.
- 못 잡는 것: 대상별 1회 표식(PropWindow·TurnSpeed·FlyHeight 꼬리표) · 「버프 동안만 게이지 증가」 · 소모형 게이지 · 주변 적 수 계수(표식 0 — 식이 변수로 넘어가 정규식에 안 걸림, FILL_LIST엔 키드 1건).

## 1. 종류별 표

| # | 구조 | 표식 | 원작 유닛 | 로스터 | 지금 상태 | 설계 한 줄 | 공수 |
|---|---|---|---|---|---|---|---|
| 1 | 지연·단계 피해 | 96 | 68 | 57 | 즉발로 넣고 있다 | 구조 아님 — 총 피해·횟수만 맞으면 된다(0.1초 간격은 시각) | 0 |
| 2 | 레벨·해금·T강화 갈래 | 58 | 34 | 33 | 없음(전부 레벨 1·미해금 쪽으로 고정) | 시전자 쪽 「능력 보유/레벨」 상태 + 그걸 올리는 출처(T특성·아이템·히든 시전)가 먼저 있어야 한다 | 큼 — 출처 시스템 셋이 선행 |
| 3 | 배타 분기 한 굴림 | 34 | 29 | 28 | 독립 굴림 둘로 근사(기댓값은 같게 적음) | `exclusiveGroup`: 같은 묶음은 한 평타에 하나만(앞이 터지면 뒤는 건너뜀) | 코드 작음 · 데이터는 트리거 28개를 하나씩 읽어야 함 |
| 4 | 움직이는 더미 | 33 | 27 | 27 | 대상 중심 원으로 근사 | 5번과 같은 축(선 모양) + 「지나가며 여러 번」은 횟수로 | 5번에 묶음 |
| 5 | 장풍 직선(carrionswarm 등) | 20 | 19 | 19 | 대상 중심 원으로 근사 | `aoeShape = Line`: 시전자→대상 방향, 길이·폭은 w3a(거리·aare) | 중 — 코드 작음 · 에셋 19 |
| 6 | 시간제 더미 오라 | 54 | 24 | 22 | 일부는 시한 효과(duration)로 넣음 | 8번 지대에 「틱마다 이감·방깎 다시 걸기」를 얹는다 | 중 — 8번 뒤 |
| 7 | 소환체·분신 | 51 | 18 | 17 | 없음 | 소환체 = 수명 있는 임시 유닛(평타·광역을 w3u에서) — 모델·유닛 수 상한·판매/조합 제외가 얽힌다 | 큼 — 설계만 |
| 8 | 주기 피해 지대(ANpi 더미·눈보라·스탬피드) | 23 | 13 | 13 | 없음(아카이누는 화력 비중 큼) | `SkillEffectKind.DamageZone`: 중심에 반경·틱 간격·수명 지대를 세우고 틱마다 피해 | 중 — 코드 + 에셋 13 |
| 9 | 평타 다중 대상(Aroc) | 11 | 11 | 11(실효 7) | 없음 | `UnitData.attackExtraTargets`·반경: 평타가 주변 N마리에게도 | 작음 — ⚠️ Efk3이 「추가」인지 「총」인지 엔진 동작 미확인 |
| 10 | 영웅 능력치 항 | 31 | 13 | 10 | 축은 있음(STR/AGI/INT basis) | 원작 영웅 능력치 크기(레벨·연구)와 우리 값이 맞는지 확인이 먼저 | 조사 |
| 11 | 토글·bool 갈래 | 12 | 11 | 10 | 없음 | 2번과 같은 선행(T특성·아이템) | 2번에 묶음 |
| 12 | 형태 전환·각성 | 22 | 17 | 9 | 없음(한 형태로 고정) | 「각성 모드」: N초 동안 평타 주기·공격 타입·스킬 목록을 다른 묶음으로 | 큼 — 설계만 |
| 13 | 「적이 근처에 오면」 | 4(+1) | 4 | 4 | 없음 | `SkillTriggerType.OnEnemyEnterRange`: 반경 안에 처음 들어온 적마다 한 번 | 작음 — 코드 + 에셋 4 |

표식 0: 대상 주기 피해(독 계열 더미) — 원작에 없다.

## 2. 구현 순서 (많이 걸린 순, 한 줄 설계가 되는 것만)

1·2·7·11·12는 위 이유로 구현에서 뺀다(1은 구조 아님, 나머지는 선행 시스템 또는 설계 필요).

1. **8 주기 피해 지대** — 값이 w3a에 그대로 있다(Eim1 피해/틱 · adur 틱 간격 · aare 반경 · 수명은 트리거의 UnitApplyTimedLife). 6번(더미 오라)이 같은 그릇을 쓴다.
2. **5·4 장풍 직선** — 19 + 27 로스터. 원 → 선으로 모양만 바꾼다.
3. **3 배타 분기** — 28 로스터. 기댓값이 이미 맞는 곳은 분산만 달라진다(우선순위 낮음).
4. **13 근접 진입** — 4 로스터(핸콕 석화 · 샹크스 패기 · 카타쿠리 · 에넬). 값이 트리거에 그대로.
5. **9 평타 다중 대상** — Efk3 해석이 정해지면.

## 3. 설계만 적는 것

### 레벨·해금·T강화·토글 갈래 (2·11)
원작은 시전자의 능력 레벨(`GetUnitAbilityLevel(u,'A0JU')` 등)과 udg bool(`udg_Law_Bool`)로 가지를 가른다. 올리는 출처는 셋: T특성 강화(특성 능력) · 아이템(`udg_item_*_bool`) · 다른 유닛의 시전(히든 류마 → 조로 A0GT).
- 그릇: `UnitAttacker`에 「플래그 집합」(문자열 id → 레벨). `SkillLevel.requiredFlag / forbiddenFlag`(지금 `requiredBuffId`와 같은 모양, 대상이 아닌 시전자 쪽).
- 출처: 특성 26명(이미 있음)에 「플래그 부여」 효과 · 아이템 장착 시 플래그 · 스킬 효과 `GrantFlag`(아군 대상).
- 선행 확인: 어느 플래그를 어느 특성·아이템이 올리는지 표(원작 `R_Unit_skill_tr`·`vivi_upgrade`·`item_up2`).

### 소환체·분신 (7)
버기 넷(h076~h079) · 시키 부유물(h07R·h0BH~J) · 우타 음표 병사(h04O·h088) · 조로 h07G · 카벤딧슈 h06H · 에이스 `change` 일곱 · 시노부 h086 · 써니호 h03L · 라일리 h05J · 드래곤 h08H · 토트 h085.
- 그릇: `SkillEffectKind.Summon`(UnitData 참조 · 수명 · 수 상한). 소환체는 `OwnedByPlayer`는 갖되 조합·판매·유닛 수에서 빠진다.
- 얽힘: 소환체 모델(지금 없음 — 주인 모델 축소본으로?) · 소환체의 제 스킬(h07G msplash 600) · 주인이 사라질 때 정리.

### 형태 전환·각성 (12)
AEme(metamorphosis) 아홉 능력: 도플라밍고 A0AL · 우솝 A0AF · 빅맘 A0DU · 카이도 A0ZI · 루피 A0XH · 야마토 A12B · 초록소 A15F · 마르코 A172 · 레베카 A14L.
- 그릇: `UnitData.awakenedForm`(UnitData) + `SkillEffectKind.Awaken`(지속). 지속 동안 평타 주기·공격 타입·스킬 목록을 그 형태 것으로 바꾸고 끝나면 되돌린다.
- 얽힘: 합친 로스터는 이미 두 형태의 스킬을 한 몸에 다 들고 있다 — 각성을 넣으면 그걸 다시 갈라야 한다(「합친 채 유지」 확정과 부딪침 → 사장님 판단 필요).

## 5. 구현 기록

### 8번 주기 피해 지대 (2026-09-30 밤) — `SkillEffect.zoneTickInterval·zoneRadius·zoneSpacing·zoneAtCaster` · `SkillDamageZone` · `Tools/apply_damage_zones.py`

- 그릇: kind Damage에 `zoneTickInterval > 0`이면 즉발 피해 대신 지대를 세운다. 지대는 러너(`SkillDamageZone`)가 세므로 시전자가 사라져도 수명까지 돈다. 틱마다 반경 안 적을 다시 모은다.
- 값: 전부 원본에서 유도 — 틱당 피해 Eim1 · 간격 adur · 반경 aare(더미의 ANpi 능력) · 수명 = 더미 uhpm ÷ |uhpr| − 0.405.
- ⚠️ 가정 둘(엔진 동작): 체력 0.405 아래에서 죽는다(체력 2 더미 = 1.595초, 2초 아님) · 첫 틱은 한 간격 뒤. 세우자마자 친다면 틱 +1(7 → 8, +14%).
- 넣은 곳 14 에셋 / 7 로스터: 아카이누 초월(유성 6·7개마다 · 대분화 둘 · 용암분출 둘) · 드래곤 용오름(레벨 2는 넷) · 뱌쿠야 천본앵 넷 × 세 스킬 · 에이스 변화(염제) · 에이스 영원(화염 기둥 셋 = 옛 120000 한 번 근사를 대체 · 1/35 · 마나 185) · 레이쥬 · 아카이누 히든.
- 안 넣은 것: 레이쥬 Legend19_1(e022 셋이 날아가는 경로 위 — 자리 미정, 지대당 90,000) · 눈보라·스탬피드·클러스터 로켓(채널 주문 — 아래 별도) · 더미가 같이 가진 이감 오라 A09M·A144(6번).
- 실측(`ZoneProbe`, 60초 2배속):
  - 합성 지대(간격/수명 → 틱): 0.2/0.2 → 1.00 · 0.2/1.595 → 7.00 · 0.15/1.595 → 10.00 · 0.15/0.595 → 3.00 · 0.18/0.595 → 3.00 · 0.15/3.595 → 23.00 · 0.2/2.595 → 12.00. 반경 밖(100 지대에 150 거리) 표적 피해 0. 시전자 없는 지대도 끝까지 돈다.
  - 로스터(표적 셋이 깎인 체력 중 지대 몫): 아카이누 초월 46% · 에이스 영원 23% · 에이스 변화 6% · 레이쥬 1% · 드래곤·뱌쿠야는 표적이 죽어 몫을 못 냄(지대 피해 8,060만 · 1억 4,182만) · 아카이누 히든은 쿨 98초라 판 안에 안 터짐(0).

### 5번 장풍 직선 (2026-09-30 밤) — `SkillEffect.lineLength·lineStartRadius·lineEndRadius` · `Tools/apply_line_skills.py`

- 그릇: `lineLength > 0`인 효과는 target·range와 무관하게 시전자 → 대상 방향 사다리꼴(길이 Ucs3 · 반경 aare → Ucs4) 안의 적 모두에게 걸린다.
- 넣은 곳: 있던 효과 12(단일 대상 9 · 대상 중심 원 3 → 선) + 빈 레벨 1(루치 Kick — T특성 없는 쪽 자리가 비어 있었다) + 새 에셋 2(키자루 장풍 1/10 · 이치고 월아천충 1/22).
  미호크·에이스 둘·레일리·제트·키드·핸콕 화살·레드·샹크스(희귀)·카벤딧슈(희귀)·특별 둘.
- ⚠️ 가정: Ucs3 빈칸(A0A1·A0PV) = 800(slk 미확인) · aare·Ucs4는 반경. 피해 타입은 있던 효과 것 그대로(옛 생성기가 AD로 적어 둔 것이 많다 — 더미 주문 피해 관례는 AP·Spells, 따로 볼 것).
- 안 넣은 것: 안흔함 A0A2 · 에드워드 Ed_Skill_2sc A0OZ(대상 둘레에서 안쪽으로 여러 발) · 영원_조세민 lock 에셋(확률 0) · 키드 아이템 갈래 A17P · 히든 A0B5(대응 로스터 없음) · 4번 「움직이는 더미」 27(선이 아니라 트리거가 단계마다 옮기며 RRD — 지금은 대상 중심 원 + 횟수로 들어가 있다, 하나씩 읽어야 함).
- 실측(`LineProbe`, 40초 2배속):
  - 합성 선(길이 1000 · 반경 200→400), 표적 (앞,옆): (60,0)·(600,0)·(950,0)·(500,250)·(950,±380) 맞음 / (1100,0)·(500,350)·(100,250)·(−100,0)·(950,420) 안 맞음 — 어긋남 0/11.
  - 로스터, 한 줄 표적 여덟(60부터 150 간격, 마지막 1110): 서민성(길이 1050) 2~7번째가 같은 값으로 맞고 8번째 0 · 임장혁(1100) 7번째까지 · 최상호 미호크(1250) 8번째까지.

### 3번 배타 분기 (2026-09-30 밤) — `SkillLevel.exclusiveGroup` · `Tools/apply_exclusive_groups.py`

- 표식 29 유닛을 하나씩 읽으니 **평타 트리거의 `if 굴림 … elseif 굴림` 짝은 8곳뿐**이었다(나머지는 스킬 안쪽의 시각 분기·소환·판매 트리거). 그중 7곳을 묶었다:
  흰수염(1/22 ↔ 1/8) · 센고쿠(1/10 ↔ 1/20) · 바르토로메오(1/10 ↔ 1/30) · 레이쥬(1/10 ↔ 1/19) · 레드필드(1/22 ↔ 1/20) · 상디(1/6 ↔ 1/18) · 뱌쿠야(1/10 ↔ 쿨 스킬).
- 그릇: 같은 묶음의 평타 확률 스킬은 한 평타에 하나만 — 목록에서 앞선 것의 굴림이 맞으면 뒤 것은 굴리지도 않는다. 뒤 스킬 확률은 원작 조건부 값으로 되돌렸다.
- 달라진 것: 기댓값은 그대로(주변 확률 보존) — 「같은 타에 둘 다」만 사라진다. 레이쥬만 뒤 확률이 날값(1/19)으로 적혀 있어 11% 과다였던 것이 고쳐졌다(5.26% → 4.74%).
- 안 넣은 것: 티치 1/10 안의 1/6 ↔ 5/6(T특성 변수 `TR_AddInt`가 분모 — 토글 갈래와 같이) · 시키 Legend4(elseif 쪽이 소환체).
- 실측(`ExclusiveProbe`, 80초 6배속, 평타 220~873): 일곱 로스터 모두 **같은 타에 둘 다 0회**. 뒤 스킬 시전 ÷ 평타: 흰수염 12.35%(기대 11.93) · 센고쿠 5.68%(4.50) · 바르토로메오 3.25%(3.00) · 레이쥬 5.37%(4.74) · 레드필드 5.98%(4.77) · 상디 5.84%(4.63) — 표본 19~74회라 ±1%p 안.

## 4. 부록 — 종류별 근거(스크립트 출력 그대로)

### 형태 전환·각성(변신 능력·ReplaceUnit·변신 명령)

- 불멸_신지우(h07M): Trig_Kaido_Attack_Actions: metamorphosis / uabi A0ZI(AEme)
- 불멸_정준영(h04Y): uabi A0DU(AEme)
- 불멸_정준영(h0AD): Trig_Kaido_Dragon_Attack_Actions: metamorphosis / uabi A0ZI(AEme)
- 제한_박성호(h08O): uabi A172(AEme)
- 제한_최영민(h08Q): uabi A14L(AEme)
- 제한_최영민(h09Q): uabi A172(AEme)
- 초월_구주호_AD(H0BE): uabi A12B(AEme)
- 초월_구주호_AD(H0BL): Trig_Rokugu_Attack_Actions: metamorphosis / uabi A15F(AEme)
- 초월_김경현_AP(H0B2): Trig_Snake_Attack_Actions: metamorphosis / uabi A0XH(AEme)
- 초월_조성진_AD(H09B): uabi A0AF(AEme)
- 초월_최상호_AP(H09D): uabi A0AL(AEme)
- 초월_최상호_AP(H09E): Trig_DP_Attack_Actions: metamorphosis / uabi A0AL(AEme)
- 초월_황준석_ADAP(h04P): uabi A0AL(AEme)
- 초월_황준석_ADAP(h04W): uabi A0AF(AEme)
- 초월_황준석_ADAP(h04X): uabi A0AF(AEme)
- 초월_황준석_ADAP(h04Z): uabi A0ZI(AEme)
- 초월_황준석_ADAP(h09J): uabi A0DU(AEme)

### 소환체·분신(공격하는 유닛을 만든다)

- 불멸_고도현(h04B): Trig_Shiki_Attack_Actions: h07R / Trig_Shiki_Attack_Actions: h0BH / Trig_Shiki_Attack_Actions: h0BI / Trig_Shiki_Attack_Actions: h0BJ
- 불멸_정윤식(h049): Trig_LaillySkill_Actions: h05J
- 불멸_정준영(h04D): Trig_Dragon_Attack_Actions: h08H
- 영원_김정래(h05A): Trig_Bugi_Attack_Actions: h076 / Trig_Bugi_Attack_Actions: h077 / Trig_Bugi_Attack_Actions: h078 / Trig_Bugi_Attack_Actions: h079
- 영원_김정래(h067): Trig_Uta_skill_1_double_Actions: h04O / Trig_Uta_skill_2_Actions: h04O / Trig_Uta_skill_3_mana_Actions: h088
- 영원_최상호(h05B): Trig_Cavendish_skill_Mana_Actions: h06H
- 전설적인_신문철(h039): Trig_Legend4_Actions: h07F
- 전설적인_이일중(h042): Trig_Legend30_Actions: h085
- 전설적인_임장혁(h02O): Trig_change_Actions: h05S / Trig_change_Actions: h05T / Trig_change_Actions: h05V / Trig_change_Actions: h05W / Trig_change_Actions: h07E / Trig_change_Actions: h07J / Trig_change_Actions: h07N
- 제한_이유범(h084): Trig_Sinobu_Attack_Actions: h086 / Trig_Sinobu_Skill_hp2_Actions: h086
- 초월_강주혁_AP(H0BT): Trig_Zoro_Samchun2_Actions: h07G
- 초월_박기찬_AD(H08Y): Trig_Franky_Sunny_Make_Actions: h03L
- 초월_박민수_AD(H09F): Trig_Zoro_Samchun2_Actions: h07G
- 특별함_임채준(h015): Trig_Buggi_Actions: h005
- 특수함_황길라(h05O): Trig_icebug_ship_Actions: h060
- 희귀함_노태현(h01O): Trig_change_Actions: h05S / Trig_change_Actions: h05T / Trig_change_Actions: h05V / Trig_change_Actions: h05W / Trig_change_Actions: h07E / Trig_change_Actions: h07J / Trig_change_Actions: h07N
- 희귀함_이상혁(h01L): Trig_change_Actions: h05S / Trig_change_Actions: h05T / Trig_change_Actions: h05V / Trig_change_Actions: h05W / Trig_change_Actions: h07E / Trig_change_Actions: h07J / Trig_change_Actions: h07N
- 희귀함_임채민(h02M): Trig_change_Actions: h05S / Trig_change_Actions: h05T / Trig_change_Actions: h05V / Trig_change_Actions: h05W / Trig_change_Actions: h07E / Trig_change_Actions: h07J / Trig_change_Actions: h07N

### 장풍 직선(선 모양 스톡 주문 더미 · 움직이는 더미)

- 랜덤_모몬가(h071): Trig_ichigo2_Actions: carrionswarm
- 변화됨_박은석(h05V): Trig_Transpom_Ace_bulRemake_Actions: carrionswarm
- 불멸_고도현(h04A): Trig_Ed_Skill_2sc_Actions: carrionswarm
- 불멸_박은석(h04G): Trig_Z_skill_3_Actions: carrionswarm
- 불멸_이승우(h04C): Trig_Garp_Mana2_Actions: A05Q(ANfb)
- 불멸_정윤식(h049): Trig_LaillySkill_Actions: carrionswarm
- 영원_윤현모(h059): Trig_Ace_Attack_Actions: carrionswarm
- 영원_조세민(h05C): Trig_arrow_h2_Actions: carrionswarm
- 영원_최상호(h058): Trig_Mihawk_2_Actions: carrionswarm
- 제한_강보명(h05G): Trig_RedAttack_Actions: carrionswarm
- 초월_구주호_AD(H0B5): Trig_Kizaru_Attack_Actions: carrionswarm
- 초월_임장혁_AD(H08W): Trig_Luchi_Skill_1_Kick_Actions: carrionswarm
- 초월_황준석_ADAP(H0B4): Trig_Kid_Skill_3_Actions: carrionswarm / Trig_Kid_Skill_3_item_Actions: carrionswarm
- 특별함_조세민(h00R): Trig_Speical7_Actions: carrionswarm
- 특별함_최동준(h01J): Trig_Speical9_Actions: carrionswarm
- 흔함_임장혁(h00M): Trig_common2_Actions: carrionswarm
- 흔함_최상호(h00A): Trig_common1_Actions: carrionswarm
- 희귀함_배병규(h029): Trig_Unique3_Actions: carrionswarm
- 희귀함_서민성(h023): Trig_Unique14_Actions: carrionswarm

### 움직이는 더미(단계마다 SetUnitX·이동 명령 — 장풍·회오리·걸어가는 오라)

- 랜덤_모몬가(h09K): Trig_En_nomal_Actions
- 랜덤_미도리야_이즈쿠(h0AC): Trig_Higma_Q_Actions
- 랜덤_이민형(h0BC): Trig_Tatsumaki_R_Attack_Actions / Trig_Tatsumaki_T_Explosion_by_AZ_Attack_Actions
- 랜덤_이즈미_신이치(h0BF): Trig_kikoyou_mana_Actions / Trig_kikoyou_skill_2_Actions
- 랜덤_이타도리_유지(h09W): Trig_Minato_Mana_Actions / Trig_Minato_Q2_Actions
- 랜덤_카마도_탄지로(h0A2): Trig_Ryougi_Shiki_2_Actions / Trig_Ryougi_Skill_3_Actions
- 랜덤_호시노_아이(h09N): Trig_BronyaMotar_E_Actions
- 불멸_고도현(h04B): Trig_Shiki_Lion_Actions
- 불멸_박은석(h04G): Trig_Z_skill_3_Actions
- 불멸_정윤식(h049): Trig_Kick_1_Actions
- 불멸_정준영(h04Q): Trig_Bigmam_Eat_Actions
- 영원_김정래(h067): Trig_Uta_skill_1_Actions / Trig_Uta_skill_1_double_Actions
- 영원_문필환(h057): Trig_vivi_Skill_4_Mana_Actions
- 영원_이지원(H0BK): Trig_Nika_Random_Punch_Actions
- 영원_조세민(h05C): Trig_arrow_h2_Actions
- 전설적인_임장혁(h087): Trig_Legend31_toki_Actions
- 전설적인_진연서(h09Z): Trig_Legend32_neko1_Actions
- 제한_김민규(h07I): Trig_katakuri_Skill_2_Actions
- 초월_구주호_AD(H0B5): Trig_Kizaru_01_Actions
- 초월_김경현_AP(H0B2): Trig_Snake_3_Kingkobra_Actions
- 초월_김만경_AD(H095): Trig_Akainu_02_Actions
- 초월_김민준_AP(H05N): Trig_Tasigi_02_Actions / Trig_Tasigi_03_Actions
- 초월_최상호_AD(H09A): Trig_Jimbe_Actions
- 초월_황준석_ADAP(H0B4): Trig_Kid_Skill_Mana_Tr_Actions
- 희귀함_최상호_오타쿠의길(h01Q): Trig_Unique30_Actions
- 희귀함_황준석(h02E): Trig_Unique34_Actions
- 히든_호치킨(h03Z): Trig_Akainu_02_hidden_Actions

### 주기 피해 지대·채널 다발(눈보라·화염·스탬피드·클러스터 로켓·더미 이몰레이션)

- 랜덤_한마_바키(h08I): Trig_byakuya_Chan_Actions: 더미 e0LP의 A11C(ANpi)
- 변화됨_박은석(h05V): Trig_Transpom_AceMana_Actions: 더미 e02Z의 A094(ANpi)
- 불멸_정준영(h04D): Trig_Dragon_Skill_1_Actions: 더미 e02R의 A092(ANpi) / Trig_Dragon_Skill_1_T_Actions: 더미 e02R의 A092(ANpi) / Trig_Dragon_Skill_1_T_Actions: 더미 e0IS의 A0V1(ANpi)
- 영원_윤현모(h059): Trig_Ace_Attack_Actions: 더미 e0AI의 A0OH(ANpi) / Trig_ace_skill_3_Actions: 더미 e0DZ의 A0J5(ANpi) / Trig_ace_skill_5_Actions: 더미 e0AM의 A0QM(ANpi)
- 전설적인_이재윤(h03K): Trig_Legend29_Actions: locustswarm
- 전설적인_임건웅(h033): Trig_Legend19_0_Actions: 더미 e0PU의 A08L(ANpi) / Trig_Legend19_0_Actions: 더미 e0PX의 A08L(ANpi) / Trig_Legend19_1_Actions: 더미 e022의 A08L(ANpi)
- 전설적인_정윤식(h03G): Trig_Legend2_Actions: stampede
- 초월_강주혁_AP(H08V): Trig_Nami_Skill_4_Actions: blizzard
- 초월_김만경_AD(H095): Trig_Akainu_01_Shoot_Actions: 더미 e04F의 A09L(ANpi) / Trig_Akainu_02_Actions: 더미 e04J의 A09L(ANpi) / Trig_Akainu_02_Actions: 더미 e0OD의 A09L(ANpi) / Trig_Akainu_03_Actions: 더미 e046의 A09L(ANpi) / Trig_Akainu_03_Actions: 더미 e0K4의 A09L(ANpi)
- 초월_신문철_AP(H099): Trig_Ruffy_Attack_Actions: clusterrockets
- 초월_유재헌_ADAP(H08U): Trig_Sirahoshi_skill_3_Actions: stampede
- 희귀함_박수찬(h01P): Trig_Unique10_Actions: blizzard
- 히든_호치킨(h03Z): Trig_Akainu_02_hidden_Actions: 더미 e0OD의 A09L(ANpi)

### 시간제 더미 오라(만든 더미가 오라 능력을 가짐)

- 불멸_고도현(h04A): Trig_Ed_Skill_1_Actions: 더미 e03E의 A100(AOae) / Trig_Ed_Skill_1_Item_Actions: 더미 e03E의 A100(AOae) / Trig_Ed_Skill_2sc_Actions: 더미 e0E9의 A100(AOae)
- 불멸_고도현(h04B): Trig_Shiki_Attack_Actions: 더미 e026의 A13U(AHad)
- 불멸_신지우(h07M): Trig_Kaido_Mana_Actions: 더미 h07O의 A0SB(AOae)
- 불멸_정준영(h0AD): Trig_Kaido_Mana_Actions: 더미 h07O의 A0SB(AOae)
- 영원_김정래(h05A): Trig_Bugi_Attack_Actions: 더미 h076의 A0E4(AHad)
- 전설적인_엄태웅(h02P): Trig_Legend25_Actions: 더미 e0NR의 A0W6(AOae)
- 전설적인_이재윤(h03K): Trig_Legend29_Actions: 더미 h06I의 A0VG(ACac)
- 전설적인_임장혁(h02O): Trig_change_Actions: 더미 h05T의 A0JY(ANbl) / Trig_change_Actions: 더미 h05V의 A131(AOae) / Trig_change_Actions: 더미 h05W의 A03P(AOae) / Trig_change_Actions: 더미 h05W의 A0FG(AHad)
- 전설적인_최상호(h03B): Trig_Legend51_Actions: 더미 e03E의 A100(AOae) / Trig_Legend5_Actions: 더미 e00D의 A100(AOae)
- 제한_임준성(h0AI): Trig_King_skill_1sc_Actions: 더미 e0NC의 A12R(AHad)
- 초월_강주혁_AP(H08V): Trig_Nami_Skill_1_Actions: 더미 e070의 A0FV(AOae) / Trig_Nami_Skill_4_Actions: 더미 e0IZ의 A0FV(AOae)
- 초월_구주호_AD(H0BL): Trig_Rokugu_Attack_Actions: 더미 e0QM의 A0SB(AOae) / Trig_Rokugu_Tree_S_Actions: 더미 e0QO의 A0SB(AOae) / Trig_Rokugu_Tree_S_Actions: 더미 e0QO의 A14Z(AHad) / Trig_Rokugu_Tree_S_Actions: 더미 e0QP의 A164(AHad) / Trig_Rokugu_skill_1_Actions: 더미 e0QB의 A0SB(AOae) / Trig_Rokugu_skill_1_Actions: 더미 e0QB의 A14Z(AHad)
- 초월_김만경_AD(H095): Trig_Akainu_01_Shoot_Actions: 더미 e04F의 A09M(AOae) / Trig_Akainu_02_Actions: 더미 e04J의 A09M(AOae) / Trig_Akainu_02_Actions: 더미 e0OD의 A09M(AOae) / Trig_Akainu_03_Actions: 더미 e01M의 A13Z(AOae) / Trig_Akainu_03_Actions: 더미 e046의 A09M(AOae) / Trig_Akainu_03_Actions: 더미 e0K4의 A09M(AOae)
- 초월_노태현_AP(H09I): Trig_Brook_Skill_Mana_Actions: 더미 e06T의 A0QZ(AOae)
- 초월_양재모_AD(H096): Trig_Law_Attack_Actions: 더미 e03U의 A03V(AOae) / Trig_Law_Attack_Actions: 더미 e03U의 A0QD(ACac)
- 초월_이태훈_AP(H08X): Trig_Huji_03_Actions: 더미 e07E의 A0H1(AOae) / Trig_Huji_03_Actions: 더미 e0E7의 A0H1(AOae)
- 초월_임채민_AP(H090): Trig_Tichi_skill_4_Actions: 더미 e066의 A100(AOae) / Trig_Tichi_skill_4_tr_Actions: 더미 e066의 A100(AOae)
- 초월_최상호_AP(H09D): Trig_DP_Attack_gaksung_Actions: 더미 e0IW의 A0VB(AOae)
- 초월_최상호_AP(H09E): Trig_DP_Attack_Actions: 더미 e06K의 A0VB(AOae)
- 특별함_주영호(h00P): Trig_Speical1_Actions: 더미 e099의 A0RF(AOae)
- 희귀함_노태현(h01O): Trig_change_Actions: 더미 h05T의 A0JY(ANbl) / Trig_change_Actions: 더미 h05V의 A131(AOae) / Trig_change_Actions: 더미 h05W의 A03P(AOae) / Trig_change_Actions: 더미 h05W의 A0FG(AHad)
- 희귀함_이상혁(h01L): Trig_change_Actions: 더미 h05T의 A0JY(ANbl) / Trig_change_Actions: 더미 h05V의 A131(AOae) / Trig_change_Actions: 더미 h05W의 A03P(AOae) / Trig_change_Actions: 더미 h05W의 A0FG(AHad)
- 희귀함_임채민(h02M): Trig_change_Actions: 더미 h05T의 A0JY(ANbl) / Trig_change_Actions: 더미 h05V의 A131(AOae) / Trig_change_Actions: 더미 h05W의 A03P(AOae) / Trig_change_Actions: 더미 h05W의 A0FG(AHad)
- 히든_호치킨(h03Z): Trig_Akainu_02_hidden_Actions: 더미 e0OD의 A09M(AOae) / Trig_Hidden5_Actions: 더미 e01M의 A13Z(AOae)

### 대상 주기 피해(독·쉐도우 스트라이크·애시드 밤 계열 더미)


### 레벨·해금·T강화 갈래(능력 레벨을 조건으로 읽거나 올린다)

- 랜덤_가사이_유노(h06Z): Trig_R_Unit_skill_tr_Actions: A0H6 A0KI A0KT A0LB A0YB
- 랜덤_모몬가(h071): Trig_R_Unit_skill_tr_Actions: A0H6 A0KI A0KT A0LB A0YB
- 랜덤_모몬가(h09K): Trig_En_Attack_Func001C: A0YB / Trig_R_Unit_skill_tr_Actions: A0H6 A0KI A0KT A0LB A0YB
- 랜덤_이민형(h073): Trig_Light1_Func002C: A0L7
- 랜덤_이즈미_신이치(h06V): Trig_Naruto1_Func002C: A0L1
- 랜덤_이타도리_유지(h072): Trig_cat1_Func001Func001C: A0L8
- 랜덤_주호페이크(h070): Trig_yomi2_Func001C: A0KC
- 랜덤_카마도_탄지로(h06T): Trig_R_Unit_skill_tr_Actions: A0H6 A0KI A0KT A0LB A0YB / Trig_vampire_Func001C: A0LA
- 랜덤_호시노_아이(h06U): Trig_R_Unit_skill_tr_Actions: A0H6 A0KI A0KT A0LB A0YB / Trig_k1_Func001C: A0KT / Trig_k1_Func002C: A0KT
- 불멸_고도현(h04B): Trig_Shiki_Attack_Actions: A0T4
- 불멸_박은석(h04G): Trig_Z_Skill_1_Actions: A09E
- 불멸_이승우(h04C): Trig_Garp_Mana2_Actions: A05Q A0GY
- 불멸_이이삭(h04E): Trig_Sengoku_Attack_Actions: A0D8
- 불멸_정윤식(h049): Trig_LaillySkill3_Func001C: A0ES
- 불멸_정준영(h04D): Trig_Dragon_Attack_Actions: A0FS
- 영원_김정래(h05A): Trig_Bugi_money_Actions: A03L A04E / Trig_Bugi_money_Func001Func004C: A03L
- 영원_문필환(h057): Trig_vivi_Skill_3_Triple_Actions: A0LZ / Trig_vivi_Skill_3_Triple_Func005Func008C: A0M1 / Trig_vivi_Skill_4_Mana_Actions: A0LZ / Trig_vivi_skill_2_Actions: A0LZ / Trig_vivi_skill_2_Func007C: A0LZ / Trig_vivi_upgrade_Actions: A0LZ A0M1 A0M2 / Trig_vivi_upgrade_Func001Func003C: A0LZ / Trig_vivi_upgrade_Func001Func003Func018C: A0LZ / Trig_vivi_upgrade_Func001Func003Func019C: A0LZ / Trig_vivi_upgrade_
- 영원_조세민(h05C): Trig_hancock_skill_Mana_Actions: A0IN / Trig_hancock_skill_Mana_Func005Func005002003002: Avul
- 전설적인_이재윤(h03K): Trig_Legend29_Func002C: A0K9
- 전설적인_홍인창(h02Y): Trig_Legend22_Actions: A0TU
- 제한_이유범(h084): Trig_treasure_Func007A: A0MX
- 제한_최영민(h05I): Trig_RebecaAttack_Actions: A0QW AIt9 / Trig_Rebeca_Skill_1_Actions: A0QW AIt9 / Trig_Rebeca_Skill_1_Func009A: A0QW / Trig_Rebeca_Skill_3_Func018A: A0QW
- 초월_강주혁_AP(H0BT): Trig_Zoro_Attack_enma_Actions: A0GT
- 초월_구주호_AD(H0BL): Trig_Rokugu_Attack_Actions: A15R A16S
- 초월_김만경_AD(H095): Trig_Akainu_01_Func029Func001C: A0HG
- 초월_김민준_AP(H05N): Trig_Tasigi_03_Func003Func001Func001C: Avul / Trig_Tasigi_03_Func003Func002C: Aamk / Trig_tahsigi_Func004Func006C: A0B7
- 초월_노태현_AP(H09I): Trig_Brook_Skill_Mana_Actions: A0R1
- 초월_박기찬_AD(H08Y): Trig_Franky_Sunny_Make_Actions: A0G7 / Trig_Franky_Sunny_Make_Func001Func010C: A0G7
- 초월_박민수_AD(H09F): Trig_Zoro_Attack_re_Actions: A0GT / Trig_Zoro_saza1_Actions: A0GQ
- 초월_신문철_AP(H099): Trig_Ruffy_Attack_Actions: A0JU
- 초월_유재헌_ADAP(H08U): Trig_Sirahoshi_Attack_Actions: A0EQ
- 초월_이태훈_AP(H08X): Trig_Huji_02_Func002Func011C: A0GR
- 특수함_장진희(h05H): Trig_treasure_Func007A: A0MX
- 특수함_황길라(h05O): Trig_icebug_ship_Actions: A0Q3 / Trig_icebug_ship_Func001Func008C: A0Q3

### 토글·bool 갈래(udg bool 변수로 가지가 갈린다)

- 불멸_고도현(h04A): Trig_Ed_Attack_Actions: udg_item_edward_bool
- 불멸_고도현(h04B): Trig_Shiki_Attack_Actions: udg_item_siki_bool
- 전설적인_임장혁(h02O): Trig_change_Func006Func009Func010C: udg_Name_fervor_bool
- 초월_강주혁_AP(H08V): Trig_Nami_Skill_1_Actions: udg_NamiT_Bool / Trig_Nami_Skill_4_Actions: udg_NamiT_Bool
- 초월_박기찬_AD(H08Y): Trig_Franky_Skill_1_Actions: udg_bool_franky_tr
- 초월_양재모_AD(H096): Trig_Law_Attack_Actions: udg_Law_Bool
- 초월_임장혁_AD(H08W): Trig_Luchi_Attack_Actions: udg_bool_lucchi_tr
- 초월_황준석_ADAP(H0B4): Trig_Kid_Attack_Actions: udg_bool_kid_tr
- 희귀함_노태현(h01O): Trig_change_Func006Func009Func010C: udg_Name_fervor_bool
- 희귀함_이상혁(h01L): Trig_change_Func006Func009Func010C: udg_Name_fervor_bool
- 희귀함_임채민(h02M): Trig_change_Func006Func009Func010C: udg_Name_fervor_bool

### 배타 분기 한 굴림(무작위 조건의 else 가지에 실효 줄)

- 랜덤_미도리야_이즈쿠(h0AC): Trig_Higma_Attack_Actions / Trig_Higma_W_Actions
- 랜덤_이타도리_유지(h09W): Trig_Minato_Mana_Actions
- 랜덤_한마_바키(h08I): Trig_byakuya_Attack_Actions
- 불멸_고도현(h04A): Trig_Ed_Attack_Actions
- 불멸_신지우(h07M): Trig_Kaido_Attack_Actions
- 불멸_이이삭(h04E): Trig_Sengoku_Attack_Actions
- 불멸_정준영(h0AD): Trig_Kaido_Dragon_Skill_2_Actions / Trig_Kaido_Dragon_melee_1_Actions
- 불멸_정준영(h0BI): Trig_Shiki_stone_Actions
- 영원_김정래(h067): Trig_Uta_skill_1_Actions / Trig_Uta_skill_1_double_Actions
- 영원_문필환(h057): Trig_vivi_upgrade_Actions
- 영원_이지원(H0BK): Trig_Nika_Random_Punch_Actions
- 전설적인_박민수(h02Z): Trig_Legend15_Actions
- 전설적인_신문철(h039): Trig_Legend4_Actions
- 전설적인_임건웅(h033): Trig_Legend19_Actions
- 제한_강보명(h05G): Trig_RedAttack_Actions
- 초월_강주혁_AP(H08V): Trig_Nami_Attack_Actions / Trig_Nami_Skill_2_Actions
- 초월_배성령_AD(H09G): Trig_SandiAttack_Upgrade_Actions / Trig_Sandi_skill_1_Actions
- 초월_임채민_AP(H090): Trig_Tichi_Attack_Actions
- 초월_최상호_AD(H09A): Trig_Jimbe_Actions
- 특별함_임채준(h015): Trig_Buggi_Actions
- 흔함_문필환(h00D): Trig_unique_sell2_Actions
- 흔함_박민석(h00K): Trig_unique_sell2_Actions
- 흔함_박민수(h00F): Trig_unique_sell2_Actions
- 흔함_양재모(h00E): Trig_unique_sell2_Actions
- 흔함_임장혁(h00M): Trig_unique_sell2_Actions
- 흔함_최상호(h00A): Trig_unique_sell2_Actions
- 희귀함_엄태웅(h01Y): Trig_Unique35_Actions
- 희귀함_장태영(h01X): Trig_Unique32_Actions
- 희귀함_최상호_오타쿠의길(h01Q): Trig_Unique30_Actions

### 「적이 근처에 오면」(TriggerRegisterUnitInRange)

- 영원_조세민(h05C): Legend14han_petrification 950
- 전설적인_백기현(h035): Legend10_shanks_pegi 1049
- 제한_김민규(h07I): kata_04_pegi 850
- 제한_전법규(h05E): enel_thunder 950

### 평타 다중 대상(Aroc 등)

- 랜덤_이즈미_신이치(h0BF): uabi A12Y(Aroc)
- 변화됨_박은석(h05W): uabi A0WD(Aroc)
- 전설적인_이일중(h042): uabi A0U7(Aroc)
- 전설적인_이재윤(h03K): uabi A0TG(Aroc)
- 제한_이유범(h084): uabi A0UA(Aroc)
- 제한_전법규(h05E): uabi A0DB(Aroc)
- 초월_강재규_AP(H098): uabi A03T(Aroc)
- 초월_구주호_AD(H0BL): uabi A03U(Aroc)
- 희귀함_김청운(h01T): uabi A0BQ(Aroc)
- 희귀함_노태현(h01O): uabi A0WE(Aroc)
- 히든_최윤서(h03R): uabi A03Q(Aroc)

### 영웅 능력치 항(GetHeroStr/Agi/Int — 축은 있음, 값 크기 미확인)

- 초월_강주혁_AP(H08V): Trig_Nami_Skill_1_Actions
- 초월_강주혁_AP(H0BT): Trig_Zoro_Samchun2_Actions / Trig_Zoro_enfor_3dragon_Actions / Trig_Zoro_enfor_flying_draong_Actions / Trig_Zoro_tiger_Actions
- 초월_구주호_AD(H0B5): Trig_Kizaru_01_Actions
- 초월_구주호_AD(H0BL): Trig_Rokugu_Tree_S_Actions
- 초월_김만경_AD(H095): Trig_Akainu_02_Actions
- 초월_김민준_AP(H05N): Trig_Tasigi_02_Func006Func001Func002Func011A / Trig_Tasigi_03_Func002Func023A / Trig_Tasigi_03_Func003Func001Func020A / Trig_Tasigi_03_Func003Func001Func025Func019A / Trig_Tasigi_03_Func004Func011A / Trig_tahsigi_Func002Func001Func004A / Trig_tahsigi_Func004Func006Func010A
- 초월_두유찬_AD(H092): Trig_Sabo_Skill_1_Actions
- 초월_박민수_AD(H09F): Trig_Zoro_Samchun2_Actions / Trig_Zoro_raven_Actions / Trig_Zoro_saza1_Actions / Trig_Zoro_tiger_Actions
- 초월_임채민_AP(H090): Trig_Tichi_skill_2_tr_Func003Func007A / Trig_Tichi_skill_2_tr_Func004Func003A / Trig_Tichi_skill_3_Func009A / Trig_Tichi_skill_4_tr_Actions
- 초월_최상호_AD(H09A): Trig_Jimbe_jingak_Actions
- 초월_최상호_AP(H09D): Trig_DP_Skill_3_Func007A
- 초월_최상호_AP(H09E): Trig_DP_Skill_3_Func007A
- 초월_황준석_ADAP(H0B4): Trig_Kid_Skill_Life2_Actions / Trig_Kid_Skill_Life_Actions / Trig_Kid_Skill_Mana_Actions / Trig_Kid_Skill_Mana_Tr_Actions

### 주변 적 수 계수(CountUnitsInGroup을 피해 식에)


### 지연·단계 피해(SleepForStage·타이머 뒤 피해 — 즉발 근사 가능한지 봐야 함)

- 랜덤_이즈미_신이치(h0BF): Trig_kikoyou_hp_Actions / Trig_kikoyou_skill_1_Actions
- 랜덤_이타도리_유지(h09W): Trig_Minato_E_Actions
- 랜덤_주호페이크(h09L): Trig_Bronya_Skill_1_Actions
- 랜덤_카마도_탄지로(h0A2): Trig_Ryougi_Shiki_2_Actions / Trig_Ryougi_Shiki_R2_Actions / Trig_Ryougi_Skill_3_Actions / Trig_Ryougi_Skill_3_double_Actions / Trig_Ryougi_Skill_3_triple_Actions
- 랜덤_한마_바키(h08I): Trig_byakuya_Chan_Actions
- 랜덤_호시노_아이(h09N): Trig_BronyaMotar_E_Actions / Trig_BronyaMotar_R_Actions / Trig_BronyaMotar_laser_Actions
- 변화됨_최상호(h05S): Trig_Transpom_dp_Actions
- 불멸_고도현(h04A): Trig_Ed_Skill_1_Actions / Trig_Ed_Skill_1_Item_Actions
- 불멸_고도현(h04B): Trig_Shiki_champa2_Actions
- 불멸_박은석(h04G): Trig_Z_Skill_1_Actions / Trig_Z_skill_3_Actions
- 불멸_신지우(h04F): Trig_Gaban_Skill_1_Actions
- 불멸_신지우(h07M): Trig_Kaido_Attack_Actions / Trig_Kaido_Dragon_buster_Actions / Trig_Kaido_Skill_1_8_Actions
- 불멸_이승우(h04C): Trig_Garp_AttackDamage_Actions
- 불멸_정윤식(h049): Trig_Kick_1_Actions
- 불멸_정준영(h04D): Trig_Dragon_Skill_1_T_Actions
- 불멸_정준영(h04Q): Trig_Bigmam_Attack_Actions / Trig_Bigmam_Eat_Actions
- 불멸_정준영(h0AD): Trig_Kaido_Dragon_buster_Actions / Trig_Kaido_Skill_1_8_Actions
- 불멸_정준영(h0BI): Trig_Shiki_stone_Actions
- 영원_김정래(h067): Trig_Uta_ArmyS_Actions / Trig_Uta_skill_1_Actions / Trig_Uta_skill_1_double_Actions
- 영원_문필환(h057): Trig_vivi_Skill_3_Triple_Actions / Trig_vivi_Skill_4_Mana_Actions
- 영원_이지원(H0BK): Trig_Nika_AttackDamage_Actions / Trig_Nika_Random_Punch_Actions
- 영원_조세민(h05C): Trig_arrow_h2_Actions / Trig_hancock_skill_Mana_Actions
- 영원_최상호(h058): Trig_Mihawk_2_Actions
- 영원_최상호(h05B): Trig_Cavendish_skill_Mana_Actions
- 전설적인_이승우(h02Q): Trig_Legend12_Actions
- 전설적인_이현주(h032): Trig_Legend_han_1_Actions / Trig_Legend_han_kiss_damagesc_Actions
- 전설적인_임건웅(h033): Trig_Legend19_0_Actions / Trig_Legend19_1_Actions
- 전설적인_임장혁(h087): Trig_Legend31_toki_Actions
- 전설적인_임채민(h02T): Trig_Legend17_MacroLifeSkill_Actions
- 전설적인_정윤식(h02N): Trig_Legend34Moria_Skill_Actions
- 전설적인_진연서(h09Z): Trig_Legend32_neko1_Actions
- 전설적인_최상호(h03B): Trig_Legend51_Actions
- 전설적인_홍인창(h02U): Trig_Legend26_tichi_crows_Actions
- 전설적인_홍인창(h02Y): Trig_Legend22_Actions
- 제한_박성호(h08O): Trig_Marco_S2_Actions
- 제한_이유범(h084): Trig_Sinobu_Skill_hp2_Actions
- 제한_이충민(h040): Trig_Ain_Skill_1_Actions / Trig_Ain_Skill_2_Actions
- 제한_임준성(h0AI): Trig_King_skill_2_tr_Actions
- 제한_최영민(h09Q): Trig_Marco_Feather2_Actions
- 초월_강재규_AP(H098): Trig_Robine_skill_1_Actions
- 초월_강주혁_AP(H08V): Trig_Nami_Skill_2_Actions
- 초월_강주혁_AP(H0BT): Trig_Zoro_enfor_flying_draong_Actions
- 초월_구주호_AD(H0B5): Trig_Kizaru_01_Actions
- 초월_구주호_AD(H0BE): Trig_Yamato_Dash_Actions
- 초월_김경현_AP(H0B2): Trig_Snake_2_Actions / Trig_Snake_3_Kingkobra_Actions
- 초월_김만경_AD(H095): Trig_Akainu_01_Shoot_Actions / Trig_Akainu_02_Actions
- 초월_김민준_AP(H05N): Trig_Tasigi_02_Actions / Trig_Tasigi_03_Actions / Trig_tahsigi_Actions
- 초월_두유찬_AD(H092): Trig_Sabo_Skill_1_Actions
- 초월_박민수_AD(H09F): Trig_Zoro_saza1_Actions
- 초월_배성령_AD(H09G): Trig_Sandi_skill_1_Actions / Trig_Sandi_skill_Mana2_Actions
- 초월_신문철_AP(H099): Trig_Ruffy_AttackDamage_Actions / Trig_Ruffy_Mana_Actions
- 초월_양재모_AD(H096): Trig_Law_skill_5_Actions
- 초월_이태훈_AP(H08X): Trig_Huji01_Actions
- 초월_임장혁_AD(H08W): Trig_Luchi_Skill_1_Kick2_Actions
- 초월_임채민_AP(H090): Trig_Tichi_skill_1_Actions
- 초월_조성진_AD(H09B): Trig_Usop_Skill_2_Actions
- 초월_최상호_AD(H09A): Trig_Jimbe_Actions
- 초월_최상호_AP(H09D): Trig_DP_Skill_2_Actions
- 초월_최상호_AP(H09E): Trig_DP_Skill_2_Actions
- 초월_황준석_ADAP(H0B4): Trig_Kid_Skill_Life2_Actions / Trig_Kid_Skill_Life_Actions
- 희귀함_엄태웅(h01Y): Trig_Unique35_Actions
- 희귀함_임채민(h02M): Trig_Unique29_Actions
- 희귀함_최상호_오타쿠의길(h01Q): Trig_Unique30_Actions
- 희귀함_황준석(h021): Trig_Tich1_danil_Actions
- 희귀함_황준석(h02E): Trig_Unique34_Actions
- 히든_최윤서(h03R): Trig_deken_skill_1_Actions
- 히든_호치킨(h03Z): Trig_Akainu_02_hidden_Actions
- 히든_황정기(h03V): Trig_koalla_skill_1_Actions
