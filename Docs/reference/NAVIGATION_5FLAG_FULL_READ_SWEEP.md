# 항법 5택 플래그 — 읽는 자리 전수(소비처 재확인 아니라 진짜 전수)

조사: 리서치담당 / 2026-09-07
요청: PM — 5개 플래그 각각을 참조하는 자리를 `war3map.j` 전체에서
리터럴 전수, 이미 아는 것 말고 더 있는지.

## 결론 요약

| 플래그 | 기존에 안 것 | 전수 결과 |
|---|---|---|
| `udg_Tech_No_support`(도움소 잠금) | 아이템 축소풀·`Story_reward8` | **더 없음 — 딱 이 둘뿐, 도움소 상점 개폐와는 무관** |
| `udg_Dobak_Tech_int`(도박광) | 고급·다른세계·고대의배 실패토큰 | 🔴 **새로 발견 — "희귀함 리롤"(`Trig_unique_rerole`) 시스템에도 쓰인다** |
| `udg_Damage_level_Fixed`(패왕의길) | `Round_10ver` | **더 없음 — 소비처는 정말 하나뿐, 재확인 완료** |
| `udg_Tech_union`(연합세력) | `UnitJohabCounter`(위습+1) | **더 없음 — 소비처는 정말 하나뿐, 재확인 완료** |
| 도움소 강화(`A0ID`/`AOeq`/`A0JR`) | 이미 레벨2 직접 세팅 확인 | 플래그가 아니라 즉시실행 액션이라 "읽는 자리" 개념 자체가 없음, 추가로 나올 게 없다 |

## `udg_Tech_No_support` — 전수 완료, 딱 2곳뿐

파일 전체 리터럴 검색 결과 **선언·초기화·SET(1곳)·READ(2곳)가 전부**다:
```
InitGlobals: 기본값 false
Trig_onedill_Tech_Actions: "도움소 잠금" 선택 시 true로 SET (유일한 SET)
Trig_item_Gemble_Func006C: (기존 확인) 아이템 도박 전체/축소 풀 갈림
Trig_Story_reward8_Func002Func001Func008C: (기존 확인) 마린포드 레일리 보너스
```
**PM이 의심한 "도움소 상점 자체가 안 열린다"는 근거가 없다** — 이
플래그는 딱 2곳만 읽히고, 도움소 건물의 개폐·재고를 직접 제어하는
코드는 어디에도 없다. `[[navigation-routes-full]]`(`NAVIGATION_ROUTES_FULL.md`)의
"도움소 사용 자체를 막는지는 미확인" 문항은 이걸로 답이 나온다 —
**막지 않는다, 딱 아이템풀 등급과 마린포드 보너스 조건 두 개만
바꾼다.**

## `udg_Dobak_Tech_int` — 🔴 새 소비처: "희귀함 리롤" 시스템

기존 3곳(`Trig_Acient_Ship_Actions`·`Trig_Unit_Gemble_3`·`Trig_Unit_Gemble_4`,
전부 도박 실패시 럭키토큰 개수) 외에 **`Trig_unique_rerole_Actions`
("희귀함 리롤", 능력 `A0VX` 캐스트로 발동)에서도 세 번 참조된다**:

```jass
function Trig_unique_rerole_Func001C takes nothing returns boolean
if(not(udg_Rerole_count_int[...]>=(2+udg_Dobak_Tech_int[...])))then
return false
```
→ **리롤 가능 횟수 상한이 기본 2회, 도박광 선택 시 3회로 오른다.**

```jass
function Trig_unique_rerole_Func001Func001Func005C takes nothing returns boolean
if(not(GetRandomInt(1,100)<=(20-(udg_Dobak_Tech_int[...]*80))))then
return false
```
→ **리롤 실패 확률 20%가, 도박광 선택 시 `20-80=-60`이 되어 수학적으로
`GetRandomInt(1,100)<=-60`은 절대 참이 될 수 없다 — 실패 확률이 0%로
사라진다.** (원문은 "성공/실패 텍스트"로 방향 확정 필요하나, 실패
텍스트("리롤을 실패하였습니다")가 이 조건이 참일 때 뜨므로 이 조건
자체가 "실패 판정"이 맞다 — 즉 도박광이면 리롤이 절대 실패하지 않는다.)

**전체 그림**: "희귀함 리롤"은 목재2를 내고 캐스터 유닛(희귀함 등급)을
같은 등급의 다른 랜덤 유닛으로 바꾸는 소모 능력이다. 기본은 2회
한도·매 시도 20% 실패(실패해도 목재는 이미 나감, 유닛은 안 바뀜).
**"도박광" 항법을 고르면 한도가 3회로 늘고, 실패 확률이 0%가 된다.**
**이건 어제까지 항법 조사에서 전혀 안 나왔던 효과다** — "도박광"이
도박(가챠) 계열에만 영향을 준다고 봤는데, **"리롤"이라는 별도
시스템까지 뻗어 있었다.**

### 🔴 곁다리 — 이 트리거가 `Dobak_Tech_int` 자체를 SET하는 자리도 있다

```jass
if(Trig_unique_rerole_Func001C())then  // 이미 리롤 한도 도달
call DisplayTimedTextToForce(...,"리롤회수를 소진하였습니다.")
set udg_Dobak_Tech_int[GetConvertedPlayerId(GetOwningPlayer(GetTriggerUnit()))]=1
call UnitRemoveAbilityBJ(GetSpellAbilityId(),GetTriggerUnit())
```
**리롤 한도를 다 쓴 뒤 능력을 한 번 더 쓰려고 하면, `Dobak_Tech_int`를
`1`로 SET한다.** 이건 항법 선택(`Trig_onedill_Tech_Actions`) 말고 **또
다른 곳에서 이 변수를 값 1로 바꾸는 유일한 자리**다. **원문 그대로만
보고한다 — 의도(리롤을 다 써버린 것에 대한 보상/구제책인지, 다른
목적의 변수 재사용인지)는 이번 조사로 판단 못 한다.** 확실한 건:
**항법을 "도박광"으로 고르지 않은 플레이어도, 희귀함 리롤을 한도까지
다 쓰면 이 변수가 1이 되어 이후 고급/다른세계/고대의배 도박의 실패
토큰이 2개로 오르고 리롤 한도도 늘어난다는 것**이다(변수가 공유되므로
부수효과가 자동으로 번진다).

## `udg_Damage_level_Fixed` — 재확인, 소비처 정말 하나뿐

```
선언/초기화, Trig_Round_10ver_Actions(소비, 2회=같은 함수 두 분기),
Trig_onedill_Tech_Actions(SET, 패왕의길), Trig_Hidden_Aokiji_Actions(SET),
Trig_Eternal_Lucci_Actions(SET), Trig_IM_dragon_Actions(SET)
```
**읽는 자리는 `Round_10ver` 하나뿐 — 전수 재확인, 추가 없음.**

## `udg_Tech_union` — 재확인, 소비처 정말 하나뿐

```
선언/초기화, Trig_onedill_Tech_Actions(SET, 연합세력),
Trig_UnitJohabCounter_Actions(소비, 위습+1)
```
**읽는 자리는 `UnitJohabCounter` 하나뿐 — 전수 재확인, 추가 없음.**

## "도움소 강화" — 플래그가 아니다, 스윕 대상 자체가 아님

이건 불리언 변수가 아니라 `Trig_onedill_Tech_Actions` 안에서
`SetUnitAbilityLevelSwapped('A0ID'/'AOeq'/'A0JR', udg_Manso[플레이어], 2)`를
**그 자리에서 바로 실행**하는 액션이다. "나중에 어디서 읽히는가"를
찾을 게 없다 — 효과가 즉시 반영되고 끝난다. 이미 확정한
`MANSO_LEVEL2_UPLIFT_RESOLVED.md`가 전부다.

## 결론 — 구현 쪽에 바로 넘길 것

- `Tech_No_support`: 도움소 상점 개폐엔 무관, 걱정 안 해도 된다.
- `Dobak_Tech_int`: **"희귀함 리롤" 시스템(한도 2→3, 실패확률 20%→0%)도
  같이 배선해야 완전하다.** 이 시스템 자체가 우리 게임에 없으면
  일단 그 사실부터 확인 필요(리서치 범위 밖, 구현 쪽 확인).
- `Damage_level_Fixed`/`Tech_union`: 추가 배선 없음, 기존 확인이 전부다.
