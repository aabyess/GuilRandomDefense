# blender 인수인계 (2026-10-06 저녁, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고, 다 읽은 뒤 PM에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.
규칙: Assets엔 안 쓴다. 산출은 정본 스크립트(Tools/…)와 ~/ 작업 폴더. 시스템 `python3`엔 PIL·numpy·yaml이 없다 → 🔴 `/usr/bin/python3`(`-I`를 주면 사용자 site-packages가 막혀 numpy를 못 찾는다 — 낯선 폴더가 cwd일 때만 `-I`, 아니면 cwd를 `/tmp`로). Blender 창(MCP)은 안 썼다(전부 헤드리스 `blender -b --factory-startup`).
PM 이름이 둘이면(`pm [ref]`) 새 쪽(ref가 PM 메시지에 적힌 것)으로만 보낸다.

## 오늘 한 것 (10-06) — 커밋 해시
1. **스킬 아이콘 표**(Tools/skill_icons/): `map_skill_icons.py` 재실행 → `skill_icon_map.csv` 744행(에셋 전부 1행), png 연결 686. 6514f9b02 → 2faa0d0ef.
   - 사장님 신규 초월 스킬·공용 디버프 4: 원작 대응 없어 효과에 맞는 웹 표준 BTN을 손으로 지정(`STD_MANUAL`, 확신도 「낮음」). 원작 `A0GQ`(PASBTNShadeTrueSight, 못 구함)는 BTNShade로 임시.
   - 못 구한 16경로(39행, `Model\BTN…` — 원작 MPQ에 없음, 이름·접두어·확장자 변형 다 시도): `FALLBACK_STD` 표준 BTN 임시 대체. 목록 `missing_icons_not_in_map.md`(83a4a0541).
   - 아직 빈 58행(「아이콘없음」: 더미채널·대표 능력 없는 게이트/회수): 기본 그림 `std_btnspellbookbls.png`, 이름은 `default_icon.txt`. 구현담당2에게 알렸다(Link 때 쓰기).
2. **히든_전유라 Move 이음새** be8375f9c: `fix_unit_fbx.py`에 `loop_clips`(자르지 않고 한 클립 끝 k프레임을 첫 자세로 당김) + 변형 `히든_전유라@동작_이음새`. Move 이음새 0.0658→0.0000m. 산출 `~/GRD_motion_trial/히든_전유라@이음새/`. 반영은 PM(고유_5묶음판 대신).
3. **손오공·조세민 Move 합성** 0a8226f05: `walk_move`(발 목표 + 2관절 IK) + 변형 `…@Move합성`. 손오공 Move 49f 이음새 0 · 걸음 0.48m · 설계 지면 속도 1.00m/s · 닿은 발 속도 폭 0.25 / 조세민 1.09m/s · 폭 0.27. 산출 `~/GRD_motion_trial/랜덤_손오공@Move합성/`·`특별함_조세민@Move합성/`(fbx·README·렌더_Move.png). 반영 PM: MoveLoopRoots에 두 유닛 추가 + Move 재생 속도 = 유닛 이속 ÷ 설계 지면 속도(이속 m/s를 주면 `walk_move: speed=`로 다시 뽑는다).
4. **로비 시안 2차** 258bd5951: `gen_lobby_art.py` 환경변수 `LOBBY_V2=1`(앞마당 돌길·자갈·풀·유닛 실루엣·노출 −0.35). 산출 `~/GRD_lobby_art_v2/`(승인본 `~/GRD_lobby_art`는 안 건드림). 재생성: `LOBBY_V2=1 blender -b --factory-startup --python Tools/blender/gen_lobby_art.py -- ~/GRD_lobby_art_v2 2560 1440 160` → `/usr/bin/python3 Tools/blender/gen_lobby_post.py ~/GRD_lobby_art_v2`(ui·fonts는 승인본 심볼릭 링크). 사장님께 보여 드리는 중 — 대체 여부는 사장님 답 뒤.
5. **효과음 46개** 182ebd374: `Tools/sfx/make_sfx.py` → `~/GRD_sfx/<자리>/<자리>_NN.wav` 13자리(hit_melee·ranged·magic · enemy_death · boss_death · ui_click · ui_error · combine · gacha · gold · round_start · boss_appear · wood). 원작 mp3 + Kenney CC0 6팩(`~/GRD_sfx/_src/`, 팩 License.txt 있음). `README.md`(출처·라이선스·길이·피크) · `_audition_all.wav`(44초, 순서=`들어보기_목록.md`). 🔴 소리는 못 들어서 이름·길이로 골랐다 — 사장님/PM의 ○△×를 받으면 `SLOTS` 표 한 줄씩 바꿔 다시 뽑기. 연결은 PM.
6. 접은 것: **적 「애매 18」 Move 합성** — 공용 Humanoid 클립을 이미 빌려 쓰는 유닛이라 Generic으로 옮기면 Attack까지 합성해야 하고 품질이 오히려 떨어진다(PM 동의). 손오공·조세민·R21/33/44 고유 동작은 10-03에 이미 반영(NEXT_SESSION 277줄 낡은 문구는 PM이 고침).

## 대기 중 / 남은 일
- **초월 엄태웅 「폭탄제조」 폭발 이펙트 + 유물 아이콘** — 사장님 답이 먼저, 그때 PM이 준다.
- 효과음 청취 결과 반영(위 5).
- 로비 시안 2차 대체 여부 · 「가죽 칸 큼」(gen_lobby_ui 메뉴 틀 칸 줄이기)은 시키면.
- 못 구한 아이콘 16경로의 진짜 그림 — 정품 War3.mpq가 생기면 `FALLBACK_STD` 줄을 지우고 `extract_icons.py`에 경로 추가.
- 바지사장·양재모·강재규 스킬 에셋 이름이 바뀌면 `STD_MANUAL`(map_skill_icons.py) 키를 맞출 것.
- 이전 보류(아직 유효): 정품 War3 텍스처 위치(근사 텍스처 39+HandsAura2) · 적 R67·R73·R74 · 분홍 오라 1.5배 관찰.

## 함정 (오늘 새로)
- 🔴 **사인 흔들림 걷기는 디딤발이 미끄러진다**(폭 4~6 m/s). 걷기·뛰기를 합성할 땐 발 목표를 먼저 정하고 IK로 푼다(`walk_move`).
- **FBX 임포트 뒤 `bpy.data.actions`에서 이름으로 Idle을 찾으면 첫 임포트 것만 나온다**(여러 FBX를 한 장면에 넣을 때) → 임포트 전후 액션 집합 차로 고를 것(`gen_lobby_art.v2_figures`).
- 후처리 클립: `clips`(프레임별 세계 행렬 목록)는 소스 공간 → 출력 공간은 `G @ world`. 합성은 `G`로 옮겨 풀고 `G⁻¹`로 되돌려 넣는다. `split_clips`는 나머지 클립을 다 버린다(한두 클립만 다듬을 땐 `loop_clips`).
- Kenney 효과음 중 `click_00N`은 0.01초 — 너무 짧아 `ui-audio/click1`(0.09초)로 바꿨다. 3% 아래에서 끊으면 꼬리가 사라지니 최소 길이를 둔다.
- zsh: `echo "== …"`의 `==`는 명령으로 읽혀 터진다 · `sed -i ''` · `git commit -- <파일>` · 새 파일은 `git add -- <파일>` 먼저.
- 리포지드 아이콘은 클래식과 결이 다르다(테두리 있음) · 유니티 YAML은 `yaml.safe_load` 불가(skillName·description만 직접 잘라 읽기).
