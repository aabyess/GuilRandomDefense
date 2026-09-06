# `unique_sell`~`unique_sell6` 6종 전수 — 소유 유닛까지 확정

## 🔴 2026-09-07 추가 — 구현이 자산에 넣은 값 3건 원문 재확인(축약 없이)

### ① `unique_sell5`(`A0BB`, 해적선) — **위습은 1개가 맞다, 지금 자산(2개)이 틀렸다**

`Trig_unique_sell5_Actions` 전문, 한 줄도 안 뺐다:
```jass
function Trig_unique_sell5_Actions takes nothing returns nothing
call KillUnit(GetTriggerUnit())
call AdjustPlayerStateBJ(1,GetOwningPlayer(GetTriggerUnit()),PLAYER_STATE_RESOURCE_LUMBER)
call DisplayTimedTextToForce(...,"1기의 흔함선택위습과 1의 목재 획득!|r")
set udg_T_Location=GetRectCenter(gg_rct_StoryReward_Base1)
call CreateNUnitsAtLoc(1,'e018',GetOwningPlayer(GetTriggerUnit()),udg_T_Location,bj_UNIT_FACING)
call RemoveLocation(udg_T_Location)
endfunction
```
**`CreateNUnitsAtLoc` 호출이 딱 한 번, count 인자도 `1`이다.** 두 번째
호출은 없다. 텍스트도 "1기의"라고 명시한다 — **위습은 정확히 1개다.**
(참고: `e018`="흔함선택위습"(고를 수 있는 흔함 위습), `unique_sell`의
`e0IX`="랜덤위습"과는 다른 아이템이다 — 서술이 "위습"으로 겹쳐 보여서
헷갈렸을 수 있다.)

### ② `unique_sell`(`A09G`, 흔함 9종) — 카운터는 **플레이어 전체 공유**, 3의 배수마다, 판당 리셋 없음

`Trig_unique_sell_Actions` 전문:
```jass
function Trig_unique_sell_Actions takes nothing returns nothing
call KillUnit(GetTriggerUnit())
set udg_Sell_Point1[GetConvertedPlayerId(GetOwningPlayer(GetTriggerUnit()))]=
    (udg_Sell_Point1[GetConvertedPlayerId(GetOwningPlayer(GetTriggerUnit()))]+1)
if(Trig_unique_sell_Func007C())then          // Sell_Point1==3
call DisplayTimedTextToForce(...,"누적 3포인트를 획득하여 1기의 랜덤위습 획득!")
set udg_Sell_Point1[...]=0                    // 3에 도달하면 즉시 0으로 리셋
call CreateNUnitsAtLoc(1,'e0IX',...)          // 랜덤위습(e018 아님)
if(Trig_unique_sell_Func007Func005C())then    // 35% 확률
call AdjustPlayerStateBJ(1,...,LUMBER)
"1개의 추가목재 획득!"
endif
else
call DisplayTimedTextToForce(...,I2S(Sell_Point1)+" 포인트 적립!")
endif
endfunction
```
`udg_Sell_Point1`는 `integer array`로 선언되고 **`GetConvertedPlayerId(...)`
(플레이어 인덱스)로만 색인된다 — 유닛타입 인덱스가 아니다.** 즉
**9명 중 아무나 팔든 같은 카운터에 쌓인다**(치치를 1번, 조로를 2번
팔아도 합쳐서 3번째에 터진다). `Func007C`는 `==3`(정확히 3일 때만),
터지자마자 그 자리에서 `0`으로 리셋 — **3의 배수마다 반복해서
터진다**(1회성이 아님). `InitGlobals`에서 게임 시작 시 0으로 세팅되는
것 말고 **다른 리셋 자리는 없다** — 판 중간에 리셋되는 코드는 없다
(라운드 전환·죽음 등과 무관하게 계속 누적).

### ③ `unique_sell6`(`A080`, 노획물)의 "가끔 100골드" — **정확히 40%, 원문에 있었다**

```jass
function Trig_unique_sell6_Func006Func004C takes nothing returns boolean
if(not(GetRandomPercentageBJ()<=40.00))then
return false
endif
return true
endfunction
```
**원문에 정말 있었다, 축약 때 빠진 거였다** — 어제 표에 "가끔"으로만
적었던 게 실수다. 정확한 구조: 바깥 37%(성공하면 위습1) → **그 안에서
다시 40% 확률로 100골드+목재1 추가**(중첩 확률, 최종 결합확률
0.37×0.40=14.8%).

## 원래 조사분(2026-09-06)
요청: PM — 6종 각각의 능력·보상·소유유닛·등급경계·판매시소멸여부.
`uabi`/`UnitAddAbilityBJ`/`GetSpellAbilityId()==` 세 그물 다 확인.

## 방법

세 그물 다 썼다: ①`Trig_unique_sellN_Conditions`의 `GetSpellAbilityId()==`
로 능력ID 확정(이미 완료) → ②그 능력ID를 `war3map.w3u`의 **모든 유닛의
`uabi`(정적 능력목록)에서 전수 검색**(신규) → ③`UnitAddAbilityBJ`로
동적 부여하는 곳이 따로 있는지도 확인(전부 0건 — 6종 다 정적 `uabi`
소유였다, 동적 부여 없음).

## 결론 — **등급 사다리와 거의 정확히 맞물린다, 예외 하나(`A080`)만 별종**

| 트리거 | 능력 | 보상 | 확정/확률 | 소유 유닛(등급) | 판매시 소멸 |
|---|---|---|---|---|---|
| `unique_sell`(공백) | `A09G` | 판매 3회 누적마다 랜덤위습1(+가끔목재1) | 누적형(3의배수마다 확정) | **흔함(`upoi`1~9) 9종 전부** — `h001`나미~`h009`해군총병(스타팅 로스터 9명) | `KillUnit` 있음 |
| `unique_sell2` | `A0B8` | 성공시 랜덤위습1, 실패시 없음 | **확률 50%**(`GetRandomPercentageBJ()<=50.00`, 원문 확인) | **안흔함(`upoi`=70) 전부** — `h00A`CP9후쿠로 등 다수 | `KillUnit` 있음 |
| `unique_sell3` | `A0BA` | 확정 랜덤위습1(+35% 확률로 목재1 추가) | **위습 확정, 목재는 35%**(`<=35.00`, 원문 확인) | **특별함(`upoi`=80) 대부분** — `h00B`겟코모리아 등 40여종 | `KillUnit` 있음 |
| `unique_sell4` | `A0B9` | 확정 랜덤위습2+목재1 | **확정** | **희귀함(`upoi`=90) 전부** — `h01L`도플라밍고~`h05L`사보 등 50여종(`h05X`레일리 자신은 `A0OE` 별도, 포함 안 됨) | `KillUnit` 있음 |
| `unique_sell5` | `A0BB` | 흔함위습1+목재1+스토리보상유닛(`e018`) | 확정 | **`h060`([히든]해적선)뿐, `upoi`=60** — 등급명 없음(히든 전용 개체) | `KillUnit` 있음 |
| `unique_sell6` | `A080` | 성공시 랜덤위습1(+가끔 100골드+목재1), 실패시 없음 | **확률 37%**(`<=37.00`, 원문 확인) | ⚠️ **등급이 안 맞는다** — `h010`압살롬(특별함,80)·`h056`"노획물품"(80)·`h06G`행운의토큰(`upoi`=0!)·`h07H`"망가진 장난감"(전설적인,80) | `KillUnit` 있음 |

**판매하면 전부 유닛이 사라진다** — 6종 다 함수 첫 줄이 `call
KillUnit(GetTriggerUnit())`이다(원문 그대로, 어제 고대의배와 같은
패턴, 예외 없음).

## 등급 경계 — `upoi`(포인트값) 기준이 맞다, 단 흔함은 다르게 매겨진다

**안흔함(70)·특별함(80)·희귀함(90)은 `upoi`값이 그 등급 전체가 공유하는
고정 상수다** — 어제 확정한 포인트값 사다리(`POINTVALUE_CENSUS.md`)와
정확히 일치, 재확인됨. **흔함만 다르다** — 9명이 상수를 공유하지 않고
**1~9의 개별 순번**을 각자 갖는다(`h001`=3,`h002`=2,`h003`=1...). 즉
"이 9명은 전부 `A09G`를 가졌다"는 **등급이 아니라 이 9명을 개별
지정**한 것으로 보인다 — `upoi` 공식으로 자동 유도되는 게 아니라
사람이 하나하나 붙인 능력일 가능성이 높다.

## 🔴 `A080`("노획물 판매") — 등급이 아니라 별도 카테고리

**등급으로 안 갈린다.** 특별함(`h010`·`h056`)·전설적인(`h07H`)·
비전투아이템급(`h06G`, 행운의토큰 자신, `upoi`=0) 넷이 섞여 있다.
**"노획물"(전리품/드랍류)이라는 이름 그대로, 등급 사다리 밖의 별도
카테고리로 보인다** — PM이 경고한 대로 "이름과 실제가 다를 수 있다"의
사례다. **일반 로스터 등급표로 분류하면 안 되고, 이 4종을 개별
지정으로 다뤄야 한다.**

## 🔴 특이사항 — `h06G`(행운의 토큰) 자신도 "판매" 가능하다

행운의 토큰 자체가 `A080`("노획물 판매")을 갖고 있다 — **토큰을
팔면 토큰이 사라지고 대신 위습(+가끔 골드/목재)을 받을 수 있다는
뜻**이다. 조합재료로 쓸지 팔아치울지 선택지가 있다는 뜻으로 보인다.

## `unique_*` 나머지 3개 — 한 줄씩 재확인(이미 조사됨, 요약만)

- `unique_rerole`(`A0VX`): 희귀함 등급 랜덤유닛을 다른 랜덤 희귀함으로
  교체. 목재2, 판당 플레이어 전역 한도 2~3회.
- `unique_unit_create`(`H0B0` 판매): "고급 유닛 생성" — 확률별 결과
  풀에서 유닛 획득(어제 조사).
- `unique_sell7`(`A0OE`): `h05X`/`h05Y`(레일리 계열) 전용, 위습1+
  특성포인트1(어제 확인, 유일하게 특성포인트를 주는 예외 등급).

## 결론 — 구현 쪽에 넘길 것

**등급 4단계(흔함·안흔함·특별함·희귀함)마다 서로 다른 판매보상
공식이 있고, 전부 원작 로스터 유닛에 이미 배선돼 있다.** 흔함은
누적형(3회마다), 안흔함/노획물은 확률형(성공/실패), 특별함/희귀함은
확정형(+보너스 조건부). **`unique_sell7`(레일리)만 특성포인트까지
주는 예외**이고, 나머지는 위습·골드·목재만 준다. `sellRewardWisp`류
필드는 **유닛 개별이 아니라 등급 단위로 값이 정해지는 구조**이므로
(흔함 9명·노획물 4종 예외 제외) 등급별 상수 하나씩으로 구현하는 게
원작에 더 가깝다.
