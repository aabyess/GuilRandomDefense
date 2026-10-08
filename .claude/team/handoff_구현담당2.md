# 구현담당2 인수인계 — 2026-10-08 밤 마감 (폭발형 체계·컷인 정지 뒤)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md` 구현담당2 항목 순서. 전부 main에 커밋(푸시는 PM). 에디터는 순번제(쓰기 전 「씁니다」를 PM·구현담당1 둘에게, 끝나면 「끝났습니다」) — **소스(.cs)·Editor 스크립트 편집·에셋 쓰기도 남의 gameshot을 도메인 리로드로 무효**로 만든다. 순번 전엔 읽기·설계·변경안 정리까지만. 남의 몫(구현담당1·blender) 커밋을 **cherry-pick하거나 reset하기 전엔 PM에게 먼저 묻는다**(10-08 18시경 구현담당1의 9da528424를 PM 지시 직전에 메인에 넣었다가 PM이 이미 푸시한 것과 어긋나 reset→ff로 복구한 사고).
작업 도구: gameshot(`ClaudeBridge/inbox/<네 접두>_이름.txt` 한 줄 → `outbox/같은이름.txt`, 사진 `ClaudeBridge/shots/`) — 🔴 **outbox에 같은 이름이 옛날 것으로 남아 있으면 즉시 끝난 걸로 읽는다** → 매번 새 이름 + `rm -f outbox/이름.txt`. `refresh`를 inbox에 넣은 뒤 `Library/ScriptAssemblies/Assembly-CSharp*.dll` 시각이 편집보다 새로워질 때까지(60~100초) 기다린 다음 gameshot. `Tools/ui/csc_check.sh <루트>`로 컴파일만(Editor 폴더는 안 봄 — 에디터 스크립트 오류는 refresh 뒤에야 드러난다).

## 오늘(10-08 저녁) 끝낸 일 (git log 제목으로 찾는다)
- **스킬 아이콘 943행 전부 연결** 1b3241219 + 촬영 탐침 a3dda4070(SpawnKimKm) — blender가 채운 표를 `SkillIconLinker.Link`(call)로.
- **초월 노태현 AP 스킬 재사양**(사장님 「방향성 제안」) f8b39c558: ①반사회적인격 평타 100% 범위300 공격력×30%+30만 ②하체부실 Aura 600(🔸) 적 Slow 0.5·아군 AllyMoveSpeedDebuff 0.5 ③가리지않는수단과방법 25% 스턴2초+30만, 보스·광폭화 ×1.3(`SkillEffect.bossBerserkDamageScale`) ④시너지폭발 10% 30만+70만(폭발형)+폭증 +3레벨 ⑤돌발행동 체력 게이지 100(재생 1/초, 평타 +1) 가득이면 현재체력 15%/보스 고정 30만. 에셋 이름: 「…가리지않는수단과방법」(옛 _보잡 개명), _체력스킬 삭제. 탐침 `NotaeProbe`(Setup·Direct·Aura·BossSetup/Mark/Report 6기·Table·WaveReport·AokijiTest).
- **컷인 중 게임 시간 정지(혼자 하기)** 063e8dcba: `GamePause`에 정지 이유 따로 세기 — Paused(사용자) · CutinHold(컷인) · Frozen(둘 중 하나). timeScale = Frozen?0:1 한 곳, AudioListener.pause는 **사용자 정지에만**(컷인은 소리 유지). 해제는 CutinOverlay.End가 아니라 Update에서 **큐가 비었을 때만**(연속 컷인 사이 안 풀림). MP는 구현담당1이 57d7ae246으로 얹음(CutinHoldInMultiplayer·Networked Paused). 탐침 `CutinPauseProbe`.
- **헤더 아래 클릭·드래그 복구 + 버그 기록 단추 이동** e79ec5fbe: 범인은 단추가 아니라 GameHud/StoryPanel(알파 0 투명 Image, raycastTarget 켜짐). CreatePanel(…Color.clear)이 만드는 투명 컨테이너는 **raycastTarget이 기본 켜져 클릭을 삼킨다** — StoryPanel·TopBarButtons·MapNamePanel 끔. 「버그 기록」은 BugNotepad.FollowScoreboard가 점수판(TeamPanel) 오른쪽 아래에 붙어 접힘·높이를 따라간다. 탐침 `UiStripProbe`(EventSystem.RaycastAll로 띠를 훑어 맨 위 히트 오브젝트 이름) — 「UI가 클릭을 먹는다」는 보고는 이걸로 먼저 범인 이름을 찍을 것.
- **항해일지 항법 선택 칸** 0bd3dc787·9d80e5c9a: VoyageLogShop 3번 칸(NavigationSlot=2) → GameHud.OnShopSlotClicked가 상점 RPC(UseShop)를 안 거치고 상단 「항법 선택」과 같은 창을 연다(고르기는 기존 NetCommands.RequestNavigation). 고른 뒤 「항법 3. 도박광」·눌림 불가, 남의 항해일지는 「보기 전용」. 아이템 도박 단축키 W(탐색 Q와 충돌 해소). 항법 이름의 동그라미 숫자는 폰트에서 ⊚로 나와 「1.」 글자로 바꿈. 탐침 `VoyageNavProbe`.
- **폭발형 데미지 체계** b0291546a (+목록 4410fa1f3, 원작 폭증 둘 5f67ab3f1) — 아래 함정 참고. 사장님 정의: 폭발형 = 마저항은 받고 마깎·마뎀증폭은 안 받고 폭뎀증폭(A11S)만 받는다 / 일반 마뎀은 마저항·마깎·마뎀증폭을 받고 A11S는 안 받는다.
- 폭증 연결: 불멸_정준영 레시피 +4(드래곤 불멸) · 초월_임장혁_AD 레시피 +2(루치 영원, 조합마다) · 히든_성탄 처음 획득 때 플레이어당 1회 +2(DamageLevelFixedState.TryGrantAokijiOnce, OnAcquired 호스트).
- 소소: 인수인계 낡은 항목 정정 edc512125 · 긴 이름 칸·버프 +N·공↓는 72383688e에서 이미 끝(사진 확인 g2n_assassin_sel·g2n_plusN).

## 남은 일 / 사장님 답 대기
- **사장님 답 대기**: ①문필환 폭증 값(지금 🔸임시 +50% = A11S +10레벨, 옛 제안 최대 +50% 이식) ②폭발형으로 안 친 비례 피해 — 범퍼류 10개·라인딜(이이삭 서울수도권을먹은자)·범위 마나스킬 최대체력(이승우 파괴의외침·두유찬 혼신의일격·임채민 천벌)을 폭발형으로 칠지 ③맥 F키 대책(PM이 물어 둔 것, 답 오면 지시) ④노태현 🔸임시값(하체부실 반경 600·돌발행동 게이지 재생 1/초·평타 +1·수단과방법 보스 ×1.3)
- 폭발형 목록 `Docs/research/EXPLOSIVE_FLAGGED_2026-10-08.md`(73효과 — 사장님 사양 단일·끝딜 17스킬, 원작 A11S 식 36파일, 보스 고정 분기 짝, 노태현 70만). 고정값 단일(③ 30만 등)은 일반 마뎀으로 뒀다.
- 아직 안 한 것: 노태현 정보창 사진 최신판 · 시너지폭발 호버 툴팁 · 컷인 효과음(사장님 선택 대기 — blender/PM 몫 Resources/Sfx/cutin_whoosh·cutin_ting) · MP 두 창 판매·소리 슬라이더·F10 · 점수판 접은 상태에서 버그 기록 단추 사진 · 히든 등급 오라 잔여(오라 7·시불 바위 텍스처는 정품 War3 필요, 도플라밍고 실 보류) · 폭발형 이후 보스 60초 재측정(±20%는 시간창 측정으로 안 잡힘 — 결정론 EV 도구가 필요하면 만들 것).

## 함정
- **폭발형 체계(b0291546a)**: `SkillEffect.explosive`(직렬화 맨 뒤, areaDamage는 삭제됨) = 폭발형 표시. `EnemyDummy.TakeDamage(…, explosive)`→`MitigatedDamage`: 폭발형 = `BaseMagicResistMultiplier`(난이도 Aegr 기본 레벨만) / 일반 = `EffectiveMagicMultiplier`(마저항×마깎×마뎀증폭). **AP(방어 무시) 스킬 피해도 이제 마저항·마깎·마뎀증폭을 받는다**(옛 코드는 AP에서 통째로 건너뜀 — 보스 AP 스킬이 마저항 0.95(God)만큼 −5%). `DealSkillDamage`는 A11S(`PercentDamageTakenMultiplier`)를 **effect.explosive일 때만** 곱한다(skipDamageTakenMultiplier는 사실상 폐기). 그래서 일반 몹 상대 일반 마뎀·물리 스킬은 A11S 0.90이 빠져 +11%. 폭증 = `SkillEffectKind.LaneExplosiveAmp`(=51, multiplier = A11S 레벨 수, 1레벨 = 5%): `UnitAttacker.LaneExplosiveAmpLevels(라인)` = 그 라인에 선 **서로 다른 유닛 종류** 레벨 합(같은 종류 1회), EnemyDummy.PercentDamageTakenMultiplier가 읽음(상한 23, 라인 한정 — 노태현이 선 라인 몹만 A11S 1.05). 노태현 시너지폭발 15%·신문철 스노우볼 +3·문필환 고집 +10·고도현 폭발증폭기 +6은 전부 Aura 트리거 passive. f17ab2495(범위 피해 전부 +15%)는 되돌려졌다 — 범위 피해 ≠ 폭발형.
- **결정론 A/B 방법**: 확률 스킬은 60초 보스 측정이 ±40% 흔들린다(엄태웅 134k→56k는 노이즈였다). 전/후 비교는 ① 같은 판에서 규칙 토글(`EnemyDummy.LegacyDamageRules` 같은 임시 static, 커밋 금지)로 약 23게임초 창을 번갈아 10창씩 평균±표준편차 ② 효과별 `CastSkillLevel` 직접 호출(확률 없이 전/후 보스 피해 비)로 닫는다. 기대 밖 하락이 −5%(AP 마저항)보다 크면 그때부터 버그. TranscendBossProbe/NotaeProbe.Boss*는 6기를 한 판에 세운다(표적 1e9 매 프레임 채움·double 누적). 신 R60 보스 마저항 0.950·방어 317.7·A11S 1.0(노태현 라인이면 1.15).
- **헤더 띠 투명 판**: 위 e79ec5fbe — 새 UI를 만들 때 `CreatePanel(.., Color.clear)`로 컨테이너를 만들면 raycastTarget=false부터. 투명 Image가 화면 띠를 가로로 덮으면 SelectionManager의 `IsPointerOverGameObject`가 참이 돼 드래그 선택이 안 된다.
- **컷인 정지 GamePause**: 정지 이유를 따로 센다(Paused·CutinHold). 뽑기·조합·스킬 막는 기준은 `Frozen`. 컷인 정지를 푸는 곳은 CutinOverlay.Update(큐 비었을 때)·씬 로드. 새 코드에서 `GamePause.Paused`만 보고 막으면 컷인 중 구멍이 난다.
- **항해일지 항법 칸**: 항법 고르기는 상점 RPC가 아니다 — 새 칸을 상점에 달 때 UI 전용 동작이면 OnShopSlotClicked에서 UseShop 앞에서 가로챌 것(MP 클라가 RPC로 보내 버린다). 동그라미 숫자(①②③)는 이 폰트에서 ⊚로 나온다 — UI 글자엔 쓰지 말 것.
- gameshot 도메인 리로드 오염(「무효」): 남(구현담당1·PM)이 소스를 저장·refresh하면 내 판이 끊긴다. `select:`는 겹친 유닛에 흔들린다 → 탐침으로 SelectionManager.SelectOnly 직접. gameshot call:은 인자 없는 static 메서드만(문자열 반환).
- 초월 노태현 AP 스킬 순서(로스터 skills): 반사회적인격·하체부실·가리지않는수단과방법·시너지폭발·돌발행동. 체력 게이지 UI 최대 = OnHitCount 스킬 문턱(돌발행동 100).
- 모델 배선 메뉴(Tools/아트/모델 배선)는 돌리지 말 것(403개 Prefab/Generated 재직렬화) — PM 확인 뒤에만, 내 경로만 커밋.
- 작업트리 미커밋 잔여(Materials·Fonts·UI/Skin .meta, `.check_entries_out/`)는 유니티 재직렬화·blender 산출 — 내 것 아니다, 건드리지 말 것.
- GameHud·UnitAttacker는 공용 — 커밋 전 `git diff -- 파일 | grep "^@@"`로 내 hunk만인지 확인, 커밋은 `git add -- 경로` + `git commit -- 경로`만.
