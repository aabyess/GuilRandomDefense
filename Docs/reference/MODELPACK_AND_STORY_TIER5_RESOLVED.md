# `Modelpack_R_unit` 정체 + 스토리 보상 두 자리 조건 확정

조사: 리서치담당 / 2026-09-07

## `Modelpack_R_unit`("다른세계 유닛 도박" 결과 풀) — **지역 스캔 방식, 목록 자체는 이번엔 확정 못함**

**populate 방식**: 새 전역변수를 코드로 채우는 게 아니라, **맵의 특정
지역(rect)에 미리 놓인 유닛을 스캔해서 그대로 그룹에 담는 방식**이다:

```jass
call ForGroupBJ(GetUnitsInRectAll(gg_rct_Model_Pack_R1Unit),
    function Trig_tier_Random_Func040A)
// Func040A: call GroupAddUnitSimple(GetEnumUnit(),udg_Modelpack_R_unit)
```

`GetUnitsInRectAll`(타입 필터 없음, 그 지역에 있는 유닛 전부)이라, **`gg_rct_Model_Pack_R1Unit`이라는 이름의 지정 구역에 어떤 유닛이 몇 종
놓여 있는지가 곧 이 풀의 목록**이다. **이 지역의 좌표·배치 유닛
목록은 `war3map.j` 스크립트만으론 못 본다** — 지역(rect) 정의와 그
안의 유닛 배치는 맵에디터가 별도 파일(리전 정의+`war3mapUnits.doo`류
배치 데이터)에 저장하는데, 이번 조사 도구로는 접근 못했다. ⚠️ **"우리
게임에 대응이 없다"는 목록 확인이 아니라 도구 한계로 인한 미확인**이다
— 목록 자체를 알아내려면 원본 `.w3x`를 에디터로 열거나 `.doo`/리전
파일을 별도로 파싱해야 한다.

**실행 시점**: `Trig_tier_Random_Actions`는 `InitTrig_tier_Random`에서
생성 직후 `DisableTrigger`되지만(뿌리 ㊸, 무시해도 됨), 별도로
`TriggerExecute(gg_trg_tier_Random)`가 **`TriggerExecute(gg_trg_MobBase)`
바로 다음 줄에서 직접 호출**된다 — **맵 초기화 시퀀스의 일부로 게임
시작 시 딱 한 번, 로스터 스폰 테이블(`MobBase`) 설정 직후에 이 풀도
같이 채워진다.** 이후 갱신되는 코드는 없다(정적 스냅샷).

**같은 함수가 다른 풀들도 이 방식으로 채운다** — `gg_rct_Ran0`~
`gg_rct_Ran_7`(→`Random4`/`Random5`/`Random_UniqueSpecial` 등),
`udg_unit_dobakGroup1/2/3`도 전부 지정 지역 스캔 방식이다. **이건
`Player(7)` 중립풀(어제 조사)과는 다른 매커니즘**이다 — 그쪽은
좌표 무관·존재판정, 이쪽은 **특정 좁은 구역에 실제로 놓인 유닛만
스캔**한다(구역 하나로 한정, `GetPlayableMapRect()` 전체가 아니다).

## `Trig_Story_reward8`의 `h05X` 보너스 조건 — **"도움소 잠금" 항법 선택자 전용**

```jass
function Trig_Story_reward8_Func002Func001Func008C takes nothing returns boolean
if(not(udg_Tech_No_support[GetForLoopIndexA()]==true))then
return false
endif
return true
endfunction
```

**`udg_Tech_No_support`는 어제(그제) 확인한 그 변수다 — "도움소 잠금"
항법을 고른 플레이어에게만 켜지는 플래그.** 즉 **마린포드 스토리
보상에서 레일리(`h05X`)를 추가로 받으려면 "도움소 잠금" 항법을
선택했어야 한다** — 그 항법의 트레이드오프(아이템 도박 축소풀 대신
받는 보상)에 이 레일리 보너스도 포함된다는 뜻이다.
(바깥쪽 조건 `Func002Func001C`는 `udg_PlayerDeath[]==0`, 이미 확인한
무사망 조건 — 그대로.)

## `Trig_Story_Tier5_rayleigh`의 발동 조건 — **리전 진입 트리거**

```jass
function InitTrig_Story_Tier5_rayleigh takes nothing returns nothing
set gg_trg_Story_Tier5_rayleigh=CreateTrigger()
call TriggerRegisterEnterRectSimple(gg_trg_Story_Tier5_rayleigh,gg_rct_StoryReward_5_2)
call TriggerAddCondition(...,function Trig_Story_Tier5_rayleigh_Conditions)
...
function Trig_Story_Tier5_rayleigh_Conditions takes nothing returns boolean
if(not(GetUnitTypeId(GetTriggerUnit())=='e01A'))then
return false
```

**챗코드도, 유닛 판매도 아니라 "특정 더미 유닛(`e01A`)이 특정
지역(`gg_rct_StoryReward_5_2`)에 들어오면" 발동하는 리전 트리거다.**
`e01A`는 플레이어가 조작하는 유닛이 아니라 **스토리 시퀀스가 자동으로
생성해 이 구역으로 이동시키는 더미**로 보인다(WC3의 표준 "컷씬
체크포인트 통과" 패턴) — **어느 스토리 단계에서 이 더미가 생성되는지는
이번엔 안 팠다**(이름 "Tier5"로 미루어 5단계 진행 보상 체인의 일부로
보인다).

## 결론

- `Modelpack_R_unit`(다른세계유닛도박 27% 결과 풀): 메커니즘은 확정
  (지역 스캔, 게임시작 1회), **목록 자체는 도구 한계로 미확인** —
  필요하면 `.w3x`를 직접 열어 지역 배치를 봐야 한다.
- `Story_reward8`의 레일리 보너스: **"도움소 잠금" 항법 전용.**
- `Story_Tier5_rayleigh`: 리전 진입 트리거, 어느 스토리 단계인지는
  미확인.
