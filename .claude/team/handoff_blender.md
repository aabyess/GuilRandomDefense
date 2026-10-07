# blender 인수인계 (2026-10-07 저녁 마감, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고 PM(`pm [ref]` — 이름이 둘이면 새 쪽)에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.
규칙: Assets엔 안 쓴다(반영은 PM·구현담당1/2). 산출은 ~/GRD_*/ + 정본 스크립트(Tools/blender/). 🔴 시스템 파이썬 PIL·numpy는 `/usr/bin/python3`(Blender 안엔 PIL 없음 → 렌더는 Blender, 후처리·합성은 별도 `post`/`compare` 모드). Blender 창(MCP)은 계속 꺼져 있어 전부 헤드리스(`blender -b --factory-startup`), Cycles GPU(Metal) 가능. 블리자드 그림 추출 금지(전부 새로 모델링).

## 오늘(10-07) 납품(마감: 새 일은 사장님 지시 대기, 진행 중인 일 없음) — 위치 · 정본
| 무엇 | 위치 | 정본 스크립트 | 상태 |
|---|---|---|---|
| 섬 루프 v2(첫 화면 영상: 스킨 사람 22+서 있는 5+탁자 손님 4) | ~/GRD_title_loop_v2/frames/title_0001~0192.png · title_loop_preview.mp4 | gen_title_loop.py(맨 위 CROWD/TAVERN_GUESTS 표로 스킨 바꿈, `post` 모드) | PM이 웹엠으로 첫 화면에 넣음. 깃발·구름 미구현 |
| 워크3풍 UI 17종(돌 콘솔 바 타일/높은 타일/양끝/기둥/판 틀 · 초상 아치+마스크 · 정보 띠·아이콘 칸·정보창 틀 · 인벤토리 · 명령 카드 칸·돌틀 · 상단 단추 3·자원 칸·시계 구슬·아이콘 3 · 타이머·점수판·접기·영웅 칸·막대 홈) | ~/GRD_wc3_ui/ (+ compare_*.png) | gen_wc3_ui.py + gen_ui_wc3_common.py (비교: `/usr/bin/python3 gen_wc3_ui.py compare`) | 구현담당2 승인, Assets 반입(콘솔 32장 커밋됨) |
| 창 4종(공통 창 부품·메뉴·채팅·점수판 세트) | ~/GRD_wc3_ui/ win_*·menu_*·chat_*·score_*·drawer_tab* | gen_wc3_ui_windows.py(Blender) + gen_wc3_ui_flat.py(PIL: 채팅 띠·구분선) + compare_wc3_windows.py | 구현담당2 검수 통과, 코드 반영은 콘솔 조립 뒤에 순서대로 |
| 명령 카드 아이콘 6(이동·홀딩·정지·공격·반복·판매) | ~/GRD_wc3_ui/icons/cmd_*_{128,64}.png · icons_sheet.png | gen_wc3_cmd_icons.py + gen_wc3_cmd_icons_post.py | PM에 납품, 반영은 구현담당2/PM |
| 적 출발점 포탈(지름 1, 돌판+회전용 문양) | ~/GRD_enemy_portal/ fbx·glb·텍스처 2 | gen_enemy_portal.py(`tex` 모드 → blender) | 구현담당1에게 전달(×56, 레인 4개) |
| 바다 물(색 2048·노멀 2048·얕은 띠·spec.txt) | ~/GRD_water/ | gen_water_tex.py | 구현담당1 반영 대기. ⚠️ 색만으론 안 밝다 — sea.mat _Smoothness 0.92→0.35·_BaseColor (1,1,1)가 핵심(spec.txt) |
| 버프·디버프 상태 아이콘 7종(공속·공격력·이속감소·마나·체력·팀공격력·기절, 64px+128px+시트) | ~/GRD_buff_icons/ | gen_buff_icons.py(PIL, `/usr/bin/python3`, Blender 불필요) | 구현담당2에 납품(10-07 늦게), 반입은 구현담당2 · 수정 요청 시 같은 파일명으로 다시 뽑기 |
| 문 v3·폭탄 이펙트·종이비행기·설정 판·선술집 UI 한 벌·스킬 아이콘 170행 | (이전 판 핸드오프에서 이어짐) ~/GRD_tavern_door_v3 · GRD_bomb_fx · GRD_item_icons · GRD_settings_panel · GRD_tavern_ui · Tools/skill_icons/skill_icon_map.csv | gen_tavern_door_v3 · gen_bomb_fx · gen_icon_paperplane · gen_settings_panel · gen_tavern_ui | 전부 PM이 반영 |
폐기(스크립트만 보존): gen_tavern_title.py(선술집 실내 첫 화면) · gen_tavern_door_v2.py · gen_title_props.py(클릭 소품).

## 대기 중 / 남은 일
- 구현담당2가 UI 조립 중 그림 수정 요청하면 받는다(공통: ~/GRD_wc3_ui 같은 파일명으로 다시 뽑기 — 스크립트의 PARTS/WC3W_PARTS 환경변수로 부품만).
- 구현담당1이 바다 반영 후 원작과 다르다고 하면 gen_water_tex.py(밝기·잔물결 세기 STR·h 혼합식) 조정.
- 섬 루프 깃발·구름(요청 시) · 보류: 효과음 46개(~/GRD_sfx) 청취 · 로비 시안 2차 · 못 구한 아이콘 16경로(정품 War3.mpq 필요) · 지배자의싸인 유물 아이콘(사장님 답 뒤).

## 함정 (오늘 새로)
- 🔴 `sed -i`는 macOS에서 `sed -i ''` — 안 그러면 조용히 실패해 변경이 없다(물 텍스처 때 한 번 낭비). 긴 치환은 `/usr/bin/python3` heredoc으로.
- Blender 5.2: 컴포지터 `scene.node_tree` 막힘 → 후처리는 PIL. `Principled`의 `Specular IOR Level`·`Coat Weight`·`Emission Color/Strength` 이름 사용. `ShaderNodeMix`(RGBA)는 입력 6·7, 출력 2.
- Cycles 정사영 UI 렌더: 돌 콘솔은 위에서 12° 내려다봐야 윗면이 밝게 읽힌다(gen_wc3_ui.py TILT). 틀(9-slice)은 정면 정사영 + 금속 `tube`(Curve bevel)·`plate`(2D 다각형 → 두께 판)·`ring_mesh`(바깥·안쪽 경로 띠 + Solidify) 도우미 패턴.
- 9-slice 납품은 목표×2: 조립 비교는 `s9()`가 여백(@1x)×2를 자른다(compare 스크립트 참고). 모서리 반지름이 여백보다 크면 둥근 끝이 변 조각에 걸린다 → 여백을 반지름 이상으로.
- 유닛 FBX 포즈: 뼈 Y축이 제각각이라 「관절→자식 관절 벡터」로 `aim()`(gen_title_loop.py·gen_tavern_title.py). 렌더 전에 후보 스킨을 줄 세워 눈으로 확인할 것(텍스처 빠진 하얀 몸·금빛 조개·거대 개구리 등이 섞여 있다).
- 백그라운드 렌더는 `nohup … &` + Monitor(`pgrep -f 스크립트이름`) — `run_in_background`의 완료 알림은 런처 셸 종료일 뿐 렌더 끝이 아니다.
- zsh: `echo "== …"` 터짐 · `git commit -- <파일>` · 새 파일은 `git add -- <파일>` 먼저. PM 이름이 둘이면 `pm [ref]`.

- 🔴 시스템 파이썬 스크립트는 `/usr/bin/python3 스크립트`로(`-I`를 주면 사용자 site-packages의 numpy·PIL이 안 보여 import 실패).
