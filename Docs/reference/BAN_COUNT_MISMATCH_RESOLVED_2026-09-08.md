# 밴 개수 불일치 47 / 45 / 53 — 닫았다. 셋이 **다른 것을 세고 있었다**

조사: PM + 리서치 / 2026-09-08
원문: `Tools/w3x/원본/war3map_new.j` (ORD11.089.w3x)

---

## 0. 한 줄

**47과 53은 서로 다른 대상의 정답이라 둘 다 맞다. 45만 오답이다**
(53을 계산하던 중간 초안에서 「제한됨 8종」이 빠진 것).

## 1. 원작 실측 — 직접 셌다

```bash
grep -oE "function Trig_Eternal_[A-Za-z0-9_]*_Actions" war3map_new.j | sort -u | wc -l   # 28
grep -oE "function Trig_Forever_[A-Za-z0-9_]*_Actions" war3map_new.j | sort -u | wc -l   #  8
grep -oE "function Trig_IM_[A-Za-z0-9_]*_Actions"      war3map_new.j | sort -u | wc -l   # 11
grep -oE "function Trig_Hidden_[A-Za-z0-9_]*_Actions"  war3map_new.j | sort -u | wc -l   # 23

grep -oE "set udg_Ban_Trigger\[[0-9]+\]="         war3map_new.j | sort -u | wc -l   # 27
grep -oE "set udg_Ban_Trigger_ET\[[0-9]+\]="      war3map_new.j | sort -u | wc -l   #  7
grep -oE "set udg_Ban_Trigger_IM\[[0-9]+\]="      war3map_new.j | sort -u | wc -l   # 11
grep -oE "set udg_Ban_Trigger_Limited\[[0-9]+\]=" war3map_new.j | sort -u | wc -l   #  8
```

리서치가 낸 값과 PM이 다시 센 값이 **전부 일치**했다.

## 2. 세 숫자의 정체

| 숫자 | 세는 대상 | 계산 | 판정 |
|---:|---|---|---|
| **47** | 채팅언락 **레시피** (채팅을 쳐서 유닛을 받는 것) | Eternal 28 + Forever 8 + IM 11 | ✅ 맞다 |
| **53** | 밴 시스템 **잠금 후보 슬롯** | 27 + 11 + 8 + 7 | ✅ 맞다 |
| **45** | — | 27 + 11 + 7 (제한됨 8 누락) | 🔴 오답(중간 초안) |

## 3. 왜 47 ≠ 53인가 — 이유가 셋이다

**① 밴 후보에서 의도적으로 빠진 2종.**
`Eternal_Nika`와 `Forever_uta`는 채팅언락 레시피로는 존재하지만 밴 배열에는 안 들어간다.
그래서 28 → 27, 8 → 7이 된다. **판마다 절대 안 잠기는 두 개**라는 뜻이다.

**② 「제한됨」 8종은 채팅언락이 아예 아니다.**
이건 트리거가 아니라 **능력 rawcode**(`'A01Y'` 등)를 `SetPlayerAbilityAvailableBJ`로
플레이어 4인에게 각각 상점 재고 불가 처리하는 **완전히 다른 메커니즘**이다.
47에는 애초에 포함될 수 없고, 53에는 포함된다.

**③ 히든조합 23종은 양쪽 어디에도 없다.** 재료 조합 확인용이라 별도 진입점이다.

```
채팅언락 트리거 70 = Eternal 28 + Forever 8 + IM 11 + Hidden 23
                    └──────── 47 (ChatUnlockManager 관리) ───┘  └ 별도 ┘

밴 후보 슬롯 53 = 27(Nika 제외) + 11 + 7(uta 제외) + 8(능력잠금, 채팅언락 아님)
```

## 4. 45는 어디서 왔나

`BAN_SYSTEM_READINESS_2026-09-07.md` 78·92행의 「PM 1차 전달(리서치 초안)」이다.
그 시점엔 「제한됨」 8종의 정체를 아직 못 찾았다. **같은 문서 88~100행에서 저자가
스스로 53으로 갱신했다** — 즉 이미 정정된 값인데 그 흔적이 남아 세 번째 숫자처럼 보였다.

📌 이건 [[docs-are-not-evidence]]의 변형이다. 「문서에 세 숫자가 있다」가 아니라
「한 숫자가 정정 전후로 두 번 적혀 있다」였다. **정정 이력을 지우지 않는 습관의 대가**이고,
그 자체는 옳다 — 다만 다음 사람이 셋 다 살아있는 값으로 오해하지 않게 여기서 못박는다.

## 5. 반영

- `ChatUnlockManager.cs` 주석의 「47종」은 **맞다**. 다만 「+ Nika 1행」이라는 부연은
  착오다 — `Eternal_Nika`는 이미 Eternal 28 안에 있고, 밴에서 빠지는 쪽이지 더하는 쪽이 아니다.
- 자산을 만들 때 **채팅언락은 47개, 밴 후보는 53개**로 각각 맞춘다. 두 수를 억지로
  일치시키려 하면 안 된다.
- ⚠️ 53을 「상위 등급 로스터 전체」로 쓰면 안 된다 — `PLAYER7_619_IDENTITY_RESOLVED.md`
  210~238행이 이미 「잠금 위험군 서브셋일 뿐」이라고 못박아 뒀다.
