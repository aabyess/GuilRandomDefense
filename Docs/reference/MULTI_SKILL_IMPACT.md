# `UnitData.skill`(단일) → 다중 스킬 — 영향 범위 견적 (2026-09-05, 구현담당3)

> 코드는 안 건드렸다. 사장님 결정용 견적 문서다.
>
> **v2 갱신(같은 날)**: 처음엔 「절대쿨 게이트 21건」(`GATED_SKILLS_14.md`)만 보고 견적했는데,
> 리서치담당이 **평타 트리거에서 실행되는 스킬 전체**를 전수했더니 규모가 한 자릿수 커졌다
> (`UNLISTED_SKILLS.md`, `d333342`) — **미수록 200건 / 유닛 96종 / 고유 스킬 172개**(1·2채널과
> 안 겹침), **유닛당 1개 32종·2개 37·3개 18·4개 5·5개 4**. 게이트는 **게이지 86건·확률 86건·
> 게이트 없음 27건**(절대쿨은 `GATED_SKILLS_14.md` 쪽이 정확 — 이 표에선 트리거 전체조건에
> 숨어 있어 대부분 안 잡혔다). **96종은 이미 1·2채널 스킬을 갖고 있어서**, 여기에 최대 5개가
> 더 필요하면 **유닛 한 종이 최대 6개**의 스킬을 가져야 한다. 이 규모에선 `secondarySkill`
> 필드 하나로는 안 되고 **`List<SkillData> skills` 전제로 견적을 다시 잡는다**(아래 ④⑤).

## ① `UnitData.skill`을 읽는 곳 전부

**코드(C#)**
| 파일:줄 | 하는 일 |
|---|---|
| `Assets/Scripts/Data/UnitData.cs:243` | 필드 선언(`public SkillData skill;`) — 유일한 원본 |
| `Assets/Scripts/Units/UnitAttacker.cs:189-200`(`Skill` 프로퍼티, 특히 198줄) | `UnitUpgrades.ReplacementSkillFor`가 없으면 `unitData.skill`을 그대로 반환 — **이 프로퍼티가 "유닛당 스킬은 하나"라는 계약을 코드 전체에 강제하는 지점** |
| `Assets/Scripts/Units/UnitAttacker.cs:206-235`(`CurrentSkillLevel`·`UpdateSkillCooldown`) | `Skill` 프로퍼티가 준 `SkillData` 하나를 그대로 받아 레벨·쿨다운을 계산(간접 의존) |
| `Assets/Scripts/Units/UnitAttacker.cs:244-273`(`TryCastOnHitSkill`) | 동일(간접 의존) |
| `Assets/Scripts/Units/UnitAttacker.cs:568,582`(`Update`) | `UpdateSkillCooldown()`/`TryCastOnHitSkill()` 호출 — 유닛당 한 번씩만 부른다 |
| `Assets/Scripts/Units/UnitUpgrades.cs:130`(`SkillLevelIndexFor`)·`146`(`ReplacementSkillFor`) | `UnitData.skill`을 직접 읽진 않지만 "그 유닛의 스킬을 어떻게 바꿀지"를 판정 — 스킬이 여러 개면 **"어느 슬롯을 바꿀지"**로 확장돼야 한다 |

**UI**: `GameHud.cs`를 전수 grep했다 — `.skill`/`skillName`을 읽는 코드가 **없다**(스킬 툴팁 자체가 아직 없음). 영향 없음.

**`Tools/check_required_fields.py`**: `skill_assets = glob("Assets/Data/UnitSkills/*.asset") + ...`로 `SkillData` 폴더를 직접 훑지, `UnitData.skill`을 거치지 않는다. **영향 없음.**

**Python 생성기·시뮬레이터**
| 파일:줄 | 하는 일 |
|---|---|
| `Tools/generate_low_grade.py:103` | 로스터 신규 생성 시 `skill: {fileID: 0}` 기본값을 템플릿에 기록 |
| `Tools/generate_recipe_assets.py:165` | 동일(조합식으로 새로 만드는 유닛) |
| `Tools/generate_unit_ability_skills.py:151`(claimed 판정)·`235-236`(기록) | "이미 스킬이 있는가"를 `skill: {fileID: 0}` 여부로 판정해 배정 대상에서 제외, 배정 시 이 줄을 실제 guid로 덮어씀 |
| `Tools/generate_unit_skill_damage_effects.py:288,291`(claimed 판정)·`329-330`(기록) | 동일 패턴(2차 채널) |
| `Tools/simulate_balance.py:141`(guid 추출)·`555`(`skill_dps_for_unit`) | 이 필드에서 guid 하나를 뽑아 그 `SkillData` 하나의 기대 DPS만 합산 — **다중 스킬이 되면 이 정규식 하나로는 두 번째 이후 스킬을 영영 못 본다(DPS가 조용히 과소평가된다)** |

## ② `UnitAttacker` 인스턴스 상태 — 지금 스킬 1개 전제

```
skillCooldownTimer        (float) — CooldownAutoCast·Aura 전용
onHitCountCounter/Initialized (int/bool) — OnHitCount 전용
onHitChanceLockedUntil    (float) — OnHitChance 절대쿨 전용(오늘 추가)
```

넷 다 "이 유닛이 가진 스킬 하나"의 상태를 담는 단일 필드다. 스킬이 여러 개가 되면 **스킬마다 따로** 있어야 한다 — 스킬 A의 절대쿨이 스킬 B의 쿨다운과 섞이면 안 된다.

⚠️ **단, `onHitCountCounter`는 예외다 — 아래 ⑤-B에서 확인했듯 게이지가 스킬별이 아니라
유닛 공유(원작 마나/체력 하나)라서, 이 카운터만은 "스킬별"이 아니라 "유닛당 게이지 종류별
(마나 1개·체력 1개)"로 따로 빼야 한다.** 나머지 셋(`cooldownTimer`·`onHitChanceLockedUntil`)은
스킬(버프 ID)마다 독립이라 그대로 스킬별이 맞다 — 원작이 버프 ID(B00K·B00N…)를 스킬마다
따로 쓰는 것과 같다.

**권장 형태**: 스킬별 상태는 `Dictionary<SkillData, SkillRuntimeState>` (SkillData 에셋 자체를
키로) — 이미 이 코드베이스가 같은 패턴을 쓴다(`SupportShop.cooldownUntil`, `GamblingProgress`).
배열/리스트 인덱스보다 스킬 슬롯 순서가 바뀌어도 안전하다. 공유 게이지는 별도 필드 둘로 뺀다.

```csharp
// 스킬(버프)별 — 최대 6개면 이 Dictionary가 6엔트리까지 큰다.
class SkillRuntimeState
{
    public float cooldownTimer;            // CooldownAutoCast·Aura 전용
    public float onHitChanceLockedUntil;   // OnHitChance 절대쿨 전용
}
Dictionary<SkillData, SkillRuntimeState> skillStates;

// 유닛 공유 — 원작 WC3 유닛이 마나 하나·체력 하나만 갖는 것과 같다(⑤-B 참고).
// "체력형" 게이지도 우리 플레이어 유닛엔 HP 개념 자체가 없어(UnitCombat에 필드가 없다,
// 이번 세션에서 이미 확인됨) 원래 체력을 대신 못 쓴다 — OnHitCount가 이미 그래서
// 별도 카운터(마나형과 같은 모양)로 흉내내고 있다. 원작에서 MANA인지 LIFE인지는
// 값의 뜻일 뿐 우리 구현에선 똑같이 "가상의 정수 카운터"다 — 그래서 필드는 "게이지
// 종류별"(원작 MANA를 공유하는 스킬들의 그룹 / 원작 LIFE를 공유하는 스킬들의 그룹)
// 딱 둘이면 된다, 스킬마다 하나씩이 아니다.
int manaGaugeCounter;
bool manaGaugeInitialized;
int lifeGaugeCounter;
bool lifeGaugeInitialized;
```

`UpdateSkillCooldown`/`TryCastOnHitSkill`을 "스킬 하나 처리"로 추출한 뒤 `foreach (SkillData skill in Skills)`로 감싸는 리팩터가 필요하다 — 지금은 이 두 메서드가 "유닛엔 스킬이 최대 하나"를 전제로 짜여 있어서 단순 확장이 아니라 **구조 변경**이다. 게이지 공유 스킬들은 이 루프 밖에서 **한 번만** 카운터를 올리고, 그 값을 그룹 안 스킬들이 각자의 추가 조건(확률·절대쿨 등)과 함께 검사하는 2단 구조가 필요하다(⑤-B의 사보 예시 참고).

## ③ 직렬화 마이그레이션 — 238개 에셋의 `skill:` 한 줄

**옵션 A(하위호환, 권장)**: 기존 `skill` 필드는 그대로 두고 `skills`(`List<SkillData>`)를 새로 추가. `UnitAttacker.Skill`(→`Skills`로 확장)이 "`skills`가 있으면 그걸, 없으면 `skill` 하나를 담은 목록처럼" 합쳐 읽는다.
- 장점: 기존 238개 에셋을 **한 개도 안 건드린다** — 이번 배치로 새로 다중스킬이 필요해진 유닛(≈수건)만 `skills`를 채우면 된다. 회귀 위험이 가장 작다.
- 단점: 필드가 두 개 공존해서 "어느 걸 봐야 하는지" 규칙이 하나 늘어난다 — 다음 사람이 `skill`만 보고 `skills`를 놓칠 함정이 생긴다. 주석으로 반드시 못박아야 한다.

**옵션 B(일괄 변환)**: 238개 파일의 `skill: {...}` 한 줄을 `skills:\n  - {...}` 리스트 항목 하나로 스크립트 일괄 치환.
- 장점: 필드가 하나로 끝나 개념이 단순해진다.
- 단점: 238개 파일이 전부 diff에 걸린다(오늘만도 "index race"로 두 번 걸린 팀이다 — 대량 일괄 치환 커밋은 리뷰가 사실상 불가능하고, 마침 그 순간 다른 세션이 그 폴더를 만지면 다시 같은 사고가 난다). 게다가 필드명이 바뀌는 순간 이걸 읽는 5개 파이썬 스크립트+`UnitAttacker.cs`+`simulate_balance.py`를 **같은 커밋에서 전부** 고쳐야 컴파일 가능한 상태가 유지된다 — 단계적 이행이 안 된다.

**결론**: 옵션 A. 반나절 안에 끝나고 기존 에셋을 안 건드린다.

## ④ 규모 추정 — `List<SkillData> skills` 전제 (v2)

**전제 변경**: `secondarySkill`(필드 하나 추가) 안은 **폐기됐다**(⑤-B 참고 — 96종 중 64종이
스킬 2개 이상, 최대 5개 + 기존 1개 = **유닛당 최대 6개**). 처음부터 `List<SkillData>`로
설계한다.

**바뀌는 파일(코드/스크립트)**: `UnitData.cs`(필드 타입 자체 변경 또는 병행 — ③의 하위호환
옵션 A는 그대로 유효, `skills` 리스트를 새로 얹는다) · `UnitAttacker.cs`(리팩터, 가장 큼 —
스킬 루프 + `Dictionary<SkillData, SkillRuntimeState>` + 공유 게이지 카운터 2개, ② 참고) ·
`UnitUpgrades.cs`(어느 슬롯을 대체/승급하는지로 확장) · `generate_unit_ability_skills.py` ·
`generate_unit_skill_damage_effects.py` · `simulate_balance.py`(⑤-C 참고, 지금은 단일 스킬
전제라 다시 짜야 함) · `generate_low_grade.py` · `generate_recipe_assets.py` = **8개**. 여기에
**이번 200건을 실제로 배정할 새 생성기 스크립트가 하나 더 필요하다**(기존 5개는 "등급 안
DPS-순위로 CSV 행 하나 ↔ 로스터 유닛 하나"를 독립적으로 매칭하는데, 이번 채널은 **원작
유닛 하나가 가진 스킬 묶음 전체를 로스터 유닛 하나에 그대로 몰아줘야** 한다 — 사보 4개가
같은 마나 게이지를 공유하는데 서로 다른 유닛에 흩어지면 그 공유 자체가 깨진다). 이 배정
전략(원작 유닛 단위 매칭)은 기존 5개 스크립트의 방식과 다른 새 설계라 **별도 스크립트로
분리하는 편이 안전**하다 — 기존 스크립트를 고치다 231종 이미 배정된 것까지 건드리는
위험을 피할 수 있다.

**바뀌는 데이터 파일**: ③의 하위호환(옵션 A) 그대로 — **강제 변경 0개**, 96종만 `skills`를
채운다.

**위험한 곳 하나**: (① 그대로) `UnitAttacker.Skill`의 "단일 반환" 계약을 깨는 리팩터가
가장 위험하다 — **이번엔 회귀 대상이 231종이 아니라, 새로 6개까지 갖는 96종까지 더한
327종 안팎**이 된다(정확한 총합은 새 채널 배정 후 확정). 스킬이 많을수록 "한 프레임에
여러 스킬이 동시에 조건을 만족하면 어느 순서로 도는가" 같은 새 엣지케이스도 늘어난다
(사보처럼 공유 게이지 하나가 여러 스킬을 동시에 충족시킬 수 있어서 — ⑤-B 참고).

**작업량(하루 단위, 순수 추정 — v1의 약 3일에서 상향)**:
| 항목 | 일수 | 비고 |
|---|---|---|
| 스키마(`skills` 필드) + 마이그레이션 폴백 | 0.5일 | ③과 동일, 안 커짐 |
| `UnitAttacker` 리팩터(스킬 루프 + Dictionary + 공유 게이지 2종) | 2일 | v1(1일)에서 상향 — 공유 게이지 2단 구조가 새로 필요 |
| 이번 채널 전용 배정 스크립트(원작 유닛 단위 묶음 매칭) | 1.5일 | v1엔 없던 항목 — 기존 랭크매칭과 다른 새 설계 |
| 기존 생성기 5개 정합화(`skills` 기준 claimed 판정) | 0.5일 | 그대로 |
| `simulate_balance.py` 다중 스킬 DPS 합산(+공유 게이지 반영) | 1일 | v1(0.5일)에서 상향 — ⑤-C 참고 |
| 회귀 확인(기존 단일스킬 유닛 + 96종 신규 스팟체크) | 1일 | v1(0.5일)에서 상향 — 대상이 커짐 |
| **합계** | **약 6.5일**(버퍼 포함 8일) | |

## ⑤ PM 요청 확인 사항 3건

### A. 스킬별 상태를 배열/리스트로 들 때의 모양

②에서 이미 다뤘다 — 요약하면 **"스킬별"과 "유닛 공유"를 갈라야 한다**:
- **스킬별**(`Dictionary<SkillData, SkillRuntimeState>`, 최대 6엔트리): `cooldownTimer`(CooldownAutoCast·Aura), `onHitChanceLockedUntil`(OnHitChance 절대쿨) — 원작이 스킬(버프 ID)마다 독립으로 쓰는 축이라 그대로 스킬별.
- **유닛 공유**(`int`/`bool` 필드 각 2개, 게이지 종류당 하나): `manaGaugeCounter`·`lifeGaugeCounter`(+초기화 플래그) — 아래 B에서 확인했듯 원작 마나·체력이 유닛에 하나뿐이라, 그걸 공유하는 스킬 여러 개가 있어도 카운터는 하나여야 한다.

배열(인덱스 기반)보다 `Dictionary<SkillData, ...>`를 권하는 이유는 ②와 같다 — 스킬 순서가
바뀌거나 `skills` 리스트 중간에 항목이 빠져도(예: 06번① 능력교체로 통째로 갈아끼움) 키가
에셋 자체라 안전하다.

### B. 게이지가 스킬마다 따로인가, 유닛의 마나 하나를 공유하는가 — **공유한다, 확인함**

`ORIGINAL_UNLISTED_SKILLS.csv`(232행)를 직접 파이썬으로 돌렸다. 한 유닛이 같은 종류
게이지(MANA 또는 LIFE)로 게이트된 스킬을 **2개 이상** 가진 경우가 **MANA 10종·LIFE 11종**
있고, **그중 서로 다른 임계값을 쓰는 사례는 0건**이다 — 즉 같은 유닛 안에서 같은 종류
게이지를 쓰는 스킬들은 **전부 같은 임계값**을 본다. 원작 WC3 엔진에서 유닛의 마나·체력이
하나뿐이라는 사실과 정확히 들어맞는다(다른 임계값이 있었다면 애초에 "공유 스탯"일 수가
없다). 가장 명확한 예:

```
H092(사보) — MANA게이지125.00, 스킬 4개가 전부 이 임계값:
  Sabo_Mana    (RRD 2건, 375만)
  Sabo_Skill_1 (RRD 3건, 325만 + 절대쿨 12.5s)
  Sabo_Skill_3 (RRD 1건, 115만 + 절대쿨 12.5s + 1/10)
  Sabo_Skill_4 (RRD 4건, 50만  + 절대쿨 12.5s + 1/10 + 1/6)
```

넷 다 "마나==125"라는 같은 조건을 보되, 그 위에 저마다 다른 추가 조건(절대쿨·확률)을
얹어서 갈린다 — **공유 게이지 하나가 열리면 그 순간 여러 스킬이 동시에 후보가 되고,
각자의 추가 조건이 다시 갈라준다**는 뜻이다. 그래서 `OnHitCount`(게이지) 카운터는
**스킬 단위가 아니라 유닛 단위(게이지 종류별)**여야 한다는 게 확정됐다 — ②에 반영.

### C. `simulate_balance.py`의 `skill_dps`가 단일 스킬 전제인가 — **그렇다, 확인함**

`Tools/simulate_balance.py:141`:
```python
skill_m = re.search(r"^  skill: \{fileID: \d+, guid: ([0-9a-f]+)", text, re.MULTILINE)
skill_dps, skill_percent_rate, skill_attack_type_idx = skill_dps_for_unit(
    skill_m.group(1) if skill_m else None, ap, aspd)
```
정규식이 **`skill:` 한 줄에서 guid 하나만** 뽑고, `skill_dps_for_unit`(555줄)에 그 guid
하나만 넘긴다. `skills:`(리스트) 형식으로 바뀌면 이 정규식은 **매치되지 않는다**(줄 형태가
`skill: {fileID: ...}`가 아니라 `skills:\n  - {fileID: ...}`가 되므로) — `skill_m`이
`None`이 되어 **`skill_dps=0`으로 조용히 떨어진다.** 다중 스킬 유닛의 DPS 기여가 시뮬레이션
에서 **전부 빠지는** 회귀다 — ③ 하위호환(옵션 A)을 택해도 **이 스크립트만은 반드시 같이
고쳐야** 한다(`skills` 필드를 새로 볼 수 있게, 그리고 리스트 안 여러 `SkillData`의 DPS를
합산하게). ④의 "1일" 항목이 이 이유다.
