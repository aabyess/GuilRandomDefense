# blender 인수인계 (2026-10-14~18, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고 PM(`pm` — 이름이 둘이면 새 쪽 ref)에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.
규칙: **Assets엔 안 쓴다**(반입은 PM·구현담당1/2). 산출은 `~/grd/GRD_*/` · `~/Desktop/구랜디스킨모음/` + 정본 스크립트(Tools/blender · Tools/w3x). 🔴 시스템 파이썬 PIL·numpy·yaml·mpyq는 `/usr/bin/python3`(`-I` 주지 말 것). Blender 창(MCP)이 켜져 있으면 시험은 창에서(`exec`로 스크립트 실행), 대량 렌더는 헤드리스(`blender -b --factory-startup --python …`). 커밋은 `git add -- <파일>` → `git commit -m … -- <파일>`(경로 없는 커밋·-a 금지), push는 PM. 큰 산출물이 새로 생기면 `Docs/ARCHIVE_INDEX.md`에 한 줄(위치+재생성 도구). 커밋 끝줄 `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. 원작 수치는 j/w3a/w3u 직접 디코드, 유료 에셋 금지. 임시 세션 blender2(S2 스킨 추출)·blender3(닫힘)가 있었다 — 새 일은 PM 지시로.

## 지금 진행 중인 일 없음 — 대기(10-18 저녁)
PM이 재매칭 이펙트 78줄을 에디터에서 반입하는 중. 크기·색 조정 숫자가 오면 해당 gen_* 스크립트 숫자만 바꿔 다시 돌린다. 미커밋 도구 없음.
Blender 창(MCP)은 10-16에 켜져 있었다(기본 장면) — 시험은 창에서, 대량 렌더는 헤드리스로 했다.

## 10-16~18 세션에 끝낸 일 (전부 커밋·대부분 PM push)
| 일 | 산출 위치 | 도구(재생성) |
|---|---|---|
| 파칭코 화면 이펙트 11종(섬광·충격파 0.4s·코인·집중선 루프·화염·슬라임·무지개·예고 글자 チャンス/激熱/確変/PUSH!!) | `~/grd/GRD_pachinko_fx/<효과>/`(rgba 투명·add 검정배경·meta.json) · 시트 `sheet_*.png` | `Tools/blender/gen_pachinko_fx.py` + `run_pachinko_fx_all.sh [효과…]`(렌더→`post` numpy bloom→`sheet`) |
| (취소된 방향) 파칭코 기계 정면 3D | `~/grd/GRD_pachinko_cabinet/` | `gen_pachinko_cabinet.py` — 더 손대지 말 것(PM) |
| S2 원작 모델·이펙트 추출 공용 도구 | — | 🔴 `/usr/bin/python3 Tools/w3x/model_pull.py <s2\|old> <출력루트> <모델…>`: mdx_extract → export_mdx_anim_fbx → none 층 전용 텍스처 알파 채움 → vfx_unity_block 자동. 이름은 `!0720_Tashigi`처럼 짧게도 됨 |
| S2 신규 기능 모델 7(오로성 워큐리·새턴·나스쥬로, 샨도라 종, 룸바 영혼 SmallLight, 엘리베이터, kineticfield) | `~/grd/GRD_s2_models/`(_verify.json·_render·sheet_s2_models.png) | model_pull + `verify_mdx_fbx.py`(_ 폴더 건너뜀) + `render_mdx.py` |
| 엘리베이터 원판·kineticfield 바닥 필드 재제작 | `~/grd/GRD_s2_models/gen_ElevatorPuzzle/`·`gen_kineticfield/` | `Tools/blender/gen_s2_props.py [elevator] [kinetic]` |
| 재매칭 이펙트 ① 꺼내기만 41 · ② 깨짐 20 | `~/grd/GRD_skill_vfx/1_extract/`·`2_fix/` → 반입본 `~/grd/GRD_orig_vfx_trial/<모델>/` | model_pull · 목록 원본 `~/grd/GRD_rematch/effects_for_blender.csv`(구현담당1) |
| 입자 목록 한 함수 | — | `Tools/w3x/pre2_unity.py`(stand_seq·pre2_entries) — scene_scripts.model_block과 공유. 따로 베끼지 말 것 |
| unity 블록 생성 | — | `Tools/w3x/vfx_unity_block.py <루트> [--check]` — json에 `unity`(OriginalVfxBuilder)·`pre2Unity`(Pre2Builder) 추가 + 입자 텍스처 Textures/ 복사. 옛 527개 대조 521 일치 |
| 재매칭 ③ 제작 16(워크3 기본 모델 베끼기 + Dashboard_UtaExAuraEff) | `~/grd/GRD_orig_vfx_trial/<이름>/` · 시트 `~/grd/GRD_skill_vfx/sheet_3_make.png` | `blender -b --factory-startup --python Tools/blender/gen_skill_vfx_wc3.py -- [이름…] [--out 루트]` |
| 반입 목록(78줄) | `~/grd/GRD_skill_vfx/반입목록_1018.csv`(.json) | PM이 이걸로 반입 |

## 10-16~18 함정 (🔴 새로 알게 된 것)
- 🔴 **반입 원본 자리 = `~/grd/GRD_orig_vfx_trial/<모델>/<모델>.json·.fbx·Textures/`**(OriginalVfxBuilder.SourceRoot). 폴더 이름은 경로 뗀 모델 이름(`war3mapImported__!0603_Rebecca` → `!0603_Rebecca`), FBX 안 메시·액션 이름은 원래 태그 그대로. json엔 `unity` 블록(없으면 「unity 블록 없음」 실패)과 입자는 `pre2Unity` 필요. 입자 전용 모델은 `unity.layers=[]`. 10-18에 같은 이름 옛 폴더 20개를 S2 판으로 바꿈 — 옛 것은 `_backup_1018/`.
- 🔴 **워터마크 알파**: 중국 모델러 BLP 일부(S2 새턴)는 알파에 「…未经许可禁止上架使用」 워터마크만 있고 98%가 0. 워크3는 none 층 알파를 무시하지만 우리 _cut 재질은 몸이 사라지고 Blender 그림은 검다. model_pull이 none 층 전용 텍스처만 알파를 채운다(transparent 층과 공유하면 그대로 — ArtBinder MakeOpaqueSlot에 맡김).
- 🔴 **export_mdx_anim_fbx json의 pre2는 유니티가 못 읽는 이름**(lifespan·rate·filter 정수 등). 유니티용은 `pre2Unity`(=대본 models[].pre2 형식, pivot·tracks 포함). 새 도구도 pre2_unity.py를 import할 것.
- 🔴 **OriginalVfxBuilder는 층 색(tint)을 안 읽는다** → Blender로 만든 메시 층 색은 텍스처에 구워 넣는다(gen_skill_vfx_wc3의 tex(…, tint)). 입자 색은 pre2Unity colorsRGB01.
- 🔴 **Blender 오브젝트 애니 FBX의 테이크 이름 = 장면 이름**(bake_anim_use_all_actions=False). 클립 이름을 맞추려면 `scene.name = "s0_Stand"`.
- **verify_mdx_fbx.py는 루트/*/*.json을 다 읽는다** — `_work`(_approx.json)·`_render`(_report.json)는 10-18에 건너뛰게 고침.
- **PRE2 색 순서(RGB/BGR)는 미확정** — 우리 반입기(CinematicImporter)는 RGB로 읽는다. kineticfield가 파랑(RGB)/주황(BGR) 갈림 → 구현담당3이 에디터에서 확인 예정.
- **워크3 기본 모델(Abilities\…·DNC…)은 맵에 없어 못 꺼낸다**(로컬에 War3.mpq도 없음). DNCLordaeron*은 낮밤 조명 모델이라 이펙트가 아님.
- 파칭코 fx: 이미지 뷰어(Read)는 알파를 무시해 반투명 줄이 흰 띠처럼 보인다 — 알파 값은 PIL로 확인. 8비트 렌더+bloom은 띠가 생겨서 blur는 float FFT + 디더(gen_pachinko_fx.blur_rgb·dither8). 영상 위 합성 시트는 글자·코인(txt 층)은 알파 합성, 나머지는 더하기.
- zsh: `${@:-a b c}`는 한 덩어리 문자열이 된다(run_all 첫 판이 아무것도 안 렌더) → 배열 `LIST=(…)`로.

## 10-15~16 세션에 추가로 끝낸 일 (PM 지시, 전부 반영·커밋됨)
- **초월_최상호_AD 컷인 title 재제작**(칭호 「킹카가될뻔했던사나이」): 칭호는 컷인 C안 `title` 레이어에 박혀 있다(다른 10층은 불변). PM이 반입·푸시(7404b80d9).
- 🔴 **컷인 정본 폴더는 `~/grd/GRD_cutin_swap/layers`**(PM이 import_cutin 기본 src를 이걸로 바꿈, c8ad2f0ed). 옛 `~/GRD_cutin/layers`는 10-08 원본판(스킨 교체 전) — 그걸로 import_cutin을 돌리면 교체 15기가 옛 판으로 덮인다. **컷인을 고치면 swap/layers에 반영.** 주의: 홈 폴더 이동 뒤 `~/GRD_cutin`(원본 render/layers/C_all, `_prev_1015` 옛 최상호 백업)이 `~/grd/` 아래에 안 보인다(찾아도 없음) — 필요하면 `Tools/blender/gen_cutin_comp.py`의 CUTIN_HOME=`~/grd/GRD_cutin_swap`(render·layers 있음)으로 돌릴 것. gen_cutin_comp C all <유닛>은 `C_all/`에 still·mp4, `layers/`에 층을 쓴다.
- **파칭코 불꽃(SymbolsFire)·전생슬(SymbolsSlime) 보석 숫자**: `~/grd/GRD_pachinko_art/symbols_fire/`·`symbols_slime/`(각 45파일, 리제로와 같은 이름·크기·여백, contact_sheet.png). 도구: `SYM_THEME=fire|slime /usr/bin/python3 Tools/pachinko/symbol_rz.py 1 2 3 4 5 6 7 8 9` 와 `--pink 1…9`(인자 없이 돌리면 리제로 시트 단계가 붙음). 팔레트·베벨은 스크립트 상단 PAL·BEVEL·SIDE, 리제로 크기 맞춤 배율 RZ_FIT=0.89. PM 반영 완료(ea7e307bf).
- 대기 중: 파칭코 4테마 밤샘 작업에서 구현담당2가 그림(告 마름모 카드·둥근 돔 PUSH 버튼·炎 문양·CRITICAL BONUS 로고 등)을 직접 요청할 수 있다 → `~/grd/GRD_pachinko_art/`에 만들어 구현담당2에게 경로, PM에 한 줄.
- 경로 규칙(10-16): 산출물 폴더는 `~/grd/GRD_*`로 이동했다. 새 산출물도 `~/grd/GRD_<이름>/`.

## 이전 세션(10-09~10-14)에 끝낸 일과 위치
| 일 | 산출 위치 | 도구(재생성) |
|---|---|---|
| **원작 스킨 교체 25기** 팩(model·model_fixed·clip_map·scripts·pairing) · 교체목록.csv(PM 정본) | `~/grd/GRD_skin_swap/<이름>/` (이전 판 `_model_fixed_prev/`, 검증표 `_재수출검증.csv`, 클립 중 알파 `클립중_알파변화.json`, 알파 복구표 `_알파복구.csv`, 재복사 목록 `_재복사목록.csv`) | `Tools/w3x/skin_swap_pack.py [--new]` → `swap_refix_all.py [팩]`(재수출+검증) |
| 교체 전후 비교 카드·목록 시트 | `~/Desktop/구랜디스킨모음/교체24_전후비교/` | `Tools/blender/swap_before_after.py` · `Tools/w3x/swap_before_after_run.py [로스터]` (옛 FBX는 git `8c522dbce^`에서 `~/grd/GRD_swap_before/`로 풂, 김경현만 `41ad6a5d0`) |
| **BLP 알파 버그 전수 복구** (Assets 밖 ~2,160개 덮어씀, Assets용 고친 PNG 228) | `~/grd/GRD_blp_audit/` (audit.csv·assets_fixed/·assets_refix_rel.txt) | `Tools/w3x/blp_decode.py` · `audit_blp_alpha.py [--fix]` · `fix_pack_textures.py` |
| 워크3 기본 텍스처 절차 재현 64종(Assets 파일 248) | `~/grd/GRD_blp_audit/placeholder_v2/`(+`_원본이름/`), 전후 `v2_전후시트.png` | `Tools/w3x/gen_wc3_textures.py [--all]` (충격파 45% 알파·얇게, 글로 후광↓, 별 굵게 — PM 조정 반영) |
| 연출 대본(원작 스킬 트리거 해석) 138개 + v2 새 컴파일 | `~/grd/GRD_scenes/` (`auto/`=전달본 batch_01~05, `auto_v2/`·`v2_compare/`·`v2_diff.csv`, `batch_01~05.txt`, `auto_render/`) | `Tools/w3x/jass_sim.py` · `scene_auto.py [prep|compile]` · `scene_auto_render.py` · `scene_thin.py`(폭주 솎기·라벨) |
| 컷인 C안 교체 15기 + 자동 검사에 「몸 비율 ≥12%」 | `~/grd/GRD_cutin_swap/C_all/` | `Tools/w3x/cutin_swap_run.py` · `Tools/blender/cutin_pick.py`(BODY_MIN·BODY_EXEMPT) |
| S2 날개 6종 Unity용(FBX·Textures·wing.json·미리보기) | `~/grd/GRD_wings/` | `Tools/blender/wing_pack.py` (원본 `_raw/`는 `export_mdx_anim_fbx.py`) |
| 가프 Spell 머리 조사 → 24기 재수출(쿼터니언 부호 연속+1/4프레임 굽기) | `spell_bones.json`, `spell_프레임시트_*.png`(박은석_몽키) | `swap_refix_b3.py`(수정) · `swap_verify_cont.py` · `dump_clip_bones.py` · `swap_clip_frames.py` · `clip_alpha_changes.py` |
| **박민수_조로**(S2 `!0702_Zoro`) 팩 + S2 연출 대본 5개(BxV·BxW·BxX·BxZ·Bxb, **미연결 — 특별함은 스킬 없음, 보관만**) | `~/grd/GRD_skin_swap/박민수_조로/` | `jass_sim`(JASS_S2=1) · `scene_auto` |
| **파칭코 그림 재료 전부**(도쿄구울 숫자 v3·로고·틀·UI·이펙트·리제로 보석 숫자) | `~/grd/GRD_pachinko_art/` — `symbols_v3/`(도쿄구울 숫자, 시안 SVG 렌더·불꽃 6프레임·별빛·블러), `symbols_rz/`(리제로 보석 숫자 1~9 + `_s0/_s1` 반짝임 + `_blur` + `_pink` 분홍 리치, 7은 금+「強」), `fx/`(카구네 촉수 4·붓 획 3·빛줄기 2·집중선·시계 문양·나비 4프레임·불똥 8), `logos/`·`ui/`·`machine_frame_1920x1080.png`·`machine_layout.json` | `Tools/pachinko/` : `symbol_v3.py`+`symbol_v3_post.py` · `symbol_rz.py [숫자…] [--pink]`(헤드리스 Chrome SVG) · `fx_art.py [kagune brush streak speed clock butterfly spark]` · `pachinko_art.py`. 구현담당2에게 경로 전달 완료 |
| **대체 폭발 겉모습 조정**(tots_R 1건 + 17건 적용·11건 예외) | `~/grd/GRD_scenes/tone_28/`(before/after·compare.png·except.csv), auto/에 반영 | `Tools/w3x/scene_tone.py`(tots_R 포함) · `scene_tone_28.py [--apply]`(수명 3초 이상만 0.3배·1.2초) |

## 남은 일·대기 중인 것
- **구현담당2 재반입 대기 확인**: symbols_rz 전체·fx 나비/카구네·tone_28 17건·tots_R은 구현담당2에게 「교체됨」 알림만 보냄(반입·재검사는 구현담당2 몫). PM이 「이펙트 그림 추가 요청이 곧 갈 수 있다」고 함 → fx_art.py에 항목 추가해 쓰면 된다.
- 리제로 숫자는 PM 통과(7의 強 조정까지 끝, 최종 배율 0.88). 분홍 7도 분홍(強만 금).
- PM 지시 대기. 교체목록.csv에 새 줄이 생기면 `skin_swap_pack.py --new` → `swap_refix_all.py <팩>` → `swap_before_after_run.py <로스터>` → 구현담당2에게 반입 목록 + PM 한 줄 보고(컷인이 필요한 초월/불멸/영원은 `cutin_swap_run.py`).
- `vivi_Skill_4_Mana`(영원_문필환) 연출 대본은 이벤트 형태가 달라 못 풀었다(대본 없음 유지).
- 「BLP 못 찾음」 중 「기타」 187종(룬·UI·지형·모델용 그림)과 선택 원·바닥 얼룩 13종은 절차 재현 안 함(이름만으론 모양 불명).
- 대표 연출 대본은 S2 j 기준 새 해석(S2 신규 유닛 스킬들)이 필요하면 JASS_S2 모드로 확장(아래 함정 참고).

## 함정 (🔴 꼭 읽을 것)
- 🔴 **BLP(JPEG) 디코드**: PIL로 열면 4번째 채널(알파)을 CMYK의 K로 곱해 투명 자리가 검게, 알파는 0이 된다(캐럿 꼬리 검은 네모). 반드시 `blp_decode.decode()`. `mdx_extract.py`엔 반영됨. 팔레트 BLP는 PIL 그대로. 이전 추출본은 `audit_blp_alpha.py --fix`로 고쳤다. 맵에 없는 워크3 기본 텍스처(307종)는 어림 그림(placeholder)이다.
- 🔴 **S2 경로 분리**: 같은 ID(H09F 등)가 구버전(`원랜디_구버전_스킨`·ord.mpq·`원본/`)과 S2(`원랜디_신작_스킨`·`~/grd/GRD_motion_trial/_work/s2.mpq`·`원본_S2_2.323/`·`s2_skin/work`)에서 **다른 모델**이다. 「신작」 줄(번호 신작… 또는 모델이 `war3mapImported\…`)은 S2만 쓴다(`skin_swap_pack.py`의 `C["s2"]`). S2 MPQ는 `Archive(path)`(이미 512바이트 헤더 뗀 `_work/s2.mpq`).
- 🔴 **JASS_S2=1 모드**: S2 war3map.j는 함수·변수 이름이 난독화돼 있다(BST=Setunit·BTV=Setlocation·BWT=Setreal·BVU=Setinteger·BJ3=SleepForStageNext·BJx=Flush·BJ7=SettingEx·`HR[DR]`=스테이지·`LoadUnitHandle(E2,0,DR*EM+N)`=슬롯·`$65303851`=`'e08Q'` 16진 ASCII). `jass_sim.translate_s2`가 옛 방언으로 번역한다. 트리거는 이름 대신 함수명(BxV…)으로 부른다. 단위 자료는 슬크(unitui·unitbalance·unitdata·unitweapons). **기본 모드로 `scene_auto.py compile`을 돌리면 `~/grd/GRD_scenes/auto/`가 덮인다** — 10-09에 실수로 덮어 옛 jass_sim(git `ec211f3ae`)으로 복원한 적 있음. S2용은 반드시 `JASS_S2=1 SCENE_TABLE=… SCENE_OUT=<다른 폴더>`.
- 🔴 **SleepForStageAdd(10-14)**: Add의 n은 「스테이지 += n」이다(kikoyou_skill_2가 1→3으로 건너뜀이 증거). `jass_sim.py`는 `JASS_ADD_REL=1`일 때만 그렇게 읽는다(기본값 옛 방식 그대로). **`~/grd/GRD_scenes/auto/`의 17개 트리거(19파일: Sabo_Skill_1·Legend51·Ed_Skill_1(_Item)·Z_Skill_Mana·Marco_S2·Franky_Skill_Mana1·Ain_Skill_1·Transpom_AceMana·Transpom_Ace_bulRemake·carrot_skill_2·hancock_skill_Mana·tots_R·Ryougi_Skill_3·King_skill_2(_tr)·kikoyou_skill_2·vivi 제외)는 JASS_ADD_REL=1 판으로 교체된 상태 — 기본 모드로 `scene_auto.py compile`을 돌리면 옛 폭주 판으로 덮인다.** 재컴파일하려면 JASS_ADD_REL=1을 주거나 `~/grd/GRD_scenes/add_rel_26/반영목록.csv`의 파일을 다시 `add_rel_26/out/`에서 복사. 백업 옛 판 `~/grd/GRD_scenes/auto_backup_1014/`. vivi_Skill_4_Mana 대본은 `~/grd/GRD_scenes/vivi4_rel/`(auto/엔 아직 안 넣음).
- 🔴 **겉모습 조정(10-14)**: 자동검사 실패 3건(vivi_Skill_4_Mana·Ryougi_Skill_3·Franky_Skill_Mana1)은 `Tools/w3x/scene_tone.py`(대체 폭발 0.3배·수명 1.2초, Franky ×0.8)로 auto/에 덮었다. 원인은 맵에 없는 원작 이펙트 모델을 SuperBigExplosion(Birth 10초)로 갈음한 것이 쌓여 어두운 연기가 판을 덮음 — `scene_auto.py compile`/`scene_thin.py` 뒤엔 scene_tone.py를 다시 돌릴 것. 해석(타임라인)은 그대로.
- 🔴 **대본 후처리 순서(10-14)**: auto/는 `scene_auto.py compile` → `scene_tone.py`/`scene_tone_28.py --apply` → `scene_fill_ourskill.py` 순으로 다시 입힌다(재컴파일하면 겉모습 조정·ourSkill이 날아간다). `scene_tone_28.py`는 before/ 원본을 우선 읽는다.
- 🔴 **파칭코 숫자 렌더**: 필터 수치를 렌더 크기에 비례해 키우면 시안보다 약해진다(viewBox 600 좌표계 그대로, SVG 크기만 1024). `symbol_rz.py`의 `fit_all`은 전 숫자에 같은 배율을 준다 — 숫자 하나(7의 強)만 바꿔도 전체를 다시 돌려야 크기가 같다. 보도니의 가는 획은 보석 면을 못 담아 윤곽을 INF=30으로 부풀렸다. 시스템 폰트(히라기노) 대신 OFL 폰트만 쓴다.
- 🔴 **쿼터니언 연속 굽기**: 회전 키는 부호를 맞춰(내적≥0) 구워야 한다. 큰 회전(한 프레임 60° 초과) 뼈가 있는 팩은 FBX를 1/4프레임 간격(`bake_anim_step=0.25`)으로 내보낸다(`swap_refix_b3.py`가 자동). 검증은 `swap_verify_cont.py`(오일러 점프 >180° 0개 + 이전 판과 자세 차 <1°). 가프 머리 사라짐 원인은 알파가 아니라 한 프레임 175° 회전의 보간(유니티 확인은 구현담당1 몫).
- 교체 모델 재수출은 **Stand 첫 프레임=바인드**·숨김 뼈(스케일<0.05)는 옛 휴식 유지·스케일 0은 1e-3 클램프(`swap_refix_b3.py` 머리말). 긴 모델은 Blender 가져오기가 액션 이름을 63자에서 잘라 번호를 잃는다(롭): 검증·카드는 「움직임 최소 액션=Stand」 대체(`swap_before_after.py`), 바인드 검수는 `?`로 나온다.
- 클립 도중 지오셋 알파가 바뀌는 건 양재모·박민석·김영학·임건웅 4기뿐(`클립중_알파변화.json`) — 구현 쪽이 그 시각에 켜고 끈다. `clip_map.hiddenMeshes`는 시퀀스 **시작 시각** 알파 기준.
- `*.blp.png` 이름은 경로의 `\`가 `_`로 납작: `Textures_GenericGlow64.blp.png` = `Textures\GenericGlow64.blp`. 모델 json의 `textureFile`/`texture`로 원본 BLP를 찾는다.
- 이펙트 라이브러리 `~/grd/GRD_orig_vfx_trial/<모델>/`은 평평한 단일 폴더(S2 전용 모델도 여기 추가). render_scene·scene_scripts의 작업 폴더 목록에 `s2_skin`을 넣었다.
- Blender 5.2: 액션 `fcurves`가 없다(레이어드 액션) → `layers/strips/channelbags`로 접근. FBX 임포트는 쿼터니언 곡선으로 바꿔 오일러 원시값을 못 본다 → `io_scene_fbx.parse_fbx`로 직접 읽는다(`swap_verify_cont.py`).
- 이전 판 함정(유효): verify_mdx_fbx는 「보이는지」를 못 본다 · 맵 밖 모델은 어림 그림 · `render_mdx`·`render_cast_vfx` 동시 실행 시 `_report.json` 덮임 · macOS엔 `timeout` 없음·`sed -i ''`·zsh에서 따옴표 안 친 변수 분할 안 됨 · 아이콘 대응은 w3a aart/arar/auar가 정본.
- 🔴 **저작물**: 원작 모델·텍스처·소리는 블리자드·중국 모델러·원작 제작자 것. Assets 반입은 PM·사장님 판단, 창고는 데스크톱·`~/grd/GRD_*`에만.

## 원작 창고 15~18 (10-08 판, 그대로 유효)
`~/Desktop/구랜디스킨모음/` 15_원랜디스킨(유닛 모델 182) · 16_원랜디스킬(이펙트 770) · 17_원랜디아이콘(243) · 18_원랜디소리(154). 전체 재현 `zsh Tools/w3x/skin_warehouse_run.sh`. 자세한 칸 설명은 git 이력(커밋 `94716b42d` 직전 판 handoff) 참고. 이름 못 찾은 455개는 PM이 「파지 마라」.
