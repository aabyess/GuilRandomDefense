# 작업 산출물 보관 목록 (Assets 밖)

사장님 지시(2026-10-09): 「나중에 재활용할 수 있게 잘 보관」.
산출물 본체는 홈 폴더(`~/GRD_*`, `~/Desktop/구랜디*`)에 있다. 다시 만드는 도구는 전부 repo `Tools/`에 커밋돼 있다.
⚠️ `/tmp`는 재부팅하면 지워진다. 남길 것은 `~/GRD_*`로 옮길 것.

## 스킨

| 무엇 | 위치 | 다시 만드는 도구 |
|---|---|---|
| 원작 스킨 교체 24기 팩(model·model_fixed·클립·텍스처·clip_map) · 교체목록.csv(정본) | `~/GRD_skin_swap/` (이전 판 `_model_fixed_prev/`, 검증표 `_재수출검증.csv`, 클립 중 알파 `클립중_알파변화.json`) | `Tools/blender/swap_refix_b3.py` · `swap_verify_b3.py` · `swap_verify_cont.py` · `swap_render_b3.py` · `Tools/w3x/swap_refix_all.py` · `skin_swap_pack.py` |
| 교체 전 옛 모델 | `~/GRD_swap_before/` | git `8c522dbce^`에서 풀기 |
| 교체 24기 전후 비교 카드·목록 | `~/Desktop/구랜디스킨모음/교체24_전후비교/` | `Tools/blender/swap_before_after.py` · `Tools/w3x/swap_before_after_run.py` |
| 신작(S2 2.323) 원랜디 스킨 362기 FBX·텍스처·등급 격자·index.csv | `~/Desktop/구랜디스킨모음/원랜디_신작_스킨/` | `Tools/w3x/s2_units.py` → `mdx_extract.py` → `Tools/blender/export_mdx_anim_fbx.py` → `skin_grade_build_s2.py` 등(README 참고) |
| 신작 매칭 시트(초월·불멸·영원·히든) | `~/Desktop/구랜디스킨모음/매칭_신작_<등급>/` | `Tools/w3x/match_sheet_s2.py <등급>` |
| 같은 캐릭터 우리↔신작 비교 188쌍·짝목록.csv | `~/Desktop/구랜디스킨모음/같은캐릭터_신작비교/` | `Tools/w3x/same_char_pairs_s2.py` · `same_char_render_s2.py` |

## 날개

| 무엇 | 위치 | 도구 |
|---|---|---|
| S2 날개 6종 FBX·텍스처·wing.json(재질 모드·크기)·미리보기 | `~/GRD_wings/` | `Tools/blender/wing_pack.py` |
| 날개·오라 원작 조사(꾸미기 상품 구조, j 줄 번호) | `~/Desktop/구랜디스킨모음/신작_날개조사/` | — |

## 스킬 연출·이펙트

| 무엇 | 위치 | 도구 |
|---|---|---|
| 원작 스킬 연출 대본(auto/<id>.json)·렌더·batch_01~05 · v2 새 컴파일(auto_v2)·전후 비교(v2_compare)·v2_diff.csv | `~/GRD_scenes/` | `Tools/w3x/jass_sim.py` · `scene_thin.py` |
| 원작 이펙트 추출본 | `~/GRD_orig_vfx_trial/` | `Tools/w3x/mdx_extract.py` |
| BLP 알파 전수 조사표 · Assets 덮어쓰기용 고친 PNG(assets_fixed) · 워크3 기본 텍스처 절차 재현 64종(placeholder_v2) · 전후 시트 | `~/GRD_blp_audit/` | `Tools/w3x/blp_decode.py` · `audit_blp_alpha.py` · `gen_wc3_textures.py` |
| 컷인(교체 반영 15기 C안) | `~/GRD_cutin_swap/C_all/` | `Tools/blender/cutin_pick.py` · `Tools/w3x/cutin_swap_run.py` |

## 게임 촬영

| 무엇 | 위치 | 도구 |
|---|---|---|
| 초월 스킬 동작 영상(4기·합본·재촬영 4기)·BLP 전후 게임 사진 | `~/Desktop/구랜디_초월_스킬모션/` | `Assets/Editor/SkillMotionProbe.cs` |
| 동작 시트·가프 머리 프레임·초월 키 줄세우기 전후·크레딧 C/D/E 캡처 | `~/GRD_skill_motion/` (원래 `/tmp/g1n_motion`·w2 shots에서 옮김) | `SkillMotionProbe` · `UnitLineupShot` · `UnitHeightProbe` |

## 함정
- 원작 BLP(JPEG) 알파는 PIL로 열면 검게 나온다 → 반드시 `blp_decode.py`(10-09 이전 추출본은 audit으로 고쳐 둠).
- 원작 모델 회전 키는 쿼터니언 부호 연속으로 다시 구워야 유니티 보간이 안 튄다(`swap_refix_b3.py`).
