# 부가 이펙트·상시 오라 파이프라인 (구현담당2 구역, 2026-10-01)

원작 모델의 부가 이펙트(초월 모델 날개·소매·입자…)와 상시 오라를 유니티에 붙이는 길. 재료는 Blender 세션이 `~/GRD_motion_trial/초월_부가이펙트/<로스터>/`(+`_미대응/`)와 `초월_상시오라/<모델>/`에 `effects.json`·FBX·Textures로 낸다(스키마 `_effects_schema.md`).

## 흐름
1. `sh Tools/sphere_art/run_all.sh` (부가 이펙트) · `sh Tools/sphere_art/run_aura.sh` (상시 오라) — `flatten_effects.py`(**/usr/bin/python3**, PIL 필요)가 effects.json을 `Tools/sphere_art/effects/<별칭>.json`(Unity JsonUtility용 납작 꼴)로 펴고 FBX·텍스처를 `Assets/Resources/Effects/Sphere/Src/<로스터|별칭>/`로 복사.
2. 유니티 `refresh` → `call SphereArtBuilder.BuildAll`(Assets/Editor) → 프리팹·재질(`Sphere/Materials/`)·`Tools/sphere_art/extra.tsv`(전체 재생성) 생성.
3. `python3 Tools/sphere_art/build_table.py` → `Resources/Effects/SphereArtTable.txt`(CSV 대응 + extra.tsv) → `refresh`.
4. 사진: `ClaudeBridge/probe_roster.txt`에 로스터 이름 → `gameshot x.png 1 1280x720 call:MotionShowProbe.ShowFile wait:3 call:MotionShowProbe.CamFront wait:0.5 snap:f.png call:MotionShowProbe.CamSide … call:UnitSphereArt.Describe`(Describe가 부품마다 실제 붙은 뼈를 찍는다). 공격 확인 `call:MotionShowProbe.AttackAll`, 스킬 `PulseAll`. 성능 `AuraPerf.Start|StartJuho|StartKim`.

## 구조
- 두 종류: **부가 이펙트**(로스터별, 슬롯 프리팹 `<별칭>_<imas 글자>_<뼈>.prefab` + extra.tsv 줄 `follow:<뼈>`) · **상시 오라**(모델 통째 `<아트키>.prefab`, 표의 아트 키와 같은 이름이면 `SphereArtTable.Create`가 임시 모양 대신 씀).
- 크기: 부가 이펙트 = (게임 키 30 ÷ 그 모델 몸 키 m)를 위치·크기·속도에 곱함(`bodyHeightM`). 오라 = 몸 없음 → 1m = 24단위(100wc3 ÷ WorldScale 4.167; HandsAura2 지름 40 = 169÷4.167로 검증).
- 배율은 **껍데기(wrapper)에** — FBX 오브젝트 로컬 위치(원작 pivot, 미터)도 같이 커져야 한다.
- 때(`SphereArtTable.When`): 비트 마스크 대기·이동(Animator Speed>0.05)·공격(Attack 상태)·스킬(`UnitSphereArt.PulseSkill`, UnitAttacker가 부름). 표 열 `대기|이동|공격|스킬` 또는 `항상`.
- 붙는 곳: `follow:<뼈>` = 몸 좌표로 세운 뒤 월드 자리 유지한 채 뼈에 옮겨 붙임(Humanoid → GetBoneTransform, Chest 없으면 UpperChest·Spine, Generic → 뼈 이름). 못 찾으면 body. `follow:limb,left|right` = 팔 소매(`SphereArtLimb` — 어깨→손 선분을 아래팔에 맞춤). `hand,*` 프리팹은 `SphereArtHold`(원작 손 pivot을 우리 손 뼈로).
- 컴포넌트: `SphereArtSpin`(메시·리본 회전) · `SphereArtLimb` · `SphereArtHold` · `SphereArtBillboard`(임시 판).
- flatten 규칙 표(코드 상단): `LIMB_PARTS`(소매) · `HELD_PARTS`(쥐는 메시) · `ATTACH_OVERRIDE`(모델별 뼈) · `POSE`(자리·기울기 덮어쓰기) · `FORCE_STATES` 환경변수(단독 이펙트의 때) · `AURA=1`.

## 함정 (겪은 것)
- **Assets 쓰기는 에디터 규칙을 따를 것**: 패치 스크립트가 Assets까지 쓰는지 먼저 볼 것(18:57 사고). 구현담당1이 판을 돌리는 동안 금지.
- 텍스처 알파가 전부 255(검정 바탕)인데 원작 필터가 「블렌드」면 검은 판이 된다 → 가산으로(flatten이 PIL로 감지). **시스템 파이썬만 PIL이 있다**(homebrew python3엔 없음 — 조용히 실패한다).
- 근사 텍스처(맵에 없는 워크3 기본 그림, `approx:true`)는 낱장이다 — 8×8 시트로 자르면 사각 조각, 바위 메시는 검게 죽는다(→ 단색).
- 정점 1개짜리 닻 메시(a_blueeff·BlackFlame·rib_sinobu)는 그릴 게 없고, 리본은 원작이 Dummy 뼈 애니로 휘젓는다 — 애니를 못 옮겨 도는 궤적으로 근사.
- 원작에서 안 켜지는 부품(`never_active`): Ora_Siki2 입자 · BlackFlame 입자 → 제외(임시 모양 유지).
- Art가 **struct**라 `when` 기본값이 0 — `new Art{…}` 때 `when = When.Always`를 꼭 적는다.
- 몸 키가 다른 모델의 부가 이펙트를 다른 로스터에 붙일 땐 비율로(`bodyHeightM`), 팔 벌린 자세의 원작 pivot은 우리 아바타 손과 안 맞는다 → 날개는 가슴, 소매는 아래팔, 손에 쥐는 건 Hold.
- 같은 메시 FBX를 재생성하면 유니티가 FBX 루트 메시 이름의 재질 접미(`_L0_add`)를 떼므로 빌더가 접두 일치로 찾는다.
- 재질은 파일 이름으로 재사용된다 — 설정이 바뀌면(가산↔블렌드) 빌더가 매번 속성을 다시 쓰게 해 뒀다.

## 상태(10-01)
반영: 구주호_AD·김만경_AD·최상호_AD(별+검은 초승달)·박기찬_AD·황준석_ADAP(마르코 날개·쿠잔 칼날·별·빅맘 구름 — **겹침 사장님 결정 대기**)·배성령_AD(세로 초승달)·박민석_ADAP(큰 베기 호)·신문철_AP·영원_이지원·김민준_AP(검 궤적, 가시 「이동|공격」 가정). 제외: 도플라밍고 실 리본 32(너무 가늚). 오라 17모델 교체 완료, 임시 유지 = 맵에 없는 7 + 안 켜지는 둘. 미해결: 황준석 겹침 · 시불 바위 단색(원작 바위 그림 없음) · 오라 리본 근사.
