# 구현담당1 인수인계 (10-07 밤 마감, 0.3.14 큐 끝)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md` → TEAM_RULES 순으로 읽는다. 전부 main에 커밋됨(푸시는 PM).

## 오늘(10-07) 끝낸 일 (커밋 해시는 git log로)
- 초월 25종 합계 DPS 표(Docs/research/TRANSCEND_DPS_AFTER_REINSTALL_2026-10-07.md, Tools/transcend_dps_table.py) · 초월 임채민 청렴결백(예배시간·지상낙원·축복의땅[지정+발동]·천벌, 새 kind ManaRegenBuff·LifeRegenBuff·DesignateAlly·타깃 DesignatedAlly) · 두유찬 더 세게(공속 3.0·게이지 12) · 조합 획득 금화(CombineRecipe.acquireGoldReward) · 스토리 이름 「01. 하이츠」(StoryManager.DisplayName)
- MP 2차 점검(두 창): 토토·두유찬 금화·재접속(회유·강화·비행 위치 유지)·비행 Stop/Hold·일시정지 단추·현재레벨 라벨 ✅, 클라 아군 지정 액티브 RPC 신설(NetCommands.RequestCastActiveOnAlly) · 회유 실전투는 표적이 0.5초 안에 사라져 확정 못 함(△)
- 레인 십자: 벽 윗면 NavMesh 제거(NavMeshModifier) + 배성령 순간이동 PathComplete 검사. 사장님 「우클릭 걷기」 재현은 못 함
- **늦게 끝낸 일**: 적 출발점 포탈(EnemyPortalApply·EnemyPortalSpin, 블렌더 enemy_portal.fbx) · 바다 물 원작 톤(SeaWater 셰이더+WaterApply) · **버그 기록지**(BugNotepad.cs — 스스로 붙는 자체 캔버스, 버그기록.txt = exe/.app 옆·못 쓰면 GameLog 폴더, `-bugnoteTest 글` 시험 인자) · **모리아 자리 = 안흔함_이호준**(좀비 raiseOnKill 이전, MoriaApply) · 스토리 건물 ×0.7 · 유물 중복 금지 · 다중 선택 정렬·초상 카드
- 해적선→상붕카 지급처 4곳 · 흔함 선택 줄 순서 · 원작 대응 이름 표시(UnitData.originalMatchName, GameVersion.BetaShowOriginalMatch) · 명령 카드 원작 배치(이동·홀딩·정지·공격 / 반복·스킬·스킬·판매 / 조합 초상) · 반복(패트롤, P) · 판매는 희귀함까지 · 다중 선택 정렬·초상 카드 · 스토리 건물 ×0.7(ArtBinder.StoryBuildingScale) · 유물 중복 금지(PickMissingRelic) · 적 출발점 포탈 · 바다 물 원작 톤

## 사장님 확인 대기 값 (제안값)
- 임채민 발동 확률 25%·천벌 마나 120 · 유물 중복 시 대체 금화 3,000엔 · 스토리 건물 배율 0.7(더 줄일지) · 좀비/모리아 등급 표기(사장님 「특별함 모리아」인데 로스터 이호준은 안흔함)
- 버그 기록지 한글 IME 조합은 친구 윈도우 PC에서 확인(배치 모드에선 못 쳐 봄) · 맥 격리 앱이면 로그 폴더로 대체됨

## 빌드 사본 상태 (../GuilRandomDefense-build)
- 지금 브랜치 dev/g1-bugnote(= main 89d11a0a2 + BugNotepad.cs 미커밋 사본). **PM이 0.3.14 빌드에 쓴다 — 손 대지 말 것.** 내 이전 브랜치: dev/g1-mp-check3(NetMainTest2 — 커밋됨, 버려도 됨)·dev/mp-check2(3cd616180, 깨끗). 빌드 사본의 DevVoiceProbe·폰트·URP 변경은 남의 것.

## 남은 일 / 사장님 답 대기
- **제안값(근거 없음 — 사장님 의견 받으면 수정)**: 임채민 발동 확률 25%·천벌 마나 120·지정 칸 쿨 1초 / 유물 중복 때 대체 금화 3,000엔 / 스토리 건물 배율 0.7(더 줄일지) / 스토리 정렬 등
- 임채민 특성강화 3pt 고정 3,000,000은 실제로 눌러 확인 안 함(주호 틀과 같음)
- 회유 유닛 실전투 결정적 시험(체력 큰 표적 필요) · 사장님 「우클릭으로 십자 넘어감」 재현 정보(위치·유닛 이름·사진) 대기
- 다중 선택 초상 얼굴 배율은 구현담당2 PortraitStage 몫
- MP 미실측 이전 항목: 마나 스킬(정윤식·이승우·고도현)·범퍼 실피해·라인딜·보잡·유닛삭제·순간이동 발동·방무뎀·폭뎀·노획물 180초 실주기

## 위치·도구
- 탐침/적용 도구(편집 모드 `call 클래스.메서드`, 플레이는 `gameshot`): ChaeminApply/Probe · DuyuchanApply/Probe · CrossProbe(십자·Reach) · CommandCardProbe · Queue0307Probe(다중선택·스토리 크기·유물) · OriginalMatchApply/Probe · EnemyPortalApply(MakePrefab·Place·PlaceAll) · WaterApply · RelicPoolApply · PirateShipSwapProbe · ChoiceRowProbe · StoryLabelProbe · DuyuGoldProbe
- 멀티 점검: 빌드 사본 브랜치 dev/g1-mp-check3(NetMainTest2, `-mpTestMain2 [state]`) — 두 창 `-mpHost`/`-mpJoin -mpSession 코드 -mpToken` · 시험 로그 접두 「[M2]」
- 표/문서: Docs/research/ORIGINAL_MATCH_NAMES_2026-10-07.tsv · ATTACK_REINSTALL_TABLE_2026-10-07.tsv · TRANSCEND_DPS_AFTER_REINSTALL_2026-10-07.md

## 함정 (오늘 겪은 것)
- **zsh는 변수를 단어로 안 쪼갠다**: `git add $F` 말고 배열 `F=(…); git add -- "${F[@]}"`. 한글 파일명 grep/status는 `core.quotepath=false`(GIT_CONFIG_COUNT 환경변수 방식 쓸 수 있음).
- **브리지 inbox 파일 이름 재사용 금지**: 같은 이름이면 옛 outbox가 남아 있어 「끝났다」로 오인한다(사진이 남의 것이었다). 항상 새 이름 + 대기 전에 `rm -f outbox/이름.txt`.
- **디스크에서 씬/에셋을 파이썬으로 직접 고치면** 유니티가 「외부에서 바뀜 Reload/Ignore」 대화상자를 띄워 브리지가 멈춘다(osascript로 Reload 클릭하면 풀림). 가능하면 편집 모드 `call`로 고칠 것.
- **gameshot 마지막 `wait:` 뒤 3초 뒤에 찍힌다**: 3초짜리 알림은 마지막 wait을 빼야 사진에 남는다.
- **에디터는 순번제**: 쓰기 전 「씁니다」, 끝나면 「끝났습니다」(구현담당2 포함). 남의 gameshot 중 소스 편집·refresh 금지(컴파일 재로드가 그 판을 무효로 만든다). Editor.log의 옛 `error CS`는 지워지지 않으니 새 줄인지 확인.
- **GameHud는 구현담당2와 같이 쓴다**: 구역(1=명령 카드·선택 카드·판매 / 2=하단 바 조립·정보창·메뉴). 커밋 전 「커밋합니다」 한 줄. 한 커밋에 남의 hunk가 섞이면 커밋 메시지에 적는다.
- **SeaWater 셰이더에서 `_Smoothness`를 낮추면 반짝임이 「넓어져」 화면이 하얘진다** — 폭은 0.88, 세기는 `_SpecIntensity`. 미니맵은 직교 카메라라 무늬·거품을 끈다(unity_OrthoParams.w).
- 새 UnitData/ItemData를 만들면 NetSetup.BuildCatalog 재생성(호스트·클라 같은 빌드). 이번엔 새 에셋 없어 불필요했다.
- 씬 커밋: 작업 트리 SampleScene에 남의 직렬화 잔여물이 섞일 수 있다 — 이번엔 내 hunk만이라 통째로 커밋했다(`git diff`로 확인할 것).
