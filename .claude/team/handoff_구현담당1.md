# 구현담당1 인수인계 (10-06 오후, 직전 세션 작성) — 읽고 남은 일부터 이어간다. 다 끝내면 이 파일을 비우고 커밋.

## 오늘(10-06) 끝낸 것 — 전부 main에 커밋됨
- **히든·불멸·초월·다른세계 조합은 채팅 전용**: `CombineSystem.IsChatOnly`(결과 등급 Hidden/Immortal/Transcendent/OtherWorld) → 조합 버튼 목록·멀티 요청 검증(NetCommands가 `GetRecipesStartingWith`를 본다)에서 빠짐. 채팅은 `CombineSystem.TryCombineByChat`(GameChatBox.TryExecuteCode → PlayerChat). 받는 문구: commandId의 「/」 양쪽 · 「친구이름 조합」(에셋 이름에서 접두 뗀 뒤 밑줄·공백 무시) · `CombineRecipe.chatPhrase`(초월 수식어). 초월 수식어 틀: `Tools/transcend_phrases.csv`(출처 「원작 임시」/「사장님」) + `call TranscendPhraseImporter.Apply`.
- **히든 19식 사장님 재료 반영**(a16437bc0) + 솔·성탄·뻬꼼 레시피 삭제(하급도박 10%로 얻음) + 조합판 가로 `CombineSizeX 1436`(여은서 재료 6칸). 조합판 Repair는 `call MapGenerator.RepairCombineBoardDryRun` → `RepairCombineBoard`(씬 쓰기, 구현담당2와 순서 맞출 것).
- **초월 3기 사장님 사양 구현**: 최상호 구일(9597323b8: 이감+개수 비례 오라·아머브레이크 부여·소환수 3종 부채꼴·보잡) · 양재모 물리간호사(9304a2fe2: 현재체력·스턴·공속 누적·다한증) · 박민석 외동의악마(0da3e8d7f: 스킬 6 + 유닛삭제 + 공용 디버프). 새 효과: SummonUnit·GrantSkillToAllies·BossDamageMultiplier·AttackSpeedStack·KillNormalEnemies·DamageOverTime(SkillData.cs 열거 맨 뒤, 직렬화 순서 — 구현담당2·3도 이어 붙임). 소환수는 `UnitIdentity.IsSummon` + `UnitSpawner.Spawn(summoned:true)`(인벤토리·판매·조합 재료 제외).
- **교체형 특성 연결 끊기**(UnitData.trait 비움 — 특성 에셋은 그대로, 선택 시 특성 버튼이 안 보임): 박민석·최상호 구일 완료. 박민수(구현담당3)·이재윤(구현담당2)은 각자 Apply에서 `unit.trait = null` 하기로 요청함(PM 지시) — 됐는지 확인.
- **원작 초월 스킬 수치 참고표**: `Docs/research/TRANSCEND_SKILL_REFERENCE_2026-10-06.md`(생성 `python3 -I Tools/transcend_skill_reference.py`).
- **하급도박 bonusPool 실측**(100번 솔2·성탄4·뻬꼼2·상붕카3) · 채팅 문구 「나나미 치아키 조합」「강재규 AP 조합」 · 문구 겹침 0 · 기준선 g1_310~316 표·「support pirate」 원인 분석 보고 완료(PM이 가졌음).

## 전설 스킬(PM 지시 「전설 스킬을 원랜디 전설 스킬과 똑같게」, Docs/research/LEGEND_SKILL_PLAN_2026-10-06.md·csv) — 1차 커밋 2327a261f + 디버프 커밋
**도구(다시 돌려도 안전, 이 순서로 만든 것)**: `Tools/apply_legend_clones.py`(중복 5기: donor 에셋 복사 + 유닛 필드 + VFX 항목 복사) → `Tools/apply_uabi_passives.py --apply <접두>`(MASTER_UID_ROSTER_MAP.csv 행으로 상시 오라 자동 — 이제 AHad PV 조건(sapper/nonancient/nonsapper) 지원, A0EI·A0EH 제외 해제) → `Tools/apply_legend_blocks.py`(평타 블록: 드래곤 Legend3·시저 A0DG·킹 Legend33·슈가 유닛 필드) → `Tools/apply_legend_structures.py`(소환체·시전 능력·크리·지대·DoT·LIFE 오라) · `Tools/apply_shared_debuffs.py`(공용 디버프 4종). 점검 프로브 `Assets/Editor/LegendProbe.cs`(gameshot: `call:ShopSlotProbe.Fund call:LegendProbe.Setup wait:32 call:LegendProbe.Report wait:3.4 call:LegendProbe.Later`).
| 유닛 | 원작 전설 | 넣은 것 | 실측 |
|---|---|---|---|
| 전설적인_김건 | 울티 h03S(복제, 임채현 donor) | 15% 블록·A0FB 공포·LIFE 35 울두건·**A0FC 파키케팔로(PV≥200 크리)**·남의 블록 찌꺼기 삭제 | 스킬 연결만(널 0) |
| 김민규 | 시노부 h042(복제, 이일중) | Legend30 1/7·1/14 스턴·**A0U7 분신 소환체(2초 h085)** | 연결만 |
| 김민준 | 네코마무시 h09Z(복제, 진연서) | LIFE 33 neko·Legend32 둘·A0Y1·A0YT | 연결만 |
| 김정래 | 아마츠키 h087(복제, 임장혁) | 1/7 토키·A0UF 오라 | 연결만 |
| 박병규 | 에이스 h02O(복제, 임장혁) | Legend18·A0DZ 아지랑이 · 찌꺼기(레일리 마나 115) 삭제 | 연결만 |
| 김용태 | 드래곤 h02W | Legend3 1/10 스톰프(500·180,000·스턴 2.75/영웅 0.41) · A0W8(PV>200 방깎 −45·1000)·A0SX·A0ST·A04K 오라 | 스톰프 피해·오라 확인(스턴은 대상이 죽어 미확인) |
| 노태현 | 시저 h038(사장님 확정) | A0DG 지대(0.4초마다 33,333·1000)·A0NU 방깎 −30 | 지대 단독 DPS **미실측**(시저 스탯 24,501/0.72 vs 시저 37,500/0.38 — 스탯 쌍둥이 아님) |
| 양재모 | 킹 h0AH | Legend33 산성탄(85,000 + 초당 42,500×2.7 + 방어 −22·−12)·**A10U 화재 DoT(초당 250,000, 지속 5초는 제안값)**·A132·A133 | 산성탄·독·화재·이감 확인 |
| 구주호 | 슈가 h037 | A0IX 마나 오라 필드·Legend27 소환체(h06N 4.5초·h07H)·**A0J0 장난감화 시전(쿨 105 스턴 3초)** · h06P는 폭발 수치 원문 없어 미반영 | 소환체 1기(장난감 콜렉션) 확인 |
| 그 밖에 빠진 항목 | | 시키 소환체 h07F(신문철)·로우 A0GA(신지우)·카르가라 A08G(박민석)·샹크스 A0PA 지대(백기현)·레이쥬 A08M 독(임건웅)·히루루크 A0RW LIFE 오라(이재윤, 유닛 필드)·에드워드 A0EI/A0EH(최상호, PV 조건 오라) | **A0GA·A08G·시키 소환체·시노부 분신·A0PA·A0RW·A0FC·A0EI/A0EH는 연결·컴파일만, 효과 실측 미완**(마지막 판들이 다른 세션의 리로드로 무효) |
**남은 일**: ① 위 미실측 항목을 gameshot으로 확인(스턴·크리·소환체 공격·지대 DPS — LegendProbe 확장) ② 노태현 시저 지대 실측 DPS를 PM에게 보고(「시저 상시 장판 83k vs 노태현 34k」) ③ 구현담당2에게 「Link 다시」(SkillIconLinker.Link — 새 SkillData·공용 디버프에 icon 연결) ④ 미반영: 보아 A084 피스톨키스 · 징베 해류(스탬피드)·마나 145 출처 없는 스턴 · 시노부 B06B 1/4 추가 굴림 · 히루루크 벚꽃 광역(보류) · 슈가 h06P · 레이쥬 투사체 셋 ⑤ 새 호출 규칙: `unit_fields`가 UnitData 필드를 정규식으로 쓴다(필드 순서가 유니티 재직렬화로 바뀌어도 이름으로 찾음).
**공용 디버프 4종**(`Assets/Data/UnitSkills/SkillData_공용_디버프_{외동,01,조씨,문}.asset`): 외동 = 주변 600 적 이속 +15%(Slow 배수 1.15, 박민석 외동의악마 skills에 있음, 김민준(구현담당2)도 쓸 예정) · 01 = 이 유닛 공격력 −15%(Self AttackPowerBuffPercent −0.15) · 조씨 = 주변 600 적 방어 +5(ArmorBonus) · 문 = 이 유닛 공속 −15%. 설명은 사장님 원문 그대로. 아직 쓰는 유닛: 외동만(박민석). **강재규 「아군발 디버프 개수」(UnitAttacker.CountAllyDebuffs)가 지금 세는 것**: AuraBonus 중 AllyMoveSpeedDebuff 또는 AttackPowerBuffPercent<0인 것뿐 — 01(공격력 −15%)은 세고, 외동(적에게 건 Slow)·조씨(적 ArmorBonus)·문(공속 −15%, AttackSpeedBuffPercent)은 안 센다. 디버프 해제(DispelAllyDebuffs)는 같은 판정의 것만 지운다. 문도 세려면 IsAuraDebuff에 `AttackSpeedBuffPercent && value<0`을 더하면 되고, 01·문을 「자기 단점이라 안 센다」로 하려면 반대로 뺀다 — 사장님 확인 필요.

## 함정 (오늘 새로 알게 된 것)
- 🔴 **에디터 순번**: 에디터 하나를 구현담당2·3·PM과 같이 쓴다. refresh·gameshot 전 「씁니다」, 끝나면 「끝났습니다」를 꼭 보낸다. 남의 소스 편집·refresh가 내 gameshot을 「플레이 도중 도메인 리로드」로 무효로 만든다(오늘 7번) — 같은 소스 파일(SkillData·UnitAttacker·GameHud)을 여럿이 고치니 편집 전 알릴 것. gameshot이 「씬 NetBoot」으로 돌면(구현담당3이 촬영 후 안 되돌림) `call G3SceneMenu.OpenSample`로 되돌린 뒤 재시도.
- 🔴 **새 Editor 파일은 저장 직후 `Tools/ui/csc_check_editor.sh 2>&1 | grep -ci " error"`가 0인지**(tail 금지 — 마지막 줄은 늘 done). 컴파일 오류가 Assets 안에 남으면 모든 팀원 gameshot이 거절된다(오늘 MinseokApply 중복 Make로 한 번 겪음).
- 공용 파일(SkillData.cs·UnitAttacker.cs)은 같은 작업 폴더에서 여럿이 편집 — 파이썬으로 읽고 바로 쓰는 짧은 수정만, 커밋은 통째로(남의 hunk 포함 동의 받음). `SkillEffectKind` 서수는 열거 순서(SummonUnit 16 · DamageOverTime 26 …) — YAML을 손으로 쓸 땐 서수를 열거에서 직접 센다(DoT를 17로 잘못 써 한 번 되돌림).
- 에셋을 파이썬으로 쓸 때 유니티가 다시 직렬화한 에셋(description이 여러 줄 \u 이스케이프)은 `sat.set_head_field`가 첫 줄만 바꿔 깨진다 → 정규식으로 이어진 줄까지 통째로 바꿀 것(외동 에셋에서 한 번 겪음).
- 이름이 같은 복제본 충돌: `apply_legend_clones.py`는 찌꺼기 삭제를 복제보다 먼저 한다(`더미채널_전설적인_박병규_1` 이름이 겹쳐 한 번 지워졌다).
- 소환 유닛 데이터: `Assets/Data/Units/Summons/Summon_*.asset`(흔함_강재규를 본떠 복사, isSystemUnit, 사거리 = 원작÷5.5). 소환체 사거리·prefab은 임시(아트 없음).
