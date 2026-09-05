# 히든 23종은 어떻게 얻는가 — **원작에 다 있다** (2026-09-06, 리서치담당)

> **결론**: **「사장님이 값을 주셔야 하는 것」이 아니다. 원작에 그대로 있다.**
> 히든 23종은 **「채팅 코드 + 재료 유닛 보유」** 하이브리드로 얻는다.
> 표: `Docs/reference/ORIGINAL_HIDDEN_RECIPES.csv` (23행, **재료 23/23 전부 확보**)

## 구조 `[파일확인]`

```jass
InitTrig_Hidden_siru:
    TriggerRegisterAnyPlayerChatEventBJ(…, "시류조합", true)     ← 채팅 코드
    TriggerRegisterAnyPlayerChatEventBJ(…, "shiryu",   true)     ← 영문 코드도 있다

Trig_Hidden_siru_Func003C  (조건):
    CountUnitsInGroup(맵 전체에서 유닛ID=='h026') >= 1     ← 재료 보유 검사
    CountUnitsInGroup(… 'h01S') >= 1
    … (재료 종수만큼 반복)

Trig_Hidden_siru_Actions:
    CreateNUnitsAtLoc(1, 'h03J', 시전 플레이어, …)         ← 결과 유닛 생성
    RemoveUnit(GroupPickRandomUnit(… 재료 …))              ← 재료 소모
```

**→ 조합표(`[조합]` 능력) 경로가 아니다.** 그래서 앞서 만든 `ORIGINAL_FUSION_RECIPES.csv`(144행)에
**히든이 한 종도 없었다** — 채널이 다르기 때문이다(뿌리 ⑨의 또 다른 얼굴).

## 규모

| 항목 | 값 |
|---|---|
| 히든 조합 트리거 | **23종** |
| 재료가 확보된 것 | **23 / 23** |
| 재료 종수 | **3종 11개 · 4종 11개 · 5종 1개** |
| 재료 개수 | **전부 각 1개** |
| 재료 소모 | **전부 소모된다**(`RemoveUnit`) |
| 고유 재료 유닛 | **60종** (총 82칸) |

**재료로 가장 많이 쓰이는 것**: `h060` **[히든]해적선 6회** · `h01T` 반 더 데켄 3 · `h02E` 죠즈 3 · `h015` 버기 마기탄 3.

### 예시

| 결과 | 채팅 코드 | 재료 |
|---|---|---|
| 아카이누(붉은개) | `아카이누조합` / `akainu` | 아카이누(희귀) + 반 더 데켄(희귀) + 킬러(특별) + 스쿼드(특별) |
| 쿠잔(푸른꿩) | `아오키지조합` / `aokiji` | 아오키지(희귀) + 죠즈(희귀) + 모몬가(희귀) |
| 발라티에 | `발라티에조합` / `baratie` | 제프(희귀) + 루피 기어세컨드(특별) + **해적선** |
| 방주 맥심 | `방주맥심조합` / `maxim` | 와이퍼(희귀) + 우솝(희귀) + 벳지(특별) + **해적선** |

## ⚠️ 재료 `h060` 「[히든]해적선」은 **유닛이 아니라 재화에 가깝다**

6개 히든 레시피의 재료이고, **에테르널 사보·포에버 미호크 레시피에도** 들어간다.
**얻는 경로가 아주 많다** `[파일확인]`:
`Trig_Random_Base1`(랜덤 뽑기) · `Trig_Acient_Ship` · `Trig_Unit_Gemble_0/2`(도박) ·
`Trig_sell_ship`(판매) · `Trig_Story_Tier5_rayleigh`(스토리 보상) · `Trig_creep_reward`(크립 보상) · `Trig_icebug_ship`
— 그리고 맵에 **3기가 미리 배치**돼 있다(`CreateUnitsForPlayer7`).

→ **해적선은 「여러 경로로 모으는 소모 재료」다.** 우리 쪽에 이 개념이 없으면 6개 레시피가 막힌다.

## 채팅 코드 목록은 **이미 있다**

`Docs/reference/ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv` **70행** — `Eternal 28 · Hidden 23 · IM 11 · Forever 8`.
**「채팅 코드로 나올 유닛」도 사장님 값이 아니라 이식 대상**이다. 이번 표의 재료만 그 표에 붙이면 히든은 완결된다.

## 조사 범위 공개

- 재료는 **조건 함수가 래퍼**라 한 홉 더 들어가야 나온다
  (`Condition(function X)` → `X`는 `GetBooleanAnd(X001, X002)`이고 **`X001`에 유닛ID**, `X002`는 소유자 검사).
  처음엔 이걸 몰라 **재료 0건**으로 나왔다.
- **안 본 것**: 재료 외의 **추가 조건**(골드·목재 비용, 라운드 제한, 1회 제한)은 이번 표에 안 넣었다 `[미확인]`.
  `ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv`에 그 컬럼이 있으니 **두 표를 트리거 이름으로 조인**하면 된다.
- 소유자 검사(`GetOwningPlayer(GetFilterUnit())==GetTriggerPlayer()`)가 붙는지는 확인했으나
  **「내 유닛만 재료로 쓰이는가」를 트리거마다 확정하진 않았다** `[미확인]`.
