# 원작 골드 경제 전수 + 우리 대조 (2026-09-06, 리서치담당)

> **한 줄 결론**: 공식·확률·보스 보상·스토리 골드는 **이미 원작과 같다.**
> 어긋난 건 **`GoldPlus`가 `int`라는 것 하나**다 — 원작 증가량이 `+0.60`·`+0.30`·`+0.20`·`+0.20`으로 **전부 소수**라
> **`int`로는 어떤 것도 표현되지 않는다.** 지금은 항상 0이라 드러나지 않지만, 채우는 순간 **10% 과소**가 된다.
>
> 정정표: `GOLD_ECONOMY_CORRECTIONS.csv`

---

## ① 처치 골드 — 원문

```jass
Trig_EnemyDeath2_Actions          ← Player(6)(라운드 몹·라운드 보스) 사망
    if GetRandomInt(1,4)==3 then
        udg_Bounty_Gold = I2R(udg_Gold_Math) * (2 + udg_Gold_Plus[플레이어])
        골드 += R2I(udg_Bounty_Gold)
```

**1/4 확률로만 나온다.** 나머지 3/4는 골드가 0이다.

### `udg_Gold_Math` — 라운드에만 의존, **정수 나눗셈** `[파일확인]`

```jass
udg_Gold_Math = 1 + (2*(udg_Level/5)) + ((3*(udg_Level/6)) - (udg_Level/10))
```

`udg_Gold_Math`는 **`integer`**로 선언돼 있고 `udg_Level`도 정수다 → **JASS의 정수 나눗셈(버림)**이다.
**소수 나눗셈으로 읽으면 값이 통째로 달라진다.**

| 라운드 | 1~4 | 5 | 6~9 | 10~11 | 12~14 | 15 | 20 | 30 | 40 | 50 | 60 | 75 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `Gold_Math` | 1 | 3 | 6 | 7 | 10 | 12 | 16 | 25 | 31 | 40 | 49 | **60** |
| 1회 지급(`Gold_Plus=0`) | 2 | 6 | 12 | 14 | 20 | 24 | 32 | 50 | 62 | 80 | 98 | **120** |

**계단식으로 오른다** — 5·6의 배수마다 뛰고 10의 배수마다 조금 깎인다.

### `udg_Gold_Plus[플레이어]` — **`real`이다** `[파일확인]`

`InitGlobals`에서 0, 증가 경로는 **넷뿐**이다(전수):

| 증가 | 트리거 | 조건 |
|---|---|---|
| **+0.60** | `Trig_UnitJohabCounter` | `GetUnitAbilityLevelSwapped('A04X', 대상)==1` **AND** `udg_Nami_legend_Boolean[플레이어]==false` |
| **+0.30** | `Trig_Forever_Bugi` | 버기 포에버 조합 **AND** `udg_Load_PlayCount[플레이어] >= 10`(누적 클리어 10회) |
| **+0.20** | `Trig_Eternal_Nami` | 나미 이터널 조합 |
| **+0.20** | `Trig_item_up` | 유닛 포인트값 22가 아이템 **`I00Z`** 획득 |

→ **최대 `Gold_Plus = 1.30`**, 즉 배수 `(2 + 1.30) = 3.30` — **최대 1.65배**다.

### 75라운드 누적 기대값

| | 누적 처치 골드 |
|---|---|
| `Gold_Plus = 0` | 약 **36,300엔** |
| `Gold_Plus = 1.30`(최대) | 약 **59,800엔** |

## ② 🔴 우리와 어긋난 것 — **`GoldPlus`가 `int`다**

```csharp
// GoldWallet.cs:13
public int GoldPlus { get; private set; }        // ← 원작은 real
// RewardDistributor.cs:188~189
int goldPlus = context.GoldWallet.GoldPlus;
context.GoldWallet.Add(goldMath * (2 + goldPlus));
```

원작 증가량이 `0.60`·`0.30`·`0.20`·`0.20`으로 **전부 소수**라 **`int`에는 하나도 안 담긴다.**
넷을 다 모아도 `1.30`인데 `int`면 **1**이 되고, 배수가 `3.30` → `3` — **10% 과소**다.

**고칠 곳**: `GoldPlus`를 `float`로 바꾸고, **곱한 뒤 마지막에만 버림**한다(원작 `R2I`가 그 자리다).

```
원작:  R2I( Gold_Math × (2 + Gold_Plus) )      ← 곱한 뒤 한 번만 버림
지금:  goldMath * (2 + (int)goldPlus)           ← 곱하기 전에 버림 → 소수가 사라진다
```

⚠️ **지금은 `GoldPlus`가 항상 0이라 결과가 같다.** 채우는 순간 조용히 틀린다 —
`GoldWallet.cs` 주석이 「조건은 찾아뒀지만 어디서 걸리는지 안 정해졌다」로 남아 있는데, **위 ①에 조건이 다 있다.**

## ③ ✅ 이미 맞는 것

| 항목 | 원작 | 우리 |
|---|---|---|
| `Gold_Math` 공식 | `1 + 2⌊L/5⌋ + 3⌊L/6⌋ − ⌊L/10⌋` | `RewardDistributor.ComputeGoldMath` **정수 나눗셈까지 동일** ✅ |
| 지급 확률 | `GetRandomInt(1,4)==3` | `KillGoldChance = 0.25f` ✅ |
| 보스 보상 R10~60 | 600/1 · 1500/2 · 2500/2 · 3000/3 · 3000/4 · 4000/3 | `BossRewardByRound` **6종 전부 일치** ✅ |
| 스토리 골드 1~13 | 180 · 800 · 1000 · 2000 · 3000 · 4000 · 6000 · 8000 · 9000 · 10000 · 10000 · 10000 · 5000 | `Story01~13.goldReward` **13/13 일치** ✅ |

## ④ 시작 자원 — 원작 원문 `[파일확인]`

게임 시작 루프(생존 플레이어마다):

```jass
call SetPlayerStateBJ(플레이어, PLAYER_STATE_RESOURCE_FOOD_USED, 0)
call AdjustPlayerStateBJ(20, 플레이어, PLAYER_STATE_RESOURCE_GOLD)     ← 골드 +20
call CreateNUnitsAtLoc(1,'h05U', …)                                    ← !#연구소 효과
call CreateNUnitsAtLoc(4,'e0IX', …)                                    ← 랜덤위습 4기
call CreateNUnitsAtLoc(1,'e0HP', …)                                    ← 영웅 뽑기 위습 1기
```

- **위습은 4 + 1 = 5기** → 우리 「위습 5」와 맞는다 ✅
- **골드는 +20**이고, **솔로 플레이면 `AdjustPlayerStateBJ(10, Player(0), GOLD)`가 추가**돼 **30**이 된다.
  → 우리 `startingGold = 30`은 **솔로 기준값**이다. 4인 기준으로는 20이어야 한다 ⚠️
- **`목재`를 주는 코드가 이 루프에 없다** — 우리 「시작 목재 1」의 근거를 이 맵에서 못 찾았다 `[미확인]`.
- ⚠️ **맵 기본 시작 자원(`war3map.w3i`)이 따로 있을 수 있는데 우리 추출본에 `w3i`가 없다** — 총액은 `[미확인]`.
  「+20」은 **그 위에 더해지는 값**이지 시작 총액이라는 근거가 없다.

## ⑤ 골드가 0이 되는 곳도 있다 `[파일확인]`

```jass
udg_PlayerDeath[i]=1 → SetPlayerStateBJ(플레이어, GOLD, 0)      ← 탈락 시 몰수
Trig_Enemy_Boss_create        → SetPlayerStateBJ(…, GOLD, 0)
Trig_Enemy_Boss_sinsekai      → SetPlayerStateBJ(…, GOLD, 0)
```

**보스 라운드 진입 시 골드를 0으로 만든다.** 「보스 전에 다 써라」는 설계다.
**우리에 이 규칙이 있는지 확인 안 했다** `[미확인]` — 있으면 경제 곡선이 완전히 달라진다. **다음 확인 1순위.**

## 조사 범위 공개

- `udg_Gold_Math`·`udg_Gold_Plus`·`udg_Bounty_Gold`의 **선언·대입·참조 전수**를 `war3map.j`에서 훑었다.
- 변수 타입은 전역 선언부에서 직접 확인했다(`integer udg_Gold_Math` / `real array udg_Gold_Plus`) —
  **정수 나눗셈이라는 결론이 여기 걸려 있어서** 이름으로 짐작하지 않았다.
- 우리 쪽은 `GoldWallet.cs` · `RewardDistributor.cs` · `Assets/Data/Stories/*.asset` 13종을 직접 읽었다.
- **안 본 것**: 스토리 **목재** 보상 13종의 우리 값 대조 `[미확인]`. 원작은 1~3번이 목재 0, 4번부터 1·1·3·4·4·5·4·4·3·3이다.
- **안 본 것**: 도박·상점의 골드 소비 쪽 `[미확인]`. 이번 범위는 **수입**이다.
- **안 본 것**: ⑤의 보스 진입 골드 몰수가 우리에 있는지 `[미확인]`.
