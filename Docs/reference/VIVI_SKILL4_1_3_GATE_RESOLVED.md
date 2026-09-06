# 문필환(비비) `vivi_Skill_4_Mana` `#1`/`#3` — `#2`/`#4`의 진짜 배타짝이다 (ⓐ)

조사: 리서치담당 / 2026-09-07, `Tools/w3x`로 `Trig_vivi_Skill_4_Mana_Actions`
및 관련 조건함수 원문 직접 확인.
요청: PM — `#1`/`#3`이 `#2`/`#4`의 포인트값 배타짝인지(ⓐ), 아니면 무관한
자리인지(ⓑ).

## 결론 — **ⓐ다. `#1`/`#3`은 `#2`/`#4`와 같은 if/else의 반대쪽이다.**
현재 `SkillData_회수_영원_문필환_55b5cff0.asset`에 두 쌍이 **둘 다 이미
들어있는 건 맞다**(근사값으로). 문제는 자리가 없는 게 아니라, **원작에선
이 둘이 같은 RRD 호출 안에서 대상의 포인트값에 따라 딱 하나만 발동하는데,
지금 자산은 4개 effects를 전부 무조건 같이 더해서 낸다는 것**이다.

## ① 원문 — 첫 번째 RRD 호출(`#1`↔`#2`)

```jass
call s__TrigVariables_SetgroupAutoRemove(GlobalTV,0,GetUnitsInRangeOfLocMatching(925.00,...))
call s__TrigVariables_Setunit(GlobalTV,1,GroupPickRandomUnit(s__TrigVariables__get_groupA(GlobalTV)))
call s__TrigVariables_SetlocationAutoRemove(GlobalTV,1,GetUnitLoc(s__TrigVariables__get_unitB(GlobalTV)))
if(Trig_vivi_Skill_4_Mana_Func004Func018C())then
call RRD(s__TrigVariables__get_unitA(GlobalTV),s__TrigVariables__get_unitB(GlobalTV),
    ((300000.00+(GetUnitStateSwap(UNIT_STATE_MAX_LIFE,s__TrigVariables__get_unitB(GlobalTV))*0.10))
        *(1+s__TrigVariables__get_realC(GlobalTV))),
    1.00,1.00,ATTACK_TYPE_CHAOS,DAMAGE_TYPE_UNIVERSAL)
else
call RRD(s__TrigVariables__get_unitA(GlobalTV),s__TrigVariables__get_unitB(GlobalTV),
    500000.00,1.00,1.00,ATTACK_TYPE_HERO,DAMAGE_TYPE_UNIVERSAL)
endif
```

조건함수 원문:
```jass
function Trig_vivi_Skill_4_Mana_Func004Func018C takes nothing returns boolean
if(not(GetUnitPointValue(s__TrigVariables__get_unitB(GlobalTV))<200))then
return false
endif
return true
endfunction
```

**`Func004Func018C()`는 `대상의 포인트값 < 200`일 때만 `true`다.**
즉 이 if/else 자체가:
- **대상 포인트값 < 200**(일반 몹) → **`#1`**: `(300,000+대상최대체력×0.10)×(1+realC)`,
  `ATTACK_TYPE_CHAOS`(방어 무시)
- **대상 포인트값 ≥ 200**(보스급) → **`#2`**: 고정 `500,000`, `ATTACK_TYPE_HERO`

## ② 두 번째 RRD 호출(`#3`↔`#4`) — 완전히 같은 구조, 다른 대상 유닛

```jass
if(Trig_vivi_Skill_4_Mana_Func004Func031C())then
call RRD(s__TrigVariables__get_unitA(GlobalTV),s__TrigVariables__get_unitD(GlobalTV),
    ((300000.00+(GetUnitStateSwap(UNIT_STATE_MAX_LIFE,s__TrigVariables__get_unitD(GlobalTV))*0.10))
        *(1+s__TrigVariables__get_realC(GlobalTV))),
    1.00,1.00,ATTACK_TYPE_CHAOS,DAMAGE_TYPE_UNIVERSAL)
else
call RRD(s__TrigVariables__get_unitA(GlobalTV),s__TrigVariables__get_unitD(GlobalTV),
    500000.00,1.00,1.00,ATTACK_TYPE_HERO,DAMAGE_TYPE_UNIVERSAL)
endif
```
`Func004Func031C`도 `Func004Func018C`와 조건식이 완전히 동일(대상만
`unitB`→`unitD`로 다름): `GetUnitPointValue(대상)<200`. **`#3`/`#4`는
`#1`/`#2`와 완전히 같은 게이트를 두 번째 표적(랜덤으로 하나 더 뽑은
근처 적)에 대해 반복한 것뿐이다.**

## ③ `realC`(강화 배율)의 정체 — 구현담당1이 이미 정확히 짚었다

```jass
call s__TrigVariables_Setreal(GlobalTV,2,(I2R(GetUnitAbilityLevelSwapped('A0LZ',s__TrigVariables__get_unitA(GlobalTV)))*0.05))
```
`realC` = 캐스터(비비)의 `A0LZ` 능력 레벨 × 0.05. `SkillData_회수_영원_문필환_55b5cff0.asset`
설명문에 이미 정확히 적혀 있다 — **A0LZ가 우리 로스터의 "레벨 트랙형"
강화 시스템과 무관한 별도 조합-소모형 스택이라 `CasterSkillLevel`로
못 펼친다는 판단도 원문과 일치한다.** 이 부분은 재확인만 하고 새로
고칠 것 없음.

## 최종 판정 — ⓐ, 그리고 실제 필요한 수정은 "빠진 자리 채우기"가 아니라 "게이트 걸기"

**`#1`/`#3`은 `#2`/`#4`의 진짜 포인트값 배타짝이 맞다(ⓐ).** 지금 자산에
이미 근사값이 들어있는 건 맞게 짚은 것이고, 구현담당1이 추측 없이
보류한 것도 옳았다(㊹) — **다만 지금 자산의 4개 effects가 무조건 다
같이 발동하는 구조라면 그건 원작과 다르다.** 원작은:

```
RRD 호출 #1: 대상 포인트값<200 → #1(가변) : ≥200 → #2(고정500,000) — 배타
RRD 호출 #2: 대상 포인트값<200 → #3(가변) : ≥200 → #4(고정500,000) — 배타
```
**한 번의 캐스트에서 최대 2번의 RRD(대상 둘)가 나가고, 각 RRD는 그
개별 대상의 포인트값에 따라 `#1`이냐 `#2`냐(또는 `#3`이냐 `#4`냐) 중
**하나만** 골라 낸다 — 넷을 다 더하는 게 아니다.** 이건 코딩 영역이라
구현담당이 판단할 몫이지만, "자리가 비어서 다른 근사로 채웠다"가
아니라 "이미 맞는 근사가 들어있는데 게이트(대상 포인트값<200 분기)가
빠져서 넷이 항상 같이 나간다"는 게 정확한 문제 설명이다.
