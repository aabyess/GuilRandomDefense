# 소소한 조사 2건 — `h05Y` 고대의 배 획득 경로 · 버프 출처 6건

조사: 리서치담당 / 2026-09-06

---

## ① `h05Y` 고대의 배 — 어떻게 얻는가

### 1. **아이템 `I00S`와 유닛 `h05Y`는 다른 것이다. 아이템을 얻으면 유닛이 나온다.**

| | ID | 이름 | 성격 |
|---|---|---|---|
| 아이템 | `I00S` | 「고대의 배 - 희귀함」 | `iusa=1`(1회 사용) · `icla=Campaign`(버릴 수 없음) · `ilev=5` · `iabi=A171` · 툴팁 「◎고대의배를 지급받습니다.」 |
| 유닛 | `h05Y` | 「고대의 배 특수 - 희귀함」 | `uabi = A023,A0OD,A0OC,A0OE,Avul` |

```jass
Trig_item_up2_Func002C:  GetItemTypeId(GetManipulatedItem()) == 'I00S'
Trig_item_up2_Actions:   call CreateNUnitsAtLoc(1,'h05Y', 소유자, udg_Mix_Loction, ...)
                         call TriggerRegisterUnitEvent(gg_trg_Acient_Ship, 생성유닛, EVENT_UNIT_SPELL_EFFECT)
                         "<유닛이름> 획득 ! " 메시지
```
→ **아이템은 「지급 티켓」이고 유닛이 실물이다.** 생성 직후 `gg_trg_Acient_Ship`에 스펠 이벤트로 등록된다
(그래서 `Trig_Acient_Ship_Conditions`가 `GetUnitTypeId(GetTriggerUnit())=='h05Y'`로 걸린다).

### 2. `h05Y`를 **플레이어에게 주는 트리거는 2개**다

| # | 트리거 | 조건 |
|---|---|---|
| 1 | `Trig_item_up2_Actions` | 아이템 `I00S` 획득/사용 |
| 2 | **`Trig_Story_reward7_Actions`** | **스토리 7 클리어 + 그 플레이어의 데스카운트 0** |

그 밖의 `'h05Y'` 등장 2곳은 `CreateUnitsForPlayer7` — **Player(7) 중립 배치**다.
좌표 `(8609, -5769)`와 `(5631, -8948)`. 플레이어 지급 경로가 아니다(해적단 3기와 같은 전시용 성격).

### 3. **스토리 9(어인섬)가 아니라 스토리 7(임펠다운)이다**

`Trig_Story_reward7_Actions`의 안내문(원문 그대로):
> 「|cffFF0000**임펠다운**|r|cffff8200까지의 스토리 진행완료 모든플레이어에게|r|cffb22222**고대의 배**,|r
> |cff00ff00**흔함유닛**|r|cffff8200**선택 위습 1기**와|r|cffffd700**6000골드**와|r |cff20b2aa**나무 4개**|r|cffff8200를 지급합니다.|r」

- 연결: `udg_StoryTrigger[7] = gg_trg_Story_reward7` (`Trig_BaseORD_Actions`)
- 실행: `ConditionalTriggerExecute(udg_StoryTrigger[udg_Story_Count])` (`Trig_Story2_Actions`)
- **지급 조건: `udg_PlayerDeath[플레이어] == 0`** — 데스카운트가 1이라도 있으면 못 받는다
- 지급 내용(플레이어 1~4 루프):
  - 골드 **+6000** · 나무 **+4**
  - **`h05Y` 1기** (+ `gg_trg_Acient_Ship` 등록)
  - `e018` 1기 = 「흔함유닛 선택 위습」, `gg_rct_StoryReward_Base1` 중앙에 생성
  - 추가: `udg_Tech_Tree[플레이어]`가 아이템 `I00L`을 **안 들고 있으면** `GetRandomInt(1,20)==5`(**5%**)로
    `e0R9` 소환 + `I00L`을 기술트리에 지급 + 아이템풀에서 `I00L` 제거

> ⚠️ **우리 스토리 번호는 원작과 별개**다(메모리 `story-numbering-is-ours`). 대응은 **「임펠다운」이라는 내용**으로 잡아야 한다.

### 4. `I00S`가 아이템 풀에서 나올 확률

`Trig_ItemPooL_Actions`가 `itpool[0..10]`을 만든다.

| 풀 | 항목 수 | 가중치 합 | `I00S` 가중치 | 확률 |
|---|---|---|---|---|
| `itpool[0..4]` (플레이어 풀) | 22 | 35.85 | 1.45 | **4.04%** |
| `itpool[5..10]` | 13 | 11.80 | 0.80 | **6.78%** |

`ItemPoolRemoveItemType`으로 뽑힌 1회성 아이템이 풀에서 빠지므로, 실제 확률은 진행에 따라 올라간다.

---

## ② 버프 출처 6건 — **누가 거는가**

전부 **더미 유닛이 `Ablo`(블러드러스트)를 시전**하는 방식이다. 능력 ID로는 안 잡히고
(`war3map.j`에 그 능력 ID가 0회 등장), **오더 스트링 `"bloodlust"`**로 걸린다.

| 버프 | 우리 유닛 | 시전 더미 | 능력 | 부모 트리거 | **게이트** | 효과 |
|---|---|---|---|---|---|---|
| `B00D` | 이현주 | `e01K` 「공격시 7% 확률로 발동」 | `A085` (`Ablo`) 「4지정 핸콕피스톨키스」 | `Trig_Legend_han_kiss_Actions` stage 0 | 부모 트리거 진입 조건 | `Blo1=+0.47` 지속 **0.4초** |
| `B03M` | 김영원 | `e0E3` 「E영원 핸콕 버프 더미」 | `A0OP` (`Ablo`) | `Trig_Hancock_Attack_Actions` (**평타**) | **`GetRandomInt(1,15)==6` = 6.67%** | `Blo1=**-0.15**` 지속 **0.7초** |
| | | (본체 `h05C`) | `A0RO` (`Absk`) 「매료매료열매-영원 액티브」 | 플레이어 시전 | 액티브 | |
| `B03Z` | 양재모 | `e00C` 「@초월 로우룸2」 | `A09H` (`ANbr`) 「!4범위 로우 룸」 | `Trig_Law_Attack_Actions` (**평타**) | **`UNIT_STATE_MANA == 135.00`** (평타마다 +1 누적, 도달 시 0 리셋) | 피해 30,000 · 지속 **5초** |
| `B045` | 김민규 | `e0FA` 「%제한됨변화 카타쿠리 버프」 | `A0R7` (`Ablo`) | `Trig_katakuriAttack_Actions` (**평타**) | **`GetRandomInt(1,7)==4` AND `LIFE > 36.00`** → 발동 시 **체력 −17** | `Blo1=+4.0` 지속 **1.25초** |
| `B05N` | 미도리야 | `e0MO` 「히그마 버프 더미」 | `A0ZH` (`Ablo`) | `Trig_Higma_Attack_Actions` (**평타**) | **`GetRandomInt(1,12)==4` = 8.33%** | `Blo1=+4.0` 지속 **1.15초** |
| | | (본체 `h04Z`·`h07M`·`h0AD`·`h08O`·`h09Q`) | `A0ZI`·`A172` (`AEme` 변신) | — | 변신 능력 | |
| `B078` | 이지원 | `e0QT` 「E영원 니카 버프더미」 | `A15E` (`Ablo`) | `Trig_Nika_Attack_Actions` (**평타**) | **`UNIT_STATE_LIFE == 115.00`** (평타마다 +1, 도달 시 1 리셋) | `Blo1=**-2.25**` 지속 **4.25초** |

`Blo1`은 `Ablo`(블러드러스트)의 **공격속도/공격력 증가율**이다. 음수(`-0.15`, `-2.25`)인 것은
버프를 **플래그로만** 쓰고 실제 효과는 다른 트리거가 `UnitHasBuffBJ`로 읽는 구조로 보인다.

### 이 6건에서 나온 일반 패턴 3가지

1. **버프를 거는 주체는 항상 더미다.** 본체는 버프를 안 건다(액티브 `A0RO`·변신 `AEme`만 예외).
2. **게이트가 두 종류다** — 확률형(`GetRandomInt`)과 **게이지형**(`UNIT_STATE_MANA`/`LIFE`를 평타마다 +1씩 쌓아 임계값에서 리셋).
   게이지형은 **실질 발동 주기 = 임계값 × 평타 간격**이다. 확률로 환산하면 틀린다.
3. `B03Z`(로우)는 **버프가 게이트이기도 하다** — `Trig_Law_Attack_Actions`가
   `UnitHasBuffBJ(GetAttacker(),'B03Z') AND LIFE >= 250.00`이면 체력 1로 만들고 **버프를 제거하면서** `e0RR`을 소환한다.
   **거는 곳과 쓰는 곳이 같은 트리거 안에 있다.**

### 미확인

- `B03Z`는 `ANbr`(화염숨결, `atar='invulnerable,sapper,ground,player'`)의 `abuf`다. 통상 적에게 붙는 자리인데
  `Trig_Law_Attack_Actions`는 **시전자(`GetAttacker()`)에게서** 읽는다. `aare=1.0`(자기 발밑)이라 자기에게 붙는 것으로 보이나
  단정하지 않는다. `[미확인]`
