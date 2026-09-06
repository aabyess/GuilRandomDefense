# e0IX/e018/e0HP 매핑 확정 + A0BA 35% 재확인 (Documents 접근불가로 임시 /private/tmp 보관)

## A0BA 35% 재확인 - 정정없음
Trig_unique_sell3_Actions 전문, Func008C: GetRandomPercentageBJ()<=35.00
다른 조건 없음. 35% 맞음.

## e0IX="랜덤위습" / e018="흔함선택위습" / e0HP="흔함영웅위습"
e0IX: 범용 소모자원. 라운드소득/판매보상 대부분/Tech_union/A0LZ 소모재료.
  "랜덤"은 등급가챠 아니라 "범용/미지정" 뜻. Wisp_흔함 대응 맞음.
e018: e0IX와 별개, unique_sell5(해적선)·일부 스토리보상 전용.
e0HP: 위습 아님. Trig_Random_BaseHero_Actions 소모시
  udg_hero_common[1..21] 21종 흔함영웅 중 랜덤1 확정지급 + Random2풀 보너스1.
  "스타터 영웅 뽑기 1회권".

## Trig_Start2_Actions 시작지급
CreateNUnitsAtLoc(1,'h05U',...) 별개유닛
CreateNUnitsAtLoc(4,'e0IX',...) 랜덤위습4개
CreateNUnitsAtLoc(1,'e0HP',...) 흔함영웅위습1개(스타터영웅뽑기권)
"5개"=e0IX4+e0HP1, 동일자원 5개 아님.
