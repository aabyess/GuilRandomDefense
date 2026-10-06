# 선술집 쌍여닫이 문 (blender, 10-06) — 정본 Tools/blender/gen_tavern_door.py
- door_left.png / door_right.png: 1920×2160 정면(각 짝이 화면 절반 960×1080의 2배), 투명 배경(짝이 프레임을 꽉 채움). 널판 틈으로 따뜻한 주황 불빛이 비친다. 경첩은 바깥, 손잡이 고리는 가운데 쪽.
- door_{left,right}_45 / _75.png: 안쪽(뒤)으로 젖힌 각도 렌더(경첩 축 회전, 같은 1920×2160 프레임에 경첩 변을 고정).
- door_gap_light.png: 1920×2160 가운데 세로 빛줄(문 전체 폭 기준 가운데), 가산합성(Additive) 권장.
- preview.png: 한눈 보기.
## 효과음 (전부 Kenney CC0 — sfx/License_*.txt)
열림: open_doorOpen_1(0.92초)·open_doorOpen_2(1.41초)·open_creak1(0.66)·creak2(0.83)·creak3(0.34, 짧은 삐걱)
닫힘: close_doorClose_1(0.68)·_2(0.61)·_3(0.71)·close_thud_impactWood_heavy_001(0.31, 쿵 보강용으로 겹쳐도 됨)
🔴 소리는 못 들어서 이름·길이로 골랐다. freesound는 계정 없이 CC0 확인이 어려워 Kenney만 썼다.
