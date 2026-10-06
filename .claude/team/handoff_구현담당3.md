# 구현담당3 인수인계 (2026-10-06 저녁, 세션 교체)

## 오늘 끝낸 것 (전부 커밋됨, error 0)
| 일 | 커밋/문서 | 한 줄 |
|---|---|---|
| 흐린 [조합] 부족 문구 | 6607c309e(HEAD 컴파일 오류 고침)·7f9ef69a2(ShortageProbe) | 재료/골드/목재 부족 세 문구 실측 통과 — 코드 수정 불필요 |
| 초월 노태현 「여동생살해자」 | ae693a745·26378c7ca·c258d379e(설계표) | 스킬 6개·재료(코알라·늑대 특별함)·최윤서 강화 6번 칸·아군 이속 감소 오라·폭발증폭 ×1.5 |
| 부서진손거울(유물) | 26378c7ca·8644f36ba | ItemData_R001, 스토리4 폐함대선 자리(1/22), 아이콘=원작 A0UF 청동 메달 |
| 초월 강재규 「상호파정보보완관」 | 21c5e5269·10ee325de·489a6b200(설계표) | 스킬 6개(깡딜·간잽이 게이지40 스턴·만성피로 자기 스턴 3초[게이지 안 채움]·끝딜·디버프비례·단일도킹[액티브+대상 클릭, 원작 A0K3]) |
| 초월 박민수 「해방된자」 | 10ee325de·42ab98e2b(설계표) | 스킬 4개+재능투자(5·5·5·3, 8~11번 칸, 레벨당 1점·18 상한)+성장속도 |
| 초월 임장혁 「짱스」 | dd525a0e1·d8ff72db9(설계표) | 스킬 8개 적용·실측(아래 「남은 일」 참고) |
| 첫 화면 | 9859a1232·8e2129327 | blender 시안(노을 섬 사진·청동 틀·송명/나눔명조) — NetLobbyUi·LobbyArtSetup |
| 대기실 자리 이동 | e56e5e38d·eeac2c42c·35d3697f6 | 빈 자리 줄 클릭 → 호스트가 옮김. PM이 맥 두 창으로 통과 확인 |

## 남은 일 — 임장혁(초월 10번)
사장님 답(10-06): 보조딜=역할 설명(스킬 없음) · 「스킬피해증가(아군 공격력 비례)」=아군 공격력 +25% 오라 · 「범위증폭」=아군(자기 포함) 스킬 피해 +25% 오라(가스라이팅) · 마나 120 · 악보완성! 아군 공속 +40% 8초 · 마나 오라 +2/초 · 평타 단일 · 이간질 특성 2pt 영구 제거.
- **적용·실측 끝난 것**(`call JanghyukApply.Apply`, outbox g3_1006_j12): 스킬 8개 · 재료(희귀 이용민·특별 배성령으로 교체) · 입력말 훌륭한장애인 · 마나 게이지 120 · 마나 오라 필드 · 고충해소(디버프 해제: 노태현 이속 디버프 100.2→125.3 복구, 임장혁 자신의 이간질은 안 지움) · 이간질(박민수 공격력 22→25) · 스킬 피해 오라 ×1.25 · 공격력 오라.
- **못 쟀다**: ① 악보완성! 실제 발동(마나 120 → 아군 공속 +40%) ② 영혼없는칭찬 공속 발동(평타 10%) ③ 마나 오라가 아군 마나 게이지를 실제로 올리는지 ④ **특성 2pt 이간질 제거**(trait 에셋은 costTraitPoints 2·skillLevelUnlockIndex 1로 세팅했고 이간질 SkillData 레벨2는 효과 0 — 버튼으로 사서 박민수 공격력이 22→27.5로 오르는지 확인 필요) ⑤ 사진 없음 ⑥ 구현담당2 「긍정의힘」(이재윤)과 DispelAllyDebuffs kind 공유 확인(같은 효과를 두 번 만들지 않았는지 PM·구현담당2와 대조).
- 임장혁 ⑦ unit.trait는 비우지 않고 이 특성(이간질 제거)을 쓴다 — 다른 초월은 trait를 비웠다. 능력교체형 replacementSkill은 null로 비웠다.
- **설계표 미반영 부분 정정**: Docs/research/TRANSCEND_JANGHYUK_DESIGN_2026-10-06.md는 사장님 답 전 버전이라 새 kind(AllyAttackDebuff·AllyAttackPowerAverage basis)가 적혀 있다 — 실제 구현은 기존 AttackPowerBuffPercent(음수=이간질)·AttackPowerBuffPercent 오라·새 kind 둘(AllySkillDamageBonus·DispelAllyDebuffs)뿐. 문서를 고치려면 §3·§4·§6을 위 「사장님 답」으로.
- 전체 초월 25종 중 오늘까지 사장님이 사양을 준 것: 최상호 구일(구현담당1)·바지사장(구현담당2)·양재모·박민석·이재윤 등은 다른 담당 — 이 문서는 내 몫만.

## 파일 위치
- 초월 적용 도구: `Assets/Editor/{Notaehyun,Jaegyu,Minsoo,Janghyuk}Apply.cs` + `…Probe.cs`(gameshot call:로 부른다). 다시 불러도 안전. 새 초월을 만들 땐 `SanghoGuilApply.cs`(구일)를 본보기로.
- 새 SkillEffectKind(전부 enum 맨 뒤): AllyMoveSpeedDebuff·SplashDamageMultiplier·DamagePerAllyDebuff·SelfStunRefillLifeGauge·DamageGrowthOverTime·AllySkillDamageBonus·DispelAllyDebuffs. SkillEffect 필드 armorIgnoreRatio/armorIgnoreRequiresBuff/talentStunScaled, SkillLevel.needsTargetClick, ItemData.isRelic, NetHudAction.Yoonseo=4·Talent=6(CastActive=5는 구현담당2).
- UnitAttacker 새 자리: 아군 디버프(IsAuraDebuff·DebuffSuppressed·CountAllyDebuffs)·재능투자(HasTalents·TryInvestTalent)·최윤서 강화(YoonseoEnhanced)·자기 스턴(BeginSelfStun)·성장 계수(GrowthDamageFactor)·단일도킹 시전(TryCastActiveOn).
- GameHud 명령 카드 칸: 5=액티브(구현담당2) · 6=최윤서 강화 · 7=판매 · 8~11=박민수 재능투자(초월 박민수는 조합 결과가 없어 비어 있다). 대상 클릭 흐름 = RefreshDockTargeting.
- 첫 화면: `Assets/Scripts/Net/NetLobbyUi.cs`(그림 `Assets/Resources/UI/Lobby/`, 폰트 `Assets/Resources/Fonts/`, 도구 `call LobbyArtSetup.Apply`). 촬영은 `Tools/Claude/씬 열기: NetBoot` 메뉴(`G3SceneMenu`) → gameshot → **SampleScene 복귀**.
- 자리 이동: NetPlayer.RPC_RequestSlot·NetSession.TryMoveSlot·NetLauncher 시험 플래그 `-mpTestSlot N 초`·`-mpTestStartAt 초`(로그 「[자리]」). 호스트 세이브 파일은 자리와 무관하게 player_0.json(PersistentSave.SavePath).

## 함정 (오늘 겪은 것)
1. **공용 파일 커밋**: UnitAttacker·SkillData·GameHud·NetCommands는 세 세션이 같이 고친다. `git commit -- 파일`은 남의 hunk까지 삼킨다. 내 hunk만 올릴 땐 `git diff -U1 파일`에서 키워드로 hunk를 골라 `GIT_INDEX_FILE=/tmp/x git read-tree HEAD && git apply --cached`로 임시 인덱스에 올려 `commit-tree` → `update-ref HEAD` 하고, 진짜 인덱스는 `git reset -q HEAD -- 그 파일들`로 맞춘다(안 맞추면 다음 커밋이 내 변경을 되돌린다). 대부분 구현담당1이 통째로 커밋해 줘서 이미 HEAD에 들어 있는 경우가 많다 — status가 깨끗하면 건너뛴다.
2. **CSV 줄바꿈**: `Tools/transcend_phrases.csv`는 CRLF다. python 텍스트 모드로 읽고 쓰면 전체가 LF로 바뀌어 52줄이 바뀐다 — 바이트로 읽고 `\r\n` 보존.
3. **gameshot 첫 판 리로드 무효**: refresh/Apply 직후 첫 gameshot은 「플레이 도중 도메인 리로드」로 무효가 자주 난다. 한 번 더 보내면 된다(NetBoot 씬에서도 첫 판이 그랬다).
4. **NetBoot 씬을 연 채로 두지 마라**: 다른 팀원의 Repair/gameshot이 SampleScene을 쓴다. 촬영 뒤 바로 `씬 열기: SampleScene`.
5. **방장 = 슬롯 0 가정**: 자리 이동 때문에 DifficultySelectHud.IsHost를 `GameAuthority.IsServer`로 바꿨다. 같은 가정이 다른 곳(PlayerContext 0·player_0)에 남았는지 계속 의심할 것.
6. **초월 trait 비움**: 새 스킬이 능력교체형 특성에 덮이지 않게 UnitData.trait를 null로(박민수·강재규 등). 임장혁만 예외(이간질 제거 특성).
7. 재료 별칭은 조합식 `commandId`(= 그 등급 유닛의 별명)로 대조한다 — 스킨 별명(skinAlias)과 다르다.
8. 에디터 `gameshot spawn:` 대신 프로브가 `UnitSpawner.Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0)`를 직접 부른다(노태현·강재규 프로브 참고).
