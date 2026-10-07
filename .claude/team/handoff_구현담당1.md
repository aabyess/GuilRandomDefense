# 구현담당1 인수인계 (10-07 밤 마감, 0.3.15 배포 뒤)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md` → TEAM_RULES 순으로 읽는다. 전부 main에 커밋됨(푸시는 PM). 에디터·빌드 사본은 비워 두었다.

## 오늘(10-07 오후~밤) 끝낸 일 (해시는 git log)
- **MP 3차 점검**(Docs/research/MP_CHECK3_2026-10-07.md): 마나 스킬·범퍼·보잡 배율(보스 1.30)·순간이동·노획물 180초 ✅ · 회유 실전투는 두 번 다 회유 유닛이 안 생겨 못 쟀음(원인 미규명, 10-07 오전엔 생겼음). MP 클라는 지점 지정·대상 지정 액티브를 못 쓴다(구현담당2가 RPC 작업).
- **광폭화 유닛**(설계 Docs/design/BERSERK_UNIT_DESIGN_2026-10-07.md): WaveSpawner(확률 10%·R61+·레인당 라운드당 최대 1·`BerserkMode.Chance/FixedTime`(25초)·이속 ×1.0·크기 `berserkLaneMobScale` 2.0·왼쪽 변 바깥 오프셋 45·회복 1%/초·방어 오라 +5) + `BerserkMob`/`BerserkLook` + `EnemyDummy.DisplayName/NameOverride/AddRegenPercentBonus`(같은 오라는 최댓값 하나) + `WaypointMover.SetShuttleLeftEdge` + `NetEntity.Berserk`(클라 이름·붉은 표시). 시험: `-mpTestBerserk 초`(NetBerserkTest, 로그 [BZK]). 확률 100% 덮어쓰기 `WaveSpawner.BerserkChanceOverride`(시험 전용 static).
- **바다 A안**(3764cdc0f), **보스 목표 키**(4bf18aca8): `EnemyData.bossTargetHeight` 96 + `bossModelHeight`(ArtBinder가 표에서 씀) → `WaveSpawner.BossScaleFor`. R70·R75·사이드보스는 자리표시 모델이라 목표 0 = ×1.6 유지.
- **퇴치 의뢰 7종**(41feeb923, 두 창 실측 46bac4276): 🔴 미니보스 프리팹이 MobPrefab 루트가 아닌 자식을 가리켜 의뢰가 한 번도 작동한 적 없었다 → 고침. 해금 플래그(`PlayerContext.QuestStoriesCleared/QuestBossRoundsKilled`), 체력 ×1.5(바제스 제외)·반복 강화 표, 레인 순회, 확률 아이템·모리아 경험치, 타이머 창 `QuestTimerPanel`(+`NetPlayer.QuestTimerText`). prefab 루트 전수 검사: 372건 정상.
- **좆돼지·볼보이 통합**(fed0c8d74): 압살롬·좀비 제거(스킨은 Assets/Art/보관_원작스킨/), 박진웅 평타 10% → 볼보이(안흔함_이호준) 소환 10초(`UnitData.summonOnHit*`), 좆돼지 도박(500엔+목재1·50%·8R·실패 토큰 1), 행운의 토큰 사용(원작 Trig_Token: 3개 → 80% 특별함/20% 희귀함 위습, 해적단 쪽 7번 칸).

## 남은 일 / 사장님 답 대기
- **볼보이 조합 확률**: 박진웅 1기면 볼보이 3기 동시 ≈17%, 2기 ≈55%(평균 살아 있는 수 1.44/기) — 수명 10초·상한·확률을 바꿀지 사장님 확인 대기(에셋 값만: 박진웅 summonOnHit*).
- **미니보스 모델 미정**: 퇴치 미니보스 7종이 모두 같은 자리표시(MobPrefab)라 라인몹과 구분 안 됨 — 사장님께 PM이 묻는다(표 21번).
- 감사표 미반영: 저항 피부(15)·마법 방어(16)·바제스 약점(20, 로스터 밖)·모델(21).
- 퇴치 의뢰 실측 못 한 것: 확률 10%·실제 R61 자연 발생(시험은 확률 100% 덮어쓰기), 광폭화 MP 실전투.
- 제안값(사장님 확인 대기, 어제부터): 임채민 발동 25%·천벌 마나 120 / 유물 중복 대체 금화 3,000 / 스토리 건물 ×0.7 / 모리아 등급 표기. 임채민 특성강화 3pt 실클릭 확인 안 함. 회유 실전투 결정적 시험 · 「우클릭 십자」 재현 정보 대기.
- 도박소 칸 위치: 행운의 토큰 사용이 해적단 쪽 7번 칸(9칸 포화) — 옮기라 하면 `GamblingShop.TokenSlot`.

## 위치·도구
- 광폭화: WaveSpawner(상수는 [SerializeField], 씬에 저장되니 기본값을 바꾸면 **필드 이름을 새로** 지을 것 — 씬이 옛 기본값을 쥐고 있다) · BerserkMob/BerserkLook(Units) · NetBerserkTest(Net).
- 퇴치: PirateQuestData(requires*·hpByAttempt·hpMultiplier·successItem*·successHeroXp·firstStockSeconds) · PirateQuestManager · PirateQuestShop · QuestTimerPanel. 시험 하네스는 브랜치 dev/g1-quest-check(NetQuestTest, `-mpTestQuest 20`, 로그 [QT]) — main엔 안 넣음.
- MP 하네스: dev/g1-mp-check4(NetMainTest3) · 두 창 실행: 호스트 `-mpHost -mpSession 코드 -mpToken … -mpAutoStart 2 -mpDifficulty Easy -mp테스트…` → 호스트 로그에 [MP]가 뜬 뒤 4초 있다가 클라 `-mpJoin -mpSession 같은코드 -mpReady …`(안 기다리면 「Game does not exist」). 빌드 사본은 GameVersion.Number를 다른 이름으로 바꿔 빌드(0.3.14 앱을 안 덮는다).
- 임시 에디터 탐침은 전부 지웠다(SeaShade·BossSizePhoto·EnemySizeTable·QuestProbe·PatchProbe·TokenWiring 등).

## 함정 (오늘 겪은 것)
- **빌드 사본(../GuilRandomDefense-build)은 여럿이 돌려 쓴다**: 쓰기 전 상대에게 묻고, 비우기 전 미커밋 확인. 내 브랜치(dev/g1-*)는 저장소를 공유하니 git branch에 남는다 — 필요 없으면 지워도 된다.
- **에디터 refresh는 남의 플레이를 끊는다**: 구현담당2가 쓰는 중이면 소스 저장·refresh 금지(디스크 저장은 안전, 리로드만 문제). 「씁니다/끝났습니다」 꼭.
- **gameshot**: `select:`는 유닛 이름이 아니라 오브젝트 이름(`Unit_안흔함_이호준`) · `spawn:`은 클릭이 끝난 뒤에 서므로 직후 `select:` 불가 → 자체 `call:` 탐침으로 세울 것 · 난이도는 `mode:God`(클릭 아님) · 라운드 점프 `jump:N`는 그 난이도 총 라운드 안에서만.
- **RepairCombineBoard를 부르면** 조합표 줄 수가 바뀌어 `MapLayout.LegendColumnCenterOffset`이 어긋났다는 경고가 나온다 → 값을 고치고 한 번 더 부를 것(씬 diff가 수십만 줄 — 정상).
- **ScriptableObject 필드를 새로 더하면** 씬 컴포넌트가 옛 기본값을 쥔다(광폭화 크기 1.3이 남았던 사건) — 새 이름으로.
- **ArtBinder 표(EnemyModels) 미터가 보스 키의 근거**: 렌더러 경계는 무기·머리카락이 섞여 틀린다(R40 98 vs 몸 62).
- zsh: 한글 경로는 배열로, `git add -- … ; git commit -- …`. 씬 커밋 시 URP 조명 데이터(474bcb49…) 블록 증감 확인(이번엔 증가 없음, 1개 감소는 재생성 부산물).
