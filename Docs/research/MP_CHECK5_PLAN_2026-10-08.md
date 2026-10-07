# 멀티(MP) 5차 점검 계획 — 0.3.16 배포 전 관문 (구현담당1, 2026-10-08 준비·미실행)

대상 = 오늘 MP를 건드린 변경 넷. 실행은 에디터·빌드 사본이 비면 한다. 시험 하네스는 main에 안 넣는다(빌드 사본 브랜치 `dev/g1-mp-check5`, 배포 아님).

| # | 변경(커밋) | 두 창에서 볼 것 | 합격 기준 |
|---|---|---|---|
| ② | 500엔 도박 졸업 뒤 목재 구입 3초 잠금 (aa5e31810 · NetPlayer.GambleSwapLock) | 호스트가 친구 슬롯을 졸업시킨 뒤 0.5초마다 **호스트·친구 두 창의** 친구 도박소 칸 라벨·흐림 · 잠금 중 목재 구입 TryUse · 3초 뒤 TryUse | 졸업 직후 두 창 모두 목재 구입 칸 「잠금 N초」+흐림, 호스트 TryUse 거절(문구 「500엔 도박을 마친 직후라 N초 뒤에 쓸 수 있습니다」), +3초 뒤 두 창 모두 열림·TryUse 성공(목재 +1·엔 −10,000). 클라 라벨의 복제 잠금 초가 호스트와 ±0.6초 이내 |
| ③ | 퇴치 미니보스 외형 QuestMobTint (820792b2f) + 실제 적 모델 (4627d3104) | 호스트가 친구 레인에 미니보스 7종을 세운 뒤 **클라 창**의 NetEntity 겉모습 | 클라가 7기 모두 QuestMobTint 1~7(의뢰별 서로 다름)·시각 이름 Mob_이호준/이정범/박민수/서승혁/최혜륜/이현주/지성현·크기 ≈ 일반 라인몹 ×1.5·QuestMobLook 있음·Animator 컨트롤러 Character. 사진: 두 창 같은 장면 |
| ④ | 다른세계강화소 삭제 + 레인 상점 6채 재배치 (332c07e8b, NetShops 번호 = 계층 경로 순) | 양쪽 NetShops 0..N 목록 이름 | 두 창 목록이 같고 「다른세계강화소」가 없다. 클라가 유닛강화소·영원함강화소·도움소에서 실제 구매 한 번씩(번호가 어긋나면 엉뚱한 상점이 눌린다) |
| ⑤ | 게이지 HUD (구현담당2 f7a39d775 · NetEntity.Gauge* 복제 4b502c187) | 클라가 호스트 소유 아닌 내 유닛(친구 슬롯)을 선택했을 때 초상 아래 마나/체력 막대 | 클라 막대의 현재/최대가 호스트의 UnitAttacker.ShownManaNow/ShownLifeNow와 ±2 이내, 게이지 없는 유닛은 마나 막대 숨김 |

## 준비(빌드 사본 — 쓰기 전 PM·구현담당2에게 「씁니다」, 비우기 전 남의 미커밋 확인)
1. `cd ../GuilRandomDefense-build && git status`(남의 미커밋 있으면 중단) → `git checkout -B dev/g1-mp-check5 main`
2. `cp <repo>/Tools/mp_check/NetCheck5Test.cs.txt Assets/Scripts/Net/NetCheck5Test.cs`
3. `Assets/Scripts/Net/NetLauncher.cs`에 훅 3줄: ① 필드 `float testCheck5Delay = -1f;` ② `case "-mpTestCheck5": testCheck5Delay = Seconds(i + 1); break;` ③ testMainDelay 줄 아래 `if (testCheck5Delay >= 0f) { var c5 = gameObject.AddComponent<NetCheck5Test>(); c5.startAt = testCheck5Delay; }`
4. `GameVersion.Number`를 `"0.3.16mp5"`로 바꿔(0.3.15 앱을 안 덮는다) 빌드: `Unity -batchmode -projectPath <build> -buildTarget OSXUniversal -executeMethod BuildBeta.Mac -quit -logFile <로그>` (Builds/Mac/GuRandi 0.3.16mp5v.app)
5. 빌드 사본에 이미 있는 판이면 `git diff`로 내 변경만인지 확인, 비우기 전에 미커밋 확인.

## 실행(두 창, 쉬움 난이도 — MP_CHECK3과 같은 방식)
호스트: `open -n "GuRandi 0.3.16mp5v.app" --args -mpHost -mpSession C5<번호> -mpToken <아무 문자열> -mpAutoStart 2 -mpDifficulty Easy -mpTestCheck5 20 -mpShotAt 24 /tmp/c5_host_a.png -mpShotAt 45 /tmp/c5_host_b.png -mpQuit 90`
→ 호스트 로그에 `[MP]`가 뜬 뒤 **4초 뒤** 클라: `open -n "…app" --args -mpJoin -mpSession C5<같은 번호> -mpReady -mpTestCheck5 20 -mpShotAt 24 /tmp/c5_client_a.png -mpShotAt 45 /tmp/c5_client_b.png -mpQuit 90` (안 기다리면 「Game does not exist」). 로그는 각 앱 Player.log에서 `[C5]`로 거른다. 창은 겹치지 않게 사람이 옮기지 말고 화면 가로채기(가상 마우스) 금지.
(시작 20초 뒤 하네스가 돈다: 상점 목록 → 졸업 잠금 5초 → 미니보스 7기. 클라는 160×0.5초 동안 관찰.)

## 로그 판독 요령
- `[C5] ① 호스트/클라 상점 N개 … 다른세계강화소 없음 ✓` 두 줄의 「첫 12」 이름 순서가 같아야 한다.
- `[C5] ② +k s 호스트 보는 친구 도박소 …` 0~4.5초: `[1]목재 구입 / 잠금 3→1(흐림)` → 3.0초 뒤 라벨 정상. `② +1.0s 목재 구입 시도 → 거절 ✓`, `② +5.0s … 성공 ✓ 목재 a → a+1`. 클라 줄 `② 클라 내 도박소 (t+…)`에 같은 변화.
- `[C5] ③ 클라 퀘스트 미니보스 겉모습 7기: …` 한 줄에 7개(틴트·시각·크기·QuestMobLook·Animator).
- ⑤는 하네스가 안 본다 — 클라 창에서 친구 유닛을 눌러 사진(c5_client_*.png)으로 막대 값을 읽는다(필요하면 하네스에 ShownManaNow vs NetEntity.GaugeManaNow 로그를 더한다).

## 관문 판정
②③④⑤ 전부 합격 + Player.log 예외 0 → 0.3.16 배포 가능. 하나라도 틀리면 원인 수정 뒤 해당 항목만 다시.
미검증으로 남기는 것: 회유 MP 실전투·광폭화 MP 실전투(MP_CHECK3과 같은 한계) · 클라의 지점/대상 지정 액티브(막혀 있음).
