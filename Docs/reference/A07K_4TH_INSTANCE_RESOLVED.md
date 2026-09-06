# 최상호 A07K — "4번째 200,000"의 정체 (쪼개기 보류 해소용)

조사: 리서치담당 / 2026-09-07, `Tools/w3x`로 `Trig_Legend6_Actions` 원문 직접 확인.
요청: PM — `SkillData_*_최상호*.asset`에 200,000이 effects에 4번 나오는데
A07K 더미 경로는 3개(e00E/e0KD/e0KE)뿐이다. 4번째가 진짜 A07K의 4번째
경로인지, 다른 능력의 배정인지 판정.

## 결론 — **진짜 4번째 경로는 없다. `원작능력` 파일 안의 중복(오류)이다.**

## ① 원문 확인 — A07K 보유 더미는 정확히 3개, 그 이상도 이하도 아니다

`Trig_Legend6_Actions` 전문(한 줄도 안 뺐다):
```jass
function Trig_Legend6_Actions takes nothing returns nothing
if(Trig_Legend6_Func002C())then
set udg_Legend_LOC_re[6]=GetUnitLoc(GetTriggerUnit())
call CreateNUnitsAtLoc(1,'e0KD',Player(8),udg_Legend_LOC_re[6],bj_UNIT_FACING)
call SetUnitUserData(GetLastCreatedUnit(),GetConvertedPlayerId(GetOwningPlayer(GetAttacker())))
call IssueImmediateOrder(GetLastCreatedUnit(),"stomp")
call CreateNUnitsAtLoc(1,'e00H',Player(4),udg_Legend_LOC_re[6],bj_UNIT_FACING)
call CreateNUnitsAtLoc(1,'e00E',Player(4),udg_Legend_LOC_re[6],GetUnitFacing(GetAttacker()))
call RemoveLocation(udg_Legend_LOC_re[6])
else
endif
if(Trig_Legend6_Func003C())then
call SetUnitManaBJ(GetAttacker(),0.00)
set udg_Legend_LOC=GetUnitLoc(GetTriggerUnit())
call CreateNUnitsAtLoc(1,'e0KE',Player(8),udg_Legend_LOC,(GetUnitFacing(GetAttacker())+180.00))
call SetUnitUserData(GetLastCreatedUnit(),GetConvertedPlayerId(GetOwningPlayer(GetAttacker())))
call IssueImmediateOrder(GetLastCreatedUnit(),"stomp")
call CreateNUnitsAtLoc(1,'e0ND',Player(4),udg_Legend_LOC,bj_UNIT_FACING)
call CreateNUnitsAtLoc(1,'e00H',Player(4),udg_Legend_LOC,bj_UNIT_FACING)
call CreateNUnitsAtLoc(1,'e00F',Player(4),udg_Legend_LOC,bj_UNIT_FACING)
call SetUnitTimeScale(GetLastCreatedUnit(),1.15)
call GroupEnumUnitsInRangeOfLoc(udg_D_Group_Legend[6],udg_Legend_LOC,512.00,Condition(function Trig_Legend6_Func003Func011004))
call ForGroupBJ(udg_D_Group_Legend[6],function Trig_Legend6_Func003Func012A)
call RemoveLocation(udg_Legend_LOC)
else
call SetUnitManaBJ(GetAttacker(),(GetUnitStateSwap(UNIT_STATE_MANA,GetAttacker())+1))
endif
endfunction
```
이 함수가 만드는 더미는 정확히 6종(`e0KD`·`e00H`·`e00E`·`e0KE`·`e0ND`·`e00F`,
`e00H`는 두 분기 모두에서 한 번씩 총 2회 생성). `war3map.w3u`에서 이 6종의
`uabi`(정적 능력목록)를 전수 확인:

```
e0KD  uabi=A07K,Aloc,Avul   "!전설 쿠마 스턴 더미"
e00H  uabi=Aloc,Avul        "!전설 쿠마 마나스킬 더미 1"   ← 능력 없음(장식용)
e00E  uabi=A07K,Aloc,Avul   "!전설 쿠마 스턴 더미2"
e0KE  uabi=A07K,Aloc,Avul   "!전설 쿠마 스턴 더미1"
e0ND  uabi=Aloc,Avul        "!전설 쿠마 마나스킬 더미 2"   ← 능력 없음(장식용)
e00F  uabi=A07L,Aloc,Avul   "!전설 쿠마 마나스킬 더미"     ← A07L(이미 죽은 참조로 확정됨)
```

**A07K를 가진 더미는 `e0KD`·`e00E`·`e0KE` 딱 3개뿐이다.** `e00H`/`e0ND`는
능력이 아예 없고(`Aloc`/`Avul`은 로커스트류 패시브, 데미지 없음),
`e00F`는 A07L을 갖지만 오더를 못 받아 원작에서도 안 도는 죽은 참조라는
점은 이미 `A07L_A07K_DISPUTE_RESOLVED.md`(2026-09-06)에서 확정된 그대로다.
**원문 어디에도 A07K의 4번째 경로는 없다.**

## ② 3개는 이미 다른 두 파일에 정확히 1:1로 잘 들어가 있다

- `SkillData_더미채널_전설적인_최상호_1.asset`: effects 2개, 둘 다
  `damageType1/attackType4/multiplier200000.0` — 설명문에 `e00E`+`e0KD`
  두 경로가 정확히 명시돼 있고 값도 정확히 2개. **정상.**
- `SkillData_더미채널_전설적인_최상호_2.asset`: effects 1개,
  `damageType1/attackType4/multiplier200000.0` — 설명문에 `e0KE` 경로
  하나만 명시, `e00F`(A07L)는 이미 "제거했다"고 명시적으로 정정되어 있음.
  **정상.**

**즉 3개의 A07K 경로는 이미 두 더미채널 파일에 합쳐서 정확히 3건
(2+1) 들어가 있다 — 이 부분은 손댈 필요가 없다.**

## ③ 문제는 세 번째 파일, `SkillData_원작능력_전설적인_최상호.asset` 안에 있다

이 파일의 effects 목록(레벨1, 14개 항목)을 순서대로 세어보면:

| # | damageType/attackType | multiplier | 비고 |
|---|---|---|---|
| 1~3 | 2/7 | 2,450,000 / 0.04 / -0.04 | 라이프-스케일 트리플릿 A |
| 4~6 | 1/1 | 2,450,000 / 0.04 / -0.04 | 라이프-스케일 트리플릿 B |
| **7** | **1/4** | **200,000** | A07K값(중복 1) |
| 8~10 | 1/1 | 2,450,000 / 0.04 / -0.04 | 라이프-스케일 트리플릿 C |
| **11** | **1/4** | **200,000** | A07K값(중복 2) |
| **12** | **1/4** | **200,000** | A07K값(중복 3) |
| **13** | **1/4** | **200,000** | A07K값(중복 4) |
| 14 | 1/4 | 1.0 | 정체불명(200,000 아님, 별건) |

**이 파일 안에만 200,000/attackType4 항목이 4개(#7,11,12,13) 있다.**
그런데 원문에서 확인되는 "Trig_Legend6 자신의 직접 효과"는 A07K와
무관한 **단 하나의 RRD 호출**뿐이다:
```jass
function Trig_Legend6_Func003Func012A takes nothing returns nothing
call RRD(GetAttacker(),GetEnumUnit(),
    (2450000.00+((GetUnitStateSwap(UNIT_STATE_MAX_LIFE,GetEnumUnit())
        -GetUnitStateSwap(UNIT_STATE_LIFE,GetEnumUnit()))*0.04)),
    1.00,1.00,ATTACK_TYPE_NORMAL,DAMAGE_TYPE_UNIVERSAL)
endfunction
```
이건 `ForGroupBJ`로 **딱 한 번만 호출**되고(트리플릿 A/B/C처럼 3벌이
아니라 1벌), A07K와는 완전히 별개의 능력(직접 RRD, 더미도 안 거침)이다.
**Trig_Legend6 자신이 200,000짜리 값을 직접 내는 자리는 원문 어디에도
없다.**

## 최종 판정

**4번째 200,000은 A07K의 진짜 4번째 경로가 아니라, 이미 다른 두
`더미채널` 파일에 들어있는 A07K 값이 `원작능력` 파일 안에 실수로
중복 삽입된 것이다.** 정확히는 "중복이 4개"가 아니라 — **`원작능력`
파일 안에는 A07K 값이 원래 하나도 있으면 안 된다**(3개 다 이미
더미채널 파일 몫이므로). 지금 이 파일 안의 4개(#7,11,12,13)는 전부
제거 대상이다.

**부수 발견(같은 파일 안의 또 다른 과잉)**: 라이프-스케일 트리플릿
(2,450,000/0.04/-0.04 세트)도 원문엔 단 1벌만 있는데 이 파일 안엔
3벌(#1-3, #4-6, #8-10)이 들어있다 — damageType/attackType 조합만
다르게 3번 반복된 것으로 보인다(2/7 한 번, 1/1 두 번). 이건 이번
요청(A07K 4번째 값)의 범위 밖이라 판정만 기록해두고 정리는 별도
확인 후로 미룬다 — **추측으로 "이것도 지워라"라고 단정하지 않는다.**
필요하면 다음 조사로 넘길 것.

`#14`(1.0/attackType4)도 200,000이 아니라 값 자체가 다르므로 이번
질문(200,000이 4번 나오는 문제)의 대상이 아니다 — 정체는 미확인으로
남긴다.

## 구현담당에게 넘길 것

`SkillData_원작능력_전설적인_최상호.asset`의 effects에서
`damageType1/attackType4/multiplier200000.0` 4건(#7,11,12,13)을
전부 제거 — A07K는 이미 `더미채널_전설적인_최상호_1`(2건)과
`_2`(1건)에 정확히 배선되어 있으므로 이 파일엔 필요 없다(중복 제거,
데이터 추가 아님). 나머지(트리플릿 3벌 과잉, #14 정체)는 이번
쪼개기 보류 해소와 무관하니 별건으로 남긴다.
