# 다음 세션 이어받기 — 2026-10-03 오후 마감 (PM 2번째, Opus 5.5 세션)

`CLAUDE.md` → `.claude/PROJECT_BRIEF.md` → `.claude/TEAM_RULES.md`를 먼저 읽고 이 문서로 온다.
🔴 **새 PM: 팀원은 이미 새 세션 — `--fresh` 돌리지 말 것**(아래 10-03 오후 판 §0).
10-01 오후판은 git 이력에 있다. 이 문서가 그것을 대체한다. **작업이 진행되면 PM이 이 문서를 갱신한다(큰 단계마다).**

---

## ⭐⭐⭐⭐⭐⭐⭐ 10-06 아침 (PM 새 세션, Opus 5.5) — 이게 최신
- 10-04 낮~10-05 기록이 이 문서에 없었다(그 PM이 갱신 안 함). git으로 복원: **0.3.6 = main에 병합됨**(rel036 → main, c4afbc489 버전) — 조합 검색 서랍 F5 · 스킨 별칭 표 · 아이템 아이콘 35 · 레인 사이 언덕 · 상점 쿨다운 덮개 · 공격 시 표적 바라보기 · 다른세계 「아무 유닛」 위습 모양 · 스토리 원작화(백수생활 위습 제거·특수지급 alwaysOpen·인원 비례 hp). **0.3.6 배포는 안 됨**(바탕화면엔 0.3.5v zip뿐).
- 10-05 구현담당1이 main 작업폴더에서 SkinAliasImporter.Apply·ItemIconLinker·RepairCombineBoard·RepairGachaRewardDisplays를 돌리고 커밋 안 한 것 → PM이 a3560ff28로 커밋·푸시.
- 기준선 g1_310~316 완료(01:05 멈춤), 결과 정리 미확인.
- 🔴 사장님 확인 대기 브랜치 셋(점검 페이지 Docs/ui_mockups/hidden_recipes·recipe_audit): **recipe-color-fix**(초월~다른세계 23식 색) · **hidden-audit**(히든 19식을 사장님 표 02_히든.png대로, 솔/성탄/뻬꼼 삭제) · **g1-combine-at-caster**(조합 결과를 시전 유닛 곁에 — 09-26 결정과 반대). g1-story-audit은 문서뿐.
- 커밋 안 한 채 남긴 것: Effects/Sphere 프리팹·재질 58개(10-03 12:48~13:28 손댄 것, 오라 2차 커밋 뒤 — 정체 미확인) · 폰트 SDF 아틀라스 3개(동적 아틀라스 흔들림).
- 팀원 셋은 10-06 아침 새로 떠 있음(started 1m) → `--fresh` 안 돌림.

## ⭐⭐⭐⭐⭐⭐ 10-04 낮 — 0.3.5v 윈도우 배포(11:48) — 이게 최신
- 사장님 결정으로 기준선 g1_300~309 중단(300만 완료 = 끔 R60 **클리어 확정**, 301 버림) → 개선 먼저.
- 0.3.5v(main 3d82830cd, 빌드 사본 release/0.3.5): F10 [화면](ScreenMode, 맥 앱으로 저장값 1280×720 복원 실측 ✅) · 상점 7채 새 모델(gen_shops_v2, RepairLaneShopModels) · 해적단→도박소 8번 칸 「해적단 ▶」(실측 구입 ✅) · 상점 8→7 · 사거리 원 A 때만(**미실측** — 도구에 선택·A 입력 없음) · 받침_조합 제거 · 뽑기섬 흔함 선택 줄 ×1.6(섬 1238) · 특수지급 이름표·잠김 글자 · 쵸파 프리팹·인형 절반(DollHeightMultiplier).
- `~/Desktop/구랜디_베타/GuRandi_Beta_Windows_0.3.5v.zip` + 안내문 갱신. 맥 0.3.5 앱은 빌드 사본 Builds/Mac에만(배포 안 함).
- 다음: 구현담당2 special-labels 브랜치(특수지급 이름표 ×5·위치 앞쪽) 병합 → RepairGachaRewardDisplays → 사진 · 기준선 재측정(g1_301~ caffeinate) · 사거리 원 실측.

## ⭐⭐⭐⭐⭐ 10-04 오전 (PM 3번째, Opus 5.5) — 이게 최신
- **0.3.4v 윈도우판 배포 완료(10-04 09:24, 사장님 지시 「윈도우버전 배포」)**: 빌드 사본 release/0.3.4 = main c465403cc · `~/Desktop/구랜디_베타/GuRandi_Beta_Windows_0.3.4v.zip`(567MB, 208항목·비ASCII 0·testzip OK·DoNotShip 제외) · 안내문 0.3.4v로 갱신(맥 칸 빼고 「맥판은 아직 0.3.3v, 같이 하기는 같은 버전만」). 빌드 DLL에 0.3.4·새 코드 문자열 확인. 맥판은 안 만듦(옛 0.3.3 zip·app과 윈도우 0.3.3 zip은 그대로 둠 — 지울지 사장님께).
- 병합: 구현담당2 wisp-navagent(WispIconBaker 경고 제거 — 실측 0건 · 메타몽 도박 6칸 가득 버그 · 「시야가 좁네」 채팅 삼킴 · 촬영 탐침 DisplayProbe.WispKinds/ItemsFull) · 구현담당1 g1-pointerband(autoloop 포탈 우클릭 띠 판정 안전판 + 진단 로그, 7ccef6951).
- 박도진(구부정)·손오공(Attack 2배속) 사장님 확정(이미 반영본).
- 🔴 **기준선 g1_290~299는 실패**: 쓸 판 290(끔 R48 패배)·291(켬 R25 패배, 포탈 건너뜀 94건이라 신뢰 낮음)뿐. 292·293 = autoloop 포탈 우클릭이 HUD 띠 판정으로 전부 건너뜀(R2 사망) · 294~298 = **맥 잠자기**로 프레임 수백만 ms·기록 끊김. → 재측정 g1_300~309를 `caffeinate -dims` 아래로(구현담당1, 10-04 오전 시작).

## ⭐⭐⭐⭐ 10-03 오후 판 (PM 2번째, Opus 5.5 — 마감) — 이게 최신. 아래 「10-03 판」은 오전 기록
전부 main 푸시. **0.3.4 배포는 여전히 「나중에」**(사장님).

### 0) 새 PM이 맨 먼저 할 것
- 🔴 **팀원 셋(구현담당1·구현담당2·blender)도 이 마감과 같이 새 세션으로 갈아 끼웠다**(spawn_team.sh --fresh) → 새 PM은 `--fresh`를 **돌리지 말 것**. ListAgents로 셋이 떠 있는지만 보고, 각자 「준비 완료」를 받은 뒤 첫 지시.
- PM 교체 규칙 새로 생김(사장님 10-03): PM 대화가 길어지면 **PM이 스스로** NEXT_SESSION 갱신·푸시 → `.claude/team/spawn_pm.sh`(새 pm 탭 · Opus 5.5 · 20초 뒤 옛 pm 탭 닫힘). CLAUDE.md 참고.
- 첫 일: **오늘 밤 기준선 10판**(구현담당1, g1_290~299) — 오늘 HEAD(유닛 1.6배·흙길·조합판 간격·카메라 줌)로. 방법은 handoff_구현담당1.md. autoloop 위습 칸 찾기를 오늘 고쳤으니 짧은 판(rounds:3)으로 위습→포탈 먼저 확인.

### 1) 오늘 오후 반영 (전부 main)
- **UI**: 미니맵 옆 둥근 단추 5개 삭제 · 초상 상반신(PortraitStage 3차 보정, 비인간형은 전신) · **둥근 모서리 UI**(UiSkin.RoundFill/Ring, 반지름 8~10px — 더 둥글게는 UiSkin.DefaultRadius) · **위습 칸 = 위습 3D 아이콘**(WispIconBaker, 이름은 툴팁 · 원작도 위습 7종이 BTNWisp 한 장이라 종류 구분은 등급색) · 영웅 단추 겹침(채팅 x 76).
- **초월 분홍 오라**: 원작 사진 오라 = 우리 HandsAura2(A07O). 번개로 보였던 건 근사 텍스처 오류 + 파이프라인이 층 첫 장만 읽어 Zap1_Red가 빠졌던 것. 2차 근사 텍스처(blender gen_aura_approx_tex.py) · 크기 1.0 · 세기 Purple 1.0/Zap 0.7 · Yellow 층 drop — 값은 `Tools/sphere_art/effects_overrides.json`(flatten 재실행해도 안 지워짐). 사장님 「저렇게 가는 걸로」 확정.
- **유닛·적 키 1.6배**(ArtBinder.UnitHeight 30→48 · EnemyHeight 22.5→36, 사장님 「원랜디보다 작다」 — 화면상 유닛/레인 1/50 vs 원작 1/23). 스토리 건물도 같이 1.6배(아직 실판에서 눈으로 못 봄 — 스토리 들어갈 때 확인).
- **적 흙길 넓힘**: 보이는 띠만 레인 가로×0.09(≈70), 남쪽은 흔함 칸 때문에 안쪽으로. 적은 그대로 가운데 선 → 밸런스 무관. `call MapGenerator.RepairLaneTracks`.
- **조합판·뽑기섬 간격 1.6배**(구현담당1 RepairCombineBoard/DryRun, BoardScale = UnitHeight÷30): 조합판 2246×2092, 뽑기섬 726×1201, 전시 섬 동쪽으로. 인형 681/681.
- **카메라**: FOV 30 구도는 사장님 반려(「위에서 본 납작한 시점」) → 원래 50°·60° 유지 + **시작 줌 0.85**(섬+우리 다 담은 구도에서 화면 가운데 쪽으로 15% 당김, startZoom은 NonSerialized) + 채팅 **「시야 N」**(100=0.65·150=0.75·200=0.85, 50~300 직선, 이 PC PlayerPrefs). 비교 사진 Docs/ui_mockups/compare/09·10.
- **바다색** 밝은 청록(SeaWater.mat 얕은 0.25/0.82/0.80 · 깊은 0.04/0.42/0.58 · 깊이 14). 사장님 「심해 느낌」 — 밝기는 원작보다 어둡지 않았고 바다 색상 문제였다.
- **고유 동작 5묶음 여섯**: 조세민·손오공(카메하메 2배속 공격)·전유라(23클립)·박도진(구부정)·서희원·강민호(서서 꼬리 든 원본 자세) — 사장님 「일단 넣어봐」. 박도진(구부정)·손오공(Attack 2배속)도 사장님 10-03 「반영해」로 확정(지금 들어간 그대로).
- **박민수 정면 보정**(ArtBinder.FacingFixes −90, 프리팹 직접) · 정면 점검 FacingAudit(328개, 고칠 건 박민수뿐).

### 2) 사장님 답 대기 / 확인할 것
- 작은 일(구현담당2 몫): WispIconBaker.Bake가 위습 복제본의 NavMeshAgent를 NavMesh 밖에서 만들어 「Failed to create agent」 경고 1건 — 복제본 NavMeshAgent 끄기.
- 초월 전시 섬 25종이 4줄에 안 들어가 1명(황준석 아오키지)이 바다 위에 서 있던 결함 → TranscendSizeX 495로 고침(구현담당1).
- 시야 채팅 실제 입력 확인(코드만 컴파일 통과, 채팅창으로는 안 쳐 봄).
- 스토리 건물 1.6배가 구역을 가리지 않는지(실판).
- 0.3.4 배포 시점 · 정의문 체력 · 3대장 반격 · 랜덤전용 one_dill · War3.mpq · 어려움 완화 · 볼륨(오전 판 §2 그대로).

### 3) 함정 (오늘 새로)
- 🔴 **「모델 배선」 메뉴는 돌릴 때마다 Generated·Data·씬 fileID를 통째로 뒤섞는다**(내용 같아도 diff 700개) — 한 유닛만 고칠 땐 그 프리팹만 직접.
- 🔴 브리지 outbox 이름이 옛 파일과 겹치면 옛 결과를 읽는다(오늘 pm_w1 — 10-02 파일) → 날짜 접두(pm1003_).
- [SerializeField] 기본값을 바꿔도 씬에 이미 박힌 값이 이긴다(startZoom 0.8 남음) → 코드가 정해야 하는 값은 NonSerialized.
- 맵 Repair는 「섬 직계 자식」만 지운다 → 다른 묶음 밑 인형이 옛 자리에 남는다(초월 아오키지가 바다 위에 남음 — 구현담당1이 정리).
- gameshot에서 클릭/콜은 spawn보다 먼저 돈다 → 유닛이 필요한 촬영은 `call:ClaudeCommands.SkillProbeSpawn`.
- 에디터 없이 컴파일 검사: `Tools/ui/csc_check.sh`(런타임) · `Tools/ui/csc_check_editor.sh`(Editor).

## ⭐⭐⭐ 10-03 판 (PM, Opus 5.5 — 12:30 마감) — 이게 최신. 아래 10-02 판은 그 전 기록
전부 main 푸시. **0.3.4 배포는 사장님이 「나중에」** — 0.3.3v 이후 변경이 많고 [Networked]도 여러 번 바뀜 → 호스트·클라 같은 빌드 필수.

### 0) 새 PM이 맨 먼저 할 것
- 🔴 **팀원 셋은 12시쯤 새 세션으로 이미 갈아 끼웠다**(구현담당1·구현담당2·blender, 셋 다 「준비 완료」 받음, 일하는 중).
  → `spawn_team.sh --fresh`를 **돌리지 말 것**(일하는 세션이 닫힌다). ListAgents로 셋이 떠 있는지만 보고 그대로 이어간다.
  팀원이 일하는 중이면 그 일이 끝난 뒤에만 갈아 끼운다(CLAUDE.md 「대화가 길어지면 갈아 끼운다」).
- 새 PM 세션 이름은 **pm**이어야 팀원 보고가 닿는다(사장님이 `/rename pm`). 팀원 지시문은 「pm 또는 PM」을 찾는다.
- 지금 진행 순서(에디터는 하나): ① 구현담당2 UI 남은 단계 → 「끝났습니다」 ② 구현담당1 카메라 구도 적용 → 비교 사진 ③ **오늘 밤 새 기준선 10판**(구현담당1, PM이 「기준선 시작」 신호).
  각 팀원의 남은 일은 `.claude/team/handoff_<역할>.md`에 있다.

### 1) 오늘 반영 (전부 main)
- **세션 운영**: `.claude/team/spawn_team.sh [--fresh] [역할]`(Orca 탭·Sonnet 5.5·bypass) · 역할 지시문은 handoff 파일을 먼저 읽음 · 대화가 길어지면 사장님께 말하고 갈아 끼우기(CLAUDE.md).
- 상점 단축키 실측 OK(gameshot `key:` 인자). **A 공격 대기 사거리 원**(주황·굵게·물결) + 선택 사거리 원이 원래 한 번도 안 그려지던 결함 수정.
- **모리아 A113 = 100%·10초**(Nba3 = 소환 지속시간). **Dark Young** 대기 4.12초 + 밝기.
- **정의문 보상 사슬**(문 HP 원작 487.7M → 생존자 골드 2만·흔함선택 2·세이브 3·XP 600 → 3대장 균등 1기 고정 표적 → 처치 특성+1·세이브+1·팀 버프 셋 다 실측). 3대장 스킨 = R15/R11/R12 프리팹, ArtBinder.EnemyModels에 Enemy_Dog_* 등록.
- 표시류: 스토리 막타 청록 +N · 클리어 칭호 13단계.
- **세이브 코드** `GRD1-…`(닉네임 열쇠, SaveCode.cs = 서버 DB로 바꿀 자리) · 시작 화면 불러오기 · 채팅 `-load`(1R 전). 시작 화면 패널은 아직 눈으로 못 봄.
- **풀카운트**(신세계 점수판 줄, 실측 6880 일치) · **신세계 진입 보상 e015**(전설·히든 위습, 사용 시 −625×배수).
- **UI 원랜디화**(사진 `Docs/reference/ui/` 셋, 설계 `Docs/UI_ORIGINAL_STYLE.md`, 비교 `Docs/ui_mockups/compare/`): 점수판 표·상단 바(구랜디)·타이머 창 스택·콘솔 돌벽·체력/마나 바·정보칸·아이템 6칸·**게임 안 난이도 대화상자(방장만, 6버튼, 시간 제한 없음)**. 원작 그림은 근사(UiSkin 슬롯) — 정품 War3 데이터 없음.
  사장님 결정: 원작 그림 써도 됨(친구끼리) → `Docs/REPLACE_BEFORE_PUBLIC.md`에 기록 · 골드·목재는 상단 바 · 아이템 6칸 · 마나 칸·항법 버튼 유지 · 사거리/공속 빼기 · 「열림」 빈칸 · 영웅 단추 대상 확대 안 함(원작 2개).
- 카메라 구도(원랜디 사진 = 섬 통째, 원근 적게): 계산 끝, 브랜치 `camera-original`(FOV 30·피치 56·높이 ~850). 구현담당1이 UI 뒤 적용.
- 기준선: 오전 체인은 사장님 결정(「UI 먼저」)으로 g1_290 R30에서 중단 — 결과 0판. 밤에 UI·카메라 들어간 HEAD로 새로.

### 2) 사장님 답 대기 / 다음 후보
- 0.3.4 배포 시점(「나중에」). 배포 전 확인: 멀티 두 창(풀카운트·세이브 코드·난이도 대화상자 복제), 시작 화면 세이브 코드 패널, 방 패널.
- 정의문 체력 원작값(실전에서 못 부술 수 있음) — 체감 대기. 3대장 반격 여부(원작 근거 없음, 지금 고정 표적).
- 랜덤전용 [조합] one_dill +8 대응 미정.
- 옛 대기: War3.mpq 위치(UI·이펙트 정품 그림) · 어려움 완화 · 볼륨 · 라이선스.

### 3) 함정 (오늘 새로)
- gameshot: 클릭이 spawn보다 먼저 돈다(spawn 유닛은 select: 불가 → `boxselect:Unit_`) · 단계 상태 바꾸면 `SaveGameShot(job)` 필수 · spawn 직후엔 UnitIdentity.Active에 아직 없을 수 있다(wait) · `mode:`는 도구 신호로 난이도 대화상자를 건너뛰고 `askmode`는 대화상자를 그대로 본다.
- 모델 배선은 Generated를 통째로 다시 만든다 — EnemyModels 표에 없는 에셋은 자리표시로 떨어진다.
- `Tools/ui/csc_check.sh` = 에디터 없이 컴파일 검사(판 도는 중에도 안전). worktree에서 준비 → 판 끝나고 합치기가 오늘 잘 먹혔다.
- Canvas 자식은 부모 정렬을 상속(가림막을 루트로).
- 측정 체인 진행은 outbox가 판 끝에만 써진다 — 돌고 있는지는 `ClaudeBridge/shots/round_*.png` 시각으로 본다.

## ⭐⭐ 10-02 오후 판 (PM, 재시동 뒤 세션) — 아래 「마무리 판」보다 이게 최신
스토리 13채 전부 반영 완료(마지막 = 스토리11 바리온). 전부 main 푸시. **0.3.4 배포는 아직 안 함** — 아래 전부가 0.3.3v에 없다. NetCatalog 지문이 두 번 바뀜(압살롬·좀비) → 호스트·클라 같은 빌드 필수.

### 1) 오늘 오후 반영
- **정의문**: 원작 doo 디코드(ZTsg (3904,384) 180° 배율1.5, dog_zone 안 단독 — 섬 가르는 담·문기둥은 우리 창작이라 삭제). 사장님 지시로 **45° 대각선 · 3배**, 모델 = **에니에스로비 정의의 문**(5_doors, Cyrone CC-BY) → 상자 비율 20.6×27.65×2.13 ×3. `call MapGenerator.RepairJusticeGate`(맵 재생성 없이 문만 다시 세우고 NavMesh 굽고 저장).
- **압살롬**(원작 h010 특별함, 새 유닛, 스킨 potk-megumin, 지팡이 1.8m 그대로) + **압살롬 도박**(h069: 500엔+목재1 · 성공 45% · R20 보스 처치 해금 · 도박소 7번 칸, AbsalomProbe로 판 확인) · 중급도박 특별함 풀에 포함(원작 Random3).
- **좀비**(원작 h00H 안흔함, 새 유닛, 스킨 potk-emilia, 가챠 풀 밖) + **A029 조합**: 좀비×3 + 흔함_강재규(원작 나미) = 압살롬.
- **모리아**: 사장님 1번안 — **특별함_임채준(모리아 스킨) = 원작 h00B 자리**(대응표 h015→h00B, 버기 자리 비움). A113: 평타로 죽인 적(PV<200) → 내 좀비. 🔴 Nba3=10.0 뜻 미확정 — 지금 「확률 10%·영구」로 들어감. PM 판단은 「100%·10초 지속」(스톡 ANba Data C=소환 지속) 쪽이 유력, **사장님 답 대기**(에셋 값 raiseOnKillChancePercent/LifetimeSeconds만 바꾸면 됨, Docs/reference/MORIA_SLOT_2026-10-02.md).
- **특수지급 칸 전시**(구현담당2): 돈+목재 자리 금화·목재 소품, 박은석 초월위습·레일리(희귀함_이승우)+배(상붕카) 인형 · 조합표 비용 아이콘도 금화·목재 모델(Assets/Art/Props, blender gen_props_coin_wood.py) · `RepairGachaRewardDisplays`.
- **상점 칸 단축키 + 범위 원**: 워크3 격자 Q W E R/A S D F/Z X C V(칸에 글자), LaneShopSlotView.hotkey로 지정 가능(항해일지 탐색 = **Q**) · 지점 칸을 찍는 동안 커서에 TargetAreaIndicator(탐색 반경·도움소 지점 스킬 반경) · 상점 건물 선택 중엔 A·S·H·V·C·M 유닛 키 무시, 대상 클릭이 건물 선택을 안 푼다. ⚠️ **실제 판에서 키 입력·원 표시는 아직 눈으로 못 봄** — 다음 판에서 확인.
- **스토리 건물 13채 스킨 전면 교체**(원본 `~/Desktop/구랜디스킨모음/92_스토리스킨/` + README에 출처·라이선스표, 스크립트 Tools/blender/gen_story*.py):
  1 옥문강 · 2 아롱파크 · 3 에니에스로비 탑+아치 · 4 이치라쿠 라멘(히든_호치킨과 같은 원본) · 5 스코퍼 가반 · 6 공중전화 부스(유리 `_잎카드` 알파 컷) · 7 도로헤도로 En 피규어 · 8 Dark Young(정적 1프레임, 원본 4.1초 대기 클립 있음) · 9 뚱뚱한 거인(팔 70° 내림) · 10 DIO 머리 전시(받침+돔 그대로) · 11 나루토 바리온(점프 자세 피규어 — 뼈 없어 자세 못 바꿈, 정점색→리메시+굽기) · 12 복마어주자 · 13 나뭇잎 마을 호카게 광장 구역.
  키는 ArtBinder.EnemyModels 표(미터 = blender 게임단위 ÷ 11.4). 「_화남」 판은 코드에서 안 쓰여 그대로 둠.

### 2) 사장님 답 대기
- 모리아 좀비: 「10%·영구」 vs 「100%·10초」.
- 히든 재료(carrot·perona·Bugi)의 h010(압살롬) 지명 — 09-06 번역 때 다른 유닛에 배정돼 있음, 보류.
- 정의문 뒤 보상 사슬(문 파괴 → 2만·흔함선택2·세이브3·XP600 → 3제독 소환 → 특성·버프) 아직 없음 — 넣을지.
- 스토리8 Dark Young을 대기 동작으로 움직이게 할지 · 몸이 거의 검정이라 밝힐지.
- 0.3.4 배포 시점.
- 옛 대기(아래 마무리 판 §3): 카메라 시작 줌 · War3.mpq 위치 · 난이도 완화 · 볼륨.

### 3) 라이선스 주의(공개·유료 배포 전 정리)
- **CC-BY-NC(비상업)**: 이치라쿠 라멘(Doverlock) — 히든_호치킨 + 스토리4.
- **게임 추출·권리 불명**: 스토리5 가반(OPDS)·스토리9 거인·스토리10 DIO, 압살롬·좀비(potk)·스토리6·8·13(출처 미확인).
- CC-BY(작성자 표기): 아롱파크 aryan_yadav · 에니에스로비 Cyrone™ · 도로헤도로 HERMIT_10G · 옥문강·복마어주자 NexusB · 바리온 MontanariArt.

### 4) 함정 (오늘 새로)
- 🔴 **씬 커밋에 URP 조명 데이터(UniversalAdditionalLightData, guid 474bcb49853aa…) 13개가 계속 섞인다** — 작업 파일에서 HEAD에 없던 그 블록과 component 줄을 빼고 `git hash-object -w` → `git update-index --cacheinfo`로 스테이징, **경로 없이** `git commit`(경로를 주면 작업 파일이 그대로 올라간다 — 한 번 당함). amend 전엔 `git diff --cached --name-only`로 남이 스테이징해 둔 것(구현담당1의 삭제 2건이 딸려 들어갔었다)부터 확인.
- 🔴 **미컴파일 소스가 작업트리에 있으면 남의 gameshot 판이 시작 때 리로드돼 무효**(모리아 탐침 3판 날아감). 소스 고친 세션은 바로 refresh까지 하거나, 판 도는 동안 소스 편집 금지.
- 브리지 inbox 파일 이름을 남과 겹치게 쓰면 옛 outbox를 읽는다(g9 사건) — PM은 `pm_` 접두.
- 건물 재질 알파 컷 = 재질 이름 끝 `_잎카드`(NatureMaterialPostprocessor, Buildings 폴더 포함). 반투명 규칙은 없다.
- `lineup <파일> Assets/Prefabs/Generated <번호> 1` — Mob_Story01~13이 정렬 0~12번.

## ⭐ 10-02 마무리 판 (PM) — 이게 아래 §3·§4 중 낡은 부분을 덮는다
**최종 기능 HEAD = a1dbdbeb7** (이후는 문서 커밋뿐). 에디터에 판 없음·체인 없음. 푸시 완료.

### 0) 맨 먼저 할 것
1. 사장님께 확인: 데스크톱 `GuRandi 0.3.3v.app` 더블클릭 실행(재시동 뒤) · **첫 곡·보스 곡 재생**(첫 곡 한 번만, 보스 곡은 끝까지 한 번 뒤 라운드 브금 이어짐) · 카메라 시작 줌(휠로 맞춘 뒤 F1 「카메라 높이」 숫자).
2. **배포 0.3.4 필요**: 오늘 변경 전부(브금·초월 위습·마나 표시·휠 줌·원작 갭 25건·아이템 복제/사용)가 0.3.3v에 없다. ⚠️ **호스트·클라 빌드 동일 필수**(NetCatalog 지문 + [Networked] HeldItems·DeathLimit·HeroLevel·TransformUsesLeft + RPC_UseItem·RPC_KillGold) — 친구에게는 같은 버전 빌드만. 배포 절차는 §3 PM 2번.
3. 기준선 재개(아래 §밸런스).

### 1) 오늘 반영한 것 (자세히: Docs/CHANGES_2026-10-02.md — 항목·원작 근거·체감·안 한 것)
- 사장님 요청: 브금(GameBgm Once/Hold) · 초월 위습 라우팅 7 · 상단 바 마나 · 휠 줌 비율 12%/칸 · 한나웅 조합법(이미 표에 있음, 히든 열).
- 원작 갭(GAP 09-27) 25건: 처치 골드 +N · 점수판 한계 제목 · 모으기 · 신세계 보스 0.85(R65+) · 스토리 진입 거부 · 발판 월드 글자 13 · 변화 플레이어당 2회 · 영웅 Lv · 자원 포탈 한글 · 클리어 보너스 신세계만 · 패배 정리(위습·목재·특성) · 스토리 보상 생존자만 · 41R 2차 세이브 보상 · 크립 2단계 50/50 · 크립 주인 전용 · 스토리 딜 기여도 · 스토리 조기 클리어 · 보스·스토리 아이템 드랍 · 패왕 목재 · 능력치 증가 재고식 · 대지진 Lv2 · 판매 목재 · 아이템도박 재고(스토리 6·9) · 패스트 유니크 · 고급도박 R15 · 500엔 도박 주인별 · 퇴치 퀘스트 minRound(와포루 21·바제스 31·모리아 41·피카 51, **매핑 추정**, 커밋 5e3180f8e·90b48463f 단독 revert 가능) · I00Z 수배서 골드 +10%.
- **멀티 결함 수정**: 친구 화면 아이템 칸이 항상 비어 있던 것(복제 신설) + 아이템 사용 경로(좌클릭 즉시, I011 위습꾸러미·I00S 고대의 배; I003 흔적-슈스이는 사용 불가 표기).
- 도구: 목재 장부 줄번호 · bosschase 대기 60초 · `support`·`pirate` 옵션 · `touch ClaudeBridge/STOP` 중단 신호.

### 2) 밸런스 현황 (BALANCE_2026-09-30.md §5~§9)
- 새 경제 전 R60 2/2 통과 → 이전 HEAD 후 1/4(도구 문제 1 포함) → 같은 HEAD 후 3/6 — **95% 구간이 12~88%라 셋 구분 불가**, 경제 변경이 통과율을 떨어뜨렸다는 증거 없음. 패배는 R45~54 중반 레인 쌓임(전설 확보 속도 편차).
- **최종 HEAD(a1dbdbeb7) 새 기준선은 2판뿐**: g1_280(끈) R60 통과 · g1_281(켠 support pirate) R60 통과. **이어서**: 남은 g1_282~289(끈 282·284·286·288·289 / 켠 283·285·287)로 총 10판. 판당 `gameshot b<번호> 75 1600x900 rounds:60 autoloop keeppen sell aim:전설 bosschase mode:보통`(켠 판 뒤에 ` support pirate`)를 `ClaudeBridge/inbox/g1_<번호>.txt`에. 체인은 `nohup … & disown`(맥엔 setsid 없음). 판 중 Assets 쓰기·refresh 금지.
- 어려움 R30 벽(보스 ×3.0 = 원작 사양값, 약 7% 모자람) — 원작값이라 안 낮춤, **난이도 완화 여부는 사장님 결정**. R65+ 측정은 R60 이후 상태로 점프하는 디버그 명령이 가장 싸다(필요할 때만).
- 측정 도구는 크립을 안 잡고 도움소·해적단상점도 기본으론 안 감(켠 옵션 별도).

### 3) 사장님 답 대기
정의문 크기·방향(원작은 아레나 정중앙 단일 오브젝트 배율 1.5, 모델 치수 못 구함 — 지금 정의문을 사진 보고 지시) · 카메라 시작 줌 숫자 · **압살롬 도박**(원작 45% 특별함 h010 1기 — 로스터 특별함 중 누구?) · **워크3 정품 데이터(War3.mpq/CASC) 위치**(근사 텍스처 39개·정의문 모델) · 난이도 완화 여부 · 볼륨(브금 10곡·효과음 어림값).

### 4) 남은 일 (CHANGES 5장 표)
- 승인·반영 완료, 확인만: 41R A022(제한_강보명 R40 이후 막힘) 탐침 · 멀티 두 창에서 딜 기여도·크립 주인.
- 보류: 압살롬 도박 · 하늘섬 퀘스트(지형·표적·비행 접근 + 크립섬 도달 수단 선행) · I003 영웅 변신·아이템 보유 효과 중 영웅 스킬 연동분 · 크립 1단계 피해 제한(원작 미확정) · 막타 청록 「+1」 글자.
- 후순위(표시류): 마나 게이지·클리어 칭호·스킬 피해 숫자·카메라 진동·풀카운트·협동건물 업적판·시간차 스킬/소환물.

### 5) 함정 메모 (이번에 새로)
- **파괴된 Unity 참조는 `== null`이 참**(스토리 딜 기여도가 측정에서 한 번도 안 지급) — `ReferenceEquals` + 탐침은 실제 호출 시점 상태로, 반영 직후 실제 판 로그에 새 줄이 찍히는지 확인(memory destroyed-unity-ref-is-null).
- 원작 문구는 wts 원문(색 태그·줄바꿈·시간)으로. w3u ureq까지 보고 「툴팁 거짓」 결론 내지 말 것(와포루 minRound 1은 틀렸고 툴팁 21~30이 맞았다).
- 에디터는 한 번에 한 세션만: 판(측정) 중엔 Assets 쓰기·refresh·CPU 무거운 작업(빌드) 금지, PM이 창을 열고 닫는다. 씬의 URP 라이트 변화는 커밋 금지.
- 씬 필드가 필요한 변경은 전체 맵 재생성(diff 80만 줄) 대신 Repair 함수만 불러 저장.

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
### 🔴 재시동 직후 (PM 먼저)
- **맥 재시동 이유**: 10-01 밤 이 맥의 앱 실행 경로(LaunchServices/runningboard)가 막혀 **계산기도 안 열렸다**(가동 11일). 앱·빌드 문제 아님 — 바이너리 직접 실행은 0.3.1·0.3.3 둘 다 사진 확인. 사용자 lsd 재시작으론 안 풀림. 재시동 뒤 `~/Desktop/구랜디_베타/GuRandi 0.3.3v.app` 더블클릭이 되는지 사장님께 확인. 🔴 실행 확인을 `pgrep -f "<경로>"`로 하지 말 것 — 그 문자열이 든 내 셸 명령줄을 잡아 「떴다」고 오보했다. `ps -axo pid,comm`에서 실행 파일 경로로.
- **main에 아직 안 들어간 두 커밋**(release/0.3.3 브랜치에만): 44b2ba80b(BuildBeta 이름 영문 GuRandi)·6c3854113(GameVersion 0.3.3) → `git cherry-pick` 해서 main에 넣고 푸시(에디터에 판이 없을 때 — Editor 스크립트라 리로드).
- 세션 넷 다시 띄우고(ListAgents) 첫 지시에 배경·에디터 규칙.

### PM
1. **0.3.3v 배포 완료(10-01 23:40)**: `~/Desktop/구랜디_베타/` = GuRandi_Beta_Mac/Windows_0.3.3v.zip · GuRandi 0.3.3v.app · 안내문. 🔴 배포 zip은 `zip -r -y -X`(Info-ZIP), BurstDebugInformation_DoNotShip 제외, 이름 영문만, 뒤에 python zipfile로 비ASCII 0·testzip 검사 — 맥 ditto zip은 한글 이름에 UTF-8 표시를 안 넣어 윈도우에서 「500MB인데 빈 폴더」가 됐다(친구 메일 0.3.1).
2. 다음 배포 0.3.4: build 사본에서 `git checkout -B release/0.3.4 main`(사본에 생기는 미추적 폴더 .meta는 지움) → GameVersion → `BuildBeta.Mac`/`Windows` 배치 → 앱 바이너리를 `-mpSolo -mpSaveDir … -mpSoloMenuAt 25 사진`으로 확인 → zip(위 규칙) → 안내문.
3. 아직 안 한 확인: 멀티 두 창(스킬 효과음·이펙트·획득 음성·브금·오라가 친구 화면에도) · 윈도우 실제 실행(맥이라 못 봄 — 친구에게 부탁) · 65·70·75R 보스 곡(보통은 60R가 끝이라 에디터 점프 불가 — 어려움 이상으로).
4. 앱 아이콘: PlayerIcon.icns가 빌드에 없음(일반 아이콘). 사장님이 원하면 아이콘 그림.
### 구현담당1
- 진행 중이던 것(재시동으로 끊김): **aim:전설 R60 1배**(23:36 시작). 새 도구(d14d13a6d — 뽑기 잎 적은 식으로 일찍 갈아타기 · 막힘 원인 갈래 로그)로 다시 돌릴 것 → BALANCE 문서.
- 직전 R20(g1_222): 통과, 골드 4125, 뽑기 잎 6라운드 낭비는 사라짐. 남은 병목 = 흔함 선택 위습 공급(게임 규칙이라 도구가 못 늘림).
- 보류: 레이쥬(전설적인_임건웅) 피해 73% = 체력 비례 스킬 가설(BossDuelProbe 분해 필요).
### 구현담당2
- 파이프라인 문서 = `Docs/research/SPHERE_ART_PIPELINE.md`(run_all.sh·run_aura.sh → BuildAll → build_table.py, 함정 8).
- 끝남: 초월 부가 이펙트 11곳(황준석 ⓐ 적용 c45af5b61) · 상시 오라 원작 모양 17 · 등급 오라 원작 크기 일치.
- 남은 것: 도플라밍고 실 32(폭 5%라 제외 — 되살리기 한 줄) · 오라 「맵에 없는 7」 임시 그대로 · 시불(Ora_siki) 바위는 근사 텍스처가 없어 단색 · 오라 리본 근사.
### Blender
- 끝남: 고유 동작 1~4묶음(유닛 30·적 16) · 부가 이펙트·상시 오라 effects.json 34폴더 · 워크3 기본 텍스처 탐색(이 컴퓨터에 없음).
- 남은 쓸 만함: 히든_전유라(gen_biped_skin 판)·랜덤_손오공·특별함_조세민·적 R21/R33/R44 — 가치 작아 보류. 애매 18은 Idle만이라 안 씀.

## 4. 사장님 답 대기
1. 재시동 뒤 앱 더블클릭 되는지.
2. **워크3 정품 데이터 있나?** 근사 텍스처 39개(HandsAura2 Zap1_Red·Blue_Glow2·EQ_Rock2 등) 원본이 이 컴퓨터엔 없음(`~/GRD_motion_trial/_wc3_textures/찾아본곳.md`). 정품 War3.mpq/리포지드 CASC 위치를 주시면 바로 뽑음.
3. 볼륨(브금 10곡·효과음) — 평균 dB로만 맞춘 어림값, PM은 소리를 못 들음.
4. 옛 대기(10-01 오후판 §4): 스킬 주인 질문 표 · 히든·다른세계 원작 대응 · 이펙트 메시 여덟 · 김건 털·박예원 자세 · 히루루크·변신·변화됨 · NC-ND 주영호(고유 동작 반영됨) · 밸런스 숫자.

## 5. 저장소·작업 폴더
- main 전부 푸시. 작업트리에 늘 남는 것(커밋 금지): Pretendard SDF 2 · Anton SDF · Materials/Map lane·rock mat · `.check_entries_out/`.
- worktree: `../GuilRandomDefense-mp`(mp = main과 같음, ff로 맞춤) · `../GuilRandomDefense-build`(release/0.3.3) · `../GuilRandomDefense-mp-build`(멀티 테스트 앱 — 옛 판).
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

