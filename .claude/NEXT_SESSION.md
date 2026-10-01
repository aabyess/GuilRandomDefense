# 다음 세션 이어받기 — 2026-10-01 오후 (PM 작성, Opus 5.5 세션)

`CLAUDE.md` → `.claude/PROJECT_BRIEF.md` → `.claude/TEAM_RULES.md`를 먼저 읽고 이 문서로 온다.
09-30 판은 git 이력(이 문서의 직전 판)에 있다. 이 문서가 그것을 대체한다. **작업이 진행되면 PM이 이 문서를 갱신한다(큰 단계마다).**

---

## 0. 세션 구성 — 넷 다 가동
| 세션 | 맡는 것 |
|---|---|
| **PM** | 사장님 지시 수신·분배·리뷰·푸시 · 맵 재생성·배포 빌드·mp 병합 · 스킨 Assets 반영(모델 배선) |
| **구현담당1** | 스킬 데이터·UnitAttacker·EnemyDummy(이 파일들의 주인) · 밸런스 측정 도구(ClaudeCommands autoloop) |
| **구현담당2** | (10-01 사장님이 다시 띄움) 브금·고유 동작 반영·상시 오라(UnitSphereArt) |
| **Blender** | 스킨 생성기(fix_unit_fbx·gen_*)·원작 모델(MDX) 추출·조사 — Assets엔 안 씀, 산출은 `~/GRD_motion_trial/` |

- 새 세션은 서로 기억이 없다 → **첫 지시에 배경과 에디터 규칙을 같이 준다.** ListAgents에 같은 이름이 여럿(오프라인)이면 `이름 [ref]`로.
- 🔴 **유니티 에디터는 main 하나를 같이 쓴다.** 판(플레이)이 도는 동안 Assets에 파일을 쓰면 도메인 리로드로 그 판이 오염된다 → 쓰기·refresh·모델 배선 전 「돌립니다」, 상대의 「끝났습니다」를 받은 뒤에만. 10-01에도 사장님이 다른 세션에 직접 시킨 일이 판 도중 Assets를 건드려 두 번 오염됐다 — **사장님이 직접 시키셔도 쓰기 전에 알린다.**
- 모델 배선은 로스터 240·적·프리팹 수백 개를 다시 쓴다 → 시작 전 `git status`로 남의 미커밋 변경이 없는지.

## 1. 사장님 확정 규칙 (누적)
- 🔴 **버전은 0.x**(베타). 뒷자리 = 고치기, 가운데 = 큰 덩어리 완성, 친구 빌드는 매번 새 번호. **0.3.0v 배포 완료(10-01).** 다음은 0.3.1(고치기) 또는 0.4.0(큰 덩어리).
- **유료 에셋 절대 금지**(후보에도 안 올림). **스킬 이펙트는 특별함부터**.
- **전부 원작대로, 우리 창작은 조합식뿐** — 원작 근거 있는 정정은 묻지 않고 한다.
- **스킬은 원작에 있는 유닛에만, 원작 스킬만, 위 등급부터.** 「원작엔 없는데 우리엔 있음」은 지우기 전에 보고.
- **한 로스터가 원작 유닛 여럿을 받는 대응은 합친 채 둔다**(다시 묻지 않음).
- **스킨 원본의 고유 동작을 쓴다** — 10-01 시범(영원_최상호) 사장님 통과(「괜찮아 보이는데」·「이대로 진행」), 공격은 제자리판.
- **브금은 첫 화면부터 1라운드까지만**(2라운드 시작 때 페이드). 곡은 사장님이 준 30초 binks_sake, 볼륨 0.5(어림값).
- **원작대로 등급 오라**(초월 발밑 HandsAura2 · 히든 발밑 BlightwalkerAura) + 캐릭터 상시 오라. 그리고 🆕 **원작 초월 유닛 모델에 들어 있던 부가 이펙트(손 빛·번쩍임·날개 등)를 뜯어서 우리 초월에 붙인다**(10-01 「초월 유닛 스킨들에 추가된 부가적인 이펙트를 뜯어서 우리 초월에」).
- 날개: 원작에서 초월 전체에 붙는 날개는 **없다**(스크립트상 특정 배틀넷 아이디 칭호로만 · 원작 초월 모델 6종 렌더에도 날개 없음). 사장님께 보고함 — 부가 이펙트 추출에서 날개 모양이 나오면 그걸로.
- 배속 측정: 「이미 검증된 앞 라운드는 배속, 집중할 라운드부터 1배」(10-01) — 도구 수정 중(§3).

## 2. 10-01까지 한 것 (전부 main 푸시 — 커밋 해시는 git log)
- **0.3.0v 배포**: mp에 main 병합 → 빌드 사본 `../GuilRandomDefense-build`(브랜치 release/0.3.0)에서 배치 빌드(맥 OSXUniversal·윈도우 Win64) → `~/Desktop/구랜디_베타/`(zip 둘·맥 앱·안내문 — 이전 판 전부 삭제, 사장님 지시). 그 뒤 mp를 main에 병합(7988acb8). 🔴 함정: 배포판 첫 씬은 NetBoot라 `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` 설치는 게임 씬에서 안 돈다 — `sceneLoaded`에도 걸 것. 빌드 확인은 `-mpSolo -mpSaveDir <폴더> -mpSoloMenuAt 25 <사진.png>`로 띄워 사진.
- **스킬(구현담당1)**: 전 등급 채우기(영원~특별·랜덤·변화·히든 대응분) · 평타 광역(스플래시 22·클리브 7) · 평타 다중 대상 7 · 보스 스턴/이감 영웅 지속(저항 피부 23) · 구조 다섯(주기 피해 지대·장풍 직선·배타 분기·근접 진입·평타 다중) · 작은 축 다섯 · uabi 상시 58. 집계 반영 405 → **583** / 빠짐 520 → 344. 등급별 `Docs/research/*_FILL_LIST.md` 끝 「구현 기록」.
- **소리**: 스킬 효과음 79(SkillSfx, `Tools/skill_sfx/`) · 획득 음성 66(SummonVoice) · 브금(GameBgm, 1라운드까지).
- **섬 가장자리**(IslandShores, 런타임 장식): 울퉁불퉁한 절벽·바위 둔덕·물속 턱(바다 셰이더가 얕은 물·물거품을 그림). **앞치마 폭 = 레인 폭**으로 맵 재생성(c73c1294 — 건물 양옆 바다 홈 제거, navlane 0 확인).
- **스킨**: 로스터 240종 전부 적용(10-01 아홉 추가: 호시노_루비·김정래·이타도리·헬로우먼(CC-BY → Docs/CREDITS.md)·장명자·임재현·황길라(발광 끔)·장진희·BJ_율희(꼬리 곡선)) · 색 죽은 재질 수정 넷(루피·조도연·탄지로·황길라). 남은 미적용은 로스터 밖 레일리 h05X·메타몽 h0BS뿐.
- **고유 동작 11종**: 영원_최상호 + 10종(문필환·김건_AP·강주혁_AP(무기)·임장혁_AD·이승우(무기)·구주호·이정범·박민수·박예원·조현규) — Generic 자체 컨트롤러 Idle·Move·Attack.
- **상시 오라 구조**(UnitSphereArt·SphereArtTable, 71e141d1): 등급 오라 둘(원작 모델 모양) + 캐릭터 오라 36줄(**임시 모양** — Blender 변환 대기).
- **밸런스**(`Docs/research/BALANCE_2026-09-30.md`): 보통 R50 통과 판 둘 / R30·R34 패배 판(도구 배치 탓 포함). 상위 목표(aim:불멸)는 흔함 공급 부족으로 불멸 0, aim:전설은 R28에 전설 1. 등급 사이 결투 격차 수백 배.

## 3. 세션별 앞으로 할 일
### PM
1. 이 문서 시작 때 `git status -sb` · ListAgents · 세 세션에 배경 전달.
2. **사장님 답 받기**(§4) — 특히 스킬 주인 질문 표, 히든·다른세계 원작 대응.
3. **멀티 두 창 확인**(0.3.0v 빌드로): 스킬 효과음·스킬 이펙트·획득 음성·브금·오라가 친구 화면에도 나는지 — `~/Desktop/구랜디_베타/멀티_테스트_두창.command`(GRD.app 경로는 mp-build — 릴리스 앱으로 바꿔 쓸 것).
4. 윈도우 빌드 실행 확인(지금까지 빌드 성공만 봄).
5. 다음 배포(0.3.1/0.4.0): mp 병합 → release 브랜치 → 배치 빌드 → 앱 사진 확인 → `~/Desktop/구랜디_베타/` 정리.

### 구현담당1
- 상태: 스킬 채우기(불멸~특별·구조 5축·이감 영웅 지속·구조 뒤 빠진 조각) · 집계 405→583 · `Docs/research/BALANCE_2026-09-30.md`(§1~5) · 도구 토큰 `aim:<등급> · fast:<배율> · focus:<라운드 구간>`.
- 밸런스 결론: 보통 R20 벽은 사라짐(여섯 판 다 통과). 이후는 도구 한계가 가름 — 쫓지 않으면 R30~R50에서 지고(`bosschase`면 R50 통과), 도구는 희귀함까지만 조합. 불멸 1기는 R50 보스를 1.8~47초에 혼자 잡고 영구 스턴 76~99%(BossDuelProbe). aim:불멸 두 판 R45·R47 패배(전설 0), aim:전설 R40까지 전설 1기.
- 배속: 충실도는 1배와 같지만 **속도 이득 미미**(R1~R10: 1배 476초 · 2배 442초 · 4배 399초) — 병목이 도구 턴의 실제 시간 작업(클릭마다 프레임 대기·카메라 이동). 지금은 **aim 판은 1배로**. 배속을 살리려면 도구 한 턴을 줄여야 함(행동이 바뀌니 기준선 재측정).
1. aim:전설 R60 1배 한 판(에디터 창 앞에 두고 — 느리면 rounds:60이 87분 상한에 잘림) → 전설 2기↑면 aim:불멸 한 판.
2. 도구 병목: 흔함 선택 위습을 「모자람 ≤3」 이름에 몰아주기 · 막힌 이름 흔함 안 팔기.
3. 설계만 적힌 구조(레벨·T강화·토글 갈래 · 소환체 · 변신 — 변신은 사장님 판단).
4. 미해결 의문: 레이쥬 1기가 6판째 피해 73% · 오타쿠의길 57%의 정확한 이유.
5. 구현담당2 요청 시 UnitAttacker SkillVfx.BeginCast 세 곳에 `UnitSphereArt.PulseSkill(초)` 한 줄.
- 함정: 판 도중 Assets 쓰기 = 「오염」 판 무효 · `EditorApplication.update`에서 Time.deltaTime 합산은 1/20쯤 — Time.time 차로 · BossDuelProbe 피해 칸은 처치된 줄에서 날 피해 · w3a를 setdefault로 읽으면 레벨 1 값만 · 표식 수는 상한(구조 29표식 중 진짜 if/elseif 짝 8) · **이 저장소에서 `git stash` 금지**(worktree 셋이 stash를 공유, 한 번 작업트리가 걷혔다) · zsh `set -- $n` 분리 안 됨.
- 기억 파일: structure-axes-2026-09-30 · balance-2026-09-30 · skill-fill-2026-09-30-night.

### 구현담당2
1. **초월 부가 이펙트 파일럿**: Blender 산출이 오면 키자루(초월_구주호_AD)부터 — FBX/파티클 프리팹을 `Resources/Effects/Sphere/<아트키>.prefab`(키 = 원작 파일 이름 소문자·확장자 없음), `Tools/sphere_art/extra.tsv`에 줄(로스터⇥키⇥뼈⇥위치⇥회전⇥크기⇥항상|공격|스킬) → `build_table.py` → 사진 → 나머지. 파티클은 Blender 설명값으로 ParticleSystem 자작(무료·자작만).
2. 「스킬 중」 보이기 — UnitAttacker의 SkillVfx.BeginCast 세 곳(1059·1137·1475행 근처)에 `UnitSphereArt.PulseSkill(초)` 한 줄을 **구현담당1에게 요청**(그 파일 주인).
3. 적 셋(주영호·왕승환·김민준안경) 고유 동작 — 적은 공용 Humanoid 컨트롤러 경로라 따로(메모리 enemy-clips-via-humanoid).
4. 확인 못 한 것: 「처음 화면으로」 뒤 브금 재시작 · 멀티 클라 브금.
- 도구: `Assets/Editor/MotionShowProbe.cs`(`gameshot x.png 1 1280x720 call:MotionShowProbe.<이름> wait:1.5 snap:파일…` — 이름 Munpil·Kimgun·Juhyuk·Janghyuk·Seungwoo·Juho·Jeongbeom·Minsu·Yewon·Hyeongyu·Sangho·AuraTrans·AuraHidden·AuraChar) · `MotionAxisProbe.cs`(진단) · `AuraPerf.Start/Report`(60기 FPS) · `call:UnitSphereArt.Describe` · `call:GameBgm.Describe`.

### Blender
1. **초월 부가 이펙트 마무리**: 원작 초월 모델 61개 분석·렌더는 끝(`~/GRD_motion_trial/초월_부가이펙트/_렌더/` 191장, 판정표 `설명표.md` — 쓸 만함: 마르코 날개 mrk7 · rokugu_tr4 잎 날개 · AkainuBW7 마그마 팔 · lb_jimbe 별빛 · lb_kz 얼음 칼날 · Franky baozha 타원 등). **미완: 부품 FBX 추출 · 로스터별 폴더 · 한눈 모음 그림(사장님께 보여 드릴 것) · 파티클 값 설명.**
   🔴 대응 문제: 61개 중 우리 로스터에 대응되는 모델은 27개뿐(대부분 받이 로스터 초월_황준석_ADAP) — 쓸 만한 모델 대부분이 미대응 → **어느 로스터에 붙일지 사장님 결정 필요**(한눈 그림과 같이 여쭐 것).
2. **상시 오라 25모델 변환**(`Docs/research/SPHERE_ART_BY_ROSTER.csv`): 맵 내장 18 중 0 착수. 맵에 없는 7(RifleImpact·Malmter Qixuan/Judgement·VampiricAura·1gun9birth·AvengerMissile·Banshee)은 목록만 — 유니티에서 기존 이펙트로 대신.
3. 대기: 우솝 지팡이 포즈(ⓐ옆 ⓑ손 ⓒ등) · 황길라 동작판(variant — 반영 시 base 승격).
- 도구: `python3 Tools/w3x/mdx_effect_parts.py <ord.mpq> <work> <models.json>`(분석) → `ANALYSIS=analysis.json blender -b --factory-startup --python Tools/blender/render_mdx.py -- <work> <out> <이름.mdx>…`(렌더) · `mdx_extract.py`(MPQ에서 꺼내기). 스크래치(ord.mpq·mdxwork·transwork)는 사라질 수 있음 — 위 도구로 재현.
- 함정: 파티클(PRE2/PREM)·리본은 FBX에 못 넣음 → 값 설명으로 · 워크3 기본 텍스처(Blue_Glow2 등)는 맵에 없어 근사 · 자동 몸/이펙트 분류는 불완전 → 렌더를 눈으로 · 가산 재질 `_add` 접미, 팀색 판 버림 · Blender python엔 mpyq·Pillow 없음(추출은 시스템 python) · fix_unit_fbx `variants["이름@변형"]`은 Assets 밖 --out으로만, 반영 확정되면 base로 승격 · check_entries 남은 실패 3(강재규 텍스처 원본 없음·상붕카 glb 소품·이호준 그대로).

## 4. 사장님 답 대기
1. **스킬 주인 질문 표** `Docs/research/SKILL_OWNERSHIP_QUESTIONS.md`(주인 다름 17 · 원작에 없는 값 10 · 대응표 질문 7 — 지운 것 없음).
2. **히든·다른세계 유닛이 원작 누구인지**(남은 빠진 스킬 344의 큰 몫).
3. 초월 부가 이펙트 — 미대응 모델(마르코 날개 등)을 어느 로스터에(Blender 한눈 그림 나오면).
4. 이펙트 메시 여덟(스킨에서 뺀 것 — `~/GRD_motion_trial/이펙트후보/`) 중 살릴 것.
5. 우솝 지팡이 ⓐ옆 ⓑ손 ⓒ등 · 영원_문필환 지상 폼/비행 폼(지금 지상) · 김건 노란 털 크기·박예원 웅크린 자세(사진 확인).
6. 히루루크 광역 최대체력(원작은 본체 사망 대가) · 변신 구조(「합친 채」와 부딪침) · 변화됨 규칙(원작 15라운드부터·한 판 2회).
7. 브금 볼륨 · 획득 음성 PM 배정 24 · 스킬 효과음 볼륨/빈도(PM은 소리를 못 들음).
8. 옛 대기: 김건부=적 R12와 같은 모델 · 브로리 화풍 · 이즈미 왼 소매 · Sketchfab Standard 14건 유료 여부 · NC-ND 1건(특별함_주영호).
9. 밸런스 조정(영구 스턴 로스터 여섯·등급 격차·후반 적 체력) — 측정은 우리, 숫자 결정은 사장님.

## 5. 저장소·작업 폴더
- main 전부 푸시. 작업트리에 늘 남는 것(커밋 금지): Pretendard SDF 2 · Anton SDF · `.check_entries_out/`.
- worktree: `../GuilRandomDefense-mp`(mp — main과 같은 데까지 병합됨) · `../GuilRandomDefense-build`(release/0.3.0 — 배포 빌드 전용, 빌드 부산물 커밋 금지) · `../GuilRandomDefense-mp-build`(멀티 테스트 앱).
- 저장소 밖: `~/GRD_motion_trial/`(Blender 산출: 신규_*·수정_*·동작판·원작_오라날개·초월_부가이펙트·이펙트후보) · `~/GRD_original_sounds/`(원작 소리 + _확장팩) · `~/Desktop/구랜디스킨모음/`(스킨 원본) · `~/Desktop/구랜디_베타/`(배포판).

## 6. 굳은 규칙·함정 (누적)
- **새 컴포넌트가 Awake에서 UnitIdentity/OwnedByPlayer를 캐시하면 안 된다** — 소환 뒤에 붙는다(오라 설치기는 UnitIdentity.Active를 훑음).
- **스킬 에셋 문자열이 「[」로 시작하면 YAML이 깨진다** — yaml_scalar.
- 새 파일은 `git add -- 경로` 후 `git commit -- 경로`. zsh에선 경로를 배열로, 한글 경로는 `git -c core.quotepath=false … -z | xargs -0`. **폴더 .meta도 커밋**(10-01 새 스킨 폴더 아홉의 .meta가 빠졌었다).
- 원작 수치는 j·w3a·w3u 직접 디코드(Tools/w3x). 빈 필드 = 기반 스톡값. **능력 ID 주인은 uabi 소유 유닛 → 대응표로.** **「반영됨」은 블록 단위로 대조하기 전엔 믿지 않는다.**
- 스킨 반영 순서: refresh(컴파일) → `menu Tools/아트/모델 배선` → `ArtBinder.PendingLinkUnits`에 이번 유닛 이름 → `call ArtBinder.LinkTexturesUnits` → refresh → `units 파일 이름`(키 30·바닥 0·몸 위쪽). 커밋엔 Art/Units·Art/Materials·Prefabs/Generated·Data/Units·Data/Enemies.
- 🔴 고유 동작(Generic) 유닛: ① `UnitModelPostprocessor.GenericRigUnits`·`BlenderClipPostprocessor` Roots/MoveLoopRoots 등록 → refresh 둘 → 그 뒤 옛 fbx·meta 지우고 새 파일(순서가 바뀌면 Humanoid로 먼저 읽힘 — meta `animationType: 2` 확인) ② **`ArtBinder.AlreadyUprightModels`에 이름**(안 넣으면 AutoUpright가 망토·날개·무기 때문에 90° 눕힘 — 10-01 다섯 종).
- FBX 재질 함정: glb 「그림 없이 색만」 재질이 회색으로 죽음 · 거울 복사 메시 법선 · **EmissiveColor가 factor 없이 실려 몸 전체가 허옇게 빛남**(황길라 — zero_emission).
- 탐침: 표적 체력 ×1e6이면 작은 피해가 float 눈금에 묻힘 · 광역 한 방에 죽는 적은 gameshot spawn 판에 「맞은 0대」로 찍힘 · 판으로 말하기 전에 **도구가 그 행동을 실제로 할 수 있나**부터.
- 에디터 명령: inbox 파일 하나에 gameshot 한 줄 · 공백 든 버튼은 click:이 못 읽음(오브젝트 이름으로) · 스크립트 고친 뒤 refresh하고 dll이 새로워진 뒤 다음 명령.
