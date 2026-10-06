# blender 인수인계 (2026-10-07 마감, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고 PM(`pm [ref]` — 이름이 둘이면 새 쪽)에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.
규칙: Assets엔 안 쓴다. 산출은 ~/GRD_*/ 와 정본 스크립트(Tools/…). 🔴 시스템 `python3`엔 PIL·numpy가 없다 → `/usr/bin/python3`(Blender 안 파이썬엔 PIL도 없음 → 후처리는 별도 `post` 모드). Blender 창(MCP)은 이번에도 꺼져 있어 전부 헤드리스(`blender -b --factory-startup`). Cycles GPU(Metal) 가능(device=GPU 설정이 스크립트에 있음).

## 오늘(10-06~07) 끝난 일 — 전부 PM에게 넘김
1. 폭탄제조 폭발 시안(gen_bomb_fx.py) — PM이 파티클 프리팹으로 게임에 넣음.
2. 유물 아이콘: 손거울은 이미 A0UF, **종이비행기 아이콘**(gen_icon_paperplane.py) 반영됨(a5b27a6db).
3. 설정 창 판(gen_settings_panel.py) · **선술집 UI 한 벌**(gen_tavern_ui.py: 하단 바·칸·단추·서랍·자원 칸) — PM이 입힘.
4. **선술집 문**: v1(gen_tavern_door.py) → v2 3D 열림 24프레임(gen_tavern_door_v2.py, 폐기됨 「외벽 만들라는 게 아니었다」) → **v3(gen_tavern_door_v3.py, door_left/right 1920×2160)** 반영됨(69abd59b1).
5. **섬 루프 첫 화면 영상**(gen_title_loop.py, 192장 8초 24fps, 포탈 소용돌이·마을 사람 12·오른쪽 선술집·횃불·바다): 완료. `~/GRD_title_loop/frames/` + `title_loop_preview.mp4`. PM이 VP8 웹엠으로 첫 화면에 붙임. **미구현: 깃발 펄럭임·구름**(깃발은 성 FBX 안에 붙어 있어 분리 필요).
6. 클릭 소품 그림(gen_title_props.py, ~/GRD_title_props/) — 「선술집 첫 화면 폐기」로 쓰임새가 바뀜. 필요하면 PM이 다시 지시.
7. **스킬 아이콘 170행**(skill_icon_map.csv, 19cf7f4c0) — PM 확인·푸시됨. 낮음 8행(정준영 웃는얼굴·조세민 프레임씌우기·박기찬 일부·이재윤 인싸…)은 뜻이 억지.
8. 폐기됨(남겨 둠): 선술집 **실내** 첫 화면 `Tools/blender/gen_tavern_title.py`(우리 유닛 스킨 손님 9명 + 벽난로·바·샹들리에, 정지 한 장까지 만들었음, ~/GRD_tavern_title/) — 사장님 「선술집 첫 화면 폐기, 섬 버전 그대로」.

## 대기 중 / 남은 일
- PM 지시 대기(새 일 없음).
- 섬 루프 깃발·구름(요청 시).
- 못 구한 아이콘 16경로(정품 War3.mpq 필요) · 효과음 46개(~/GRD_sfx) 청취 반영 · 로비 시안 2차(~/GRD_lobby_art_v2) 대체 여부 — 전부 사장님 지시 있을 때까지 보류.
- 초월 엄태웅 유물 「지배자의싸인」 아이콘 — 사장님 답 뒤.

## 함정 (오늘 새로)
- 🔴 Blender 안에서 `import PIL` 안 됨 → 렌더는 Blender, 자르기·합성·mp4는 `/usr/bin/python3 <스크립트> post <폴더>` 패턴(gen_title_loop·gen_title_props 참고).
- GPU(Metal)와 CPU 렌더를 동시에 돌려도 서로 안 막는다(GPU 작업은 빠르고 CPU 루프는 그대로 진행).
- 노드 Mix(RGBA)는 `inputs[6]`·`[7]`, 결과 `outputs[2]`. `ShaderNodeMath` 연산 이름: ARCTAN2·FRACT·FLOOR·GREATER_THAN·LESS_THAN.
- `film_transparent` + 볼륨은 알파가 부분적으로 남는다(문 v2). Blender 5.2: `scene.use_nodes`/`scene.node_tree` 컴포지터는 막힘 — 후처리는 PIL로.
- 드라이버로 노드 소켓·재질 키 가능(`sock.driver_add('default_value')`, 표현식에 `frame`). 키프레임은 `preferences.edit.keyframe_new_interpolation_type='LINEAR'`로 선형 루프.
- 유닛 FBX(Assets/Art/Units/<이름>/<이름>.fbx)의 mixamorig 뼈는 **뼈 Y축이 제각각**(어떤 건 수평) → 포즈는 뼈 축이 아니라 「관절 → 자식 관절 벡터」를 목표 방향에 맞춰 회전(gen_tavern_title.py `aim`).
- 쉘: zsh에서 `echo "== …"` 터짐 · `git commit -- <파일>` · 새 파일은 `git add -- <파일>` 먼저.
