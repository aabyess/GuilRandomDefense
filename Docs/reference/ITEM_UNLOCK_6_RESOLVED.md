# 해금형 아이템 6종 확정 — `I010`/`I00L` 외 나머지 전부

조사: 리서치담당 / 2026-09-07
요청: PM — `I003`·`I00T`·`I004`(시키)·`I00J`(센고쿠)·`I00M`(흰수염)·
`I00U`(나미) 각각이 여는 것, 배타쌍인지 순수추가인지, 수치, 검사 형태.

## ⚠️ 검사 형태 4종 확인(뿌리 ㊼ 대비, 먼저 확인)

전부 검색해서 확인 — 이 4가지 형태를 다 훑어야 안 놓친다:
1. `GetItemTypeId(GetManipulatedItem())=='I0xx'` — **획득 순간**(픽업) 처리, `Trig_item_up_Actions` 하나가 대부분의 아이템을 이 형태로 몰아서 처리한다.
2. `UnitHasItem(유닛,'I0xx')` — 타입으로 직접 검사(`I010` 방식).
3. `udg_item_X_bool[플레이어]` — **부울 플래그로 대신 저장**해두고 나중에 이걸로 검사(`I00L`/`I00M`/`I004`/`I00P` 전부 이 형태 — 아이템 타입 문자열 검색만으론 안 잡힌다).
4. `UnitHasItem(유닛, udg_Item_X[플레이어])` — **아이템 타입이 아니라 저장해둔 "그 아이템 인스턴스 변수"로 검사**(`I00U`가 이 형태 — `udg_Item_nami`).

**전부 `Trig_item_up_Actions`(아이템 픽업 시 발동하는 단일 트리거)가
①과 ③/④의 저장 단계를 겸한다.** 나머지 6종 전부 이 트리거 안에
있었다.

## 결론 요약

| 아이템 | 여는 것 | 배타쌍? | 수치 |
|---|---|---|---|
| `I003`(조로#1) | "Hidden_Ryuma"(시류) 히든조합 체인 진행 게이트 | 배타 아님, 스토리 진행 전제조건 | 값 없음(진행 여부만) |
| `I00T`(조로#2) | **아무것도 안 연다** — 읽히는 곳 자체가 없음 | 해당 없음 | 해당 없음 |
| `I004`(시키) | 기존 1/16 확률 안에 1/2 서브확률 **추가**(새 스킬) | **순수 추가** | 새 스킬 자체(멀티스테이지 AoE), 별도 수치 없음(광역 스킬 연출) |
| `I00J`(센고쿠) | 완전히 새로운 1/12 확률 **추가**(아이템 없으면 이 분기 자체가 없음) | **순수 추가** | **300,000** 고정 AoE(450범위) |
| `I00M`(흰수염) | 기존 1/22 확률의 분기를 아이템버전/기본버전으로 **교체** | **배타(값은 동일)** | 데미지는 둘 다 **동일**(`(1,750,000+최대체력×3%)×(1.5+0.025×R01V연구수)`) — **차이는 시전 속도**: 아이템판이 0.19초 더 빠름(스테이지1 대기 0.30초 vs 0.49초) |
| `I00U`(나미) | 🔴 **두 자리** — 아래 상세 | 🔴 배타(값 다름) | 아래 상세 |

## `I003`(조로#1) 상세

`Trig_item_up_Actions`: `if GetItemTypeId(...)=='I003' then
set udg_Item_zoro_enma[]=true, udg_Item_Zoro[]=아이템, 연구 R039`.

읽히는 곳: `Trig_Hidden8Ryuma_Func005Func001Func036C`:
```jass
if(not(udg_Item_zoro_enma[GetConvertedPlayerId(GetOwningPlayer(GetTriggerUnit()))]==true))then
return false
```
**"Hidden_Ryuma"(시류) 히든조합 체인의 중간 조건이다** — 조로가 이
아이템을 갖고 있어야 그 체인이 진행된다. 완료되면(`Trig_item_up2_Func007C`
또는 이 트리거) 조로가 영구화 히어로(`H0BT`)로 변신하고, `Item_Zoro`가
`I00T`로 교체된다(아래).

## `I00T`(조로#2) 상세 — **읽히는 곳이 없다**

파일 전체에서 `I00T` 리터럴 2건, **둘 다 생성(`CreateItemLoc`)뿐이고
검사하는 코드가 0건이다.** `I003`(체인 진행 게이트) → 변신 완료 →
`I00T`(트로피/장식 아이템)로 자동 교체되는 구조로 보인다. **이 아이템
자체는 아무 것도 열지 않는다** — `linkedAbilityId`를 비워두는 게 맞다.

## `I004`(시키) 상세

`Trig_item_up_Actions`: `UnitAddAbilityBJ('A16Z',...)` 영구 부여 +
`udg_item_siki_bool[]=true`.

읽히는 곳: `Trig_Shiki_Attack_Actions` 맨 끝, 기존 `if GetRandomInt(1,16)==3`
분기(이미 존재하는 스킬) **안쪽에 추가로**:
```jass
if udg_item_siki_bool[...]==true then
if GetRandomInt(1,2)==1 then
call ConditionalTriggerExecute(gg_trg_Shiki_SKill_item)
```
**아이템이 없으면 이 안쪽 if 자체가 실행 안 된다(바깥 1/16이 이미
빠지므로) — 완전히 새로운 확률(1/16×1/2=1/32)로 붙는 신규 스킬이다,
기존 값을 대체하지 않는다.** `Trig_Shiki_SKill_item_Actions`는 멀티스테이지
소환/AoE 연출(`h07P` 소환 등) — 고정 수치 데미지가 최상위에 안 보여서
정확한 값은 이번엔 끝까지 안 팠다(필요하면 이어서).

## `I00J`(센고쿠) 상세

`Trig_item_up_Actions`: `SetPlayerTechResearchedSwap('R03A',1,...)`뿐
(부울 플래그 없이 연구로 대신 저장).

읽히는 곳: `Trig_Sengoku_Attack_Actions` 맨 끝:
```jass
if GetPlayerTechCountSimple('R03A',GetOwningPlayer(GetAttacker()))==1 then
if GetRandomInt(1,12)==6 then
call ConditionalTriggerExecute(gg_trg_Sengoku_Skill_Item)
```
**아이템(=연구) 없으면 이 블록 자체가 절대 안 돈다(원작에 없던 걸
새로 여는 것) — 순수 추가.** `Trig_Sengoku_Skill_Item_Actions`→
`Func021A`: `RRD(...,300000.00,...)` — **300,000 고정 데미지, AoE(450범위,
`Trig_Sengoku_Skill_Item_Func020004` 조건그룹).**

## `I00M`(흰수염/에드워드) 상세

`Trig_item_up_Actions`: `udg_item_edward_bool[]=true`뿐.

읽히는 곳: `Trig_Ed_Attack_Actions`:
```jass
if GetRandomInt(1,22)==6 then
if udg_item_edward_bool[...]==true then
call ConditionalTriggerExecute(gg_trg_Ed_Skill_1_Item)
else
call ConditionalTriggerExecute(gg_trg_Ed_Skill_1)
```
**`I010`(키드)과 같은 모양의 진짜 배타 쌍**이지만, **`Trig_Ed_Skill_1_Actions`와
`Trig_Ed_Skill_1_Item_Actions`의 `RRD` 데미지 공식이 완전히 동일하다**
(둘 다 `(1,750,000+대상최대체력×0.03)×(1.5+0.025×R01V연구수)`, 원문 대조
완료). **차이는 딱 하나, 애니메이션·타이밍이다** — 기본판은 스테이지1에서
0.49초 대기(`"spell one"` 애니메이션), 아이템판은 0.30초 대기(`"attack gold"`
애니메이션) — **아이템판이 시전 완료까지 약 0.19초 더 빠르다.** 데미지가
아니라 **DPS(캐스트 속도)가 오르는 아이템**이다.

## 🔴 `I00U`(나미) 상세 — 두 자리, 하나는 기존 조사를 정정해야 한다

`Trig_item_up_Actions`: `udg_Item_nami[]=아이템`, 살아있는 `TR_Hero`가
있으면 즉시 그 유닛에게 아이템 이전, 연구 `R02G`(읽히는 곳 없음, 장식).

**읽히는 곳 ①**: `Trig_Eternal_Nami_Func002Func015C`:
```jass
if(not(UnitHasItem(udg_Tech_Tree[...],udg_Item_nami[...])==true))then
return false
```
**"Eternal_Nami"(채팅언락 47종 중 하나) 전제조건이다** — 도움소
건물이 이 아이템을 갖고 있어야 나미 영원한 등급 언락이 가능하다.
어제 조사(Eternal 47종 전제유닛)에서 놓친 자리 — **채팅언락 전제조건이
`Player(7)` 중립유닛 존재뿐 아니라 아이템 보유로도 걸리는 경우가
있다는 뜻**이다(이 47개 전부 다시 훑을지는 판단 필요, 이번엔 나미만
확인).

**읽히는 곳 ② — 🔴 `Trig_Nami_Skill_2_Actions`, 어젯밤(초반) 조사와 다시
맞대야 한다:**
```jass
elseif s__TrigVariables_Stage[GlobalTV]==1 then
if UnitHasItem(캐스터,udg_Item_nami[...])==true then
  if GetRandomInt(1,100)<33 then
    ... 800,000 데미지(대형 이펙트) ...
  else
    ... 400,000 데미지 ...
  endif
endif
// ↓ 이 if 블록 바깥, 무조건 실행
call UnitDamagePointLoc(...,400000.00,...)
call RRD(...,400000.00,...)
```
**이 자리가 초반 조사(`RRD_SEQUENCE_DUPLICATE_AUDIT.md`)에서 이미
"33%/67% 배타 쌍 + 무조건 발동 컴포넌트 하나"로 확인했던 그 트리거다.**
**그런데 그 조사는 이 33%/67% 쌍 전체가 `UnitHasItem(Item_nami)`로
게이트돼 있다는 걸 놓쳤다** — 즉:
- **`I00U`가 없으면**: 800,000도 400,000(아이템분기)도 둘 다 원천적으로
  발동 안 하고, **무조건 컴포넌트(400,000)만** 나간다.
- **`I00U`가 있으면**: 33% 확률로 800,000(+무조건 400,000=합계1,200,000),
  67% 확률로 400,000(+무조건 400,000=합계800,000).

**이건 이미 "확정된 버그"로 처리된 강주혁/김민준 자산 수정(오늘 밤
초반 보고)이 정확한지 다시 봐야 한다는 뜻이다** — 그 수정이 "33%/67%를
조건 없이 걸어라"였다면, **아이템 게이트가 통째로 빠진 채 수정된
것**일 수 있다. 이건 리서치 영역 밖(자산 값 판단)이라 판단은 넘기고
사실만 보고한다.

## 남은 것

- `A16Z`(시키가 받는 신규 능력) 자체의 수치는 이번엔 안 팜.
- `Trig_Shiki_SKill_item_Actions`의 최종 데미지값(멀티스테이지 끝
  `Func0xxA`)까지는 이번엔 안 팜.
- `I00U`가 47종 Eternal 전제조건 중 유일한 "아이템형" 게이트인지,
  다른 것도 있는지는 확인 안 함.
