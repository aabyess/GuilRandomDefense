# `UnitData.skill`(단일) → 다중 스킬 — 영향 범위 견적 (2026-09-05, 구현담당3)

> 코드는 안 건드렸다. 사장님 결정용 견적 문서다. 배경: 리서치담당이 「절대쿨 게이트 안에서
> 별도 트리거를 실행」하는 원작 스킬 채널을 새로 찾았다(`Docs/reference/GATED_SKILLS_14.md`,
> `07c574d`) — 뱌쿠야 450만/14초·사보 3개·나미·카이도·레일리 등 실질적으로 9행(서로 다른
> 능력 기준 7개, 원작 인물 기준 5명). 대상 유닛이 이미 1·2채널 스킬을 갖고 있어 넣으려면
> `UnitData.skill`이 하나가 아니라 여러 개여야 한다.

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

**권장 형태**: `Dictionary<SkillData, SkillRuntimeState>` (SkillData 에셋 자체를 키로) — 이미 이 코드베이스가 같은 패턴을 쓴다(`SupportShop.cooldownUntil`, `GamblingProgress`). 배열/리스트 인덱스보다 스킬 슬롯 순서가 바뀌어도 안전하다.

```csharp
class SkillRuntimeState
{
    public float cooldownTimer;
    public float onHitChanceLockedUntil;
    public int onHitCountCounter;
    public bool onHitCountInitialized;
}
```

`UpdateSkillCooldown`/`TryCastOnHitSkill`을 "스킬 하나 처리"로 추출한 뒤 `foreach (SkillData skill in Skills)`로 감싸는 리팩터가 필요하다 — 지금은 이 두 메서드가 "유닛엔 스킬이 최대 하나"를 전제로 짜여 있어서 단순 확장이 아니라 **구조 변경**이다.

## ③ 직렬화 마이그레이션 — 238개 에셋의 `skill:` 한 줄

**옵션 A(하위호환, 권장)**: 기존 `skill` 필드는 그대로 두고 `skills`(`List<SkillData>`)를 새로 추가. `UnitAttacker.Skill`(→`Skills`로 확장)이 "`skills`가 있으면 그걸, 없으면 `skill` 하나를 담은 목록처럼" 합쳐 읽는다.
- 장점: 기존 238개 에셋을 **한 개도 안 건드린다** — 이번 배치로 새로 다중스킬이 필요해진 유닛(≈수건)만 `skills`를 채우면 된다. 회귀 위험이 가장 작다.
- 단점: 필드가 두 개 공존해서 "어느 걸 봐야 하는지" 규칙이 하나 늘어난다 — 다음 사람이 `skill`만 보고 `skills`를 놓칠 함정이 생긴다. 주석으로 반드시 못박아야 한다.

**옵션 B(일괄 변환)**: 238개 파일의 `skill: {...}` 한 줄을 `skills:\n  - {...}` 리스트 항목 하나로 스크립트 일괄 치환.
- 장점: 필드가 하나로 끝나 개념이 단순해진다.
- 단점: 238개 파일이 전부 diff에 걸린다(오늘만도 "index race"로 두 번 걸린 팀이다 — 대량 일괄 치환 커밋은 리뷰가 사실상 불가능하고, 마침 그 순간 다른 세션이 그 폴더를 만지면 다시 같은 사고가 난다). 게다가 필드명이 바뀌는 순간 이걸 읽는 5개 파이썬 스크립트+`UnitAttacker.cs`+`simulate_balance.py`를 **같은 커밋에서 전부** 고쳐야 컴파일 가능한 상태가 유지된다 — 단계적 이행이 안 된다.

**결론**: 옵션 A. 반나절 안에 끝나고 기존 에셋을 안 건드린다.

## ④ 규모 추정

**바뀌는 파일(코드/스크립트)**: `UnitData.cs`(+1줄) · `UnitAttacker.cs`(리팩터, 가장 큼) · `UnitUpgrades.cs`(슬롯 지정으로 확장) · `generate_unit_ability_skills.py` · `generate_unit_skill_damage_effects.py` · `simulate_balance.py` · `generate_low_grade.py` · `generate_recipe_assets.py` = **8개**.

**바뀌는 데이터 파일**: 옵션 A 채택 시 **강제 변경 0개** — 이번에 다중스킬이 필요한 유닛만 `skills`를 채운다(실제 배정 전엔 정확한 수를 모른다, 아래 ⑤ 참고).

**위험한 곳 하나**: `UnitAttacker.Skill`이 지금 "단일 `SkillData` 반환"이라는 계약이고, `TryCastOnHitSkill`·`UpdateSkillCooldown` 둘 다 그 계약 위에 짜여 있다. 이걸 리스트로 바꾸는 리팩터 중 실수하면 **이미 스킬이 배정된 231종(현재까지 채널 1·2 합산)의 동작까지 조용히 깨질 수 있다** — 가장 조심해야 할 지점이자, 회귀 테스트를 반드시 여기 집중해야 한다.

**작업량(하루 단위, 순수 추정)**:
| 항목 | 일수 |
|---|---|
| 스키마(`skills` 필드) + 마이그레이션 폴백 | 0.5일 |
| `UnitAttacker` 리팩터(런타임 상태 Dictionary화 포함) | 1일 |
| 생성기 5개 스크립트 정합화(claimed 판정을 `skills` 기준으로) | 0.5일 |
| `simulate_balance.py` 다중 스킬 DPS 합산 | 0.5일 |
| 회귀 확인(기존 단일스킬 유닛 스팟체크) | 0.5일 |
| **합계** | **약 3일**(버퍼 포함 4일) |

## ⑤ 대안 — `secondarySkill` 필드 하나만 추가

**원작 한 유닛 최대 스킬 수**: `ORIGINAL_GATED_SKILLS_14.csv`(30행)를 직접 셌다. 미수록 20행 중 실제 서로 다른 능력은 **7개**(`Byakuya_E`·`Sabo_Skill_1`·`Sabo_Skill_3`·`Sabo_Skill_4`·`Nami_Skill_4`·`Kaido_Skill_1_8`·`Kick_1`, 나머지는 같은 능력의 조건별 수치 변형 행), 원작 **인물 기준 5명**(뱌쿠야·사보·나미·카이도·레일리)이다. **사보 한 명이 하위 능력 3개**로 최댓값이다.

다만 "이름 매핑 불가" 원칙상(등급 안 DPS-순위로 CSV 행과 로스터 유닛을 독립적으로 짝짓는다) 사보의 3개 행이 **로스터의 서로 다른 유닛 3종에 나뉘어 배정될 가능성이 높다** — 그러면 로스터 유닛 한 종이 실제로 받는 스킬 수는 "기존 채널(1·2차) 1개 + 이번 게이트채널 1개" = **2개**가 현실적 최댓값이 된다. **확정은 아니다** — 등급 안 순위가 겹치면 같은 유닛에 둘 다 갈 수도 있고, 이건 실제 배정 스크립트를 돌려봐야 안다.

**`secondarySkill` 하나로 충분한가**: 지금까지 확인된 규모(최대 2개/유닛)에는 **충분해 보인다.** `UnitAttacker` 쪽 변경도 "if문 하나 더"에 가까워 List/Dictionary 전체 리팩터보다 훨씬 작다(1일 미만).

**위험**: `GATED_SKILLS_14.md` 스스로 "전수하면 더 나올 것"이라고 적어뒀다 — 3번째 채널이 또 나오면 `secondarySkill` 하나로는 다시 막힌다. 그때는 이 견적을 그대로 반복해야 한다.

**권장**: 지금 확인된 규모엔 `secondarySkill` 쪽이 더 싸고 안전하다 — 다만 게이트형 채널이 아직 전수 조사 전이라는 점을 감안해, `secondarySkill`을 먼저 넣고 **3번째 사례가 실제로 나오면 그때 `List<SkillData>`로 확장**하는 단계적 접근도 고려할 만하다(이름이 "두 번째"에 고정돼 있어 나중에 확장할 때 리네이밍 부담이 생긴다는 점은 미리 알아둘 것).
