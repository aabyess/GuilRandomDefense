# `RRD`의 4·5번째 인자는 **난수 배율**이다 — 피해 649건 중 152건이 ×1이 아니다 (2026-09-06, 리서치담당)

> **한 줄 결론**: `RRD(a,b,c,min,max,f,g)`의 실제 피해는 **`c × GetRandomReal(min,max)`**다.
> 우리가 지금까지 옮긴 값은 `c`뿐이라, **`min`·`max`가 1이 아닌 152건(23.4%)이 전부 틀린 값**이다.
>
> 바뀌는 행만 모은 표: `MULTIPLIER_CORRECTIONS.csv` (113행)

---

## ① 근거 — 원문 그대로

`war3map.j`에서 이름에 `RRD`가 들어간 함수 정의는 **단 하나**다(재정의·유사이름 0건). 전문이 4줄이라 통째로 옮긴다:

```jass
function RRD takes unit a,unit b,real c,real min,real max,attacktype f,damagetype g returns nothing
local real r=GetRandomReal(min,max)
local real r2=r*c
call UnitDamageTarget(a,b,r2,true,false,f,g,WEAPON_TYPE_WHOKNOWS)
endfunction
```

읽는 데 해석이 끼어들 자리가 없다:

1. 매개변수 이름 자체가 **`min`·`max`**다.
2. 그 둘은 **`GetRandomReal(min,max)` 한 곳에만 쓰인다.** 다른 용도로 갈 경로가 없다.
3. 결과 `r`은 **`r2 = r*c`로 곱해진다.** 더하지 않고, 반복하지 않고, 지속시간에 쓰지 않는다.
4. `r2`가 그대로 `UnitDamageTarget`의 **피해량 인자**로 들어간다. **함수 안에 루프가 없으므로 「타격 횟수」일 수 없고, 시간 관련 호출이 없으므로 「지속」일 수도 없다.**

**반증도 성립한다**: 배율이 `2.58`·`3.25`·`0.12`처럼 **소수**인 호출이 실재한다(아래 ④).
「0.12번 타격」·「2.58번 타격」은 뜻이 되지 않는다 — **횟수 해석은 데이터로 반박된다.**

> 덧붙임: `UnitDamageTarget(a,b,r2,**true**,**false**,f,g,WEAPON_TYPE_WHOKNOWS)` — `isAttack=true`, `isRanged=false`로 고정이다.
> 「엔진 피해 대응」 조사에서 쓴 전제(`ATTACK_TYPE_*`가 상성표의 어느 행을 타는가)와 어긋나지 않는다.

## ② 왜 여태 안 보였나

**497건(76.6%)이 `min=max=1.00`이라 정말로 ×1**이기 때문이다.
표본을 몇 개 열어보면 전부 `1.00, 1.00`이 나오고, 그러면 「이 두 자리는 안 쓰는 자리」로 굳는다.
**「대부분이 기본값인 인자」는 「무의미한 인자」와 겉모습이 같다** — 뿌리 ⑪(이름이 뜻이 아니다)의 반대 방향 함정이다.
값 하나를 보고 판단하지 않고 **분포를 세었어야** 했다.

## ③ 전수 분포 `[파일확인]`

`RRD` 호출 **649건 전수**. 인자가 7개가 아닌 호출 0건, `min`/`max`가 숫자 리터럴이 아닌 호출 0건 — **전부 정적으로 읽힌다.**

| 배율 | 건수 | 기대값(평균) |
|---|---|---|
| **1.00 고정** | **497** | 1.00 |
| 난수 1.0~1.5 | 86 | 1.25 |
| 난수 1.0~2.0 | 18 | 1.50 |
| 난수 0.8~1.5 | 11 | 1.15 |
| 난수 1.0~3.0 | 7 | 2.00 |
| 난수 1.0~1.44 | 5 | 1.22 |
| 고정 1.5 | 4 | 1.50 |
| 난수 1.0~2.5 | 3 | 1.75 |
| 고정 0.8 / 고정 1.25 / **고정 2.58** | 각 2 | — |
| 난수 1.0~4.0 · 0.5~2.0 · 1.0~1.25 · 1.0~1.75 | 각 1 | — |
| 고정 1.05 · 1.15 · **1.85** · **3.25** · **0.33** · **0.12** · 0.5 · 0.7 | 각 1 | — |
| **합계 배율≠1** | **152 (23.4%)** | |

## ④ 극단값 3건 — 원문 인용으로 확인

```jass
Kizaru_01#1              RRD(unitA, unitB, realD,                 3.25, 3.25, ATTACK_TYPE_NORMAL, DAMAGE_TYPE_UNIVERSAL)
Kaido_Dragon_buster#2    RRD(unitD, unitB, realD,                 2.58, 2.58, ATTACK_TYPE_CHAOS,  DAMAGE_TYPE_UNIVERSAL)
Ryougi_Skill_3_triple#4  RRD(unitA, unitB, (최대체력×…),           0.12, 0.12, ATTACK_TYPE_HERO,   DAMAGE_TYPE_NORMAL)
```

키자루는 **3.25배 과소**로, 시키의 4번째 타격은 **8.3배 과다**로 들어가 있었다.

## ⑤ 표에 무엇을 넣었나

네 표 전부에 **`rand_min` · `rand_max` · `rand_mean`**(= `(min+max)/2`, 균등분포의 기대값) 세 컬럼을 붙였다.

| 표 | 행 | 붙임 | 배율≠1 |
|---|---|---|---|
| `ORIGINAL_DAMAGE_TYPING.csv` | 694 | **694 (100%)** | 152 |
| `ORIGINAL_UNLISTED_SKILL_EFFECTS.csv` | 302 | **302 (100%)** | **79** |
| `ORIGINAL_SKILL_DAMAGE_TABLE.csv` | 208 | 122 (트리거 채널 전부) | **25** |
| `ORIGINAL_GATED_SKILLS_14.csv` | 27 | 18 (더미 7 제외, 카이도 2 수동) | **5** |
| `ORIGINAL_TRAIT_LEVEL2.csv` · `ORIGINAL_TRAIT_FLAG_CHANNEL.csv` | 26+6 | 계열 단위 표기 | 각 2 (프랑키·나미) |

`ORIGINAL_SKILL_DAMAGE_TABLE.csv`의 나머지 **86행은 「더미 유닛 + 스톡 능력 필드」 채널**이라 `RRD`를 거치지 않는다.
`[미확인]`이 아니라 **`해당없음(더미채널·RRD 아님)`**으로 적었다 — 값을 못 찾은 것과 값이 없는 것은 다르다.
`ORIGINAL_GATED_SKILLS_14.csv`의 7행도 절대쿨을 **거는 쪽** 더미라 같은 이유로 `해당없음`이다.

### 조인 키를 어떻게 검증했나

표의 `RRD순번`은 **트리거 계열 안에서 「피해를 주는 호출」을 파일 순서대로 센 번호**다.
`RRD`뿐 아니라 `UnitDamageTargetBJ`(40건)·`UnitDamageTarget`(5건)까지 함께 세야 번호가 맞는다 —
처음에 `RRD`만 세었더니 45행이 안 붙고 11행이 어긋났다.

세 종류를 다 넣고 다시 세니 `ORIGINAL_DAMAGE_TYPING.csv` **694행이 함수 이름·호출 종류·계산식 앞부분까지 694/694 전부 일치**했다.
**조인 키가 표 전체와 맞아떨어진 뒤에야** 다른 표에 값을 붙였다.

## ⑥ 구현담당1께 — 무엇을 어떻게 고치나

`MULTIPLIER_CORRECTIONS.csv` 113행이 전부다. **바뀌는 행만** 들어 있다.

**곱하는 자리가 중요하다.** `rand_mean`은 **계산식 전체 바깥**에 곱한다:

```
원작:   피해 = (multiplier × basis + bonus) × rand
잘못:   피해 = (rand × multiplier × basis + bonus)      ← bonus가 안 곱해진다
```

`bonus`가 큰 행(예: `2,250,000`)에서 이 차이가 그대로 오차가 된다.

**난수를 그대로 옮길지 기대값으로 눌러 넣을지는 사장님/PM 판단**이다. 표엔 셋 다 적어뒀다:
- 원작대로 매 타격 굴리려면 `rand_min`~`rand_max`
- 결정론적으로 가려면 `rand_mean`

## 조사 범위 공개

- `war3map.j` 8,156개 함수 전수에서 `RRD(`·`UnitDamageTargetBJ(`·`UnitDamageTarget(` 호출을 **괄호 균형을 맞춰** 인자 단위로 잘랐다(문자열 안의 괄호·쉼표 무시). 총 694건.
- `RRD` 정의는 이름에 `RRD`가 든 함수를 전부 찾아 **정의가 하나뿐임**을 확인했다. 다른 곳에서 덮어쓰지 않는다.
- **안 본 것**: `UnitDamageTargetBJ`·`UnitDamageTarget` 직접 호출 45건에는 난수 배율 인자가 **없다**(그래서 `1`로 적었다). 그 45건의 값 자체가 맞는지는 이번 범위 밖이다 `[미확인]`.
- **안 본 것**: 능력(`w3a`) 필드로 피해를 주는 더미 채널 86건은 이 배율과 무관하다. 그쪽 값은 별도 조사 대상이다.
