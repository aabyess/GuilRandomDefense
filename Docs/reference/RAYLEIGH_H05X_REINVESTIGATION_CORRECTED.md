# `h05X`(레일리 희귀함) 재조사 — 🔴 이전 보고 정정, 사장님 말씀이 맞았다

조사: 리서치담당 / 2026-09-06
요청: PM — 사장님이 "레일리는 스토리 깨면 주거나 고급도박에서 확률로
나오는 것 아니냐"고 직접 반박, `MapGenerator` 배선 되돌림(`6fe3193`).
「원문 줄 그대로, 판정은 안 한다」는 요청대로 원문만 그대로 옮긴다.

## 🔴 정정 — `CreateBuildingsForPlayerN`이 아니었다

이전 보고: "`h05X`가 `CreateBuildingsForPlayer1`/`CreateBuildingsForPlayer3`에서
게임 시작 시 배치된다." **이 문장 자체가 틀렸다** — 그 두 함수 검색은
실제로는 `H0C4`(항해일지) 배치를 확인하던 자리였고, `h05X`는 거기 없다.
**실제로는 `CreateUnitsForPlayer7`(전혀 다른 함수)에서 2회 나온다:**

```
function CreateUnitsForPlayer7 takes nothing returns nothing
local player p=Player(7)
...
set u=CreateUnit(p,'h05X',-3418.1,-6737.3,270.000)   ← 1번째
...
set u=CreateUnit(p,'h05X',6562.1,-7284.8,274.845)    ← 2번째
```

그리고 **`Player(7)`은 사람 플레이어가 아니다** — 원문에서 직접 확인:
```
call SetPlayerController(Player(7),MAP_CONTROL_NEUTRAL)
call SetPlayerTeam(Player(7),1)
```
(참고로 실제 4명 플레이어는 `Player(0)~Player(3)`, `CreateBuildingsForPlayer0~3`/
`CreateUnitsForPlayer0~3`가 그들 몫이다 — `H0C4`는 이쪽에서 확인된 게 맞다.)

**즉 이 2개 인스턴스는 중립 소유로 맵 어딘가에 놓인 것이지, 플레이어가
게임 시작부터 손에 쥐고 있는 게 아니다.** 이걸 "플레이어가 시작부터
갖고 있다"로 잘못 결론 낸 게 이전 보고의 핵심 오류다. 이 중립 배치가
정확히 어떤 용도인지(전리품 방어 유닛인지, 장식인지)는 이번에도
확정 못했다 — 아래 진짜 획득 경로들과는 별개 문제로 남긴다.

## 🔴 `A0OD` 재조사 — 접근불가가 아니었다, 검색 방법이 못 잡았다

**원인 확정: `A0OD`는 `uabi`(정적 능력목록)나 `UnitAddAbilityBJ`(동적
부여)로는 안 걸린다 — 조건함수 안의 `GetSpellAbilityId()=='A0OD'` 형태로만
나온다.** 어제 이 형태를 검색 안 했다.

```
function Trig_Acient_Ship_Func002C takes nothing returns boolean
if(not(GetSpellAbilityId()=='A0OD'))then
return false
endif
return true
endfunction
```

**`A0OD`를 실제로 갖고 있는 유닛도 찾았다** — `war3map.w3u`에서
`uabi=A023,A0OD,A0OC,A0OE,Avul`인 오브젝트의 `unam`을 직접 확인:

```
unam = |cff4682b4고대의 배|r |cffb22222특수|r - |cffff00ff희귀함|r
```

**이 유닛의 오브젝트ID가 `h05Y`다** — `Trig_Acient_Ship_Conditions`가
`GetUnitTypeId(GetTriggerUnit())=='h05Y'`로 게이트하는 걸로 교차 확인.
**`h05Y`="고대의 배"가 `A0OD`("레일리 도박")를 포함해 3개의 도박 능력을
갖고 있는 그 유닛이다.**

## 🔴 2026-09-07 정정 — 아래 발췌가 `RemoveUnit` 줄을 빠뜨렸다

아래 코드 블록은 요약하면서 `call RemoveUnit(GetTriggerUnit())` 줄들을
빼고 옮겼다 — **실제로는 세 분기(A0OC/A0OD/A023) 전부, 성공·실패
가리지 않고 `RemoveUnit(GetTriggerUnit())`(=`h05Y` 자기 자신 소모)가
있다.** 즉 **고대의 배는 도박 1회마다 소모된다** — "목재만 있으면
무한 도박"이 아니다. 전문·경제적 결론은
[[acient-ship-consume-confirmed]](ACIENT_SHIP_CONSUME_CONFIRMED.md) 참고.

## `Trig_Acient_Ship_Actions` — "고대의 배" 유닛이 갖는 3가지 도박, 원문 그대로(⚠️ 요약본, 위 정정 참고)

```jass
if(Trig_Acient_Ship_Func001C())then   // GetSpellAbilityId()=='A0OC' — "다른세계 유닛 도박"
  ... 목재7 소모, 성공시 udg_Modelpack_R_unit에서 랜덤, 실패시 h06G(위로품) ...

if(Trig_Acient_Ship_Func002C())then   // GetSpellAbilityId()=='A0OD' — "레일리 도박"
  if(Func001Func004C())then   // GetRandomInt(1,100)<=25  ← 25% 확률
    call CreateNUnitsAtLoc(1,'h05X',GetOwningPlayer(GetTriggerUnit()),udg_Mix_Loction,bj_UNIT_FACING)
    "h05X 획득 !"
  else
    "님이 레일리 도박을 실패 하셨습니다!"
  endif
  call SetPlayerStateBJ(...,PLAYER_STATE_RESOURCE_LUMBER,(...)-7))   // 목재 7 소모

if(Trig_Acient_Ship_Func003C())then   // GetSpellAbilityId()=='A023' — "해적선 도박"
  ... 목재4 소모, 성공시 h060 ...
```

**"레일리 도박" = 목재7 소모, 확률 정확히 25%(원문 `GetRandomInt(1,100)<=25`),
성공시 `h05X` 1기 획득.** 기존 `UPGRADE_SHOP.md`의 "27% 확률"은 근사였고
실측은 25%다.

## `h05Y`("고대의 배") 획득 경로 — 원문 그대로

**① 스토리 보상(임펠다운) — 전원 확정 지급:**
```jass
function Trig_Story_reward7_Actions ...
"임펠다운까지의 스토리 진행완료 모든플레이어에게 고대의 배, 흔함유닛
선택위습 1기와 6000골드와 나무 4개를 지급합니다."
...loop 4명...
call CreateNUnitsAtLoc(1,'h05Y',ConvertedPlayer(GetForLoopIndexA()),udg_Mix_Loction,bj_UNIT_FACING)
call TriggerRegisterUnitEvent(gg_trg_Acient_Ship,GetLastCreatedUnit(),EVENT_UNIT_SPELL_EFFECT)
```

**② 아이템 `I00S` 사용으로도 생성:**
```jass
function Trig_item_up2_Func002C ... GetItemTypeId(GetManipulatedItem())=='I00S' ...
function Trig_item_up2_Actions ...
call CreateNUnitsAtLoc(1,'h05Y',GetOwningPlayer(GetTriggerUnit()),udg_Mix_Loction,bj_UNIT_FACING)
call TriggerRegisterUnitEvent(gg_trg_Acient_Ship,GetLastCreatedUnit(),EVENT_UNIT_SPELL_EFFECT)
```

## `h05X`(레일리 자신) 직접 지급 경로 — 원문 그대로 (도박 말고 확정 지급도 있다)

**③ 스토리 보상(마린포드) — 조건부 추가 지급:**
```jass
function Trig_Story_reward8_Actions ...
"마린포드까지의 스토리 진행완료 모든플레이어에게 8000골드와 나무4개
희귀함(특수함) 랜덤 2기를 지급합니다."
...
if(Trig_Story_reward8_Func002Func001Func008C())then
"테크 추가효과: [히든]실버즈 레일리 세계의 진실을 아는 자 - 희귀함 추가 획득!."
call CreateNUnitsAtLoc(1,'h05X',ConvertedPlayer(GetForLoopIndexA()),...)
```
(조건 `Func008C`가 정확히 뭘 요구하는지는 이번엔 안 팠다 — 아마 특정
테크/선택 여부, `[미확인]`으로 남긴다.)

**④ `Trig_Story_Tier5_rayleigh_Actions`** — 별도 소형 트리거, 조건 없이
`h05X`+`h060` 동시 지급(퀘스트/아이템 트리거로 보이나 진입 조건은 이번에
안 팠다):
```jass
call RemoveUnit(GetTriggerUnit())
call CreateNUnitsAtLoc(1,'h05X',GetOwningPlayer(GetTriggerUnit()),...)
call CreateNUnitsAtLoc(1,'h060',GetOwningPlayer(GetTriggerUnit()),...)
```

**⑤ `Trig_Unit_Gemble_3_Actions`("고급유닛 도박") — 유닛판매/훈련 기반 도박에도 `h05X`가 있다:**
```jass
function Trig_Unit_Gemble_3_Actions ...
call RemoveUnit(GetTrainedUnit())
if(Trig_Unit_Gemble_3_Func007C())then
  ... 실패: h06G(위로품) ...
else
  if(Trig_Unit_Gemble_3_Func007Func005C())then
    call CreateNUnitsAtLoc(1,'h05X',GetOwningPlayer(GetTrainedUnit()),...)
    "고급유닛 도박으로 h05X 획득 !"
  else
    ... udg_unit_dobakGroup3에서 랜덤 ...
```

**⑥ `Trig_unique_unit_create_Actions`("고급 유닛 생성", `I00A`와 이름이
같다) — 유닛 `H0B0` 판매로 발동:**
```jass
function Trig_unique_unit_create_Conditions ... GetSoldUnit()=='H0B0' ...
function Trig_unique_unit_create_Actions ...
if(Trig_unique_unit_create_Func007C())then
  call CreateNUnitsAtLoc(1,'h05X',GetOwningPlayer(GetTriggerUnit()),...)
  "고급유닛 생성으로 h05X 획득 !"
else
  ... 실패시 다른 랜덤 풀 ...
```
⚠️ **이건 아이템 `I00A`(어제 "게이트 없음"으로 닫은 그 아이템)와 이름이
정확히 같다** — `I00A` 자체는 문자열 검색 0건이었지만, **같은 이름의
유닛판매(`H0B0`) 메커니즘이 따로 있었다.** 아이템 `I00A`와 유닛 `H0B0`가
같은 걸 가리키는지, 서로 다른 두 개의 "고급 유닛 생성"인지는 이번엔
확정 못했다 — `[미확인]`.

## 🔴 추가 — 사장님 "초월/불멸 재료" 발언 확인 + `Player(7)` 배치의 진짜 용도

### `h05X`가 실제로 조합 재료다 — `war3map.w3a` 원문

```
acat = h05X,h028,h018
alig = 1,1,1
atat = h03A
aub1 = |cffFF8200레일리+ 센토마루+ 징베|r = |cff2f4f4f'로저해적단 부선장' 레일리|r
       목재 5필요
```
**레일리(h05X,희귀함)+센토마루(h028)+징베(h018) → '로저해적단 부선장' 레일리(h03A,전설적인), 목재5.**
w3a 전체에서 `h05X`는 정확히 이 자리 1건뿐이고, `h03A`도 이 결과값
1건뿐(더 위 등급으로 또 조합되는 자리는 w3a엔 없다).

**게다가 `h05X`는 "히든조합" 2건의 재료로도 쓰인다** —
`Trig_Hidden_Tiger_Func001Func004...`/`Trig_Hidden_carrot_Func013...`가
둘 다 `GetUnitTypeId(GetFilterUnit())=='h05X'`로 확인(맵 전체 스캔,
소유자 무관). **즉 `h05X`는 팔거나(`A0OE`), 전설적인으로 조합하거나,
다른 히든조합 재료로 쓰거나 — 최소 3갈래 용도가 있다.** 사장님의
"초월·불멸 재료"는 "히든조합"(피셔타이거·캐럿 등)을 가리킨 것으로
보인다 — 이 결과유닛들 자체가 등급표시는 안 갖고 있지만("[히든조합]"
접두만, `그레이드` 문구 없음) 정규 등급 사다리 밖의 "히든" 전용 트랙이다.

### `CreateUnitsForPlayer7`은 "플레이어 지급"이 아니라 "히든조합 재료를 맵에 뿌려두는 자리"였다

**결정적 교차확인**: `Trig_Hidden_Aokiji`가 요구하는 3종
(`h02B`쿠잔·`h02E`죠즈·`h01Y`모몬가)이 **전부 `CreateUnitsForPlayer7` 안에도
있다**(직접 확인, `True`/`True`/`True`). `h04S`·`h060`도 마찬가지다.

**두 사실이 동시에 참인 이유**: `CreateUnitsForPlayer7`(`Player(7)`,
`MAP_CONTROL_NEUTRAL`)은 **모든 히든조합 레시피가 요구하는 "맵 어딘가에
있어야 할 재료 유닛"들을 게임 시작 시 미리 중립 소유로 뿌려두는 자리다**
— 히든조합 판정 자체가 `GetUnitsInRectMatching(GetPlayableMapRect(),...)`
(맵 전체, 소유자 무관)이므로, **플레이어가 이 유닛을 소유할 필요가 없다.
그냥 맵 어딘가에 존재하기만 하면 챗코드를 친 아무 플레이어의 조합이
성립하고, 그 유닛은 `RemoveUnit(GroupPickRandomUnit(...))`으로 소모된다.**
`h05X`도 이 "재료 풀"의 일부로 뿌려진 것이지, 플레이어에게 준 게 아니다.

## 결론 — 원문만 나열, 판정은 안 한다

- `h05X`는 `Player(7)`(중립) 소유로 맵에 2개 배치돼 있다 — **이게 "플레이어가
  게임 시작부터 갖고 있다"를 뜻하지는 않는다** (중립 소유라 플레이어가
  직접 `A0OE`를 캐스트할 수 없다, 소유권 이전 경로는 확인 못했다).
- 확정 지급: 스토리 보상 2곳(임펠다운=`h05Y`→도박용, 마린포드=`h05X`
  직접·조건부).
- 확률 지급: 최소 3갈래 — "레일리 도박"(`A0OD`,25%,목재7,`h05Y` 필요),
  "고급유닛 도박"(`Trig_Unit_Gemble_3`), "고급 유닛 생성"(`H0B0` 판매).
- `A0OD`는 어제 "접근불가 가능성 높음"이 틀렸다 — 실재하고 배선돼 있다.
  원인은 검색 방법의 사각(`GetSpellAbilityId()==` 형태를 안 봤다)이었다.
- `h05X`는 판매(`A0OE`)·전설적인 조합 재료(`h05X+h028+h018→h03A`)·히든조합
  2건(피셔타이거·캐럿)의 재료 — 최소 4가지 용도가 있다.
- `CreateUnitsForPlayer7`(중립)은 플레이어 지급이 아니라 **히든조합
  전체가 공유하는 "맵에 뿌려진 재료 풀"**이었다 — `Hidden_Aokiji`가
  요구하는 3종이 전부 같은 함수 안에 있다는 게 결정적 증거다.
