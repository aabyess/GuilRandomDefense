# 다음 세션 이어받기 — 2026-09-29 (PM 작성, Opus 5.5 세션)

`CLAUDE.md` → `.claude/PROJECT_BRIEF.md` → `.claude/TEAM_RULES.md`를 먼저 읽고 이 문서로 온다.
09-26판은 git 이력에 있다. 이 문서가 그것을 대체한다. **작업이 진행되면 PM이 이 문서를 갱신한다(세션 끝에만이 아니라 큰 단계마다).**

---

## 0. 세션 구성 (09-29)
- **PM**(이 세션) · **구현담당1**(main, UI·이펙트) · **구현담당2**(멀티 mp + 배포 담당) · **Blender**(이펙트 메시·리서치). 리서치담당은 꺼져 있다.
- 유니티 에디터는 main 하나를 셋이 같이 쓴다 → **판·refresh 전후 「판 돌립니다」·「끝났습니다」 필수.** 판 도중 Assets 저장 = 도메인 리로드 = 판 무효(오늘 세 번 당함).
- ListAgents에 같은 이름이 여러 개(원격 오프라인 포함) — `구현담당1 [e95502]`처럼 ref 붙여 보낸다.

## 1. 사장님 확정 규칙 (09-29, 메모리에도 있음)
- **버전 규칙**(release-versioning): 뒷자리=고치기·다듬기(1.2.1→1.2.2), 가운데=큰 덩어리 **완성** 때만(1.3.0 = 「스킬 전체 살리기」 완성), 앞자리=정식 출시급. 친구에게 나가는 빌드는 매번 새 번호.
- **유료 에셋 절대 금지**(free-assets-only) — 무료만, 유료는 후보 목록에도 올리지 않는다.
- **스킬 이펙트는 특별함부터**(skill-vfx-from-special) — 시전자 등급 < 특별함이면 안 띄움(SkillVfx.BeginCast/EndCast).
- 에셋 스토어 「내 에셋에 추가」는 약관 동의라 매번 사장님 확인. 크롬 Unity 로그인은 사장님이 직접.

## 2. 오늘 한 것 (전부 main 푸시, mp는 e02b8b12까지 푸시)
**배포판(1.2.0) 유저 피드백 전부 반영**: 흔함·안흔함 강화 열기(레벨당 공격력+3·공속+5%, 최대20, 10엔) · 시작 특별함 1기 · 표시 이름 「박예원 성대결절」·정보창 「이름 - 등급」(UnitData.DisplayName) · 보스 1.6배(WaveSpawner.BossScale) · A키 빨간 칼 커서 · 게임오버/메뉴 겹침 · 하단 UI 워크3 콘솔(명령 카드 4×3·판매 칸·아이템 2×4·M 이동) · 상단 판매 버튼 삭제 · 미니맵 칸 = 땅 비율(검은 여백 0) · 상점 이름표(ShopNameplateLayer) · 한글 단어 줄바꿈.

**🔴 큰 버그(3e9fc630·6b944848)**: 유닛 프리팹 214개에 UnitIdentity·OwnedByPlayer가 없어 UnitAttacker·UnitMover의 Awake 캐시가 **08-28부터 늘 null** → 등급 강화·특성·스킬 데이터·처치자 번호가 소환 유닛에서 전부 죽어 있었다. **09-29 이전 밸런스 수치(승산표·R20 벽)는 전부 이 버그 위** — 재측정 필요. 메모리 spawn-order-awake-cache.

**스킬 전체 살리기(1.3.0 목표) — 진행 중**
- 1단계 발동 측정: SkillTelemetry(계측) + ClaudeCommands.SkillProbeArena/SkillProbeReport, 구현담당2의 SkillGateProbe(의심 유닛만 오래). 157종 중 121종 → 수정 후 대부분 발동.
- 고친 것: 히든 3종 스킬 YAML 깨짐(be502a04) · 범위 스킬 처치 시 순회 예외로 시전·게이지 끊김(c747afb3).
- 36종 미발동 분류(구현담당2 보고): 빈 껍데기 11종(원래 없음) · 표본 부족 · **채우기 후보**: 희귀 박민수 A095(블러드러스트 공속 버프) · 희귀 이상혁 A06D(트리거 피해) · 초월 김만경 A0HG·박기찬 A069·유재헌 A0EQ·이태훈 A0GR(ACbh 껍데기, 트리거 피해) · 영원 조세민 A0J7(쿠사의 패기).
- **강타형 스턴 전부 누락**, **이감(둔화) kind 자체가 없었음** → SkillEffectKind.Slow(13) 신설(f04207d0), 데이터는 아직 0.
- 기본 이펙트: SkillVfx(Kenney CC0 텍스처 11장, 재질 SkillVfxMaterials.Build → Resources/Effects) — 적중·마법적중·방깎·스턴·이감·버프. 받는 쪽 EnemyDummy 훅. 멀티 복제는 mp e02b8b12. 점검 ClaudeCommands.VfxShowcase(표적 넷).

## ▶ 09-30 진행 (새 PM 세션) — 가동: PM·구현담당1·Blender (**구현담당2는 사장님 지시로 꺼짐, 지시 금지**)
**끝난 것 (main 푸시)**
- 어제 워크플로 결과가 유실돼서 손으로 다시 함 → 문서 셋: SKILL_FILL_STAGE2_seven(Blender) · SKILL_FILL_STAGE2_slow_stun(구현담당1) · SKILL_BASIS_AUDIT(구현담당2) · SKILL_AOE_CENTER(Blender).
- 🔴 **스킬 반경 단위 버그**(4aad0072): range는 원작 단위인데 세계 거리에 그대로 대서 범위 스킬·오라 전부 4.167배 반경이었다 → SkillLevel.WorldRange.
- **범위 중심 = 맞은 적**(aaeb4894): SkillLevel.aoeCenter(기본 Target), 시전자 7개만 1.
- 구현담당1: 오라 Slow·최저 이속(70 원작)·같은 id 공속 버프 갱신(0909227e) · 강타/더미 스턴 91건(b94146cc) · 이감 7건(62132f2f) · 황준석·정준영 분할 파일 값 섞임(88b30fd7). 실측 판 StunSlowProbe 진행 중.
- 구현담당2: 7종(이상혁·배현진·박기찬·김만경·유재헌·김영원 skill_9·조세민 A0J7·박민수 A095) 반영(b643b068·0be24a23). mp 병합·VFX 측정 수정 푸시(14f6fcd9).
- v1.2.1 배포 끝남(09-29 18:10) · release/1.2.1 푸시.
- 이펙트 팩 두 개 **다운로드 완료**(~/Library/Unity/Asset Store-5.x/). Cartoon FX = URP 셰이더 내장, Hovl = 재질 34개가 빌트인 파티클 셰이더 → 우리 URP 파티클로 재질만 바꾸면 됨(유료 지원팩 불필요). 사장님 승인: 쓸 것만 골라 넣기(만화 글자·해골·하트 제외), **스킬 데이터 작업 끝난 뒤**.
**구현담당2가 남긴 것 (PM 이어받음)**
- 체력 비례 누락 21개 전부 미착수: 영원 4(미호크 MIhawk_Mana·우타 Uta_skill_1/_double은 새 게이트 에셋으로 떼기 승인·카벤디시·비비) · 불일치 2(Kick_1 원작 최대체력×0.15↔우리 현재체력×1.0, Sabo_Skill_4 0.04↔0.01) · 초월 9 · 불멸 · 샹크스 3종 소속 확인.
- 겹침 파일 남은 것: 원작능력_영원_최상호 A0HP 둔화 오라(likely) · 게이트_영원_최상호_a8343962(카벤디시) · 더미채널_영원_김정래_1(우타 A16W 스턴 1.5 confirmed).
- 회수_초월_김만경_AD_0ac0451e 라벨 틀림: 원문은 마나≠135 AND 대상 버프 B06B(광폭화 적) → OnHitChance 0.075 + requiredTargetBuffId B06B 제안.
- 이태훈 A0GR: 운석은 별도 SkillData(levels[1] 1/96) · 스톰프 스턴 3초는 likely라 빼고 피해·방깎만.
**진행 중**: Blender = 보스 체력 비례 분기(SKILL_BOSS_BRANCH.md, 보스 36종은 %체력 스킬 0 — 원작은 별도 고정값 분기).
**작업트리 소음**: 폰트 SDF 3개 · SampleScene 빛 14개 URP 자동 추가 — 커밋 안 함.

## 3. ⬜ 진행 중 / 다음
1. **워크플로 `skill-fill-stage2` (run wf_88215736-b55)** — 원작 j/w3a에서 이감 전수·강타 스턴 전수·7종 개별 값 추출 + 제안마다 반박 검증 2명. 결과가 오면 **survived만** Assets/Data/UnitSkills에 반영(disputed는 PM이 근거 재확인). 스크립트: `~/.claude/projects/-Users-sang-GitHub-GuilRandomDefense/b2a0a06b-e05d-4ccf-9d09-6408605e6203/workflows/scripts/skill-fill-stage2-wf_88215736-b55.js`. 계측 주의: 피해 합계를 볼 땐 표적 체력 배율 1e3 이하.
2. **v1.2.1 배포(구현담당2 담당)**: 구현담당1 SkillVfx 다듬기 커밋 → mp에 main 병합 → main `git merge --ff-only mp`(그 사이 main 커밋 금지 공지) → ../GuilRandomDefense-build 에서 release/1.2.1 → BuildBeta 윈도우·맥 → F1·F2·G 꺼짐 확인 → ~/Desktop/구랜디_베타/ zip 두 개 + 안내문 맨 위 「1.2.1v 변경 내역」(초안 승인됨), 1.2.0v 산출물은 이전_1.2.0v로. 이감·스턴 데이터는 1.2.1에 안 넣음(1.2.2).
3. **Blender**: 이펙트 메시 (A)충격파 고리 (B)초승달 검기 (D)번개 기둥 (+C 땅 폭발) — gen_vfx_*.py, Assets/Art/Effects/Meshes. 끝나면 구현담당1이 SkillVfx에 메시 파티클로 연결. 리서치 Docs/research/ONE_RANDOM_SKILL_VFX.md(원작 이펙트 782종, 89%가 맵 커스텀, 모양 다섯 묶음, 상위 등급은 이펙트 3~8개 겹침).
4. **에셋 팩**: Cartoon FX Remaster Free·Hovl Magic Effects FREE 계정 추가됨. 사장님께 패키지 매니저 My Assets에서 **Download만** 부탁(Import 금지) — 배포 뒤 PM이 쓸 것만 골라 넣기(만화 글자·외곽선 폭발 제외).
5. 스킬 전체 살리기 남은 것 → 1.3.0: 체력 비례 공식 확인(SkillEffectBasis.TargetMaxHpPercent 등은 코드에 있음 — 데이터 적용 여부 점검) · 스킬별 이펙트 모양 매핑(원작 능력 ID → 모양 묶음) · **밸런스 재측정**(버그 수정으로 강화·스킬이 처음 실제로 먹음).
6. 원작 곁가지(추정): 조합 상위 유닛 몸의 상시 장식(발밑 룬·날개) — 디테일 체감용, 사장님께 제안 전.

## 4. 굳은 규칙 (누적)
- **새 컴포넌트가 Awake에서 UnitIdentity/OwnedByPlayer를 캐시하면 안 된다** — 소환 뒤에 붙는다. 지연 조회로.
- **스킬 에셋 문자열이 「[」로 시작하면 YAML이 깨진다** — 생성기는 yaml_scalar로 감싼다(be502a04).
- 새 파일은 `git commit -- 경로`에 안 담긴다 → 폴더 단위 `git add` 후 `git status --untracked-files=all`에 `??` 0.
- 파이썬으로 C# 문자열 넣을 때 `\n` 주의. 7z는 RAR5를 0바이트로 푼다.
- 원작 수치는 j·w3a 직접 디코드(Tools/w3x/w3a.py). 빈 필드 = 기반 능력 기본값(0 아님). Docs는 근거가 아니다.
- 판으로 말하기 전에 **도구가 그 행동을 실제로 할 수 있나**부터(오늘도 계측 도구의 시간 멈춤이 가짜 「이펙트 안 보임」을 만들었다).
