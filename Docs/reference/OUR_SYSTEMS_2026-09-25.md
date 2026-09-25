# 구랜디 — 현재 Unity 코드에 구현된 게임플레이 시스템 전수 목록 (2026-09-25)

범위: `Assets/Scripts/**` 112개 .cs(21,014줄) + `Assets/Editor/MapGenerator.cs`(5,942줄)·`MapLayout.cs`(839줄) + `Assets/Data/**` SO 에셋.
방법: 코드 정독과 소비처 grep만 했다. **플레이모드는 돌리지 않았다.** 상태 판정은 코드 기준이다.
「원작」 주석은 73개 파일에 766건 있다(UnitAttacker 90, SkillData 80, EnemyDummy 51, UnitData 45, RoundManager 37, GameHud 33 …).

상태 표기: **작동**(런타임 경로 연결됨) / **부분**(일부 분기만) / **스텁**(코드는 있으나 데이터나 호출자가 없어 불활성) / **데이터만**(필드는 있으나 런타임이 읽지 않음)

참고: `Docs/GDD.md`(v0.2)는 MVP 시절 문서다(25라운드, 4등급, 60/30/8/2, 골드 400, 뽑기 60골드). 현재 코드는 09-06 「전부 원작대로」 결정 이후 원작 이식판이라 **GDD 3장 수치는 전부 낡았다.** 비교는 코드 기준으로 할 것.

---

## 0. 맵 구조 (Editor/MapGenerator.cs, MapLayout.cs) — 작동
- 메뉴 `Tools/맵/원랜디 맵 생성`: SampleScene의 Map 루트를 새로 짓고 NavMesh를 구운 뒤 씬을 저장한다. `WorldScale.Value=4.167`(WC3 1 = 우리 1/4.167), NavMesh voxel 2.
- **4인 고정**: 레인 4, 창고 4, PlayerContext 4(`EnsureFourPlayers`). 시작 자원은 30엔 + 목재 1.
- **섬 18개**
  - Lane1~4: 2×2 격자, 781×690. 레인 사이는 벽이고 다리는 없다.
  - Warehouse1~4: 개인 창고섬.
  - SealIsland1~4: 물개 크립섬.
  - PunkHazard: 파괴 가능한 관문(DestructibleGate) 섬.
  - ImmortalDisplay·TranscendDisplay: 전시대.
  - StoryZone: 750×625.
  - GachaIsland: 위습 포탈섬.
  - CombineTable: 3D 조합표 판.
- **레인마다 있는 것**
  - `WaypointPath`: 트랙 사각형 4모서리를 도는 루프.
  - 유닛 우리: 9칸 칸막이(원작 `1com1~1com9`). 트랙까지 거리는 흔함 최소사거리의 0.85배(≈96.7).
  - 상점줄: 도박·연구소·다른세계연구·영원연구·지원·공격타입강화·해적퀘스트·항해일지(H0C4).
  - 스토리존 포탈.
- **가챠섬**
  - 위쪽 줄: 흔함 선택 부스.
  - 왼쪽 5칸: 안흔함 / 특수함 / 희귀·상위(보너스 3%) / 백수생활 특별지급줄 / 전설·히든.
  - 오른쪽: 랜덤유닛·다른세계 전시.
- **자원허브**(BuildResourceHub): 중앙에 위습이 생긴다. 포탈은 북=랜덤유닛(0.24% 해적선 h060), 동=엔, 서=목재, 남=지원마나.
- **기타 배치**: 해왕류 o02N(HP 36M, 정지), 보물찾기 구역, 매니저 오브젝트(PirateQuest·ChatUnlock·HiddenCombine·GameChatBox·SideBossManager·Difficulty).

## 1. 유닛 소환: 위습 → 포탈 — 작동
파일: `Units/Wisp, WispCell, UnitPortal, ResourcePortal, UnitSpawner, NavPlacement, DebugWispTrigger`, `Data/WispData, WispReward, GachaTable`
- **플레이어 입장에서**
  - 위습이 등급별 벽칸(WispCell)에 생긴다.
  - 위습을 선택해 우클릭으로 포탈 트리거에 넣으면 `OnTriggerEnter`(서버만)가 위습을 소모하고 `UnitSpawner.Spawn`을 부른다.
  - 유닛은 레인 우리 칸에 나오고, 보너스 뽑기로 나온 유닛은 레인 중앙에 나온다.
- **UnitPortal**
  - `acceptedGrades`, `specificUnit`, `rewardGrade` 덮어쓰기(랜덤유닛 위습 → 흔함).
  - 보너스 확률: 원작 「특수함(3%확률)」, 「랜덤 포탈의 1% 상붕카」.
  - **흔함 천장**: 원작 `Trig_Random_Base1` 「7번 랜덤 + 8번째 확정」. 8번째는 9종 중 가장 적게 나온 흔함을 주고, 플레이어별로 센다.
- **ResourcePortal**: 지급량 = `round(base + U(perRound, perRoundMax) × 라운드)`, 성공확률을 굴린다.
  - 원작 엔포탈 「15 + 라운드×12~35」, 목재 「66% 확률로 목재 1」.
  - 실패해도 위습은 소모된다.
- **UnitSpawner.Spawn**: 플레이어 유닛이 생기는 유일한 경로.
  - 스탯 적용, NavMesh 영역마스크(바다는 비행·수상보행만).
  - 회피 LowQuality r=0.28. UnitMover의 None과 충돌하며 문서화돼 있다.
  - UnitInventory 등록, 연합 항법 훅, 보물 훅.
- **GachaTable**: `RollFromGrade`만 쓰인다. **`Roll()`은 호출자 0**(데이터만). MainGachaTable 가중치는 흔함60/안흔함30/특수8/희귀2이고 나머지는 0이다.
- **WispData.isPlayerChoice**: 읽는 곳 0(데이터만).
- **DebugWispTrigger**: G키로 테스트 위습 1개. 씬에 배치돼 있고 「제거 예정」 표시가 있다.
- 위습 종류 에셋 9개.

## 2. 조합 — 작동(일반) / 스텁(히든)
파일: `Units/CombineSystem, HiddenCombineManager`, `Data/CombineRecipe(207 에셋), HiddenCombineData(0 에셋)`
- **일반 조합**
  - 트리거: 유닛을 선택하면 HUD 명령칸 4~15에 가능한 레시피가 뜨고, 클릭하면 `TryCombine`. DebugHud F2는 첫 가능 조합을 실행한다.
  - 재료: 특정유닛 → 등급와일드카드(시스템 유닛 제외) → 특정아이템(ItemInventory) 순으로 가져간다. 창고 유닛이 재료로 먼저 빠진다.
  - 조건: `goldCost`, `resourceCosts`, `minRound`/`maxRound`, `requiredSaveCount`(영원함, 원작 `udg_Load_PlayCount` 5·10…35).
  - 결과: 레인 중앙에 나온다(사장님 09-24).
  - 부수효과: 초월함 결과면 지원건물 H0B7 선행조건을 켜고, `damageLevelFixedBonus`를 더한다(히든_성탄 +2만).
- **히든 조합**(채팅 암구호): 코드는 완성됐지만 **HiddenCombineData 에셋이 0개**라 일치하는 게 없다. 재료 `unit`도 설계상 비어 있어 에셋이 있어도 실패한다. **스텁.**
  - 코드 경고: Hidden_Aokiji +2가 두 경로로 이중 가산될 위험.

## 3. 선택·이동·명령·배치 — 작동
파일: `Units/SelectionManager, Selectable, SelectionIndicator, WorldPick, UnitMover, UnitCommands, UnitCombat(홀드), LaneMarker, NavPlacement, OwnedByPlayer, LocalPlayer, PlayerColors, AttackRangeIndicator`
- **선택**: 좌클릭 또는 드래그 박스, 내 유닛만, 최대 12(「원작·워크3와 동일」).
- **이동**: 우클릭. 목적지는 8×Scale 안에서 샘플하고, 도착할 때까지 자동추적을 멈춘다.
- **단축키**: H 홀드, V 모으기(같은 이름 유닛을 육각 링으로 순간이동, 원작 모으기), C 우리로. 채팅이 열려 있으면 막힌다.
- **배치**: 자유 이동. LaneMarker가 흔함 종류마다 고정 칸을 주고(중복은 같은 점에 겹침), 나머지는 다음 빈 칸에 둔다. 칸 카운터는 줄지 않는다.
- **HUD 명령칸 0 「공격」은 미배선.**
- 순수 시각: SelectionIndicator(등급색 링), AttackRangeIndicator.

## 4. 전투·피해·스킬·특성 — 작동(부분 누락)
파일: `Units/UnitCombat, UnitAttacker(1920줄), UnitIdentity, DamageLevelFixedState`, `Data/UnitData(243), SkillData(387), UnitTraitData(239), DamageTable(1), SelfUpgradeAbilityData(0)`
- **UnitCombat 상태기계**
  - 상태: Idle / Chasing / Returning / PlayerMoving / Holding. 0.25초마다 스캔한다.
  - 탐색거리 = max(aggro 18, 사거리). **aggroRange 18은 최소사거리 30에 늘 져서 무효.**
  - 무적 스토리건물도 일부러 친다(「미리 깎아두는 것이 설계다」).
- **평타 흐름**
  - 방깎·슬로우 특성 → 물리평타 → Bash 치명 → 적중시 스킬.
  - 사거리 안에 적이 없으면 DestructibleGate(5000HP)를 친다.
- **공식**
  - 공격력 = (base + 주스탯×800 + 고정버프) × (1+특성피해) × 파워버프 + 연구 고정보너스.
  - 공속 = 연구 × 공격타입트랙 × (1+AGI×0.01) × 스킬버프.
- **영웅 XP**: 레벨 1~24, 표 33/75/116/152… 레인 킬마다 초월·영원 유닛 +1(원작 `war3map.j:14734`).
- **스킬**: 유닛당 최대 6개.
  - 발동: OnHitChance / CooldownAutoCast / Aura / OnHitCount, 마나·생명 게이지.
  - 처리하는 효과: Damage, Stun, ArmorBreak, ArmorBonus, HoT, Buff 적용·해제, Aegr/Aisr/A11S 스택, 공격력·공속 버프.
  - **`ExtraProjectile`은 조용히 무시한다**(UnitAttacker:1544).
- **자기강화(비비 A0LZ)**: `TryUpgradeSelf` 호출자 0, 에셋 0. **죽은 코드.** 그래서 `CasterSelfUpgradeLevel` 기반 스킬은 항상 0이다.
- **특성(특성강화)**: 특성포인트로 산다(기본 2).
  - 읽히는 것: DamageIncrease·SlowOnHit·ArmorShred·MagicArmorShred·스킬해금·스킬교체·변신·영웅XP·스탯·반복구매·우솝.
  - **안 읽히는 TraitEffectKind 9종**: Summon, MovementAbilityGrant, StatusAilment, DamageTypeChange, MechanismChange, UtilityBuff, AreaDamage, AccuracyIncrease, CastMethodChange. `specialEffectId`도 안 읽힌다(데이터만).
  - 메모리 기록: 원작 특성은 최상위 26명 전용인데 에셋은 239개다.
- **상성표(DamageTable)**: 행 normal/pierce/siege/hero/chaos/magic/spells × 열 large/fort/normal.
  - 방어 공식은 WC3식이지만 상수 0.02(0.06 아님), 하한 −20.
  - AP 스킬 피해 = UNIVERSAL(방무).
  - **`RowMatches`가 늘 true라 EnemyDummy의 불일치 경고 분기는 도달 불가.**
- **UnitData**
  - `hp`는 HUD 표시용뿐이다(아군엔 HP가 없고 적도 아군을 공격하지 않는다).
  - 물리 199종 대부분 `attackType`=Unassigned(배율 1.0).
  - `UnitGambleOption.abilityId`는 꼬리표뿐이다.
  - `hasRobinWingBlessing`은 표시만 하고, `MovementAbility.Teleport`는 필드만 있다.
- **등급 분포(243)**: 흔함11 안흔함15 특수33 희귀44 히든22 전설33 제한9 초월25 불멸8 영원8 랜덤유닛14 다른세계9 상위6 초월위습2 변화됨4.
- **DamageLevelFixedState**(원작 `udg_Damage_level_Fixed`): A11S 레벨 = 14 + 이 값. 항법·조합·히든조합이 올리고 WaveSpawner:160이 읽는다. 헤더 주석 「아무도 안 부른다」는 낡았다.

## 5. 적 — 작동
파일: `Units/EnemyDummy, EnemyAuraCaster, DestructibleGate`, `Waves/WaypointMover, WaypointPath`, `Data/EnemyData(105: Enemies 98 + 해적퀘 미니보스 7; 보스 25)`
- **이동**: 웨이포인트 사각 루프를 무한히 돈다. 슬로우 하한 0.01, 이속 하한 220/522×speed.
- **적은 아군을 공격하지 않는다.**
- **EnemyDummy**
  - 체젠, 빙결·슬로우, 방깎·마방깎.
  - Aegr/Aisr/A11S 스택(A11S = 0.20 + 0.05×lv, 상한 23).
  - 무적 두 종류(1HP 하한 / 진짜 무적).
  - 사망 시 레인 주인에게 엔(25%), 레인에 XP, 보스킬 이벤트.
  - `RemoveInstantly`는 보상 없이 없앤다.
- **EnemyAuraCaster**: `auraSkills`를 런타임에 붙인다. 적끼리 버프만 준다(A153 방어, A11T 재생). 스킬 레벨은 항상 1(부분).
- **DestructibleGate**: 5000HP. 부서지면 가라앉아 길이 열린다.

## 6. 라운드 진행·패배 — 작동
파일: `Waves/RoundManager, WaveSpawner`, `Data/WaveData(75 에셋, 스폰엔트리 78)`
- **시작**: 호스트가 난이도를 고르기 전에는 무한 대기(원작 `InitTrig_Select1`).
- **대기 시간**: R1 전 21초, R60(신세계) 전 40초.
- **라운드 길이**: R1 40.65초, R2~39 40.67초, 보스(R<61) 75.4초, R40~60 38.67초, R61~75 36.67초.
- **총 라운드**: 쉬움50 / 보통60 / 그 외 75.
- **웨이브 구성**: 예) R1 = 35마리 @0.65초. 보스 라운드는 R10/20/30/40/50/60/65/70/75.
- **패배(플레이어별, 레인 기준)**
  - 레인 적 ≥70이면 데스카운트(9부터)가 0.65초마다 −1. 회복되지 않는다(누적).
  - R60에 전원 카운트가 1로 바뀐다.
  - 0이 되면 엔 0, 유닛 소멸, 레인 적 무보상 제거. 전원이 죽으면 게임 끝.
  - 보스 제한시간: 구세계 75.30초 / 신세계(R65+) 34.80초를 넘기면 그 레인 플레이어 패배. 쉬움은 구세계 보스 면제.
  - R41 문턱: 지옥60 / 신55 / 악몽50. 스토리클리어 소거 체크는 **0으로 꺼져 있다**([미확인]).
- **보스 라운드 시작 시 전원 엔 0**(원작 `Trig_Enemy_Boss_create`).
- **클리어**: 마지막 라운드를 넘기면 `FinishPersistentSave(cleared)`.

## 7. 보상·화폐(엔·목재·위습·마나·토큰) — 작동
파일: `Units/GoldWallet, ResourceWallet, RewardDistributor, PlayerContext`
- **시작 지급**: 30엔 + 목재1, 랜덤위습 5, 시작 특수유닛, 특성포인트 1, 세이브 문턱 보상.
- **마나**: 시작 15 / 최대 1000 / 초당 0.30(원작 h08A). Token·LuckyToken도 있다.
- **라운드 보상**: 라운드마다 랜덤유닛 위습 2(`roundRewardCount=2`). 보스 웨이브에는 `wispRewards` 안흔함 1이 붙는다. 09-25에 흔함선택 이중지급을 제거했다.
- **킬 엔**: 25% 확률, 레인 주인만. `floor((1+2⌊r/5⌋+3⌊r/6⌋−⌊r/10⌋)×(2+GoldPlus))`. 원작 「확정 지급이 아니라 25%」.
- **보스 보너스(R10~60)**: 600/1, 1500/2, 2500/2, 3000/3, 3000/4, 4000/3(엔/목재). 원작 `udg_Level<62`.
- **보상 라우팅**: 물개처럼 `rewardsAllPlayers`는 전원, `rewardsKillerOnly`는 킬한 사람만.
- **스토리 보상**: 7번은 고대선, 12번은 특성포인트.
- **PlayerContext**: 지갑·인벤·창고·업글·세이브·항법·DLF·리롤·아이템을 묶는 플레이어별 허브.

## 8. 도박(유닛/엔) — 작동
파일: `Units/GamblingShop, GamblingProgress, UniqueRerollAbility, UniqueRerollState`, `Data/GamblingOptionData(6), UniqueRerollAbilityData(1)`
- **엔 도박**
  - 10엔: 64%로 7~50엔, 실패 시 3~5엔, 10회 제한.
  - 500엔: 59%로 500~4500엔. R10 보스를 잡으면 열린다.
  - **툴팁 불일치 의심**: 「0엔이 나올 수도」라고 쓰는데 10엔 옵션의 실패값은 3~5엔이다.
- **유닛 도박**
  - 하급 250엔+목재1: 85%, 흔함·안흔함, 보너스 2%.
  - 중급 1500+2: 70%, 특수, 보너스 3.5%.
  - 고급 2500+4: 70%, 희귀·특수. 실패 시 럭키토큰+목재.
  - 다른세계 3500+5: 17%. 실패 시 럭키토큰+목재.
- **슬롯2**: 특성포인트 1 = 15000엔, 1회.
- **희귀함 리롤(A0VX)**: 도박으로 얻은 희귀함에 붙는다. 목재 2, 2회(도박사 항법 3회), 실패 20%(도박사 0%).
- **HUD 유닛별 도박 버튼**(`UnitData.gambleOptions`, 예: 고대선): 목재를 쓰고, 성패와 관계없이 시전 유닛을 소모한다.

## 9. 업그레이드(연구소) — 작동(일부 등급 잠금)
파일: `Units/UnitUpgrades, UnitUpgradeShop, AttackTypeUpgradeShop, UsoppDockhouseTrait, ILaneShop`, `Data/UnitUpgradeTrackData(11), AttackTypeUpgradeTrackData(4)`
- **등급별 연구**
  - 비용: `costBase`, 이후 레벨마다 `+growth`.
  - 공속 배율 `1+L1+g(L−1)`, 고정 공격력 보너스.
  - 예) 불멸: max 21, 75→+50엔, 공속 2.10 + 0.06/lv, 공격력 +30000 + 2500/lv.
  - 흔함·안흔함·영원·다른세계는 `hasOriginalResearch=0`이라 잠겨 있다.
  - 09-06에 피해 배율을 공속 배율로 정정했다.
- **공격타입 강화**: 일반/공성/관통/패기, 최대 3레벨, 3000엔+목재500, 공속 +3%/lv(패기 4%).
- **특성포인트 출처**: 시작, 15000엔 구매, 스토리12, 피카 퀘스트, 세이브 300/600/900.
- **UsoppDockhouseTrait**: 지원건물 강화 전역 플래그.

## 10. 지원 상점(마나 스킬) — 작동
파일: `Units/SupportShop`, `Data/SupportSkillData(12)`
- 버스터콜500, 출항이다150, 흡수100, 불비210, 폭우25, 대지진320, 지진200, 독약620, 해루석700, 낙뢰100(22,500 피해 + 2초 스턴, 쿨 17초).
- 선택위습제조: 80마나 + 특성1, 3회.
- 능력치증가: 2500엔, 4회.
- 즉사는 보스·스토리·특수 대상에게 막힌다.

## 11. 항법(패왕·연합·도박사·지원강화·지원잠금, 1회 선택) — 작동
파일: `Units/NavigationState` (원작 `Trig_onedill_Tech_Actions`). 선택 모달은 GameHud에 있다.
- 패왕: DLF +2, 채팅언락 조건.
- 연합: 상위 이상 유닛당 보너스 위습.
- 도박사: 리롤·도박.
- 지원강화 / 지원잠금: 아이템풀 축소.

## 12. 아이템 — 부분
파일: `Units/ItemInventory, ItemEffectApplier, ItemGambleState, VoyageLogShop`, `Data/ItemData(38), ItemGamblePoolData(1)`
- **획득 경로**
  - 항해일지(H0C4)에서 메타몽 H0BS(5000엔+목재3)를 산다.
  - 그 유닛을 팔면 아이템 뽑기가 된다(전체풀 22, 지원잠금 시 13, 이미 뽑은 것 제외).
  - 재고는 0에서 시작해 R6에 1, R9에 2. 원작 「6/9라운드 스토리 클리어시」를 라운드 번호로 근사한 것이다.
- **효과 적용**: 공격력%·공속%·마나젠만. HP젠과 StackDamage%는 경고만 낸다(「10건 중 6건만 건다」).
- **데이터만**: `ItemData.hasGrade/grade/linkedAbilityId/sellWoodMin·Max/designNote`, `ItemGambleState.AddStock/HasDrawn`(외부 호출 0).
- 아이템은 조합 재료로도 쓰인다(CombineSystem SpecificItem).

## 13. 창고 — 작동
파일: `Units/Warehouse, WarehouseController`
- B키로 선택 유닛을 개인 창고섬으로 보내거나(황금각 나선) 레인 중앙으로 되돌린다.
- 용량은 0 = 무제한이고, 한도는 미정이다.

## 14. 스토리 — 작동
파일: `Waves/StoryManager`, `Units/InterludeGate, StoryZonePortal, StoryReturnPortal`, `Data/StoryData(13)`
- 13개가 순서대로 진행되고, 첫 스토리는 게임 시작 10초 후에 나온다.
- 무적 건물로 있다가 다음 5의 배수 라운드에 보스가 된다. 처치 시 전원에게 보상을 준다(180 → 10000엔).
- 스토리09 앞에는 300초 「백수생활」 막간이 있다. 그동안만 InterludeGate가 열리고, 특별지급줄이 활성화된다.
- 포탈이 유닛을 스토리존으로 보내고 되돌린다.
- 스토리 번호는 우리 것이고 원작과 무관하다(주석 반복).
- **데이터만**: `IsTransformed`(외부 소비 0).

## 15. 난이도 — 작동
파일: `Waves/DifficultyManager`, `Data/DifficultyMode`(enum, 에셋 없음), `UI/DifficultySelectHud`
- 6종(원작 Mode_int 1~6). 호스트만 6버튼 창을 보고, 나머지는 대기문구를 본다.
- **PlayerPrefs에 기억해 다음 실행에 자동 적용한다.** 원작과 다른 테스트 편의다.

| 모드 | 라운드 | 몹HP+% | 보스HP% | 이속 | Aegr 오프셋 | R41 문턱 |
|---|---|---|---|---|---|---|
| 쉬움 | 50 | 0 | −15 | 1.00 | 0 | – |
| 보통 | 60 | 10 | 0 | 1.00 | 0 | – |
| 어려움 | 75 | 50 | +200 | 1.097 | 0 | – |
| 지옥 | 75 | 100 | +450 | 1.193 | −5 | 60 |
| 신 | 75 | 140 | +725 | 1.29 | −5 | 55 |
| 악몽 | 75 | 150 | +725 | 1.29 | −10 | 50 |

- 구간 보너스: R15~29 / 31~49 / 51~59 / 61~75. R39는 항상 1배.
- **데이터만**: `magicMultiplier`, `sideBossExcluded`(SideBossManager가 어려움을 하드코딩), `isNightmare`.

## 16. 해적 퀘스트(미니보스) — 작동
파일: `Units/PirateQuestManager, PirateQuestShop`, `Data/PirateQuestData(7)`
- 10엔, 재고 1, 재입고 360~3600초. 레인 중앙에 미니보스를 소환하고 60초 제한이다.
- 성공 보상은 연 사람에게 준다. 실패하면 라운드 위습 보상 2라운드 차단.
- 7종: 배고픈황정기 2000+위습 / 박성호 3000, HP +50%/회 / 신림패거리 1500 / 조규룡(R1~39) 위습 / 허브수경비원(R1~30) 위습 / 김선우 위습 / 이영용 특성1.
- **데이터만**: `storyDamage`(배선은 됐지만 전 에셋이 0).

## 17. 보물찾기 — 작동
파일: `Waves/TreasureHunt`
- R10~60의 10단위 라운드마다 숨은 상자 7개를 둔다(전설 나미가 있으면 9개).
- 탐색: 항해일지 슬롯2. 사거리 30, 쿨 100초(도구 소지 70초), 나미 34.5.
- 보상은 위습(나미면 ×2)이고, 나미는 GoldPlus +0.6.
- 팀이 9개를 찾으면 전원 목재2 + 세이브포인트2.

## 18. 해왕류·물개 크립 — 작동
파일: `Waves/SeaKingSpawner, SealSpawner`
- **해왕류 o02N**: HP ×1.5, 1회. 처치 시 전원에게 3000엔 + 위습1 + 세션포인트1. 원작의 1/6 아이템 드롭은 일부러 뺐다.
- **물개**: 3단 순차 체인(물개 → 노루 → 양), 리스폰 없음(「원작은 리스폰 루프가 아니라」).

## 19. 신세계 사이드보스(도플·빅맘·카이도) — 작동(씬 배선 의존)
파일: `Waves/SideBossManager`, `Units/SideBossEncounter`, `UI/SideBossBarLayer`
- R62/66/71, 레인 15번째 스폰 때 살아 있는 플레이어마다 1기(15 vs 16은 미확인). 어려움 제외.
- 시전 중 무적. 시전바는 0.2초마다 +5.
- 스턴 게이지가 2 미만이면 시전이 취소되고 잡을 수 있게 된다. 100에 도달하면 광폭 몹을 소환한다(B06B).
- 정산: 다음 보스 HP × (0.85 + 0.15 × 남은HP%).
- `boss62`만 SampleScene에 할당돼 있다. **66·71은 씬에서 확인할 것.**

## 20. 채팅 코드 / 해금(=밴 시스템 자리) — 스텁
파일: `Units/ChatUnlockManager, ChatInputGate, GameChatBox`, `Data/ChatUnlockData(0 에셋)`
- Enter로 「코드:」 입력창을 연다. 입력은 ChatUnlock → HiddenCombine 순으로 매칭하고, 없으면 「인식할 수 없는 코드」를 낸다.
- 규칙
  - 영원/영구/불멸 카테고리는 패왕 항법이 필요하고, 한 판에 1회 공유(원작 `udg_Tech_Onedill`).
  - 영구는 세이브 클리어수가 필요하다.
  - 목재를 쓰고 DLF를 가산한다.
- **`Assets/Data/ChatUnlocks` 폴더 자체가 없다**(MapGenerator:1161이 이 폴더에서 로드). 주석 기대치는 47개(영원28·영구8·IM11)다.
- `HasNikaPrerequisite`는 항상 false다.
- **밴 시스템(악몽 전용 14개 무작위 영구잠금)은 코드가 없다.** `-save` 같은 채팅 명령도 없다.

## 21. 영구 세이브 — 부분
파일: `Units/PersistentSave`, `Data/PlayerSaveData`
- `persistentDataPath/Save/player_{id}.json`에 JSON으로 저장한다.
- 세션포인트: 보스·크립·해왕류·보물 + 클리어 보너스.
- 로드 문턱 보상: 10→10엔, 100→목재1, 300/600/900→특성1.
- 클리어 시에만 저장한다. 패배 시 저장 불가는 원작대로이고, 중도 종료 시 저장 호출자는 0이다.
- **데이터만**: `playerLevel`. `bestRunPoint`는 쓰기만 한다.

## 22. 멀티플레이 — 스텁
파일: `Net/GameAuthority`
- `IAuthorityProvider`와 `IsServer` 가드가 전 시스템에 깔려 있다.
- **`GameAuthority.Provider`는 어디서도 대입되지 않는다** → IsServer는 항상 true(사실상 싱글).
- `Assets/Photon` 폴더(Fusion SDK)는 있지만 Scripts에서 Fusion 참조는 0이다.
- `LocalPlayer.Id=0` 고정. 4인 PlayerContext는 존재한다.

## 23. UI — 작동
파일: `UI/GameHud(3102줄, uGUI를 코드로 생성), DebugHud, DefeatOverlay, DifficultySelectHud, HealthBarLayer, UnitNameplateLayer, SideBossBarLayer, MinimapCamera, MinimapBlips, MinimapViewportIndicator, PlayerNotification, RtsCameraController`
- **GameHud**
  - 하단바 22%: 미니맵 / 선택정보(초상 + 12카드) / 4×4 명령칸.
  - 상단바: 엔·목재·라운드·남은시간, 항법·메뉴·동맹·채팅 버튼.
  - 스토리 줄.
  - 팀 패널(멀티보드 4인).
  - 아이템 8칸.
  - 단일 선택 시 특성·도박·판매·리롤 버튼.
  - 위습 슬롯(종류별 개수, 클릭하면 이동+선택).
  - 상점 타게팅.
- **DebugHud**: F1 정보창, F2 첫 조합(치트).
- **카메라**: WASD·화살표·가장자리 스크롤, 휠 줌(12~1750), Space·Home으로 내 레인. 미니맵 클릭으로 이동.
- **입력**: Input System 폴링. `InputSystem_Actions.inputactions`는 템플릿 그대로이고 쓰지 않는다.

## 24. 순수 시각(게임플레이 무관)
`Units/CharacterAnimator, DollIdle, MagicCircleSpin, FireLightFlicker`, `UI/SeaScroll`, `Data/WorldScale`(상수 4.167)

---

## 데이터 에셋 수 (`Assets/Data/**`, m_Script guid로 분류)
- **내용 있는 타입**: SkillData 387(유닛385+적2) · UnitData 243 · UnitTraitData 239 · CombineRecipe 207 · EnemyData 105 · WaveData 75 · ItemData 38 · StoryData 13 · SupportSkillData 12 · UnitUpgradeTrackData 11 · WispData 9 · PirateQuestData 7 · GamblingOptionData 6 · AttackTypeUpgradeTrackData 4 · GachaTable 1 · DamageTable 1 · ItemGamblePoolData 1 · UniqueRerollAbilityData 1.
- **에셋 0개**: ChatUnlockData, HiddenCombineData, SelfUpgradeAbilityData.
- **에셋이 아닌 타입**: DifficultyMode(enum), WispReward·PlayerSaveData(직렬화 클래스).

## 「데이터엔 있는데 런타임이 안 읽는다」 모음
1. `GachaTable.Roll()` (RollFromGrade만 쓰임)
2. `WispData.isPlayerChoice`
3. `DifficultyModeData.magicMultiplier / sideBossExcluded / isNightmare`
4. `PlayerSaveData.playerLevel`, `bestRunPoint`(쓰기만)
5. `ItemData.hasGrade / grade / linkedAbilityId / sellWoodMin·Max / designNote`. ItemEffect 중 HP젠·StackDamage%
6. `ItemGambleState.AddStock / HasDrawn`
7. `StoryManager.IsTransformed`
8. `PirateQuestData.storyDamage`(전부 0)
9. `UnitTraitData.specialEffectId` + TraitEffectKind 9종
10. `SkillEffect.ExtraProjectile`, `CasterSelfUpgradeLevel` 기반(자기강화 죽음)
11. `UnitCombat.aggroRange`(최소사거리에 가려 무효)
12. `DamageTable.RowMatches`(항상 true)
13. `UnitPortal.spawnPoint`(맵 생성이 null로 덮음, 09-05 의도)
14. `UnitData.hp`(표시만), `UnitGambleOption.abilityId`, `MovementAbility.Teleport`, `hasRobinWingBlessing`(표시만)
15. `GameAuthority.Provider`(미대입)
16. RoundManager `hellRound41ClearGateOrder / godNightmareRound41ClearGateOrder`(=0, 꺼짐)
17. HUD 명령칸 「공격」 미배선, `InputSystem_Actions` 미사용

## 파일 분류 확인: 112 / 112
- Data 25: 전부 위 시스템에 들어갔다. 아래 숫자는 파일이 속한 §번호다.
  - AttackTypeUpgradeTrack 9, ChatUnlock 20, CombineRecipe 2, DamageTable 4, DifficultyMode 15, EnemyData 5, GachaTable 1, GamblingOption 8, HiddenCombine 2, ItemData 12, ItemGamblePool 12, PirateQuest 16, PlayerSave 21, SelfUpgrade 4, SkillData 4, StoryData 14, SupportSkill 10, UniqueReroll 8, UnitData 4, UnitTrait 4, UnitUpgradeTrack 9, WaveData 6, WispData 1, WispReward 1, WorldScale 24.
- Net 1: §22
- UI 13: §23, SeaScroll §24, SideBossBarLayer §19, DifficultySelectHud §15
- Waves 10
  - RoundManager·WaveSpawner §6
  - WaypointMover·WaypointPath §5
  - DifficultyManager §15
  - SeaKing·Seal §18
  - SideBossManager §19
  - StoryManager §14
  - TreasureHunt §17
- Units 63
  - §1: Wisp, WispCell, UnitPortal, ResourcePortal, UnitSpawner, NavPlacement, DebugWispTrigger
  - §2: CombineSystem, HiddenCombineManager
  - §3: SelectionManager, Selectable, SelectionIndicator, WorldPick, UnitMover, UnitCommands, LaneMarker, OwnedByPlayer, LocalPlayer, PlayerColors, AttackRangeIndicator
  - §4: UnitCombat, UnitAttacker, UnitIdentity, DamageLevelFixedState
  - §5: EnemyDummy, EnemyAuraCaster, DestructibleGate
  - §7: GoldWallet, ResourceWallet, RewardDistributor, PlayerContext
  - §8: GamblingShop, GamblingProgress, UniqueRerollAbility, UniqueRerollState
  - §9: UnitUpgrades, UnitUpgradeShop, AttackTypeUpgradeShop, UsoppDockhouseTrait, ILaneShop
  - §10: SupportShop
  - §11: NavigationState
  - §12: ItemInventory, ItemEffectApplier, ItemGambleState, VoyageLogShop
  - §13: Warehouse, WarehouseController
  - §14: InterludeGate, StoryZonePortal, StoryReturnPortal
  - §16: PirateQuestManager, PirateQuestShop
  - §19: SideBossEncounter
  - §20: ChatUnlockManager, ChatInputGate, GameChatBox
  - §21: PersistentSave
  - §24: CharacterAnimator, DollIdle, MagicCircleSpin, FireLightFlicker
  - UnitInventory(유닛 레지스트리, §7/§12 보조)
- 합계: 25+1+13+10+63 = **112**
