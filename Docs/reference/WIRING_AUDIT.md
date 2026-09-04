# 배선 감사 — 「완료」라고 적힌 것이 실제로 도달 가능한가 (2026-09-04)

계기: `MobPrefab.prefab`의 `EnemyDummy.damageTable`이 **한 번도 배선된 적이 없어서 `null`**이었다.
코드도 에셋도 멀쩡했는데 **둘이 연결이 안 돼 있었다.** 그동안 맞춘 배율이 전부 곱해지지 않았다.

그래서 같은 종류가 더 있는지 전수로 봤다. 물은 것은 셋:

```
1. 코드가 있는가
2. 데이터(에셋)가 있는가
3. 둘이 실제로 연결돼 있는가      ← damageTable이 여기서 죽어 있었다
```

**고치지 않았다. 목록만이다.**

---

## 🔴 죽어 있음

### 1. `UnitUpgrades` 컴포넌트가 씬에 하나도 없다 — **강화·특성·방깎이 통째로 죽었다**

`MapGenerator`가 `PlayerContext`를 지으면서 지갑·인벤토리·도박진행도·창고는 붙이는데
**`UnitUpgrades`는 만들지도, 연결하지도 않는다** (`MapGenerator.cs:2379-2394`).

씬의 `PlayerContext` **4개 전부** `unitUpgrades: {fileID: 0}`이다. 그래서 `PlayerContext.UnitUpgrades`는 **항상 `null`**이다.

**그 null 하나에 매달려 있는 것들:**

| 죽는 것 | 어디서 | 증상 |
|---|---|---|
| **유닛강화소 12개** | `UnitUpgradeShop.cs:93` | `TryLevelUp`이 **항상 false**. 씬에 12개가 서 있는데 전부 안 눌린다 |
| **특성포인트 구매** (15,000엔) | `GamblingShop.cs:175` | 항상 false |
| **특성포인트 지급** | `RewardDistributor.cs:53·193` | `GrantStartingPoint()`·`GrantStoryPoint()`가 **no-op**. 아무도 포인트를 못 받는다 |
| **등급 강화 배율** | `UnitAttacker.cs:112` | 항상 1.0 |
| **방깎 · 마방깍** | `UnitAttacker.cs:112` | `ApplyArmorShred`가 `ResolveUpgrades()`에서 **즉시 리턴**. 한 번도 안 걸린다 |

> ⚠️ **`ArmorShred`가 여기서 죽어 있다.** `ARMOR_SYSTEM_DESIGN.md §8`의 구현순서 5번
> 「`ArmorShred` 특성을 `armorShred`에 연결」은 **코드로는 됐지만 실제로는 도달하지 못한다.**
> `damageTable`과 정확히 같은 모양이다.

**고치면 동작이 바뀐다.** 강화소 12개가 살아나고 배율이 1.0을 벗어난다.

### 2. Trait 에셋 239개가 씬에서 도달 불가 — **3중으로 죽어 있다**

| | 무엇이 없나 |
|---|---|
| (a) | **`UnitData`에 trait 필드가 없다.** 유닛 → 특성 경로 자체가 존재하지 않는다 |
| (b) | **`UnitUpgrades.Unlock(trait)`를 부르는 코드가 하나도 없다** (주석: *"포인트 차감·비용 확인은 상점(아직 없음) 몫"*) |
| (c) | **239개 전부 `effects: []`.** 연결돼도 효과가 0이다 |

`targetUnit`은 239개 전부 채워져 있다 — 껍데기는 다 있고 **속과 연결만 없다.**

**셋을 다 채워야 동작이 바뀐다.** 하나만 고치면 아무 일도 안 일어난다.

### 3. 미니맵이 씬에 없다

`MinimapCamera`·`MinimapBlips`·`MinimapViewportIndicator` — 셋 다 코드는 있는데
**씬·프리팹 어디에도 없고 `MapGenerator`도 안 만든다.**

**동작은 안 바뀐다**(UI만). 다만 「미니맵 구현됨」이라고 적힌 곳이 있으면 그건 거짓이다.

### 4. 창고 4개 중 3개가 주인과 연결 안 됨

씬에 `Warehouse`가 **4개** 있는데, `PlayerContext.warehouse`가 채워진 건 **0번 플레이어 하나뿐**이다.
`MapGenerator.FindWarehouse(playerId)`가 `OwnerPlayerId`로 찾는데 1·2·3번은 못 찾았다.

**지금은 안 문다** — 1·2·3번은 `occupied: 0`이라 아직 안 쓴다.
**멀티플레이로 레인을 켜는 순간 창고 없는 플레이어가 셋 생긴다.**

---

## ✅ 멀쩡함 (확인함)

### 에셋 도달성 — 901개 중 660개 도달, **못 도달하는 건 Trait 239개뿐**

씬·프리팹에서 출발해 GUID 참조를 전이적으로 따라간 결과:

| 폴더 | 수 | 도달 |
|---|---|---|
| Recipes 204 · Waves 75 · Enemies 89 · Traits 239 · Stories 13 · SupportSkills 10 · UnitUpgrades 10 · Wisps 7 · Gambling 6 · Items 2 · MainGachaTable · DamageTable | 901 | **Trait 239개 + 작업중 SupportSkill 2개만 도달 불가** |

**`DamageTable.asset`도 오늘 배선해서 이제 도달 가능하다.**

### 비어 있는 참조 대부분은 **코드에 폴백이 있다**

`{fileID: 0}`이라고 다 죽은 게 아니다. 확인한 것:

| 필드 | 폴백 |
|---|---|
| `CombineSystem.unitSpawner` | `FindFirstObjectByType` |
| `SupportShop.roundManager` | 〃 |
| `GameHud.selectionManager` · `DebugHud.selectionManager` | 〃 |
| `WarehouseController.warehouse` | 〃 |
| `DebugHud.warehouse` | `PlayerContext.Local.Warehouse` |
| `DestructibleGate.openTarget` | 비면 아래로 가라앉힘 |
| `Selectable.selectedIndicator` | 런타임 생성 |
| `WaypointMover.path` · `Wisp.data` | 스폰 때 주입 |

`AttackRangeIndicator`·`SelectionIndicator`는 `Selectable`이 런타임에 만든다.
`UnitCombat`은 없으면 `UnitAttacker`가 직접 탐색한다 — 선택 사항이다.

### 생성기와 에셋이 어긋난 것 — **하나뿐이었고 오늘 고쳤다**

`Tools/generate_*.py` 13개 중 **에셋 필드를 덮어쓰는 건 `generate_unit_stats.py`뿐**이다.
그게 `damageType`을 1(AD)로 덮고 있었고, 오늘 표에서 뺐다(`7ff02b5`).
나머지는 에셋을 새로 만들 뿐 기존 값을 덮지 않는다.

---

## ⚪ 못 정함

| | 왜 |
|---|---|
| 창고 3개가 왜 안 잡혔나 | `OwnerPlayerId`가 안 맞아서인지, `EnsureFourPlayers`가 창고보다 먼저 돌아서인지 **씬을 열어봐야 안다** |
| `MISSING_SYSTEMS.md:42`의 마법방어력 항목 | *"일부만 있음 / 보류"*로 적혀 있는데 **2026-09-04에 구현됐다.** 문서가 낡았다 |

---

---

# 🚨 정정 — **`UnitUpgrades`만 배선하면 오히려 나빠진다** (2026-09-04, 넣기 전 실측)

「①만 넣으면 다섯이 살아난다」고 적었는데, **넣었을 때 실제로 무엇이 바뀌는지 재보니 틀렸다.**

## 배선 후에도 **전투 수치는 하나도 안 바뀐다**

특성 효과는 전부 `UnitUpgrades.EffectSum()`을 거치는데, 그게 **`unlockedTraits`를 훑는다.**
그 집합은 **비어 있고, `Unlock(trait)`을 부르는 코드가 하나도 없다**(§2의 (b)).

| | 배선 후 | |
|---|---|---|
| 등급 강화 배율 | `EffectSum(DamageIncrease)` → **0** | ❌ **여전히 1.0** |
| **방깎 · 마방깍** | `EffectSum(ArmorShred)` → **0** | ❌ **여전히 안 걸린다** |

→ **`ArmorShred`는 `UnitUpgrades` 배선으로 안 살아난다.** §2의 특성 시스템까지 풀려야 한다.
   PM이 우려한 *"방깎이 살아나 적 방어력이 깎이기 시작한다"*는 **일어나지 않는다.**

## 🚨 대신 **골드가 나가기 시작한다 — 대가 없이**

### 유닛강화소 12개는 배선하면 **골드만 먹는다**

```csharp
// UnitUpgradeShop.TryUse — 배선되면 여기까지 도달한다
if (!context.GoldWallet.TrySpend(track.CostForLevel(level))) return false;   // 골드 차감
context.UnitUpgrades.LevelUp(track);                                        // 딕셔너리에 +1
```

`LevelUp`이 올리는 `legacyGradeLevels`를 **읽는 코드가 없다.** `UnitAttacker`는 `EffectSum`만 본다.

**그런데 툴팁은 `"다음 레벨: x1.20 — 비용 12000엔"`이라고 약속한다**(`UnitUpgradeShop.cs:79`).

> `UnitUpgradeShop.cs:9`의 주석은 *"실제 배율 적용은 `UnitAttacker`가 `UnitUpgrades.MultiplierFor(grade)`를 [부른다]"*
> 라고 적어 뒀는데, **`MultiplierFor`라는 메서드는 존재하지 않는다.** 주석이 없는 코드를 설명하고 있다.

**지금 안 눌리는 게 차라리 낫다.** 배선하면 사장님이 12,000엔을 내고 아무것도 못 받는다.

### 특성포인트 3종은 **숫자만 오른다**

시작 1개·구매 1개·스토리 1개가 전부 작동하게 되지만, **포인트를 쓸 상점이 없다**(§2 (b)).
구매(15,000엔)는 **골드를 받고 못 쓰는 숫자를 준다.**

## 그래서 순서를 바꿔야 한다

| | 넣을 것 | 왜 |
|---|---|---|
| **1** | **`UnitUpgradeShop`이 올린 레벨을 실제로 읽게 한다** (또는 상점을 잠근다) | 안 하면 골드 싱크만 열린다 |
| **2** | `UnitUpgrades` 배선 | 1 없이 하면 손해다 |
| **3** | 특성 시스템 3중 결락(§2) | **`ArmorShred`가 여기 매달려 있다** |

**①을 단독으로 넣는 건 「한 줄이 다섯을 살린다」가 아니라 「한 줄이 골드 싱크 12개를 연다」이다.**

---

## 우선순위 (제안)

1. **`UnitUpgrades`를 `MapGenerator`가 붙이게 한다** — 한 줄이면 다섯 시스템이 살아난다.
   **동작이 크게 바뀌므로** 구현담당2의 공격력·HP 작업과 겹치면 원인을 못 가린다. **순서를 잡아야 한다**
2. 창고 3개 배선 — 지금은 안 물지만 멀티 켜면 문다
3. Trait 3중 결락 — 셋을 다 채워야 하므로 **설계 결정이 먼저**다
4. 미니맵 — UI라 급하지 않다

---

# 죽은 배선을 「살아 있다」고 말하는 문장 (2026-09-04)

배선 감사의 다음 단계다. **배선이 죽은 걸 찾았으니, 그게 살아 있다고 말하는 안내판을 찾는다.**
그 문장이 남아 있으면 다음 사람이 또 속는다 — **실제로 내가 속았다**(아래 ②).

## 🔴 거짓 — 3건

### ① `UnitUpgradeShop.cs:9` — **존재한 적 없는 메서드를 보증했다** ✅ 고침(`ebd3eab`)

> *"실제 배율 적용은 `UnitAttacker`가 공격 시점에 `UnitUpgrades.MultiplierFor(grade)`를 읽어서 하므로,
> 여기서 살아있는 유닛을 순회할 필요가 없다"*

**`MultiplierFor`는 존재한 적이 없다.** `UnitAttacker`가 보는 건 `EffectSum(unitData, DamageIncrease)`
하나뿐이고, 그건 특성을 훑지 이 상점이 올리는 `legacyGradeLevels`를 안 본다.

이 주석이 *"순회할 필요가 없다"*의 **근거 노릇**을 하고 있었다 — 읽는 사람은 연결돼 있다고 믿는다.

### ② `UnitTraitData.cs:7-8` — **「살아났다」가 거짓이다**

> *"지금 전투에 반영되는 건 `DamageIncrease`와 `ArmorShred` 둘이다.
> `ArmorShred`는 2026-09-03 방어력 시스템이 들어오면서 **살아났다** — `EnemyDummy.AddArmorShred`로 쌓인다."*

**둘 다 도달하지 못한다.** 경로가 두 군데서 끊긴다:

```
UnitAttacker.ApplyArmorShred
  → ResolveUpgrades()          → PlayerContext.unitUpgrades 가 씬에서 null   ← 여기서 끝
  → EffectSum(ArmorShred)      → unlockedTraits 가 비어 있음(Unlock 호출 0)  ← 여기서도 끝
```

**「반영된다」가 아니라 「반영되게 코드는 써 뒀다」가 맞다.**

> 🚨 **내가 이 주석에 속았다.** 배선 감사 1차 보고에서 *"`UnitUpgrades`를 배선하면 방깎이 살아난다"*고
> PM에게 보고했고, PM은 그걸 사장님께 전했다. **재보고 나서야 틀린 걸 알았다.**
> 주석이 안내판 노릇을 한다는 게 이런 뜻이다.

### ③ `ARMOR_SYSTEM_DESIGN.md:417` — 완료 목록에 있는 항목이 도달 못 한다

§8 구현순서 **5번 「`ArmorShred` 특성을 `armorShred`에 연결」**. 코드로는 됐고 **도달은 못 한다.**

## 🟡 예측이 틀린 것 — 2건

`UNIT_STATS_RESEARCH.md:128` · `:411`

> *"방어력 시스템 하나를 만들면 AD/AP와 **방깎** 특성이 같이 살아난다"*

**AD/AP는 살아났고 방깎은 안 살아났다.** 쓸 당시엔 예측이었지만 지금은 답을 안다.

## ⚪ 애매한 것 — 1건

`EVENTS_RESEARCH.md:12` · `EVENTS_RESEARCH_LIST.md:65` — 폐문 **「✅ 구현됨(접근조건까지)」**

접근 조건은 맞다. 다만 원작이 주는 보상인 **특성포인트는 코드에 경로가 아예 없다**
(지급 경로는 시작·구매·스토리 셋뿐이고, 그 셋도 지금 죽어 있다).
*「접근조건까지」*라는 단서 덕에 거짓은 아니지만, **표의 ✅만 보면 오해한다.**

## ✅ 기계로 훑은 결과 — 이 종류는 이게 전부다

| 검사 | 결과 |
|---|---|
| 주석 **1,837줄**에서 `Type.Member` 꼴로 지목한 것 전수 대조 | **코드에 없는 멤버는 `MultiplierFor` 하나뿐** |
| *"부른다 / 읽는다 / 쓴다 / 재사용한다"* 류 주장 **14건** | **호출부가 없는 것도 그 하나뿐** |
| *"살아났다 / 구현됐다 / 반영된다 / 동작한다"* 류 주장 | 실질 **1건**(②), 나머지는 사실 |

**즉 「없는 걸 가리키는 주석」은 드물고, 위험한 건 「있는 걸 가리키지만 그 길이 끊긴」 주석이다.**
기계로는 후자를 못 잡는다 — ②는 지목한 메서드가 **전부 실재한다.** 끊긴 건 그 사이다.

---

# 손으로 따라간 계통 ① 조합 · ② 도박 (2026-09-04)

기계 검사가 못 잡는 것을 찾는다 — **지목한 심볼은 전부 실재하는데 그 사이가 끊긴 경우.**
경로를 끝에서 끝까지 손으로 따라갔다. **고치지 않았다.**

## ✅ 멀쩡함 — 끝까지 이어진다

### 조합 결과 유닛에 데이터가 붙는다

PM이 지목한 자리(*"예전에 `Selectable`이 `Instantiate` 뒤에 데이터를 받아서 색이 안 나온 적이 있다"*)는 **막혀 있다.**

```
UnitSpawner.Spawn(data, …)
  → Instantiate(data.prefab)
  → identity.SetData(data)
       └→ Selectable.RefreshIndicatorColor()      ← 그 사고를 막는 자리. 실제로 불린다
  → attacker.ApplyStats(공격력·사거리·공속)
  → agent.areaMask = ComputeAreaMask(이동능력)     ← 지상 유닛을 바다에 안 놓는다
  → identity.RegisterTo(inventory)                 ← 필드/인벤토리 어긋남 방지
```

`UnitData.prefab`도 **239종 전부 채워져 있다**(`UnitPrefab` 237 · `Unit_안흔함_상붕카` 1 · `Unit_idle` 1).
비어 있으면 `Spawn`이 경고만 찍고 `null`을 돌려주는데, 그런 유닛은 없다.

### 조합식 204개가 씬에 다 걸려 있다

`CombineSystem.recipes`에 **204개** — `Assets/Data/Recipes`의 204개와 일치한다.

### 도박 비용이 양쪽에 다 걸린다

PM이 지목한 `goldCost`가 **`CanRoll`과 `TryRollUnit` 양쪽에 다 있다.**

| | |
|---|---|
| `CanRoll` (`GamblingShop.cs:284`) | 골드와 자원을 **둘 다** 본다 |
| `TryRollUnit` (`:349`) | **골드를 먼저** 뺀다 — 자원부터 빼면 골드가 모자랄 때 자원만 날아간다 |
| 실패 시 | 이미 뺀 골드를 **되돌린다**(`:359`) |

에셋에도 값이 있다 — 하급 250 · 중급 1,500 · 고급 2,500 · 다른세계 3,500엔.
씬의 도박소 4개 전부 `gachaTable`·`unitSpawner`·옵션 목록이 **채워져 있다.**

### 위습이 갈 곳 없이 남지 않는다

`WispData` 7종의 `targetGrade`를 씬의 `UnitPortal` 17개가 받는 등급과 대조했다:

| 위습 | 등급 | 받는 포탈 |
|---|---|---|
| 흔함 선택 | 0 | **9개** |
| 안흔함 | 1 | 1 |
| 특별함 | 2 | 1 |
| 희귀함 | 3 | 1 |
| 전설·히든 | 5 | 1 |
| 백수생활 선택 | 7 | 3 |
| 랜덤유닛 | 10 | 1 |

**갈 곳 없는 위습은 없다.** (PM이 말한 *"레일리+배가 영원히 거절되던"* 종류의 사고는 지금 없다.)

`MainGachaTable`도 등급 0~12 전부 풀이 차 있다(9~43종). `UnitPortal`은 `reward == null`을 막고,
`GamblingShop`은 `HasPool`로 미리 막는다 — **빈 풀에 자원만 날아가지 않는다.**

## 🟡 위험 — 2건

### ① 자원 포탈 5개가 **모든 위습을 받는다**

```csharp
// ResourcePortal.Accepts
return acceptedGrades == null || acceptedGrades.Count == 0 || acceptedGrades.Contains(grade);
```

씬의 `ResourcePortal` **5개 전부 `acceptedGrades`가 비어 있다** → **무엇이든 받는다.**

그리고 `OnTriggerEnter`는 **확률 판정보다 먼저 위습을 소모한다**(원작의 "66% 확률로 목재 1"이 그런
구조라 의도된 것이다). 그래서 **초월위습이나 랜덤유닛 위습을 목재 칸에 잘못 넣으면 목재 1과 맞바꾼다.**

거절 로그도 안 뜬다 — 받아버리니까. **원작이 그런지 확인이 필요하다.**

### ② 조합·아이템·지갑이 **0번 플레이어에만 묶여 있다**

씬의 `CombineSystem`은 하나뿐이고, `inventory`·`itemInventory`·`goldWallet`·`resourceWallet`이
**0번 플레이어의 컴포넌트로 직접 배선**돼 있다. 폴백도 `PlayerContext.Local` 하나다.

지금은 0번만 `occupied: 1`이라 안 문다. **멀티로 레인을 켜면 2·3·4번의 조합이 0번 지갑을 쓴다.**
`WIRING_AUDIT §4`(창고 3개 미연결)와 **같은 뿌리**다 — 1인 기준으로 배선돼 있다.

> `unitSpawner`는 `{fileID: 0}`인데 **폴백이 있다**(`FindFirstObjectByType`). 문제 아니다.

## ⚪ 확인 못 한 것

`ItemInventory`는 씬에 **1개**뿐이다(0번 소유). `ItemData` 에셋은 2개고 조합식이 참조한다.
아이템이 실제로 **들어오는 경로**(획득처)가 있는지는 이번에 안 봤다 — 조합 재료로 쓰이기만 하고
아무도 안 넣어주면 그 조합식들은 영원히 안 된다. **다음에 볼 것.**

## 범위

**조합·도박 두 계통만 봤다. 웨이브·스토리는 안 봤다.**
그리고 이 방식은 **경로를 아는 만큼만 잡는다** — 내가 안 떠올린 경로는 여기에도 안 나온다.
