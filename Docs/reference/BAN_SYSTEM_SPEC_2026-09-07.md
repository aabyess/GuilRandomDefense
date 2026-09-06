# 밴(잠금) 시스템 완전 명세 — 구현용, 7항목 전부 원문 근거와 함께

조사: 리서치담당 / 2026-09-07, `war3map.j` 원문(`Trig_ban_Actions`,
`Trig_ban_transendence_*`, `s__TrigVariables_SleepForStage*`,
`Trig_Select_effect_Actions`) 전수 확인.
요청: PM — `PLAYER7_619_IDENTITY_RESOLVED.md`에서 찾은 잠금 시스템의
구현용 완전 명세 7항목. 추측 없이, 못 찾은 건 "못 찾음(어디를 봤는지)"로.

## 🔴 정정 — 이전 문서(`PLAYER7_619_IDENTITY_RESOLVED.md`)의 "전부 잠긴다"는 틀렸다

이전 문서는 반복 루프가 27/11개를 **전부** 훑을 것처럼 적었는데, 정지
조건 함수(`Func003Func004C`/`Func004Func003C`/`Func005Func003C`)를
마저 읽으니 **정해진 개수만 잠그고 멈춘다.** 아래 ④에서 정정.

---

## ① 주기 — 언제 발화하고, 반복 간격이 얼마인가

**발화 시점**: `gg_trg_ban`은 `InitTrig_ban`에서 `DisableTrigger`로
만들어지고 **이벤트 등록이 아예 없다** — 오직 `TriggerExecute`로만
실행된다. 그 호출 지점을 찾았다:

```jass
// Trig_Select_effect_Actions 안, 특정 모드/난이도 분기의 끝
...
call SetPlayerTechResearchedSwap('R00V',6,Player(6))
call SetPlayerTechResearchedSwap('R00U',8,Player(6))
call SetPlayerTechResearchedSwap('R00W',9,Player(6))
call TriggerExecute(gg_trg_ban)
set udg_Mode_int=6
set udg_sinsaki_mob_abil='A141'
```
`Trig_Select_effect_Actions`는 **모드/난이도 선택 트리거**다(Player(6)에
난이도 보정 연구를 스택시키는 코드 — README가 이미 확인한 "난이도는
`R00A` 등 업그레이드를 적 플레이어에 건다"는 방식과 정확히 같은 패턴).
**즉 밴 시스템은 라운드나 타이머가 아니라 "게임 시작 시 모드 선택이
끝나는 순간" 딱 1번 발화한다.**

`Trig_ban_Actions`(설정 함수) 맨 끝에 `call
TriggerExecute(gg_trg_ban_transendence)`가 있어, 설정이 끝나자마자
바로 잠금 루프가 시작된다 — **둘 사이에 지연이 없다.**

**반복 간격**: `s__TrigVariables_SleepForStage(this, dur, stage)`의
정의:
```jass
function s__TrigVariables_SleepForStage takes integer this,real dur,integer stage returns nothing
...
call TimerStart(s__TrigVariables_t[this],dur,false,function s__TrigVariables_TimerExecuteTrigger)
endfunction
```
`dur`이 네이티브 `TimerStart`에 그대로 들어간다 — **초 단위 실제
시간이다.** 루프 안에서 쓰는 값은 전부 `0.02`(간혹 마지막에 `0.03`)다.
**약 0.02초(50분의 1초) 간격으로 한 번씩 무작위 추첨을 시도한다** —
사실상 게임 시작 직후 눈 깜짝할 사이(1~2초 이내)에 전체 시퀀스가
끝난다(④의 정지 조건이 최대 시도 횟수를 사실상 정하지 않고 "성공
횟수"만 세므로, 이미 잠긴 슬롯을 계속 뽑아도 계속 재시도한다 — 운이
나쁘면 조금 더 걸릴 뿐 실질적으로 초 단위다).

## ② 선택 방식 — 균등 무작위, 이미 잠긴 건 그냥 재시도

```jass
call s__TrigVariables_Setinteger(GlobalTV,0,GetRandomInt(0,26))  // Eternal, 27개 중 균등
call s__TrigVariables_Setinteger(GlobalTV,0,GetRandomInt(0,10))  // IM, 11개 중 균등
call s__TrigVariables_Setinteger(GlobalTV,0,GetRandomInt(0,7))   // Limited, 8개 중 균등
call s__TrigVariables_Setinteger(GlobalTV,0,GetRandomInt(0,6))   // Forever(게임시작 1회), 7개 중 균등
```
전부 `GetRandomInt`(균등 난수, 가중치 없음). 이미 잠긴 슬롯을 다시
뽑으면(`Ban_int2[idx]==true` 등) **조건 함수가 `false`를 반환해서 그
사이클은 아무 일도 안 하고 지나간다** — 재추첨을 그 자리에서 다시
하는 게 아니라, **다음 0.02초 사이클에 또 새로 무작위로 뽑는다.**
따로 "이미 뽑은 것 제외하고 뽑기" 로직은 없다(단순 재시도형 — 남은
슬롯이 적어질수록 헛방 확률이 올라가지만, 27개 중 8개만 뽑으면
되므로 정지까지 오래 안 걸린다).

## ③ 범위 — **게임 전역이다, 플레이어별이 아니다** (가장 중요한 항목)

**결론: 전역 공유.** 근거 둘:

1. **Eternal/IM**: `DisableTrigger(udg_Ban_Trigger[idx])` — `trigger`는
   워3에서 플레이어 단위 객체가 아니라 **게임에 하나만 있는 전역
   핸들**이다. 한 번 `DisableTrigger`되면 그 트리거는 **어느 플레이어가
   채팅을 쳐도 반응하지 않는다.**
2. **Limited**: 아래 ④에서 보듯 `SetPlayerAbilityAvailableBJ(false,
   ability, Player(N))`을 **`Player(0)`·`Player(1)`·`Player(2)`·
   `Player(3)` 4명 전부에게 순서대로 개별 호출한다** — 이건 "한
   플레이어만 잠근다"가 아니라 **"모든 실제 플레이어에게 동일하게
   잠근다"는 걸 코드가 명시적으로 보여주는 결정적 증거**다. 굳이
   4번 반복 호출하는 이유 자체가 "이 잠금은 플레이어별이 아니라
   전원에게 동시에 적용되어야 한다"는 설계 의도를 보여준다.

**→ 우리 `ChatUnlockManager`의 `hasClaimedShared[playerId]`류
플레이어별 상태 구조는 이 밴 시스템에 안 맞는다.** 이건 "게임 하나당
전역 잠금 세트 하나"(예: `Dictionary<string recipeId, bool locked>`
같은 게임-전역 단일 상태)로 짜야 원작과 맞다 — **4인이 전부 같은
잠금 목록을 공유한다.**

## ④ 정지 조건 — **다 안 잠근다. 정해진 개수만 잠그고 멈춘다** (①에서 예고한 정정)

각 단계의 정지 조건 함수 원문:
```jass
function Trig_ban_transendence_Func003Func004C takes nothing returns boolean
if(not(s__TrigVariables__get_integerB(GlobalTV)==8))then   // Eternal: 성공 8회째에 정지
return false
endif
return true
endfunction

function Trig_ban_transendence_Func004Func003C takes nothing returns boolean
if(not(s__TrigVariables__get_integerB(GlobalTV)==3))then   // IM: 성공 3회째에 정지
return false
endif
return true
endfunction

function Trig_ban_transendence_Func005Func003C takes nothing returns boolean
if(not(s__TrigVariables__get_integerB(GlobalTV)==2))then   // Limited: 성공 2회째에 정지
return false
endif
return true
endfunction
```
`integerB`는 "이번 단계에서 성공적으로 잠근 횟수" 카운터다(매 성공마다
`+1`, 단계 전환 시 `0`으로 리셋). **상한이 명확히 있다:**

| 단계 | 전체 슬롯 | 잠그는 개수 | 비고 |
|---|---:|---:|---|
| Forever(ET) | 7 | **1**(게임시작 즉시, 별도 함수) | `Trig_ban_Actions`에서 |
| Eternal | 27 | **8** | `Trig_ban_transendence` Stage1 |
| IM | 11 | **3** | `Trig_ban_transendence` Stage10 |
| Limited | 8 | **2** | `Trig_ban_transendence` Stage20, 끝나면 `Flush`(전체 시퀀스 종료) |
| **합계** | 53 | **14** | 매 게임 무작위로 이 14개가 확정된다 |

**나머지 39개(53−14)는 그 게임에서 끝까지 잠기지 않는다** — 위
문서(`PLAYER7_619_IDENTITY_RESOLVED.md`)에 적었던 "결국 시간이
지나면 27개+11개가 전부 잠긴다"는 서술은 **오독이었다, 정정한다.**
Limited 단계가 끝나면 `s__TrigVariables_Flush(GlobalTV)`로 코루틴
자체가 완전히 종료된다 — 그 이후 재개되는 코드가 없다(다시 도는
자리를 못 찾음, `war3map.j` 전체에서 `gg_trg_ban_transendence`를
다시 `TriggerExecute`하는 곳도 없음).

## ⑤ 시작 즉시 잠기는 Forever 1종 — 무작위, 고정 아님

```jass
set udg_int_Ban=GetRandomInt(0,6)
call DisableTrigger(udg_Ban_Trigger_ET[udg_int_Ban])
```
7개 중 균등 무작위 1개. **고정된 특정 레시피가 아니다** — 매 게임
다른 "영원한" 캐릭터가 시작부터 봉인될 수 있다.

## ⑥ 플레이어에게 어떻게 보이나 — **텍스트 안내 없음, 시각효과뿐**

`Trig_ban_Actions`/`Trig_ban_transendence_Actions` 전체를 다시
훑었지만 `DisplayTimedTextToForce`/`DisplayTextToPlayer` 같은
문자 안내 호출이 **단 하나도 없다.** 유일한 피드백은:
```jass
call AddSpecialEffectTargetUnitBJ("origin",마커유닛,"Effect Lock Black9.mdx")
call SetUnitVertexColorBJ(마커유닛,15.00,15.00,15.00,35.00)
```
**자물쇠 모델 이펙트 + 유닛을 거의 검게(RGB 15/15/15) + 반투명(알파
35)으로 물들이는 것뿐이다.** 텍스트 알림이 원작에 없으므로, **우리
UI에 "이 레시피가 잠겼습니다" 같은 팝업/토스트 문구를 새로 만든다면
그건 원작에 없는 창작이 된다** — 사장님 방침(전부 원작대로)을
따르려면 **텍스트 없이, 해당 마커/캐릭터 아이콘을 어둡게+자물쇠
아이콘으로 표시하는 것만** 넣는 게 맞다. UI 요소가 필요하다면
"어떤 마커를 무엇으로 보여줄지"부터 결정이 필요하다(이건 판단
요청 사안, 코딩은 안 함).

## ⑦ 이미 재료를 모은 상태에서 잠기면 — 반환 없음, 그냥 막힌다

`DisableTrigger(udg_Ban_Trigger[idx])`가 잠그는 대상은 **그 채팅언락
트리거 자체**다(예: `gg_trg_Eternal_Snake_Luffy`). 워3에서 트리거가
비활성화되면 **그 트리거에 달린 이벤트가 발생해도 Actions 자체가
전혀 실행되지 않는다** — 채팅을 쳐도 조건 검사조차 안 일어난다.
**재료 소비는 그 트리거의 Actions 안에서 일어나므로(다른 문서들에서
이미 확인한 "재료 스캔→KillUnit/제거" 패턴), Actions가 아예 안 도는
이상 재료를 반환하는 로직도, 추가로 소모하는 로직도 없다 — 그냥
그 시점부터 채팅이 아무 반응 없는 상태가 된다.** 이미 인벤토리에
모아둔 재료는 그대로 플레이어 손에 남지만(제거되는 코드가 없음),
그걸로 그 레시피를 다시는 못 연다. **`war3map.j`에 "잠긴 트리거의
재료를 돌려준다"는 코드는 없다(리터럴 검색 0건) — 확정.**

## 🟡 부수 해결 — `Ban_Unit_Limited`/`Ban_Trigger_Limited`의 정체(이전 미확인 항목)

`PLAYER7_619_IDENTITY_RESOLVED.md`에서 "읽는 자리를 못 찾았다"고
남겼던 것 — **이번에 찾았다.** `Ban_Trigger_Limited[i]`는 트리거가
아니라 **능력ID 문자열**이었고, 이렇게 쓰인다:
```jass
call SetPlayerAbilityAvailableBJ(false,udg_Ban_Trigger_Limited[idx],Player(0))
call SetPlayerAbilityAvailableBJ(false,udg_Ban_Trigger_Limited[idx],Player(1))
call SetPlayerAbilityAvailableBJ(false,udg_Ban_Trigger_Limited[idx],Player(2))
call SetPlayerAbilityAvailableBJ(false,udg_Ban_Trigger_Limited[idx],Player(3))
```
**"제한됨" 8종은 트리거를 끄는 게 아니라, 그 캐릭터의 특정 능력
자체를 4명 전부에게서 상점 재고 불가 처리한다**(`SetPlayerAbilityAvailableBJ`는
보통 "상점에서 이 능력/유닛을 살 수 있는지"를 제어하는 네이티브
함수다). `h0AI`(킹, `A10A`)가 그 8종 중 하나로, **결손 27칸의
"제한됨 −2"와 직접 연결될 가능성이 매우 높다** — 원작에서 킹을
포함한 제한됨 8종 중 매 게임 2종이 아예 못 사는 상태가 될 수
있었다는 뜻이다.

## 남은 미확인

- `Trig_Select_effect_Actions`가 정확히 어느 모드/난이도 조합에서만
  이 분기(위 ①의 `TriggerExecute(gg_trg_ban)` 포함 분기)를 타는지는
  이번엔 전체 조건 트리를 안 폈다 — "모드 선택 완료 후 특정 조건"까지만
  확인, 정확한 조건식은 `[미확인, 필요하면 후속]`.
- Eternal/IM/Limited 각 8·3·2개가 정확히 몇 번째 시도에서 끝나는지(기대
  시행 횟수)는 확률 계산이지 원문 사실이 아니라 이 문서엔 안 넣었다.
