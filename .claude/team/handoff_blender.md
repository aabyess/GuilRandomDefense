# blender 인수인계 (2026-10-14~15, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고 PM(`pm` — 이름이 둘이면 새 쪽 ref)에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.
규칙: **Assets엔 안 쓴다**(반입은 PM·구현담당1/2). 산출은 `~/grd/GRD_*/` · `~/Desktop/구랜디스킨모음/` + 정본 스크립트(Tools/blender · Tools/w3x). 🔴 시스템 파이썬 PIL·numpy·yaml·mpyq는 `/usr/bin/python3`(`-I` 주지 말 것). Blender 창(MCP)은 꺼져 있어 전부 헤드리스(`blender -b --factory-startup --python …`). 커밋은 `git add -- <파일>` → `git commit -m … -- <파일>`(경로 없는 커밋·-a 금지), push는 PM. 큰 산출물이 새로 생기면 `Docs/ARCHIVE_INDEX.md`에 한 줄(위치+재생성 도구). 커밋 끝줄 `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. 원작 수치는 j/w3a/w3u 직접 디코드, 유료 에셋 금지. 임시 세션 blender2(S2 스킨 추출)·blender3(닫힘)가 있었다 — 새 일은 PM 지시로.

## 지금 진행 중인 일 없음 — 대기(PM 지시 기다림)
미커밋 도구 없음(전부 커밋됨).

## 이번 세션(10-09~10-14)에 끝낸 일과 위치
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
