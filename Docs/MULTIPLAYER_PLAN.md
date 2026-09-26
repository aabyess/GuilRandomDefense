# 멀티플레이 설계 — Photon Fusion 2 (초안, PM 검토 대기)

> 작성: 구현담당2, 2026-09-25 · 브랜치 `mp` (worktree `../GuilRandomDefense-mp`)
> 전제 문서: `Docs/MULTIPLAYER_MIGRATION.md`(08-28, 영향 분석 — **파일 목록은 낡았다**, 개념은 유효) · `Docs/PHOTON_SETUP.md`
> SDK: `Assets/Photon/Fusion` **2.1.2 Stable 2279**(build_info.txt). App ID는 `PhotonAppSettings.AppIdFusion`에 **들어 있다**(값은 안 적음).
> ⚠️ Fusion API 이름은 구현 때 **SDK 소스(`Assets/Photon/Fusion/Runtime`)로 확인**한다 — 공식 문서는 봇 차단(PHOTON_SETUP.md)이라
> 이 문서의 API 이름은 가설이다.

---

## 0. 한 줄 요약

**호스트가 지금의 싱글 게임을 그대로 돌리고, 네트워크는 그 결과를 클라에 비추는 거울이다.**
실물(전투·이동·판정을 하는 GameObject)은 호스트에만 있고, 클라에는 「껍데기(NetworkObject) + 겉모습(로직을 뗀 프리팹)」만 있다.
클라가 게임을 바꾸는 길은 **요청 RPC 하나뿐**이다.

## 1. 현재 상태 (실측, 09-25 HEAD `453d065d`)

| 항목 | 상태 |
|---|---|
| Fusion 참조 | 게임 코드 0곳. `Assets/Photon/`에 설치만 |
| `GameAuthority` | `Provider` 대입 0곳 → `IsServer` 항상 true(= 싱글) |
| `IsServer` 가드 | 20개 파일. 판정 쪽은 대부분 막혀 있다(RoundManager·RewardDistributor 7·UnitPortal·ResourcePortal·WaveSpawner·Story·Seal·SeaKing·Treasure·CombineSystem 등) |
| 로컬 플레이어 ID | **두 군데**: `LocalPlayer.LocalPlayerId`(static, 0) · `GameAuthority.LocalPlayerId`(Provider→없으면 LocalPlayer). UI 대부분이 전자를 직접 읽는다 |
| 「내 것」 필터 | 이미 있다: SelectionManager(소유자≠나 → 선택 불가) · UnitMover(소유자≠나 → 무시) · PlayerNotification(내 큐만 그림) · RtsCameraController(내 레인으로) · DifficultySelectHud(`IsHost`=ID 0) |
| 플레이어 슬롯 | `PlayerContext` 4개(씬), `occupied` 플래그, `LaneMarker` 4개, WaveSpawner는 「레인 N = 플레이어 N」·빈 슬롯 스킵 |
| 생성 지점(`Instantiate`) | 9곳: UnitSpawner · RewardDistributor(위습) · WaveSpawner · SealSpawner · SeaKingSpawner · StoryManager · TreasureHunt · PirateQuestManager · RecipeDollSpawner(인형=로컬 장식) |
| 씬 | `SampleScene` 하나(빌드 세팅도 하나) |
| 데이터 카탈로그 | **없다.** UnitData/EnemyData/WispData를 번호로 가리킬 목록이 없다 → 새로 만든다(§4) |
| 테스트 도구 | Multiplayer Play Mode **미설치**(`multiplayer.center`만 있음) |

## 2. 토폴로지 — **Host 모드** 추천

| 모드 | 판정 | 이유 |
|---|---|---|
| **Host** | ✅ 채택 | 우리 코드는 이미 「한 곳(서버)이 전부 판정」 구조다(IsServer 가드). 호스트 = 친구 한 명의 PC라 서버 비용 0. Photon Cloud가 릴레이/NAT 통과를 해 줘서 포트포워딩이 필요 없다 |
| Shared | ❌ | 각 클라가 자기 오브젝트의 권위를 갖는다 → 「골드·라운드·보상은 한 곳에서만」이라는 지금 구조와 정면충돌. 적·라운드·스토리 같은 공유 상태의 주인이 애매해진다 |
| Server(전용) | ❌(지금은) | 헤드리스 서버를 어딘가에 띄워 둬야 한다. 친구 베타에 과하다. 코드가 Host와 거의 같아서 나중에 옮기기는 쉽다 |

감수하는 것:
- **호스트가 나가면 판이 끝난다**(호스트 이전은 안 한다 — 베타엔 과함).
- **클라 예측 없음** — 클라의 우클릭 이동은 왕복 지연(수십~150ms)만큼 늦게 출발한다. 디펜스 장르라 감수 가능하다고 본다.
- 호스트는 자기 화면에서 지연 0으로 논다(호스트 유리). 친구 베타 범위에서 문제 삼지 않는다.

## 3. 핵심 구조 — 「실물은 호스트, 클라는 거울」

### 왜 프리팹마다 NetworkObject를 붙이지 않나
- 유닛 프리팹 209개는 `Tools/generate_*.py`가 만든다(**파괴적** — 다시 돌리면 붙인 컴포넌트가 날아간다). 적 프리팹 ~78개는 ArtBinder(PM) 소관.
- 정석 Fusion은 로직을 `FixedUpdateNetwork`로 옮기는 것인데, 게임 코드 2.1만 줄을 다시 쓰게 된다.
- 지금의 싱글 경로(gameshot·밸런스 측정)가 **한 줄도 안 바뀌어야** PM·구현담당1 작업이 안 흔들린다.

### 구조

```
[호스트]                                          [클라]
실물 유닛/적/위습 (지금 그대로, 로직 전부)
   │  NetLink(실물에 붙는 꼬리표)
   ▼
NetEntity (공용 NetworkObject 프리팹 1종) ──복제──▶ NetEntity.Spawned()
  [Networked] Kind · CatalogIndex · Owner             └ 카탈로그로 프리팹을 찾아 「겉모습」 생성
  [Networked] Hp · MaxHp (적)                            (로직 컴포넌트 제거 → 렌더러·애니·선택만)
  NetworkTransform ← 매 틱 실물 위치를 복사           겉모습이 NetEntity 위치를 따라간다(보간)
   │
실물 파괴(OnDestroy) → Runner.Despawn ──────────▶ Despawned() → 겉모습 파괴
```

- **호스트 쪽 NetEntity는 보이지 않는다**(렌더러 없음). 호스트는 실물을 본다.
- **발견은 「등록부 훑기」로 한다** — 생성 지점 9곳에 한 줄씩 넣지 않는다.
  호스트의 `NetMirrorHost`가 매 틱 `UnitIdentity.Active` · `EnemyDummy.Active` · 위습 등록부(새로 추가)를 훑어
  `NetLink`가 없는 것에 NetEntity를 붙인다.
  - 장점: RewardDistributor(PM)·WaveSpawner·SealSpawner… **남의 파일을 안 고친다**. 앞으로 생길 생성 경로도 자동으로 잡힌다.
  - 단점: 한 틱 늦게 나타난다(무시 가능). 같은 프레임에 생겼다 사라진 것은 안 비친다(무해).
  - 필터: `OwnedByPlayer`가 있는 유닛만(조합표 인형 등 장식 제외 — **구현 때 인형에 UnitIdentity가 붙는지 실측**).
- 위치 복사: 호스트 NetEntity가 `FixedUpdateNetwork`에서 `transform.SetPositionAndRotation(실물)` → NetworkTransform이 보낸다.

### 클라의 「겉모습」 만들기

1. 카탈로그 인덱스 → 프리팹. **비활성 부모 아래 Instantiate** 한다.
   ⚠️ Unity는 **꺼진 컴포넌트에도 Awake를 돌린다** — enabled=false로는 부족하다. 비활성 상태에서 로직 컴포넌트를 **제거**한 뒤 활성화한다.
2. **남기는 목록(화이트리스트)** 방식: Transform·렌더러·Animator·`CharacterAnimator`·`Selectable`·`SelectionIndicator`·`OwnedByPlayer`·`UnitIdentity`·`Wisp`·`UnitMover`·콜라이더(클릭 선택용)·`AttackRangeIndicator`,
   적은 `EnemyDummy`(복제 모드, §6) 추가. **그 밖은 전부 제거**하고 프리팹별 첫 1회 제거 목록을 로그로 남긴다(검증용).
   블랙리스트로 하면 새로 생기는 로직 컴포넌트가 클라에서 조용히 돈다.
   - `NavMeshAgent`는 `UnitMover`가 `RequireComponent`라 **끄기만** 한다.
   - `CharacterAnimator`는 에이전트가 꺼지면 이미 「위치 변화로 속도 재기」로 떨어진다(CharacterAnimator.cs:72~80) → 걷기 애니가 그냥 돈다.
3. `OwnedByPlayer.SetOwner(Owner)` · `UnitIdentity.SetData(data)` · **`UnitIdentity.RegisterTo(내 인벤토리)`** —
   클라의 `UnitInventory`가 자동으로 「내 유닛 복제본 목록」이 된다(겉모습이 생기고 사라지는 것이 곧 복제). UI가 인벤토리를 읽는 곳은 손 안 대도 된다.

## 4. 데이터 카탈로그 (`NetCatalog`)

- ScriptableObject 하나에 UnitData · EnemyData · WispData 목록. **에셋 GUID 순 정렬**(생성기가 다시 돌아도 .meta가 살아 있으면 순서 불변).
- 에디터 메뉴 `Tools/Net/카탈로그 다시 만들기`로 채운다(생성기처럼 트리를 스캔하지만 **카탈로그 파일에만 쓴다** — 로스터는 안 건드림).
- 접속 때 호스트가 카탈로그 **개수+해시**를 보내고, 다르면 클라가 「빌드가 다릅니다」로 접속을 거절한다(빌드 버전 어긋남을 조용히 틀린 유닛으로 보여 주지 않게).
- 네트워크로는 `short CatalogIndex`만 오간다.

## 5. 무엇을 어떻게 동기화하나

### 5-1. NetworkObject

| 대상 | 방식 | 비고 |
|---|---|---|
| 유닛 | NetEntity(Kind=Unit) | 소유자·카탈로그·위치 |
| 적(라인몹·보스·사이드보스·물범·해왕류 전부) | NetEntity(Kind=Enemy) | + Hp·MaxHp. 등록부 훑기라 생성 경로 무관 |
| 위습 | NetEntity(Kind=Wisp) | 소유자·카탈로그·위치 |
| 포탈 소모 | **동기화 대상 아님** | 위습이 호스트에서 포탈 트리거에 닿으면(UnitPortal:OnTriggerEnter, 이미 IsServer 가드) 위습 파괴 → Despawn, 유닛 생성 → 새 NetEntity. **이동 RPC만 있으면 저절로 된다** |
| 플레이어 | `NetPlayer`(접속자당 1, 입력권한=그 플레이어) | RPC 창구 + 플레이어별 [Networked] |
| 게임 전역 | `NetGameState`(호스트가 1개 스폰) | 라운드·타이머·데스카운트 등 |
| 씬 고정물(포탈·칸·창고·레인) | **동기화 안 함** | 모두가 같은 씬을 로드 = 같은 값(정적). 상태 있는 고정물(파괴 가능 문 `DestructibleGate` 등)은 2단계 |

### 5-2. [Networked] 상태 — 호스트가 **기존 시스템을 읽어서** 쓴다(기존 파일은 안 바뀐다)

| 위치 | 필드 | 출처(호스트가 읽는 곳) | 클라에서 쓰이는 곳 |
|---|---|---|---|
| NetPlayer | `Slot`(0~3) · `Gold` · `Wood` · `IsDead` · `Name` | PlayerContext.Get(slot)의 GoldWallet·ResourceWallet·IsDead | 클라의 같은 컴포넌트에 **복제 값 주입**(§6) → GameHud 무수정 |
| NetGameState | `Round` · `RoundTimer` · `DeathCount[4]` · `Difficulty` · `GameOver` | RoundManager·DifficultyManager 공개 프로퍼티 | HUD 라운드/타이머 표시 |
| NetGameState | `StoryIndex` · 스토리 진행 | StoryManager | 2단계 |
| NetEntity | `Hp` · `MaxHp` (적) | EnemyDummy | 체력바(HealthBarLayer) |
| NetEntity | `AttackSeq`(공격할 때마다 +1) | UnitAttacker/EnemyDummy 공격 이벤트 | 클라가 값이 바뀌면 `PlayAttack()` — 2단계 |

위습 개수·유닛 목록은 [Networked]로 안 둔다 — **오브젝트 자체가 복제**되니 세면 된다.

### 5-3. RPC — 클라 → 호스트 「요청」 (`NetPlayer`, 입력권한 → 상태권한)

**원칙: 선택은 로컬, 바꾸는 건 전부 요청.** 호스트는 받을 때마다 「요청자 슬롯 == 대상 소유자」를 검사한 뒤 **지금 싱글에서 쓰는 그 함수**를 부른다.
호스트 자신의 조작은 RPC를 안 타고 지금처럼 직접 실행된다(`IsServer`).

| 요청 | 인자 | 호스트 처리 | 단계 |
|---|---|---|---|
| 이동 | 대상 NetworkId(들) · 땅 좌표 | NavMesh 표본 → `UnitCombat.IssueMoveCommand` / `agent.SetDestination`(UnitMover와 같은 경로) | **1** |
| 정지(H)·모으기(V)·우리로(C) | NetworkId들 | `UnitCommands.ToggleHold/Gather/SendToPen` | 1(싸면) / 2 |
| 조합 | 누른 유닛 NetworkId · 조합 번호 | `CombineSystem.TryCombine` | 2 |
| 상점·도박·강화·도움소·해적퀘스트·항해일지·창고·아이템 | 가게 종류 · 항목 번호 · (대상 유닛) | 각 Shop의 기존 구매 함수 | 2 |
| 채팅 | 문자열 | GameChatBox → 채팅 언락 판정 | 2 |
| 난이도 선택 | — | 호스트만 고른다(이미 `IsHost`). 클라는 대기 화면 | 1(기존 동작 확인만) |

**호스트 → 특정 클라**(`RpcTargets.InputAuthority`): `Notify(string)` — `PlayerNotification.Show(playerId, …)`가 호스트에서 불렸는데
그 playerId가 원격이면 해당 NetPlayer로 넘긴다. 「이 포탈에 쓸 수 없습니다」 같은 안내가 이거 없으면 클라에게 안 보인다 → **1단계에 넣는다**.

배열 인자(NetworkId 여러 개)를 Fusion RPC가 그대로 받는지는 **SDK로 확인**. 안 되면 고정 길이 struct(예: 24칸) 또는 유닛당 1회.

## 6. 클라에서 「돌면 안 되는 것」과 복제 값 주입

- 판정은 이미 IsServer 뒤에 있지만 **가드 없는 Update가 남아 있다.** 1단계 완료 조건에 **「클라에서 도는 Update 전수 점검」**을 넣는다
  (방법: 씬 오브젝트의 MonoBehaviour 중 Update/코루틴이 있는 것 목록 → 클라에서 상태를 바꾸는지 하나씩).
- **복제 값 주입** — 클라의 GoldWallet·ResourceWallet 등에 `ApplyReplicated(int)`(이벤트는 그대로 발생) 같은 **작은 setter**를 단다.
  GameHud(3108줄)는 지금처럼 `PlayerContext.Local.GoldWallet.Gold`를 읽으면 된다.
- **적 체력바** — `EnemyDummy`에 복제 모드 한 줌: `IsReplica`면 Update 로직 건너뜀 + `SetReplicaHp(hp, max)`. `EnemyDummy.Active` 등록은 유지 → HealthBarLayer·MinimapBlips 무수정.
- **라운드 표시** — RoundManager(PM 소유)의 필드는 클라에서 멈춰 있다. HUD가 읽는 곳에 복제 값을 넣는 방법은 둘:
  (a) RoundManager에 `ApplyReplicated(round, timer)` 추가(**PM 파일**) · (b) HUD가 `NetGameState`가 있으면 그쪽을 읽게. → **PM 결정 필요**(§10-②).
- **클라의 HUD 조작 버튼**(조합·상점·도박…)은 2단계 RPC가 생기기 전까지 **클라에서 막는다** — 지금 누르면 클라 로컬 상태만 바뀌어 화면이 거짓말을 한다.
  `GameAuthority.IsServer`가 false일 때 한 곳에서 비활성 + 「2단계에서 열립니다」.
- DebugHud 치트는 클라에서 끈다.

## 7. LocalPlayer.Id · 레인 배정 · 「내 것」

- **슬롯 배정은 호스트가 한다**: 호스트 = 0, 접속 순서대로 비어 있는 가장 작은 번호. `NetPlayer.Slot`([Networked])으로 알린다.
- **로컬 ID는 한 군데로**: `FusionAuthorityProvider`가 슬롯을 받는 순간 `LocalPlayer.LocalPlayerId`에 **직접 쓴다**. UI 대부분이 LocalPlayer를 직접 읽으므로
  `GameAuthority.LocalPlayerId`와 값이 갈리면 안 된다(두 곳이 따로 놀던 것을 한 곳으로 묶음).
- **레인 = 슬롯**(WaveSpawner·LaneMarker·RtsCameraController가 이미 그렇게 쓴다). 2인이면 슬롯 0·1.
- **occupied**: 게임 씬 로드 **전에** 정해져야 한다 — `RewardDistributor.Start`가 씬 로드 즉시 `PlayerContext.Occupied`에 위습을 뿌린다.
  → 정적 `MatchConfig.OccupiedSlots`를 로비에서 채우고, `PlayerContext.Awake`가 `MatchConfig`가 켜져 있으면 그걸 따른다(꺼져 있으면 지금 직렬화 값 그대로 = 싱글 무변화).
- **판 시작 뒤 난입 불가**(세션 닫음). 재접속은 3단계.
- 「내 것」으로 나뉘어 있어야 하는 곳 — 이미 된 것 ✅ / 할 것 ⬜:
  ✅ 선택 필터 · 이동 소유자 검사 · 알림 큐 · 카메라 레인 · 난이도 선택 권한 · 발밑 고리 색(소유자)
  ⬜ HUD 숫자(§6 주입) · 인벤토리 UI(§3 겉모습 등록으로 해결) · 조작 버튼(§6 막기 → 2단계 RPC) · 알림 RPC(§5-3)

## 8. 접속 흐름

```
NetBoot 씬(새로, 빌드 세팅 0번 — mp 브랜치에서만)
  [호스트 만들기] [참가] 방 이름 입력 (OnGUI 수준, 우리가 짠다 — FusionMenu unitypackage는 안 들인다)
  → NetworkRunner.StartGame(Host / Client, SessionName)
  → 호스트: 접속마다 NetPlayer 스폰, 슬롯 배정 / 인원 표시
  → 호스트 [시작] → MatchConfig 확정 · 세션 닫음 · Runner.LoadScene(SampleScene)
SampleScene
  → 호스트: NetGameState 스폰, NetMirrorHost 가동 / 모두: 기존 게임 그대로
```

- **SampleScene을 그냥 Play하면 지금과 똑같은 싱글**이어야 한다(Runner 없음 → Provider null → IsServer true). gameshot·ClaudeBridge·판 측정 무영향 — **이게 설계의 제1 조건**.
- NetPlayer는 씬 전환을 넘어 살아야 한다(`Runner.MakeDontDestroyOnLoad` 류 — SDK 확인).

## 9. 단계별 계획

### 1단계 — 2인 시제품 (PM 지시 범위)

| # | 내용 | 완료 조건(도달로 증명) |
|---|---|---|
| 1-a | 연결: NetBoot 씬 · NetLauncher · `FusionAuthorityProvider` · 슬롯 배정 · MatchConfig/occupied · SampleScene 로드 | 호스트(에디터)+클라(빌드) 둘 다 게임 씬 진입. 클라 `LocalPlayerId=1`, 카메라가 레인 1, 호스트 로그에 occupied 슬롯 {0,1}. **SampleScene 직접 Play = 싱글 그대로** |
| 1-b | 거울: NetCatalog · NetEntity · NetMirrorHost(등록부 훑기) · 클라 겉모습(화이트리스트 제거) · 위습 등록부 | 클라 화면에 양쪽 위습 5+5가 보이고, 호스트가 쓴 유닛이 클라에도 같은 자리에 나타남. 겉모습 제거 로그 확인 |
| 1-c | 이동 요청 RPC · UnitMover 클라 경로 · 소유자 검증 | **클라가 자기 위습 5개를 포탈에 넣어 유닛 5기가 클라 레인 우리에 생김(양쪽 화면)**. 클라가 유닛 이동. 클라가 호스트 유닛 선택 불가 |
| 1-d | 적 동기화 · 체력바 · 최소 HUD(라운드·타이머·골드·목재) · 알림 RPC · 클라 조작 버튼 막기 · 클라 Update 전수 점검 | 두 레인에 적이 나오고 **죽는 순간이 양쪽 화면에서 같다**(같은 적의 소멸 시각 차 ≤ 왕복 지연). 라운드 번호 일치. 포탈 거절 안내가 클라에 뜸 |

측정할 것(1-d 끝에): 적 최다 시점 대역폭 · 틱레이트(기본값 → 필요하면 30) · 클라 체감 이동 지연.

### 2단계 — 한 판을 끝까지
조합·상점·도박·강화·창고·스토리 포탈·아이템·채팅 요청 RPC(GameHud 조작 경로 전부) · 공격 애니(AttackSeq)·투사체 이펙트 · 파괴 가능 문 등 상태 있는 고정물 · 스토리/사이드보스 HUD · 패배 화면 · 이름표/미니맵 확인.

### 3단계 — 친구 베타
4인 · 이탈/재접속 처리 · **PersistentSave(영속 세이브)는 각자 PC 파일**이라 접속 때 클라가 호스트로 보내야 한다(지금은 호스트 파일로 전 슬롯 판정될 위험 — 구현 때 확인) ·
대역폭 최적화(적은 경로가 정해져 있으니 위치 대신 「경로 진행도」만 보내기) · 배포용 빌드(mac/win) · MPM 1.6 도입 검토.

## 10. PM 결정이 필요한 것

1. **남의 파일에 작은 setter/훅**: GoldWallet·ResourceWallet(`ApplyReplicated`), EnemyDummy(복제 모드), UnitMover(클라면 RPC), PlayerContext(`MatchConfig` 읽기), Wisp(등록부),
   GameHud(클라 버튼 막기 한 곳). **모두 `mp` 브랜치에서**, 싱글 경로에서는 조건문 한 줄로 무동작. 소유자가 따로 있는 파일이면 알려 주십시오.
2. **라운드 표시**: RoundManager에 setter(a, PM 파일) vs HUD가 NetGameState를 읽음(b). 저는 (b)를 권합니다 — RoundManager를 안 건드립니다.
3. **빌드 세팅에 NetBoot를 0번으로** 넣는 건 mp 브랜치에서만. main 병합 시점은 PM 판단.
4. **테스트 방식**: 호스트=worktree 에디터(배치모드 아님, 별도 창), 클라=worktree에서 배치모드로 뽑은 macOS 빌드. MPM 설치(패키지 변경)는 안 합니다.
   worktree의 Library는 비어 있어 첫 임포트가 깁니다 — main `Library`(5.3GB)를 APFS 복제(`cp -c`, 읽기만)로 가져와 시간을 줄이려 합니다. 사장님 에디터·ClaudeBridge는 안 건드립니다.

## 11. 위험 · 모르는 것

- **Fusion 2.1.2 API 세부**(RPC 배열 인자, DontDestroyOnLoad, NetworkTransform에 스크립트로 위치 넣기, 씬 로드 API) — SDK 소스로 확인. 이 문서의 이름은 가설.
- **한 틱 늦은 발견**: 생기자마자 같은 프레임에 파괴되는 것은 안 비친다 — 이펙트성 오브젝트면 무해, 게임 오브젝트면 문제. 1-b에서 로그로 확인.
- **클라에서 가드 없이 도는 로직**: 전수 점검 전까지는 「화면이 거짓말」 가능성이 있다(1-d 완료 조건).
- **Physics 트리거가 클라 겉모습에도 있다** — 포탈 트리거는 IsServer로 막혀 있지만 다른 트리거(Story 존 등)도 가드 여부를 1-d 점검에 포함.
- **호스트 배속**(DebugHud 속도) — 호스트 시뮬만 빨라지고 클라는 결과를 본다. 동작은 맞지만 디버그 도구는 호스트 전용.
- **대역폭**: 레인당 적이 100마리 가까이 쌓이는 게임이다(데스카운트). 2인·NetworkTransform으로 먼저 재고, 넘치면 3단계 최적화를 앞당긴다.

---

## 12. 1단계 결과 (2026-09-26, 구현담당2)

커밋: 1-a `7fa81b14`·`8af60a77` · 1-a' `d6bb7b34` · 1-b `f95edecc` · 1-c `8d6427e8` · 1-d `ee03da10`·`63cb20df`·`8064c32a`(main bdd0f875 병합 포함)·지역 고정.
검증은 전부 **macOS 빌드 두 개를 명령줄 옵션으로 손 없이** 돌려 로그·덤프·캡처로 했다(`NetLauncher` 머리 주석의 `-mp*` 옵션).

| 항목 | 결과 |
|---|---|
| 접속·대기실 | 방 코드 5자 · 닉네임(기억) · 슬롯 4 · 준비 · 방장 난이도 · 전원 준비 시 시작 · 방장 이탈 알림 |
| 거울 | 서 있는 위습·유닛 좌표가 호스트·클라 소수점까지 일치, 회전까지 |
| 이동·명령 | 이동 · 정지 · 홀드 · 적 공격(우클릭/A) · 공격이동 · 모으기 · 우리로 — 전부 클라→호스트 RPC, 소유자 검사 |
| 위습→포탈→유닛 | 클라 위습 5 → 호스트 포탈 소모 → 클라 레인 우리에 유닛 5기 |
| HUD | 골드·목재·라운드·타이머·레인별 적 수·이름표가 클라에서 호스트 값 |
| 대역폭 | 2인·라운드 2(적 ~70, 유닛 18) 호스트 송신 약 25KB/s(≈200kbps), 클라 송신 약 2.4KB/s |

**설계와 달라진 것·새로 안 것**
- 유닛 프리팹엔 `UnitIdentity`가 없다(UnitSpawner가 소환 때 붙임) → 겉모습에도 붙여야 이름표·인벤토리가 산다.
- **Photon 지역은 고정해야 한다**(기본 kr, `-mpRegion`). 각자 핑 기준으로 고르게 두면 방장 jp·친구 kr로 갈려 `GameNotFound`.
- NetEntity 프리팹 첫 동기 스폰은 지연 로드 때문에 실패할 수 있다 → 다음 틱 재시도.
- `EditorSceneManager.NewScene(Single)`은 먼저 불러 둔 프리팹 참조를 죽인다(NetSetup).

**남은 것(2단계로)**
- 상점·도박·조합·강화·창고·항법·특성·채팅 요청 RPC(지금은 클라에서 막고 「다음 단계에서 열립니다」).
- 호스트만 바뀌고 안 오는 상태: 도박 해금(GamblingProgress), 막간 문(InterludeGate), 스토리 HUD, 파괴 가능 문.
- 투사체·타격 이펙트(지금은 공격 모션만).
- **간헐 끊김**: 판 중 호스트가 Photon Cloud 연결을 잃고 재접속 실패(10판 중 2판). `NetDiagnostics`가 이유를 찍는다 — 재현되면 원인부터.
- 사람 손 확인 필요: 실제 우클릭/단축키 경로, 공격 모션 재생, 알림 넘김(판에서 한 번도 안 불림).
