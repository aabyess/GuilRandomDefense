# `chance:1` 다중효과 62파일 감사 — 배타 vs 별개 기계판정

조사: 리서치담당 / 2026-09-06
요청: PM — 「게이트별 배정 파일 118개 중 62개가 효과 2개+를 전부 chance:1로 갖는다.
`Nami_Skill_2`(강주혁·김민준 AP, 이미 확정 버그)처럼 배타인지, `Tasigi_03`형 진짜
별개인지 84개 그룹에 쓴 방법 그대로 기계추적하라」

## 결론 — 70개 트리거 중 25개 배타, 45개 별개

62개 파일이 참조하는 원작 트리거 **70개**를 전부 `Docs/reference/RRD_SEQUENCE_DUPLICATE_AUDIT.md`와
같은 방법(if/elseif/else/endif 중첩 추적, ForGroupBJ 콜백까지 포함)으로 기계판정했다.

- **배타(잠재 버그) — 25개**: 지금 자산이 "항상 다 발동"으로 만들어져 있다면 원작보다
  세게 때리는 중이다. 구현담당2가 즉시 손볼 수 있게 트리거별 원인·조건을 아래에 남긴다.
- **별개(문제 없음) — 45개**: `chance:1` 여러 개가 실제로 전부 맞다. 손댈 필요 없다.

## ⚠️ 방법 보정 — "Stage 분기"는 배타가 아니다

처음 자동판정에서 `Legend19_0`·`Sandi_skill_1`이 배타로 잡혔는데, 원인이
`s__TrigVariables_Stage[GlobalTV]==0/1/2` 같은 **단계(Stage) 분기**였다. 이건 **같은
캐스트 안에서 시간차를 두고 순서대로 전부 실행되는 구조**(스킬 1단계→2단계→3단계)라
"둘 중 하나만 실행"이 아니다 — **다단히트를 표현하는 정상적인 방식**이다. 이 둘은
**배타 후보에서 제외**하고 별개(45개) 쪽으로 옮겼다. **이 함정 자체가 하나의 발견이다**
— 앞으로 같은 방식(Stage 비교)을 배타로 오판하지 않도록 `RRD_SEQUENCE_DUPLICATE_AUDIT.md`에도
남겨둔다.

## 배타 25건 — 분기 성격별 분류

### A. 대상 강함(`GetUnitPointValue`) 기준 — **확률 아니고 결정론적 조건부** (19건)

원작이 "대상이 약한 몹(포인트값<200~300)이면 A식, 강한 몹/보스면 B식"으로 **항상
결정적으로 갈라 쓰는** 패턴이다. 무작위가 아니라 **어떤 대상을 치느냐로 매번 똑같이
결정**된다 — 즉 "50%씩 섞여 나간다"가 아니라 "약한 몹껜 A만, 센 몹껜 B만" 나간다.

| 트리거 | 조건 | 파일(대표 1개) |
|---|---|---|
| `Garp_AttackDamage` | `<200` | `SkillData_회수_불멸_이승우_355f5d39.asset` |
| `Minato_E` | `<200` | `SkillData_게이트_랜덤_이타도리_유지_84bf5e76.asset` |
| `Marco_S1` | `<200` | `SkillData_게이트_제한_박성호_874ea782.asset` |
| `Marco_S2` | `<200` | `SkillData_게이트_제한_박성호_3a13fd3d.asset` |
| `Usop_Skill_2` | `<300` | `SkillData_게이트_초월_조성진_AD_33bd8aa4.asset` |
| `kikoyou_skill_1`(2차 분기만) | `<200` | `SkillData_게이트_랜덤_이즈미_신이치_41677101.asset` |
| `kikoyou_hp` | `<200` | `SkillData_게이트_랜덤_이즈미_신이치_351a0f00.asset` |
| `Ryougi_Shiki_R2` | `<200` | `SkillData_게이트_랜덤_카마도_탄지로_3c5f8bd9.asset` |
| `Ryougi_Skill_3` / `_double` / `_triple` | `<200`(3개 파일 동일 패턴) | `SkillData_게이트_랜덤_카마도_탄지로_ff8bed43.asset` |
| `Ryougi_Shiki_2` | `<200` | `SkillData_게이트_랜덤_카마도_탄지로_d370bb23.asset` |
| `Tichi_skill_1` | `<300` | `SkillData_게이트_초월_임채민_AP_da9fb163.asset` |
| `Legend_han_1` | `<200` | `SkillData_게이트_전설적인_이현주_0e5cf375.asset` |
| `Legend26_tichi_crows` | `<200` | `SkillData_게이트_전설적인_홍인창_fbb3c4ff.asset` |
| `King_skill_2_tr` | `<200`(중첩함수로 확인) | `SkillData_게이트_제한_임준성_355f5d39.asset` |
| `byakuya_Chan` | `<200`(중첩함수로 확인) | `SkillData_게이트_랜덤_한마_바키_355f5d39.asset` |
| `vivi_Skill_4_Mana` | `<200`(배타 2쌍, 대상 다름) | `SkillData_회수_영원_문필환_55b5cff0.asset` |
| `Legend13_2` | `<200`/`==200`(3단 분기 중 최소 2개 배타) | `SkillData_게이트_전설적인_정준영_3a13fd3d.asset` |
| `Gaban_Skill_mana` | `<200`/`==200`/`>=300`(3단 분기) | `SkillData_게이트_불멸_신지우_d33a6a78.asset` |

### B. 진짜 확률(RNG) — **1건뿐**

| 트리거 | 조건 | 확률 | 파일 |
|---|---|---|---|
| `Nami_Skill_2` | `GetRandomInt(1,100)<33` | **33% / 67%** | `SkillData_게이트_초월_강주혁_AP_5c20b80f.asset` — **이미 확정된 버그, PM 보고대로** |

### C. 대상 버프 보유 여부 — **결정론적 조건부** (2건)

| 트리거 | 조건 | 파일 |
|---|---|---|
| `Ain_Skill_1` | `UnitHasBuffBJ(대상,'B06B')` | `SkillData_게이트_제한_이충민_18aa2343.asset` |
| `Ain_Skill_2` | `UnitHasBuffBJ(대상,'B06B')` | `SkillData_게이트_제한_이충민_8f2b5872.asset` |

### D. 대상 생존·캐스터 상태(`Avul` 능력 레벨) — **결정론적 조건부** (1건)

| 트리거 | 조건 | 파일 |
|---|---|---|
| `Tasigi_03` | `IsUnitAliveBJ(대상) AND GetUnitAbilityLevelSwapped('Avul',캐스터)==0`(타겟팅 방식 분기) | `SkillData_게이트_초월_김민준_AP_2a646778.asset` — 이미 PM께 보고한 그 파일. 앞서 "타겟팅 방식만 다른 if/else"라고 한 것과 일치, 조건 원문이 이거였다 |

**요약: 25건 중 24건이 "확률 없는 조건부"(대상 강함/버프/상태 기준으로 항상 결정),
`Nami_Skill_2` 1건만 진짜 RNG(33%/67%)다.** 즉 대부분은 "가끔 둘 다 나간다"가 아니라
"어느 대상이냐에 따라 매번 정해진 하나만 나가야 하는데 우리 자산엔 둘 다 chance:1로
박혀서 항상 같이 나가는" 형태 — **원작에선 절대 동시발동할 수 없는 조합이 우리
자산에선 매번 동시발동 중**이라는 뜻이다.

## 별개(문제 없음) 45건

`Legend34Moria_Skill`·`Huji01`·`Huji_03`·`Sinobu_Skill_hp2`·`Ed_Skill_1`·
`Ed_Skill_1_Item`·`BronyaMotar_E`·`BronyaMotar_laser`·`kikoyou_mana`·
`Snake_3_Kingkobra`·`Gaban_Skill_1`·`DP_Skill_2`·`Zoro_raven`·`Cavendish_skill_Mana`·
`Ruffy_Mana`·`Luchi_Skill_3_6kinggun`·`Rebeca_Skill_1`·`Akainu_02_hidden`·
`Legend32_neko1`·`koalla_skill_Mana`·`Yamato_Attack_Damage2`·`Zoro_Samchun2`·
`Zoro_samchun1`·`King_skill_2`·`Brook_Skill_1`·`Red_skill_1`·`koalla_skill_1`·
`Jimbe`·`Tasigi_02`·`tahsigi`·`Tich1_danil`·`Tichi_skill_2_tr`·`Legend31_toki`·
`Byakuya_R`·`Bronya_Skill_1`·`Snake_2`·`Legend19_1`·`Legend51`·`Transpom_dp`·
`Sandi_skill_Mana2`·`BronyaMotar_R`·`Sabo_Mana`·`Higma_Q`·**`Legend19_0`**·
**`Sandi_skill_1`**(뒤 둘은 Stage 오판 정정으로 여기 합류).

## 남은 것 / `[미확인]`

- **62개 파일 중 아직 파일↔트리거 1:1 대조를 안 끝냈다** — 위는 트리거 판정표다.
  판정을 실제 62개 각 `.asset` 파일의 어느 effect 인덱스에 매칭할지는 트리거명이
  파일 설명문에 `#1`/`#2`로 이미 박혀 있어 기계적으로 가능하지만, 이번엔 트리거
  판정까지만 하고 파일별 수정 지시서는 안 만들었다 — 필요하면 이어서 만들겠다.
- **118개 중 62개 이외의 나머지(전부 chance:1은 아니지만 효과 2개 이상인 파일들)는
  이번 조사 대상이 아니었다** — PM이 "chance:1 전부"인 것만 지목했다.
- 위 A(19건) 중 일부(`kikoyou_skill_1`)는 Stage 분기와 진짜 배타가 섞여 있어
  대상포인트값 조건만 배타로 인정했다 — 상세 로그 필요하면 재현 가능.
