# 구현담당1 인수인계 (10-03 밤, 직전 세션 작성) — 읽고 남은 일부터 이어간다. 다 끝내면 이 파일을 비우고 커밋.

## 오늘 끝낸 것 (참고만 — 전부 main에 커밋됨, HEAD 61781050 이후)
- **정의문 사슬**(문 파괴→3제독→버프) · **위습 아이콘 조사**(원작 위습 e015~e01A·e0IX는 base ewsp + uico 수정 0건 → 맵에 아이콘 없음, 사장님 답은 PM이 받음).
- **조합판·뽑기섬 간격 Repair**(사장님 「따닥따닥」): 
  · `MapGenerator.BoardScale` = `ArtBinder.UnitHeight ÷ 30`(=1.6). RecipeSlot·Gap·ArrowGap·RowHeight·GradeWallGap·GradeGroupGap·Cost*·ColumnPad·SlotHeight는 「…Base × BoardScale」 정적 속성. **조합판 짓는 동안(BuildCombineColumns)과 뽑기섬 짓는 동안(BuildGachaPortals)만** 켜지고 try/finally로 1로 돌아온다. 키(UnitHeight)를 또 바꾸면 간격이 따라오지만 **섬 크기는 MapLayout 리터럴×CombineBoardScale이라 실측 대조 필요**(아래 함정).
  · `MapLayout`: CombineSizeX=1404×S · CombineSizeZ=1307.5×S · LegendColumnCenterOffset=1010×S · 뽑기섬 GachaSizeX=GachaSizeXBase+265.9×(S−1)(왼쪽 등급 칸 열 폭 GachaCellColumnWidth는 그대로) · GachaExtraZ=9줄×46.2×(S−1)(윗변 고정, 아래로만) · TranscendSizeX 495(줄당 7칸 4줄).
  · **`call MapGenerator.RepairCombineBoardDryRun`**(아무것도 안 지움: 섬별 지울 개수·이름 묶음·새 발자국 안 남의 것 검사) → **`call MapGenerator.RepairCombineBoard`**(조합판·초월·불멸·뽑기섬을 지우고 다시 짓고 인형 목록·부두·뽑기섬 자연물·PlayerContext·카메라 범위·NavMesh·저장). 전체 맵 재생성은 오늘 씬에서만 고친 것(흙길 등)을 되돌려서 **안 쓴다**.
  · 실행 뒤 보고문에서 볼 것: 「가로 N/M 여유」(0 이하면 RecipeScale 축소가 걸림 → CombineSizeX 기준 올림) · 「LegendColumnCenterOffset이 ±N 어긋났습니다」(실측 값÷S로 고침; **축소가 걸린 판의 값은 틀린다** — 섬 폭부터 고치고 다시 잴 것) · 뽑기섬 「다른세계 조합식: 가장 넓은 줄/리터럴」·줄 수.
  · 초월 전시 25번째 인형이 섬 밖 바다 위에 떠 있던 건 **원래부터 있던 결함**(줄당 6칸=5줄인데 섬 깊이는 4줄) → 폭 495로 고침 + Repair가 이름 접두(초월_·불멸_·…전시)로 섬 밖 전시물도 지움 + BuildTranscendDisplay가 줄 수 넘치면 경고.
  · 결과: 인형 목록 681기(조합판 635+뽑기섬 46), 「[조합표 인형] 681/681기」, 전 섬 겹침 없음, 바다 ±3334 안(동 여유 ≈1787·남 788), 뽑기섬↔스토리 303.5·조합판↔뽑기섬 303.5 유지.
- **카메라 구도**: FOV30·피치56(camera-original)을 넣었지만 **사장님이 반려**(「위에서 내려다보는 납작한 시점, 원작은 대각선에서 가깝게」) — FOV를 좁혀 멀어진 게 원인. PM이 b49e50a63을 revert하고 시작 줌 0.85 + 채팅 「시야 N」(100=0.65·150=0.75·200=0.85)로 대체. 다시 건드리지 말 것.
- **바다색**: 밝기를 재 보니 우리가 원작보다 어둡지 않다(잔디 2배 밝음) — 차이는 **바다 색상**(원작 R≈0 짙은 청록 (7,60,91), 우리 (38,70,106)). 시험판 패치 `~/GRD_motion_trial/sea_trial.patch`(SeaWater.mat 얕은 (0.25,0.82,0.80,a0.60)·깊은 (0.04,0.42,0.58,a0.90)·깊이 8→14, 밝은 청록으로 바뀜). **main에 안 넣음 — 사장님 답 대기 중**(PM이 받는다).
- **autoloop 위습 칸 수정**: 사장님 지시로 HUD 위습 칸 글자(Name TMP)가 3D 아이콘으로 바뀌어(자식 Icon·Count뿐) autoloop이 「랜덤유닛」 칸을 못 찾아 위습→포탈이 한 번도 안 돌았다. `ClaudeCommands.WispSlotKind(slotRoot)`가 GameHud.wispSlots(private, 리플렉션)의 칸 WispData.wispName에서 옛 글자(「위습」 뺀 이름)를 다시 만들고 `ButtonLabel`·`ReadWispSlots`가 그걸 쓴다. rounds:3 판에서 WispSlot0「랜덤유닛」→Portal_유닛랜덤 5/5 확인.
- **카메라 시험 중 측정도구 소견**: 새 구도에서 드래그 「안 먹음」이 29번 중 5번(옛 9번 중 0, 표본 작음) — 기준선 로그의 「드래그가 안 먹음」 수를 같이 볼 것.
- 새 도구: `Tools/ui/csc_check_editor.sh`(Editor 폴더 Roslyn 검사 — worktree에서 Editor만 고친 경우 쓴다; 참조는 주 저장소의 마지막 Assembly-CSharp.dll), `Tools/g1_chain.sh`(기준선 체인, 아래).

## 남은 일: 오늘 밤 새 기준선 g1_290~299 — **새 세션이 돌린다**(PM 「기준선 시작」 신호 뒤)
- 끈 판 290·292·294·296·298 / 켠 판(support pirate) 291·293·295·297·299. 오늘 HEAD(조합판 Repair·초월 수정 반영) 기준.
- **정확한 명령**: 판당 `gameshot b<번호> 75 1600x900 rounds:60 autoloop keeppen sell aim:전설 bosschase mode:보통`(켠 판은 끝에 ` support pirate`)를 `ClaudeBridge/inbox/g1_<번호>.txt` 한 줄로. 체인은 `nohup /Users/sang/GitHub/GuilRandomDefense/Tools/g1_chain.sh > /dev/null 2>&1 & disown`(맥엔 setsid 없음) — 번호마다 outbox를 지우고 inbox에 쓰고 「gameshot 결과」가 outbox에 생길 때까지 10초 간격으로 기다린 뒤 20초 쉬고 다음, 진행은 `ClaudeBridge/g1_chain.log`(끝나면 ALLDONE). **gameshot outbox는 판이 끝난 뒤에야 생긴다**(안 생겼다고 죽은 게 아님). 중단 신호 `touch ClaudeBridge/STOP`.
- **판 중엔 Assets 쓰기·소스 편집·refresh 금지** — 시작 전 다른 세션 둘(PM·구현담당2)에 알릴 것(미컴파일 소스가 있으면 남의 판이 시작 때 리로드돼 무효).
- 시작 전 확인: 짧은 판 `gameshot x 40 1600x900 rounds:3 autoloop keeppen sell aim:전설 bosschase mode:보통`에서 「WispSlot0「랜덤유닛」」 클릭·「Portal_유닛랜덤」 우클릭이 도는지, `[조합표 인형] 681/681기` 로그.
- 끝나면 통과율·패배 라운드·원인 갈래를 BALANCE 문서 §10(새 기준선)에 정리 + 새로 들어간 것(압살롬 도박·좀비/모리아 100%·10초·정의문 사슬·막타 목재 글자·유닛/적 1.6배(UnitHeight 30→48·EnemyHeight 22.5→36)·흙길 넓힘·위습 칸 아이콘화)이 도구 판에 영향 있는지 한 줄씩. 1.6배 키·흙길 폭은 **사거리 닿는 범위·가동률(lane-uptime)에 영향 가능** — 이전 기준선과 직접 비교하지 말고 새 기준선으로 읽을 것.

## 함정
- 🔴 에디터는 main 하나를 여러 세션이 같이 쓴다. refresh·플레이·Assets 쓰기 전 「씁니다」, 끝나면 「끝났습니다」. 브리지 inbox 파일 이름에 `g1_` 접두.
- **빈 인자 grep**: `grep -n x $(grep -l y … | head -1)`에서 grep -l이 아무것도 못 찾으면 인자가 비어 stdin을 기다리며 **무한 대기**한다(오늘 5분 날림). 변수에 담아 `[ -n "$f" ]`로 확인.
- 씬 커밋: URP 조명 블록(guid 474bcb49853aa…)은 HEAD에 없던 것만 지운 정리본을 `git hash-object -w` → `git update-index --cacheinfo 100644,<hash>,Assets/Scenes/SampleScene.unity` → **경로 없이** `git commit`(경로를 주면 작업 파일이 그대로 올라감). 그 전에 `git diff --cached --name-only`가 비었는지(남의 것) 확인. 정리 스크립트는 문서 단위로 114 블록 id를 HEAD와 비교해 빼고 GameObject의 `- component: {fileID: …}` 줄도 지움(오늘 두 번 씀).
- Repair 기본기: `call MapGenerator.X`는 private static string 메서드도 부른다. 타일 재질 변종(.mat)은 Paint가 새 크기마다 만들고 Repair에선 Sweep을 **일부러 안 부른다**(사용 기록 기반이라 다 지움) — 쌓이는 파일은 새로 커밋.
- 「조합표 인형 미리 세우기」 메뉴는 편집 화면에서 인형을 보게 해 주고(씬에 저장 안 됨) 「…미리보기 지우기」로 지운다. 전시 인형(초월_·불멸_)은 목록이 아니라 씬 오브젝트(위치 0,0,0 부모 + 자식 — 원래 그렇다).
- 판 시작 때 「Failed to create agent because it is not close enough to the NavMesh」 경고 1건은 **WispIconBaker.Bake**(구현담당2의 위습 칸 아이콘 굽기)가 위습 프리팹 복제본(NavMeshAgent 포함)을 NavMesh 밖에 Instantiate해서 나는 무해한 경고 — 고치려면 복제본의 NavMeshAgent를 끄거나 NavMesh 위에 둘 것(구현담당2 소유).
- `shot`(편집 화면 촬영)은 인형·HUD가 없다. UI·인형 포함 사진은 gameshot(해상도 필수).
- 원작 수치·근거는 Tools/w3x의 j·w3a·w3u 직접 디코드로. 문서·보고문의 숫자는 근거가 아니다(오늘도 조합판 자연 폭 1373이 1403이었다 — 실측으로 잡음).
