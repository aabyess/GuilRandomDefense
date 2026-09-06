# 히든3종 = 이미 이식된 채팅언락 시스템, 같은 계열 확정 + CSV 사각지대 발견

조사: 리서치담당 / 2026-09-06
요청: PM — 「어젯밤 찾은 히든3종(Hidden_Aokiji/Eternal_Lucci/IM_dragon)이 이미 이식된
22종 히든조합·`ChatUnlockManager`와 같은 계열인가, 닫기 전에 한 번 더 보라」

## 결론 — **같은 계열 맞다. 셋 다 이미 카탈로그(`ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv`
70행)에 있다.** 단, 어젯밤 찾은 `Damage_level_Fixed` 가산 효과는 **그 CSV
70행 어디에도 안 잡혀 있다** — 새로 발견한 사각지대다.

## ① 대응관계 확정

| 어젯밤 찾은 트리거 | CSV 행 | 계열 | 우리 구현 상태 |
|---|---|---|---|
| `Hidden_Aokiji` | 39행 `Hidden,Hidden_Aokiji,aokiji / 아오키지조합,,,h041=[히든조합]쿠잔푸른 꿩` | **Hidden**(재료조합, `HiddenCombineManager`) | **이미 에셋 있음** — `Assets/Data/Recipes/히든_성탄.asset`(commandId "아오키지조합 / aokiji", 재료 3개 = 쿠잔·죠즈·모몬가, 어젯밤 원문에서 찾은 그 3종과 정확히 일치) |
| `Eternal_Lucci` | 11행 `Eternal,Eternal_Lucci,lucci tr / 검은정의를좇는하얀새,,,H08W=CP.Zero,,,396,"udg_Tech_Onedill,udg_Tech_Onedill_int,udg_one_dill"` | **Eternal**(순수채팅, `ChatUnlockManager`) | 스키마·매니저 이미 있음(`ChatUnlockCategory.Eternal`=목재0). **에셋은 아직 없음** — 47종 전부 "사장님이 유닛별 배정 나중에" 상태라 원래부터 미생성(구현 문제 아님) |
| `IM_dragon` | 64행 `IM,IM_dragon,dragon im / 폭풍을몰아오는바람,,10,h04D=몽키.D.드래곤 혁명군의 수장 - 불멸의,,,268,"udg_Tech_Onedill,udg_Tech_Onedill_int,udg_one_dill"` | **Immortal**(순수채팅, `ChatUnlockManager`) | 스키마 이미 있음(`ChatUnlockCategory.Immortal`=목재10 — **어젯밤 원문에서 찾은 "목재≥10" 조건과 정확히 일치**). 에셋 없음(위와 같은 이유) |

**메커니즘도 코드↔원문이 정확히 일치했다** — `ChatUnlockManager`의 주석 "Eternal·Forever·
Immortal 47개가 이 게이트(`Tech_Onedill`) 하나를 같이 쓴다"는 어젯밤 내가
`Trig_Eternal_Lucci_Conditions`/`Trig_IM_dragon_Conditions` 원문에서 직접 찾은
`udg_Tech_Onedill[플레이어]==false` 공유게이트와 정확히 같다. `IM_dragon`의
목재10 요구도 `ChatUnlockCategory.Immortal`의 "목재10"과 정확히 일치한다.
**지어낸 게 하나도 없었다 — 과거 조사(`ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv`,
`ChatUnlockData.cs`/`HiddenCombineData.cs`)가 이미 맞게 짜여 있었다.**

## ② 🔴 새로 발견한 사각지대 — `Damage_level_Fixed` 가산은 CSV 어디에도 없다

`ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv`의 컬럼: `계열,트리거,채팅문구,골드비용,
목재비용,결과유닛(ID=이름),세이브조건,라운드조건,화이트리스트수,1회제한플래그`
— **"부가 스탯 효과" 칸 자체가 없다.** 전체 70행에서 `Damage_level_Fixed`
문자열 검색 결과 **0건.** 이 CSV는 "채팅→유닛 지급"만 잡아냈고, 어젯밤
원문에서 직접 찾은 "같은 트리거가 추가로 `Damage_level_Fixed`를 올린다"는
**애초에 이 CSV를 만든 조사가 안 본 부분**이다 — `ChatUnlockData`/
`HiddenCombineData` 스키마에도 이 효과를 담을 필드가 없다.

**Eternal 계열 47행 전부가 똑같은 "`udg_Tech_Onedill,udg_Tech_Onedill_int,
udg_one_dill`" 태그를 달고 있다** — 이건 그 47개가 전부 "능력9개 비활성화"
서브블록을 복붙해 공유한다는 뜻인데, **같은 이유로 47개 중 다른 것들도
`Damage_level_Fixed`류 부가 가산을 갖고 있을 가능성이 있다**(Eternal_Lucci·
IM_dragon 둘 다 갖고 있었으니). **이건 이번엔 확인 안 했다** — 70개 전부의
Actions 원문을 다시 훑어야 나오는데, 오늘 밤엔 지목받은 3개만 봤다.
뿌리 ㊲("몇 점 뽑지 말라") 위반 소지가 있다는 걸 스스로 밝힌다 — **일반화
안 하고 `[미확인]`으로 남긴다.**

## 결론 — PM 질문 답

1. **채팅 언락이 전부 "유닛+암구호"인가, 이 셋만 특별한가**: Eternal/Forever/
   Immortal 47종은 "유닛 존재 불필요, 순수 채팅+자원비용"(`ChatUnlockManager`
   그대로), Hidden 23종은 "재료 유닛 소모+채팅"(`HiddenCombineManager`) —
   **두 계열이 이미 원작 CSV에서부터 구분돼 있었다.** 이 셋이 특별한 게
   아니라, 내가 어젯밤 우연히 각 계열에서 하나씩 건드린 것이었다.
2. **22(23)종의 원작 대응**: `Hidden_Aokiji`가 그중 하나 맞다(`히든_성탄.asset`).
3. **`ChatUnlockManager`와 같은 메커니즘인가**: 예, Eternal_Lucci·IM_dragon
   둘 다 정확히 그 시스템(게이트 변수·자원비용까지 일치).

**지금 당장 만들 수 있는 것**: 없다 — 셋 다 "지어낼 게 없는" 상태까지 왔지만,
**`Damage_level_Fixed` 가산 자체를 두 스키마(`ChatUnlockData`/`HiddenCombineData`)
어디에도 아직 못 담는다**(필드가 없다). 사장님이 47+23종에 유닛을 배정하는
시점에 이 필드가 같이 생겨야 한다 — 스키마 추가는 코딩 영역이라 구현담당
쪽 결정 사항으로 넘긴다.
