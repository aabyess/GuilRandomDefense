# ⑤ 불리언 플래그 채널 — 전수 조사

조사: 리서치담당 / 2026-09-06
요청: PM — 「채널 하나를 통째로 못 보고 있었다면 특성 8건 말고 더 있을 수 있다. 전수해달라」

## 결론

- **원작의 전역 boolean은 63개.**
- 그중 **특성 트리거(`T_Ability_hero`)가 켜는 것은 정확히 5개**다. 8건이 아니다.
  이미 보고한 2개(`udg_Law_Bool`·`udg_NamiT_Bool`) 외에 **3개가 더 있다.**
- 조합/아이템 트리거가 켜고 다른 트리거를 분기시키는 것이 **20개** 더 있다.
  그중 **평타 트리거를 직접 분기시키는 것이 2개**(흰수염·시키)다.
- 딸린 문제로 **더미 채널 표가 77기 빠져 있다** (§3).

방법: `war3map.j` 전체에서 `boolean (array )?udg_*` 선언을 뽑고, 각 변수의 **쓰기 지점**(`set X…=`)과
**읽기 지점**을 함수 경계로 귀속시켜 분류했다. `InitGlobals`(초기화 false)는 제외했다.

---

## 1. 특성이 켜는 불리언 — **5개 (전수)**

| 불리언 | 켜는 곳 | 읽는 곳 | 성격 | 이미 보고? |
|---|---|---|---|---|
| `udg_Law_Bool` | `Trig_T_Ability_hero_Actions` | `Trig_Law_Attack_Actions` (**평타**) | 스킬2를 강화판으로 교체 | ✅ (A10S) |
| `udg_NamiT_Bool` | `Trig_T_Ability_hero_Actions` | `Trig_Nami_Skill_1_Actions`, `Trig_Nami_Skill_4_Actions` | 스킬에 추가 블록 | ✅ (A107) |
| **`udg_bool_franky_tr`** | `Trig_T_Ability_hero_Actions` | `Trig_Franky_Skill_1_Actions` | 스킬1 통째 교체 | 🆕 |
| **`udg_bool_kid_tr`** | `Trig_T_Ability_hero_Actions` | `Trig_Kid_Attack_Actions` (**평타**) | 마나 스킬을 강화판으로 교체 | 🆕 |
| **`udg_bool_lucchi_tr`** | `Trig_T_Ability_hero_Actions` | `Trig_Luchi_Attack_Actions` (**평타**) | 킥을 강화판으로 교체 | 🆕 |

### 새로 나온 3건의 실제 값

**① `udg_bool_franky_tr` — 프랑키 스킬1 (`Trig_Franky_Skill_1_Actions`)**

한 트리거 안에서 if/else로 갈린다. **게이트는 같고 값만 다르다.**

| | 더미 | 범위 | RRD |
|---|---|---|---|
| 특성 ON | `e06I` (TimeScale 2.15, 투명도 35) | **500** | `250,000` × 0.80~1.50 · NORMAL+UNIVERSAL |
| 특성 OFF | `e0KC` (TimeScale 3.75) | **400** | `200,000` × 0.80~1.50 · NORMAL+UNIVERSAL |

**② `udg_bool_kid_tr` — 키드 (`Trig_Kid_Attack_Actions`)**

게이트: 평타로 쌓은 `UNIT_STATE_LIFE == 50.00` (체력을 마나 게이지로 쓰는 방식, 도달 시 1로 리셋).
그다음 특성 유무로 실행 트리거가 갈린다.

| | 트리거 | RRD |
|---|---|---|
| 특성 ON | `gg_trg_Kid_Skill_Mana_Tr` | `(400,000 + realD) × (1 + realE×0.01)` — **3회** (Func024/047/060), 배수 1.00 · NORMAL+UNIVERSAL. 더미 `e0N3`·`e0N0` |
| 특성 OFF | `gg_trg_Kid_Skill_Mana` | `(1,000,000 + realD) × (1 + realE×0.01)` — **1회**, 배수 1.00. 더미 `e0N0`·`e09P`·`e0N1`·`e0N3`·`e0N2` |

→ **합계 1,200,000 vs 1,000,000.** 특성은 「한 방」을 「세 방」으로 쪼개고 총량을 20% 올린다.

**③ `udg_bool_lucchi_tr` — 루치 (`Trig_Luchi_Attack_Actions`)**

게이트: `GetRandomReal(1,100) <= 7.25 + 0.01 × (udg_Load_PlayCount[플레이어] / 250)` — **누적 플레이 횟수로 확률이 오른다.**
(기본 7.25%. 세이브 데이터 축이라 우리에 대응이 없다.)

| | 트리거 | 내용 |
|---|---|---|
| 특성 ON | `gg_trg_Luchi_Skill_1_Kick2` | RRD `400,000`×2회 (MAGIC+UNIVERSAL) + **최대체력 4%** (CHAOS+UNIVERSAL) + `h09O` **6기** 소환 |
| 특성 OFF | `gg_trg_Luchi_Skill_1_Kick` | **RRD 0건** — 더미 `e0F3`·`e0F4`·`e0FF` 소환만 (더미 채널로 피해) |

---

## 2. 조합·아이템이 켜는 불리언 — 20개

**평타 트리거를 직접 분기시키는 것 2개:**

| 불리언 | 켜는 곳 | 읽는 곳 | 게이트 | 내용 |
|---|---|---|---|---|
| `udg_item_edward_bool` | `Trig_item_up_Actions` (아이템) | `Trig_Ed_Attack_Actions` (**평타**) | `GetRandomInt(1,22)==6` | `Ed_Skill_1_Item` vs `Ed_Skill_1`. **RRD 수식은 동일** — `(1,750,000 + 대상최대체력×0.03) × (1.50 + 0.025×R01V연구수)`. 차이는 소환체 `e0QY` 1기 추가뿐 |
| `udg_item_siki_bool` | `Trig_item_up_Actions`, `Trig_sell_ship_Actions` | `Trig_Shiki_Attack_Actions` (**평타**) | `GetRandomInt(1,16)==3` → 그 안에서 다시 `GetRandomInt(1,2)==1` | `gg_trg_Shiki_SKill_item` 추가 실행. RRD `250,000` + 더미 `e0QV`·`e027`·`e0QW` |

**나머지 18개** (피해 분기 아님 — 조합 가능 여부·외형·추가 연출):

| 불리언 | 역할 |
|---|---|
| `udg_Tech_Onedill` | 조합 46곳에서 켜고 46곳의 `Conditions`에서 읽는다. **등급 해금 플래그**. 피해 무관 |
| `udg_Nika_Johab_Bool` · `udg_Nika_Item_Bool` · `udg_lucho_ch1/ch2` | 니카(루피) 조합 가능 조건 |
| `udg_aocho`/`aocho2` · `udg_chocho`/`chocho2` · `udg_zocho1`/`zocho2` · `udg_sancho1`/`sancho2` | 아오키지·쵸파·조로·상디의 **특성 분기 조건**(`T_Ability_hero_Func0xxC`) |
| `udg_Item_zoro_enma` · `udg_Uta_item_bool` · `udg_aohidden_bool` | 히든/조합 조건 |
| `udg_item_Bustercall_bool` | `Trig_Absolb1_Func005Func005C` (흡수 조건) |
| **`udg_item_slow_bool`** | 🔴 **읽는 곳이 0건 — 죽은 플래그다.** 켜기만 하고 아무도 안 본다 |

> 📌 `udg_Name_Boolean`은 쓰기 237회·읽기 2회지만 **유닛 이름 표시용**이라 피해와 무관하다.

---

## 3. 딸린 문제 — 더미 채널 표가 **77기** 빠져 있다

PM이 「더미 채널 52행은 완전하지 않다」고 받아들인 판정의 규모를 실측했다.

**방법**: `war3map.j`에서 `CreateNUnitsAtLoc` / `CreateNUnitsAtLocFacingLocBJ` / `CreateUnit`로
생성되는 유닛 타입 **1,162종**을 뽑고, 그중 **더미 서명**(`uabi`에 `Aloc` 보유 또는 base가 `ewsp`)을 가진 것에서
**피해 필드가 확정된 능력**만 남겼다.

| | 수 |
|---|---|
| 트리거가 생성하는 유닛 타입 | 1,162 |
| 그중 더미(`Aloc`/`ewsp`) + 피해 확정 능력 보유 | **95기** |
| 그중 `DUMMY_CHANNEL_GATES.csv`에 **없는 것** | **77기 (97행 중 79행)** |

전체 목록: **`Docs/reference/DUMMY_CHANNEL_MISSING.csv`** (더미ID · 이름 · 능력 · 피해필드 · 값 · 범위 · 부모 트리거)

**왜 빠졌나**: 기존 스캔도 유닛 `uabi`에서 출발했다. 특성·조합·아이템으로 런타임에 붙는 능력이
부르는 더미, 그리고 다른 더미가 부르는 2차 더미가 그물에 안 걸린다. **특성 8건과 같은 원인이다.**

**피해 확정으로 인정한 필드만 넣었다** (필드 의미가 확실한 것):
`Htc1`(천둥소리) · `Ucs1`(시체폭발/죽음의사슬) · `Hbz2`(블리자드 파동당) · `Ocl1`(연쇄번개) ·
`Nbr1`(화염숨결) · `Ncs1`(클러스터로켓) · `Hbh3`(배시 추가피해) 등 13종.

**의도적으로 제외한 것** — 값은 있으나 피해가 아니다. 넣었으면 오염됐을 것들:
- `Owk1` (0.90) = 윈드워크 **이동속도 배율**. 13기에서 나왔다
- `Oeq1` (0.15~0.20) = 지진 계열 **감속/건물피해 배율**
- `Nst1` (4·7) = 정수 카운트
- `Isx1`·`Idam` = 소환체가 든 **아이템 공격력/공속 보너스** — 그 소환체의 평타로 나가는 것이라 더미 채널이 아니다
  (`h05J` 레일리 소환체 `Idam=100,000`, `h0BH`/`h0BJ` 시키 부유물 `Idam=52,500` 등)

큰 값 몇 개 (전부 미기재였다):
`e056`·`e0GH`·`e0GK` Z 대구경/차지 블래스터 **1,000,000** · `e08E`·`e08Q`·`e0PP`~`e0PR` 미호크 먼지 **750,000** ×5 ·
`e0F4`~`e0FF` 루치 육왕권 **600,000** ×5 · `e0MY` 키드 **600,000** · `e09N`·`e0ML` 아오키지 아이스볼 **500,000** ·
`e09R` 키자루 레이저 **500,000** · `e0KH` 징베 **500,000** · `e09G`·`e09H`·`e09I`·`e0IZ` 나미 블리자드 계열 **500,000**

---

## 남은 것

- `DUMMY_CHANNEL_MISSING.csv` 77기의 **진입 게이트**는 아직 안 풀었다(부모 트리거 이름만 넣었다).
  기존 52행처럼 조건 스택을 펴려면 별도 작업이 필요하다.
- 피해 필드 의미가 미확정인 능력들(위 「제외」 목록 밖의 소수)은 넣지 않았다. `[미확인]`
