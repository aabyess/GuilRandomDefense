# 고대의 배(`h05Y`) — 🔴 소모 맞다, 내가 어제 `RemoveUnit` 줄을 빼고 인용했다

조사: 리서치담당 / 2026-09-07
요청: PM — 구현담당1의 "소모"(Consume) 구현이 맞는지, 원문에 `RemoveUnit`이
있는지 재확인.

## 결론 — **구현담당1이 맞다. 내가 어제 인용할 때 그 줄을 빠뜨렸다.**

`Trig_Acient_Ship_Actions`(함수 전체, 세 분기 전부)를 다시 그대로
옮긴다 — **이번엔 한 줄도 안 빼고**:

```jass
function Trig_Acient_Ship_Actions takes nothing returns nothing
if(Trig_Acient_Ship_Func001C())then                     // A0OC 다른세계유닛도박
if(Trig_Acient_Ship_Func001Func001C())then               // 목재 충분?
call SetPlayerStateBJ(...,LUMBER,(...)-7)
if(...성공...)then
  ...랜덤유닛 획득...
else
  ...실패, 럭키토큰 지급...
endif
call RemoveUnit(GetTriggerUnit())                         // ← 성공/실패 공통, 무조건
else
  "목재가 부족합니다!"
endif
else
endif
if(Trig_Acient_Ship_Func002C())then                       // A0OD 레일리도박
if(Trig_Acient_Ship_Func002Func001C())then                // 목재 충분?
if(25% 성공)then
  call CreateNUnitsAtLoc(1,'h05X',...) "획득!"
  call RemoveUnit(GetTriggerUnit())                        // ← 성공 분기
else
  "레일리 도박을 실패 하셨습니다!"
  call RemoveUnit(GetTriggerUnit())                        // ← 실패 분기, 여기도 있다
endif
call SetPlayerStateBJ(...,LUMBER,(...)-7)
else
  "목재가 부족합니다!"
endif
else
endif
if(Trig_Acient_Ship_Func003C())then                       // A023 해적선도박
if(Trig_Acient_Ship_Func003Func001C())then
if(40% 성공)then
  ...h060 획득... call RemoveUnit(GetTriggerUnit())        // ← 성공 분기
else
  "해적선 도박을 실패 하셨습니다!" call RemoveUnit(GetTriggerUnit())  // ← 실패 분기
endif
call SetPlayerStateBJ(...,LUMBER,(...)-4)
else
  "목재가 부족합니다!"
endif
else
endif
endfunction
```

**`RemoveUnit(GetTriggerUnit())`가 세 분기 전부, 성공/실패 가리지 않고
전부 들어있다.** `GetTriggerUnit()`이 누구인지도 확인: 이 트리거는
`EVENT_UNIT_SPELL_EFFECT`로 **`h05Y` 생성 시점에 그 개별 인스턴스에
직접 등록된다**(`TriggerRegisterUnitEvent(gg_trg_Acient_Ship,
GetLastCreatedUnit(),EVENT_UNIT_SPELL_EFFECT)`, `h05Y` 생성 2곳 모두
동일) — **`GetTriggerUnit()` = 캐스트한 유닛 = `h05Y` 자기 자신.**
`Trig_Acient_Ship_Conditions`(`GetUnitTypeId(GetTriggerUnit())=='h05Y'`)와도
정확히 맞아떨어진다.

## PM 질문 4개 답

1. **`RemoveUnit` 있는가**: **있다.** 함수 전체에 정확히 4곳(A0OC 1곳
   +공통, A0OD 2곳=성공/실패 각각, A023 2곳=성공/실패 각각) — 다
   대상은 `GetTriggerUnit()`=`h05Y` 자신.
2. **A023·A0OC도 같은가**: **셋 다 같은 트리거(`Trig_Acient_Ship_Actions`)
   안의 세 분기이고(`GetSpellAbilityId()`로 어느 능력을 캐스트했는지만
   가른다), 셋 다 예외 없이 `RemoveUnit`으로 끝난다.**
3. **목재만 있으면 무한 도박 가능한가**: **아니다.** 시도할 때마다
   `h05Y`가 사라지므로, **도박 1회 = 배 1척**이다. 다음 도박을 하려면
   `h05Y`를 다시 구해야 한다.
4. **`h05Y` 획득 경로가 스토리7 하나뿐인가**: **아니다, 최소 2갈래다.**
   - `Trig_Story_reward7_Actions`: 임펠다운 스토리 완료 시 **전원에게
     1척씩 확정 지급(1회성).**
   - `Trig_item_up_Actions`/`Trig_item_up2_Func002C`: **아이템 `I00S`를
     사용하면 `h05Y`가 하나 더 생긴다**(`GetItemTypeId(GetManipulatedItem())=='I00S'`).
     `I00S`는 `ITEM_POOL_FULL_CENSUS.md`가 이미 확인한 22종 아이템 풀
     구성원 중 하나다 — **`H0BS`(메타몽) 아이템도박에서 뽑을 수 있는
     아이템**이고, `ItemPoolRemoveItemType`로 "한 번 뽑으면 그 종류는
     그 플레이어 풀에서 사라진다"는 기존 확인이 그대로 적용된다 —
     **플레이어당 `I00S`는 사실상 최대 1개**(재뽑기 없음, 풀에서
     제거되므로).

## 결론 — 경제 그림

**한 플레이어가 한 판에 "고대의 배" 도박을 시도할 수 있는 총
횟수는 사실상 최대 2회다**(스토리7 확정지급 1척 + 아이템도박에서
`I00S`가 걸리면 1척 더, 상한). **"목재만 있으면 무한"도 아니고
"평생 딱 1번"도 아니다 — 스토리 진행 + (운이 좋으면) 아이템도박 결과에
따라 1~2회.** `A0OD`(레일리 25%)만 매번 쓴다고 가정해도, 배가 2척이면
레일리 획득 기대치는 약 **1-(0.75×0.75)=43.75%**(2회 시도 기준)다 —
"사실상 확정 획득"은 아니다.

**PM이 지적한 `RemoveUnit` 출처 혼동**: 판매(`A0OE`)의 `RemoveUnit`이
아니라 **도박 함수 자기 자신의 코드**였다 — `A0OE`는 완전히 다른
트리거(`Trig_unique_sell7_Actions`)이고, 거기도 `KillUnit(GetTriggerUnit())`
으로 별도로 소모시킨다(어제 확인). 헷갈린 게 아니라 **내가 어제
`Trig_Acient_Ship_Actions`를 문서에 옮길 때 코드 블록을 축약하면서
`RemoveUnit` 줄들을 빠뜨렸다** — 원인은 그거였다.
