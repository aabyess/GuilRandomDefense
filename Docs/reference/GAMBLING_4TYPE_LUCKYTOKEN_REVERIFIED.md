# 유닛도박 4종·행운토큰 재확인 — `GAMBLING.md` 기존 조사 원문으로 재검증

조사: 리서치담당 / 2026-09-06
요청: PM — 구현담당1이 찾은 `failureLuckyTokens` 불일치(고급=2, 중급/하급=2)
관련, "원작 쪽만" 4가지 확인. **`GAMBLING.md`에 이미 상세 조사가 있었다**
— 이번엔 그 결론을 문서만 믿지 않고 raw JASS로 다시 직접 대조했다.
전부 일치, 정정 없음.

## ① 중급·하급 유닛도박 원작에 실재하는가 — **실재한다, "초급도박 없음" 확정과는 별개 얘기**

**혼동 주의**: "초급도박 없음"(사장님 확정, `GamblingShop.cs:12`)은
**돈도박(`Money_Gemble`) 계열의 "초급"(`h06F`, 10골드 소액 gold 도박)**
을 가리킨다. **PM이 물은 "중급/하급"은 완전히 다른 계열, `Unit_Gemble`
(유닛도박) 계열이다** — 둘 다 실재하고, 우리 게임에도 이미 있다(구현
자체는 존재, 값만 문제).

원문(조건함수)으로 직접 확인:
```jass
function Trig_Unit_Gemble_0_Conditions ... GetUnitTypeId(GetTrainedUnit())=='h06B' ...  // 하급
function Trig_Unit_Gemble_2_Conditions ... GetUnitTypeId(GetTrainedUnit())=='h06C' ...  // 중급
function Trig_Unit_Gemble_3_Conditions ... GetUnitTypeId(GetTrainedUnit())=='h06D' ...  // 고급
function Trig_Unit_Gemble_4_Conditions ... GetUnitTypeId(GetSoldUnit())=='H0AW' ...     // 다른세계
```

## ② 실패 시 무엇을 주는가 — **하급·중급은 아무것도 안 준다, 고급·다른세계만 토큰**

`Trig_Unit_Gemble_0_Actions`(하급)의 실패 분기 전문:
```jass
if(Trig_Unit_Gemble_0_Func006C())then
call DisplayTimedTextToForce(...,"실패 !|r")
else
  ...성공 처리...
```
**딱 이거뿐이다 — `CreateNUnitsAtLoc`도, `h06G`(행운토큰)도, 골드/목재
환급도 없다. 완전히 빈손이다.**

`Trig_Unit_Gemble_2_Actions`(중급)의 실패 분기도 동일하게 텍스트만:
```jass
if(Trig_Unit_Gemble_2_Func007C())then
call DisplayTimedTextToForce(...,"중급유닛 도박을 실패 하셨습니다!|r")
else
  ...성공 처리...
```
**마찬가지로 빈손.**

반면 `Trig_Unit_Gemble_3_Actions`(고급)의 실패 분기:
```jass
else  // 실패
call CreateNUnitsAtLoc((1+udg_Dobak_Tech_int[...]),'h06G',...)
call DisplayTimedTextToForce(...,"돈도박에 실패하여..." (오타로 보이나 원문 그대로))
```
`Trig_Unit_Gemble_4_Actions`(다른세계)의 실패 분기도 동일 패턴:
```jass
else  // 실패
call CreateNUnitsAtLoc((1+udg_Dobak_Tech_int[...]),'h06G',...)
if(...)then
  call AdjustPlayerStateBJ(1,...,LUMBER)
  "항법효과: + 나무 1개 +행운의 토큰 1개를 돌려받습니다!"
```

**결론: 하급·중급 실패 시 행운토큰 지급 없음(빈손) — `GAMBLING.md:80`의
"토큰 지급 대상이 아닐 가능성"은 추정이 아니라 확정으로 승격해도 된다.**
`구현담당1`이 찾은 "중급/하급 `failureLuckyTokens:2`"는 원작 기준 **버그다
(원작은 둘 다 0이어야 한다).**

## ③ `Dobak_Tech_int`(도박광 항법) 적용 범위 — **고급·다른세계 둘뿐, 하급·중급은 아예 안 건드림**

`udg_Dobak_Tech_int`를 참조하는 함수 전수(파일 전체 검색):
```
InitGlobals, Trig_onedill_Tech_Actions(항법 선택 자체)
Trig_Acient_Ship_Actions        ← "고대의 배"쪽 A0OD/A0OC 도박(레일리·다른세계)
Trig_Unit_Gemble_3_*            ← 고급유닛도박(h06D)
Trig_Unit_Gemble_4_*            ← 다른세계도박(H0AW)
Trig_unique_rerole_*            ← 별도 시스템(리롤, 이번 질문과 무관, 안 팠음)
```
**`Trig_Unit_Gemble_0`(하급)·`Trig_Unit_Gemble_2`(중급) 어디에도
`Dobak_Tech_int` 참조가 없다.** 애초에 실패시 토큰 자체를 안 주니
부스트할 대상이 없다 — ②·③이 서로를 뒷받침한다.

**기본값**: `set udg_Dobak_Tech_int[i]=0`(InitGlobals, 항법 미선택 시
기본0) → 고급/다른세계 실패시 토큰 `1+0=1`개. "도박광" 항법 선택 시
`=1`로 설정(`Trig_onedill_Tech_Actions`) → `1+1=2`개. **`GAMBLING.md`의
기존 결론과 정확히 일치, 재확인 완료.**

## ④ 행운토큰(`h06G`)의 용도 — **최소 10개 조합식의 재료(원문 직접 재확인)**

`war3map.w3a`의 `acat`(조합재료) 필드에서 `h06G`를 포함하는 레시피를
전부 셌다 — **10개** 확인(예: `h02B,h03W,h06G,h01S`→?, `h06V,h07J,h042,h024,h06G`→`h09W`,
`h072,h02R,h03T,h01N,h06G`→`h0BF` 등). `GAMBLING.md`가 이미 "행운토큰을
요구하는 레시피 10개"라 적어둔 것과 정확히 일치 — **재확인 완료, 정정
없음.** ⚠️ `GAMBLING.md`가 언급한 "토큰 2개+목재3개로 스킬 업그레이드"라는
두 번째 용도는 이번 재검증으로도 확인도 반박도 못했다(w3a `acat`엔 안
걸림 — 있다면 트리거 코드 쪽일 텐데 이번엔 안 팠다) — **`GAMBLING.md`
원래 태그([추정]) 그대로 둔다.**

## 요약 — PM 질문 4개 답

| 질문 | 답 |
|---|---|
| ① 중급·하급 실재? | 실재. "초급도박 없음" 확정과는 다른 계열(그건 돈도박) |
| ② 실패 시 지급? | 하급·중급=**없음**. 고급·다른세계만 토큰 |
| ③ Dobak_Tech_int 범위? | **고급(`Gemble_3`)·다른세계(`Gemble_4`+`Acient_Ship`)만.** 하급·중급은 무관 |
| ④ 토큰 용도? | 최소 **조합식 10개**의 확정 재료(원문 재확인). 스킬업그레이드 용도는 미확인 유지 |

**구현담당1이 찾은 "중급/하급 `failureLuckyTokens:2`"는 원작 기준
결측·버그 — 원작 값은 둘 다 0(지급 자체가 없음)이다.** 사장님 확정
("실패시 행운토큰 지급"이 설계 결정이라면) 값과 원작 값이 여기서
갈리는데, 그 판단은 PM 몫으로 남긴다.
