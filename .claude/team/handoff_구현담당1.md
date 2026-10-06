# 구현담당1 인수인계 (10-06 저녁, 직전 세션 작성) — 읽고 남은 큐부터 이어간다. 다 끝내면 이 파일을 비우고 커밋.

## 오늘(10-06) 끝낸 것 — 전부 main에 커밋됨
- **전설 지대·미반영** 실측(LegendProbe, SoloSetup/SoloMark/SoloEnd): 노태현 A0DG 지대 92.9k/초(설계 83k ✓)·백기현 A0PA 168k/초(상한값, 평타 섞임)·최상호 A0EH −15 ✓(A0EI 보스 −45는 근처 보스 없어 미확정)·신지우 A0GA +150%·박민석 A08G +400% ✓. 미반영 3건 구현(4d01a8aa8): 징베 A001(쿨 7초·반경 225·초당 750k DoT 2초 — 해석) + 정윤식 마나 145 블록 스턴 1.0 삭제 · 레이쥬 거품광선 투사체 둘 · 보아 A084(평타 7.9% +50,000 + 0.4초 공속 +47% — 해석). 보류: 시노부 B06B 1/4 · 슈가 h06P(수치 없음). **A0FC 크리·A0RW LIFE 오라는 실측 안 함.**
- **엄태웅 중사(진)**(46554c6ab·4d01a8aa8): 스킬 4(타고난신체·포박·주특기·강도높은트레이너) + 폭탄제조(명령 카드 칸 FlexKind.Bomb · 목재 1 · 5,000,000 방어 무시 · 반경 500 · 연타 0.5초, 값은 SkillData 「폭탄제조」 한 칸) + 웅교교주(칸 GambleBoost · 엔 10000 · 도박 +4%p · PlayerContext 개인 누적 최대 5회) · unitName 「중사(진)」 · trait 비움 · 입력말 태웅사마. 유물은 아래.
- **김건 잃어버린웃음보따리**(223c3e6c8, 사장님 확정): 스킬 6 · 새 kind Knockback·FormChange · SkillEffect.formSelfAttackSpeed · WaypointMover.PushBack · UnitAttacker.UnitDeleteCount/GunFormActive · 채팅 구두점 무시 · 변화됨 김건 「New건」. **지배자의싸인**(R003, 스토리 6 우타의 헤드셋 자리 대체, 같은 커밋).
- 소환체 prefab 재연결 도구 `Tools/relink_summon_prefabs.py`(모델 배선 뒤마다 한 번).
- **설계표(사장님 확정 포함)**: `Docs/research/TRANSCEND_{TAEWOONG,GUN,MANGYEONG,JUHO,GYEONGHYEON}_DESIGN_2026-10-06.md` · `IMMORTAL_DESIGN_2026-10-06.md`(불멸 8종 + §0 공통 팔레트 + §「사장님 확정」 + §10 유닛회유 + §11 세부).

## 남은 큐 (이 순서) — 에디터 순번은 구현담당2·3과 협의
1. **김만경 윤식파해결사 구현**: 설계표 확정(「다 추천대로」). **준비물이 `Tools/wip_g1/`에 있다**: `mang_patch.py`(SkillData.cs enum 맨 뒤 `SkillDamagePerHighGradeUnit` + UnitAttacker `HighGradeSplashFactor`·ApplyAttackSplash 한 줄 — **적용 전에 현재 UnitAttacker.cs·SkillData.cs에 맞는지 확인**, assert 실패하면 문자열을 현재 파일에 맞춰 고칠 것) · `MangyeongApply.cs`(→ Assets/Editor/) · `MangyeongProbe.cs`(→ Assets/Editor/). 순서: 소스 패치 → `Tools/compile_check.sh | grep -c " error"` 0 → `echo refresh` → dll이 새로워진 뒤 `call MangyeongApply.Apply` → `call TranscendPhraseImporter.Apply`(csv에 김만경 「구일의집행인」·구주호 「힘법사」가 이미 있음 — **미커밋일 수 있으니 git status 확인**) → gameshot `call:ShopSlotProbe.Fund wait:30 call:MangyeongProbe.Run wait:1.2 call:MangyeongProbe.After` → 커밋.
2. **구주호 주호리얼**: 새 kind 없음. `Tools/wip_g1/JuhoApply.cs`(→ Assets/Editor/). 방깍 오라 −30(특성 −40·공격력 +20% = skills[0] 레벨 2) · 그래플러(스턴 1/6·405·1.0초) + 암브(=아머브레이크 단일 −9, 평타 1/8) · 오라오라!(범퍼 최대체력 1%, 평타 25%, **방어 무시 armorIgnoreRatio 1 — PM 새 규칙, JuhoApply에 이미 넣음**) · 죽지않은노장 = 특성강화 이름 · trait `Trait_초월_구주호_AD`(skillLevelUnlockIndex 1, cost 3) 연결 · 재료에서 희귀 배성령 삭제.
3. **김경현 상호파주방장**: 설계표 확정(`TRANSCEND_GYEONGHYEON_…`) — 액티브 끝딜 22%(대상 지정·쿨 60·방어 무시)·몹삭제 쿨 180초 + 노획물(**원작 노획물품 h056 한 종류, 이름만 치킨·떡볶이·돈까스·토스트·맥주 5개**, 판매 37% 위습·그중 40% +100엔·목재 1)·특성 2pt = 노획물 50% 1개 더. 새 코드 `SkillEffectKind.GrantLoot` + 노획물 ItemData 5 + 판매 규칙. 재료: 안흔함 엄태웅 교체 + 특별 유재헌 추가. 아직 코드 없음.
4. **불멸 8종**(정윤식·김용태·정준영·이승우·신지우·박은석·고도현·이이삭): `IMMORTAL_DESIGN_2026-10-06.md`가 정본(사장님 확정이 위쪽에 있다). 새 코드: 유닛회유(RecruitEnemy·IsRecruit·판매 경로) · 이이삭 방깍 비례(DamagePerTargetArmorShred) · 김용태 적 사망 훅 · 고도현은 구현담당2의 `needsAllyClick`. 바다이동·전지역이동·순간이동은 구현담당2 FlyingMover. 재료 교체: 김용태(제한 박성호 → 전설 박성호) · 박은석(NPC = 변화됨 박은석, 이미 그렇다).
(참고) 구현담당3이 %체력 피해를 방어 무시로 일괄 수정할 때 내 에셋 줄을 건드린다고 먼저 알린다.

## 함정
- 🔴 **에디터 순번**: 「씁니다」·「끝났습니다」를 꼭 보낸다. 새 에디터 파일(Assets/Editor/*.cs)도 만든 뒤 **바로 refresh** — 컴파일 안 된 새 파일이 있으면 남의 gameshot이 「플레이 도중 도메인 리로드」로 무효(오늘 세 번).
- 🔴 `csc_check`/`compile_check` 마지막 줄 done에 속지 말고 `| grep -c " error"`. 새 Runtime 소스 고친 뒤 Editor csc_check는 옛 Assembly-CSharp를 봐서 거짓 오류가 난다 — refresh 뒤 다시.
- gameshot 탐침: 적 체력을 1e9로 두면 float 눈금(64)에 작은 피해가 묻힌다 → 1e8/5e6. 적을 유닛 곁으로 데려오려면 `NavMeshAgent.Warp`. 공용 파일(SkillData·UnitAttacker·GameHud)에 남의 미커밋 hunk가 섞이면 `git diff -U3` 후 내 hunk만 `git apply --cached`(키워드 필터)로 스테이징 → 인덱스만 `git commit`(오늘 223c3e6c8).
- 셸 heredoc에 백틱이 있으면(따옴표 없는 heredoc) 명령으로 실행돼 글자가 지워진다 — python 본문은 `<<'EOF'`로.
- 구현담당3이 UnitAttacker.UpdateAuraTick(스킬 레벨 바뀌면 오라 재적용)·강재규 마나 끝딜을 고쳤다 — 김만경 패치 전 UnitAttacker 현재 모양 확인.
- 불멸 식은 재료 3칸(위습 없음) · 입력말은 초월 임포터(초월 전용)가 아니라 Apply가 `chatPhrase`를 직접 쓴다.
