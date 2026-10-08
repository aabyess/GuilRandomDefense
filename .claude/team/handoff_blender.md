# blender 인수인계 (2026-10-08 밤 마감, 직전 blender 세션 → 새 blender 세션)

새 세션: 이 파일을 먼저 읽고 PM(`pm` — 이름이 둘이면 새 쪽 ref)에게 「[blender → PM] 준비 완료」. 일이 끝나면 이 파일을 비우고 커밋.
규칙: Assets엔 안 쓴다(반입은 PM·구현담당). 산출은 ~/GRD_*/ · ~/Desktop/구랜디스킨모음/ + 정본 스크립트(Tools/blender·Tools/w3x). 🔴 시스템 파이썬 PIL·numpy·yaml·mpyq는 `/usr/bin/python3`(`-I` 주지 말 것). Blender 창(MCP)은 꺼져 있어 전부 헤드리스(`blender -b --factory-startup`, 5.2.1). 유료 에셋 금지. push는 PM만. 커밋은 `git add -- <파일>` 뒤 `git commit -- <파일>`.

## 진행 중인 일 없음 — 대기(PM 지시 기다림)
남은 일(사장님 판단 대기): **원작 이펙트·아이콘·모델의 Assets 반입** — 사장님이 PM에게 정하신 뒤 PM이 시킨다. 그 전엔 반입 준비도 하지 말 것(PM 지시).

## 오늘 한 일 (전부 커밋됨, 미커밋 없음)

### 1) 원작 창고 15~18 — `~/Desktop/구랜디스킨모음/` (Assets 밖, 원작 저작물)
| 폴더 | 내용 | 크기 | 검사 |
|---|---|---|---|
| `15_원랜디스킨` | 원작 맵 안 커스텀 **유닛 모델 182개(유닛 318명)** — `<대표uid>_<원작이름>/`: FBX(뼈+스킨 가중치 균등, 시퀀스마다 30fps 액션)·`Textures/*.png`·`<모델>.json`·`preview.png` | 667MB | 부품 수 대조 182/182 통과 |
| `16_원랜디스킬` | 이펙트·투사체·장식 **모델 770개** — `<모양 묶음>/<모델>/` 같은 구성(+ 오늘 초월 37종 포함) | 348MB | 770/770 통과 · 메시 있음 663 · 입자 전용 107 |
| `17_원랜디아이콘` | 아이콘 **243개** PNG(명령 224·비활성 2·기타 17) | 4.5MB | — |
| `18_원랜디소리` | 소리 **154개**(mp3 153·wav 1) 원본 그대로 | 3MB | — |
- **전체 재현**: `zsh Tools/w3x/skin_warehouse_run.sh`(저장소 루트에서). 단계: `original_asset_inventory.py`(이름 모으기 → `~/GRD_motion_trial/inventory/inventory.json`) → `vfx_warehouse_index.py`(→ `~/GRD_motion_trial/original_vfx/models.json`) → 15: `skin_warehouse_build.py prep/build` · 16: `vfx_warehouse_build.py` · `icon_warehouse_build.py` · `sound_warehouse_build.py`. 작업 폴더: `~/GRD_motion_trial/original_skin/`·`original_vfx/`(work=MDX·_tex, fbx, preview, verify.json). 변환기 `Tools/blender/export_mdx_anim_fbx.py`, 검사 `verify_mdx_fbx.py`, 미리보기 `render_mdx.py`(15)·`render_cast_vfx.py orig`(16).
- **`index.csv` 칸**
  - 15: 원작 ID · 원작 이름 · 우리 이름(MASTER_UID_ROSTER_MAP, 169/318 대응) · 모델 파일 · 애니 클립(`s<번호>_<이름>`) · 변환 · 부품 수 검사 · 퇴화 삼각형(원본) · 메시/뼈 수 · 원본 지오셋/정점/삼각형 · 파티클 줄기(JSON만) · 리본 · 폴더 · 미리보기. 같은 모델을 쓰는 유닛은 한 줄씩, 폴더는 같음.
  - 16: 원작 모델 파일 · 폴더 이름 · 모양 묶음/후보(이름·텍스처 키워드 투표, 틀릴 수 있음) · **쓰는 원작 능력/더미 유닛/트리거(used_by)** · 참조 수 · **주인 유닛 후보**(H코드+이름, 공격 트리거 사슬로 넓게 잡힘) · 변환/검사 · 클립 · PRE2/리본 수 · FBX · 미리보기.
  - 17: 아이콘 경로 · 종류 · 쓰는 원작 능력(w3a aart/arar/auar)·유닛(w3u uico, 우리 로스터)·아이템(w3t iico)·업그레이드(w3q gar1)·버프(w3h fart) · **우리 스킬 대응**(skill_icon_map.csv가 그 아이콘 경로를 쓰는 SkillData) · PNG.
  - 18: 파일 · 크기 · 길이(MPEG 비트레이트 추정) · j 변수(gg_snd_…) · 트리거하는 원작 트리거 · 주인 유닛(추정) · 쓰는 능력(w3a). 트리거 찾은 것 87·주인까지 77.
- 목록 문서: `Docs/research/ORIGINAL_ASSET_INVENTORY_2026-10-08.md`(MPQ 2,787개 중 이름 확인 2,332·**못 찾은 455개**(전부 암호화, 11.4MB — 이름 없이는 종류도 못 가림, 그중 5.3MB 1개는 war3map.j 추정). 맵 안: mdx 952·blp 1,209·소리 154. 음악·커스텀 로딩 화면은 맵 안에 없음. 맵 밖(워크3 기본)은 이름만 참조).

### 2) 초월 스킬 ↔ 원작 이펙트 짝표 (PM 첫 지시)
`Docs/research/TRANSCEND_VFX_PAIRING_2026-10-08.md`(+ `.csv`, 근거 줄·필드 포함, 생성 `Tools/w3x/transcend_vfx_pairing.py`). 초월 스킬 133개 중 짝 13개(이름 11·능력코드 1·문턱 2), 나머지 120은 사장님 신규 사양이라 원작 대응 없음. 짝 모델 37종은 `~/GRD_motion_trial/transcend_vfx/`(31 변환 · 입자 전용 2 · 변환불가 4 = 워크3 기본 모델) — 같은 변환은 16에 합쳐짐. 새 스킬 아이콘 29행도 `Tools/skill_icons/skill_icon_map.csv`에 추가(SkillData 943개 전부 행 있음, 연결은 구현담당2).

### 3) 폭뎀·폭증 조사 (PM 지시)
`Docs/research/EXPLOSIVE_DAMAGE_2026-10-08.md` — 원작 폭발형 = 대상의 `A11S`(「폭발형데미지 증폭」) 레벨을 읽는 식 43곳 ×(0.20+0.05×레벨), 라인 몹(PV<200)만. 폭증 = `A11S`+N(1레벨=5%p, 상한 23). 마깎(`Aegr`)·마뎀증폭(`AIsr`)은 별개 능력. 사장님 답(10-08): 폭발형 = 원작 43곳 + 사장님 사양 단일·끝딜, 폭증 = 내 라인 적 전부 → 구현은 구현담당2가 함(커밋 b0291546a 등). **구현담당2가 「원작 43곳 → 우리 스킬 대응」을 물으면 도울 것**(문서 §3.4 함수 36개 목록, 재현 스크립트 §재현).

### 4) 원작 대응 짧은 조사 (구현담당2가 폭증 연결에 씀)
- 아오키지 히든(Hidden_Aokiji +2, 채팅 「아오키지조합/aokiji」, 1번만) → 우리 `히든_성탄`(원작 h041 쿠잔푸른 꿩; 조합식 a6c190c7d에서 삭제, `하급도박` 보너스 풀 10%로만 얻음).
- 루치 영원(Eternal_Lucci +2, 「검은정의를좇는하얀새/lucci tr」, 원딜 항법 안 쓴 플레이어만) → 원작 H08W CP.Zero = 우리 `초월_임장혁_AD`(조합식 `Recipes/초월_임장혁_AD.asset`, commandId 「Janghyuk tr」).
- 원딜 항법 연동(one_dill 카운터)은 안 봄(PM 지시).

## 한계·함정
- 🔴 **저작물**: 원작 모델·텍스처·아이콘(애니 캐릭터 그림)·소리(성우)는 블리자드·중국 모델러·원작 제작자 저작물. Assets 반입 전 사장님 판단 필요. 지금 창고는 데스크톱에만 있다.
- **이름 못 찾은 455개** 미확인(PM이 「파지 마라」). 이름 모으는 곳을 늘리면 줄 수 있다(w3t ifil, PRE2 번호 등).
- **팀색**: ReplaceableTextures\TeamColor/TeamGlow 층·지오셋은 FBX에서 뺐다(15에서 24모델이 팀색 판을 뜯어냄). 미리보기(render_mdx)에는 팀색이 붉은 판으로 그려진다 — 정상.
- 보간은 선형(헤르미트/베지어 접선 무시), 뼈 가중치는 행렬 그룹 균등, 지오셋·층 알파·UV 이동·PRE2·리본은 JSON에만. 맵 밖 워크3 기본 텍스처는 `mdx_extract`가 비슷한 그림으로 대신 깐 것(원작과 다름).
- **부품 수 검사 규칙**: 넓이 0 삼각형(같은 정점 번호)과 정점 집합이 같은 겹친 면(양면 복사)은 Blender가 합치므로 기대값에서 뺀다(index의 「퇴화 삼각형」). 이걸 안 빼면 헛 실패가 나온다.
- 함정: 파일 이름에 `\`가 든 모델(`war3mapImported\x.mdx`)은 폴더 이름을 `__`로 치환(`export_mdx_anim_fbx.py`). 뼈 이름은 GBK 깨짐이 있어 `n<번호>_<ASCII>`로 유일화, 시퀀스 이름은 `s<번호>_…`. 스케일 0 키는 1e-3으로 클램프(역행렬 없음 오류 방지). `render_mdx.py`는 지오셋 없는 모델을 이제 건너뜀(예전엔 한 모델이 죽으면 그 배치가 멈춤).
- 🔴 `render_mdx`·`render_cast_vfx` 여러 개를 동시에 돌리면 `_report.json`을 서로 덮어쓴다(무시해도 됨). macOS엔 `timeout` 명령 없음. zsh 커밋은 `git commit -- <파일>`.
- 기존 함정(10-08 새벽 판에서): 셀 컷인 C안(`~/GRD_cutin/`, `Tools/blender/run_cutin_c_all.sh`·`cutin_picks.json`)과 `mesh.materials.clear()` 재질 슬롯 버그는 그대로 유효 — 컷인 상세는 git 이력(커밋 `94716b42d` 직전 판 handoff)에서 볼 것.

## 이전 판 납품(10-07까지, 반영됨 또는 반영 대기)
섬 루프 v2(~/GRD_title_loop_v2) · 워크3풍 UI 17종+창 4종+명령 아이콘 6(~/GRD_wc3_ui) · 적 출발 포탈 · 바다 물(~/GRD_water) · 버프 아이콘 7종(~/GRD_buff_icons) · 문 v3·폭탄·종이비행기·설정 판·선술집 UI·스킬 아이콘 170행 · 컷인 C안 41기(~/GRD_cutin, 구현담당2가 Unity 반영) · 초월 시전 이펙트 조사(`Docs/research/TRANSCEND_CAST_VFX_2026-10-08.md`).

## 함정 추가(10-09 시범 납품)
- 🔴 **verify_mdx_fbx는 「보이는지」를 못 본다.** 부품 수·삼각형·뼈·텍스처 파일만 대조하므로 33/33 통과인데도 레일건(Mdx_Effect_Railgun)은 빈 화면이었다. 원인: 번개가 **TXAN KTAT(UV 오프셋 계단 스크롤)** 로만 보이는 모델인데, `export_mdx_anim_fbx.py`가 `info["txan"]`을 `x.get("id")`로 찾아 항상 None이 돼 uvAnim이 버려졌다(TXAN엔 id 필드 없음, 번호=순서). 고침: 레이어 texanim 번호로 직접 인덱싱. FBX 정지 UV는 텍스처 아래 20% 빈 곳. 시범 json의 `unity.layers[].uvOffsetClip0`(STEP,[초,u,v], Unity offsetV=-v)에 담았다. 변환 산출은 **텍스처 입힌 실제 렌더 사진**(FBX 다시 읽어 emission+가산)으로 반드시 볼 것. 이전 산출 창고(15·16)도 UV 애니 모델은 같은 결함 — 재변환 필요.
- `render_cast_vfx.py` 미리보기는 TXAN UV 애니를 안 읽는다 → 같은 모델이 빈 막대로 보임.
- 아이콘 대응 판정: w3a의 aart/arar/auar가 정본. 새 표(index 기준)가 맞고 기존 skill_icon_map의 「코드 파일명에서」 png 64건은 틀림.
