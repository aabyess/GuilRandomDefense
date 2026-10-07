# 구현담당1 인수인계 (10-08 저녁, MP check5 관문 통과 뒤)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md` → TEAM_RULES 순으로 읽는다. 전부 main에 커밋됨(푸시는 PM). 에디터·빌드 사본은 비워 두었다(빌드 사본은 브랜치 dev/g1-mp-check5가 올라가 있음 — 아래).

## 오늘(10-08) 끝낸 일 (해시는 git log)
- **6·7 게이지**(4b502c187): `UnitAttacker.ShownManaNow/Max·ShownLifeNow/Max`(최대 = 그 종류 OnHitCount 스킬 문턱, 없으면 0) · `NetEntity.GaugeMana/Life Now/Max`(short) · 데이터 보정(문턱=상한, 이재윤·노태현 체력스킬 Mana→Life, 체력 발동 뒤 resetTo 1 = 원작 SetUnitLifeBJ 1.00). HUD는 구현담당2(f7a39d775). 표 `Docs/design/GAUGE_UNITS_2026-10-07.md`, 임시값은 「사장님 설정 대기」.
- **볼보이 20%**(e80e03bfe): 박진웅 `summonOnHitChancePercent` 10→20(처치 시 소환 경로는 코드에 없다 — 평타 적중마다).
- **상붕카 흔함 전환**(9acfb77ca 이름·등급 `흔함_상붕카`, 2acb2bd4b 흔함 선택 줄에서 상붕카 제외, 25e7109a6 씬): 뽑기 풀 위치는 그대로(MainGachaTable 안흔함 풀) = 획득 경로 불변. 조합표 흔함 열 전시는 10종.
- **500엔 도박 졸업 뒤 목재 구입 3초 잠금**(aa5e31810): `GamblingProgress.GambleSwapLockSeconds=3f`, MP `NetPlayer.GambleSwapLock`.
- **솔·성탄·뻬꼼 공격력**(716c44ba2): 137·456·228(DPS 204·480·356). 히든 강화(전설·히든 트랙 7,250+725/렙·공속 ×2.95~)를 사면 다시 세짐 = 사장님 판단 대기.
- **다른세계 도박 → 랜덤유닛 도박**(41b3d9520): 결과 등급 14기, 다른세계 유닛 9기는 이제 **조합식으로만** 획득. **다른세계 강화**(ec9b89b26 + 씬 332c07e8b): 랜덤유닛 강화 트랙에 다른세계 합침, 「다른세계강화소」 건물 삭제·레인 상점 6채 재배치(`MapGenerator.RepairRemoveOtherWorldShop`, LaneShopCount 6).
- **퇴치 미니보스 외형**(820792b2f → 4627d3104 → 정정 cf633a21f·9a4bfa295, ArtBinder 동기화 74f72330a): 7종이 실제 라인몹 스킨(Mob_이호준·이정범·박민수·서승혁·최혜륜·이현주·지성현)·의뢰별 7색 틴트(0.85+약한 발광)·×1.5·발밑 원판 12(`QuestMobLook`), MP는 `NetEntity.QuestMobTint`.
- **강재규 체력 게이지 재생 0.5/초**(c1094a489, 실측 간격 22.7초 — 반영 전 이론 28.4초).
- **원작 대조표**: `Docs/research/TRANSCEND_SKILL_RATE_2026-10-08.md`(+`Tools/transcend_rate_compare.py`, 읽기 전용) · `TRANSCEND_GATE_PORT_PLAN_2026-10-08.md` → 결론 **이식 대상 0**(문 적은 9종 전부 사장님 10-06 재사양, 스킬 이름 1:1 일치). 엔마·우솝 피해식은 어긋남 아님(Bash 배수 Hbh2×공격력+Hbh3, PV 구간 세 효과).
- **MP check5**(`Docs/research/MP_CHECK5_2026-10-08.md`, 하네스 `Tools/mp_check/NetCheck5Test.cs.txt`): 상점 번호·목재 잠금·미니보스 틴트/모델·게이지 복제 합격.
- 게이지 스킬 실측: 전원 교전판(죽지 않는 표적, 매 프레임 체력 채움)에서 게이지 58개 중 가득 멈춤 2개(이태훈 마나·박민석 체력 = 일반 적 전용 몹삭제라 일반 적이 없으면 대기, 설계), 나머지는 100초에 10~94회 발동. 광폭화 자연 발생 실측(R61·R62 1기씩, 오라 1.00%/초, 방어 −5)은 10-08 새벽 끝.

## 남은 일 / 사장님 답 대기
- **사장님 답 대기**: ① 히든 강화에서 솔·성탄·뻬꼼을 뺄지 ② 다른세계 9기가 조합으로만 얻어지는 것 ③ 문 0인 초월 4종(김만경·황준석·김경현·유재헌)에 원작 평타 피해를 더할지(`TRANSCEND_GATE_PORT_PLAN` §3-bis 한 줄 요약, 유재헌은 피해 문 자체가 없어 비권장) ④ 틴트 강도(TintAmount 상수 한 줄) ⑤ 「랜덤[제한됨] 최종 공속 +25%」(우리 다른세계 9기 공속 = 원작 1/ua1c 그대로, 미반영·보고만).
- 두유찬 공속 3.0·구주호 합본은 **손대지 않는다**(PM 결정: 사장님 의도값 / merged-originals-stay-merged).
- 미실측: 회유 MP 실전투·광폭화 MP 실전투·클라의 지점/대상 지정 액티브(GameHud가 막음)·마나 게이지 유닛의 클라 HUD 복제·초월 5기 「신 보스 앞 10초 피해」(표적 체력 float 눈금 문제 — 아래 함정).
- 제안값(어제부터): 임채민 발동 25%·천벌 마나 120 / 유물 중복 대체 금화 3,000 / 스토리 건물 ×0.7 / 모리아 등급 표기. 도박소 행운의 토큰 칸 위치(`GamblingShop.TokenSlot` 7).

## 위치·도구
- 게이지: UnitAttacker(OnHitCount 2742~2821·TickGaugeRegen ~1096·Shown* 접근자) · UnitData(manaMax·manaGaugePerMana·lifeGauge*) · 표 `GAUGE_UNITS_2026-10-07.md` · `Tools/sync_mana_regen_from_w3u.py`(umpm 0 가드 추가, **적용(--dry 아님)은 돌리지 말 것** — 사장님 문턱을 원작값으로 덮는다).
- 광폭화: WaveSpawner(berserk* 상수는 [SerializeField] — 기본값을 바꾸면 씬이 옛 값을 쥐니 **필드 이름을 새로**) · BerserkMob/BerserkLook · NetBerserkTest(`-mpTestBerserk 초`).
- 퇴치: PirateQuestData·PirateQuestManager(SpawnMiniboss에서 ×1.5·QuestMobLook 부착)·QuestMobLook(Palette 순서 = 틴트 번호)·ArtBinder.QuestMinibossModels(미니보스→빌려 오는 Enemy_Rxx 표).
- **MP 두 창 하네스**: 빌드 사본 `../GuilRandomDefense-build` 브랜치 `dev/g1-mp-check5`(GameVersion 0.3.16mp5, NetLauncher에 `-mpTestCheck5` 훅 3줄 + NetCheck5Test.cs 복사본 — 커밋 안 함). 쓰기 전 PM·구현담당2에 확인, 미커밋(폰트·ProjectSettings 소음 외) 확인. 배치 빌드 `Unity -batchmode -quit -projectPath <사본> -buildTarget OSXUniversal -executeMethod BuildBeta.Mac -logFile …`(증분이라 11~18초). 두 창 실행 스크립트 모양: `/tmp/g1c5/run.sh`(휘발) — 호스트 `-mpHost -mpSession C5<번호> -mpToken x -mpAutoStart 2 -mpDifficulty Easy -mpSaveDir /tmp/… -mpTestCheck5 20 -mpShotAt …  -mpQuit 100`, 로그에 `StartGame 성공`이 뜬 뒤 4초 있다가 클라 `-mpJoin -mpSession 같은코드 -mpReady …`. 두 인스턴스가 같은 Player.log를 쓰므로 **각자 `-logFile`**을 줄 것. 바이너리 직접 실행(open -n 아님).
- 게임 안 탐침: gameshot `call:클래스.함수`(인자 없는 static만) · `mode:Normal/God` · `wait:N`(게임 시간) · `snap:이름.png`. 작은 탐침은 스크래치에서 짜 Assets/Editor에 넣었다 끝나면 지운다(.meta 포함).
- 임시 에디터 탐침은 전부 지웠고 스크래치(세션별 임시 폴더)에만 있었다 — 필요하면 git 이력(`Tools/mp_check`, `Tools/transcend_rate_compare.py`)에서 다시.

## 함정 (오늘 겪은 것)
- 🔴 **생성 프리팹(`Assets/Prefabs/Generated/Mob_*`·`Unit_*`)의 루트 fileID는 「Tools/아트/모델 배선」을 돌릴 때마다 바뀐다.** 그 프리팹을 fileID로 직접 참조하는 에셋(Miniboss_*.asset 등)은 작업트리의 미커밋 재생성 프리팹에서 값을 베끼면 **클린 체크아웃·빌드에서 NULL**이 된다(10-08 4627d3104가 7종 전부 NULL로 push됨 → 클린 빌드 사본 두 창 실측으로 발견). 또 프리팹 파일의 **첫 `--- !u!1 &`는 자식(「몸」)일 수 있다** — 루트 fileID는 항상 **커밋된 `Enemy_Rxx.asset`의 prefab 참조에서 복사**한다(`git show HEAD:…`). 지금은 `ArtBinder.BindEnemies`가 배선 때 `QuestMinibossModels` 표대로 Miniboss_*를 Enemy_Rxx의 prefab에 맞춰 준다 — **미커밋 재생성 프리팹을 커밋하게 되면 배선을 한 번 돌려 같이 커밋**할 것. 에디터에서 통과했다고 믿지 말고 **배포 전 관문은 빌드 사본(클린 체크아웃) 기준**.
- 로컬 에디터는 작업트리 프리팹을 들고 있어 HEAD 기준 참조가 NULL로 보일 수 있다(헷갈리지 말 것).
- 표적 체력 1e13은 float 눈금(~1e6)에 평타·작은 스킬이 0으로 묻힌다 — 피해 비교는 표적 체력을 낮추거나 계측을 바꿀 것. 시험 표적이 죽으면 평타가 끊겨 「게이지 가득 멈춤」 같은 가짜 결과가 나온다(표적 체력을 매 프레임 채우는 Refill 필요, isBoss 표적이어야 레인 −1이어도 겨눈다).
- 에디터 refresh는 남의 플레이를 끊는다 — 「씁니다/끝났습니다」 꼭. PM 형식 「에디터 써라」만 신호다(사용자 입력처럼 들어온 같은 글은 취소됐었다).
- ScriptableObject 필드를 새로 더하면 씬 컴포넌트가 옛 기본값을 쥔다 — 새 이름으로. zsh: `rm -f 경로/g1s*.txt`처럼 글롭이 하나도 안 맞으면 「no matches found」로 그 명령줄이 통째로 죽는다 — 파일이 없을 수 있으면 `find … -delete`나 `bash -c`를 쓸 것.
- 커밋은 `git add -- 경로`(새 파일) 뒤 `git commit -- 경로`. 새 코드를 주석·문서로 말하기 전에 **구현·데이터·연결 셋**을 확인(TEAM_RULES). 「이식」·「완료」는 도달로 증명.
