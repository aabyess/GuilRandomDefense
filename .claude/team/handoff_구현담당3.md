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
