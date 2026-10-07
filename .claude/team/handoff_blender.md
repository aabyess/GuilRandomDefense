# blender 인수인계 (2026-10-08 새벽, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고 PM(`pm` — 이름이 둘이면 새 쪽 ref)에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.
규칙: Assets엔 안 쓴다(반영은 PM·구현담당). 산출은 ~/GRD_*/ + 정본 스크립트(Tools/blender·Tools/w3x). 🔴 시스템 파이썬 PIL·numpy는 `/usr/bin/python3`(`-I` 주지 말 것). Blender 창(MCP)은 꺼져 있어 전부 헤드리스(`blender -b --factory-startup`, 5.2.1). 유료 에셋·원작(블리자드/중국 모델러) 그림 반입 금지.

## 진행 중 — 🔴 최우선: 상위 등급 뽑기 「컷인」 시안 (사장님 새 기능, PM 10-08)
대상 초월 25·불멸 8·영원 8 = 41기. 시안 유닛 = 초월_노태현_AP(여동생살해자)·초월_최상호_AD(구일에서가장자유로운남자). 사장님이 A/B/C 중 고르시는 중 — **C안이 최우선**(PM 10-08 새벽).
| 안 | 무엇 | 파일(~/GRD_cutin/) | 상태 |
|---|---|---|---|
| A | 단간론파 「논파」 컷인(등급색 집중선+먹물+붓글씨 별명+칼자국+노란 외곽선 기울인 얼굴) 1.5초 | A_<유닛>.mp4 · _still.png | 납품, 사장님 보심 |
| B | 캐릭터 소개 카드(V3 한글판: 동심원 대리석+망점+먹 띠 이름(둘째 글자 강조)+흰 줄 칭호+평행선+허리에 손 상반신) 1.5초 | B_<유닛>.mp4 · _still.png | 납품 |
| C | **단간론파1 프롤로그 자기소개**(PM 12fps 분해 sheet_in/out 기준): 게임 화면→등급색 불투명→캐릭터 떠올라 가운데→왼쪽 1/3로 휙→실루엣 그림자+망점→끊긴 동심원 회전·확대+아래 줄무늬→흰 빛줄기→흰 띠+회색 이름→검정 이름+작은 칭호. 등장 1.0·유지 1.2·퇴장 0.4초 | C_<유닛>.mp4 · _still.png · _sheet12.png(12fps 대조 시트) · _timing.txt(효과음 자리: 슉 0.25s·칭 0.83s·퇴장 2.20s) | 납품(다듬은 판), 다음 손질 지시 대기 |
- 정본: `Tools/blender/gen_cutin_char.py`(Blender: 셀 셰이딩+Solidify 외곽선, A 얼굴/B 상반신 포즈 5종 calm·hips·crossed·point·fist/C 허벅지까지. 포즈는 믹사모 뼈 `spin()` 근사 — 사장님 「성격 드러나는 포즈」 요청으로 유닛별 고르게 해 둠) → `~/GRD_cutin/render/`. `Tools/blender/gen_cutin_comp.py`(PIL+ffmpeg 합성·영상. `C` 인자면 C안, 없으면 A·B. JOBS 표에 유닛·등급·이름·별명).
- 등급색 = Assets `UnitData.GradeColor`(초월 청록·불멸 상아·영원 남색)를 `GRADE` 표에 옮김(불멸 상아는 밝아서 글자 대비 확인 필요). 폰트 OFL: ~/GRD_cutin/fonts(East Sea Dokdo·Nanum Brush·Nanum Gothic ExtraBold·Black Han Sans, 구글 폰트 raw에서 받음).
- 다음: 사장님 선택 오면 41기 일괄(스킨 FBX = Assets/Art/Units/<로스터명>/<로스터명>.fbx, 정면 방향 모델마다 달라 `front` 인자 확인 · 별명 긴 유닛 줄바꿈 · 불멸/영원 등급색) → Unity 반영은 구현담당2.
- 함정: 최상호_AD는 가면 괴물 스킨이라 얼굴이 사람처럼 안 읽힘(스킨 문제). `place()`의 offset은 감싸므로 밀려난 쪽을 지우는 마스크가 들어 있다. 영상 프레임은 ~/GRD_cutin/_frames에 썼다 지운다.

## 미뤄 둔 일 — 초월 스킬 「시전 이펙트」 원작 MDX 조사(PM 10-08, 컷인 뒤)
- 1단계 목록 **완료·커밋**(29f8786a3): `Docs/research/TRANSCEND_CAST_VFX_2026-10-08.md`(초월 21종 원작 사슬에서 모델 276종, 맵 안 ✅ 263 · 기본 17 · 없음 2 · 우리 스킬 짝은 이름/발동 문턱으로, 대부분 「짝 없음」= 사장님 신규 사양). 생성: `Tools/w3x/cast_vfx_survey.py` → ~/GRD_cast_vfx_trial/survey.{json,md}.
- 2단계 시범 3종 **완료**: animeslashfinal(초승달)·saitamawave3(먼지 충격파)·BY_Wood_GongChengSiPai_46(입자 폭발) — `Tools/blender/render_cast_vfx.py`(MDX 지오셋+뼈 애니+지오셋/층 알파+PRE2 입자 추정 시뮬 `orig` / 우리 VFX_*.fbx `ours`) → 비교 그림 `Docs/research/images/transcend_cast_vfx_compare_3.png`. 「다시 짜야 할 것」은 문서 표에.
- 남은 것: 사장님/PM이 어떤 원작 이펙트를 어떤 우리 스킬에 붙일지 정하면 그 모델들을 FBX+Kenney 텍스처로 변환(원작 텍스처 금지). 입자는 Unity ParticleSystem으로 값 옮기기(문서 「일반 규칙」).
- 함정: `mdx_extract`는 맵 밖 워크3 기본 텍스처(Textures\…)를 알파 255 근사 그림으로 깔아 둔다 → 렌더 재질은 알파 대신 밝기 마스크를 쓴다. 입자 시뮬은 추정(난수·헤드/테일·PREM·리본 안 읽음).

## 이전 판 납품(10-07까지, 전부 반영됨 또는 반영 대기) — 위치·정본
섬 루프 v2(~/GRD_title_loop_v2, gen_title_loop.py) · 워크3풍 UI 17종+창 4종+명령 아이콘 6(~/GRD_wc3_ui, gen_wc3_ui*.py) · 적 출발 포탈(~/GRD_enemy_portal) · 바다 물(~/GRD_water, gen_water_tex.py — 구현담당1 반영 뒤 원작과 다르면 밝기·STR 조정) · 버프 아이콘 7종(~/GRD_buff_icons, gen_buff_icons.py) · 문 v3·폭탄·종이비행기·설정 판·선술집 UI·스킬 아이콘 170행. 퇴치 미니보스 모델은 사장님 「크기·색만」 → 안 만든다.

## 함정(누적)
- 🔴 `sed -i`는 macOS에서 `sed -i ''`. 긴 치환은 `/usr/bin/python3` heredoc.
- 🔴 `rm -f "$O"/…` 꼴은 안전 검사에 걸린다 → `"${O:?}"/…` 또는 절대 경로.
- Blender 5.2: 컴포지터 막힘 → 후처리는 PIL. `ShaderNodeMix`(RGBA) 입력 6·7, 출력 2. `Material.use_nodes` 경고는 무시.
- zsh: `--include=*.cs` 같은 글롭은 따옴표. `git commit -- <파일>`, 새 파일은 `git add -- <파일>` 먼저. push는 PM만.
- 백그라운드 렌더는 `nohup … &` + pgrep.
