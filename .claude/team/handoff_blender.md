# blender 인수인계 (2026-10-08 새벽 마감, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고 PM(`pm` — 이름이 둘이면 새 쪽 ref)에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.
규칙: Assets엔 안 쓴다(반영은 PM·구현담당). 산출은 ~/GRD_*/ + 정본 스크립트(Tools/blender·Tools/w3x). 🔴 시스템 파이썬 PIL·numpy는 `/usr/bin/python3`(`-I` 주지 말 것). Blender 창(MCP)은 꺼져 있어 전부 헤드리스(`blender -b --factory-startup`, 5.2.1). 유료 에셋·원작(블리자드/중국 모델러) 그림 반입 금지.

## 진행 중인 일 없음 — 대기(PM 지시 기다림)
마지막 상태: 상위 등급 뽑기 「컷인」 **C안 확정**(사장님 「잘 만들었다」), 41기 일괄·다듬기·머리 잘림 보정까지 끝, 자동 검사 41/41 통과. 사장님 확인 대기. Unity 반영은 구현담당2(레이어 PNG + UI 애니) — 구현담당2에게 layers 갱신 알림 보냄(0ef0e5303 기준).

### 컷인 C안 — 위치·정본·바꾸는 법
- 무엇: 단간론파1 프롤로그 자기소개 흐름(게임 화면→등급색 불투명→캐릭터 떠오름→왼쪽 1/3로 휙→실루엣 그림자·망점→끊긴 동심원·줄무늬→흰 빛줄기→흰 띠+이름+칭호 「등급 별명」→퇴장). 등장 1.0·유지 1.2·퇴장 0.4 = 2.6초, 효과음 자리 슉 0.25s·칭 0.83s·퇴장 2.20s.
- 대상 41기 = 초월 25 · 불멸 8 · 영원 8(로스터 파일). 이름 = 파일명 둘째 칸, 별명 = 에셋 unitName(별명이 이름과 같은 강주혁·조성진·김영원은 칭호에 등급만). 등급색 = UnitData.GradeColor(불멸 상아는 띠 테두리·진한 칭호 글씨).
- 산출: `~/GRD_cutin/C_all/`(C_<유닛>.mp4·_still.png·_timing.txt · _contact_sheet.png · _candidates_sheet.png · _check.txt · 컷인_C안_41기_연속.mp4) · `~/GRD_cutin/layers/C_<유닛>_{char,shadow,band,band_name,band_gray,title,ring,stripes,halftone,flat,streak}.png`(Unity용) · `~/GRD_cutin/render/C_<유닛>_thigh_{c1,c2,c3,p_hips,p_crossed,p_point,p_fist}.png` + `C_<유닛>_cands.json`(후보·chosen·dy·검사). 사장님용 사본: `~/Desktop/구랜디_베타/컷인_시안/`(41기 모아보기·포즈후보·연속 영상·자동검사.txt, 「옛_」 = 02:33 시험본).
- 정본 스크립트(전부 커밋, 마지막 0ef0e5303):
  - `Tools/blender/run_cutin_c_all.sh [유닛…]` — 전체(렌더→고르기→합성→시트). 41기 약 25분.
  - `gen_cutin_char.py`(Blender) — 셀 셰이딩 렌더. C는 유닛 Attack 클립(자체 13기 FBX 안 / 공용 28기 `Assets/Art/Characters/attack.fbx`)을 16등분해 「허벅지 위 넓이×높이」 큰 프레임 3개(c1~c3, 서 있고 정면인 프레임 우선) + 프리셋 4종(목표 방향 겨누기 `aim_bone`, 믹사모만).
  - `cutin_pick.py` — 고르기 + 자동 검사(① 눈이 화면 안·흰 띠 위쪽·띠/줄무늬에 안 가림 ② 실루엣 높이 ≥ 화면 50% ③ 머리 꼭대기 ≥ 화면 위 18px — 모자라면 캐릭터를 내림 dy). 순서: `cutin_picks.json` → 공용 클립 3기 중 1기 프리셋 섞기 → c1~c3 통과 → 프리셋 통과 → c1.
  - `cutin_picks.json` — **사람이 고른 것**. 값 = 후보 id(`"p_point"`) 또는 `{"pick": "p_point", "zoom": 1.3}`(zoom = 머리 꼭대기 기준 위쪽을 잘라 키움, 날개·건물 스킨용).
  - `gen_cutin_comp.py C all [유닛…]` — 합성·영상(frame_C가 움직임 정의) · `cutin_sheets.py` — 후보 시트·연속 영상.
- **포즈만 바꾸는 법(렌더 없이 약 8분)**: cutin_picks.json 고침 → `/usr/bin/python3 Tools/blender/cutin_pick.py` → `/usr/bin/python3 Tools/blender/gen_cutin_comp.py C all` → `/usr/bin/python3 Tools/blender/cutin_sheets.py` → 바탕화면 사본 덮어쓰기. 새 포즈가 필요하면 `C_PICK`/`POSES` 대신 그 유닛만 run_cutin_c_all.sh로 다시 렌더.
- A안(논파)·B안(소개 카드) 시안도 남아 있음(~/GRD_cutin/A_*·B_*, `gen_cutin_comp.py` 인자 없이) — 사장님이 C안으로 정해 쓰지 않는다.

### 초월 시전 이펙트 조사 — 끝(문서 커밋 29f8786a3)
`Docs/research/TRANSCEND_CAST_VFX_2026-10-08.md` + `Docs/research/images/transcend_cast_vfx_compare_3.png`. 도구 `Tools/w3x/cast_vfx_survey.py`(원작 사슬 → 모델 276종·맵 안 여부) · `Tools/blender/render_cast_vfx.py`(MDX 뼈 애니·알파·PRE2 입자 추정 렌더 / 우리 VFX_*.fbx). 남은 것: 사장님/PM이 어떤 원작 이펙트를 어느 스킬에 붙일지 정하면 그때 변환(원작 텍스처 금지).

## 함정 (오늘 새로)
- 🔴 **`mesh.materials.clear()` 뒤 다시 append하면 면마다 붙은 재질 번호가 0으로 돌아간다** → 여러 재질 스킨이 통째로 첫 재질(머리 피부) 텍스처로 칠해져 검·살색 얼룩(이재윤·두유찬·임채민·박기찬…). **재질은 `materials[i] = 새것`으로 제자리 교체.** 원인 찾는 법: 원래 재질 그대로 렌더해 보고(정상이면 우리 코드 문제), 새 재질에 텍스처만 얹어 비교.
- 셀 셰이딩은 원래 재질을 **복사해서 그 안에서** Principled만 갈아 끼운다(Base Color·Alpha로 들어오던 소켓 그대로 — 「이름.001」 알파 마스크·뒷면 버림 보존).
- Solidify 법선 반전 외곽선은 법선 뒤집힌 립 스킨에서 몸을 덮는다 → C안은 합성에서 실루엣 외곽선(알파 부풀리기).
- 공용 믹사모 클립을 다른 축의 립에 얹으면 몸이 돌아가 등을 보인다 → 어깨 방향(+X)으로 정면 판정해 깎는다.
- 자동 검사는 머리뼈 기준이라 팔·모자가 얼굴을 가리는 건 못 잡는다 → 모아 보기를 눈으로 볼 것. 1차 검사가 「눈이 띠 아래」(윤현모)와 루프 변수 `dy` 가림을 놓쳤다.
- zsh는 따옴표 없는 변수를 단어로 안 쪼갠다(`for a in "x 3 4"; cmd $a` → 인자 1개). `pgrep -f 스크립트이름`은 그 이름이 든 감시 셸 자신도 잡는다.
- 🔴 `rm -f "$O"/…` 꼴은 안전 검사에 걸린다 → `"${O:?}"/…`. macOS `sed -i ''`.
- Blender 5.2: 컴포지터 막힘 → 후처리는 PIL. `ShaderNodeMix`(RGBA) 입력 6·7, 출력 2. 액션은 슬롯(`action_slot = act.slots[0]`).
- zsh 커밋: `git add -- <파일>` 뒤 `git commit -- <파일>`. push는 PM만.

## 이전 판 납품(10-07까지, 반영됨 또는 반영 대기)
섬 루프 v2(~/GRD_title_loop_v2) · 워크3풍 UI 17종+창 4종+명령 아이콘 6(~/GRD_wc3_ui) · 적 출발 포탈 · 바다 물(~/GRD_water — 구현담당1 반영 뒤 원작과 다르면 gen_water_tex.py 조정) · 버프 아이콘 7종(~/GRD_buff_icons) · 문 v3·폭탄·종이비행기·설정 판·선술집 UI·스킬 아이콘 170행. 퇴치 미니보스 모델은 사장님 「크기·색만」 → 안 만든다.
