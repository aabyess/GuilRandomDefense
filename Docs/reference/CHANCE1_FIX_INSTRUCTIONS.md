# `chance:1` 배타 24건 — 파일별 수정지시서 (지우지 않고 조건을 붙인다)

조사: 리서치담당 / 2026-09-06
전제: `CHANCE1_DUPLICATE_EFFECT_AUDIT.md`의 배타 25건 중 `Nami_Skill_2`(RNG, 이미
확정)를 뺀 **24건**. PM 지시대로 **삭제가 아니라 조건 배정**이다 — 두 효과 다
자산에 남기고, 어느 효과가 어느 대상 조건에서 나가야 하는지만 표시한다.

## 읽는 법

`effect_index`는 그 `.asset` 파일의 `levels[0].effects` 배열 순서(0부터)다.
`대상포인트값`은 JASS `GetUnitPointValue(대상)` — 원작 필드값이며, 우리에 대응
축이 아직 없다(이어서 조사할 항목, 아래 "다음 단계" 참고). **지금은 매핑만
확정하고, 실제 게이트 구현은 포인트값 실측 뒤로 미룬다** — PM 지시대로 순서는
지시서 먼저, 포인트값 실측은 다음 메시지에서 잇는다.

## 파일별 매핑 (21개 파일 확정)

| 파일 | effect_index | 원본(트리거#N) | 조건 |
|---|---:|---|---|
| `SkillData_회수_불멸_이승우_355f5d39.asset` | 0 | `Garp_AttackDamage#1` | 대상포인트값 `<200` |
| | 1 | `Garp_AttackDamage#3` | ELSE(`>=200`) |
| `SkillData_게이트_랜덤_이타도리_유지_84bf5e76.asset` | 0 | `Minato_E#1` | 대상포인트값 `<200` |
| | 1 | `Minato_E#2` | ELSE(`>=200`) |
| `SkillData_게이트_초월_김민준_AP_2a646778.asset` | 0 | `Tasigi_03#1` | 타겟팅분기A(`Avul`능력==0) |
| | 1 | `Tasigi_03#2` | 타겟팅분기A **AND** 대상포인트값`<200` |
| | 2 | `Tasigi_03#3` | 타겟팅분기B(ELSE) |
| | 3 | `Tasigi_03#4` | 타겟팅분기B **AND** 대상포인트값`<200` |
| `SkillData_게이트_제한_박성호_874ea782.asset` | 0 | `Marco_S1#1` | 대상포인트값 `<200` |
| | 1 | `Marco_S1#2` | ELSE(`>=200`) |
| `SkillData_게이트_불멸_신지우_d33a6a78.asset` | 0 | `Gaban_Skill_mana#1` | 대상포인트값 `<200` |
| | 1 | `Gaban_Skill_mana#2` | 대상포인트값 `==200`(정확히 200) |
| | 2 | `Gaban_Skill_mana#3` | 대상포인트값 `>=300` |
| `SkillData_게이트_초월_조성진_AD_33bd8aa4.asset` | 0 | `Usop_Skill_2#1` | 대상포인트값 `<300` |
| | 1 | `Usop_Skill_2#2` | ELSE(`>=300`) |
| | 2 | `Usop_Skill_2#5` | 무조건(별개, 조건 없음 — 그대로 둔다) |
| `SkillData_게이트_랜덤_이즈미_신이치_41677101.asset` | 0 | `kikoyou_skill_1#1` | 대상포인트값 `<200` |
| | 1 | `kikoyou_skill_1#2` | ELSE(`>=200`) |
| | 2 | `kikoyou_skill_1#3` | 무조건(별개 — Stage1, 시간차 다단히트라 조건 없음) |
| | 3 | `kikoyou_skill_1#4` | 무조건(별개) |
| `SkillData_게이트_제한_이충민_18aa2343.asset` | 0 | `Ain_Skill_1#1` | 대상 버프 `B06B` 보유 |
| | 1 | `Ain_Skill_1#2` | ELSE(버프 없음) |
| | 2~4 | `Ain_Skill_1#3~5` | 무조건(별개) |
| `SkillData_게이트_전설적인_정준영_3a13fd3d.asset` | 0 | `Legend13_2#1` | 무조건(별개) |
| | 1 | `Legend13_2#2` | 대상포인트값 `<200` |
| | 2 | `Legend13_2#3` | 대상포인트값 `==200`(정확히 200) |
| | 3 | `Legend13_2#4` | 무조건(별개) |
| `SkillData_게이트_랜덤_카마도_탄지로_3c5f8bd9.asset` | 0 | `Ryougi_Shiki_R2#2` | ELSE(`>=200`) — ⚠️ **짝인 `#1`(`<200`)이 어느 파일에도 없다, 아래 "빠진 반쪽" 참고** |
| | 1~2 | `Ryougi_Shiki_R2#3~4` | 무조건(별개) |
| `SkillData_게이트_랜덤_이즈미_신이치_351a0f00.asset` | 0 | `kikoyou_hp#3` | 무조건(별개) |
| | 1 | `kikoyou_hp#4` | (그 외 트리거, 별개 확정) — ⚠️ **`#1`/`#2`(포인트값 배타 쌍)가 이 파일엔 없다** |
| `SkillData_게이트_초월_임채민_AP_da9fb163.asset` | 0 | `Tichi_skill_1#1` | 대상포인트값 `<300` **AND** 버프 `B06B` 보유 |
| | 1 | `Tichi_skill_1#2` | ELSE |
| | 2 | `Tichi_skill_1#3` | 무조건(별개) |
| `SkillData_게이트_랜덤_카마도_탄지로_ff8bed43.asset` | 0 | `Ryougi_Skill_3#1` | 무조건(별개) |
| | 1 | `Ryougi_Skill_3#2` | 대상포인트값 `<200` |
| | 2 | `Ryougi_Skill_3#3` | ELSE(`>=200`) |
| | 3 | `Ryougi_Skill_3#4` | (그 외, 별개 확정) |
| | 4 | `Ryougi_Skill_3_double#1` | 무조건(별개) |
| | 5 | `Ryougi_Skill_3_double#2` | 대상포인트값 `<200` |
| | 6 | `Ryougi_Skill_3_double#3` | ELSE(`>=200`) |
| | 7 | `Ryougi_Skill_3_double#4` | (그 외, 별개 확정) |
| | 8 | `Ryougi_Skill_3_triple#1` | 무조건(별개) |
| | 9 | `Ryougi_Skill_3_triple#2` | 대상포인트값 `<200` |
| | 10 | `Ryougi_Skill_3_triple#3` | ELSE(`>=200`) |
| | 11 | `Ryougi_Skill_3_triple#4` | (그 외, 별개 확정) |
| `SkillData_게이트_전설적인_이현주_0e5cf375.asset` | 0 | `Legend_han_1#1` | 대상포인트값 `<200` |
| | 1 | `Legend_han_1#2` | ELSE(`>=200`) |
| | 2 | `Legend_han_1#3` | 무조건(별개) |
| `SkillData_게이트_전설적인_홍인창_fbb3c4ff.asset` | 0 | `Legend26_tichi_crows#1` | 대상포인트값 `<200` **AND** 버프 `B06B` 보유 |
| | 1 | `Legend26_tichi_crows#2` | ELSE |
| | 2 | `Legend26_tichi_crows#3` | 무조건(별개) |
| `SkillData_회수_영원_문필환_55b5cff0.asset` | 0 | `vivi_Skill_4_Mana#2` | ELSE(대상B, `>=200`) — ⚠️ **짝 `#1`이 이 파일엔 없다** |
| | 1 | `vivi_Skill_4_Mana#4` | ELSE(대상D, `>=200`) — ⚠️ **짝 `#3`도 없다** |
| `SkillData_게이트_제한_이충민_8f2b5872.asset` | 0 | `Ain_Skill_2#1` | 대상 버프 `B06B` 보유 |
| | 1 | `Ain_Skill_2#2` | ELSE(버프 없음) |
| | 2~6 | `Ain_Skill_2#3~7` | 무조건(별개, 서로 다른 독립 조건) |
| `SkillData_게이트_제한_임준성_355f5d39.asset` | 0 | `King_skill_2#1` | (다른 트리거, 별개 확정) |
| | 1 | `King_skill_2_tr#1` | 대상포인트값 `<200` |
| | 2 | `King_skill_2_tr#2` | ELSE(`>=200`) |
| | 3 | `King_skill_2_tr#3` | 무조건(별개) |
| `SkillData_게이트_제한_박성호_3a13fd3d.asset` | 0 | `Marco_S2#1` | 대상포인트값 `<200` |
| | 1 | `Marco_S2#2` | ELSE(`>=200`) |
| | 2 | `Marco_S2#3` | 무조건(별개) |
| `SkillData_게이트_랜덤_한마_바키_355f5d39.asset` | 0 | `Byakuya_R#1` | (다른 트리거, 별개 확정) |
| | 1 | `byakuya_Chan#1` | 대상포인트값 `<200` |
| | 2 | `byakuya_Chan#2` | ELSE(`>=200`) |
| `SkillData_게이트_랜덤_카마도_탄지로_d370bb23.asset` | 0 | `Ryougi_Shiki_2#1` | 대상포인트값 `<200` |
| | 1 | `Ryougi_Shiki_2#2` | ELSE(`>=200`) |
| | 2~3 | `Ryougi_Shiki_2#3~4` | 무조건(별개) |
| | 4 | `Ryougi_Shiki_2#5` | (다른 트리거, 별개 확정) |

## ⚠️ 빠진 반쪽 — 정정(2026-09-06), 원문 CSV 행 재대조 완료

이 절의 최초 판정(아래 3건 전부 "배타 쌍의 절반 결측")은 **틀렸다.** 원문을 다시
안 열고 CSV 행 순서로 추정한 게 원인이다 — 상세 근거·정정 경위는
`APPROXIMATION_LEDGER.md` §11에 있다. **여기서는 결론만 갱신한다:**

- **`Ryougi_Shiki_R2#1`(0.45,`<200`)/`#2`(0.03,ELSE)는 둘 다 원래 이 배타 쌍이
  맞고, 이미 파일에 배선돼 있다**(2026-09-06, 구현담당2). `#1`이 결측이라던 게
  틀렸다. **진짜 결측은 이 배타 쌍과 무관한 원문 CSV 124행**(`5,000,000` Flat,
  `Enemies`그룹 AoE, 무조건) — 이 값은 여전히 어느 파일에도 없다.
- **`kikoyou_hp#3`(현재체력×0.40,`<200`)/`#4`(1,000,000,ELSE)도 둘 다 이미
  파일에 있다.** `#1`/`#2`가 결측이라던 것도 틀렸다 — 애초에 `#1`/`#2`는
  포인트값 배타 쌍이 아니라 **`Trig_kikoyou_hp_Func034A`(`ForGroupBJ`로
  `Enemies`그룹 순회) 안의 무조건 RRD 2개**(각 1,500,000)였다. 이 둘은
  포인트값과 무관하게 여전히 결측이다.
- **`vivi_Skill_4_Mana#1`/`#3`은 재확인 결과 원래 판정이 맞다** — 포인트값
  문제가 아니라 캐스터(비비) 강화스택(`A0LZ`) 결측이며, 이 트리거엔
  `Enemies`그룹 AoE 성분 자체가 없다(원문 재확인 완료, `GroupPickRandomUnit`으로
  단일 대상만 뽑는 구조).

**교훈**: "배타 쌍" 틀이 처음 2건에 잘 맞았다고 나머지도 그 틀로 읽으면 안
된다 — 결측 판정마다 **원문에 `ForGroupBJ`/`Enemies` 그룹 순회가 있는지**,
**그 값이 정말 지금 파일에 없는지**를 개별로 다시 확인해야 한다.

## 조건 배정 방법 (구현 쪽 참고)

`<200`/`ELSE`/`==200`류는 **대상(피격 유닛)의 속성**으로 갈리므로, `SkillEffect`에
"이 효과는 대상 포인트값이 이 구간일 때만 발동"이라는 필드가 필요하다(지금 없음,
`gate`류처럼 캐스터 게이지가 아니라 **대상 쪽 속성**이라는 점이 다르다). 버프
`B06B` 보유 조건은 이미 있는 `requiredTargetBuffId`류 필드로 바로 표현 가능해
보인다(06번 버프게이트 배선과 같은 방식) — **포인트값 축만 새로 필요**하다.

## 다음 단계

이어서 `GetUnitPointValue` 실측(원작 적 전수, 경계 200/300이 뭘 가르는지, 우리
`EnemyData` 대응 여부)을 보고하겠다.
