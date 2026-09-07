# `I00F`(다른세계 도박) 잠금 해제 경로 — 재구성 완료. `I00E`는 여전히 미확인

조사: 리서치 + PM 재검증 / 2026-09-08
원문: `Tools/w3x/원본/` (ORD11.089.w3x)

---

## 0. 한 줄

**`I00F`는 「어떻게 열리는가」가 완전히 재구성됐다.** 맵 중앙 유닛을 클릭해 인증 유닛을
받고, 스토리 9단계 연구를 끝내면 상점 조건이 열린다.
**다만 「어느 상점이 파는가」는 여전히 미확인** — 그 정보가 담긴 파일이 이 맵에 없다.

**`I00E`는 획득 경로가 어디에도 없다.** 리터럴 0건이 재확인됐다.

## 1. `I00F` 해제 체인 — 원문 그대로

```
① 게임 시작(Trig_Start2, war3map_new.j:3722)
     set udg_UTextLoc = GetRectCenter(gg_rct_Expansion_pack)
     call CreateNUnitsAtLoc(1, 'h06M', Player(7), udg_UTextLoc, ...)
   → 맵 중앙(rect 이름이 그대로 "Expansion_pack")에 유닛 h06M이 선다.
     h06M 이름: |cffff8200원랜디 확장팩 - 모델 팩 가동   ·   upoi(Point Value) = 500

② 플레이어가 그 유닛을 **클릭**한다
     InitTrig_Model_Pack: TriggerRegisterPlayerSelectionEventBJ(..., Player(N), true)
     Trig_Model_Pack_Conditions (3747): GetUnitPointValue(GetTriggerUnit()) == 500
   → Point Value 500이 이 유닛을 특정하는 열쇠다.

③ Trig_Model_Pack_Actions (3748)
     CreateNUnitsAtLoc(1, 'h08B', Player(N), ...)   ← 인증 증표 유닛
     "확장팩 인증 완료되었습니다!" 출력
     udg_Model_Pack[N] = true
     ⚠️ Player1~4용으로 Model_Pack / _Pack2 / _Pack3 / _Pack4 네 벌이 따로 있다(3747~3765).

④ 아이템 데이터(war3map.w3t)의 I00F 필드
     ureq = 'h08B,R02P'      ← 유닛 h08B 보유 AND 연구 R02P 완료
     R02P = "스토리:9.어인섬파괴" (war3map.w3q)
   → 이건 트리거가 아니라 **워크3 엔진 내장 요구사항 판정**이다.
     조건이 차면 상점 재고에서 구매 가능해진다.

⑤ 그 밖 I00F 필드
     igol = 3000   ·   isto = 3(재고 최대)   ·   istr = 99(재고 보충 99초)
     툴팁: "17%확률로 랜덤전용의 유닛을 얻습니다" / "99초마다 회수 1회 충전"
   → istr=99가 툴팁의 99초와 정확히 맞는다. 서로 독립적인 두 출처가 일치한다.
```

## 2. 무엇이 정정됐나

`PLAYER7_619_IDENTITY_RESOLVED.md`가 「맵 중앙 유닛은 `h08B`일 가능성이 높다」고
추정했었다. **틀렸다** — 맵 중앙에 서는 건 `h06M`이고, `h08B`는 그걸 클릭해서 **받는** 쪽이다.

## 3. `I00E`(랜덤 특수 도박) — 0건 재확인

```bash
grep -c "'I00E'" war3map_new.j        # 0
grep -c "1227894853" war3map_new.j    # 0  (FourCC 십진값)
grep -ao "I00E" war3map_new.w3u | wc -l  # 0
```

- `iabi`(내재 능력) 비어 있음 → 「먹으면 자동 발동」형도 아니다.
- `ureq` 필드 **자체가 없음** → 전제조건 없이 팔리는 물건이다.
- `utip`이 `I00D`("돈 도박 중급(W)")와 **글자 그대로 같다** — 복붙 잔재다.

📌 정황은 「`I00D`를 파는 상점이 `I00E`도 같이 팔 것」이지만 **이건 추론이지 확정이 아니다.**
지어내지 않고 [미확인]으로 둔다.

## 4. 왜 「어느 상점이 파는가」를 못 밝히나 — 구조적 한계

워크3에서 유닛별 판매 목록은 보통 `war3mapUnits.doo`(배치 인스턴스별 오버라이드)에 있다.
**이 맵에는 그 파일이 아예 없다.**

- `war3map.doo`(20,006바이트)는 정상 추출된다 → 아카이브 접근 자체는 멀쩡하다.
- `war3mapUnits.doo`만 없다.
- 대신 `war3map_new.j`에 `CreateUnitsForPlayer7`가 619개 `CreateUnit` 호출로 배치를
  통째로 JASS화해 놨다 → 보호 처리 과정에서 `.doo`가 제거된 것으로 **보인다**(정황).

🔴 **이건 아이템 38종 전체에 걸리는 한계다.** `I00E`/`I00F`만의 문제가 아니다.
`AddItemToStock` 계열 호출이 스크립트 전체에 **0건**이므로, 상점 재고는 전부 정적 데이터고
그 데이터가 우리에게 없다.

## 5. 재현

```bash
cd Tools/w3x/원본
grep -c "'I00E'" war3map_new.j                    # 0
grep -c "'I00F'" war3map_new.j                    # 0
grep -n "gg_rct_Expansion_pack" war3map_new.j     # 3235(정의) · 3722(사용)
grep -n "Trig_Model_Pack_Conditions" war3map_new.j # 3747
```
