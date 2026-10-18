# S2 2.323 맵 읽기·그림 도구 (구현담당1, 2026-10-18 — 맵 작업은 보류, 산출은 ~/grd/GRD_s2_map/)
- `w3map.py` w3e(지형)·doo(장식) 파서 · `render_map.py` 위에서 본 그림(옛 판·S2) · `render2.py` 미니맵 색으로 칠한 지형 · `zones.py` 구역 지도 + 미니맵 겹쳐 보기 · `render_ours.py` 우리 씬의 섬 표(씬 YAML에서 읽음)
- `rect_use*.py` S2 war3map.j의 Rect 변수 용도 추적(CreateUnit 등) · `units_j.py` 옛 판 j의 CreateUnit 좌표 · `camdetect.py` 영상 HUD 미니맵의 카메라 박스 검출
- 입력: `~/grd/GRD_s2_map/{s2,old}/`에 풀어 둔 w3e·doo·w3d·j·미니맵 blp(Tools/w3x/mpqread.py로 추출). 경로가 `~/grd/GRD_s2_map/`로 박혀 있다. PIL은 /usr/bin/python3에 있다(venv엔 없음).
