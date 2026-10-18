# 작업 산출물 보관 목록 (Assets 밖)

사장님 지시(2026-10-09): 「나중에 재활용할 수 있게 잘 보관」.
산출물 본체는 홈 폴더(`~/grd/GRD_*`, `~/Desktop/구랜디*`)에 있다. 다시 만드는 도구는 전부 repo `Tools/`에 커밋돼 있다.
⚠️ `/tmp`는 재부팅하면 지워진다. 남길 것은 `~/grd/GRD_*`로 옮길 것.

## 스킨

| 무엇 | 위치 | 다시 만드는 도구 |
|---|---|---|
| 원작 스킨 교체 24기 팩(model·model_fixed·클립·텍스처·clip_map) · 교체목록.csv(정본) | `~/grd/GRD_skin_swap/` (이전 판 `_model_fixed_prev/`, 검증표 `_재수출검증.csv`, 클립 중 알파 `클립중_알파변화.json`) | `Tools/blender/swap_refix_b3.py` · `swap_verify_b3.py` · `swap_verify_cont.py` · `swap_render_b3.py` · `Tools/w3x/swap_refix_all.py` · `skin_swap_pack.py` |
| 교체 전 옛 모델 | `~/grd/GRD_swap_before/` | git `8c522dbce^`에서 풀기 |
| 박민수_조로 S2 연출 5개(BxV·BxW·BxX·BxZ·Bxb, 미연결 — 특별함은 스킬이 없어 보관만, 스킬 생기면 사용) | `~/grd/GRD_skin_swap/박민수_조로/scripts/` (시트 `스킬연출_시트.png`·`pairing.csv`) | `Tools/w3x/jass_sim.py`(JASS_S2=1) · `scene_auto.py` |
| 교체 24기 전후 비교 카드·목록 | `~/Desktop/구랜디스킨모음/교체24_전후비교/` | `Tools/blender/swap_before_after.py` · `Tools/w3x/swap_before_after_run.py` |
| 신작(S2 2.323) 원랜디 스킨 362기 FBX·텍스처·등급 격자·index.csv | `~/Desktop/구랜디스킨모음/원랜디_신작_스킨/` | `Tools/w3x/s2_units.py` → `mdx_extract.py` → `Tools/blender/export_mdx_anim_fbx.py` → `skin_grade_build_s2.py` 등(README 참고) |
| 신작 매칭 시트(초월·불멸·영원·히든) | `~/Desktop/구랜디스킨모음/매칭_신작_<등급>/` | `Tools/w3x/match_sheet_s2.py <등급>` |
| 같은 캐릭터 우리↔신작 비교 188쌍·짝목록.csv | `~/Desktop/구랜디스킨모음/같은캐릭터_신작비교/` | `Tools/w3x/same_char_pairs_s2.py` · `same_char_render_s2.py` |

## 날개

| 무엇 | 위치 | 도구 |
|---|---|---|
| S2 날개 6종 FBX·텍스처·wing.json(재질 모드·크기)·미리보기 | `~/grd/GRD_wings/` | `Tools/blender/wing_pack.py` |
| 날개·오라 원작 조사(꾸미기 상품 구조, j 줄 번호) | `~/Desktop/구랜디스킨모음/신작_날개조사/` | — |

## 스킬 연출·이펙트

| 무엇 | 위치 | 도구 |
|---|---|---|
| 원작 스킬 연출 대본(auto/<id>.json)·렌더·batch_01~05 · v2 새 컴파일(auto_v2)·전후 비교(v2_compare)·v2_diff.csv | `~/grd/GRD_scenes/` | `Tools/w3x/jass_sim.py` · `scene_thin.py` |
| 원작 이펙트 추출본 | `~/grd/GRD_orig_vfx_trial/` | `Tools/w3x/mdx_extract.py` |
| BLP 알파 전수 조사표 · Assets 덮어쓰기용 고친 PNG(assets_fixed) · 워크3 기본 텍스처 절차 재현 64종(placeholder_v2) · 전후 시트 | `~/grd/GRD_blp_audit/` | `Tools/w3x/blp_decode.py` · `audit_blp_alpha.py` · `gen_wc3_textures.py` |
| 컷인(교체 반영 15기 C안) | `~/grd/GRD_cutin_swap/C_all/` | `Tools/blender/cutin_pick.py` · `Tools/w3x/cutin_swap_run.py` |

## 게임 촬영

| 무엇 | 위치 | 도구 |
|---|---|---|
| 초월 스킬 동작 영상(4기·합본·재촬영 4기)·BLP 전후 게임 사진 | `~/Desktop/구랜디_초월_스킬모션/` | `Assets/Editor/SkillMotionProbe.cs` |
| 동작 시트·가프 머리 프레임·초월 키 줄세우기 전후·크레딧 C/D/E 캡처 | `~/grd/GRD_skill_motion/` (원래 `/tmp/g1n_motion`·w2 shots에서 옮김) | `SkillMotionProbe` · `UnitLineupShot` · `UnitHeightProbe` |
| `~/grd/GRD_scenes/add_rel_26/` | SleepForStageAdd 상대값 해석(JASS_ADD_REL=1)으로 다시 푼 연출 대본 26트리거 + 전후표·전후 렌더(compare/)·판정표·반영목록 | `JASS_ADD_REL=1 SCENE_TABLE=… SCENE_OUT=… Tools/w3x/scene_auto.py compile` (10-14) |
| `~/grd/GRD_pachinko_art/` | 30R 보스 파칭코 그림 재료: 숫자 심볼 1~9(+모션블러)·글자 로고 6·기계 틀 1920×1080+릴 창 json·contact_sheet·FONTS.txt | `Tools/pachinko/pachinko_art.py` (10-14) |

## 함정
- 원작 BLP(JPEG) 알파는 PIL로 열면 검게 나온다 → 반드시 `blp_decode.py`(10-09 이전 추출본은 audit으로 고쳐 둠).
- 원작 모델 회전 키는 쿼터니언 부호 연속으로 다시 구워야 유니티 보간이 안 튄다(`swap_refix_b3.py`).
- 파칭코 이펙트 그림 재료(카구네 촉수·붓 획·빛줄기·집중선·시계 문양·나비·불똥): ~/grd/GRD_pachinko_art/fx/ — 재생성 `/usr/bin/python3 Tools/pachinko/fx_art.py`
- 리제로 보석 숫자 1~9(+반짝임·블러·분홍): ~/grd/GRD_pachinko_art/symbols_rz/ — 재생성 `/usr/bin/python3 Tools/pachinko/symbol_rz.py`
- ~/grd/GRD_pachinko_fx/ — 파칭코 화면 이펙트 스프라이트 11종(<효과>/rgba 투명·add 검정배경, meta.json). 재생성 `Tools/blender/run_pachinko_fx_all.sh [효과…]` ← gen_pachinko_fx.py
- ~/grd/GRD_pachinko_cabinet/ — 파칭코 기계 정면 3D 1차(취소된 방향, 보관만). Tools/blender/gen_pachinko_cabinet.py
- ~/grd/GRD_s2_models/ — S2 신규 기능용 원작 모델 추출(오로성 3·샨도라 종·룸바 영혼·엘리베이터·kineticfield). <모델>/fbx·Textures·json, _verify.json(부품 대조), _render/, sheet_s2_models.png. 재생성 `Tools/w3x/model_pull.py s2 ~/grd/GRD_s2_models <모델…>` → verify_mdx_fbx.py · render_mdx.py
- ~/grd/GRD_skill_vfx/ — 재매칭 스킬 이펙트(원작). 1_extract/(① 꺼내기만 41: fbx·Textures·json, _verify.json, _render/) · sheet_1_extract.png · effect_list.json(구현담당1 목록) · b1_summary.json. 재생성 `Tools/w3x/model_pull.py s2 ~/grd/GRD_skill_vfx/1_extract <경로…>`(blink-yellow2만 old)
