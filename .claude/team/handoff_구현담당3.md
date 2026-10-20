# 구현담당3 인수인계 (2026-10-20 마감 — 세션 교체, 이 역할은 다시 안 띄움)

> 아래 10-06 본문은 그날의 인수인계(함정 목록은 여전히 유효). 이 절이 최신이다.

## 10-18~20 끝낸 것
- **S2 규칙 9종·맵 목표물·오로성·진화체·에그헤드·모드 토글·S2Anchors·S2Features 스위치(가드 cd57c7b5c)** — 사장님 10-19 방침(구버전 원랜디 따라감)으로 main에서 되돌림. **전부 `s2/parked` 브랜치에 보관**(필요하면 거기서 꺼냄). 상세 표는 그 브랜치의 옛 handoff_구현담당3.md(b71e8049a)에 있음.
- **S2 영상 리서치**: ~/grd/GRD_s2_map/영상분석_2.md (상점 열 6채 확정 · §F 못 밝힌 구역: 조합 구역 섬 이름·허브 어두운 원·오른쪽 위 4칸·퀘스트 NPC·룸바 배/포네글리프·봉인섬/#0/모래 대륙, 창고 4칸 테두리 근거 없음).
- **구버전 레인 설계(10-20)**: ~/grd/GRD_oldmap_lane/ — 레인_설계.md(띠별 표 §6)·레인_시안.png·textures/(ambientCG CC0 4종, LICENSE.txt)·frames/(영상 6KOsgr1BLLo 16장). 배치는 영상 2020 기준(웅덩이 모래 남쪽), 구현은 구현담당1.
- **성능 점검(10-20)**: ~/grd/GRD_perf_1020/보고.md(핫스팟 상위 15·빌드 측정)·표_0.3.34.md. 병합된 커밋: 256b4aedd UnitFacing 캐시 · e4cb89780 SkinClipHider · 48ee8993c SkillVfx.Burst 커서 · 445466607 UpdateSkillCooldown 쿨다운 스킬 캐시(UnitUpgrades.Version) · b5a2e9897 검증 탐침 CooldownEquivProbe(에디터 검증 통과: 일치·Diff 0.081초) · 6db0c7c46 CharacterAnimator 클립 배열 · a79336c88 RegenPercentNow · 0dccefd0d UpdateAuraTick 목록 재사용(perf/1020b, main 병합).

## 남은 일
1. **보스 라운드·R25~31 끊김 원인 미확인**(프로파일러 필요): 0.3.34 솔로 측정에서 R17(평균 37)·R20 보스(최저 FPS 7, 208ms)·R29~31(R30 보스 최악 399ms)·R36~38(적 11~16마리, 평균 40대). 적별 피격 이펙트 상한(0.12초)·평타 적중음 음소거·텔레메트리 꺼짐은 이미 확인 — 타격당 이펙트로는 설명 안 됨. 에디터 프로파일러(CPU 타임라인) 또는 빌드에 라운드별 Burst·TakeDamage 횟수 로그 한 줄로 확인할 것.
2. 보고.md 핫스팟 중 **미착수**: #6 UnitCombat 적 탐색(레인별 버킷)·#12 UnitAttacker Tick* 7개 묶기(규칙 시간 정밀도 확인 필요)·#11 SkillVfx.TickAttached 간격화·#13 UnitSphereArt Setup(1회 보장 확인). UI 몫(#14·#15, 구현담당4)은 제외했음.
3. 성능 측정 한계: 빌드에 autoloop 없음 → `-mpHost -mpAutoStart 1 -mpTestUnits 120 -mpDifficulty Easy`(흔함 120기, 이펙트 수 로그 없음)로 대체. 측정 스크립트 ~/grd/GRD_perf_1020/table.py(로그→라운드별 FPS 표).
4. (s2/parked와 함께) S2 쪽 미완: 니카 유닛 없음(오로성 퇴치)·이세계 기본값 결정·LK+10 마법저항 미구현.

## 함정 (이번에 추가)
- 워크트리 `../GRD-wt-g3`(g3/s2-anchors·g3/s2-off)·`../GRD-wt-perf2`(perf/1020b)·`../GRD-wt-handoff`가 남아 있다 — 필요 없으면 `git worktree remove`. main 폴더에서 커밋하지 않으려고 쓴 것.
- `Tools/ui/csc_check.sh`는 **주 저장소 Library/Bee rsp**를 쓴다 — 에디터가 재컴파일 중이면 CS0006(ref.dll 없음)이 쏟아진다(환경 문제, 잠시 뒤 재시도). `csc_check_editor.sh`는 낡은 dll이라 새 멤버를 못 찾는다(무시).
- `pkill -f "<패턴>"`은 그 패턴을 담은 자기 셸 명령까지 죽인다 — 패턴을 변수로 쪼개거나 PID로 종료.
- 빌드 앱(GuRandi_최신_맥.app)은 새 배포가 덮어쓴다 → 측정 전 zip을 따로 풀어 두기.

---

# 구현담당3 인수인계 (2026-10-06 밤, 세션 교체)

규칙·환경은 CLAUDE.md·TEAM_RULES·`.claude/team/구현담당3.md`. 에디터는 여럿이 하나를 같이 쓴다(「씁니다/끝났습니다」). 사장님 확정 답은 모두 아래 설계표·문서에 「✅ 사장님 확정」 절로 적혀 있다.

## 오늘 끝낸 것 (전부 커밋, 컴파일 오류 0, 신 모드 gameshot 실측)
| 일 | 커밋 | 한 줄 |
|---|---|---|
| 임장혁 짱스 실측 + 결함 3 수정 | b53dd7585 | manaGaugePerMana 0(상한 0이라 악보완성! 불가)→1 · 특성으로 스킬 레벨 바뀌면 옛 오라 효과 안 떼던 것(UnitAttacker.UpdateAuraTick 레벨 교체 처리) · 고충해소 자기 포함(Self 효과) |
| 신 기준 능력치 감사표 | 585a4155d·afcde5c46·c130250dd | Docs/research/TRANSCEND_GOD_BALANCE_AUDIT_2026-10-06.md (사장님 확정 표시 + 방어 반영 재계산) |
| 강재규·노태현 | 6dab5fbb5 | 깡딜 40만(3대악질·돌발행동) · 강재규 끝딜 → 마나 스킬(게이지 125, 잃은 체력 5%, 보스 상한 해제, 체력 게이지와 별개) |
| 박기찬 최고의선생님 | d49655ba4·59994c4b6(CSV) | 스킬 6(방깍40 오라·암브 −7·파트너 공속 +50% 오라·강간 스턴·무언가의분출 체력 50 둔화 0.25·만능플레이 멀티샷 attackExtraTargets 3) |
| 두유찬 보스의앞잡이 | 65ea047fb | 스킬 3(혼신의일격 체력 게이지 50 최대체력 7% 방어 무시·짓밟기·폭언) · 임채민 재료 전설→희귀 |
| 이태훈 윤식파군기반장 | 90b8e8cc4 | 스킬 4(방깍35 오라·이감 −20% 오라·약자멸시 마나 140 몹삭제[잃은 체력 큰 순, 일반 적만]·명치적중) · 김용태 재료 전설→특별함 · 새 소스 SkillEffect.killMostLostHp + UnitAttacker.MostLostHpNormalEnemy |
| %체력 스킬 방어 무시 일괄 | f469fc2dd | 박민석 흑인·양재모 상호파의최강자·박민수 인싸 + (두유찬·이태훈 처음부터) — 원작 j RRD 약 700건 중 UNIVERSAL 513건(73%) |
| 설계표 | 7dafbafd4·089725db6·c67476bce·5c7def1ef | 박기찬·배성령·두유찬·이태훈(「사장님 확정」 절 포함) |

## 남은 큐 (PM 순서)
1. **이재윤 체육특기생재윤(현재체력 25%, AD) 방어 무시** — 구현담당2의 JaeyunApply.cs:72에 `armorIgnoreRatio = 1f` 한 줄. 구현담당2에게 요청해 둠(했는지 `python3 -I` 스캔으로 확인: scratch의 scan_pct.py 방식 — `SkillData_사장님_*.asset`에서 damageType 1 + basis 최대/현재/잃은 체력 + armorIgnoreRatio 0인 행 찾기).
2. **고정 AD 깡딜 5건 방어 무시 결정 대기(PM·사장님)** — 박민석 Devil 40만 · 박민수 인싸 보스 30만 · 엄태웅 타고난신체 50만 · 최상호AP 절대공격 2M · 이재윤 보스 30만. 감사표 c130250dd 표 참고, 내 권장 = 일괄 방어 무시.
3. **배성령 투명색으로얼룩진감정 구현**(설계표 TRANSCEND_SEONGRYEONG_DESIGN, 사장님 확정): 새 소스 큼.
   - 방무뎀 50% = **적 방어의 50%를 무시**(평타·스킬 피해 계산 때 방어 ×0.5, 실피해 ≈1.8배): `UnitData.attackArmorIgnoreRatio`(맨 뒤 필드 0.5) → UnitAttacker 평타·스킬 방어 계산 한 곳. SkillEffect.armorIgnoreRatio는 이미 있음.
   - 끝딜 = 마나 115 → 잃은 체력 5% 보스 상한 없음(강재규와 같은 틀) · 이감(평타 12.5% 0.5·3초) · **순간이동 = 명령 카드 액티브 + 지점 클릭 → NavMeshAgent.Warp**(쿨 12초 제안): 새 `SkillEffectKind.TeleportToPoint`(enum 맨 뒤, **공용** — 유재헌·신지우·박은석·전법규도 쓴다) + `SkillLevel.needsPointClick`. GameHud 5번 칸 액티브(구현담당2 NetHudAction.CastActive)·`TargetAreaIndicator` 지점 지정 흐름(도움소 지점 칸·항해일지 탐색)을 따라 만들 것. NetCommands 멀티 RPC 필요할 수 있음 → PM과 상의.
   - 재료: 노태현 → 임장혁(전설적인_임장혁), 희귀 배성령 제거 → 4칸(임장혁·이재윤·히든 미소야·초월위습). 입력말 인간의탈을쓴암살자 · 칭호 투명색으로얼룩진감정(긴 이름 UI 폭 확인). trait 비움.
4. **영원함 설계표**(구현은 사장님 질문 답 뒤): 원문 Docs/research/SPEC_IMMORTAL_ETERNAL_OTHERWORLD_LIMITED_2026-10-06.md의 「영원함」 절 — **8종**(김영원·조세민·이지원·문필환·서민성·김정래·윤현모·최상호 킹카; PM 메모의 10종은 틀림). 윤현모는 스킬 이름만(스킬 없음), 김영원은 스킬 1(토토 Style)·이름 1. 설계표 먼저 → 질문은 PM에 모아 사장님 웹 질문지로. 방침은 초월과 같다.

## 파일 위치
- 적용 도구: `Assets/Editor/{Janghyuk,Jaegyu,Notaehyun,Gichan,Duyuchan,Taehun,Minseok,Minsoo,Nurse}Apply.cs` + `…Probe.cs`(gameshot `call:`로 부른다). 새 초월은 `GichanApply.cs`(가장 단순한 틀)를 본보기로. 설계표 `Docs/research/TRANSCEND_*_DESIGN_2026-10-06.md`, 큐 `TRANSCEND_QUEUE_2026-10-06.md`(정본).
- gameshot 예: `ClaudeBridge/inbox/g3_<접두>_<번호>.txt`에 `gameshot 이름.png 3 1600x900 mode:신 wait:2 call:XProbe.Setup wait:1 call:XProbe.Targets … call:XProbe.Report`. 난이도 `mode:신`은 도구가 지원(ClaudeCommands.cs:1399). 결과는 `ClaudeBridge/outbox/<같은 이름>.txt`.
- 입력말 CSV: `Tools/transcend_phrases.csv`(CRLF 보존 — 바이트로 읽고 쓸 것). 한 줄 형식 `조합식 에셋 이름,칭호,commandId,chatPhrase,출처`. 작업 파일엔 구현담당2의 신문철 줄이 미커밋일 수 있다.

## 함정 (오늘 겪은 것)
1. **탐침 표적 체력 ×1e9 = float 눈금에 작은 피해가 묻힌다**(박기찬 실측 「맞은 수 0」) → `Initialize(data, 1e3f)`(6e8). 체력 비례 효과의 감소율(%)을 볼 땐 ×1e3로도 충분.
2. **Apply 직후·refresh 직후 첫 gameshot은 「플레이 도중 도메인 리로드」로 무효**가 잦다 → 한 번 더 보낸다. 다른 세션이 소스를 편집·미컴파일 상태로 두어도 같다(오늘 구현담당2 미커밋 소스가 원인) — 판을 돌리기 전 `git status --short Assets/Scripts`로 남의 미커밋 소스를 본다.
3. **outbox 이름이 옛 파일과 겹치면 옛 결과를 읽는다**(g3_1006_j4 사건) → 매번 새 접두+번호.
4. **공용 파일 커밋(UnitAttacker·SkillData·GameHud)**: 남의 미커밋 hunk가 같이 들어간다 → HEAD 내용에 내 치환만 적용해 만든 blob을 임시 인덱스(`GIT_INDEX_FILE`)에 `git update-index --cacheinfo`로 올리고 `commit-tree` → `update-ref HEAD`, 진짜 인덱스는 `git reset -q HEAD -- 경로`. 스크립트는 세션 scratch(`commit_mine2.sh`)에 있었으나 세션 교체로 사라질 수 있음 — 원리만 기억. CSV처럼 한 줄만 내 것일 때도 같은 방식.
5. **AD 피해(평타 포함)는 표적 방어를 탄다**(계수 1/(1+0.02×방어), 신 R60 보스 317.6 → ×0.136). %체력은 방어 무시(armorIgnoreRatio 1)로. AP 피해는 방어를 안 탄다(상성 배율만 ≈0.86~1.035).
6. 마나/체력 게이지 유닛은 **manaGaugePerMana 1**이 필수(0이면 상한 0). UnitData.manaMax 0 유닛은 마나 게이지 자체가 없다. 마나 게이지와 체력 게이지는 별개 카운터(한 유닛이 둘 다 가능, 실측 확인).
7. ArmorBreak(암브)는 영구 누적(원작 AId1 합계 상한 −75). 오라 방깍은 `ArmorBonus` 음수 + 범위 안에서만. 멀티샷 = `UnitData.attackExtraTargets`(= 총 수 −1) + `attackExtraTargetRadius`.
8. 스킬 YAML 문자열이 `[`로 시작하면 깨진다(yaml-inline-list-trap). 새 enum 값은 맨 뒤.
9. 재료 별칭은 조합식 `commandId`로 대조(스킨 별칭과 다름) — 오늘 두유찬(임채민 채민파리더=희귀)·이태훈(김용태 흑화=특별) 재료가 식에서 어긋나 있었다.
