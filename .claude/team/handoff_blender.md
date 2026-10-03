# blender 인수인계 (2026-10-03 마무리, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고, 다 읽은 뒤 PM에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.

## 끝난 일 (오늘)
1. **분홍 오라**: 원작 사진의 초월 발밑 분홍·흰 조각 = **HandsAura2**(A07O 「스피어 초월함」 atat, 로스터 23명)였다. 모델은 맞았고 근사 텍스처(Zap1_Red·Purple_Glow)가 틀렸다(mdx_extract.placeholder가 이름 「zap」만 보고 지그재그 번개를 그림). 방사 광선 + 분홍 바탕으로 다시 그림 → 2차(더 밝게)까지 PM이 게임에 반영(크기 1.0 · 세기 Purple 1.0 / Zap 0.7).
   - 산출: `~/GRD_motion_trial/초월_상시오라/HandsAura2/Textures/` 두 장 · 비교.png(원작 사진 ↔ 렌더) · 설명.md에 정정 기록.
   - 남은 관찰(미해결): 원작 판이 사진상 우리 모델 치수보다 1.5배쯤 커 보임(맵 쪽 배율 필드 미확인) · 지오셋 0의 Yellow_Glow는 사진에 안 보임(유니티에서 끄거나 약하게) · 정품 Zap1_Red.blp를 구하면 교체.
2. **고유 동작 5묶음** 여섯 → `~/GRD_motion_trial/고유_5묶음/`(README.md에 클립표·판단·결함검사). **PM이 Assets 반영 완료(06ba66713, Generic 확인)**. 단 README의 「박도진(구부정)」「손오공(Attack 2배속)」은 사장님 답 대기였다 — 반영 여부는 PM에게 확인.

## 도구 위치·쓰는 법 (전부 Tools/blender/, 커밋됨)
- `gen_aura_approx_tex.py` — Zap1_Red·Purple_Glow 근사 재생성. `blender -b --factory-startup --python … -- <출력폴더>`. 값 조정은 stops(색 사다리)·core·ray 줄.
- `render_mdx.py` — MDX 구경 렌더. 환경변수 `GROUND_RGB=r,g,b`(바닥 판 + 배경, 「사진」 시점 추가) · `EMIT_STRENGTH`(가산 세기, 기본 1.6 — 실제와 맞추려면 1.0).
- `fix_unit_fbx.py` 「이름@동작」 변형 — `split_clips`에 새 키: `src`(자를 클립을 take_names 이름으로 고름) · `step`(k배속) · `land=dict(to="Idle", k=N)`(끝 자세에서 Idle 첫 자세로 N프레임 보간 — 끝이 공중인 한 번짜리용). `clip_floor_feet`에 발뼈 이름 목록을 줄 수 있음(비-mixamorig 리그). `drop_material_faces`는 클립 때문에 원본을 다시 읽은 뒤에도 재적용됨. `skip_shapes=True`는 모양 키 클립 중복(Idle·Attack ×2) 방지.
- `clip_sheet.py <fbx> <png> [--side] [--only Idle,Move]` — 클립마다 5프레임 렌더 시트(뒷면 컬링 켬).
- `frames_sheet.py <fbx> <클립이름 일부> <간격> <png> [--front] [--from N --to M] [--cols C] [--H 키] [--zoom Z]` — 한 클립을 간격 프레임으로(구간 자르기용, 카메라가 골반을 따라감). 골반 높이 곡선이 stdout.
- `verify_clips5.py <fbx…>` — 키·뒤집힌 면 비율·순흑색 텍스처·Emission·뼈 꼬리·구조. clip_table·clip_check·bind_check와 같이 돌린다(수치.md 형식은 5묶음 폴더).

## 함정 (오늘 새로)
- **Blender 5의 레이어 액션**: `act.fcurves` 없음. FBX에 모양 키가 있으면 같은 이름 액션이 하나 더 있고(슬롯 KESlot) 그걸 고르면 포즈가 안 변한다 → 슬롯 `target_id_type == 'OBJECT'`인 액션만, `animation_data.action_slot`도 지정(clip_sheet·frames_sheet가 이미 처리).
- **블렌더 상대경로 저장**: 시스템 파이썬이 아니라 Blender 안에서 `image.save()`는 상대경로를 못 쓴다 → 절대경로.
- 시스템 `python3`에는 PIL·numpy·mpyq가 없다(Blender 안에는 numpy 있음). 원작 j·w3a는 `Tools/w3x/원본/war3map_new.*`(디코드본)을 직접 읽는다.
- **원본 클립이 한 클립에 연출을 이어 붙인 경우**(강민호 skill_b: 나타남→뜀→도약→사라짐)는 이름·길이로 못 가른다 — 골반·발 높이 곡선 + 간격 렌더로 구간을 잡을 것.
- 클립을 새로 자르거나 이어 붙이면 clip_table(이음새)·clip_check(바닥)을 **둘 다**.
- Blender 창(MCP)이 꺼져 있으면 헤드리스로 하고 그렇다고 보고(오늘은 내내 꺼져 있었다).

## 남은 보류
- 히든_전유라 Move가 15프레임·이음새 0.066 — 필요하면 split_clips `loop=`로 다듬기(다른 클립도 split에 다 적어야 함).
- 박도진(클립이 구부정) · 손오공 Attack(2배속 가공) — 사장님 결정 대기.
- 정품 War3 텍스처(War3.mpq/CASC) 위치 — 사장님 답 대기. 근사 텍스처 39개 + HandsAura2 두 장이 근사다.
- 적 R67·R73·R74(항목 없음) · 애매 18(Idle만) — 가치 작아 보류.
