# blender 인수인계 (2026-10-06 마무리, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고, 다 읽은 뒤 PM에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.
규칙: Assets엔 안 쓴다. 산출은 정본 스크립트(Tools/…)와 ~/ 작업 폴더. 시스템 `python3`엔 PIL·yaml이 없다 → 🔴 `/usr/bin/python3`(PIL·numpy·yaml·mpyq). Blender 창(MCP)은 오늘 안 썼다(전부 헤드리스 `blender -b --factory-startup`).

## 오늘 한 것 (10-06)
1. **협곡 텍스처**(`Tools/blender/gen_canyon_tex.py`) → `~/GRD_canyon/canyon_strata.png`(지층 띠 7·이음새 없음)·`canyon_top.png`(붉은 흙). 4×4 미리보기도 있음. PM이 레인 사이 십자 대지에 쓴다.
2. **전설 스킬 대응 제안**(`Docs/research/LEGEND_SKILL_PLAN_2026-10-06.md/.csv` + `LEGEND_PARTIAL_MISSING_2026-10-06.csv`, 5d8b4beda). 핵심: ① 남는 원작 6은 실제 4(h06N·h07H는 슈가의 소환체, 능력 없음) ② 우리 전설 33의 (공격력·주기)는 원작 전설과 정확히 같다(「스탯 쌍둥이」, 스킬 대응과는 별개) ③ 대응 없는 9기 배정 제안(구주호→슈가 · 양재모→킹 · 김용태→드래곤 · 노태현→시저(낮음) · 김건→울티 · 김민규→시노부 · 김민준→네코마무시 · 김정래→아마츠키 · 박병규→에이스) ④ 일부 반영 11 중 3은 CSV 오탐. SkillData 작성은 구현담당 몫.
3. **스킬·아이템 아이콘 추출**(`Tools/skill_icons/`): `extract_icons.py`(맵 w3a/w3t 아이콘 → `~/GRD_skill_icons/`, 맵 안 201장) → `fill_std_icons.py`(맵 밖 표준 BTN을 웹 모음으로 채움: `~/GRD_skill_icons/std_*.png` 369장) → `map_skill_icons.py`(우리 UnitSkills → 원작 능력 코드 → png: `skill_icon_map.csv`). 7721f5d1e · 015f81b54.
4. **웹 BTN 수집**: 출처 github.com/Wc3ReforegIcons/Wc3ReforegIcons.github.io(리포지드 HD, 256², 이름 1:1). 능력 363 중 360 · 아이템 31/31 채움. 못 구한 3: PASBTNShadeTrueSight · BTNShoveler · UI\infocard-banshee. 내려받은 건 `~/GRD_skill_icons_dl/reforge/`. 공개 전 교체 줄 `Docs/REPLACE_BEFORE_PUBLIC.md`에 있음.
5. **로비 워크3풍 아트**(사장님 「이대로 가자」로 확정, 구현담당3이 Assets에 붙인다 — `~/GRD_lobby_art/` 파일 옮기거나 이름 바꾸지 말 것): `Tools/blender/gen_lobby_ui.py`(단추 9-slice 3·메뉴 틀·제목 판, numpy+PIL) · `gen_lobby_art.py`(Cycles 3D 배경: 높이장 섬·십자 대지·상점 7채 FBX·횃불·안개 — 2560×1440 160샘플 3분) · `gen_lobby_post.py`(꽃·비네트·색조·시안). 폰트 `~/GRD_lobby_art/fonts/`(Nanum Myeongjo ExtraBold · Song Myung · Gowun Batang, OFL). 재생성: ui → art → post 순.

## 함정 (오늘 새로)
- **Blender 월드에 볼륨 산란을 달면 하늘이 통째로 사라진다**(무한 거리 감쇠). 안개는 큰 상자(`ground_fog`)로. 월드 `Generated` 좌표는 0..1 → 방향으로 쓰려면 ×2−1 정규화(이번엔 결국 물리 하늘 `MULTIPLE_SCATTERING` 사용; 블렌더 5엔 `NISHITA` enum이 없다).
- 안개 상자 경계가 하늘에 줄(이음선)로 보인다 → 상자를 하늘 쪽으로 크게(2400) 잡을 것.
- 능력 w3a 파서: 새 ID가 NUL 4칸인 항목이 있다(원본 능력을 고친 것) → 코드에 `*`(map_skill_icons가 처리). csv에 NUL이 섞이면 읽기가 터진다.
- Unity 에셋 YAML은 따옴표 없는 줄에 콜론이 있어 `yaml.safe_load`가 터진다 → skillName·description만 직접 잘라 읽을 것(map_skill_icons.ydec).
- zsh에서 `$VAR`에 공백 든 여러 경로를 넣으면 한 인자가 된다 · macOS `sed -i ''` · `git commit -- <파일>`로만(멀티세션).
- 리포지드 아이콘은 클래식과 그림 결이 다르다(테두리 있음). 원작 아이템 아이콘 34/38은 원래 맵 밖이라, `Assets/Art/Items`의 지금 그림은 원작이 아니라 game-icons.net(CC BY 3.0)이다.

## 남은 일·보류
- 못 구한 아이콘 3(위). 정품 War3.mpq가 생기면 클래식 BTN으로 교체 가능(`extract_icons.py`에 경로만 추가하면 됨).
- 대응표 「낮음」 306행은 코드에 아이콘이 없어 원작 유닛 대표 능력으로 임시 대체한 것 — 구현담당이 쓰기 전에 필요하면 손으로 확정.
- 바지사장·양재모·강재규 스킬 에셋이 생기면 `map_skill_icons.py` MANUAL 표(설계표 코드)에 이름을 추가하고 다시 돌릴 것(「계획」 행이 있다).
- 로비 시안 약점(사장님은 통과시킴): 상점 모델 장난감 느낌 · 땅 밋밋 · 가죽 칸 큼 — 필요하면 2차(더 어둡게·땅 질감·유닛 실루엣).
- 이전 보류(아직 유효): 정품 War3 텍스처 위치(근사 텍스처 39+HandsAura2) · 히든_전유라 Move 이음새 · 적 R67·R73·R74·애매 18 · 분홍 오라 1.5배 관찰.
