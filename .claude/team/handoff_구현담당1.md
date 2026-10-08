# 구현담당1 인수인계 (10-08 밤 마감, 0.3.16 배포 직전)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md` → TEAM_RULES 순으로 읽는다. 전부 main에 커밋됨(푸시는 PM). 에디터는 비웠고, 빌드 사본(`../GuilRandomDefense-build`)은 main과 같은 clean 체크아웃(분리 HEAD)이다 — PM이 0.3.16 빌드에 쓴다.

## 오늘(10-08 오후~밤) 끝낸 일 (해시는 git log)
- **초월 5기 신 보스 피해 재측정**(9281d8157, `Assets/Editor/TranscendBossProbe.cs`): 표적 체력 1e9를 매 프레임 채우고 피해를 double로 누적. 60초 초당 피해 박민석 39.2만·엄태웅 14.1만·박민수 5.4만·이재윤 2.2만·최상호AP 0.9만 — 고정 깡딜 유닛은 신 보스(179.8M)에 사실상 무의미(사장님 판단 거리).
- **유닛 6종 prefab 참조 수리**(0e9823a51): 히든 솔·성탄·뻬꼼·특별함 조도연·안흔함 이호준·초월 강재규AP의 `UnitData.prefab` fileID가 재생성된 프리팹 루트와 안 맞아 **클린 빌드에서만** 스폰 불가. 검사 도구 `Tools/check_prefab_refs.py`(아래 함정).
- **MP 점검 6~9차 + 동맹 하네스**(빌드 사본 전용, `Tools/mp_check/`): 지점/대상 지정 액티브·회유·광폭화 오라·마나/체력 게이지 복제·다중 선택 판매·F10 소리 슬라이더·일시정지·컷인 정지·재접속 — 전부 통과, 결함은 prefab 참조 6종 하나뿐.
- **같이 하기 일시정지 + 컷인 전원 정지**(57d7ae246) — 구조는 아래.
- **동맹 유닛 공유**(faa35ced0, 마무리 feca25fe5) — 구조는 아래.
- **조합 검색 닫으면 초기화**(18ea72127): 서랍 SetOpen(false)가 검색어·등급 칩·「지금 가능」·고른 줄·스크롤을 비움, 큰 창(RecipeHelperPanel) SetOpen(false)가 검색어·능력 필터·정렬·숨김·탭·스크롤을 비움([작게 보기]는 GoBack이 닫기 전에 검색어를 챙겨 넘김).
- **위습 배치**(64bb4d344): 주인별 벌림 삭제(칸마다 같은 자리), 흔함 칸은 쵸파(`흔함_강재규` 포탈) 아래 한 곳에 모음, 겹친 위습은 내 것 우선 선택(WorldPick).
- **스토리존**(16bb6fc14·0298642c5·61d221909·685ea90c8·씬 ff69cd162·f16921a17): 착지·추격·복귀 포탈 — 아래.
- **모델 크기**(ce6d2a140 코드 + 배선 결과 1edf9b7c3): 오븐 3.0·등급 배율·넷 개별 — 아래.

## 구조 메모

### 같이 하기 일시정지 / 컷인 전원 정지
- `GamePause`(정적): `Paused`(사용자) · `CutinHold`(컷인) · `Frozen = 둘 중 하나` · `PausedBy`(슬롯). `timeScale = Frozen ? 0 : 1`, `AudioListener.pause`는 사용자 정지만.
- 흐름: P → `NetCommands.RequestPause` → `NetPlayer.RPC_Pause` → 호스트 `NetCommands.ExecutePause`가 `NetGameState.Paused/PausedBy` 세움 + 전원 알림(「○○님이 일시정지했습니다/재개했습니다」) → 전원 `NetGameState.Render()`가 `GamePause.ApplyNetworked`. 재접속 클라도 Networked 값으로 멈춘 채 입장(실측). 호스트가 timeScale 0이어도 Fusion 틱(초당 ~65)·RPC·접속은 산다(실측).
- 규칙 상수(사장님 답 대기, PM 추천 기본값): `GamePause.PausesPerPlayer = 0`(무제한) · `AnyoneCanResume = true`.
- 멈춘 동안 호스트가 경제·스킬 RPC를 `GamePause.Frozen`으로 거절(`NetCommands.Execute*` 맨 앞).
- 컷인: 호스트만 `UnitIdentity.OnAcquired`(CutinOverlay.HandleAcquired)를 알아채 `CutinOverlay.Play` + `NetGameState.RPC_Cutin(이름)`으로 전원이 Play. 정지는 `NetGameState.CutinHold`(= 호스트 `GamePause.CutinHold`)를 클라가 `ApplyNetworkedCutin`. 클라 자기 `SetCutinHold`는 무시(`IsNetworked && !IsServer`). 연속 획득 큐 동안 정지가 이어지고 한 번만 풀림(실측).
- 혼자 하기는 `NetGameState`가 없어 기존 동작 그대로.

### 동맹 유닛 공유 (사장님 10-08, 워크3식 — 내가 B에 체크하면 B가 내 유닛을 조종, 소유는 그대로)
- 상태: `NetGameState.ShareMasks[주인 슬롯]` = 조종을 열어 준 슬롯 비트(호스트 상태 → 재접속 유지). 쓰기는 `NetPlayer.RPC_SetShare` → `NetCommands.ExecuteSetShare`만.
- 판정 도우미 `AllianceShare`(Scripts/Net): `IsShared(주인,조종자)` · `CanControl(...)` · `CanControlLocal(GameObject)`(공유는 **UnitIdentity에만**, 위습·건물은 주인만) · `IsViewOnly`.
- 호스트 판정 `NetCommands.TryGetOwnedReal(..., allowShared)`: **허용** 이동·공격·정지·홀드·반복·스킬(CastActive 3종 + HudUnitAction.CastActive). **주인만** 판매·조합·창고·특성·강화·토토·도박강화·모으기(Gather)·정렬(SendToPen). 거절하면 요청자에게 「다른 플레이어의 유닛입니다.」.
- 클라 UI: `SelectionManager` 보기 전용 선택(`viewOnlyPick`, `IsViewOnlySelection`, `BlockedByViewOnly()`) — 남의 유닛은 한 기만, 초상·정보·스킬 칸 툴팁은 뜨고 명령 단축키는 무시·명령 카드는 알림, 명령 카드는 반투명 어둠막(`GameHud.commandViewOnlyDim` — CanvasGroup 알파는 칸 그림 재질이 안 따라서 못 씀), 인벤토리는 주인 것을 읽기 전용(`PlayerContext.Get(주인)`; 호스트가 슬롯마다 HeldItems 복제), 제목 띠 「○○ 인벤토리(보기)」. 공유가 꺼지면 `SelectionManager.PruneDestroyed`가 선택에서 즉시 뺌. 동맹 창 `AlliancePanel`(F11·상단 단추).
- 사장님 답이 다르면 `allowShared`만 바꾸면 된다(판매·조합 재료를 공유 받은 사람이 해도 되는지 — 원작 방침은 주인만).

### 스토리존
- `StoryZonePortal.LandingFor`: 같은 귀퉁이 방향으로 유닛마다 사거리×0.97 거리에 내림(상한 `maxLandingRadius` = 존 짧은 반변×√2×0.85 = 375.7, 하한 = 기본 착지 거리). `storyCenter`·`maxLandingRadius`는 **씬 직렬화 필드**라 `MapGenerator`가 `SetStoryZone`으로 기록 — 씬은 전체 재생성 대신 `call MapGenerator.RepairStoryZone`(복귀 포탈 다시 지음 + NavMesh 재굽기, 두 번 돌려도 안전).
- 복귀 포탈: 존 맨 위 가운데·지름 150(옛 100의 1.5배 — 광장 반지름 137.5와 위 가장자리 사이 최대). 라벨 ×4.2·가시 거리 1600~3200(`WorldLabel.SetFade`).
- 추격: `UnitCombat.StopAtRangeEveryFrame`(기본 켬)이 사거리 안에 든 프레임에 바로 세움. 끄면 예전 0.25초 주기. A/B 프로브 `ChaseStopProbe`(켬 97~98% / 끔 74~90%). 레인 유닛은 탐색 범위(aggro 18) < 사거리라 스스로 안 걷고, 바뀌는 건 A 강제 추격·공격 이동뿐. 스토리 실측 프로브 `StoryRangeProbe`(gameshot `call:StoryRangeProbe.Setup wait:30 call:StoryRangeProbe.Report`). 로스터 사거리는 월드 단위 85~900이라 「근접」 유닛은 거의 없다.

### 위습 겹침 · WorldPick
- `RewardDistributor.SpawnWisp`: `ownerSpot`=0(칸마다 같은 자리), 한 주인 안 `WispSpread` 원 벌림은 그대로. 흔함 칸은 `CommonGatherPoint`(쵸파 포탈 `UnitPortal.SpecificUnit.name == "흔함_강재규"`의 x, 칸 생성 줄에서 포탈 접촉 거리 밖).
- `SelectionManager.TrySelectAtCursor`: 첫 적중이 남의 것이면 `WorldPick.TryHitControllable`(첫 적중 +40 안쪽 광선 겹침 중 `CanControlLocal` 가장 가까운 것)로 내 것을 고름. 드래그(`SelectInBox`)는 `IsSelectableByLocalPlayer` = `CanControlLocal`이라 원래 내 것만.

### 등급 배율 · 모델 크기 (`ArtBinder`)
- `UpperGradeHeightScale = 1.2`(PM 임시값): 접두사 **전설적인·변화됨·제한·초월·불멸·영원·랜덤·다른세계**(`UnitGrade.Tier()` 5 이상 + 랜덤·다른세계). **히든·특수함은 Tier 4(전설 아래)라 제외**, 흔함~희귀 제외. 개별 표·길이표 값 위에도 곱한다(`HeightScaleFor = HeightScaleBase × GradeScaleFor`). 흔함은 기존 `CommonHeightScale 0.85`.
- 개별: 카이도(`전설적인_김용태`) 최종 ×1.7 = 표 `1.7/1.2` · `히든_석성례` 1.6 · `전설적인_임장혁` `1.6/1.2` · `히든_이동엽` 길이표 1.3→2.08. 오븐 `Enemy_R04_배병욱` 4.6375→3.0(미터 단위, 다른 거인 라인몹 규칙). 전부 PM 임시값 — 사진 보고 사장님이 조정.
- 배선 실측(전→후 프리팹 키): 카이도 ×1.7·석성례/이동엽/임장혁 ×1.6·전설/초월/불멸/영원 ×1.2·희귀/흔함 ×1.0.

## MP 두 창 하네스 쓰는 법 (빌드 사본 전용 — main에 넣지 않는다)
- 파일: `Tools/mp_check/NetCheck{5..12}Test.cs.txt` + 훅 패치(`check6_hooks.patch`·`check67_hooks.patch`·`check8_hook.patch`·`check9_hook.patch`·`check10_hook.patch`). 사용: 빌드 사본에서 `git checkout -b dev/g1-mp-xxx <main HEAD>` → 하네스를 `Assets/Scripts/Net/NetCheckNTest.cs`로 복사 → 패치(`git apply`) 또는 NetLauncher에 `-mpTestCheckN` 훅 3줄(변수·case·AddComponent) 수동 추가 → 배치 빌드 → 두 창 실행 → 끝나면 하네스를 stash/Tools로 치우고 **main과 같은 clean 분리 HEAD**로.
- 배치 빌드: `Unity -batchmode -quit -projectPath ../GuilRandomDefense-build -buildTarget OSXUniversal -executeMethod BuildBeta.Mac -logFile …`(증분 11~18초, 앱 이름 `Builds/Mac/GuRandi <버전>v.app`).
- 두 창 실행 모양(바이너리 직접 실행, `open -n` 아님, 각자 `-logFile`): 호스트 `-mpHost -mpSession <코드> -mpToken x -mpAutoStart 2 -mpDifficulty Easy -mpSaveDir <폴더> -mpTestCheckN 20 -mpQuit 130`, 로그에 `StartGame 성공`이 뜬 4초 뒤 클라 `-mpJoin -mpSession <같은 코드> -mpToken y -mpReady -mpSaveDir <다른 폴더> -mpTestCheckN 20 -mpQuit 120`. 재접속 시험은 클라에 `-mpRejoinAfter 6`(+ 하네스가 리플렉션으로 `NetLauncher.dropAt=0`). 사진은 `-mpCheckNShot <접두>`.
- 번호: 5 상점·목재 잠금·미니보스·게이지 / 6 지점·대상 지정·회유·게이지·광폭화 / 7 다중 선택 판매·F10 소리·단일도킹·광폭화 오라 / 8 일시정지·컷인 정지 / 9 연속 컷인·재접속 / 10 동맹(공유 켬·끔·역방향·재접속·보기 전용·인벤토리·어둠막) / 11 조합 검색 초기화(단독 `-mpHost -mpAutoStart 1`) / 12 위습 배치.
- 시험 중 사람 마우스가 창 위에 있으면 선택이 지워진다(「[선택] … 눌렀지만」 로그) — 창 위에 올리지 말 것.

## 함정 (오늘 겪은 것)
- 🔴 **「모델 배선」을 돌리면 `Assets/Prefabs/Generated` 전체가 새로 만들어져 루트 fileID가 전부 바뀐다.** Roster·Enemy·Miniboss는 배선이 새로 맞추지만 **Roster 밖 참조(Summons 등)는 옛 값에 남아** 클린 빌드에서 null이 된다. 배선 뒤 **반드시** `python3 -I Tools/check_prefab_refs.py .`(370건 깨짐 0 확인), 깨졌으면 `--fix`(루트 fileID로 고쳐 씀). 배포 전 관문은 **빌드 사본(클린 체크아웃) 기준**. 0.3.15 릴리스 트리는 깨짐 0이었다.
- 🔴 **`WorldLabel` 기본 보이는 거리가 260~520**이라 카메라가 높으면 글자가 사라진다(스토리 복귀 포탈 라벨이 「안 보인다」였던 원인). **다른 큰 포탈 라벨도 같은 증상일 수 있다** — 보이는 거리를 `SetFade(start,end)`로 넓힐 것.
- 씬 직렬화 필드를 새로 더하면 씬 컴포넌트가 기본값을 쥔다(`StoryZonePortal.storyCenter`) — 씬을 다시 만들거나 Repair로 기록해야 한다. 맵 전체 재생성(`Tools/맵/원랜디 맵 생성`)은 씬에서 고친 것을 되돌리니 쓰지 말고 `Repair*`(call MapGenerator.RepairXxx)를 쓴다.
- CanvasGroup 알파는 Wc3 스킨 칸 그림 재질에 안 먹는다(명령 카드 흐림은 어둠막으로).
- `UnitCommands.*`는 호스트가 클라 요청을 대신 실행할 때도 불린다 — 거기서 「호스트 로컬 플레이어 기준」 필터를 걸면 친구 유닛이 안 움직인다. 권한 필터는 UI 진입점과 `NetCommands.TryGetOwnedReal`에.
- 빌드 사본 브랜치 운영: main과 분리 HEAD로 두고, 시험은 `dev/g1-…` 브랜치를 새로 따서 하고 끝나면 지운다. 스테시에 하네스를 넣어 두면 `stash pop`이 untracked 복원 실패로 반쯤 되는 일이 있다 — 하네스는 `Tools/mp_check/`에 커밋해 두고 복사해서 쓰는 쪽이 안전. **PM이 빌드를 뽑기 전에 작업트리에 남은 미커밋·stash 확인**(10-06 남의 시험 코드를 날린 전례). 지금 빌드 사본 stash 9개는 옛 하네스 조각이라 지워도 된다(Tools/mp_check에 정본).
- 메인 에디터 순번: 「씁니다/끝났습니다」 + 남의 판 중 소스 편집·refresh 금지. 소스만 바꿀 일은 빌드 사본에서 개발·컴파일(배치 빌드)·두 창 실측 후 메인에는 `git cherry-pick`(텍스트 반영+커밋만, 에디터 안 씀)으로 얹었다.
- zsh: 글롭이 안 맞으면 명령줄이 통째로 죽는다 — 파일이 없을 수 있으면 `find … -delete`·`bash -c`. macOS `sed -i`는 `-i ''`.
- 커밋은 `git add -- 경로` 뒤 `git commit -- 경로`. 한글 경로는 `git diff --name-only -z | xargs -0`.

## 남은 일 / 사장님 답 대기
- 같이 하기 일시정지 규칙(횟수 제한·풀기 권한) · 동맹 판매·조합 재료 공유 허용 여부 · 등급 배율 1.2 및 넷 개별 크기(전부 PM 임시값) · 히든 강화에서 솔·성탄·뻬꼼 제외 · 다른세계 9기 조합 전용 · 문 0인 초월 4종 평타 · 틴트 강도 · 랜덤[제한됨] 공속 +25%.
- 미실측: 보기 전용 클릭을 실제 마우스 클릭으로 쏘는 것(리플렉션으로 같은 상태를 만들어 검사) · 겹친 위습 클릭 실제 마우스 · 컷인 큐 + 사용자 정지 중 연속 컷인의 장시간 · 광폭화 처치 뒤 오라 해제 MP · 강재규 단일도킹 외 다른 대상 지정 액티브.
- 두유찬 공속 3.0·구주호 합본은 손대지 않는다(PM 결정).
