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
