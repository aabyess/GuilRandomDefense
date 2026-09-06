# ① `A07L` 크로스파일 분쟁 — 해결. 레일리 것이고, 쿠마 쪽 인용은 죽은 값이다

조사: 리서치담당 / 2026-09-06(계속)

## 결론

**`A07L`은 레일리(`h03A`, `전설적인_진연서`) 것이다. 쿠마(`h030`, `전설적인_최상호`)의 인용은 오더가 안 내려가 실제로 안 돈다.**
두 유닛이 같은 능력을 "공유"하는 게 아니다 — 제작자가 같은 `AOws`(War Stomp) 템플릿을 복사해서
쿠마용(`A07K`, 200,000)과 레일리용(`A07L`, 1,000,000)을 **따로** 만들어 뒀는데, 쿠마 쪽 더미가
스폰 순서상 오더를 못 받는다.

**이미 `DUMMY_CHANNEL_GATES.csv`(20·21·22·23·28행)에 정확히 이 결론이 적혀 있었다.**
다만 두 에셋 파일의 `description` 텍스트에는 그 판정(🔴 미작동)이 반영이 안 돼 있어서
양쪽 다 "A07L을 인용한다"로만 보여 분쟁처럼 보였다.

## 근거 — 코드로 확인

`A07L`과 `A07K`는 필드가 거의 동일한 형제 능력이다(둘 다 base `AOws`, `atar` 동일, `abuf=B07H`,
`aare=500`). 값만 다르다: `A07K`(쿠마) `Wrs1=200,000`·`ahdu=0.6` / `A07L`(레일리) `Wrs1=1,000,000`·`ahdu=0.45`.

### 레일리 — `Trig_Legend7_Actions` → `e00J` → **오더가 붙는다. 돈다.**
```jass
if 시전자마나==115.00 then
    CreateNUnitsAtLoc(1,'e00J', ...)
    IssueImmediateOrder(GetLastCreatedUnit(),"stomp")   ← 생성 직후 바로 오더. e00J가 받는다.
```
`e00J.uabi = A083, A07L`. `A083`(base `Asph`)은 데이터 필드가 0개인 순수 연출이라
`stomp` 오더는 사실상 `A07L`(`AOws`)에게만 유효하다. **1,000,000이 실제로 터진다.**

### 쿠마 — `Trig_Legend6_Actions` → `e00F` → **오더가 다른 더미로 샌다. 안 돈다.**
```jass
CreateNUnitsAtLoc(1,'e0KE', ...)
IssueImmediateOrder(GetLastCreatedUnit(),"stomp")        ← 이 오더는 e0KE가 받는다(A07K, 200,000)
CreateNUnitsAtLoc(1,'e0ND', ...)
CreateNUnitsAtLoc(1,'e00H', ...)
CreateNUnitsAtLoc(1,'e00F', ...)                          ← e00F는 이 뒤에 생성됨
SetUnitTimeScale(GetLastCreatedUnit(), 1.15)              ← e00F가 받는 건 이것뿐, 오더 없음
GroupEnumUnitsInRangeOfLoc(...) → ForGroupBJ(...)         ← 진짜 피해는 이 콜백(2,450,000+)
```
`e00F.uabi = A07L` 하나뿐인데 **오더를 아예 못 받는다.** 오늘 하루 반복된 「오더가 다른 더미로 갔다」
패턴과 완전히 같다(`e08G`/`A0B3` 건과 판박이 — 실제로 `A0B3`도 `A07L`과 같은 `AOws` 형제 능력이다).

### 덤으로 하나 더 나왔다 — 쿠마의 `A07K` 인용 3건 중 1건도 재확인이 필요했다
`Trig_Legend6_Actions`의 다른 분기에서 `e00E`도 오더 없이 생성된다. 그런데 `e00E`는
**`Trig_kuma_warp_Actions`라는 별도 트리거에서 오더를 받는다**(`CreateNUnitsAtLoc(1,'e00E',...) → IssueImmediateOrder(...,"stomp")`).
→ `e00E`(`A07K`)는 **소환 경로가 둘이고, 적어도 하나(`kuma_warp`)에서 정상 작동한다.** 3건(`e00E`·`e0KD`·`e0KE`) 전부 유효.

## 무엇을 고쳐야 하나

| 파일 | 지금 상태 | 조치 |
|---|---|---|
| `전설적인_최상호`(쿠마) | `effects`에 `1,000,000`이 **들어가 있지 않다**(확인함 — `multiplier` 목록에 없음). `description`만 e00F→A07L을 인용 | **데이터 변경 없음.** description에서 그 인용을 지우거나 "🔴 미작동(오더 없음)" 표시만 추가하면 된다 |
| `전설적인_진연서`(레일리) | `description`은 e00J→A07L을 정확히 인용하는데 **`effects`에 `1,000,000`이 빠져 있다**(확인함) | 🔴 **`Wrs1=1,000,000`을 추가해야 한다.** 지금 진짜 값이 통째로 빠져 있다 |

**즉 지금은 "분쟁"이 아니라 "레일리 쪽에 값이 통째로 안 들어간 상태"다.** 최상호 파일은 손댈 값이 없다.

### 추가할 값 (레일리, `A07L`)

같은 `AOws` 형제 `A0B3`(캐번디시, 이미 `구현담당1`이 확정한 전례)와 필드가 동일한 구조이므로
그때 쓴 관례(`attackType: Hero(4)`, `damageType: AD(1)`)를 그대로 따르는 게 일관적이다.

| 필드 | 값 |
|---|---|
| `multiplier` (Wrs1) | **1,000,000** |
| `range` (aare) | 500 |
| 스턴/지속 (ahdu) | 0.45초 |
| `attackType` / `damageType` | Hero(4) / AD(1) — `A0B3` 전례 준용, [확인 요망: 표준 War Stomp 네이티브 타입을 별도로 검증하진 않았다] |
| 게이트 | `시전자 마나 == 115` (게이지형, 확률 아님 — `Trig_Legend7` 자체 게이트. `A07L`은 그 안의 추가 피해) |

⚠️ 이 1,000,000은 파일에 이미 있는 `RRD 1,200,000 × 0.8~1.5`(`Trig_Legend7_Func008A`)와 **같은 발동에 동시에 나가는 별개 효과**다(둘 다 마나==115 게이트 안에서 실행). 대체가 아니라 추가다.
