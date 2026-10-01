# 다음 세션 이어받기 — 2026-10-01 저녁 (PM 작성, Opus 5.5 세션)

`CLAUDE.md` → `.claude/PROJECT_BRIEF.md` → `.claude/TEAM_RULES.md`를 먼저 읽고 이 문서로 온다.
10-01 오후판은 git 이력에 있다. 이 문서가 그것을 대체한다. **작업이 진행되면 PM이 이 문서를 갱신한다(큰 단계마다).**

---

## 0. 세션 구성 — 넷 다 가동
| 세션 | 맡는 것 |
|---|---|
| **PM** | 사장님 지시 수신·분배·리뷰·푸시 · 브금(GameBgm·yt-dlp) · 스킨/고유 동작 Assets 반영(모델 배선) · 배포 빌드 |
| **구현담당1** | 스킬 데이터·UnitAttacker·EnemyDummy · 밸런스 측정 도구(ClaudeCommands autoloop) |
| **구현담당2** | 초월 부가 이펙트·상시 오라(SphereArt*·flatten·SphereArtBuilder) · 고유 동작(적) · MotionShowProbe |
| **Blender** | fix_unit_fbx·원작 MDX 추출·조사 — Assets엔 안 씀, 산출 `~/GRD_motion_trial/`(원작 작업 스크래치 `_work/`) |

- 새 세션은 서로 기억이 없다 → **첫 지시에 배경과 에디터 규칙을 같이 준다.**
- 🔴 **유니티 에디터는 main 하나를 같이 쓴다.** 판(플레이) 중 Assets 쓰기 = 도메인 리로드 = 판 오염. 쓰기·refresh·배선 전 「씁니다/돌립니다」, 상대 「끝났습니다」 뒤에만. 10-01에도 두 번 어겨짐(저장 뒤 알림 · 패치 스크립트가 Assets까지 씀).
- 배포 빌드는 `../GuilRandomDefense-build`(별도 사본·배치 모드)라 main 에디터와 안 겹친다.

## 1. 사장님 확정 규칙 (누적)
- 🔴 **버전 0.x**(베타). 뒷자리 = 고치기, 가운데 = 큰 덩어리 완성. **0.3.1v 배포 완료(10-01 저녁).** 다음 0.3.2.
- **유료 에셋 절대 금지.** **전부 원작대로, 우리 창작은 조합식뿐.** 원작 근거 있는 정정은 묻지 않는다.
- **스킨 원본의 고유 동작·꾸밈은 살려도 좋다**(10-01 「각 스킨에 고유 스킨이나 모션 있으면 살려도 좋아」).
- **브금**: 1R binks · 2~59R 라운드 브금(보스 동안 멈췄다 이어서 = 사장님 「②」) · **60R 이후 깔리는 곡 없음**(보스 곡만) · 보스 라운드마다 사장님이 준 곡. 곡 원본은 `~/Desktop/구랜디스킨모음/80_사운드/`에 보관, 유튜브 링크는 yt-dlp로 받는다(가끔 403 → 재시도).
- **초월 부가 이펙트**: 원작 초월 모델의 날개·빛 등을 우리 초월에. 미대응 모델 배정은 PM 위임(마르코 날개·쿠잔 칼날 → 황준석, 검은 초승달 → 최상호_AD, 초승달 베기 → 배성령(세로판으로 세움), 큰 베기 호 → 박민석, 구름+모자 뺌). 원작 크기 그대로(최상호 별빛 「원작대로 둬」).
- 우솝 지팡이 = 손 · 황길라 동작판 base · 강재규 혀·눈 단색/그림.

## 2. 10-01까지 한 것 (전부 main 푸시)
- **0.3.1v 배포**: release/0.3.1(빌드 사본) → `~/Desktop/구랜디_베타/`(맥·윈도우 zip·앱·안내문, 0.3.0 삭제). 앱 바이너리 직접 실행 사진으로 0.3.1v 확인. 🔴 **사장님: 데스크톱 .app 더블클릭이 실행 안 됨** — 미해결(§4).
- **브금 10곡**(GameBgm Cues 표 — 위에서 첫 줄 우선, `resume` 플래그). 볼륨은 평균 dB로 ≈ −33.5dB 맞춤(어림).
- **고유 동작 46종**(유닛 30·적 16 Generic): Blender 전수조사(`~/GRD_motion_trial/고유동작_전수조사.md`) 1~4묶음. 🔴 FitToHeight가 Generic을 「가장 긴 축」으로 재서 무기·외투 큰 모델이 작아졌다 → AlreadyUpright면 키(Y)로(measureHeight).
- **초월 부가 이펙트 11곳**(effects.json → `Tools/sphere_art/flatten_effects.py` → `SphereArtBuilder` → extra.tsv): 구주호·김만경(소매=아래팔)·최상호_AD 별빛·박기찬·신문철·이지원·김민준_AP 검 궤적(가정)·최상호 검은 초승달·배성령·박민석·황준석(겹침 결정 대기). 뼈 따라가기·보이는 때 비트 마스크(대기·이동·공격·스킬)·리본(TrailRenderer)·PulseSkill(UnitAttacker 1초).
- **상시 오라 원작 모양 17모델**(구현담당2 e9b05e74): 등급 오라 원작 크기 일치. 임시 유지 = 맵에 없는 7 + 원작에서 안 켜지는 2. 근사 텍스처 바위는 단색.
- **측정 도구**: 흔함 위습 몰아주기 · 뽑기 전용 잎(악의근원·상붕카) 점수 +10·보유 시 0 · 그 잎 안 팔기(445e739e). 옛/새 R20 대조는 BALANCE §4.

## 3. 세션별 앞으로 할 일
### PM
1. 사장님 답 받기(§4) — 특히 앱 실행·황준석 겹침.
2. 다음 배포 0.3.2: 부가 이펙트 11곳·상시 오라·고유 동작 3·4묶음(이미 0.3.1에 들어감 — 확인)·도구는 무관. 절차: build 사본에서 `git checkout -B release/0.3.2 main`(사본에 남는 폴더 .meta 미추적 파일은 지우고) → GameVersion → `BuildBeta.Mac`/`Windows` 배치 → 앱 바이너리를 `-mpSolo -mpSaveDir … -mpSoloMenuAt 25 사진` → ditto zip → 안내문.
3. 멀티 두 창 확인(아직 안 함) · 윈도우 실행 확인(아직).
### 구현담당1
- 진행 중(19:40~): 새 도구 R20 → aim:전설 R60 1배. 결과 BALANCE 문서.
- 보류: 레이쥬(전설적인_임건웅) 피해 73% = 체력 비례 스킬 가설(BossDuelProbe 분해 필요).
### 구현담당2
- 부가 이펙트·상시 오라 파이프라인 = `Docs/research/SPHERE_ART_PIPELINE.md`(흐름 run_all.sh·run_aura.sh → BuildAll → build_table.py, 함정 8, 상태).
- 남은 것: 황준석 겹침 조정(사장님 답) · 도플라밍고 실 32(폭 5%라 제외 — 되살리기 한 줄) · 오라 맵에 없는 7 · NEXT_SESSION용 구역 정리 문서.
### Blender
- 진행 중: 워크3 기본 텍스처 원본이 이 컴퓨터에 있는지(읽기 전용) — 있으면 근사 텍스처 교체.
- 남은 쓸 만함: 히든_전유라(gen_biped_skin 판)·랜덤_손오공·특별함_조세민·적 R21/R33/R44 — 가치 작아 보류.

## 4. 사장님 답 대기
1. 🔴 **0.3.1 앱 더블클릭 실행 안 됨**: 바이너리 직접 실행은 정상. PM이 옛 LaunchServices 등록 8곳 정리(lsregister -u) + xattr -cr + adhoc 재서명. PM 셸에선 `open`이 멈춰 확인 불가 → 사장님 재시도/우클릭→열기 결과 대기. 아이콘(PlayerIcon.icns) 빌드에 없음(일반 아이콘).
2. **황준석 겹침**: ⓐ빅맘 구름 빼기 ⓑ+마르코 날개 0.6배 ⓒ다른 조합(사진 e3_h_f).
3. **워크3 정품 데이터 있나?** 근사 텍스처 39개(HandsAura2 Zap1_Red·Blue_Glow2·EQ_Rock2 등) 원본이 이 컴퓨터엔 없음(Blender 전수 탐색, `~/GRD_motion_trial/_wc3_textures/찾아본곳.md`). 정품 War3.mpq/리포지드 CASC 위치를 주시면 바로 뽑음. 커뮤니티 추출본은 출처 판단 필요.
4. 옛 대기(10-01 오후판 §4): 스킬 주인 질문 표 · 히든·다른세계 원작 대응 · 이펙트 메시 여덟 · 김건 털·박예원 자세 · 히루루크·변신·변화됨 · 브금/효과음 볼륨 · NC-ND 주영호(이번에 고유 동작 반영됨) · 밸런스 숫자.

## 5. 저장소·작업 폴더
- main 전부 푸시. 작업트리에 늘 남는 것(커밋 금지): Pretendard SDF 2 · Anton SDF · Materials/Map lane·rock mat · `.check_entries_out/`.
- worktree: `../GuilRandomDefense-mp`(mp = main과 같음, ff로 맞춤) · `../GuilRandomDefense-build`(release/0.3.1) · `../GuilRandomDefense-mp-build`(멀티 테스트 앱 — 옛 판).
- 저장소 밖: `~/GRD_motion_trial/`(고유_1~4묶음·초월_부가이펙트·초월_상시오라·_work 스크래치) · `~/Desktop/구랜디스킨모음/80_사운드/`(브금 원본·잘린 판) · `~/Desktop/구랜디_베타/`(배포판).

## 6. 굳은 규칙·함정 (누적)
- **새 컴포넌트가 Awake에서 UnitIdentity/OwnedByPlayer를 캐시하면 안 된다** — 소환 뒤에 붙는다(오라 설치기는 UnitIdentity.Active를 훑음).
- **스킬 에셋 문자열이 「[」로 시작하면 YAML이 깨진다** — yaml_scalar.
- 새 파일은 `git add -- 경로` 후 `git commit -- 경로`. zsh에선 경로를 배열로, 한글 경로는 `git -c core.quotepath=false … -z | xargs -0`. **폴더 .meta도 커밋**(10-01 새 스킨 폴더 아홉의 .meta가 빠졌었다).
- 원작 수치는 j·w3a·w3u 직접 디코드(Tools/w3x). 빈 필드 = 기반 스톡값. **능력 ID 주인은 uabi 소유 유닛 → 대응표로.** **「반영됨」은 블록 단위로 대조하기 전엔 믿지 않는다.**
- 스킨 반영 순서: refresh(컴파일) → `menu Tools/아트/모델 배선` → `ArtBinder.PendingLinkUnits`에 이번 유닛 이름 → `call ArtBinder.LinkTexturesUnits` → refresh → `units 파일 이름`(키 30·바닥 0·몸 위쪽). 커밋엔 Art/Units·Art/Materials·Prefabs/Generated·Data/Units·Data/Enemies.
- 🔴 고유 동작(Generic) 유닛: ① `UnitModelPostprocessor.GenericRigUnits`·`BlenderClipPostprocessor` Roots/MoveLoopRoots 등록 → refresh 둘 → 그 뒤 옛 fbx·meta 지우고 새 파일(순서가 바뀌면 Humanoid로 먼저 읽힘 — meta `animationType: 2` 확인) ② **`ArtBinder.AlreadyUprightModels`에 이름**(안 넣으면 AutoUpright가 망토·날개·무기 때문에 90° 눕힘 — 10-01 다섯 종).
- FBX 재질 함정: glb 「그림 없이 색만」 재질이 회색으로 죽음 · 거울 복사 메시 법선 · **EmissiveColor가 factor 없이 실려 몸 전체가 허옇게 빛남**(황길라 — zero_emission).
- 탐침: 표적 체력 ×1e6이면 작은 피해가 float 눈금에 묻힘 · 광역 한 방에 죽는 적은 gameshot spawn 판에 「맞은 0대」로 찍힘 · 판으로 말하기 전에 **도구가 그 행동을 실제로 할 수 있나**부터.
- 에디터 명령: inbox 파일 하나에 gameshot 한 줄 · 공백 든 버튼은 click:이 못 읽음(오브젝트 이름으로) · 스크립트 고친 뒤 refresh하고 dll이 새로워진 뒤 다음 명령.
- 🔴 GameBgm Cues는 **위에서 첫 줄**이 이긴다 — 보스 곡은 라운드 브금(넓은 구간)보다 위. 새 곡 = mp3를 Audio/Music/Resources/Music/에 + binks meta 복사(guid만 새로) + 표 한 줄 + `jump:` gameshot으로 Describe 확인. 보통 난이도는 60R가 끝이라 65+ 점프는 거부된다.
- 🔴 Generic 고유 동작 반영 = GenericRigUnits(적은 ArtBinder.GenericEnemyModels)·BlenderClipPostprocessor Roots/MoveLoopRoots(Move 없는 건 Roots만)·AlreadyUprightModels·PendingLinkUnits → refresh → 옛 fbx·meta 지우고 새 fbx+Textures rsync → refresh(meta animationType 2 확인) → 배선 → LinkTexturesUnits → units(키 30).
- yt-dlp는 `-x --audio-format mp3`. 31분 곡은 128kbps로(30MB).

