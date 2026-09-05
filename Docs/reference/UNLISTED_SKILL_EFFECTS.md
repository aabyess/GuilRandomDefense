# 미수록 스킬 효과 상세표 — RRD 하나가 한 행 (2026-09-05, 리서치담당)

표: `Docs/reference/ORIGINAL_UNLISTED_SKILL_EFFECTS.csv` — **302행**
키: `유닛ID · 스킬트리거 · RRD순번` (**`ORIGINAL_DAMAGE_TYPING.csv`와 같은 키**)

컬럼: `등급 · 유닛ID · 유닛이름 · 스킬트리거 · RRD순번 · 계산식 · basis · multiplier · bonus ·
target · range · hitCount · attackType · damageType · gate · 축밖메모 · 판정`

## 판정 등급

| 판정 | 행수 | 뜻 |
|---|---|---|
| **확정** | **144** | `basis`·`multiplier`·`bonus`가 원문에서 분해됐다. **그대로 꽂으면 된다** |
| **축밖** | **72** | `realD`·`GetEventDamage`·버프개수 등 우리 축에 없는 항. **`multiplier`는 비웠고 `축밖메모`에 무엇인지 적었다** |
| **미확인** | **86** | `basis`는 알지만 **식 모양을 분해 못 했다.** 지어내지 않고 **`축밖메모`에 「식 안의 상수」를 원문 숫자 그대로** 넣었다 |

**등급별 행수**: 초월함 **94**(확정 37·축밖 35·미확인 22) · 랜덤전용 63 · 제한됨 36 · 불멸의 33 · 전설적인 29 · 영원한 26 · 히든 13 · 변화된 6 · 희귀함 2.
**정렬은 등급순**이라 위에서부터 초월함이다. **초월함 94행이 파일 맨 앞에 있으니 그것만 먼저 돌려도 된다.**

## 각 컬럼이 어떻게 채워졌나

| 컬럼 | 채운 방법 |
|---|---|
| `basis` | 식을 정규화(`GetUnitStateSwap(UNIT_STATE_MAX_LIFE,…)`→`MAXHP` 등, **괄호 균형으로 치환**)한 뒤 판정 |
| `multiplier`·`bonus` | `c + HP×k`, `HP×k`, `c + k×레벨`, `(HP×k)×(a+b×레벨)` 등 **여섯 꼴**을 분해. 안 맞으면 비움 |
| `target`·`range` | `ForGroup`/`GroupEnumUnitsInRangeOfLoc` 반경이 잡히면 `Enemies`+반경, 아니면 `SingleTarget`+0 |
| `hitCount` | **같은 스킬트리거 안에서 같은 식이 몇 번 나오는가** |
| `attackType` | **고친 대응 적용** — `NORMAL→Spells`, `MELEE→Normal`, 나머지는 그대로 |
| `damageType` | `UNIVERSAL→AP(방어무시)`, `NORMAL→AD`, 그 외 `[미확인]` |
| `gate` | `ORIGINAL_UNLISTED_SKILLS.csv`의 게이트를 스킬트리거 키로 붙임(확률/게이지/절대쿨) |

## ⚠️ 쓰기 전에 볼 것

1. **`판정=미확인` 86행은 `multiplier`가 비어 있다. 0으로 읽지 말 것.**
   `축밖메모`의 「식 안의 상수」가 **원문에 있는 숫자 전부**이니, 그걸 보고 사람이 정하는 게 맞다.
2. **`판정=축밖` 72행은 값을 넣을 수 없다.** `realD`(29) · `GetEventDamage`(13)가 대부분이고,
   **축 둘을 만들면 대부분 덮인다**(앞 조사 결론과 같다).
3. **`range`가 0이라고 반드시 단일 대상은 아니다** — `ForGroup` 반경을 못 잡은 경우가 섞여 있다 `[미확인]`.
   `target=SingleTarget`은 **「범위를 못 찾았다」와 구분되지 않는다.**
4. **`hitCount`는 「같은 식의 반복」이지 「연타 수」가 아니다** — 서로 다른 두 스킬이 같은 상수를 쓰면 같이 세어질 수 있다.
   앞 조사에서 **원작이 둥근 상수를 널리 재사용**하는 걸 확인했으니, **같은 트리거 안**으로 범위를 좁혀 세긴 했지만 그 한계는 남는다.
