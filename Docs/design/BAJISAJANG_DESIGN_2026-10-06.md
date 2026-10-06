# 초월 최상호 「바지사장」 설계표 (2026-10-06, 구현담당2)

사장님 원문 + PM 확정(10-06) 기준. **코드는 아직 안 씀 — PM 확인 뒤 구현.** 수치 근거 = `Docs/research/TRANSCEND_SKILL_REFERENCE_2026-10-06.md`(구현담당1).

## 1. 대조 — 에셋·조합식·칭호

| 항목 | 지금 | 목표(사장님) | 비고 |
|---|---|---|---|
| 로스터 | `초월_최상호_AP` (grade 7 초월, unitName 「바지사장」, 스킨 아이젠, damageType 1(마딜/AP)) | 그대로 재설계 | 새 로스터 안 만든다(PM). `영원_최상호_바지사장`은 영원 식(재료로 이 유닛 사용) — 안 건드림 |
| 재료 | 전설적인_노태현(사회복무요원) + **변화됨_최상호** + 희귀함_이은엽(드럼신동) + **희귀함_이용민** + 희귀함_박수찬(심각한로리콘) + 초월위습_박은석 | 노태현 사회복무요원 + 최상호 한남 + 이은엽 드럼신동 + 박수찬 심각한로리콘 + 배성령 해커 + 초월위습 | 이용민 → **특별함_배성령(unitName 해커)** 로 교체. 노태현·이은엽·박수찬·초월위습은 이미 맞음 |
| 「최상호 한남」 | 로스터에 unitName 「한남」인 유닛 **없음**(최상호 계열: 흔함_최상호·특별함_일진·희귀함_윤식파의두뇌·오타쿠의길·전설적인_상호파수장·변화됨_변화된윤식파의두뇌·영원_최상호) | ? | ❓질문1 — 기본안: 지금 쓰는 `변화됨_최상호` 그대로 |
| 칭호(unitName) | 바지사장 | 바지사장 | 유지 |
| chatPhrase(타이핑) | 「검은정의를좇는하얀새」(원작 임시), commandId `Hannam tr` | 「씹덕대마왕」 | `Tools/transcend_phrases.csv` 25행 값 변경 + 출처 「사장님」 → `TranscendPhraseImporter.Apply` (commandId는 그대로) |
| 스킬 | 도플라밍고 10개(A0AL 6변신·평타 범위 피해·각성창·새장·DP_Skill 등) | 사장님 4개 | 구 10개 전부 버린다(skills 리스트 교체, 구 SkillData 에셋은 놔두거나 지움 — 질문5) |
| 특성강화 | `Trait_초월_최상호_AP` 3pt·skillLevelUnlockIndex 1(도플 스킬승급 설명) | 3pt → 액티브 쿨 −50%(PM 확정) | **구조가 이미 있다**: UnitTraitData.costTraitPoints 3 + skillLevelUnlockIndex 1 → 액티브 SkillData.levels[1]에 쿨 절반. 설명문만 교체 |
| 이펙트 | — | 다른 초월과 같은 기본 규칙(Style은 스킬 이름의 일부) | |

## 2. 우리 코드에 있나

| 필요한 것 | 있나 | 위치 | 새로 만들 것 |
|---|---|---|---|
| 마나 게이지 N타째 발동 | ✅ | `SkillTriggerType.OnHitCount` + `SkillLevel.gaugeKind=Mana/hitCountThreshold/resetTo`, UnitAttacker.manaGaugeCounter(평타마다 +1) | 없음 |
| 체력 게이지 발동(두 번째 게이지) | ✅ | 같은 OnHitCount `gaugeKind=Life`, lifeGaugeCounter(평타 +1, `UnitData.lifeGauge*`로 시작·상한·증가율) — 키드·아카이누·강재규가 같은 구조 | 없음(UnitData.lifeGaugeMax만 채움) |
| 스턴 | ✅ | `SkillEffectKind.Stun`(duration·heroDuration) | 없음 |
| 깡딜 | ✅ | `SkillEffectKind.Damage`(multiplier/bonus·basis) | 없음 |
| 자신 공속 증가(시한) | ✅ | `SkillEffectKind.AttackSpeedBuffPercent` Self + duration(구주호 초록소·야마토 선례) | 없음 |
| **공속 비례** | ❌ | AttackSpeedMultiplier는 UnitAttacker 안 private. 효과에 공속을 곱하는 축 없음 | ➊ SkillEffect 끝에 필드 `attackSpeedScale`(0=끔)·`attackSpeedScaleCap`, UnitAttacker에 `public float CurrentAttackSpeedMultiplier`. Damage의 multiplier·bonus와 Stun의 duration에 `× (1 + scale×(AS−1))`(상한 cap) |
| **액티브(누르는) 스킬** | ❌ | 단추 구조 없음(영웅 단추는 능력치 증가용, 상점 칸은 상점 전용) | ➋ `SkillTriggerType.ActiveButton`(맨 뒤) · UnitAttacker `TryCastActive(skill)`+쿨 상태 · 명령 카드 칸(아래) · MP `NetUnitCommand.CastActive`(호스트에서 시전) · 쿨 덮개는 상점 칸의 쿨 덮개 코드 재사용(GameHud.UpdateShopCooldownOverlays) |
| 특성 포인트로 유닛 전용 강화 | ✅ | UnitTraitData(스킬승급) + UnitUpgrades.SkillLevelIndexFor | 없음(에셋만) |

⚠️ 구현담당1이 SkillData.cs 끝에 SummonUnit·GrantSkillToAllies·BossDamageMultiplier + SkillEffect 필드를 추가 중 — 내 ➊➋는 **그 뒤**에 이어 붙인다(커밋 알림 받은 뒤). UnitAttacker.cs도 그쪽 커밋 뒤.

## 3. 스킬 4개 설계 + 수치 근거

(마딜 = damageType AP. 공속 배율 AS = 현재 공속 배율의 곱(연구소·버프·오라 포함), 기본 1.0.)

| # | 사장님 이름 | 종류 | 발동 | 효과 | 우리 수치(제안) | 참고한 원작 유닛·능력·원작 수치 → 근거 |
|---|---|---|---|---|---|---|
| ① | 적응불가의덕력 | 스턴 | 평타 확률(OnHitChance) 15% | 대상 스턴 | 확률 15% · 스턴 3.0초(영웅 지속 1.5) · 공속 비례 없음 | 구주호 A0CS 15%·3.0초(ahdu 1.5), 박민수 A0PZ 25%·3.0초, 노태현 A04D 17.5%·1.75초 → 중앙값 15%·3.0초. 「공속 비례」는 마나·체력 스킬에만 쓰라는 원문대로 여기엔 안 건다 |
| ② | 절대공격 | 마나스킬(공속비례 깡딜) | OnHitCount 마나 **125** | 600 범위 피해 | 피해 2,000,000 × (1 + 1.0×(AS−1)), 상한 AS 4 → 최대 8,000,000 · 범위 600 · resetTo 0 | 게이지: 두유찬 125·구주호 115·노태현 115·강주혁 145·신문철 160·박민수 145 → 125. 피해: 구주호 초록소 2,000,000(×5)·노태현 브룩 3,000,000·강주혁 염왕 1,500,000+STR×15000 → 2,000,000. 범위 600(브룩·염왕·조성진 600). **「공속 비례」는 원작 근거 없음 — 제안값**(비례 계수 1.0·상한 4배) |
| ③ | 절대방어 | 체력스킬(공속비례 스턴) — 두 번째 게이지(체력바)가 차면 스턴(PM·사장님 확정) | OnHitCount 체력 **50** | 500 범위 스턴 | 스턴 2.0초 × (1 + 0.5×(AS−1)), 상한 4.0초(영웅 지속 ×0.5) · 범위 500 · resetTo 0 | 황준석 키드 체력 스킬 LIFE50: 500범위·스턴 2.0초, 김만경 아카이누 LIFE50: 스턴 3.0·1.595. → 50·500·2.0초. 공속 비례는 원작 근거 없음 — 제안값(계수 0.5·상한 2배) |
| ④ | 분노조절장애(Style : 최상호) | 액티브(버튼) — 자신 공속 증가 | 버튼 | 자기 공속 +100% 10초 | 공속 +100%(×2.0) · 지속 10초 · 쿨 40초 · 특성 3pt 강화 시 쿨 **20초(−50%, PM 확정)** | 원작 공속 선례: 구주호 야마토 +400%·1.25초(1/4 확률), 초록소 +5%·6.85초, 상초강 +50% 상시, 오라 +12~20%. 시전형 시한 +100%/10초/쿨 40초는 **원작 근거 없음 — 제안값**. 공속이 오르면 ②③이 같이 세진다(사장님 의도의 시너지) |

스킬 이름은 사장님 원문 그대로: `적응불가의덕력` · `절대공격` · `절대방어` · `분노조절장애(Style : 최상호)`(괄호 포함 한 이름).

## 4. 버튼 위치 제안

명령 카드(4×3) — 0~2 공격/정지/모으기, 3~11 조합 결과·상점 칸, 12 정렬. 초월은 조합 결과가 없어 3~11이 비므로 **3번(워크3 격자 키 R, 맨 위줄 오른쪽 끝)** 에 「분노조절장애(Style : 최상호)」를 둔다. 라벨 두 줄(이름 / 남은 쿨 초), 쿨 중엔 상점 칸과 같은 시계방향 덮개, 툴팁에 효과·쿨. 한 번에 한 유닛만 선택돼 있을 때만 표시(여럿 선택 시 숨김 — 상점 칸과 같은 규칙).

## 5. 질문(사장님께)

1. 「최상호 한남」은 어느 유닛? (기본안: 지금 재료인 `변화됨_최상호`(unitName 「변화된윤식파의두뇌」)를 그대로 둠) ← **필요**
2. 액티브 버튼 칸: 3번(R)·쿨 덮개 괜찮은가?
3. 수치 제안값(공속 비례 계수 1.0/0.5·상한, 액티브 +100%·10초·쿨 40초) 괜찮은가?
4. 구 도플라밍고 스킬 10개 에셋은 지울까 둘까? (기본안: 유닛에서만 떼고 에셋은 둔다 — 다른 유닛 참조 없음 확인 뒤 지우기 가능)
5. 액티브 사용 중 마나 소모는 없음(쿨만)으로 둠 — 맞나?

(체력스킬 해석·이름 순서·Style 뜻·쿨 감소 50%·초월위습 재료는 PM이 이미 사장님 확정으로 알려 줌 — 반영됨.)

## 6. 구현 순서(PM 확인 뒤)

1. 데이터: 재료 교체·chatPhrase·SkillData 4+1(액티브 레벨2) 에셋·UnitData skills/lifeGaugeMax·특성 설명 · 구현담당1 커밋 뒤 SkillData/UnitAttacker에 ➊➋.
2. 실측: gameshot 탐침으로 공속을 바꿔 ②피해·③스턴 시간이 비례하는지 숫자 확인(공속 1.0·2.0·4.0), ④쿨 40→20초(특성 3pt), ①확률 통계. 멀티는 CastActive 호스트 시전 확인.
