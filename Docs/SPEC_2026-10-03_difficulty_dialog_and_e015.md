# 사양 확정 — ① 난이도 선택 대화상자(게임 안) · ② 신세계 진입 보상 e015 · ③ UI 미결 4건 판정

구현담당2, 2026-10-03. 근거 = `Tools/w3x/원본/war3map_new.j`(줄 번호 = python `split('\n')`) + `war3map_new.w3u` 직접 디코드. 문서는 근거로 안 씀. **코드는 아직 안 넣음**(기준선 판이 에디터 사용 중).

---

## ① 난이도 선택 — 원작은 「게임에 들어가서」 방장이 정한다

### 1-1. 원작 흐름 (j)
| 시각 | 일 | 근거 |
|---|---|---|
| 0.02초 | `InitStart`: 전원 시네마틱 모드(화면 검정·UI 숨김) · 카메라 거리 3000 · 카메라 범위 제한 · 「잠시만 기다려주십시오」 6초 | [j 15260, 15208~15209] |
| 6.6초 | `Select1`: 시네마틱 해제(UI 보임) → 대화상자 「모드를 선택하세요.」 + 버튼 **6개** `쉬움|cff00bfff`·`보통|cffee82ee`·`어려움|cffff0000`·`지옥|cff9400d3`·`신|cffffd700`·`악몽|cffffd700` + 「 모드」(색은 앞 글자만) | [j 15264~15278, 15284] |
| 같은 때 | **방장(Player(0))에게만** 대화상자 표시 [j 15280]. 전원에게 「방장이 모드를선택하고있습니다. 잠시기다려주세요.」 **30초**(방장 화면에도 뜸) [j 15279] | |
| 고른 뒤 | `Select_effect`: 모드별 설정(데스카운트·마법DP·연구·신세계 여부) + 모드별 안내 30초(TRIGSTR_8904~8914) → -load 안내 20초 → **「1라운드 시작까지」 21초 타이머** → 점수판 생성 → 스토리 시작 | [j 15324~15492] |

- **악몽 버튼이 뜨는 조건**: **없다.** j(ORD11.089)는 6개를 무조건 추가한다 [j 15267~15278]. 사장님 사진(시즌 6 화면)은 5개(신까지)라 **맵 버전 차이** — 악몽은 이 j 버전에서 추가된 것. 우리는 j 기준 **6개 유지**.
- **시간 제한·기본값**: 없음(아무도 안 누르면 영원히 대기 — 이미 `DifficultySelectHud` 주석과 같은 결론). 시간 만료 트리거 없음(`TriggerRegisterDialogEventBJ`만 [j 15496]).
- **고르기 전 게임 정지?**: 워크3 시간은 멈추지 않지만 **라운드를 시작하는 모든 것(타이머·Start2·점수판·스토리)이 `Select_effect` 안에 있어** 고르기 전엔 아무 일도 안 일어난다. 우리도 이미 `RoundManager.roundsStarted`가 `DifficultyManager.IsModeSelected`를 기다린다 [우리: RoundManager.Update]. 대기 중 위습 이동·조작은 가능(원작도 시네마틱만 풀림).
- **투표 없음.** 방장 단독.

### 1-2. 지금 우리 (코드 대조)
- **게임 안 대화상자는 이미 있다**: `UI/DifficultySelectHud.cs`(호스트에게 6버튼, 나머진 안내 문구, 선택 전 대기) — 즉 「원작처럼 게임에서 정한다」의 뼈대는 끝나 있고, 문제는 **대기실에서 먼저 정하는 경로가 겹쳐 있다**:
  - 로비: `NetLobbyUi` 난이도 6버튼 → `NetLauncher.SetDifficulty` → `NetGameState.Difficulty`([Networked]) → `MatchConfig.Difficulty` → `DifficultyManager.Awake`가 읽어 **대화상자를 건너뜀**(`MatchConfig.Active && MatchConfig.Difficulty.HasValue`) [Waves/DifficultyManager.cs 42~44].
  - 싱글: `DifficultyManager.Awake`가 `PlayerPrefs`의 기억값이 있으면 **대화상자를 건너뜀** [같은 파일 54~66], `SelectMode`가 기억값을 저장 [81~88].
- 원작과 다른 점 둘: ①로비 선택 ②**기억값으로 건너뜀**(원작은 매 판 묻는다).

### 1-3. 구현 계획 (기준선 끝난 뒤)
1. `NetLobbyUi` 방 패널에서 난이도 6버튼·힌트 삭제(방 패널 개편안 수정 — 대신 왼쪽 정보 카드에 「난이도는 시작 후 방장이 고릅니다」 한 줄). `NetLauncher.SetDifficulty`·`MatchConfig.Difficulty`는 **남기되 호출부 없음**(재접속·호환용) 또는 삭제.
2. `DifficultyManager.Awake`: **기억값 자동 선택 제거**(원작은 항상 묻는다). 대신 `PlayerPrefs` 기억값은 대화상자에서 **마지막으로 고른 버튼 강조**용으로만 남김(선택은 사람이). 기존 `mode:` gameshot 인자는 **자동 클릭 경로**로 유지(= `DifficultySelectHud`의 호스트 버튼을 코드로 누르는 헬퍼 `DifficultySelectHud.AutoPick(mode)` — 측정 도구 `ClaudeCommands`의 mode 처리가 기억값 설정 대신 이것을 부르게).
3. `DifficultySelectHud`를 **원작 대화상자 모양**으로: 검정 전체 화면 오버레이(시네마틱 구간은 6.6초 연출 — 「잠시만 기다려주십시오」 6초 문구 후 대화상자) · 돌 테+금테 남색 판 · 제목 금색 「모드를 선택하세요.」 · 버튼 6개 세로(남색+금테 가장자리, 앞 글자 원작 색 + 흰 「모드」). 사진 `Docs/reference/ui/원랜디_난이도선택.png` 기준: 판 가운데, 폭 ≈ 화면 33%·높이 ≈ 48%, 버튼 6개(사진은 5개 — 악몽 추가). 다른 플레이어 화면은 왼쪽 가운데(x 6~34%, y 65~70%)에 「방장이 모드를선택하고있습니다. 잠시기다려주세요.」.
4. 안내 문구: 원작 「방장이 모드를선택하고있습니다. 잠시기다려주세요.」 **30초**(방장에게도) + 선택 직후 TRIGSTR 안내(이미 `AnnounceDifficultyOnce` 있음) + 「-load」 20초(이미 있음) + 「1라운드 시작까지」 21초(이미 있음 — `firstRoundDelay`).
5. 멀티: **호스트가 클릭 → `DifficultyManager.SelectMode`(호스트 로컬) → `NetGameState.Difficulty` 쓰기(이미 [Networked]) → 클라 `ApplyReplicatedMode`**(이미 있음 — NetGameState 190~193 재접속 경로와 같은 호출). 새 [Networked] 필요 없음. 클라 대화상자는 `IsModeSelected`가 되면 사라진다(이미 그렇게 돎).
6. 영향 파일: `UI/DifficultySelectHud.cs`(모양·시네마틱 구간), `Waves/DifficultyManager.cs`(기억값 건너뛰기 제거), `Net/NetLobbyUi.cs`·`Net/NetLauncher.cs`(로비 버튼 제거), `Editor/ClaudeCommands.cs`(`mode:` → AutoPick, **측정 도구가 계속 돌게 먼저 확인**), `Net/MatchConfig.cs`(Difficulty 필드는 유지해도 무방).
7. 난이도: 중-하. 위험: `ClaudeCommands`의 `mode:`/`click?:쉬움` 흐름과 구현담당1의 기준선 도구(`gameshot … mode:보통`)가 대화상자 전환 중 깨질 수 있어 **전환 커밋 직후 짧은 mode: 판으로 확인**(그리고 구현담당1에게 알림).

---

## ② 신세계 진입 보상 e015 — 원작 사양

### 2-1. 지급 (j `Trig_Round_10ver_Actions`, Stage 11 = 「60라운드-신세계 대기중」)
| 항목 | 원작 | 근거 |
|---|---|---|
| 시점 | 60라운드 시작 전 대기(신세계 진입) 블록에서, 점수 1250 덮어쓰기와 같은 때 | [j 29684~29760] |
| 대상 | **살아 있는 플레이어만**(`PlayerDeath==0`) | [j 29529·29535·29547] |
| 수량 | **악몽 1기 · 신 1기 · 그 밖(어려움·지옥, 이 블록에 닿는 모드) 2기** — 모드 문자열 비교 `악몽`→1, `신`→1, else→2 | [j 29553~29754, 조건 함수 29529~29553] |
| 위치 | 그 플레이어 소유로 `StoryReward_Base6` 구역 중앙에 생성 | [j 29714·29728·29741] |
| 표식 | **`SetUnitUserData(unit,1)` = 신세계 보상 표식** | [j 29716·29730·29743·29747] |
| 같은 때 | 안내 「미지의 바다인 신세계로 출항합니다…」「신세계는 라운드 타이머가…」(TRIGSTR_15735, 이미 있음) · 전원 데스카운트 1 · 퇴치 상점 파괴(`h07A` 4기 `KillUnit`) [j 29692~29759] | |
| 지급 문구 | 없음(위습이 조용히 생김) | — |

- `e015` = 「전설위습」(w3u: 이름 `전설위습`, 기본 `ewsp`, 능력 `Aeth,Avul`(유령·무적), 이동 522, 선택칸 없음). 우리 대응 = **`Wisp_전설히든`**(`wispName 「전설·히든 위습」`, `targetGrade Legendary`, 선택 아님) — 이미 R65/70/75 보상으로 쓰이고 있어 **그대로 같은 위습을 쓰면 된다**(표식만 추가).

### 2-2. 사용 (j `Trig_Story_Tier6_Legend`, 위습이 `StoryReward_Tier_Legend` 구역에 들어갈 때)
| 단계 | 원작 | 근거 |
|---|---|---|
| 조건 | 들어온 것이 e015 | [j 84665~84670] |
| ① 감점 | **userdata==1(신세계 표식)이고 Level≤75**이면 `풀카운트 −= sinsekai_reward_int × 625` + 문구 `|cffFF0000신세계 보상사용:풀카운트{625×int}점 감소!` **2초(그 플레이어에게)**. `sinsekai_reward_int` = 기본 1, **신·악몽은 2**(Select_effect) | [j 84671~84696, 15422·15451·10936] |
| ② 결과 | **50%**: `Random5` 그룹(전설 유닛 풀)에서 무작위 한 종 / **50%**: 히든 풀 `Unit_type_Hidden[0..20]`(21종) 중 하나 — 그 플레이어 구역에 생성, 문구 `|cffFF0000{이름} 획득!|r` 3초, 위습은 `KillUnit`(소모) | [j 84683~84707] |
- 표식 없는 e015(= `hokins_fortune` [j 100296]·`yostuba_tr` [j 102285]가 만드는 것)는 ②만 받고 **감점 없음**.
- 총합: 어려움·지옥 = 위습 2기 × 625 = 1250 감점 가능, 신·악몽 = 1기 × 1250 = 1250 — **어느 모드든 보상 전부 쓰면 −1250**.

### 2-3. 우리 쪽 대응 (코드 대조)
- `Wisp_전설히든` 사용 경로: 위습이 포탈/구역에 들어가 `WispData.targetGrade`(Legendary)에 따라 유닛을 뽑는다(`UnitPortal` ~199행). **전설 50% / 히든 50%의 분기가 이미 있는지** 구현 때 `UnitPortal`에서 확인 — 없으면 원작 분기(50% 전설 풀 / 50% 히든 21종) 추가.
- 표식: 위습 컴포넌트(`Wisp`)에 `bool newWorldReward` 한 칸 + `RewardDistributor.GrantNewWorldWisps()`(진입 때 호출, `RoundManager`의 `currentRound == 60 && totalRounds > 60` 블록 — 풀카운트 진입 훅과 같은 자리).
- 감점: `FullCountScore.OnNewWorldRewardUsed(slot, times)` — `−= times × 625 × (신·악몽 ? 2 : 1)`, 문구 2초 빨강, **Level≤75 가드**(마지막 라운드를 끝낸 뒤엔 감점 없음). 로그 `[풀카운트] 슬롯 N 신세계 보상 사용 −M · 합 K`.
- 쓰지 않은 e015: 원작도 R75를 넘어 쓰면 감점 없음(가드), 안 쓰고 끝나면 그대로 점수 유지 → 「점수를 위해 보상을 아낀다」가 원작 의도.
- **멀티**: 위습은 호스트가 만든다(`GrantWisps` 호스트 가드). 표식은 호스트 로직 전용 → **새 [Networked] 불필요**. 감점은 이미 복제되는 `FullCounts`가 나른다.
- 영향 파일: `Units/RewardDistributor.cs`(지급), `Units/Wisp.cs`(표식), `Units/UnitPortal.cs`(사용 시 감점·50/50), `Units/FullCountScore.cs`(감점), `Waves/RoundManager.cs`(호출 한 줄).
- 확인(실측) 계획: `jump:59`→`AdvanceRound`(진입) → 위습 1~2기 확인 → 그 위습을 포탈로 보냄 → 풀카운트 로그/사진.

---

## ③ UI 미결 4건 판정 (PM 질문)
1. **「퀘스트」 버튼**: j에 `CreateQuest`·`QuestSetDescription` **0건**(퀘스트 로그를 안 씀) — 사진의 첫 버튼이 흐린 이유와 일치. → **버튼만 두고 비활성(흐리게)**.
2. **영웅 단추**: 원작 영웅(`H`로 시작 + 베이스가 영웅 클래스) = **w3u에서 28종**(`Hvwd` 27 + `Hpal` 1). 그중 이름이 `흔함영웅`인 23종과 `팔라딘-스킬데미지+창고용`은 이름만 있는 틀이고, **우리 로스터와 매핑된 것은 `MASTER_UID_ROSTER_MAP.csv`의 `H…` 행 27개(초월_* 26 + 영원_이지원 1, 일부 중복 매핑)**. → 영웅 단추 = **내 소유 유닛 중 이 27행에 대응하는 것**(왼쪽 아래 세로). ⚠️ 사진에서 정보칸에 뜬 「몽키.D.루피 … 초월함」은 `h04R`(소문자 h, 베이스 `hrif` = 영웅 아님)이라 **영웅 단추 대상이 아니다**(마나 160이 있을 뿐). 레벨/XP(`AddHeroXP`)는 영웅 그룹에만 [j 14734] → 우리 `CharacterLevel`을 가진 유닛과 이 27행의 교집합을 구현 때 데이터로 확정.
3. **마나**: 원작 마나는 유닛마다 `umpm`(w3u). `h`·`H` 유닛 중 **`umpm>0`인 것이 85종**(예: `h04R` 루피 160) — 초상화 아래 마나 줄은 **그 유닛만**. → 우리 쪽은 로스터↔원작 id 대응(MASTER_UID_ROSTER_MAP)으로 `umpm>0`을 뽑아 `UnitData.hasMana`(생성 스크립트로 에셋에 기록)로 두고, 표시값은 PM 지시대로 **상단 마나와 같은 값**.
4. **인벤토리 소유 상한 6**: `MaxItemInventorySlots 8 → 6`. 7·8번째 보유 경로가 있는지는 `ItemInventory`의 추가 경로(획득·도박·보스 드랍·창고)를 구현 때 전부 훑어 **상한 6에서 막고 넘치면 알림**(원작 한 영웅 6칸 — 넘치면 땅에 떨어짐인지는 [워크3 기본 동작] 확인 필요).
