# `Player(7)` 619개체의 정체 — 🔴 도감 전시가 아니라 "시간이 지나면 사라지는 특전" 시스템의 상태판이다

조사: 리서치담당 / 2026-09-07, `war3map.j`(`CreateUnitsForPlayer7`,
`Trig_ban_Actions`, `Trig_ban_transendence`) 원문 전수 확인.
요청: PM — 619개체의 정체(`war3mapUnits.doo` 접근 불가 재확인 포함),
"도감/조합표 전시" 가설 검증.

## 결론 — **가설은 틀렸다. 이건 전시가 아니라 "27+11+7개 특전 레시피가
게임 도중 랜덤 순서로 하나씩 영구 잠긴다"는 시스템의 실시간 상태판이다**

619개체 중 최소 **53개**(`Eternal` 27 + `IM` 11 + `Forever` 7 + `Limited` 8)가
이 시스템의 **개별 마커**로 확정됐다. "도감처럼 늘어놓았다"가 아니라
**각 마커가 특정 채팅언락 레시피 하나씩을 대표하고, 그 레시피가
게임 중 잠기면 마커에 자물쇠 이펙트가 걸리고 색이 어두워진다** —
플레이어들이 맵을 돌아다니며 "이 캐릭터는 아직 열려있다/이미
잠겼다"를 시각적으로 확인하게 하는 실시간 UI다.

## ① `war3mapUnits.doo` 접근 — PM과 동일하게 재확인, 그리고 왜 못 읽는지도 드러났다

`Archive.read('war3mapUnits.doo')`는 나도 `None`을 받는다(`war3map.doo`는
20,006바이트로 정상 읽힘). 그런데 **`CreateUnitsForPlayer7` 자체를 열어보니
이유를 알 수 있었다** — 이 함수는 **`CreateUnit(플레이어,'유닛ID',x,y,각도)`
호출 619개를 그냥 나열한 것**이다. 이건 정상적인(보호 안 된) 워3 맵이라면
**`war3mapUnits.doo`가 엔진에 의해 직접 로드되지 배치 정보가 JASS
코드로 안 나온다.** 이 맵은 **보호 처리되면서 `.doo`가 지워지고 그
배치 데이터가 이 함수 하나로 통째로 JASS화됐을 가능성이 높다** —
그래서 `.doo` 파일 자체가 없는 것으로 보인다(단정은 아니고 정황).

**부가 확인**: 619개 생성 호출 중 **50개만 전역변수(`gg_unit_XXXX_NNNN`)로
저장되고 나머지 569개는 지역변수 `u`로 던져진다**(핸들을 안 남김). 이건
워3 에디터가 "다른 트리거에서 이 특정 배치 유닛을 이름으로 참조해야
하는 경우"에만 전역변수를 만드는 표준 동작과 일치한다 — 즉 **이
50개는 원래부터 "특정 개체를 나중에 다시 가리켜야 하는" 목적으로
배치된 것**이라는 뜻이다. 그 50개 중 실제로 뭘 하는지 추적했다.

## ② `Trig_ban_Actions` — 게임 시작 시 1회, 53개 레시피를 마커에 등록한다

원문(핵심부, 축약 없이):
```jass
set udg_Ban_Trigger[0]=gg_trg_Eternal_Snake_Luffy
set udg_Ban_unit[0]=gg_unit_H0B2_0231
... (Eternal 0~26, 총 27개, 트리거+마커 쌍)
set udg_Ban_Trigger_IM[0]=gg_trg_IM_roger_1
set udg_Ban_Unit_IM[0]=gg_unit_h04J_0046
... (IM 0~10, 총 11개)
set udg_Ban_Unit_Limited[0]=gg_unit_h05E_0347
set udg_Ban_Trigger_Limited[0]='A01Y'
... (Limited 0~7, 총 8개 — 이건 트리거가 아니라 능력ID를 저장한다, ③ 참고)
set udg_Ban_Trigger_ET[0]=gg_trg_Forever_ACE
set udg_Ban_Unit_ET[0]='h059'
... (Forever/ET 0~6, 총 7개)
set udg_int_Ban=GetRandomInt(0,6)
call DisableTrigger(udg_Ban_Trigger_ET[udg_int_Ban])
set udg_T_Location=GetRectCenter(gg_rct_banForever)
call CreateNUnitsAtLoc(1,udg_Ban_Unit_ET[udg_int_Ban],Player(7),udg_T_Location,bj_UNIT_FACING)
call AddSpecialEffectTargetUnitBJ("origin",GetLastCreatedUnit(),"Effect Lock Black9.mdx")
call SetUnitVertexColorBJ(GetLastCreatedUnit(),15.00,15.00,15.00,35.00)
call RemoveLocation(udg_T_Location)
call TriggerExecute(gg_trg_ban_transendence)
```
**게임이 시작되자마자, "영원한"(`Forever`, ET) 레시피 7개 중 정확히
1개를 `GetRandomInt(0,6)`로 무작위로 골라 그 자리에서 즉시 영구
비활성화한다**(`DisableTrigger`) — 즉 **"영원한" 등급 캐릭터 7명 중
1명은 매 게임마다 무작위로 아예 얻을 수 없게 확정된다.** 그 캐릭터의
전용 검은자물쇠 마커를 `gg_rct_banForever`(고정 지역)에 새로
생성해서 시각 표시하고, 마지막 줄에서 `Trig_ban_transendence`를
즉시 실행해 **다음 단계(Eternal·IM 순환 잠금)를 개시**한다.

## ③ `Trig_ban_transendence` — 반복 루프로 Eternal 27개·IM 11개를 무작위 순서로 하나씩 영구 잠근다

```jass
// Stage 3(Eternal 단계): 매 사이클마다
call s__TrigVariables_Setinteger(GlobalTV,0,GetRandomInt(0,26))
if(udg_Ban_int2[index]==false)then           // 아직 안 잠긴 것만
  set udg_Ban_int2[index]=true                // 잠금 처리 완료 표시
  call DisableTrigger(udg_Ban_Trigger[index]) // 그 Eternal_X 트리거 영구 비활성화
  call AddSpecialEffectTargetUnitBJ("origin",udg_Ban_unit[index],"Effect Lock Black9.mdx")
  call SetUnitVertexColorBJ(udg_Ban_unit[index],15.00,15.00,15.00,35.00) // 어둡게 tint
endif
// (27개 다 잠기면 Stage 10으로 전환)

// Stage 20(IM 단계): 완전히 동일한 구조
call s__TrigVariables_Setinteger(GlobalTV,0,GetRandomInt(0,10))
if(udg_Ban_int3[index]==false)then
  set udg_Ban_int3[index]=true
  call DisableTrigger(udg_Ban_Trigger_IM[index])
  call AddSpecialEffectTargetUnitBJ("origin",udg_Ban_Unit_IM[index],"Effect Lock Black9.mdx")
  call SetUnitVertexColorBJ(udg_Ban_Unit_IM[index],15.00,15.00,15.00,35.00)
endif
```
`SleepForStageNext`/`SleepForStage(...,0.02)`로 **일정 간격을 두고
반복**(정확한 초 단위 환산은 이번엔 안 함). **매 사이클 무작위로
"아직 안 잠긴" Eternal(또는 IM) 레시피 하나를 골라 그 자리에서 영구로
잠근다.** 이미 잠긴 걸 다시 뽑으면(`udg_Ban_int2[index]==true`) 그냥
아무 것도 안 하고 넘어간다 — **결국 시간이 충분히 지나면 27개 Eternal과
11개 IM 레시피가 (무작위 "순서"만 다르고) 전부 잠긴다는 뜻**이다(막는
조건이 안 보인다 — 게임이 끝날 때까지 계속 도는 것으로 보인다).

**즉 "전설급 캐릭터를 채팅으로 여는" 47종(Eternal) + 다른세계 도박
관련 11종(IM) + 영원한 7종(Forever) 레시피는, 게임이 진행될수록
무작위 순서로 하나씩 영구히 사라지는 "한정판" 시스템이다.** 아무리
재료를 다 모아도, 그 레시피가 이미 잠긴 뒤라면 다시는 못 연다 —
**타이밍 요소가 있는 시스템**이다. 이건 지금까지 우리 조사(가챠/조합/판매
전부) 어디에도 안 잡혀 있던 완전히 새로운 매커니즘이다.

## ④ "제한됨" 8종(`Ban_Unit_Limited`)만 다른 방식 — 트리거가 아니라 능력ID

```jass
set udg_Ban_Unit_Limited[0]=gg_unit_h05E_0347   set udg_Ban_Trigger_Limited[0]='A01Y'
set udg_Ban_Unit_Limited[1]=gg_unit_h05F_0631   set udg_Ban_Trigger_Limited[1]='A02O'
set udg_Ban_Unit_Limited[2]=gg_unit_h06L_0183   set udg_Ban_Trigger_Limited[2]='A022'
set udg_Ban_Unit_Limited[3]=gg_unit_h05I_0355   set udg_Ban_Trigger_Limited[3]='A007'
set udg_Ban_Unit_Limited[4]=gg_unit_h040_0334   set udg_Ban_Trigger_Limited[4]='A0RP'
set udg_Ban_Unit_Limited[5]=gg_unit_h07I_0295   set udg_Ban_Trigger_Limited[5]='A0S0'
set udg_Ban_Unit_Limited[6]=gg_unit_h084_0661   set udg_Ban_Trigger_Limited[6]='A0UH'
set udg_Ban_Unit_Limited[7]=gg_unit_h0AI_0759   set udg_Ban_Trigger_Limited[7]='A10A'
```
`Ban_Trigger[]`류와 달리 이 배열엔 **트리거 핸들이 아니라 능력ID
문자열**이 들어간다 — 잠금이 아니라 **"이 능력을 이미 획득했는가"를
검사하는 다른 용도**로 보인다(이번 조사에서 `Ban_Unit_Limited`/
`Ban_Trigger_Limited`를 읽는 자리는 못 찾았다 — `[미확인, 시간 부족]`).
⚠️ **`h0AI`(8번째)가 `PM`이 예로 든 "제한됨 −2" 결손의 그 킹**이다 —
이 배열이 결손 27칸 중 제한됨 몫과 관련 있을 가능성이 있다, 확정은
못한다. `h07I`(6번째)는 이미 다른 조사(`BLOCKERS_AND_ROSTER_DEFICIT.md`
㉢)에서 확인한 샬롯 카타쿠리다.

## ⑤ 619개 중 나머지 ~566개는 여전히 미확인

이번 조사로 확정된 건 53개(27+11+7+8)뿐이다. **나머지는 이 "Ban"
시스템에 안 걸린다** — `udg_Ban_*` 계열 배열 전체(Ban_unit/Ban_Trigger/
Ban_Unit_IM/Ban_Trigger_IM/Ban_Unit_Limited/Ban_Trigger_Limited/
Ban_Unit_ET/Ban_Trigger_ET) 리터럴을 전부 뒤졌지만 이 53개 말고
다른 유닛 핸들은 안 나왔다. **"전시/도감" 가설은 이 53개에 한해서는
확실히 기각됐지만(전시가 아니라 시스템 마커), 나머지 566개가 뭔지는
여전히 열려 있다** — 어쩌면 같은 "상태 마커" 패턴이 우리가 아직 못
찾은 다른 시스템(히든조합·다른 등급 레시피 등)에도 쓰이고 있을
가능성이 높다고 본다(같은 팀이 짠 코드라 재사용 패턴일 가능성).
다음 조사로 남긴다.

## 우리 구현에 대한 함의 (판단만, 코딩은 안 함)

**우리 `HiddenCombineManager`가 플레이어 인벤토리에서 재료를 소비하는
구조는 여전히 맞다**(어제 정정한 소유자 필터 건과 별개). 다만 이번
발견은 **완전히 새로운 축**이다 — Eternal 47종 중 27종(전체는 아니고
`Ban_Trigger` 배열에 등록된 27종만) + IM 11종 + Forever 7종은 **원작에서
"영구히 못 열릴 수도 있는" 시간제한부 콘텐츠**였다는 뜻이라, 우리가
지금 이 47+11+7종을 "언제나 조건만 맞으면 100% 열리는" 것으로
구현했다면 그 부분이 원작과 다르다. **사장님 판단이 필요한 사안**이라
판단만 남기고 코드는 안 건드린다.
