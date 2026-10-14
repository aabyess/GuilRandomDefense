# 구현담당2 인수인계 — 2026-10-14 세션 교체 (스킨 교체 24기·키 맞추기·크레딧 E 뒤)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md` 순서. 작업 장소는 **worktree `../GuilRandomDefense-w2`(브랜치 w2, 에디터는 내 것)**. 거기서 커밋(`git add -- 경로` → `git commit -m … -- 경로`), push 금지(PM이 병합), **SampleScene.unity 수정 금지**, main 새 커밋은 `git merge main`(PM이 ff해 주기도 한다). 브리지 파일 접두 `g2_`, 이름은 매번 새로 + `rm -f outbox/이름.txt`(와일드카드 rm은 zsh에서 「no matches」 오류 — 이름을 그대로). 커밋 끝줄 `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. 🔴 gameshot 중에는 소스(.cs)를 건드리지 말 것 — 에디터가 재컴파일하며 그 건이 「촬영 실패」로 남는다(10-09 호시노 아이 건).

## 끝낸 일 (git log 제목으로 찾는다, 전부 w2)
- **원작 스킨 교체 24기** — `Assets/Editor/SkinSwapImporter.cs`: `SwapAll`(전체 반입; model_fixed 우선, 없으면 model) · `RelinkAll`(텍스처·재질만 다시) · `RehideAll`(프리팹 새로 지은 뒤 SkinClipHider 숨김 메시 + 클립 도중 알파를 다시 채운다) · `ApplyTimedAlpha`(~/GRD_skin_swap/클립중_알파변화.json → SkinClipHider.timed, 대상: 양재모·박민석·김영학·임건웅) · `SwapOne`(ClaudeBridge/g2_swap.txt = 「로스터|팩 폴더」). 팩·목록: ~/GRD_skin_swap/(교체목록.csv, <팩>/model_fixed/·clip_map.json). 반입 전 로스터 에셋이 있어야 한다.
- **슬롯→텍스처 표**: 슬롯 이름(BIGMOM7_g0_L0_cut)과 텍스처 파일명(mm03.blp)이 달라 10여 기가 무텍스처였다 → 팩 json의 메시→텍스처·필터를 `slot_textures.json`으로 유닛 폴더에 쓰고 ArtBinder.LinkTexturesFor가 읽는다(필터 none=불투명, 그 밖=알파 컷). blender 알파 재추출 55+5개 반영.
- **초월 키 맞추기**: ArtBinder.TierHeightMultipliers(유닛별 배수 한 곳, Head 뼈 중간값 49.7 기준) + 빅맘 ×1.4(카이도는 이미 1.7). `HeightRebind.Run`(표 유닛 프리팹만 재조립 + RehideAll). 실측 도구 `UnitHeightProbe`(Head 뼈·경계), 줄세우기 사진 `UnitLineupShot`(g2_lineup.txt 첫 줄 이름 + 로스터 이름들), 동작 시트 `SwapMotionSheet`(g2_motion.txt). 전후 사진 ClaudeBridge/shots/g2_lineup_T_before·after.png. 거구 후보(흰수염 등)는 사장님 답 대기.
- **컷인 15기 재반입**(C_all 최신판, Tools/cutin/import_cutin.py --src ~/GRD_cutin_swap/layers).
- **크레딧 시안**: C 보물지도·D 엔딩 크레딧·E 현상수배는 `Assets/Scripts/UI/CreditsConcepts.cs`, CreditsSplash.Variant 기본값 **4(E, 사장님 확정)**, 미리보기 `CreditsPreview.ShowA~E/FreezeA~E`(플레이 중 call). E 결함 셋은 코드는 고쳤다(현상금·역할 칸을 종이 안으로, 행 간격 압축, `PlayerNotification.OnGUI`가 `CreditsSplash.Active`면 숨김) — **고친 뒤 처음부터 끝까지 다 나온 한 장은 아직 못 찍었다**(PM이 요청). 찍을 때: `gameshot … call:CreditsPreview.ShowE wait:2.4 snap:…`(로고·줄이 다 나오는 건 약 3.3초, 페이드아웃은 3.6초부터 — 3.2~3.5초에 찍을 것).
- **연출 자동검사**(Tools/cinematics/auto_check.py): batch_05 v2 교체·batch_04·남은 목록 122건 중 약 40건까지 돌렸다. 결과 로그가 /tmp에 있다가 사라졌다 → **통과 기록(CINEMATIC_PASSED.txt)은 앞 18건뿐**. 다시 돌릴 것(이미 반입된 건 ImportOne이 덮어쓴다). id 목록 ~/GRD_scenes/check_logs/ids_1009.txt(= batch_05 44 + batch_04 15 + 남은 것 순서), 대본 폴더 ~/GRD_scenes/scripts(batch_05는 auto_v2에서 덮어 복사 끝, 원본 백업 scripts_bak_1009). 건당 약 30초(에디터 하나 독점). 화면 70% 넘게 덮는 것(실패 판정)은 PM이 목록으로 달라 했다. 통과 뒤 `CinematicImporter.LinkPassed`.

## 남은 일
1. **0.3.17 배포판에서 교체 스킨 유닛이 안 보임(사장님 제보, 에디터에선 보임)** — 🔴 최우선. 빌드 사본에서 프리팹은 정상(렌더러·재질·텍스처 붙음). 짚이는 것: ①**SkinnedMeshRenderer 컬링 경계** — 에디터는 경계를 매 프레임 갱신하지만 빌드는 임포트 때 굳은 localBounds·루트본 기준이라 blender 변환 FBX(키가 크거나 루트본이 멀리 움직이는 것, 1/4프레임 굽기 전 구판)에서 화면 밖으로 컬링될 수 있다 — `updateWhenOffscreen=true`로 켜서 빌드로 확인(UnitHeightProbe는 이걸 켜고 재서 에디터에서만 정상이었음). ②**알파 컷 재질 셰이더 변형**(_ALPHATEST_ON·AlphaToMask)이 빌드에서 스트립 — 재질 키워드가 있는데 그 변형을 쓰는 씬/Resources 참조가 없으면 빠진다(필터 transparent 슬롯을 컷으로 바꾼 것이 이번 변경이라 의심). Always Included Shaders·셰이더 변형 수집 확인. ③애니메이션 압축·`Optimize Game Objects`처럼 임포트 설정 차이 — blender가 Anim. Compression Off 권장(현재 meta는 animationCompression 1). ④프리팹 Resources 로드 경로(UnitData.prefab 참조는 정상). 확인 순서: 빌드에서 그 유닛을 소환해 렌더러 enabled·bounds·카메라 앞인지 콘솔/탐침으로 찍는다.
2. **24기 재수출 FBX 재반입**(가프 머리 뒤집힘 조사): ~/GRD_skin_swap/<팩>/model_fixed/*.fbx·*.json(쿼터니언 연속 + 1/4프레임 굽기) → `Assets/Art/Units/<로스터>/<로스터>.fbx`로 같은 이름 덮어쓰기(로스터 목록 ~/GRD_skin_swap/_재수출검증.csv 첫 두 열), 각 fbx.meta `animationCompression: 0`, 재임포트 → `RehideAll`(클립 이름은 .meta clipAnimations에 남아 있다, 확인할 것). 끝나면 불멸_박은석 Spell로 머리 증상을 `SwapMotionSheet`로 다시 본다.
3. **조로 반입**: ~/GRD_skin_swap/박민수_조로/ (S2 !0702_Zoro, model_fixed + clip_map.json) → `SkinSwapImporter.Swap("특별함_박민수", 팩 경로)`(g2_swap.txt = 「특별함_박민수|박민수_조로」, SwapOne). 🔴 **연출 대본 5개는 반입만, 연결 금지**(PM 결정, 특별함_박민수는 스킬 없음 — 새 필드 vertexColor·animIndex·move도 지원 안 함). 옛 zoro14 판은 폐기라 들어간 적 없다. 이후 RehideAll·check_prefab_refs.
4. **클립 중간 알파 4기 사진 확인**: SkinClipHider.timed는 코드·프리팹(양재모 6·박민석 2·김영학 9·임건웅 3건)은 들어갔지만 **동작 사진으로 안 봤다**. 특히 브룩 Attack 2의 0.468초 g7↔g8 교체. 김영학 「spell alternate three」는 임포트 클립이 아니라 건너뛴다(경고).
5. **크레딧 E 완성 사진 한 장**(위) · **체력바·선택 원·사거리 원이 바뀐 키(초월 배수·빅맘)를 따라가는지 게임 사진** 확인(콜라이더·에이전트만 코드로 확인).
6. **연출 자동검사 이어서**(위 id 목록). 실패 일부는 blender에 돌려보낼 목록으로: 랜덤_이민형 Tatsumaki_R_Attack(덮음 0.72·검은 판), 랜덤_한마_바키 Byakuya_E/R(0.56~0.57), 호시노 아이 BronyaMotar_R(내 컴파일 때문에 촬영 실패 — 재촬영).
7. 아직: 교체 24기 연출 연결(스킨 교체 대본은 CINEMATIC_PAIR_EXTRA.tsv에 짝을 적어 뒀고 `LinkPassed`가 읽는다) · 컷인 적용 후 인게임 컷인 사진 · 배포 0.3.18 포함 목록은 PM 확인.

## 함정 (이번 작업에서)
- **BindOneUnit/ArtBinder가 프리팹을 새로 지으면 SkinClipHider가 사라진다** — 키 맞추기 때 12기가 잃었다. 새로 짓는 도구 뒤엔 반드시 `SkinSwapImporter.RehideAll`(HeightRebind는 끝에 자동 호출).
- 슬롯 이름과 텍스처 이름이 다른 팩은 이름 맞추기가 조용히 실패(「짝을 못 찾은 머티리얼」 로그가 대화상자에만 남는다) → slot_textures.json 경로 필수. SwapAll 출력에서 그 문구를 grep해 볼 것.
- 경계 높이(렌더러 bounds)는 소품이 섞여 몸 크기가 아니다 → Head 뼈 높이로 본다. 백분위 정점 높이는 노이즈가 커서 쓰지 말 것.
- SwapAll은 로스터 폴더를 지우고 다시 만든다(내 손 수정 날아감). 텍스처만 고칠 땐 RelinkAll.
- 줄세우기(UnitLineupShot) 사진은 카메라가 뒤집혀 좌우가 반대(목록 순서가 오른쪽→왼쪽).
- auto_check는 통과 목록을 **끝나야** 파일에 쓴다 — 중간에 끊으면 /tmp 로그에서 직접 복구해야 한다(이번에 /tmp가 날아가 못 했다). 로그는 처음부터 ~/GRD_scenes/check_logs/에 쓰게 리다이렉트할 것.
- 산출물은 /tmp가 아니라 ~/GRD_*(PM 공지), 큰 산출물은 Docs/ARCHIVE_INDEX.md에 한 줄.

---------- 아래는 10-08 저녁 인수인계(그대로 유효한 함정 포함) ----------

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
