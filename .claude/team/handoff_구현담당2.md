# 구현담당2 인수인계 — 2026-10-06 오후 (명령 카드 재배치·푸바오·이재윤 끝, 신문철 이후 큐)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md`의 구현담당2 항목 → 아래 설계표 순서로 읽는다. 전부 main에 커밋됨(푸시는 PM). 사장님 확정은 **각 설계표 맨 아래 「사장님 확정」 절**에 있다.

## 오늘 끝난 것 (제목으로 git log에서 찾는다)
- **명령 카드 재배치**(bcee45a76 · 70efd0d1b): 1줄(0~3) 홀드·공격·모으기·판매 고정 · 4~11 유닛별 칸은 `GameHud.ReflowFlexSlots`가 11→4 순서로 채움(액티브→재능투자→최윤서→폭탄/웅교교주→특성강화→조합 결과) · 이동·정지·정렬 칸 뺌(단축키 M·S·C 유지). 새 칸을 만들 땐 `FlexKind`에 맨 뒤로 + Reflow의 Put 순서 + 클릭·툴팁 switch 세 곳. 사진 `Docs/ui_mockups/compare/26~30`.
- **푸바오**(초월_김민준_AP, fa18d7747): 찍어누르기(마나 135·잃은 체력 2%·보스 상한 없음) · 포커싱오더(토글 `SkillLevel.toggleMode`, `UnitAttacker.FocusLostHp/FocusActive`, UnitCombat 표적 재선택, 소환수는 `SummonedBy`로 따라감) · 특출난분석력(평타 25% 마방 +6레벨) · 산하동료호출(소환체 `Summon_푸바오_산하동료`, **프리팹 참조는 Apply가 매번 흔함_강재규 현재 참조로 이음** — 모델 배선 때 fileID가 바뀌어 소환체 에셋이 끊긴다). 도구 `FubaoApply`·`FubaoProbe`.
- **이재윤 초특급인싸**(초월_이재윤_AD, 79f0a36a7): 스킬 6 · `SkillLevel.laneCountWindow`(레인 적 수 [한계−10, 한계] 오라 게이트, 내 레인 적만) · 종이비행기 유물(스토리 7 버스터콜 자리, `PaperPlane.TryUse`·`EnemyDummy.FreezeForever`·GameHud 표적 고르기) · trait 비움. 도구 `JaeyunApply`·`JaeyunProbe`. ⚠️ **비행(Flying)은 섬 밖 바다로 못 나간다**(미해결, 아래).
- **버전 글자**(GameVersion.cs): 하단 바 RectTransform 윗선 기준(0.3.7v가 판매 칸에 안 겹침) — 실측 사진은 아직 안 찍음.
- **설계표 6개 + 사장님 확정 기록**: `Docs/design/` 의 FUBAO · JAEYUN · SHINMUNCHEOL · JUNSEOK · JAEHEON · OTHERWORLD_RECIPES · LIMITED_RECIPES (`*_2026-10-06.md`).

## 남은 큐 (순서대로, 에디터 순번 지킬 것)
0. 🔴 **%체력 피해는 전부 `armorIgnoreRatio = 1f`**(PM·사장님 10-06 「원작대로」, 신 보스 방어 317에서 AD 7%가 1%로 깎임). **이재윤 체육특기생(JaeyunApply.cs `Hit(TargetCurrentHpPercent…)`)에 `armorIgnoreRatio = 1f` 넣고 `call JaeyunApply.Apply` 다시.** 신문철 사고뭉치(MuncheolApply, AP라 이미 무시지만 같은 규칙으로 1f 넣기)·푸바오(AP, 변경 불필요). 구현담당3이 자기 몫(박민석·양재모·박민수)은 고침.
1. **신문철 말썽쟁이**(초월_신문철_AP): 소스·`MuncheolApply.cs` 커밋됨(07e0d261a, **Apply·실측 아직 안 함**). `call MuncheolApply.Apply` → gameshot 실측: ①공속 +15% 오라 ②사고뭉치 현재체력 2%(armorIgnore) ③스노우볼 누적 +1%·상한 +15%·3초 초기화(`AttackDamageStackValue`) ④노출중독 이감 0.85+깡딜 ⑤엄마간식 아군 클릭 → 공속 +100% 10초·쿨 40초·우클릭 취소(GameHud `RefreshAllyTargeting`은 가상 마우스로만 클릭 경로 확인 가능 — 안 되면 `ExecuteCastActiveOnAlly` 직접 호출 탐침). 재료는 이미 맞음. 사장님 확정: ②폭증=해석 A.
2. **FlyingMover(B안, PM 결정)**: Flying 유닛만 NavMesh 대신 직선 이동 컴포넌트. 에이전트 API 호출 14곳(UnitMover·UnitCombat·UnitCommands)에 분기. **movementAbility 값으로 일반화** — 앞으로 「바다이동」(불멸 박은석)·「전지역이동」(불멸 김용태)도 같은 틀. 이재윤은 지금 Flying인데 섬 가장자리(약 26)에서 멈춘다(`JaeyunProbe.MoveSea`/`MoveReport`로 재현).
3. **황준석 공짜집착증**(초월_황준석_ADAP, 설계 확정): 소스 일부 커밋됨(GoldPlusBonus·StoryDamageMultiplier·GoldWallet.AdjustGoldPlus). **JunseokApply.cs와 탐침은 아직 안 씀.** 스킬 4(준석의담판=처치 골드 +0.2 · 결의=맵 전체 공속 +20% · 믿음직한도움=PV≥200 상대 ×1.3 · 어디든지달려갑니다=Flying) · unitName 「공짜집착증」 · 입력말 구일대표홍보대사(CSV+식) · `trait = null` · 재료 이미 맞음. FlyingMover와 함께.
4. **유재헌 앰생파조장**(초월_유재헌_ADAP, 설계 확정): 아직 소스 없음. 새 구조 `SlowRewardBonus`(이감 1%당 처치 골드·목재 +1%)·`FlexKind.Toto`(엔 1,000 도박 버튼, 쿨 없음)·`SkillLevel.ownLaneOnly`(현상수배는 내 레인만) · 소환수 2기 상시(죽으면 CooldownAutoCast로 재소환) · 순간이동은 구현담당3 배성령 틀 커밋 뒤 공유 · 재료 제한_김민규→전설적인_김민규 + 초월위습 추가(5종) · trait 비움.
5. **다른세계 9식 재료**: 5개 식만 바뀐다(김건부 신문철 빼기 · 모리야스와코 엔 10,000 · 고죠사토루 제한_김민규→전설 + 랜덤전용 1기 · 브로리 랜덤전용 1기 + 전설_양재모→희귀_양재모 + 「임채현 or 이재윤」 · 호시노루비 전설_김정래→특별_김정래). 확정: 고태훈 개명 · **「or」 재료 새 기능**(`RecipeIngredient.alternativeUnit` 맨 뒤 + CombineSystem 판정·소모·안내 한 자리) · 9기 칭호를 `unitName`으로(「이름 칭호」 한 덩어리 — 인벤토리 키라 `unitName` 참조 grep 필수) · (신)은 칭호 일부.
6. **제한됨 8종**: 재료 바뀌는 식 — 박기찬 희귀→특별(김강민·이유범) · 김민규 식 강주혁·김용태 → 특별함 · 박성호 식 「분실된지갑」 아이템(**에셋 없음**, 얻는 길 질문 대기). 스킬은 사장님이 일부만 적음 — 질문 8건은 설계표 §4. **사장님 답은 오는 대로 설계표 맨 아래에 기록**(PM이 줌).
7. **스킬 아이콘 Link 한 번**(blender `skill_icon_map.csv` 커밋 6514f9b02·2faa0d0ef · 구현담당1 새 SkillData 커밋 뒤): `call SkillIconLinker.Link`. 「아이콘없음」 58행(게이트·더미)은 기본 그림 `~/GRD_skill_icons/std_btnspellbookbls.png`(이름은 `Tools/skill_icons/default_icon.txt`)으로 칸이 안 비게. 초월 최상호 구일 선택 화면 사진(PM 요청)도 아직.
8. 푸바오·이재윤·신문철 등 새 초월은 **PM 확인 뒤 푸시** — 보고에 설계표·실측 사진·커밋 해시.

## 함정 (오늘 배운 것)
- 🔴 **소스(.cs) 편집도 남의 gameshot을 「플레이 도중 도메인 리로드」로 무효로 만든다** — Assets 쓰기뿐 아니라 컴파일 되는 소스 파일을 고쳐도 같다. 내 에디터 순번 밖에서는 소스·Assets을 안 고치고 설계표(Docs)·git·읽기만. PM이 「소스 편집은 괜찮다」고 해도 구현담당1·3이 판 도는 중이면 안 된다(두 번 사고 냄). 순번: `씁니다`→작업→`끝났습니다`, 새 에디터 파일은 만든 뒤 바로 refresh.
- **모델 배선 재실행(PM) 뒤 Summons 폴더 소환체의 프리팹 참조가 끊긴다**(흔함_강재규 프리팹 루트 fileID 변경 → `UnitSpawner: UnitData 또는 prefab이 비어있어 소환할 수 없습니다`). 푸바오는 Apply가 매번 잇는다. 구현담당1 소환체(슈가·시키·시노부 분신)는 재연결 도구를 만든다고 함.
- 신 기준(DifficultyTable): 적 체력 = 에셋 hp × 배율(R45 4.9M·R50 보스 87.5M·R60 보스 179.8M). 탐침의 가짜 표적은 인위적 체력이라 비율만 본다. 끝딜 분류: 확률=최대체력 · 마나=잃은체력(보스 상한 없음) · 처형 · 현재체력은 끝딜 아님(보스 고정 300,000은 원작 분기).
- 탐침 판은 한 번에 한 가지: `snap:`은 판마다 한 장. 판이 무효 났는지는 outbox 맨 위 「❌ 오염」 줄로. 새 SkillData 필드가 생기면 에디터가 모든 SkillData 에셋을 재직렬화한다(대량 diff는 정상, 커밋엔 내 파일만).
- `git add -- 파일` 후 `git commit -- 파일`. 공유 파일(SkillData·UnitAttacker·GameHud)은 남의 hunk가 섞이면 상대에게 먼저 알린다. 한글 경로 glob은 zsh에서 배열로. 임포트 전 새 .cs는 .meta가 없으니 refresh 뒤 커밋.
- 탐침 도구: `CommandCardProbe`(칸 12개 표·레이캐스트·클릭) · `FubaoProbe` · `JaeyunProbe`(Fill59~71 레인 적 수·MoveSea·UsePlane). `Tools/ui/csc_check.sh`·`csc_check_editor.sh`는 `| grep -c error` 0이어야 통과.

## 위치
- 설계표: `Docs/design/*_2026-10-06.md` (사장님 확정은 각 파일 맨 아래) · 사진 `Docs/ui_mockups/compare/26~32` · 사양 원문 `Docs/research/SPEC_IMMORTAL_ETERNAL_OTHERWORLD_LIMITED_2026-10-06.md`·`TRANSCEND_QUEUE_2026-10-06.md` · 신 기준 감사표 `Docs/research/TRANSCEND_GOD_BALANCE_AUDIT_2026-10-06.md`.
- 도구: `Assets/Editor/{Fubao,Jaeyun,Muncheol}Apply.cs`·`{CommandCard,Fubao,Jaeyun}Probe.cs`. CSV 입력말 `Tools/transcend_phrases.csv`(바이트로 바꿀 것 — 줄바꿈 보존).
