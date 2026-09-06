# "희귀함 리롤"(`unique_rerole`) 전수 + `unique_*` 계열 정체

조사: 리서치담당 / 2026-09-07
요청: PM — `Trig_unique_rerole` 전문(축약 금지), 무엇을/어떻게/비용/한도기준/
실패결과, `unique` 계열이 뭔지.

## ① `Trig_unique_rerole_Actions` 전문 — 한 줄도 안 뺐다

```jass
function Trig_unique_rerole_Actions takes nothing returns nothing
if(Trig_unique_rerole_Func001C())then                          // Rerole_count_int >= (2+Dobak_Tech_int) : 이미 한도 도달
call IssueImmediateOrderBJ(GetTriggerUnit(),"stop")
call DisplayTimedTextToForce(...,"리롤회수를 소진하였습니다.")
set udg_Dobak_Tech_int[...]=1                                   // ⚠️ 아래 ⑦ 참고
call UnitRemoveAbilityBJ(GetSpellAbilityId(),GetTriggerUnit())
else
if(Trig_unique_rerole_Func001Func001C())then                    // 목재 >= 2
set udg_Rerole_count_int[...]=(udg_Rerole_count_int[...]+1)     // 시도 횟수 +1(성공/실패 무관)
call SetPlayerStateBJ(...,LUMBER,(...)-2)                       // 목재 2 차감(성공/실패 무관)
if(Trig_unique_rerole_Func001Func001Func005C())then              // GetRandomInt(1,100)<=(20-Dobak_Tech_int*80) : 실패
call DisplayTimedTextToForce(...,"리롤을 실패하였습니다.")
call UnitRemoveAbilityBJ(GetSpellAbilityId(),GetTriggerUnit())   // 이 유닛의 리롤 능력만 회수(유닛은 안 죽는다)
else                                                              // 성공
call KillUnit(GetTriggerUnit())                                  // 원래 유닛 제거
call CreateNUnitsAtLoc(1,GetUnitTypeId(GroupPickRandomUnit(
    GetUnitsInRectMatching(GetPlayableMapRect(),
        Condition(...IsUnitInGroup(GetFilterUnit(),udg_Random4)==true...)))),
    GetOwningPlayer(GetTriggerUnit()),T_Location,bj_UNIT_FACING) // Random4 풀에서 새 유닛 생성
call DisplayTimedTextToForce(...,"희귀함 리롤을 사용하여 [원래이름]->[새이름]으로 변환되었습니다!")
endif
if(Trig_unique_rerole_Func001Func001Func006C())then               // Rerole_count_int >= (2+Dobak_Tech_int) : 방금 시도로 한도 도달했는가
call SetPlayerAbilityAvailableBJ(false,'A0VX',GetOwningPlayer(GetTriggerUnit()))  // 플레이어 전체에서 A0VX 영구 비활성화
call DisplayTimedTextToForce(...,"리롤회수를 소진하였습니다.")
else
call DisplayTimedTextToForce(...,"리롤회수:"+(2+Dobak_Tech_int-Rerole_count_int)+"회 남았습니다.")
endif
else
call DisplayTimedTextToForce(...,"목재가 부족합니다!")
call IssueImmediateOrderBJ(GetTriggerUnit(),"stop")
endif
endif
endfunction
```

## ②~⑥ 답

| 질문 | 답 |
|---|---|
| ② 무엇을 리롤하는가 | **유닛 그 자체.** "희귀함 등급 랜덤 유닛"을 킬(`KillUnit`)하고 `udg_Random4`(리전스캔 풀, "희귀함" 티어로 보임)에서 새 랜덤 유닛으로 교체한다. 도박(가챠) 자체를 다시 돌리는 게 아니라 **이미 뽑은 결과물을 다른 결과물로 맞바꾸는 것**이다. |
| ③ 발동 방식 | **능력**(`A0VX`), `GetSpellAbilityId()=='A0VX'`로 감지. 상점·채팅 아님 — **그 능력을 가진 유닛이 직접 캐스트**한다. |
| ④ 비용 | **목재 2**(시도할 때마다, 성공/실패 무관하게 차감). 골드·위습·행운토큰은 안 든다. |
| ⑤ 한도 기준 | **플레이어당, 판 전체 기준**(라운드·유닛 단위 아님). `Rerole_count_int`는 플레이어별 전역 누적 카운터라, **어느 유닛으로 시도하든 전부 같은 카운터에 쌓인다.** 기본 한도 2회, "도박광" 항법 선택 시 3회. |
| ⑥ 실패하면 | **유닛은 안 죽는다.** 목재 2와 시도 횟수 1회만 소모되고, **그 유닛의 `A0VX` 능력만 회수**된다(그 유닛으론 다시 못 함, 다른 `A0VX` 보유 유닛이 있으면 그걸로는 가능 — 플레이어 전체 한도 안에서). |

## ⑦ `A0VX`를 누가 갖는가 — 3곳에서 부여, 전부 "희귀함 가챠의 꽝(대체) 결과"

```
Trig_unique_unit_create_Actions  ("고급 유닛 생성", H0B0 판매)의 최종 폴백 분기
Trig_Unit_Gemble_3_Actions       ("고급유닛 도박", h06D)의 최종 폴백 분기
Trig_Story_Tier4_Actions         스토리 보상(Tier4)
```
**셋 다 "지정된 특별한 결과(레일리·해적선 등)가 아니라 일반 랜덤풀에서
나온 유닛"에게 붙는다** — 이 능력을 가진 유닛 = "가챠에서 나온 이름
없는 희귀함 유닛", 그걸 마음에 안 들면 리롤로 다른 랜덤 희귀함으로
바꿀 수 있게 하는 보정 장치로 보인다.

## 🔴 곁다리(원문 그대로 남김, 해석 안 함) — 이 트리거 자체가 `Dobak_Tech_int`를 SET하는 자리

한도를 이미 다 쓴 뒤 능력을 한 번 더 캐스트하면(`Func001C` 분기),
`udg_Dobak_Tech_int[플레이어]`를 **`1`로 SET**한다. 이건 항법 선택
(`Trig_onedill_Tech_Actions`) 말고 이 변수를 바꾸는 유일한 다른
자리다 — 의도(리롤 소진에 대한 보상인지, 변수 재사용인지)는 판단
안 하고 원문만 보고한다(`NAVIGATION_5FLAG_FULL_READ_SWEEP.md`에서
이미 짚은 것과 동일 건).

## ⑦-2 `unique` 계열 전수 — **놓친 시스템 맞다, 「판매」쪽이 하나 더 있었다**

`Trig_unique_*` 9개 트리거를 전부 나열:

```
unique_rerole        A0VX   희귀함 리롤(위)
unique_unit_create   —      "고급 유닛 생성"(H0B0 판매, 어제 조사)
unique_sell          A09G   판매 3회 누적마다 랜덤위습1(+가끔 목재1)
unique_sell2         A0B8   "안흔함 판매" — 성공/실패 있음(성공시 랜덤위습1+가끔목재1)
unique_sell3         A0BA   판매시 확정 랜덤위습1(+가끔 목재1)
unique_sell4         A0B9   판매시 확정 랜덤위습2+목재1
unique_sell5         A0BB   판매시 흔함위습1+목재1+스토리보상유닛(e018) 지급
unique_sell6         A080   "노획물 판매" — 성공/실패(성공시 랜덤위습1+가끔 100골드+목재1)
unique_sell7         A0OE   레일리(h05X/h05Y) 전용 — 위습1+특성포인트1(이미 확인)
```

**`unique`은 "특정 유명 캐릭터 하나"를 뜻하는 게 아니라 "판매/생성/리롤
같은 유틸리티 능력" 카테고리 이름이다.** `unique_sell`(공백)~`sell6`
6종은 **등급대별로 다른 "유닛을 팔면 위습/골드/목재를 돌려받는"
범용 판매 보상 시스템**이다(안흔함·노획물 등 등급별로 트리거가
갈림) — `unique_sell7`(레일리 전용, 특성포인트까지 주는 것)만 예외적으로
강화된 버전이다.

**🔴 PM이 걱정한 대로, 이건 우리가 통째로 못 본 시스템이다** — 지금까지
"판매"는 `A0OE`(레일리) 하나로만 알고 있었는데, **일반 유닛을 팔아도
위습·골드·목재를 돌려받는 범용 판매 보상 체계가 최소 6종(등급/카테고리별로
갈림) 더 있다.** 각 능력을 어느 유닛(들)이 갖고 있는지, 정확히 어느
등급대에 대응하는지는 이번엔 안 팠다 — **필요하면 다음 작업으로.**
