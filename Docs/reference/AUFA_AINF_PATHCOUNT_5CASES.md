# `AUfa`/`Ainf` 5건 — 파일 안 값 중복이 진짜 중복인지 원문 경로 수로 판정

조사: 리서치담당 / 2026-09-07, `Tools/w3x`로 각 능력의 실제 소유 더미와
그 더미가 몇 번 생성되는지(경로 수), 생성될 때마다 그 더미 자신에게
오더가 실제로 내려가는지(살아있는 경로인지)까지 확인.
요청: PM — 「AUfa 3파일(정윤식 A07B·윤현모 A0CB·김영원 A0LY) + Ainf 2건
(신지우 A0ZX·김정래 A12V), 각 파일에서 값이 등장한 횟수와 원작 경로 수가
같은지」. 방법은 A07K와 동일: 경로 수 = 등장 횟수면 전부 유효(전부
제거 대상), 경로 수 < 등장 횟수면 초과분이 중복.

## 결론 표

| # | 유닛 | 능력 | 필드값 | 파일 안 등장 | 원작 경로 수(살아있는 것만) | 판정 |
|---|---|---|---|---:|---:|---|
| 1 | 정윤식 | `A07B` | `Ufa1=7.0` | 2 | **1** | 🔴 **1개 초과 — 중복, 삭제 대상** |
| 2 | 윤현모 | `A0CB` | `Ufa1=4.25` | 3 | **2** | 🔴 **1개 초과 — 중복, 삭제 대상** |
| 3 | 김영원 | `A0LY` | `Ufa1=6.0` | 2 | **1** | 🔴 **1개 초과 — 중복, 삭제 대상** |
| 4 | 신지우 | `A0ZX` | `Inf1=0.35` | 2 | **2** | ✅ **일치 — 중복 아님, 둘 다 유지(단 필드뜻 자체는 이미 "피해 아님"이라 애초에 제거 대상)** |
| 5 | 김정래 | `A12V` | `Inf1=0.45` | 2 | **1(확인) + 확정 못한 나머지** | 🟡 **1개는 확실히 초과로 보이나 완전 확정은 못함(아래 참고)** |

**모두 필드뜻 자체(`Ufa1`=절대쿨 지속시간, `Inf1`=공격력 증가율)는 이미
「피해 아님 → 제거」로 닫혀 있다.** 이 문서는 "제거해야 하냐"가 아니라
"파일 안에 몇 개가 들어있어야 정상이냐"(제거 후 몇 개가 빠져야 하는지,
혹은 앞으로 비슷한 값겹침을 볼 때 몇 개가 정상 범위인지)만 판정한다.

---

## ① 정윤식 `A07B` — 더미 `e006` 1개, 오더 살아있음 → 경로 1

```jass
call CreateNUnitsAtLoc(1,'e006',Player(8),udg_Legend_LOC_re[2],bj_UNIT_FACING)
call SetUnitUserData(GetLastCreatedUnit(),GetConvertedPlayerId(GetOwningPlayer(GetAttacker())))
call IssueTargetOrderById(GetLastCreatedUnit(),String2OrderIdBJ("frostarmor"),GetAttacker())
call IssuePointOrderByIdLoc(GetLastCreatedUnit(),String2OrderIdBJ("stampede"),udg_Legend_LOC_re[2])
```
`e006`(`uabi`에 `A07B` 보유) 생성 호출은 `war3map.j` 전체에 **이 한 곳뿐**이고,
생성 직후 `frostarmor` 오더가 자기 자신에게 바로 들어간다 — **살아있는
경로 1개.** 파일엔 2번 들어있으니 **1개가 중복이다.**

## ② 윤현모 `A0CB` — 더미 `e0D8`+`e0AB` 2개, 한 트리거 안에서 같이 생성 → 경로 2

```jass
call CreateNUnitsAtLoc(1,'e0D8',GetOwningPlayer(GetAttacker()),udg_Eternal_LOC,GetUnitFacing(GetAttacker()))
call IssueTargetOrder(GetLastCreatedUnit(),"firebolt",GetTriggerUnit())
call CreateNUnitsAtLoc(1,'e0AB',GetOwningPlayer(GetAttacker()),udg_Eternal_LOC,GetUnitFacing(GetAttacker()))
call IssueTargetOrderById(GetLastCreatedUnit(),String2OrderIdBJ("frostarmor"),GetAttacker())
call IssuePointOrderByIdLoc(GetLastCreatedUnit(),String2OrderIdBJ("carrionswarm"),udg_Eternal_LOC)
call SetUnitTimeScale(GetLastCreatedUnit(),3.00)
```
**`e0D8`와 `e0AB` 둘 다 `uabi`에 `A0CB`를 갖고 있다**(형제 더미, 같은
능력을 공유). 이 한 트리거(`Trig_Ace_Attack`) 안에서 **`e0D8`을 만들고
`firebolt` 오더 → 곧바로 `e0AB`를 만들고 `frostarmor` 오더** — 한 번
발동에 **두 더미가 동시에 같이 생성되고 둘 다 자기 오더를 받는다.**
각 더미의 생성 호출은 파일 전체에 **각 1회뿐**이라 다른 branch에서
추가로 도는 경로는 없다. **살아있는 경로 = 2(e0D8·e0AB, 동시발동).**
파일엔 3번 들어있으니 **1개가 중복이다.**

## ③ 김영원 `A0LY` — 더미 `e0D3` 1개, 오더 살아있음(2개 연속) → 경로 1

```jass
call CreateNUnitsAtLoc(1,'e0D3',Player(4),udg_PlayZoneLOC[...],bj_UNIT_FACING)
call IssueTargetOrderById(GetLastCreatedUnit(),String2OrderIdBJ("frostarmor"),GetAttacker())
call IssueTargetOrderById(GetLastCreatedUnit(),String2OrderIdBJ("bloodlust"),GetAttacker())
```
`e0D3`(`uabi`에 `A0LY` 보유) 생성은 **이 한 곳뿐**이고, 생성 직후
`frostarmor`+`bloodlust` 오더 2개가 연달아 자기 자신에게 들어간다 —
오더가 2개라고 능력이 2개가 되는 게 아니라 **같은 더미 하나가 버프를
두 개 받는 것**뿐이다. **살아있는 경로 = 1.** 파일엔 2번 들어있으니
**1개가 중복이다.**

## ④ 신지우 `A0ZX` — 더미 `e0MP` 1개인데 생성 호출은 3곳, 그중 2곳만 살아있음 → 경로 2, **일치**

`e0MP`(`uabi`에 `A0ZX` 보유) 생성 호출 3곳 전부 원문 확인:

```jass
// ㉮ Trig_Kaido_Attack 분기1
call CreateNUnitsAtLoc(1,'e0MP',Player(4),s__TrigVariables__get_locationA(GlobalTV),bj_UNIT_FACING)
call IssueTargetOrderById(GetLastCreatedUnit(),String2OrderIdBJ("innerfire"),s__TrigVariables__get_unitA(GlobalTV))
// → e0MP 자신에게 "innerfire" 오더. 살아있음.

// ㉯ Trig_Kaido_Attack 분기2(1/7 확률, B05Q버프 없을 때)
call CreateNUnitsAtLoc(1,'e0MP',Player(4),udg_Immortal_LOC,bj_UNIT_FACING)
call IssueTargetOrderById(GetLastCreatedUnit(),String2OrderIdBJ("innerfire"),GetAttacker())
// → e0MP 자신에게 "innerfire" 오더. 살아있음.

// ㉰ Trig_kaido_buff_Actions (별도 트리거)
call CreateNUnitsAtLoc(1,'e0MP',Player(4),udg_Immortal_LOC2,bj_UNIT_FACING)
call CreateNUnitsAtLoc(1,'e0MG',Player(4),udg_Immortal_LOC2,bj_UNIT_FACING)
call IssueTargetOrderById(GetLastCreatedUnit(),String2OrderIdBJ("frostarmor"),GetTriggerUnit())
// → 오더 대상은 GetLastCreatedUnit() = 방금 만든 e0MG다, e0MP가 아니다!
//   e0MP는 이 branch에서 오더를 못 받는다 — 죽은 인스턴스.
```
**㉮·㉯는 살아있고 ㉰는 죽었다(오더가 뒤에 만든 다른 더미 `e0MG`로
간다).** 살아있는 경로 = **2.** 파일에도 2번 들어있으니 **정확히
일치한다 — 중복이 아니다.** (필드뜻 자체는 이미 "피해 아님"으로
닫혀 있어 둘 다 결국 제거 대상이지만, "몇 개가 정상이냐"의 답은 2다.)

## ⑤ 김정래 `A12V` — 소유 유닛 `h076`, 확정 경로 1 + 확정 못한 나머지

```jass
function Trig_Bugi_Attack_Actions takes nothing returns nothing
if(Trig_Bugi_Attack_Func001C())then
set udg_Eternal_LOC=GetUnitLoc(GetAttacker())
call CreateNUnitsAtLoc(1,'h076',GetOwningPlayer(GetAttacker()),udg_Eternal_LOC,GetUnitFacing(GetAttacker()))
call UnitApplyTimedLife(GetLastCreatedUnit(),'Bmec',4)
call RemoveLocation(udg_Eternal_LOC)
else
endif
```
`h076`(base `hrif`, 이름 "[히든조합]엠포리오 이완코브...소환됨")이
`uabi`에 `A12V`를 갖는다. `'A12V'` 리터럴 자체는 `war3map.j` 전체에
**0건**(오더로 캐스트되는 스킬이 아니라 `Ainf`류 오라형 패시브라
오더 없이 유닛이 존재하기만 하면 자동 적용되는 것으로 보인다). 동적
생성 호출은 이 한 곳뿐 — **확인된 살아있는 경로 = 1.**

**하지만 완전히 확정은 못한다** — `h076`은 어제 조사한
`PLAYER7_NEUTRAL_POOL_CENSUS.md`의 619개체 사전배치 풀에도 이미
등장하는 유닛타입이다. 즉 **`war3mapUnits.doo`(맵 배치 원본, `Tools/w3x`로
못 꺼냄 — PM도 이미 확인한 한계)에 정적으로 몇 기가 더 배치돼 있을 수
있고, 그건 이 방법으로 못 센다.** 그래서:

**판정: 동적 생성 경로는 확실히 1개다. 파일엔 2개 들어있어서 최소
1개는 초과로 보이나, 정적 배치분이 있을 가능성 때문에 "100% 확정"은
못 한다 — `[경로 1 확인, 완전확정 아님]`으로 남긴다.** 필드뜻 자체가
이미 "피해 아님"이라 어차피 전부 제거 대상이므로, 실무적으로는
①~③과 같은 방식(초과분 정리)으로 처리해도 큰 위험은 없다고 본다.

## 요약 — 구현담당1에게

| 파일 | 정상 개수 | 지금(중복 포함) | 지울 개수 |
|---|---:|---:|---:|
| 정윤식(`A07B`) | 1 | 2 | 1 |
| 윤현모(`A0CB`) | 2 | 3 | 1 |
| 김영원(`A0LY`) | 1 | 2 | 1 |
| 신지우(`A0ZX`) | 2 | 2 | 0(중복 아님, 그대로) |
| 김정래(`A12V`) | 1(확정) | 2 | 1(권장, 완전확정은 아님) |

**5건 다 필드뜻은 이미 "피해 아님"으로 닫혀 있으므로, 결국 5개 파일
전부에서 이 값들은 사라져야 한다** — 이 문서는 "몇 개가 원래도
정상이었는지"만 답한 것이고, 최종 삭제 대상은 5건 각각의 등장 횟수
전부(2+3+2+2+2=11개)다.
