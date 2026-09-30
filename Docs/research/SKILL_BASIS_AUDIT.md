# 스킬 비례 공식(SkillEffectBasis) 점검 — 2026-09-30

구현담당2, 읽기 전용 점검(PM 지시). 코드·에셋은 건드리지 않았다.
원작 대조 원천: `war3map_new_lf.j`(메모리 pending-drafts, 119,737줄). 트리거→원작 유닛은 `Docs/reference/ORIGINAL_DAMAGE_TYPING.csv`, 원작 유닛→로스터는 `MASTER_UID_ROSTER_MAP.csv`에서 가져왔다.

## 한 줄 결론

- **코드는 살아 있다.** basis 13종 모두 계산 분기가 있고(`UnitAttacker.ResolveBaseSkillEffectValue`, `UnitAttacker.cs:1400~1470`), 비례 효과 322개는 전부 `kind=Damage`여서 실제로 읽힌다(`DealSkillDamage`, `:1630`).
- **데이터에 들어간 비율:** 원작 체력 비례 트리거 121개(호출 173건) 가운데 **66개(97건)는 우리 에셋에 계수까지 맞게 들어가 있다.**
- **확정 누락:** **우리 로스터 유닛인데 빠진 것이 21개 트리거(23건)**다. 여기에 **미호크 `MIhawk_Mana`(최대체력×0.05+525만)**가 들어 있다. 메모리 「skill-damage-lives-in-triggers」가 대표 사례로 든 바로 그 식이다.
- 계수가 어긋난 것 2건, 로스터 밖 유닛 14개, 유닛 미상 20개(대부분 플레이어 4명 몫 복제)는 따로 적었다.
- **보스에게는 체력 비례가 0이다.** 적 105종 중 36종이 `takesPercentDamage=0`이고, 이때는 상수항까지 0이 된다. 원작에서는 보스가 별도 고정값 분기로 가는데, 그 분기가 우리에게 없다.

## 1. 코드 — basis별 계산 분기

| # | basis | 계산(`ResolveBaseSkillEffectValue`) | 입력값이 실제로 0이 아닌가 |
|---|---|---|---|
| 0 | Flat | multiplier | — |
| 1 | TargetMaxHpPercent | `TakesPercentDamage ? MaxHp×m+b : 0` | 보스 36종은 0(상수항 포함) |
| 2 | TargetCurrentHpPercent | `TakesPercentDamage ? Hp×m+b : 0` | 위와 같음 |
| 3 | CasterAttackPower | `AttackDamage×m+b` | 산다. AttackDamage에 연구소 배수(UpgradeMultiplier)·연구 가산이 들어 있다(`:99`) |
| 4 | ResearchLevel | `CountResearchLevel()×m+b` | 산다. 공격타입 강화 구매 때 `UnitUpgrades.cs:330`에서 +1 |
| 5 | ReceivedDamage | `recentAttackDamage×m+b` | 평타 발동 경로에서만 값이 있다. 쿨다운·오라 경로는 0 |
| 6 | TargetMoveSpeed | `MoveSpeed×m+b` | 산다 |
| 7 | CasterGaugeValue | `manaGaugeCounter×m+b` | 산다(E 경로 +5가 없어 과소한 것은 이미 알려짐) |
| 8 | CasterSkillLevel | `(레벨 1/2)×m+b` | 산다 |
| 9~11 | CasterStrength/Agility/Intelligence | `스탯×m+b` | 초월 25종만 값이 있다(`generate_hero_stats.py`) |
| 12 | CasterSelfUpgradeLevel | `selfUpgradeLevel×m+b` | 산다 |

- 분기 없는 basis(죽은 enum)는 **0개**다. `default: return 0`에 걸리는 값도 데이터에 없다.
- 체력 비례 두 종 뒤에는 `EnemyDummy.PercentDamageTakenMultiplier`가 곱해진다(A11S 감수성). 원작 `×(0.2+0.05×A11S레벨)` 꼴 40건이 바로 이 축이라, 축 자체는 맞다.
- 다만 원작은 A11S 항을 **체력 비례 호출 안에서만** 곱하는데, 우리는 `DealSkillDamage`에서 스킬 피해 전반에 곱한다. 이것은 이번 점검 범위 밖이다. 차이가 있을 수 있다는 것만 적어 둔다.

## 2. 데이터 — basis별 에셋 수

`Assets/Data/UnitSkills/*.asset` 385개, 효과 834개. 385개 모두 다른 에셋·프리팹에서 GUID로 참조되고 있다(고아 에셋 0). YAML 한 줄 붙음 함정으로 파싱이 어긋난 파일도 0이다(`basis:` 줄 수 = 파싱된 효과 수).

| basis | 효과 수 | 비고 |
|---|---:|---|
| Flat | 512 | |
| CasterAttackPower | 125 | 대부분 w3a 「배수」(치명타형, 예: 미호크 A0HV 3.5)에서 왔다. 원작 j에는 평타 비례 식(BlzGetUnitBaseDamage)이 **0건**이다 |
| TargetMaxHpPercent | 91 | |
| TargetCurrentHpPercent | 51 | |
| ReceivedDamage | 29 | 원작 GetEventDamage 34건 |
| ResearchLevel | 9 | 원작 GetPlayerTechCountSimple 18건 |
| CasterGaugeValue | 5 | |
| CasterStrength | 5 | 원작 GetHeroStat 12건 |
| TargetMoveSpeed | 4 | |
| CasterSkillLevel | 3 | 원작 능력 레벨(A11S 제외) 11건 |
| CasterAgility · CasterIntelligence · CasterSelfUpgradeLevel | **0** | 분기는 있지만 쓰는 데이터가 없다 |

- 비례 basis를 하나라도 쓰는 에셋은 200개이고, 체력 비례를 쓰는 에셋은 76개다(목록은 부록 A).
- 체력 비례 효과 142개 중 **32개는 상수항(bonus>0)이 있다.** 보스 상대로는 이 상수항도 0이 된다(§4).

## 3. 원작 대조 — 체력 비례 피해

j에서 피해 호출(`RRD`·`UnitDamageTarget*`)은 695건이다. 그중 체력이 들어간 호출을 모두 뽑았다.

- 최대체력 119건, 현재체력 59건이다. 여기서 잃은체력(`MAX−LIFE`) 5건은 양쪽 모두에 걸린다. 제 추출은 최대체력 114·현재체력 54·잃은체력 5, 합 173건이고 j 전체 개수와 맞다.
- 호출을 둘러싼 함수 이름에서 `Trig_`·`_Actions`·`_FuncNNN`를 떼어 **트리거 키**로 삼았다. 이렇게 키가 121개 나왔다.
- 이 키를 우리 에셋 설명의 `키#순번` 표기 또는 `Trig_키` 표기와 대조했다.

| 판정 | 트리거 | 호출 | 뜻 |
|---|---:|---:|---|
| A 담김 | 40 | 60 | `키#n`으로 참조되고, 그 에셋에 체력 basis가 있다 |
| C 언급+체력축 있음 | 26 | 37 | `Trig_키`로 언급되고, 체력 basis가 있다. 계수를 대조해 보니 24개 일치 → 사실상 담김 |
| B 참조됨·체력축 없음 | 5 | 7 | 참조는 되는데 체력 항을 Flat·연구 등으로 대신했다 |
| D 언급만 | 1 | 1 | 언급만 있고 체력 basis가 없다 |
| **E 없음(로스터 유닛)** | **15** | **15** | 우리 로스터 유닛인데 어디에도 없다 |
| F 로스터 밖 유닛 | 14 | 19 | 히든조합·소환체 등 우리 로스터에 없는 원작 유닛 |
| G 유닛 미상 | 20 | 34 | 트리거→유닛 대응이 CSV에 없다 |

### 3-1. 확정 누락 후보 (B·D·E = 21개 트리거·23건, 전부 우리 로스터 유닛)

| 트리거 | 로스터 | 원작 식(요지) | 우리 쪽 |
|---|---|---|---|
| **MIhawk_Mana** | 영원_최상호(h058 미호크) | 최대체력×0.05 **+5,250,000**, 게이지 175 | 없음. 미호크 에셋은 A0HV(평타×3.5)·게이지 750,000 Flat뿐 |
| Z_Skill_Mana | 불멸_박은석 | 4,850,000 + 잃은체력×0.08 | 없음 |
| Uta_skill_1 / _double | 영원_김정래 | 최대체력×0.10×A11S / 250,000+잃은체력×0.12 | 없음. Uta_ArmyS는 Flat(B) |
| Sirahoshi_skill_Mana | 초월_유재헌_ADAP | 3,350,000 + 잃은체력×0.06 | 없음 |
| Tichi_skill_3 | 초월_임채민_AP | 1,750,000+STR×50,000 + 현재체력×0.03 | 없음 |
| Robine_skill_1 | 초월_강재규_AP | 1,850,000 + 현재체력×0.01 | 없음 |
| BrookAttack | 초월_노태현_AP | 최대체력×0.06×A11S | 없음 |
| Luchi_Skill_1_Kick2 / 2_ZiGun | 초월_임장혁_AD | 최대체력×0.04 / 현재체력×0.25×(플레이 횟수) | 없음 |
| Law_Skill_2 / _reinforce | 초월_양재모_AD | 현재체력×0.2 (대상 udg_Hero[1]) | 없음 |
| SandiAttack_Upgrade | 초월_배성령_AD | 현재체력×0.10×A11S | 없음 |
| Hidden5 | 히든_호치킨 | 최대체력×0.1 | 없음 |
| Enel_Mana | 제한_전법규 | 주 대상 최대체력×0.02(범위) | 없음 |
| DP_Attack_gaksung | 초월_최상호_AP | 현재체력×0.01+10,000 | 더미채널 10,000 Flat만(D) |
| Cavendish_skill_Mana | 영원_최상호(h05B) | 현재체력×0.01 두 건 | 연구·Flat만(B) |
| Garp_AttackDamage | 불멸_이승우 | 현재체력 두 건 | ReceivedDamage만(B) |
| Usop_Skill_2 | 초월_조성진_AD | 최대체력 한 건 | Flat만(B) |
| Uta_ArmyS | 영원_김정래 | 현재체력 한 건 | Flat만(B) |
| vivi_skill_2 | 영원_문필환 | 최대체력 한 건 | Flat만(B) |

- **영원 등급만 추려도 미호크·우타·카벤디시·비비가 걸린다.** 초월 등급은 9명(유재헌·임채민·강재규·노태현·임장혁·양재모·배성령·조성진·최상호_AP)이다.
- 이 스킬들의 후반 화력은 고정분만 남거나 통째로 없다. 메모리가 경고한 「후반에 스킬이 죽는다」가 이 21개 트리거에 해당한다.
- E의 다수는 게이지(MANA게이지 N)·특성·버프 조건 게이트 뒤에 있다. 게이트 배정 CSV(`ORIGINAL_UNLISTED_SKILL_EFFECTS.csv`)에 행 자체가 없는 경우가 많다. 누락 원인은 「게이트 배정 단계에서 행이 안 만들어졌다」로 추정하고, 확인은 하지 않았다.

### 3-2. 계수 불일치 (C 가운데 2건)

- `Kick_1`(불멸_정윤식): 원작은 **최대체력×0.15**인데 우리는 **현재체력×1.0**(`SkillData_절대쿨_불멸_정윤식`)이다. basis와 계수가 둘 다 다르다. 다른 원작 식을 옮겨 적었을 가능성이 있어 확인이 필요하다.
- `Sabo_Skill_4`(초월_두유찬_AD): 원작은 최대체력×0.04×(0.2+0.10×A11S)인데 우리는 최대체력×0.01이다. 원작에는 이 트리거에 식이 두 건 있어서, 다른 한 건과 짝지어졌을 수 있다.
- 나머지 C 24개는 계수와 상수항이 일치한다. `Legend6`의 잃은체력은 Max×0.04 + Cur×−0.04로 분해해 정확히 담겨 있다.

### 3-3. 누락으로 세면 안 되는 것 (F·G)

- **F 14개**: 히든조합 7종(Hidden1·4·8·11·12·15·16), 소환체 거울(`Legend30mirror`·`Sinobu_Skill_5_mirror`), `Jotaro_R`·`Cavendish_skill_6`(하쿠바)·`Dragon_Skill_2dummy`·`SandiAttack`(H09H)·`Usop_Skill_10Ton`(H09C). 이 원작 유닛들이 우리 로스터에 없다. 로스터에 들이는 판단이 먼저다.
- **G 20개**:
  - 플레이어 4명 몫 복제: `Shanks_ET_pegi1~4` 16건, `Legend10_shanks_pegi1~4`, `Legend14han_petrification1~4`. 원작 능력 수로는 3개다.
  - 하위 함수: `Legend16sc_Filterfunc001/002`는 Legend16sc(C, 담김)의 필터 함수다.
  - 유닛 없는 시스템: `Absolb1`·`EXPLOSION`·`Pirates_docking_1`.
  - 나머지: `vivi_Skill_5_buff`·`vivi_skill_Double2`·`Legend_han_kiss_damagesc`. 소속 유닛은 j에서 더 따라가야 한다.
  - 샹크스 계열 3종이 어느 로스터 유닛인지 CSV에 비어 있다. **만약 로스터 유닛이면 누락 후보가 3개 더 는다.**

### 3-4. 축 차이 (담긴 것 포함, 8건)

원작 8건은 **각 적 자신이 아니라 주 대상(또는 트리거 유닛)의 체력**을 기준으로 범위 피해를 준다. 해당: Enel_Mana, Law_skill_5, Cavendish_skill_Mana×2, Cavendish_skill_6, vivi_Skill_5_buff, Legend16sc_Filterfunc×2.
우리 `TargetMaxHpPercent`는 맞는 적 각자의 체력을 쓴다. 범위 안에 체력이 다른 적이 섞이면 값이 달라진다. 이 중 우리에 담긴 것은 Law_skill_5 하나다.

## 4. 보스 게이트 — 「값이 실제로 쓰이나」

- 적 105종 중 36종이 `takesPercentDamage: 0`(보스)이다. 체력 비례 효과 142개는 이 36종에게 **0**이 된다.
- bonus가 있는 32개는 상수항도 같이 사라진다(`ResolveBaseSkillEffectValue` 주석: 「게이트에 막히면 상수항도 0」).
- 예: 부릉냐 `3,125,000 + 최대체력×0.02`는 보스에게 0이다.
- 원작은 `GetUnitPointValue(대상)<200`으로 갈라서, 보스에게는 **별도 고정값 분기**(12,500·150,000·300,000 등)로 보낸다. 그 분기가 우리 데이터에 없다(`EnemyData.cs:176` 주석이 이미 인정). 보스전 스킬 화력이 과소한 원인 후보다. 숫자 확인은 원작 j의 `GetUnitPointValue` 분기를 전수로 뽑는 것이 다음 단계다.

## 5. 다음 할 일 제안 (PM 판단)

1. 3-1의 21개 트리거를 에셋으로 배정한다. 특히 영원 4명(미호크·우타·카벤디시·비비). 대부분 게이지 게이트라 게이트 배정 CSV에 행을 추가하는 작업이 된다.
2. 3-2의 Kick_1·Sabo_Skill_4 두 건을 원문과 다시 대조한다.
3. §4 보스 고정값 분기를 조사한다(리서치담당 몫).
4. G의 샹크스 3종·비비 2종이 어느 로스터 유닛인지 확인한다.

## 부록 A. 비례 basis를 쓰는 에셋 (200개)

| 에셋 | 비례 basis(효과 수) |
|---|---|
| `SkillData_게이트_랜덤_이즈미_신이치_351a0f00` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_랜덤_이즈미_신이치_41677101` | TargetCurrentHpPercent 2 |
| `SkillData_게이트_랜덤_이타도리_유지_84bf5e76` | TargetMaxHpPercent 2 |
| `SkillData_게이트_랜덤_카마도_탄지로_3c5f8bd9` | TargetMaxHpPercent 2 |
| `SkillData_게이트_랜덤_카마도_탄지로_d370bb23` | TargetMaxHpPercent 2 |
| `SkillData_게이트_랜덤_카마도_탄지로_ff8bed43` | TargetMaxHpPercent 6 |
| `SkillData_게이트_랜덤_한마_바키_355f5d39` | TargetMaxHpPercent 1 |
| `SkillData_게이트_랜덤_호시노_아이_33bd8aa4` | CasterGaugeValue 1 · TargetMaxHpPercent 1 |
| `SkillData_게이트_랜덤_호시노_아이_3a13fd3d` | CasterGaugeValue 4 · TargetMaxHpPercent 1 |
| `SkillData_게이트_랜덤_호시노_아이_55b5cff0` | TargetMaxHpPercent 1 |
| `SkillData_게이트_변화됨_김건_355f5d39` | ReceivedDamage 1 |
| `SkillData_게이트_변화됨_최상호_9ffa9c45` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_불멸_고도현_caa364ec` | TargetMaxHpPercent 4 |
| `SkillData_게이트_불멸_신지우_84bf5e76` | TargetMaxHpPercent 1 |
| `SkillData_게이트_불멸_신지우_d33a6a78` | TargetMaxHpPercent 3 |
| `SkillData_게이트_영원_윤현모_aa9cf1bd` | TargetMaxHpPercent 1 |
| `SkillData_게이트_영원_이지원_3ba504d5` | ReceivedDamage 4 · TargetMoveSpeed 2 |
| `SkillData_게이트_영원_최상호_a8343962` | ResearchLevel 2 |
| `SkillData_게이트_전설적인_이현주_0e5cf375` | TargetMaxHpPercent 1 |
| `SkillData_게이트_전설적인_임건웅_87ed0ff5` | TargetCurrentHpPercent 3 |
| `SkillData_게이트_전설적인_임건웅_9ffa9c45` | TargetCurrentHpPercent 2 |
| `SkillData_게이트_전설적인_임채민_355f5d39` | ReceivedDamage 1 |
| `SkillData_게이트_전설적인_임채현_03847fe6` | ReceivedDamage 1 |
| `SkillData_게이트_전설적인_정준영_3a13fd3d` | TargetMaxHpPercent 1 |
| `SkillData_게이트_전설적인_진연서_483e0464` | TargetMaxHpPercent 2 |
| `SkillData_게이트_전설적인_최상호_355f5d39` | TargetMaxHpPercent 2 |
| `SkillData_게이트_제한_강보명_57c27e37` | TargetMaxHpPercent 1 |
| `SkillData_게이트_제한_김민규_136d09f9` | ReceivedDamage 1 |
| `SkillData_게이트_제한_박성호_3a13fd3d` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_제한_박성호_874ea782` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_제한_이유범_351a0f00` | TargetMaxHpPercent 2 |
| `SkillData_게이트_제한_전법규_9ffa9c45` | ResearchLevel 1 |
| `SkillData_게이트_초월_구주호_AD_355f5d39` | ReceivedDamage 3 |
| `SkillData_게이트_초월_김경현_AP_4266b6e3` | TargetMoveSpeed 1 |
| `SkillData_게이트_초월_김경현_AP_b7ae9b41` | TargetMoveSpeed 1 |
| `SkillData_게이트_초월_김민준_AP_2a646778` | TargetMaxHpPercent 1 |
| `SkillData_게이트_초월_김민준_AP_355f5d39` | CasterStrength 2 · ReceivedDamage 2 |
| `SkillData_게이트_초월_두유찬_AD_8eab1c6f` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_초월_배성령_AD_18aa2343` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_초월_배성령_AD_3c5f8bd9` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_초월_신문철_AP_355f5d39` | ReceivedDamage 1 |
| `SkillData_게이트_초월_신문철_AP_55ecbfb2` | TargetMaxHpPercent 2 |
| `SkillData_게이트_초월_양재모_AD_29846395` | ReceivedDamage 2 |
| `SkillData_게이트_초월_임장혁_AD_5e483927` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_초월_임채민_AP_42844439` | CasterStrength 2 |
| `SkillData_게이트_초월_조성진_AD_29846395` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_초월_최상호_AD_3c5f8bd9` | CasterSkillLevel 1 |
| `SkillData_게이트_초월_최상호_AD_b8d2fd85` | CasterSkillLevel 2 |
| `SkillData_게이트_초월_최상호_AP_18aa2343` | CasterStrength 1 |
| `SkillData_게이트_초월_최상호_AP_355f5d39` | ReceivedDamage 1 |
| `SkillData_게이트_초월_최상호_AP_5c20b80f` | TargetCurrentHpPercent 1 |
| `SkillData_게이트_히든_최윤서_3c5f8bd9` | TargetMaxHpPercent 1 |
| `SkillData_게이트_히든_호치킨_21297197` | TargetMaxHpPercent 1 |
| `SkillData_게이트_히든_황정기_2251ce1a` | ResearchLevel 1 |
| `SkillData_게이트_히든_황정기_2a646778` | ResearchLevel 3 |
| `SkillData_더미채널_영원_김정래_A0F1` | CasterAttackPower 1 |
| `SkillData_원작006_H096` | TargetCurrentHpPercent 1 |
| `SkillData_원작023_h04C` | TargetMaxHpPercent 3 |
| `SkillData_원작024_h04D` | TargetMaxHpPercent 2 |
| `SkillData_원작028_h05C` | ResearchLevel 2 · TargetMaxHpPercent 2 |
| `SkillData_원작능력_랜덤_가사이_유노` | CasterAttackPower 1 |
| `SkillData_원작능력_랜덤_모몬가` | TargetMaxHpPercent 1 |
| `SkillData_원작능력_랜덤_야사카_카나코` | CasterAttackPower 1 |
| `SkillData_원작능력_랜덤_이민형` | TargetCurrentHpPercent 3 |
| `SkillData_원작능력_랜덤_이즈미_신이치` | TargetCurrentHpPercent 3 |
| `SkillData_원작능력_랜덤_이타도리_유지` | TargetMaxHpPercent 2 |
| `SkillData_원작능력_랜덤_주호페이크` | CasterAttackPower 1 |
| `SkillData_원작능력_랜덤_주호페이크_A0KU` | CasterAttackPower 1 |
| `SkillData_원작능력_랜덤_카마도_탄지로` | TargetMaxHpPercent 9 |
| `SkillData_원작능력_랜덤_한마_바키` | CasterAttackPower 1 · TargetMaxHpPercent 1 |
| `SkillData_원작능력_랜덤_호시노_아이` | TargetMaxHpPercent 2 |
| `SkillData_원작능력_랜덤_호시노_아이_A0KT` | CasterAttackPower 1 |
| `SkillData_원작능력_변화됨_강재규` | CasterAttackPower 1 |
| `SkillData_원작능력_변화됨_김건` | TargetCurrentHpPercent 1 · TargetMaxHpPercent 2 |
| `SkillData_원작능력_변화됨_박은석` | CasterAttackPower 2 |
| `SkillData_원작능력_불멸_고도현` | CasterAttackPower 1 · TargetMaxHpPercent 1 |
| `SkillData_원작능력_불멸_김용태_A0FI` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_박은석` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_신지우` | CasterAttackPower 4 · TargetMaxHpPercent 1 |
| `SkillData_원작능력_불멸_이승우_A044` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_이이삭` | TargetCurrentHpPercent 2 |
| `SkillData_원작능력_불멸_정윤식_A0EM` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_정윤식_A0EO` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_정준영_A0IT` | CasterAttackPower 2 |
| `SkillData_원작능력_불멸_정준영_A0SN` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_정준영_A0YY` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_정준영_A0YZ` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_정준영_A0Z8` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_정준영_A0ZV` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_정준영_A13O` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_정준영_A13P` | CasterAttackPower 1 |
| `SkillData_원작능력_불멸_정준영_A13Q` | CasterAttackPower 1 |
| `SkillData_원작능력_안흔함_문필환` | CasterAttackPower 1 |
| `SkillData_원작능력_안흔함_박민수` | CasterAttackPower 1 |
| `SkillData_원작능력_안흔함_엄태웅` | CasterAttackPower 1 |
| `SkillData_원작능력_안흔함_이재윤` | CasterAttackPower 1 |
| `SkillData_원작능력_안흔함_이호준` | CasterAttackPower 1 |
| `SkillData_원작능력_안흔함_황정기` | CasterAttackPower 1 |
| `SkillData_원작능력_영원_김정래` | CasterAttackPower 5 |
| `SkillData_원작능력_영원_문필환_A0LZ` | CasterAttackPower 1 |
| `SkillData_원작능력_영원_서민성` | CasterAttackPower 2 |
| `SkillData_원작능력_영원_최상호` | CasterAttackPower 1 |
| `SkillData_원작능력_영원_최상호_A0EA` | CasterAttackPower 1 |
| `SkillData_원작능력_영원_최상호_A0HV` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_박민석` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_박민수` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_박성호` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_박은석` | CasterAttackPower 1 · TargetCurrentHpPercent 1 · TargetMaxHpPercent 1 |
| `SkillData_원작능력_전설적인_백기현` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_신문철` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_신지우` | TargetCurrentHpPercent 1 |
| `SkillData_원작능력_전설적인_양재모` | TargetMaxHpPercent 4 |
| `SkillData_원작능력_전설적인_이시원` | CasterAttackPower 1 · TargetCurrentHpPercent 2 |
| `SkillData_원작능력_전설적인_이일중` | CasterAttackPower 1 · TargetMaxHpPercent 4 |
| `SkillData_원작능력_전설적인_이재윤` | TargetMaxHpPercent 2 |
| `SkillData_원작능력_전설적인_이현주` | CasterAttackPower 1 · TargetCurrentHpPercent 1 |
| `SkillData_원작능력_전설적인_임건웅` | CasterAttackPower 1 · TargetCurrentHpPercent 2 |
| `SkillData_원작능력_전설적인_임채민` | TargetCurrentHpPercent 2 |
| `SkillData_원작능력_전설적인_임채민_A048` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_임채민_A074` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_임채현` | TargetCurrentHpPercent 1 |
| `SkillData_원작능력_전설적인_정윤식` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_정준영` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_정준영_A06M` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_정준영_A0IS` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_정준영_A0PQ` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_진연서_A0EN` | CasterAttackPower 1 |
| `SkillData_원작능력_전설적인_최상호` | TargetCurrentHpPercent 3 · TargetMaxHpPercent 3 |
| `SkillData_원작능력_전설적인_홍인창` | CasterAttackPower 1 |
| `SkillData_원작능력_제한_강보명_A0GK` | CasterAttackPower 1 |
| `SkillData_원작능력_제한_강보명_A0GM` | CasterAttackPower 1 |
| `SkillData_원작능력_제한_김민규` | CasterAttackPower 1 · TargetMaxHpPercent 1 |
| `SkillData_원작능력_제한_김민규_A0QO` | CasterAttackPower 1 |
| `SkillData_원작능력_제한_박성호` | CasterAttackPower 1 · TargetMaxHpPercent 1 |
| `SkillData_원작능력_제한_이유범` | TargetMaxHpPercent 1 |
| `SkillData_원작능력_제한_이충민` | CasterAttackPower 1 |
| `SkillData_원작능력_제한_임준성` | CasterAttackPower 1 · TargetMaxHpPercent 4 |
| `SkillData_원작능력_제한_전법규` | CasterAttackPower 1 |
| `SkillData_원작능력_제한_최영민` | CasterAttackPower 2 · TargetMaxHpPercent 1 |
| `SkillData_원작능력_제한_최영민_A0QV` | CasterAttackPower 1 |
| `SkillData_원작능력_제한_최영민_A17K` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A03W` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A04D` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A051` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0AG` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0AK` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0CS` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0G4` | CasterAttackPower 2 |
| `SkillData_원작능력_초월_황준석_ADAP_A0HA` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0HK` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0HN` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0IT` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0KA` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0KE` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0X0` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A0ZV` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A101` | CasterAttackPower 1 |
| `SkillData_원작능력_초월_황준석_ADAP_A103` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_양재모` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_왕승환` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_유재헌` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_이정범` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_이지원` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_이현빈` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_임장혁` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_임채준` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_정승준` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_조도연` | CasterAttackPower 1 |
| `SkillData_원작능력_특별함_조세민` | CasterAttackPower 1 |
| `SkillData_원작능력_특수함_장진희` | CasterAttackPower 1 |
| `SkillData_원작능력_특수함_황길라` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_김만경` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_김청운` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_노수신` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_노태현` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_두유찬` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_박기찬` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_배병규` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_배성령` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_서민성` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_양재모` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_장태영` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_장하민` | CasterAttackPower 1 · TargetCurrentHpPercent 2 |
| `SkillData_원작능력_희귀함_조현규` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_최상호_오타쿠의길` | CasterAttackPower 1 |
| `SkillData_원작능력_희귀함_최상호_윤식파의두뇌` | CasterAttackPower 1 · TargetCurrentHpPercent 2 |
| `SkillData_원작능력_희귀함_최현우` | TargetCurrentHpPercent 2 |
| `SkillData_원작능력_희귀함_현성현` | CasterAttackPower 1 · TargetCurrentHpPercent 2 |
| `SkillData_원작능력_희귀함_황준석` | TargetCurrentHpPercent 1 |
| `SkillData_절대쿨_불멸_정윤식` | TargetCurrentHpPercent 1 |
| `SkillData_절대쿨_초월_두유찬_AD_4` | TargetMaxHpPercent 1 |
| `SkillData_카이도_불멸_신지우_buster` | ReceivedDamage 2 |
| `SkillData_카이도_불멸_신지우_life100` | ReceivedDamage 4 |
| `SkillData_카이도_불멸_신지우_skill18` | ReceivedDamage 2 |
| `SkillData_회수_불멸_이승우_355f5d39` | ReceivedDamage 2 |
| `SkillData_회수_불멸_정윤식_355f5d39` | ReceivedDamage 1 |
| `SkillData_회수_영원_문필환_55b5cff0` | TargetMaxHpPercent 2 |
| `SkillData_회수_초월_김만경_AD_0ac0451e` | TargetMaxHpPercent 1 |
| `SkillData_회수_초월_양재모_AD_f54a123f` | TargetCurrentHpPercent 1 |
| `SkillData_회수_초월_최상호_AP_355f5d39` | ReceivedDamage 1 |

## 부록 B. 원작 체력 비례 트리거 121개 판정

| 판정 | 원작 트리거 | 체력 종류(호출 수) | 원작 유닛 | 우리 로스터 | j 줄 |
|---|---|---|---|---|---|
| A 담김 | `Akainu_02` | 최대체력 1 | H095 | 초월_김만경_AD | 96198 |
| A 담김 | `Akainu_02_hidden` | 최대체력 1 | h03Z | 히든_호치킨 | 88748 |
| A 담김 | `BronyaMotar_E` | 최대체력 1 | h09N | 랜덤_호시노_아이 | 105602 |
| A 담김 | `BronyaMotar_R` | 최대체력 1 | h09N | 랜덤_호시노_아이 | 105518 |
| A 담김 | `BronyaMotar_laser` | 최대체력 1 | h09N | 랜덤_호시노_아이 | 105417 |
| A 담김 | `DP_Skill_2` | 현재체력 1 | H09E | 초월_최상호_AP | 98566 |
| A 담김 | `Ed_Skill_1` | 최대체력 2 | h04A | 불멸_고도현 | 108802, 108838 |
| A 담김 | `Ed_Skill_1_Item` | 최대체력 2 | h04A | 불멸_고도현 | 108860, 108897 |
| A 담김 | `Gaban_Skill_1` | 최대체력 1 | h04F | 불멸_신지우 | 109137 |
| A 담김 | `Gaban_Skill_mana` | 최대체력 3 | h04F | 불멸_신지우 | 109160, 109162, 109164 |
| A 담김 | `Law_skill_5` | 현재체력 1 | H096 | 초월_양재모_AD | 95712 |
| A 담김 | `Legend13_2` | 최대체력 1 | h02S | 전설적인_정준영 | 90327 |
| A 담김 | `Legend19_0` | 현재체력 2 | h033 | 전설적인_임건웅 | 90722, 90728 |
| A 담김 | `Legend19_1` | 현재체력 3 | h033 | 전설적인_임건웅 | 90760, 90774, 90788 |
| A 담김 | `Legend32_neko1` | 최대체력 2 | h09Z | 전설적인_진연서 | 91485, 91532 |
| A 담김 | `Legend51` | 최대체력 2 | h03B | 전설적인_최상호 | 89921, 89954 |
| A 담김 | `Legend_han_1` | 최대체력 1 | h032 | 전설적인_이현주 | 90388 |
| A 담김 | `Luchi_Skill_3_6kinggun` | 현재체력 1 | H08W | 초월_임장혁_AD | 95014 |
| A 담김 | `Marco_S1` | 현재체력 1 | h08O | 제한_박성호 | 94612 |
| A 담김 | `Marco_S2` | 현재체력 1 | h08O | 제한_박성호 | 94661 |
| A 담김 | `Minato_E` | 최대체력 2 | h09W | 랜덤_이타도리_유지 | 103803, 103805 |
| A 담김 | `Red_skill_1` | 최대체력 1 | h05G | 제한_강보명 | 93045 |
| A 담김 | `Ruffy_Mana` | 최대체력 2 | H099 | 초월_신문철_AP | 95132, 95169 |
| A 담김 | `Ryougi_Shiki_2` | 최대체력 2 | h0A2 | 랜덤_카마도_탄지로 | 104060, 104062 |
| A 담김 | `Ryougi_Shiki_R2` | 최대체력 2 | h0A2 | 랜덤_카마도_탄지로 | 104545, 104547 |
| A 담김 | `Ryougi_Skill_3` | 최대체력 2 | h0A2 | 랜덤_카마도_탄지로 | 104238, 104240 |
| A 담김 | `Ryougi_Skill_3_double` | 최대체력 2 | h0A2 | 랜덤_카마도_탄지로 | 104358, 104360 |
| A 담김 | `Ryougi_Skill_3_triple` | 최대체력 2 |  |  | 104450, 104452 |
| A 담김 | `Sabo_Mana` | 현재체력 1 | H092 | 초월_두유찬_AD | 96257 |
| A 담김 | `Sandi_skill_1` | 현재체력 1 | H09H |  | 95836 |
| A 담김 | `Sandi_skill_Mana2` | 현재체력 1 | H09H |  | 95899 |
| A 담김 | `Sinobu_Skill_hp2` | 최대체력 2 | h084 | 제한_이유범 | 94115, 94118 |
| A 담김 | `Tasigi_03` | 최대체력 2 | H05N | 초월_김민준_AP | 97296, 97323 |
| A 담김 | `Transpom_dp` | 현재체력 1 | h05S | 변화됨_최상호 | 92183 |
| A 담김 | `Usop_Skill_Mana` | 현재체력 1 | H09B | 초월_조성진_AD | 98689 |
| A 담김 | `byakuya_Chan` | 최대체력 1 | h08I | 랜덤_한마_바키 | 106019 |
| A 담김 | `deken_Mana` | 최대체력 1 | h03R | 히든_최윤서 | 88961 |
| A 담김 | `kikoyou_hp` | 현재체력 1 | h0BF | 랜덤_이즈미_신이치 | 105181 |
| A 담김 | `kikoyou_skill_1` | 현재체력 2 | h0BF | 랜덤_이즈미_신이치 | 105001, 105003 |
| A 담김 | `vivi_Skill_4_Mana` | 최대체력 2 | h057 | 영원_문필환 | 111562, 111579 |
| B 참조됨·HP축 없음 | `Cavendish_skill_Mana` | 현재체력 2 | h05B | 영원_최상호 | 111057, 111064 |
| B 참조됨·HP축 없음 | `Garp_AttackDamage` | 현재체력 2 | h04C | 불멸_이승우 | 108348, 108350 |
| B 참조됨·HP축 없음 | `Usop_Skill_2` | 최대체력 1 | H09B | 초월_조성진_AD | 98652 |
| B 참조됨·HP축 없음 | `Uta_ArmyS` | 현재체력 1 | h067 | 영원_김정래 | 112682 |
| B 참조됨·HP축 없음 | `vivi_skill_2` | 최대체력 1 | h057 | 영원_문필환 | 111234 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Dragon_Skill_1_T` | 최대체력 2 | h04D | 불멸_정준영 | 108231, 108234 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Garp_Mana2` | 최대체력 3 | h04C | 불멸_이승우 | 108382, 108415, 108427 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Kick_1` | 최대체력 1 | h049 | 불멸_정윤식 | 107493 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend16sc` | 현재체력 1 | h03C | 전설적인_신지우 | 90527 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend1sc` | 현재체력 1 | h034 | 전설적인_임건웅 | 89792 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend21sc` | 현재체력 1 | h02R | 전설적인_임채민 | 90834 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend23` | 현재체력 1, 최대체력 1 | h03I | 전설적인_박은석 | 90941, 90943 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend26` | 현재체력 1 | h02U | 전설적인_홍인창 | 90976 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend28koby` | 현재체력 1 | h03E | 전설적인_이시원 | 91096 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend29` | 최대체력 1 | h03K | 전설적인_이재윤 | 91126 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend30` | 최대체력 2 | h042 | 전설적인_이일중 | 91172, 91174 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend32` | 최대체력 2 | h09Z | 전설적인_진연서 | 91454, 91465 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Legend6` | 잃은체력 1 | h030 | 전설적인_최상호 | 89978 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Light1` | 현재체력 2 | h073 | 랜덤_이민형 | 102564, 102579 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `RedAttack` | 최대체력 1 | h05G | 제한_강보명 | 93031 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Sabo_Skill_4` | 최대체력 2 | H092 | 초월_두유찬_AD | 96392, 96394 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Sengoku_Attack` | 현재체력 2 | h04E | 불멸_이이삭 | 107893, 107899 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Sinobu_Attack` | 최대체력 2 | h084 | 제한_이유범 | 94055, 94057 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Transpom6` | 현재체력 1, 최대체력 1 | h07N | 변화됨_김건 | 92308, 92310 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Unique19` | 현재체력 1 | h026 | 희귀함_현성현 | 87847 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Unique20` | 현재체력 1 | h01M | 희귀함_최상호_윤식파의두뇌 | 87872 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Unique21` | 현재체력 1 | h01S | 희귀함_장하민 | 87885 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Unique22` | 현재체력 1 | h025 | 희귀함_최현우 | 87908 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `Z_Attack` | 최대체력 1 | h04G | 불멸_박은석 | 108541 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `hancock_skill_Mana` | 최대체력 1 | h05C | 영원_조세민 | 110673 |
| C 언급 에셋에 HP축 있음(대응 불확실) | `ichigo1` | 최대체력 1 | h071 | 랜덤_모몬가 | 102805 |
| D 언급만·HP축 없음 | `DP_Attack_gaksung` | 현재체력 1 | H09D | 초월_최상호_AP | 98429 |
| E 없음(로스터 유닛) | `BrookAttack` | 최대체력 1 | H09I | 초월_노태현_AP | 99900 |
| E 없음(로스터 유닛) | `Enel_Mana` | 최대체력 1 | h05E | 제한_전법규 | 92424 |
| E 없음(로스터 유닛) | `Hidden5` | 최대체력 1 | h03Z | 히든_호치킨 | 88661 |
| E 없음(로스터 유닛) | `Law_Skill_2` | 현재체력 1 | H096 | 초월_양재모_AD | 95651 |
| E 없음(로스터 유닛) | `Law_Skill_2_reinforce` | 현재체력 1 | H096 | 초월_양재모_AD | 95671 |
| E 없음(로스터 유닛) | `Luchi_Skill_1_Kick2` | 최대체력 1 | H08W | 초월_임장혁_AD | 94913 |
| E 없음(로스터 유닛) | `Luchi_Skill_2_ZiGun` | 현재체력 1 | H08W | 초월_임장혁_AD | 94973 |
| E 없음(로스터 유닛) | `MIhawk_Mana` | 최대체력 1 | h058 | 영원_최상호 | 111948 |
| E 없음(로스터 유닛) | `Robine_skill_1` | 현재체력 1 | H098 | 초월_강재규_AP | 96436 |
| E 없음(로스터 유닛) | `SandiAttack_Upgrade` | 현재체력 1 | H09G | 초월_배성령_AD | 95783 |
| E 없음(로스터 유닛) | `Sirahoshi_skill_Mana` | 잃은체력 1 | H08U | 초월_유재헌_ADAP | 96659 |
| E 없음(로스터 유닛) | `Tichi_skill_3` | 현재체력 1 | H090 | 초월_임채민_AP | 97529 |
| E 없음(로스터 유닛) | `Uta_skill_1` | 최대체력 1 | h067 | 영원_김정래 | 112960 |
| E 없음(로스터 유닛) | `Uta_skill_1_double` | 잃은체력 1 | h067 | 영원_김정래 | 113021 |
| E 없음(로스터 유닛) | `Z_Skill_Mana` | 잃은체력 1 | h04G | 불멸_박은석 | 108558 |
| F 로스터 밖 유닛 | `Cavendish_skill_6` | 현재체력 1 | h06H |  | 111135 |
| F 로스터 밖 유닛 | `Dragon_Skill_2dummy` | 최대체력 2 | h08H |  | 108177, 108179 |
| F 로스터 밖 유닛 | `Hidden1` | 최대체력 1 | h03M |  | 88557 |
| F 로스터 밖 유닛 | `Hidden11` | 최대체력 1 | h03L |  | 88864 |
| F 로스터 밖 유닛 | `Hidden12` | 최대체력 2 | h03U |  | 88889, 88891 |
| F 로스터 밖 유닛 | `Hidden15` | 최대체력 1 | h03J |  | 89447 |
| F 로스터 밖 유닛 | `Hidden16` | 현재체력 1, 최대체력 1 | h045 |  | 89572, 89574 |
| F 로스터 밖 유닛 | `Hidden4` | 현재체력 1 | h047 |  | 88626 |
| F 로스터 밖 유닛 | `Hidden8` | 현재체력 1 | h044 |  | 88791 |
| F 로스터 밖 유닛 | `Jotaro_R` | 잃은체력 1 | h09R |  | 103432 |
| F 로스터 밖 유닛 | `Legend30mirror` | 최대체력 2 | h085 |  | 91214, 91216 |
| F 로스터 밖 유닛 | `SandiAttack` | 현재체력 1 | H09H |  | 95812 |
| F 로스터 밖 유닛 | `Sinobu_Skill_5_mirror` | 최대체력 2 | h086 |  | 94135, 94137 |
| F 로스터 밖 유닛 | `Usop_Skill_10Ton` | 최대체력 1 | H09C |  | 98678 |
| G 없음(유닛 미상) | `Absolb1` | 최대체력 1 |  |  | 82342 |
| G 없음(유닛 미상) | `EXPLOSION` | 최대체력 2 |  |  | 102372, 102378 |
| G 없음(유닛 미상) | `Legend10_shanks_pegi1` | 최대체력 1 |  |  | 90133 |
| G 없음(유닛 미상) | `Legend10_shanks_pegi2` | 최대체력 1 |  |  | 90151 |
| G 없음(유닛 미상) | `Legend10_shanks_pegi3` | 최대체력 1 |  |  | 90169 |
| G 없음(유닛 미상) | `Legend10_shanks_pegi4` | 최대체력 1 |  |  | 90187 |
| G 없음(유닛 미상) | `Legend14han_petrification1` | 최대체력 1 |  |  | 110747 |
| G 없음(유닛 미상) | `Legend14han_petrification2` | 최대체력 1 |  |  | 110775 |
| G 없음(유닛 미상) | `Legend14han_petrification3` | 최대체력 1 |  |  | 110803 |
| G 없음(유닛 미상) | `Legend14han_petrification4` | 최대체력 1 |  |  | 110831 |
| G 없음(유닛 미상) | `Legend16sc_Filterfunc001` | 현재체력 1 |  |  | 90504 |
| G 없음(유닛 미상) | `Legend16sc_Filterfunc002` | 현재체력 1 |  |  | 90497 |
| G 없음(유닛 미상) | `Legend_han_kiss_damagesc` | 최대체력 1 |  |  | 90457 |
| G 없음(유닛 미상) | `Pirates_docking_1` | 최대체력 1 |  |  | 87593 |
| G 없음(유닛 미상) | `Shanks_ET_pegi1` | 최대체력 4 |  |  | 95259, 95271, 95276, 95281 |
| G 없음(유닛 미상) | `Shanks_ET_pegi2` | 최대체력 4 |  |  | 95345, 95357, 95362, 95367 |
| G 없음(유닛 미상) | `Shanks_ET_pegi3` | 최대체력 4 |  |  | 95431, 95443, 95448, 95453 |
| G 없음(유닛 미상) | `Shanks_ET_pegi4` | 최대체력 4 |  |  | 95517, 95529, 95534, 95539 |
| G 없음(유닛 미상) | `vivi_Skill_5_buff` | 현재체력 2 |  |  | 111691, 111708 |
| G 없음(유닛 미상) | `vivi_skill_Double2` | 최대체력 1 |  |  | 111333 |
