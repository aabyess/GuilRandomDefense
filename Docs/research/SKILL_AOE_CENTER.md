# 범위 스킬 중심점 전수 조사 (2026-09-30, Blender 세션, 읽기 전용)

근거는 `Tools/w3x/원본/war3map_new.j`(줄 번호 = `grep -n` 기준)와 w3a/w3u 직접 디코드뿐이다. Docs는 안 봤다.

## 결론 먼저

- 대상 에셋은 **133개**다. 합계: **caster 7 · target 107 · 기타 19**(line 9 · random 7 · mixed 1 · unknown 2).
- **원작 범위 스킬의 80%(107/133)는 공격받은 적의 위치가 중심이다.** 그래서 우리 기본값(시전자 중심)은 대부분 틀렸다.
- **시전자 중심은 7개뿐**이고, 아래 표에 전부 적었다. 「중심을 대상으로」 바꿀 때는 이 7개만 예외로 두면 된다.
- 기타 19개 처리 제안:
  - random 7: 대상 주변 무작위 지점이다. 대상 중심으로 근사하면 된다.
  - line 9: 시전자에서 대상 쪽으로 나가는 빔·투사체·돌진이다. 대상 중심으로 근사하면 과소보다는 낫다. 정확히 하려면 경로 축이 따로 필요하다.
  - mixed 1: 이즈미 신이치의 whirlwind는 시전자, stomp는 대상이다.
  - unknown 2: 원작009(소환체 오라)와 이충민(750000 출처 미확정)이다.

### ⚠️ 지시문 한 곳 정정 — `GetTriggerUnit()`은 시전자가 아니다

유닛 공격 트리거는 전부 `Trig_Main_Attack_Trigger_Manager`(j:3433)를 거쳐 `TriggerExecute`로 실행된다. 이 트리거의 이벤트는 `TriggerRegisterPlayerUnitEventSimple(…,Player(5)|Player(6),EVENT_PLAYER_UNIT_ATTACKED)`, 즉 **적 유닛이 공격받을 때**다. 그래서 이 문맥에서는:

- `GetTriggerUnit()` = **공격받은 적 = 대상**
- `GetAttacker()` = **우리 유닛 = 시전자**

`ConditionalTriggerExecute`로 이어지는 하위 트리거도 같은 이벤트 문맥을 물려받는다. 조사한 트리거 140개 중 자체 이벤트(SPELL 등)를 가진 것은 0개였다. 예외는 Huji02DM(`EVENT_UNIT_DAMAGED`를 동적으로 등록)인데, 여기서도 `GetTriggerUnit()`은 피해를 받은 적이다.

## 방법 (목록은 손으로 고르지 않고 유도했다)

1. **목록**: `Assets/Data/UnitSkills/*.asset` 385개 중에서 골랐다. 조건은 레벨 하나라도 `range > 0`이고, 그 레벨 effects에 `target: 2(Enemies)` 또는 `1(Allies)`가 있는 것이다. 결과는 133개이고 Allies는 0개다.
   - 교차 확인: `grep -lE "^      target: [12]"` 결과도 133개다. 즉 Enemies 효과가 있는데 range가 0인 에셋은 지금 없다.
   - 첫 스캔은 129개였다. 조사 중에 PM이 7종 문서를 반영해서 4개(이상혁·원작003·008·017)가 새로 채워졌다. 최종 수치는 현재 작업 트리 기준이다.
2. **트리거 특정**: 각 에셋의 skillName·description 토큰을 j의 `InitTrig_*` 이름 1,4xx개와 대조했다(129/133 자동). 나머지 4개는 해시테이블 줄(`SaveTriggerHandle(udg_HashAttack,'유닛ID',…)`)로 유닛 공격 트리거를 찾아 손으로 붙였다.
3. **중심 판정 (두 겹)**
   - ① **반경 대조**: 트리거(와 `ConditionalTriggerExecute`로 부르는 하위 트리거 2단계까지) 안의 범위 호출을 모았다. 대상은 `GroupEnumUnitsInRangeOfLoc`·`GetUnitsInRangeOfLocMatching`·`UnitDamagePointLoc`·더미 생성 후 `stomp/thunderclap/…` 오더다. 에셋 range와 같은 반경인 호출의 위치 인자를 되짚었다.
     - `GetUnitLoc(X)` → X
     - `udg_*_LOC` → **그 호출 바로 앞의 마지막 set**
     - `s__TrigVariables__get_locationK` → `Setlocation(GlobalTV,K,…)`
     - `udg_Hero_*[n]` → 공격 트리거의 `=GetAttacker()`/`=GetTriggerUnit()`
     - `PolarProjectionBJ` → 오프셋·무작위
   - ② **효과 대조**: 효과마다 피해 상수(multiplier/bonus ≥ 1000)를 j의 `RRD`·`UnitDamage*`에서 찾았다. 그 콜백을 부르는 `ForGroupBJ`의 그룹 중심을 되짚었다(194개 효과 중 141개 적중).
     - j에 없는 값은 w3a 필드값(Wrs1·Htc1·Ucs1…)으로 더미 능력을 찾았다. 이어서 그 더미의 생성 위치를 보거나, description에 적힌 「트리거 → 더미 → 능력」 경로로 더미 생성 위치를 봤다.
   - ①과 ②가 어긋난 26건은 트리거를 직접 읽고 판정을 적었다(표의 「근거」 칸이 문장이면 수동 판정이다).
4. 분류 기준:
   - **caster**: 시전자 위치(또는 시전자 전방 고정점)
   - **target**: 공격받은 적 위치(오프셋 ≤ 25 포함, 여러 적이면 적마다)
   - **random**: 대상 주변 무작위 지점
   - **line**: 시전자에서 출발해 이동하는 빔·투사체·돌진 더미 위치
   - **mixed**: 한 에셋 안에서 효과마다 중심이 다름
   - **unknown**: 끝까지 못 따라감

## 시전자 중심 7개 (예외 목록)

| 에셋 | 트리거 | 반경 | 근거 |
|---|---|---|---|
| `게이트_불멸_김용태_57c27e37` | roger_Skill_2 | 850 | j:19195 `udg_Immortal_LOC=GetUnitLoc(udg_Hero_Roger[0])` → 850 그룹 |
| `게이트_영원_김정래_a22567e4` | Uta_skill_3_mana | 800 | j:20292 locA=`GetUnitLoc(unitA=udg_Hero_Uta[6]=공격자)` → 800 그룹 |
| `게이트_제한_강보명_57c27e37` | Red_skill_1 | 900 | j:15402 `udg_Trasnpom_LOC=GetUnitLoc(udg_Hero_Red[0])` → thunderclap 더미 + 900 그룹 |
| `게이트_제한_최영민_30f704bf` | Rebeca_Skill_3 | 850 | j:15387 locA=시전자 → thunderclap 더미 + 850 그룹 |
| `게이트_히든_황정기_2a646778` | koalla_skill_Mana | 625 | j:14581 locC=시전자 전방 275 고정점 → 625 그룹 3회 |
| `원작019_H08Z` | Shanks_Attack | 1100 | j:15919 e01U(A0S1 `Wrs1=1500000 aare=1100`)를 `udg_Legend_LOC=GetUnitLoc(GetAttacker())`에 생성해 stomp |
| `원작028_h05C` | hancock_skill_Mana | 760 | j:19614 locA=시전자. 760은 **대상 탐색 반경**이고, 피해는 고른 적 각각에게 간다. e0AH stomp도 시전자 위치 |

## 전체 표 (133행)

| 에셋 | skillName | 트리거 | range | 중심 | 근거 | j 줄 |
|---|---|---|---|---|---|---|
| `SkillData_게이트_랜덤_모몬가_ff8bed43.asset` | 옌 - 랜덤전용 — 1/4 | En_nomal | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17841 |
| `SkillData_게이트_랜덤_미도리야_이즈쿠_079f80a5.asset` | 마운틴.D.히그마 - 랜덤전용[제한됨] — LIFE게이지85.00 AND | Higma_W | 400 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18711 |
| `SkillData_게이트_랜덤_미도리야_이즈쿠_2c7de21c.asset` | 마운틴.D.히그마 - 랜덤전용[제한됨] — LIFE게이지85.00 AND | Higma_Q | 375 | **line** | 시전자 전방 75에서 출발해 이동하는 더미(unitC) 위치마다 375 | j:18672, j:18674 |
| `SkillData_게이트_랜덤_손오공_948ad1b7.asset` | 야쿠모 유카리 - 랜덤전용[제한됨] — 1/20 | Yukari_R2 | 395 | **target** | 그룹에 잡힌 적마다 그 적 위치+145(Func052A GetEnumUnit) | j:18276 |
| `SkillData_게이트_랜덤_이민형_4266b6e3.asset` | 타츠마키 - 랜덤전용[제한됨] — MANA게이지85.00 | Tatsumaki_T_Explosion_by_AZ_Attack | 600 | **target** | Stage1에 잡은 대상 위치(locB), Stage27 폭발 | j:18640 |
| `SkillData_게이트_랜덤_이즈미_신이치_41677101.asset` | 키쿄우 - 랜덤전용[제한됨] — LIFE게이지50.00 AND 1/6 | kikoyou_skill_1 | 375 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18310, j:18311 |
| `SkillData_게이트_랜덤_이즈미_신이치_61743e88.asset` | 키쿄우 - 랜덤전용[제한됨] — LIFE게이지50.00 AND MANA게 | kikoyou_mana | 525 | **random** | 대상 위치에서 70~325 무작위 지점 | j:18365 |
| `SkillData_게이트_랜덤_이즈미_신이치_d3498c23.asset` | 키쿄우 - 랜덤전용[제한됨] — LIFE게이지50.00 AND 1/16 | kikoyou_skill_2 | 425 | **line** | 시전자에서 쏜 투사체(e0P7) 위치 | j:18320 |
| `SkillData_게이트_랜덤_이타도리_유지_18aa2343.asset` | 나미카제 미나토 - 랜덤전용[제한됨] — 1/6 | Minato_Q2 | 500 | **random** | 대상 위치(locB)에서 300 떨어진 무작위 방향 지점 | j:18007 |
| `SkillData_게이트_랜덤_이타도리_유지_a8343962.asset` | 나미카제 미나토 - 랜덤전용[제한됨] — MANA게이지100.00 | Minato_Mana | 500 | **random** | 대상 위치에서 반경 500 안 무작위 지점 | j:18049 |
| `SkillData_게이트_랜덤_주호페이크_3a13fd3d.asset` | 이치의 율자 - 랜덤전용 — 1/7 | Bronya_Skill_1 | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18387 |
| `SkillData_게이트_랜덤_카마도_탄지로_d370bb23.asset` | 료우기 시키 - 랜덤전용[제한됨] — 1/4 AND 1/16 | Ryougi_Shiki_2 | 425 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18094 |
| `SkillData_게이트_랜덤_한마_바키_355f5d39.asset` | 쿠치키 뱌쿠야 - 랜덤전용[제한됨] — (게이트 없음) | Byakuya_R,byakuya_Chan | 700 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18576 |
| `SkillData_게이트_랜덤_한마_바키_9ffa9c45.asset` | 쿠치키 뱌쿠야 - 랜덤전용[제한됨] — 1/10 | byakuya_Q | 525 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18553 |
| `SkillData_게이트_랜덤_호시노_아이_33bd8aa4.asset` | 부릉냐 - 랜덤전용[제한됨] — 1/5 | BronyaMotar_E | 415 | **line** | 시전자 앞에서 전진하는 빔(locB=시전자+거리) | j:18434 |
| `SkillData_게이트_랜덤_호시노_아이_3a13fd3d.asset` | 부릉냐 - 랜덤전용[제한됨] — 1/7 | BronyaMotar_laser | 400 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18393, j:18395, j:18396 |
| `SkillData_게이트_랜덤_호시노_아이_55b5cff0.asset` | 부릉냐 - 랜덤전용[제한됨] — MANA게이지150.00 | BronyaMotar_R | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18407, j:18408 |
| `SkillData_게이트_변화됨_강재규_5c20b80f.asset` | 캐럿 스론 달의 사자 - 변화된 — 1/9 | carrot_skill_2 | 425 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15646 |
| `SkillData_게이트_변화됨_김건_355f5d39.asset` | 간부 파견 해군 - 변화된 — (게이트 없음) | Bugi_SommonDamage2 | 325 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19694 |
| `SkillData_게이트_변화됨_박은석_8eab1c6f.asset` | 포트거스.D.에이스 스페이드 해적단 선장 - 변 — MANA게이지125. | Transpom_AceMana | 575 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15184 |
| `SkillData_게이트_변화됨_박은석_9ffa9c45.asset` | 포트거스.D.에이스 스페이드 해적단 선장 - 변 — 1/10 | Transpom_Ace_bulRemake | 400 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15192 |
| `SkillData_게이트_불멸_고도현_caa364ec.asset` | 에드워드 뉴게이트 한시대를 주름잡던 대해적 -  — LIFE게이지115. | Ed_Skill_1,Ed_Skill_1_Item | 625 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19167, j:19168, j:19176, j:19178 |
| `SkillData_게이트_불멸_김용태_55b5cff0.asset` | 골.D.로져 해적왕 - 불멸의 — MANA게이지150.00 | roger_Mana | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19204 |
| `SkillData_게이트_불멸_김용태_57c27e37.asset` | 골.D.로져 해적왕 - 불멸의 — 1/22 | roger_Skill_2 | 850 | **caster** | GetUnitLoc(GetAttacker()) 계열 | j:19195 |
| `SkillData_게이트_영원_김영원_5a6f48de.asset` | '/CFFFFFA78''아마존릴리의 여제''  보아  — 마나≠175 A | hancock_skill_9,hancock_skill_Mana | 450 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19585, j:19588 |
| `SkillData_게이트_영원_김영원_b3948c74.asset` | /CFFFFFA78'아마존릴리의 여제'  보아  — 1/14 | hancock_skill_10 | 600 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19594 |
| `SkillData_게이트_영원_김정래_a22567e4.asset` | /CFFFFFA78세계의 가희 우타 - /CFF — 1/10 AND 1/ | Uta_skill_3_mana | 800 | **caster** | GetUnitLoc(GetAttacker()) 계열 | j:20291 |
| `SkillData_게이트_영원_문필환_355f5d39.asset` | /CFFFFFA78토트 무지카 봉인된 악보의 괴 — (게이트 없음) | tots_R | 415 | **line** | 시전자 앞 175, 이후 290×n 전진 | j:20183 |
| `SkillData_게이트_영원_서민성_948ad1b7.asset` | /CFFFFFA78참된 호걸 코즈키 오뎅 - / — 1/20 | Odeng_Skill | 600 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:20106 |
| `SkillData_게이트_영원_서민성_b5c9b49a.asset` | /CFFFFFA78참된 호걸 코즈키 오뎅 - / — MANA게이지145. | Odeng_Skill_mana | 600 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:20087 |
| `SkillData_게이트_영원_윤현모_aa9cf1bd.asset` | /CFFFFFA78'화권의' 포트거스.D.에이스 — MANA게이지185. | ace_skill_5 | 850 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19549 |
| `SkillData_게이트_영원_윤현모_d319dd11.asset` | /CFFFFFA78'화권의' 포트거스.D.에이스 — 1/35 | ace_skill_3 | 650 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19532 |
| `SkillData_게이트_영원_이지원_3ba504d5.asset` | /CFFFFFA78태양의신-니카 — LIFE게이지115.00 AND 버프 | Nika_AttackDamage,Nika_Random_Punch | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19491, j:19493, j:19507, j:19508 |
| `SkillData_게이트_영원_최상호_a8343962.asset` | /CFFFFFA78해적왕자-롬멜의 카마이타치 카 — MANA게이지100. | Cavendish_skill_Mana | 900 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19722, j:19724 |
| `SkillData_게이트_전설적인_이현주_0e5cf375.asset` | 보아 핸콕 쿠사 해적단 선장 - 전설적인 — 버프B00D==true AN | Legend_han_1 | 345 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14826 |
| `SkillData_게이트_전설적인_임건웅_9ffa9c45.asset` | 빈스모크 레이쥬 포이즌 핑크 - 전설적인 — 1/10 | Legend19_0 | 333 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14882 |
| `SkillData_게이트_전설적인_임장혁_3a13fd3d.asset` | 아마츠키 토키 시간 여행자 - 전설적인 — 1/7 | Legend31_toki | 415 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14995, j:14996 |
| `SkillData_게이트_전설적인_임채민_355f5d39.asset` | 마르코 환수종'불사조' - 전설적인 — (게이트 없음) | Legend17_Marcodamage | 285 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14855 |
| `SkillData_게이트_전설적인_임채현_03847fe6.asset` | 울티 토비롯포 - 전설적인 — LIFE게이지35.00 | Legend35ulti_trg | 400 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15087 |
| `SkillData_게이트_전설적인_정윤식_355f5d39.asset` | 겟코 모리아 쉐도우 아스가르드 - 전설적인 — (게이트 없음) | Legend34Moria_Skill | 400 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15072 |
| `SkillData_게이트_전설적인_정준영_3a13fd3d.asset` | 롤로노아 조로 죽음의경지'사자의노래' - 전설적 — 1/7 | Gaban_Skill_mana,Legend13_2 | 450 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14818 |
| `SkillData_게이트_전설적인_진연서_483e0464.asset` | 네코마무시 밤의 왕 - 전설적인 — LIFE게이지33.00 | Legend32_neko1 | 525 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15049 |
| `SkillData_게이트_전설적인_최상호_355f5d39.asset` | 에드워드 뉴게이트 '사황' 흰수염 해적단의 아버 — (게이트 없음) | Legend51 | 625 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14736 |
| `SkillData_게이트_제한_강보명_57c27e37.asset` | 패트릭 레드필드 붉은백작 - 제한됨 — 1/22 | Red_skill_1 | 900 | **caster** | GetUnitLoc(GetAttacker()) 계열 | j:15401 |
| `SkillData_게이트_제한_김강민_948ad1b7.asset` | 크로커다일 사막의 악어 - 제한됨 — 1/20 | cro_skill_2 | 550 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15211 |
| `SkillData_게이트_제한_김민규_136d09f9.asset` | 샬롯 카타쿠리 장성 밀가루 대신 - 제한됨 — 1/7 AND 버프B045 | Kata_03 | 415 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15418 |
| `SkillData_게이트_제한_김민규_3a13fd3d.asset` | 샬롯 카타쿠리 장성 밀가루 대신 - 제한됨 — 1/7 | katakuri_Skill_2 | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15409 |
| `SkillData_게이트_제한_박성호_3a13fd3d.asset` | 마르코 흰수염 유산의 수호자 - 제한됨 — 1/7 | Marco_S2 | 475 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15793 |
| `SkillData_게이트_제한_이유범_43ad0210.asset` | 시노부 뇌쇄 쿠노이치 - 제한됨 — LIFE게이지50.00 AND 1/1 | Sinobu_Skill_4 | 475 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15688 |
| `SkillData_게이트_제한_이충민_18aa2343.asset` | 아인 네오 해군 장교 - 제한됨 — 1/6 | Ain_Skill_1 | 475 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15551, j:15564 |
| `SkillData_게이트_제한_이충민_8f2b5872.asset` | 아인 네오 해군 장교 - 제한됨 — 1/21 | Ain_Skill_2 | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15588, j:15592, j:15595 |
| `SkillData_게이트_제한_임준성_355f5d39.asset` | 킹 삼재해:화재 - 제한됨 — (게이트 없음) | King_skill_2,King_skill_2_tr | 475 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15721, j:15741 |
| `SkillData_게이트_제한_전법규_9ffa9c45.asset` | 에넬 G.O.D - 제한됨 — 1/10 | Enel_skill_2 | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15261 |
| `SkillData_게이트_제한_최영민_30f704bf.asset` | 레베카 콜로세움 전설의  후예 - 제한됨 — 1/12 | Rebeca_Skill_3 | 850 | **caster** | GetUnitLoc(GetAttacker()) 계열 | j:15387 |
| `SkillData_게이트_제한_최영민_33bd8aa4.asset` | 레베카 콜로세움 전설의  후예 - 제한됨 — 1/5 | Rebeca_Skill_1 | 450 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15382 |
| `SkillData_게이트_초월_강재규_AP_85d0117d.asset` | 오하라의 마지막 생존자 — LIFE게이지40.00 AND 1/10 | Robine_skill_2 | 525 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16214 |
| `SkillData_게이트_초월_강재규_AP_fcba0856.asset` | 오하라의 마지막 생존자 — LIFE게이지40.00 AND 1/20 | Robine_skill_3 | 350 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16216 |
| `SkillData_게이트_초월_구주호_AD_355f5d39.asset` | 오니히메 인수화 — (게이트 없음) | Yamato_Attack_Damage2 | 425 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17567, j:17568 |
| `SkillData_게이트_초월_김경현_AP_4266b6e3.asset` | 다섯번째 황제 — MANA게이지85.00 | Snake_3_Kingkobra | 550 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17340, j:17341 |
| `SkillData_게이트_초월_김경현_AP_b7ae9b41.asset` | 다섯번째 황제 — 1/15 | Snake_2 | 475 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17310, j:17317 |
| `SkillData_게이트_초월_김민준_AP_355f5d39.asset` | 해군의 홍일점 — (게이트 없음) | Tasigi_02,tahsigi | 450 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16271, j:16280 |
| `SkillData_게이트_초월_두유찬_AD_8eab1c6f.asset` | 혁명군 참모총장 — MANA게이지125.00 | Sabo_Mana | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16178 |
| `SkillData_게이트_초월_박기찬_AD_a8343962.asset` | 무적의 아이언 파이러츠 — MANA게이지100.00 | Franky_Skill_Mana1 | 425 | **line** | 시전자→대상 방향 105×1..8 지점(빔) | j:16654 |
| `SkillData_게이트_초월_박기찬_AD_b3948c74.asset` | 무적의 아이언 파이러츠 — 1/14 | Franky_misiile_re | 400 | **random** | 대상 위치에서 30~200 무작위 지점(미사일) | j:16698 |
| `SkillData_게이트_초월_박민수_AD_b5c9b49a.asset` | 수라의 검사 — MANA게이지145.00 | Zoro_Samchun2,Zoro_samchun1 | 525 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16932, j:16944 |
| `SkillData_게이트_초월_박민수_AD_b7ae9b41.asset` | 수라의 검사 — 1/15 | Zoro_raven | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16913, j:16915, j:16917 |
| `SkillData_게이트_초월_배성령_AD_3c5f8bd9.asset` | 정열의 요리사 — MANA게이지115.00 | Sandi_skill_Mana2 | 600 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16068 |
| `SkillData_게이트_초월_신문철_AP_55ecbfb2.asset` | 명왕 레일리의 제자 — MANA게이지160.00 | Ruffy_Mana | 625 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15913, j:15914 |
| `SkillData_게이트_초월_양재모_AD_29846395.asset` | 해군 대장 '성난 보라호랑이' — MANA게이지140.00 | Huji01,Huji_03 | 485 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16533, j:16590 |
| `SkillData_게이트_초월_임장혁_AD_5e483927.asset` | CP.Zero — Real<=5.25 | Luchi_Skill_3_6kinggun | 400 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15883 |
| `SkillData_게이트_초월_임채민_AP_42844439.asset` | 지진과 어둠의 검은수염 — LIFE게이지85.00 AND 1/25 | Tichi_skill_2_tr | 415 | **line** | 시전자 앞 50에서 +300/+285씩 전진 | j:16494, j:16498 |
| `SkillData_게이트_초월_조성진_AD_29846395.asset` | G.O.D — MANA게이지140.00 | Usop_Skill_Mana | 600 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16788 |
| `SkillData_게이트_초월_조성진_AD_33bd8aa4.asset` | G.O.D — 1/5 | Usop_Skill_2 | 600 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16771 |
| `SkillData_게이트_초월_최상호_AD_3c5f8bd9.asset` | 어인협객 — MANA게이지115.00 | Jimbe_Mu | 600 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17256 |
| `SkillData_게이트_초월_최상호_AD_b8d2fd85.asset` | 어인협객 — 1/16 | Jimbe | 625 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17206 |
| `SkillData_게이트_초월_최상호_AP_18aa2343.asset` | "어둠의 조커"드레스로자의 악몽 — 1/6 | DP_Skill_3 | 525 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16737 |
| `SkillData_게이트_초월_최상호_AP_355f5d39.asset` | "어둠의 조커"드레스로자의 악몽 — (게이트 없음) | DP_AttackDamagegaksung | 350 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16731 |
| `SkillData_게이트_히든_최윤서_3c5f8bd9.asset` | '[히든조합]반 더 데켄어인섬 절반면적의 노아를  — MANA게이지115 | deken_Mana | 800 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14487 |
| `SkillData_게이트_히든_호치킨_21297197.asset` | '[히든조합]아카이누흰수염을죽인 붉은개 — 버프B06B==true' | Akainu_02_hidden | 525 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14430 |
| `SkillData_게이트_히든_황정기_2251ce1a.asset` | '[히든조합]코알라혁명군-어인공수도대리사범 — MANA게이지135.00  | koalla_skill_1 | 500 | **target** | 대상 위치에서 −25(사실상 대상) | j:14605, j:14608 |
| `SkillData_게이트_히든_황정기_2a646778.asset` | '[히든조합]코알라혁명군-어인공수도대리사범 — MANA게이지135.00' | koalla_skill_Mana | 625 | **caster** | 시전자 전방 275 고정점 | j:14571, j:14574, j:14578 |
| `SkillData_원작003_H08Y.asset` | A069 !꾸드방-프랑초 | Franky_Attack,Franky_Skill_1 | 400/500 | **target** | Franky_Skill_1: udg_Transcendence_LOC=GetUnitLoc(udg_Hero_Franky[1]=공격받은 적) 400/500 | j:16665, j:16666 |
| `SkillData_원작006_H096.asset` | A10S !감마나이프 | Law_Attack | 485 | **target** | Law_Attack 650 그룹·Law_Skill_2_reinforce 더미 모두 대상 위치 | j:16014 |
| `SkillData_원작007_H08V.asset` | A107 $제우스 | Nami_Attack,Nami_Skill_4 | 400 | **random** | Nami_Skill_1: 대상 위치에서 100~375 무작위 지점 400(번개) | j:17030 |
| `SkillData_원작008_H08U.asset` | A0EQ !해류조정-시라호시 | Sirahoshi_Attack | 600 | **target** | Sirahoshi_Attack: 대상 위치에 e04V/e0KA stomp(600) | j:16229 |
| `SkillData_원작009_H094.asset` | A0WK $미니 스트로맨1 | (A0WK 오라, T_Ability_hero에서 부여 j:20528) | 950 | **unknown** | A0WK 오라는 소환된 허수아비가 냄 — 소환 위치 미추적 | j:20528 |
| `SkillData_원작013_H099_A09S.asset` | A09S !2범위 호크 개틀링 더미 | Ruffy_Attack | 500 | **target** | e050 stomp(A09S 500)를 대상 위치+0~20에 생성(Ruffy_Attack) | j:15890 |
| `SkillData_원작017_H095.asset` | A0HG !유성화산-아카초 | Akainu_01,Akainu_Attack | 465 | **random** | Akainu_01: 대상 위치에서 0~700 무작위 지점마다 유성 465 | j:16088, j:16128 |
| `SkillData_원작019_H08Z.asset` | A136 샹초 특강마나 | Shanks_Attack | 1100 | **caster** | e01U stomp(A0S1 Wrs1=1500000 aare=1100)를 시전자 위치에 생성(Shanks_Attack) | j:15919 |
| `SkillData_원작022_h04G.asset` | A09E !차지 버스터 | Z_Skill_1 | 475 | **random** | Z_Skill_1: 대상 위치에서 45~150 무작위 지점 475 | j:19128 |
| `SkillData_원작023_h04C.asset` | A0GY !특제대포환 | Garp_Attack,Garp_Mana2 | 650 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19086, j:19091, j:19093 |
| `SkillData_원작024_h04D.asset` | A0FS !용오름-드불 | Dragon_Skill_1,Dragon_Skill_1_T,Dragon_Skill_Mana | 475 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19036, j:19042 |
| `SkillData_원작028_h05C.asset` | A0IN !파퓸 페뮤르 | arrow_h2,hancock_skill_9,hancock_skill_Mana | 760 | **caster** | 시전자 기준 760 안에서 대상 고르기 + e0AH stomp 시전자 위치 | j:19606, j:19614 |
| `SkillData_원작능력_랜덤_이즈미_신이치.asset` | A0H6 !슈타인베르거 | Naruto1 | 375 | **mixed** | whirlwind A0LG(550)=시전자 / stomp A0LF(600)=대상 | j:17766 |
| `SkillData_원작능력_랜덤_이타도리_유지.asset` | A0XB !무기화 빌드 | cat1 | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17775 |
| `SkillData_원작능력_랜덤_카마도_탄지로.asset` | A0KT !체인드라이브 | vampire | 425 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17731 |
| `SkillData_원작능력_랜덤_한마_바키.asset` | A0GL !신의 손 | isz | 525 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17663 |
| `SkillData_원작능력_랜덤_호시노_아이.asset` | A0KW !액션펭귄 | k1 | 500 | **target** | 460 그룹·stomp e0CT 대상 위치, 350은 대상 ±100 무작위 | j:17799 |
| `SkillData_원작능력_변화됨_박은석.asset` | A0S7 !전격-캐럿 | Transpom1 | 400 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15166 |
| `SkillData_원작능력_불멸_고도현.asset` | A0EM !명왕의 검술 | Shiki_Attack,Z_Attack | 450 | **target** | Shiki stomp e026 대상 위치, 700/450 그룹 대상(400 pointdmg 한 줄만 시전자 기준 이동) | j:18927, j:18949, j:18967 |
| `SkillData_원작능력_불멸_김용태.asset` | A0LS !천신 | Dragon_Attack,roger_Attack | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19204 |
| `SkillData_원작능력_영원_서민성.asset` | A0LZ !알라바스타-비영 | Uta_Tots | 450 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:20174 |
| `SkillData_원작능력_영원_최상호.asset` | A0J7 !쿠사의 패기 | Cavendish_Attack | 600 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19706 |
| `SkillData_원작능력_전설적인_임채현.asset` | A052 !고무고무 열매 | Legend11 | 345 | **target** | Legend11: e00Z stomp는 LOC를 GetTriggerUnit으로 다시 잡은 뒤 생성 | j:14794 |
| `SkillData_원작능력_전설적인_정윤식.asset` | A074 !불사조불사조열매 | Legend2 | 415 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14719 |
| `SkillData_원작능력_전설적인_정준영.asset` | A0QL !어인족 수장1 | Legend9 | 400 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:14761 |
| `SkillData_원작능력_제한_이충민.asset` | A15N !봉황인 | katakuriAttack | 475 | **unknown** | katakuriAttack엔 자기 블러드러스트뿐, 750000 적 효과 출처 미확정 | j:15405 |
| `SkillData_원작능력_제한_임준성.asset` | A17K !불사조의 불꽃 | Sinobu_Attack | 475 | **target** | 400000 = 475 그룹 대상 위치(firebolt는 단일) | j:15688 |
| `SkillData_원작능력_제한_전법규.asset` | A05M !뇌영 | croTrans | 475 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:15207 |
| `SkillData_원작능력_제한_최영민.asset` | A0GK !단신 최강 | RedAttack | 475 | **line** | carrionswarm(A0PV) 시전자에서 발사되는 파동 | j:15394 |
| `SkillData_원작능력_희귀함_이상혁.asset` | Trig_Unique12 도플라밍고 희귀함 | Unique12 | 450 | **target** | Unique12: udg_Unique_LOC=GetUnitLoc(GetTriggerUnit()) 450 | j:14194 |
| `SkillData_절대쿨_랜덤_한마_바키.asset` | 뱌쿠야 — 5절대쿨 | Byakuya_E | 925 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18565 |
| `SkillData_절대쿨_불멸_정윤식.asset` | 레일리 — 5절대쿨(Kick_1) | Kick_1 | 400 | **line** | 이동하는 더미(e01F, unitC) 위치 — 발차기 돌진 | j:18869 |
| `SkillData_절대쿨_초월_강주혁_AP.asset` | 나미 — 5절대쿨 | Nami_Skill_4 | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17053 |
| `SkillData_절대쿨_초월_두유찬_AD_1.asset` | 사보 — 게이지(Sabo_Skill_1) | Sabo_Skill_1 | 475 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16184 |
| `SkillData_절대쿨_초월_두유찬_AD_3.asset` | 사보 — 게이지+확률(Sabo_Skill_3) | Sabo_Skill_3 | 415 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16195 |
| `SkillData_절대쿨_초월_두유찬_AD_4.asset` | 사보 — 게이지+확률×2(Sabo_Skill_4) | Sabo_Skill_4 | 415 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16201 |
| `SkillData_카이도_불멸_신지우_buster.asset` | 카이도 — 용형 평타(Kaido_Dragon_buster) | Kaido_Dragon_buster | 575 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19446 |
| `SkillData_카이도_불멸_신지우_life100.asset` | 카이도 — 용형 체력100%(Kaido_Dragon_Skill_2·mel | Kaido_Dragon_Skill_2,Kaido_Dragon_melee_1 | 500 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19387, j:19418 |
| `SkillData_카이도_불멸_신지우_skill18.asset` | 카이도 — Kaido_Skill_1_8(신규 확정행) | Kaido_Skill_1_8 | 575 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19346 |
| `SkillData_회수_불멸_고도현_8eab1c6f.asset` | 금사자 시키 천신 - 불멸의 — MANA게이지125.00 | Shiki_Lion | 700 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18927 |
| `SkillData_회수_불멸_고도현_a23cc245.asset` | 금사자 시키 천신 - 불멸의 — 1/10 AND 1/33 AND 1/16 | Shiki_SKill_item | 450 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18949 |
| `SkillData_회수_불멸_고도현_a80b907f.asset` | 금사자 시키 천신 - 불멸의 — 1/10 AND 1/16 | Shiki_champa2 | 450 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18959 |
| `SkillData_회수_불멸_이승우_355f5d39.asset` | 몽키.D.거프 해군영웅 주먹의 거프 - 불멸의 — (게이트 없음) | Garp_AttackDamage | 360 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19074 |
| `SkillData_회수_불멸_이이삭_f50ad66e.asset` | 센고쿠 해군원수 "부처님" - 불멸의 — LIFE게이지75.00 AND  | Sengoku_Skill_Item | 450 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19010 |
| `SkillData_회수_불멸_정윤식_355f5d39.asset` | 실버즈 레일리 명왕 - 불멸의 — (게이트 없음) | LaillySkill1 | 400 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:18910 |
| `SkillData_회수_영원_문필환_9ffa9c45.asset` | /CFFFFFA78알라바스타의 왕녀-ver파이러 — 1/10 | vivi_skill_2 | 450 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:19769 |
| `SkillData_회수_초월_강주혁_AP_b7ae9b41.asset` | 염왕 — 1/15 | Zoro_enfor_3dragon | 425 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16968 |
| `SkillData_회수_초월_구주호_AD_3a13fd3d.asset` | '신'해군대장-초록소 — 1/7 | Rokugu_skill_1 | 485 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17633 |
| `SkillData_회수_초월_구주호_AD_c453ba09.asset` | 해군대장-보르살리노 — 1/17 | Kizaru_01,Kizaru_01_shoot | 385 | **line** | 투사체 더미 위치(Kizaru_01_shoot) | j:16815 |
| `SkillData_회수_초월_노태현_AP_3a13fd3d.asset` | 소울 킹 — 1/7 | Brook_Skill_1 | 360 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:17076, j:17077 |
| `SkillData_회수_초월_양재모_AD_f54a123f.asset` | 로키포트 사건의 주모자 — 버프B03Z==true AND 1/20 | Law_skill_5 | 575 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16030 |
| `SkillData_회수_초월_최상호_AP_355f5d39.asset` | "어둠의 조커"드레스로자의 악몽 — (게이트 없음) | DP_AttackDamage | 275 | **target** | GetUnitLoc(GetTriggerUnit()) 계열(공격받은 적) | j:16714 |
## 합계

| 중심 | 개수 |
|---|---|
| caster | **7** |
| target | **107** |
| 기타 (line 9 · random 7 · mixed 1 · unknown 2) | **19** |
| 합 | 133 |

## 반영 제안 (PM 판단)

- **SkillLevel에 중심 축을 하나 둔다.** 예: `aoeCenter {Target=0, Caster=1}`. **기본값을 Target으로** 하고 위 7개만 Caster로 표시하면 된다(107+16이 Target 근사).
  - 다만 기존 에셋에서 enum의 0이 Target이 되면 직렬화된 에셋 133개 전부가 그 순간 바뀐다. 의도한 변경이지만, 이 표를 대조해 캐스터 7개를 **같은 커밋에서** 표시해야 한다.
- **random 7**: 원작 무작위 반경(30~700)이 우리 범위보다 크거나 비슷한 경우가 많다. 대상 중심으로 두면 첫 대상은 항상 맞고 주변은 원작보다 조금 더 맞는다. A0HG(0~700에 유성)처럼 분산이 큰 것은 7종 문서의 0.66 보정을 참고할 것.
- **line 9**: 빔·돌진은 시전자→대상 선분 위를 훑는다. 대상 중심이 원작에 더 가깝다(맞는 적은 대부분 대상 쪽에 몰려 있다). 시전자 중심으로 두면 선분 앞쪽 절반만 맞는다.
- **mixed 1** (`원작능력_랜덤_이즈미_신이치`): whirlwind(A0LG, aare 550)는 시전자, stomp(A0LF, aare 600)는 대상이다. 효과 단위로 중심을 두지 않으면 한쪽은 틀린다. 에셋을 둘로 쪼개는 게 가장 싸다.
- **unknown 2**:
  - `원작009_H094`(A0WK 950 오라)는 허수아비 소환체가 오라를 낸다. 소환 위치를 따라가지 못했다(T_Ability_hero j:20528에서 A0WK 부여만 확인).
  - `원작능력_제한_이충민`의 750000 적 효과는 katakuriAttack(j:15405)에 자기 블러드러스트밖에 없어서 출처를 확정하지 못했다.

## 한계

- 반경만으로 호출을 고른 곳에서, 같은 트리거 안에 같은 반경의 호출이 둘 이상이면 중심을 합집합으로 봤다. 불일치는 수동 판정으로 풀었다.
- 비율형 효과(multiplier < 1000, 예: 0.02·5.0)는 상수 대조가 안 된다. 그래서 반경 대조(①)에만 기댔다. 카이도 3개, 김건, 윤현모 ace_skill_5, 김민준 Tasigi, 이승우 Garp, 정윤식 LaillySkill1이 여기에 해당하며, 전부 target이다.
- 스크래치 스크립트만 썼고 repo는 이 파일 외에 건드리지 않았다.
