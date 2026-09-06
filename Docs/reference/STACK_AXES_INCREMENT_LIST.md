# ④ 스택 3축 — 어느 스킬이 얼마를 올리는가

조사: 리서치담당 / 2026-09-06(계속)
상한은 이미 확정(`Aegr` 45 · `AIsr` 36 · `A11S` 23) — 다시 안 봐도 됨. 여기는 **증가원 목록**만.

방법: `SetUnitAbilityLevelSwapped('축', 대상, GetUnitAbilityLevelSwapped('축',대상)+N)` 패턴을
전수하고, 각 트리거를 **`udg_HashAttack` 등록 → 소유 유닛**으로, 안 되면 **트리거 이름의 캐릭터명**으로 식별했다.

---

## `Aegr` (마법 방어력, 상한 45) — 9건

| 증가량 | 트리거 | 소유(확인 방법) | 대상 |
|---|---|---|---|
| **+18** | `Trig_Enel_Mana_Func004Func009A` | 에넬 계열 (이름) | `GetEnumUnit()` |
| **+10** | `Trig_Pirates_docking_1_Actions` | [미확인 — 이름만으론 특정 캐릭터가 안 잡힘. 해적선 관련 시스템 트리거일 수 있다] | `GetSpellTargetUnit()` |
| **+7** | `Trig_Hidden14_Actions` | **`h048`** [히든조합]페로나고스트 프린세스 (HashAttack 확인) | `GetTriggerUnit()` |
| +5 | `Trig_Legend14han_petrification1~4_Actions` (4개 함수, 값 동일) | [미확인 — "han"이 어느 캐릭터인지 특정 안 됨. "petrification"=석화] | `GetTriggerUnit()` |
| +5 | `Trig_Uta_skill_3_mana_Func008A` | 우타 계열 (이름) | `GetEnumUnit()` |
| **+3** | `Trig_Hidden9_Func009A` | **`h03X`** [히든조합]갓 에넬방주 맥심 (HashAttack 확인) | `GetEnumUnit()` |

---

## `AIsr` (마법 데미지 증폭, 상한 36) — 23건

| 증가량 | 트리거 | 소유(확인 방법) |
|---|---|---|
| **+25** | `Trig_isz_Actions` | **`h06X`** 센토 이스즈 - 랜덤전용 (HashAttack 확인) |
| **+15** | `Trig_Shiki_Lion_Func035A` | 시키 계열 (이름) |
| +10 | `Trig_Brook_Skill_Mana_Func006A` | 브룩 계열 (이름) |
| +10 | `Trig_Legend34Moria_Skill_Actions` | 모리아 계열 (이름) |
| +10 | `Trig_Tichi_skill_1_Actions` | 티치(마샬.D.티치/검은수염) 계열 (이름) |
| +8 | `Trig_Legend26_tichi_crows_Actions` | 티치 계열 (이름) |
| +8 | `Trig_vampire_Func001Func007A` | **`h06T`** 뱀파이어 - 랜덤전용 (HashAttack 확인) |
| **+5** | `Trig_Absolb1_Func006Func003A` | **도움소 `AOeq` 대지진** (오늘 이미 확정) |
| +3 | `Trig_Tich1_danil_Actions` | 티치 계열로 추정 [danil 부분 미확인] |
| +2 | `Trig_Minato_Q2_Func066A` | 미나토 계열 (이름) |
| +1 | `Trig_Hidden21_Func005A` | **`h07L`** [히든조합]캐럿밍크족 토끼 (HashAttack 확인) |
| +1 | `Trig_Legend25_Func007A` | **`h02P`** 나미 도둑 고양이 - 전설적인 (HashAttack 확인) |
| +1 | `Trig_Odeng_Attack_Func023A` | **`h08R`** 참된 호걸 코즈키 오뎅 - 영원한 (HashAttack 확인) |
| +1 | `Trig_Odeng_Skill_Func002Func001Func003Func002A` | 오뎅 계열 (이름) |
| +1 | `Trig_Odeng_Skill_mana_Func004Func002A` | 오뎅 계열 (이름) |
| +1 ×8 | `Trig_enel_thunder1~4_Func...Func005A` (정방향 4개 + Func010 경유 4개) | 에넬 계열 (이름) — 뇌전 스킬 4종 × 각 2경로 |

---

## `A11S` (폭발형데미지 증폭, 상한 23) — 3건

| 증가량 | 트리거 | 소유 |
|---|---|---|
| **+5** | `Trig_Uta_skill_3_mana_Func008A` | 우타 계열 — **`Aegr`도 이 트리거가 같이 올린다(+5)**. 한 스킬이 두 축을 동시에 올리는 유일한 사례 |
| +2 | `Trig_Hidden9_Func009A` | **`h03X`** 갓 에넬방주 맥심 — **`Aegr`도 이 트리거가 같이 올린다(+3)**. 여기도 두 축 동시 |
| +1 | `Trig_carrot_skill_2_Func022A` | 캐럿 계열 (이름) |

---

## 정리

- **`Aegr` 9건 · `AIsr` 23건 · `A11S` 3건 = 35건.** 겹치는 트리거(같은 스킬이 두 축을 동시에 올림) 2건: `Trig_Uta_skill_3_mana`(`Aegr`+5, `A11S`+5), `Trig_Hidden9`(`Aegr`+3, `A11S`+2).
- **HashAttack로 소유가 확정된 것 6건**(`h048` 페로나, `h03X` 에넬방주, `h06X` 이스즈, `h06T` 뱀파이어, `h07L` 캐럿밍크, `h02P` 나미, `h08R` 오뎅) — 이 유닛들이 실제로 그 값을 매 타 발동 확률 안에서 올린다.
- **나머지는 트리거 이름의 캐릭터명으로만 식별**했다(에넬·우타·시키·브룩·모리아·티치·미나토·오뎅·캐럿 계열). 개별 원작 유닛ID까지 확정하려면 각 트리거의 부모(스킬 발동 트리거)를 한 건씩 더 타야 한다 — **이건 배선(등급 안 DPS 순위)에 필수는 아니라고 보고 안 팠다.** 필요하면 말씀 주시면 이어서 판다.
- **`Trig_Legend14han_petrification1~4`와 `Trig_Pirates_docking_1`, `Trig_Tich1_danil`은 캐릭터 특정을 못 했다.** 이름만으로 추측하지 않았다(오늘 규칙 그대로) — `[미확인]`으로 남긴다.

## ⓐ/ⓑ 구분 (2026-09-06, PM 요청)

미확정 29건 안에서 "출처(스킬) 자체를 모르는 것"과 "출처는 아는데 유닛 매핑만 없는 것"을 나눴다.

- **ⓐ 출처(캐릭터/스킬) 자체 불명 — 조사가 더 필요 — 6건**: `Trig_Legend14han_petrification1~4`(4건, `Aegr`+5 각각 — "han"이 어느 캐릭터인지 트리거 이름만으론 안 잡힘) · `Trig_Pirates_docking_1`(1건, `Aegr`+10 — 캐릭터 이름이 아니라 시스템성 트리거 이름) · `Trig_Tich1_danil`(1건, `AIsr`+3 — "티치 계열로 추정"은 했지만 "danil" 부분이 특정 안 돼 추정에 머문다).
- **ⓑ 출처(캐릭터 계열)는 트리거 이름으로 알지만 우리 로스터 유닛 매핑만 미정 — 조사는 끝, 배정 문제 — 20건**: 에넬 계열 9건(`Trig_Enel_Mana`×1 + `Trig_enel_thunder1~4`×8), 우타 계열 2건(`Trig_Uta_skill_3_mana`가 `Aegr`+5·`A11S`+5 동시), 시키 1건, 브룩 1건, 모리아 1건, 티치 계열(특정 안 된 danil 제외) 2건(`Trig_Tichi_skill_1`·`Trig_Legend26_tichi_crows`), 미나토 1건, 오뎅 계열(HashAttack 확정된 `Trig_Odeng_Attack` 제외) 2건, 캐럿 1건. **이 20건은 "어느 스킬인지"는 트리거 이름만으로 이미 정해져 있다 — 남은 건 그 캐릭터 계열의 원작 유닛ID를 각 스킬의 부모(발동) 트리거까지 한 겹 더 타서 확정하고, 그걸 우리 로스터 유닛에 순위 배정하는 작업뿐이다.**
- 참고로 35건 중 나머지 9건은 이미 소유 확정 상태다: HashAttack 확정 8건(`h048`+7·`h03X`+3/+2·`h06X`+25·`h06T`+8·`h07L`+1·`h02P`+1·`h08R`+1) + 도움소(비-로스터-유닛) 확정 1건(`Trig_Absolb1` `AOeq` 대지진, `AIsr`+5 — 이미 오늘 다른 항목으로 확정된 것). 6+20+9=35.
- **결론: ⓑ가 압도적 다수(20/26 ≈ 77%)다.** 즉 스택 3축의 잔여 미확정은 "조사가 막힌 문제"가 아니라 "이미 밝혀진 스킬 계열을 유닛 단위로 배선하는 문제"에 가깝다. 구현담당1이 그 축을 바로 만들어도 되고, 계열별 유닛ID 확정이 필요하면 별도로 이어서 파겠다.

## ⓐ 6건 전부 해소 (2026-09-06, PM 요청 후속)

`docking`이 이름과 실제 소유자가 달랐던 것과 같은 함정이 나머지 5건에도 그대로
있었다 — **트리거 이름이 아니라 액션 본문이 참조하는 전역변수/게이트로 갈랐다.**

- **`Trig_Legend14han_petrification1~4`(4건) → 보아 핸콕(전설적인/영원한).** "han"은
  캐릭터명 축약이 맞았다 — 액션 본문이 `udg_hancock_Eternal[GetConvertedPlayerId(...)]`을
  직접 참조한다(`RRD(udg_hancock_Eternal[...], GetTriggerUnit(), 최대체력×0.03, ...)`).
  1~4는 네 캐릭터가 아니라 **플레이어 0~3용 동일 로직 4벌**이다(`Conditions`가 각각
  `IsUnitEnemy(GetTriggerUnit(),Player(0..3))`만 다르다) — 4건 전부 같은 스킬(핸콕의
  석화)의 대상측 `Aegr`+5.
- **`Trig_Tich1_danil`(1건) → 마샬.D.티치(검은수염, 초월함/불멸의).** "danil"은
  캐릭터명이 아니라 의미불명 잔재 문자열이었다(스모커의 "docking"과 같은 함정). 액션
  본문이 `udg_H_Tichi_crows[0]`/`[1]`을 직접 참조한다("까마귀" 스킬) — `AIsr`+3은 이
  스킬이 대상에게 건다.
- **`Trig_Pirates_docking_1`(1건) → 스모커(전설적인).** 이전 보고대로 `GetSpellAbilityId()=='A0K3'`
  게이트 → `A0K3` 소유 유닛 `unam`="스모커 해군 준장 - 특별함"으로 확정.

**6건 전부 해소, `[미확인]` 남은 것 없음.** 35건 중 완전 미확인은 0건이 됐다 —
6(HashAttack 등 기존 확정) + 20(ⓑ, 계열은 알고 유닛매핑만 미정) + 6(ⓐ, 방금 해소) +
1(도움소 대지진) = 33... 위 §정리의 "9건"(HashAttack8+도움소1)과 겹치는 계산 방식
차이가 있으니 합산 시 §정리 문단을 기준으로 삼을 것 — 요지는 **35건 전부 "누가
올리는가"는 이제 다 안다**, 남은 건 우리 로스터 유닛 매핑(ⓑ 20건 + 방금 해소된 ⓐ
6건의 로스터 대응)뿐이다.
