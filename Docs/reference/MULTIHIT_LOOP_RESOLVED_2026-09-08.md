# 다단히트 [미확인] 117건 — 전수로 닫았다. 새로 찾은 과소 2건

조사: PM / 2026-09-08
도구: `Tools/classify_multihit_loops.py` (이 문서의 모든 수치를 재현한다)
원문: `Tools/w3x/원본/war3map_new.j` (ORD11.089.w3x)

---

## 0. 한 줄

**자기루프 70건을 전부 갈랐다. 진짜 반복 피해는 3건뿐이고, 그중 2건이 우리 쪽에서
과소였다**(키자루 3배, 뱌쿠야 32배). 나머지 67건은 발사체 이동이거나 단발이다.

`MULTIHIT_TYPE_C_SURVEY_2026-09-07.md`의 「117건 [미확인]」을 닫는다.

## 1. 왜 지난번엔 못 갈랐나 — 두 가지가 겹쳐 있었다

**① 조건이 헬퍼 함수에 숨어 있다.**
이 맵의 JASS는 스테이지 판정을 `_FuncXXXC` 헬퍼로 빼놨다. 액션 함수 본문만 보면
`Stage==N`이 **한 줄도 없다** — 정답 케이스인 `Tasigi_03`이 정확히 그랬다.

```jass
function Trig_Tasigi_03_Actions ...
  if(Trig_Tasigi_03_Func003C())then      ← 여기엔 Stage가 안 보인다

function Trig_Tasigi_03_Func003C ...
  if(not(s__TrigVariables_Stage[GlobalTV]==2))then return false endif
  return true                             ← 조건은 여기 있다
```

→ 도구가 이 헬퍼를 **본문에 인라인**한다. 이것만으로 후보가 25건 → 70건이 됐다.

**② `else`를 안 보면 결론이 정확히 뒤집힌다.** 🔴 이게 제일 컸다.

```jass
if integerA<=10 then
    ... SleepForStage(...,4)      ← 루프는 여기
else
    ... RRD(...)                  ← 피해는 여기 (루프를 빠져나온 뒤 한 번)
endif
```

`else`를 구분 안 하면 "루프 안에서 10번 반복"으로 읽힌다. **실제로는 한 번이다.**
실측: 후보 넷 중 **셋**이 이 모양이었다.

## 2. 결과 — 자기루프 70건

| 판정 | 건수 | 뜻 |
|---|---:|---|
| 반복피해 | 3 | 루프 안에서 매 회 때린다 — `hitCount` 축이 필요하다 |
| 단발피해 | 4 | 루프는 대기·연출이고 피해는 빠져나온 뒤 한 번 |
| 발사체이동 | 11 | 위치만 갱신하며 날아간다 |
| 판정불가 | 52 | 피해도 이동도 없다(버프·이펙트·그룹 처리 등) |

### 반복피해 3건 — 실제 횟수까지 확정

| 트리거 | 루프 조건 | 실제 반복 | 우리 값 | 조치 |
|---|---|---:|---:|---|
| `Trig_Tasigi_03_Actions` | `integerB < 11+능력레벨` | **11** | 11 | 이미 맞음(`35652b7`) |
| `Trig_Kizaru_01_Actions` | `integerB<6`, RRD는 `B%2==0`일 때만 | **3** | 1 | 🔴 **1→3** |
| `Trig_byakuya_Chan_Actions` | `integerA>=33`이면 탈출 | **32** | 1 | 🔴 **1→32** |

- 키자루: B가 1~6을 도는 동안 짝수(2·4·6)에서만 RRD → 3회. 간격 0.09초.
- 뱌쿠야: A가 1~32에서 RRD, 33에서 탈출 → 32회. 간격 0.12초.
  ⚠️ 같은 자산의 `Byakuya_R#1`(5,000,000)은 **다른 트리거**라 1이 맞다 — 안 건드렸다.

두 간격 모두 `MULTIHIT_INTERVAL_SPEC`의 「0.02~0.25초는 실질 동일」 범위라 `duration=0` 유지.

### 단발피해 4건 — 우리 `hitCount=1`이 맞다

| 트리거 | 루프의 정체 |
|---|---|
| `Trig_Dragon_Skill_1_T_Actions` | 6틱 타이머. RRD는 `integerA==2`일 때만 → **한 방** |
| `Trig_Kick_1_Actions` | 10틱 대기. RRD는 `A<=10` **아닐 때**(탈출 후) |
| `Trig_Jimbe_Actions` | `realA`가 1.10에 닿을 때까지 램프. RRD는 닿은 뒤 |
| `Trig_Snake_3_Kingkobra_Actions` | 발사체 비행. RRD는 도착한 뒤 |

⚠️ 카운터 상한만 읽으면 이 넷이 각각 6·10·—·— 로 부풀려진다. `else`와
「카운터 특정값 게이트」를 안 가르면 **넷 다 틀린다.**

## 3. 판정불가 52건은 무엇이었나 — 호출 빈도로 확인했다

52건에서 실제로 불리는 함수를 세어 보니 **소환·연출 루프**다. 피해 함수가 없는 게 맞다.

```
235 Setinteger          171 CreateNUnitsAtLoc   153 SleepForStage
116 SetlocationAutoRemove  99 Setunit            97 Setreal
 82 Flush                80 KillUnit            64 SetUnitAnimationByIndex
 56 UnitAddAbilityBJ     40 SetUnitScalePercent 37 ForGroupBJ
```

`CreateNUnitsAtLoc`(171회) + `UnitAddAbilityBJ`(56회) + `KillUnit`(80회) 조합은
**더미 유닛을 만들어 능력을 붙이고 곧 지우는** 패턴이다 — 원작이 광역기·채널링을
구현하는 표준 방식이고, 우리 쪽에는 이미 `더미채널_*` 자산 계열로 별도 축이 있다.

🔴 **따라서 이 52건의 피해는 「없다」가 아니라 「다른 축에 있다」**. 소환된 더미가
자기 능력으로 때리는 경로다. `hitCount`(같은 RRD를 N번 반복)와는 다른 물건이므로
이 조사의 결론(반복 피해 3건)은 그대로 유효하다.

## 4. 남는 한계 — 솔직히 적는다
- 도구는 **자기루프(같은 스테이지로 되돌아감)**만 본다. 여러 스테이지를 순회하며
  매 바퀴 때리는 구조가 있다면 못 잡는다.
- `RRD`·`UnitDamageTarget` 말고 다른 경로로 피해를 주는 자리가 있으면 못 잡는다
  (`DAMAGE_CALLS` 상수에 추가하면 된다).

## 5. 재현

```bash
python3 Tools/classify_multihit_loops.py --csv out.csv
```

원본은 `Tools/w3x/원본/`에 있어야 한다(gitignore 대상).
