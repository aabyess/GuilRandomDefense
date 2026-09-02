# 특성강화 설계안 (2026-09-02, 구현담당1)

> PM 지시로 코드 작성 전에 먼저 정리한다. 등급 전체 강화(구 유닛강화소)는 사장님 결정으로 중단됐고,
> 원작 "특성강화"(유닛 1종당 개별 효과, 특성포인트 소모) 방식으로 간다. 표기: **[설계 제안]** = 이 문서가
> 새로 판단한 것 / **[확인 필요]** = 사장님·PM 확정이 있어야 코드로 들어갈 수 있는 것 / **[원작]** =
> `Docs/reference/UPGRADE_SHOP.md`·`GAMBLING.md`·`PUNK_HAZARD.md` 조사에서 이미 확인된 사실.

## 업데이트 (2026-09-02, PM 승인 + 3차 조사 반영)

리서치담당 3차 조사(`UPGRADE_SHOP.md` "3차 조사")가 표본을 22종으로 늘렸다. 결정적 발견: **유닛 1개 = 효과 유형 1개가 아니다.** 조로는 딜증가+방깎+이감을 한 특성강화로 동시에 받는다 — 아래 1번 항목이 전제했던 "유닛당 하나"를 **"유닛당 {효과유형, 수치} 리스트(보통 1~3개)"로 넓혔다.** PM이 이 구조로 승인했고, 아래 순서로 이미 구현했다:

1. `TraitEffectKind`(enum, 11개 + 확장 여지) — `Assets/Scripts/Data/UnitTraitData.cs`
2. `UnitTraitData`(SO, `targetUnit` + `List<TraitEffect>` + `specialEffectId`) — 같은 파일
3. `UnitUpgrades` 재설계(특성포인트 보유량 + 언락 `HashSet<UnitTraitData>`) — `Assets/Scripts/Units/UnitUpgrades.cs`
4. `UnitAttacker` 반영 — **`DamageIncrease`만.** 나머지 10개 유형은 아래 표 그대로 미반영.
5. 235개 스캐폴딩(`Tools/generate_unit_traits.py`, `Assets/Data/Traits/`) — 전부 효과 없음, `costTraitPoints=4`

**이감·방깎을 미루는 게 "나중에 여유될 때"가 아니라는 걸 숫자로 남긴다.** 3차 조사 표본 22종 중 이감을 쓰는 게 4종(브룩·아오키지·빅맘 + 조로), 방깎이 2종(조로·킹) — 겹치는 조로를 빼면 **5종/22종(약 23%)이 이감이나 방깎 중 하나 이상을 쓴다.** 효과 인스턴스 단위로는 34건 중 6건(약 18%). **"2차로 미룬다"는 이 5종(조로·브룩·아오키지·빅맘·킹)의 특성강화가 지금은 절반도 못 채워진 채로 들어간다는 뜻이다** — `EnemyDummy`에 방어력 필드·%감속 인프라가 생기기 전까진 이 유닛들의 트레잇 에셋에 `SlowOnHit`/`ArmorShred` 항목을 넣어도 전투에 반영되지 않는다.

**획득처는 사장님 결정 대기 중이라 배선하지 않았다.** "35,000골드 누적 졸업"은 이번 세션에서 한 번 명시적으로 제거된 개념과 이름이 같다는 걸 PM이 확인해줬다(`GamblingProgress` 재설계 때, 사장님이 도박 규칙을 직접 정하며 제거) — 다시 넣을지 다른 획득처를 쓸지는 사장님 몫이다. `UnitUpgrades.AddTraitPoints(int)`는 저장소·이벤트 자리만 만들어뒀고 아직 아무도 안 부른다.

**UI(등급 탭 → 유닛 목록)는 이번 라운드에서 빠졌다** — 구현담당2의 `ILaneShop`/`GameHud` 리팩터와 맞물려 있어 승인 후 별도로 진행한다. 구 등급강화 상점(`UnitUpgradeShop.cs`, `UnitUpgradeTrackData.cs`, `Assets/Data/UnitUpgrades/*.asset` 10개)은 그대로 살아있다 — `UnitUpgrades.cs`에 구 등급강화용 `Level`/`LevelUp(UnitUpgradeTrackData)` 메서드를 "레거시" 블록으로 남겨뒀다(그 상점이 아직 이걸 참조해서 컴파일이 깨지지 않게). 특성강화 UI가 그 자리를 대체하면 이 블록과 구 에셋 전부 한 번에 지운다.

---

## 요약 (최초 작성분, 아래는 원문 유지)

| 항목 | 결론 |
|---|---|
| 효과 표현 | 스탯 수정자(데이터) + 특수 스킬(코드, 필요할 때만) 두 층으로 나눈다 |
| 특성포인트 저장 | `PlayerContext` 사이드카(기존 `UnitUpgrades`를 리네임·재설계) |
| 획득처 | 4개 후보 전부 선행 시스템이 없다 — 단계적으로 배선 |
| 적용 지점 | 플레이어 유닛 스탯은 `UnitAttacker`(기존 강화 배율 자리 재사용), 적 디버프·특수 스킬은 선행 시스템 필요 |
| 235종 데이터 | 유닛 1종 = 에셋 1개, 전부 자리만 만들고 값은 임시 |

---

## 1. 효과를 어떻게 표현할 것인가

리서치담당이 뽑은 4개 사례(`UPGRADE_SHOP.md` 2차 조사)를 다시 보면:

| 유닛 | 효과 | 분류 |
|---|---|---|
| 조로(초월) | 방깎·이감 각각 +5 | **스탯 증분** |
| 우솝 | 공격력 +22,222 | **스탯 증분** |
| 아오키지(초월) | 공중이동 가능 + 폭뎀 5% + 이감 10% | **스탯 증분 2개 + 플래그 변경 1개** |
| 브룩(초월) | 이감 7% + 9번째 공격마다 6초 공속25%+마나5 | **스탯 증분 1개 + 조건부 트리거 스킬** |
| 키자루(초월) | 분신 개수 제한 변경 | **순수 커스텀 로직** |

**[설계 제안] 단일 enum+수치 공식으로는 전부 못 담는다.** 조로·우솝처럼 "스탯 하나에 값 하나 더한다"는 데이터로 완전히 표현되지만, 키자루(분신 개수)나 브룩(9타 트리거)은 그 유닛 전용 코드가 있어야 한다. 억지로 하나의 표로 밀어넣으면 나머지 234종을 위해 만든 범용 구조가 정작 "재미있는" 특성(브룩·키자루류)을 못 담는 모순이 생긴다.

그래서 두 층으로 나눈다:

### Tier A — 스탯 수정자 (데이터, 코드 없이 채운다)
```csharp
public enum TraitStatKind { AttackPowerFlat, AttackPowerPercent, AttackSpeedPercent,
                             AttackRangeFlat, MoveSpeedPercent, SlowOnHitPercent,
                             ArmorShredOnHitFlat, ... }
public enum TraitStatOp { Add, Multiply }

[System.Serializable]
public class TraitStatModifier
{
    public TraitStatKind kind;
    public TraitStatOp op;
    public float value;
}
```
조로(방깎+5, 이감+5)·우솝(공격력+22222)·아오키지(폭뎀+5%, 이감+10%)의 스탯 부분·브룩(이감+7%)이 전부 이 표만으로 표현된다. 대부분의 유닛은 여기서 끝날 것으로 예상한다.

**주의 — `SlowOnHitPercent`·`ArmorShredOnHitFlat`는 지금 게임에 받아줄 자리가 없다.** `EnemyDummy`엔 방어력 필드 자체가 없고(데미지가 hp에 그대로 들어간다), 이동속도 디버프도 `AddFreeze/RemoveFreeze`(완전 정지)만 있고 %감속은 없다. 이 두 종류는 **선행 작업**(4번 항목 참고)이 끝나야 실제로 동작한다 — 그 전엔 에셋에 값만 들어있고 전투에 반영 안 되는 상태로 둘 수밖에 없다.

### Tier B — 특수 스킬 (코드, 실제로 만들 유닛만)
```csharp
public string specialEffectId; // 예: "kizaru_clone_limit", "brook_ninth_hit_proc" — 비어있으면 없음
```
키자루·브룩(트리거 부분)처럼 범용 표로 못 담는 유닛은 이 문자열 키만 데이터에 심어두고, **실제로 그 유닛이 콘텐츠에 들어갈 시점에** 이 키를 보고 분기하는 코드를 그때 가서 짠다. 235종을 한 번에 다 만들 수 없으니, 처음엔 전부 비워두고(효과 없음) 나중에 사장님이 우선순위를 주는 유닛부터 채운다.

**[확인 필요]** 이 두 층 구조 자체(범용 스탯표 + 특수 유닛만 코드)에 동의하는지.

---

## 2. 특성포인트를 어디에 저장할 것인가

플레이어별이다(사장님 확정). `PlayerContext` 사이드카가 맞다 — `GamblingProgress`·(구)`UnitUpgrades`와 같은 자리.

**[설계 제안]** 기존 `UnitUpgrades.cs`를 재활용해서 이렇게 바꾼다(파일명 유지 여부는 PM 판단에 맡긴다):
```csharp
public class UnitUpgrades : MonoBehaviour
{
    public int TraitPoints { get; private set; }
    public void AddTraitPoints(int amount) { ... }

    readonly HashSet<UnitTraitData> unlocked = new HashSet<UnitTraitData>();
    public bool IsUnlocked(UnitTraitData trait) => unlocked.Contains(trait);
    public void Unlock(UnitTraitData trait) { ... TraitPoints -= trait.cost; unlocked.Add(trait); }
}
```
`GamblingProgress`의 `HashSet<GamblingOptionData> unlockedOptions`와 완전히 같은 패턴이다 — 특성강화가 "유닛 1종당 1회, 이후 영구 적용"이라는 원작 서술(레벨 표기가 어디에도 없다)에 근거해 **1회성 언락**으로 설계했다. **[확인 필요]** 특성이 여러 단계로 반복 강화되는 사례가 있는지(리서치 4개 사례 전부 단발성 서술이라 없다고 가정했다).

`UnitAttacker`가 이미 `UnitUpgrades.OnLevelChanged` 이벤트를 구독해서 배율 캐시를 무효화하는 구조를 만들어놨다(등급강화용) — 이벤트 이름만 유지하면 그대로 재사용된다.

---

## 3. 획득처 배선

`UPGRADE_SHOP.md`·`GAMBLING.md`·`PUNK_HAZARD.md`에서 이미 확인된 후보 4개, 전부 **선행 시스템이 아직 없다**:

| 획득처 | 조건 [원작] | 이 게임에 있는가 |
|---|---|---|
| 폐문(에니에스로비) 보스 처치 | 15/20/25회마다 1개(최대 3개), 보스는 아카이누·아오키지·키자루 | **없음.** `PUNK_HAZARD.md`가 조사한 "정의문"이 이 폐문에 대응하는 것으로 보이지만, 그 조사 자체가 "판단 필요"로 열려 있다(문서 57행) |
| 도박 누적 35,000골드 "졸업" | 초급·중급·고급 돈 도박 누적 획득 35,000골드 | **없음** — 그리고 **이전에 한 번 제거된 개념이다.** `GamblingProgress`를 지금 형태(옵션별 사용횟수/해금)로 다시 짤 때, "35,000골드 누적 졸업"은 사장님이 명시적으로 걷어낸 설계였다(이번 세션 안에서 있었던 일). **지금 특성포인트 획득처로 다시 나온 이 "졸업"이 그때 제거된 것과 같은 개념인지, 아니면 완전히 다른 새 카운터인지 사장님 확인이 필요하다** — 특히 "졸업 = 더 이상 그 도박을 못 한다"는 잠금까지 같이 가져올지, 아니면 그냥 누적치 도달 시 포인트만 주고 도박은 계속 열려 있을지. |
| 누적 세이브 15/20/25회 | [원작], 세이브 1회의 정의가 불명 | **없음** — "세이브"가 라운드 클리어인지 오토세이브 체크포인트인지부터 확인 필요 |
| 고대의 배 판매 | [원작] | **없음** — `Warehouse.cs`에 판매(sell) 기능 자체가 없고, 로스터 235종 중 "고대의 배"라는 이름의 유닛도 없다(직접 검색 확인) |

**[설계 제안]** 4개를 한 번에 다 만들 필요는 없다. 저장소(`AddTraitPoints`)와 UI(보유 포인트 표시)만 먼저 만들어두고, 위 표의 "없음" 칸이 하나씩 채워질 때마다(다른 스토리/시스템 작업으로) 그 시스템이 `AddTraitPoints(1)`을 부르게 연결하면 된다 — `EnemyDummy.OnBossKilled`(정적 이벤트, 신호만 보내는 패턴)를 이미 이 프로젝트가 쓰고 있으니 같은 결로 확장 가능하다.

**[확인 필요]** 위 표에서 어느 것부터 먼저 만들지 우선순위, 그리고 도박 "졸업" 개념의 재도입 여부.

---

## 4. 효과를 실제로 어디에 적용할 것인가

### 플레이어 유닛 스탯(Tier A 대부분) → `UnitAttacker`
등급강화 때 이미 만든 자리를 그대로 확장한다. 지금 `UnitAttacker`는:
```csharp
public float AttackDamage => attackDamage * UpgradeMultiplier;   // UpgradeMultiplier: 등급별 배율
```
로 되어 있다(PM이 "때릴 때만 곱한다"로 확정한 설계 — 원본 스탯은 안 건드리고, 도움소 임시 버프와 안 겹친다). 이걸 "등급 배율"이 아니라 **"등급 배율 × 이 유닛 종의 특성 배율/가산"** 으로 확장하면 된다:
```csharp
float StatMultiplier(TraitStatKind kind) => /* UnitUpgrades에서 이 유닛(UnitData) 특성의 Tier A 수정자 조회 */
public float AttackDamage => (attackDamage + FlatBonus(AttackPowerFlat)) * PercentMultiplier(AttackPowerPercent);
```
`AttackSpeedPercent`도 이미 있는 `attackSpeedBuffs`(도움소 임시 버프가 곱연산으로 쌓는 리스트) 자리에 영구 항목 하나를 추가하는 식으로 자연스럽게 얹힌다 — **새 구조가 필요 없다.**

### 적에게 거는 디버프(이감%, 방깎) → 선행 작업 필요
`EnemyDummy`에 **방어력 필드가 없고**, 이동속도 감속도 `AddFreeze`(완전 정지)뿐 %감속이 없다. 이 두 효과 종류를 쓰는 특성(조로·아오키지·브룩 전부 이감을 쓴다 — 즉 235종 상당수가 걸릴 가능성이 높은 흔한 효과 종류다)은 아래 선행 작업이 먼저 있어야 한다:
- `EnemyDummy`에 `float ArmorFlat`(또는 %) 필드 + `TakeDamage`가 이걸 반영하는 공식
- `EnemyDummy`에 지속시간 있는 %이동속도 디버프 스택(도움소가 여러 스킬을 걸 수 있으니 `UnitAttacker.attackSpeedBuffs`와 같은 리스트-누적 패턴을 추천)

**[확인 필요]** 이 선행 작업을 특성강화 1차 범위에 포함할지, 아니면 1차는 `AttackPowerFlat/Percent`·`AttackSpeedPercent`·`AttackRangeFlat`·`MoveSpeedPercent`(전부 지금 있는 필드)만 지원하고 이감·방깎은 2차로 미룰지. **[설계 제안]** 후자를 권장한다 — 방어력·감속 시스템은 특성강화 말고도 다른 스킬(도움소 등)에서도 쓸 범용 인프라라, 특성강화 문서 하나에서 같이 결정하기엔 범위가 넓다.

### 특수 스킬(Tier B) → 그 유닛 전용 코드
지금 `SkillData`는 `Damage/Slow/Buff` 3종 범용 효과만 있고, 유닛별 커스텀 로직을 얹을 자리가 없다. 브룩(9타 트리거)·키자루(분신 개수)류는 유닛 전용 `MonoBehaviour`(예: 프리팹에 조건부로 붙는 컴포넌트)를 그 유닛이 실제로 필요해질 때 만드는 걸 제안한다 — 235종 전부에 미리 자리를 만들 수 없다.

---

## 5. 235종 데이터를 어떻게 채울 것인가

로스터 확인: `Assets/Data/Units/Roster/*.asset` 235개(PM이 말한 234종과 거의 일치, 오차는 재확인 필요 수준).

**[설계 제안] 유닛 1종 = 에셋 1개**, 기존 컨벤션(로스터 자체가 이미 이 방식) 그대로 따른다:
```csharp
[CreateAssetMenu(...)]
public class UnitTraitData : ScriptableObject
{
    public UnitData targetUnit;          // 이 유닛 1종
    public string traitName;             // 표시용
    public int costTraitPoints = 4;      // 원작 사례 대부분 4개 [원작]
    public List<TraitStatModifier> statModifiers = new();
    public string specialEffectId;       // Tier B, 비어있으면 없음
}
```
표 하나로 235행을 관리하는 방식(스프레드시트형 SO)도 고려했지만, 기각한다 — 유닛 참조(`UnitData`)를 인스펙터에서 드래그하기 어렵고, `targetGrades`처럼 이미 리스트 필드가 있는 이 프로젝트 컨벤션과도 안 맞고, Tier B 문자열 키를 유닛 하나씩 눈으로 확인하며 채우기도 표보다 개별 에셋이 낫다.

**생성**: 기존 패턴(`Tools/generate_unit_upgrade_tracks.py`처럼 guid 고정 파이썬 스크립트)으로 235개를 한 번에 생성한다 — `costTraitPoints=4`, `statModifiers=[]`(효과 없음), `specialEffectId=""`인 **완전히 비어있는 자리**로 시작한다. 이러면 상점 UI(등급 탭 → 유닛 목록)가 지금 당장 "234종 전부 나열되지만 아직 아무 효과도 없음" 상태로 돌아가는 걸 확인할 수 있고, 사장님이 유닛별 수치를 주실 때마다 해당 에셋 하나만 갈아끼우면 된다.

---

## UI/상호작용 구조 — 열린 질문

지금 8칸(흔함&안흔함/특별함/.../랜덤유닛)은 등급마다 상점 건물 하나(`UnitUpgradeShop` 인스턴스)가 아니라, **건물 하나 안의 슬롯 8개**였다(`ILaneShop.SlotCount=8`). "등급 탭 → 그 안에서 유닛 선택"이 되려면, 한 등급 안에 유닛이 많게는 수십 종 있을 수 있어(로스터 235종 ÷ 등급 12종 안팎) `ILaneShop`이 지금처럼 고정 그리드(예: 도박소 9칸)로 전부 못 담을 가능성이 높다.

**[설계 제안]** 사장님이 확정한 "건물 5칸"은 그대로 두되(다른세계 강화소·영원함 강화소 포함), 유닛강화소 안에서는 **등급 선택 → 그 등급 유닛 목록** 2단 구조가 필요하다고 본다. 다만 이건 `GameHud.cs`가 그리드를 어떻게 그리는지와 맞물려 있어서, UI 쪽은 구현담당2(ILaneShop/GameHud 리팩터 담당)와 조율했다.

**해결됨(구현담당2, 2026-09-02)**: `GameHud`는 지금 "같은 `ILaneShop` 인스턴스인지"만 보고 슬롯 매핑(`RebuildShopSlots`)을 한 번만 짠다 — 상점이 내부적으로 "지금 보여주는 등급 탭"만 바꾸는 경우, `SlotCount`/`GetSlotView`는 0.4초마다 다시 불려도 **슬롯 개수·매핑 자체는 안 다시 짜인다.** 도움소/도박소/기존 강화소는 전부 단일 계층이라 지금은 이 문제가 실제로 안 생겨서 인터페이스에 미리 안 넣어뒀다.

특성강화가 탭 구조로 가면, `ILaneShop`에 `event Action OnSlotsChanged`(선택적 — `UnitUpgrades.OnLevelChanged`와 같은 결)를 추가해서, 상점이 탭을 바꿀 때 이걸 쏘고 `GameHud`가 구독해서 그 시점에 `RebuildShopSlots`를 다시 부르면 풀린다. 다른 3개 상점은 이 이벤트를 안 쏘면 그만이라 기존 동작에 영향 없다. **코드 승인 나면 구현담당2와 함께 이 이벤트를 같이 넣는다.**

---

## 기존 자산 재활용 범위 정리

| 기존 (등급강화) | 특성강화로 |
|---|---|
| `UnitUpgradeTrackData`(SO, 등급 1개=1개) | `UnitTraitData`(SO, 유닛 1종=1개)로 대체 |
| `UnitUpgradeShop`(ILaneShop, 등급 8슬롯) | 등급 탭 + 유닛 목록 2단 구조로 재작성 (위 열린 질문 참고) |
| `UnitUpgrades`(레벨 Dictionary) | 특성포인트 보유량 + `HashSet<UnitTraitData>` 언락 상태로 재작성 |
| `UnitAttacker.UpgradeMultiplier` | 이름·의미 확장(등급 배율 → 등급+특성 배율), 때릴 때 곱하는 구조는 그대로 |
| `Assets/Data/UnitUpgrades/*.asset` 10개(내 8 + 구현담당2의 2) | 폐기 — 새 `UnitTraitData` 235개로 대체 |
| `Tools/generate_unit_upgrade_tracks.py` | 폐기, `Tools/generate_unit_traits.py`(가칭)로 대체 |

---

## 다음 단계

이 문서 승인 후:
1. `TraitStatKind`·`UnitTraitData`·`UnitUpgrades`(재설계) 순으로 구현
2. 235개 빈 트레잇 에셋 생성 스크립트
3. `UnitAttacker`에 Tier A 반영
4. UI 2단 구조는 GameHud 쪽과 별도 조율 후 착수
5. 획득처 4종은 각자의 선행 시스템이 준비되는 대로 순차 배선(3번 항목 우선순위는 PM/사장님 확인 후)

구현담당2와는 이미 공유했다 — 다른세계·영원함 강화소도 같은 개편 대상이라 지금 단계에서 코드 작업은 같이 보류 중이다.
