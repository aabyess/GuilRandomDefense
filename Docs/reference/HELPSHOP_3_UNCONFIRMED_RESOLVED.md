# ③ 도움소 미확인 셋 — 전부 해결

조사: 리서치담당 / 2026-09-06(계속)

## ①·③ 해루석 「초당 2,500,000」과 targetKind — **둘 다 확정. 우리 값이 맞았다**

### 「초당 2,500,000」은 필드에 있었다 — 내가 놓쳤던 것이다

`A0ID`(해루석, base `Amls`)를 오늘 아침 처음 훑을 때 데이터 필드를 **대문자로 시작하는 필드만**
걸러서 봤다(`Wrs1`·`Htc1`처럼). 그런데 `Amls`의 필드는 **`mls1`로 소문자**다 — 내 필터가 놓쳤다.

```
A0ID.mls1 = 2,500,000.0
```

**툴팁의 「초당 2,500,000데미지」와 정확히 일치한다.** 우리 `SupportSkill_해루석.asset`의
`damageBase = 2,500,000`은 **툴팁을 베낀 게 아니라 원작 필드값 그대로였다.** `[미확인]` 태그를 지워도 된다.

> ⚠️ 오늘 하루 반복된 함정과 같은 종류다 — **필드 이름의 대문자 관례를 가정하지 말 것.**
> base마다 접두 3글자가 다르고(`Wrs`·`Htc`·`Ucs`·`mls`…), 대소문자도 통일돼 있지 않다.

### targetKind = **단일이 맞다. Ground AoE로 바꾼 게 틀렸다**

```jass
Trig_Absolb1_Func003C:  GetSpellAbilityId() == 'A0ID'
  → UnitDamageTargetBJ(시전자, GetSpellTargetUnit(), 최대체력×0.07, CHAOS, UNIVERSAL)
```
`GetSpellTargetUnit()` — **단일 대상**이다. `ForGroupBJ`도 `GetUnitsInRangeOfLoc`도 없다.
`Amls`가 네이티브 바인딩 계열 능력이라 지속 피해(`mls1`)도 **같은 그 대상 하나에게만** 걸린다
(네이티브 효과가 별도로 범위를 갖는다는 근거가 전혀 없다 — 트리거·필드 어디에도 반경 필드가 없다).

**→ 우리 `targetKind`를 `Ground AoE`(1)가 아니라 `단일`(0)로 되돌려야 한다.**
`radius: 12`도 이 능력엔 뜻이 없다 — 지워도 된다.

## ② 버스터콜 7,000,000 — **레벨과 무관하게 고정값이다**

`A0JR`의 레벨별 필드를 전부 봤다. **레벨 1↔2로 바뀌는 건 쿨다운(`acdn` 100→66)과 마나(`amcs` 500→333)뿐이다.**
피해 관련 필드(`Ncs1~6`)는 레벨 1·2가 **완전히 동일**하다. → **7,000,000은 레벨에 관계없이 고정값이다.**
(`AOeq` 대지진처럼 레벨2에서 `ForGroup`이 한 번 더 도는 구조가 **여기엔 없다.**)

```jass
Trig_Absolb1_Func005C: GetSpellAbilityId()=='A0JR'
  → ForGroupBJ(600범위, Trig_Absolb1_Func005Func003A)   ← RRD 7,000,000 (고정)
  → if udg_item_Bustercall_bool[플레이어]==true then
        ConditionalTriggerExecute(udg_BusterCall_Item_Triger[GetRandomInt(0,4)])   ← 별개 아이템 보너스
```

**아이템(`item_Bustercall_bool`) 보유 시 5개 중 랜덤 1개의 "추가" 트리거가 더 실행된다** —
이건 `7,000,000`을 곱하거나 늘리는 게 아니라 **완전히 별개의 보너스 효과**다(내용은 안 팠다,
아이템 시스템이라 우선순위 밖으로 판단). **필요하면 후속으로 판다.**

## 정리

| 항목 | 판정 |
|---|---|
| 해루석 초당 2,500,000 | ✅ **확정.** `mls1` 필드에 그대로 있다. `[미확인]` 태그 제거 |
| 해루석 targetKind | 🔴 **단일로 정정.** 지금 `Ground AoE`는 틀렸다. `radius` 필드 무의미 |
| 버스터콜 7,000,000 | ✅ **고정값.** 레벨은 쿨다운·마나만 바꾼다. 아이템 보너스는 별개 시스템(미조사) |
