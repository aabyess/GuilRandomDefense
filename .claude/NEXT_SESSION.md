# 다음 세션 이어받기 — 2026-09-30 (PM 작성, Opus 5.5 세션)

`CLAUDE.md` → `.claude/PROJECT_BRIEF.md` → `.claude/TEAM_RULES.md`를 먼저 읽고 이 문서로 온다.
09-29판은 git 이력에 있다. 이 문서가 그것을 대체한다. **작업이 진행되면 PM이 이 문서를 갱신한다(큰 단계마다).**

---

## 0. 세션 구성
- **PM** · **구현담당1**(스킬 데이터·UnitAttacker·EnemyDummy) · **Blender**(스킨·원작 j/w3a 조사 — 읽기 전용 조사도 맡는다).
- 🔴 **구현담당2는 꺼짐 — 지시 금지**(사장님 09-30). mp 병합·배포는 PM 몫. 리서치담당·구현담당3도 꺼짐.
- ListAgents에 같은 이름이 여럿(원격 오프라인) — `구현담당1 [ref]`처럼 ref를 붙여 보낸다. 새 세션은 서로 기억이 없다 → 첫 메시지에 배경을 준다.
- 유니티 에디터는 main 하나를 같이 쓴다 → **판·refresh·모델 배선 전후 「돌립니다」·「끝났습니다」**. 모델 배선은 로스터·적 에셋을 다시 쓰니 구현담당1에게 「커밋했음」을 받고 돌린다.

## 1. 사장님 확정 규칙 (누적)
- **버전**(release-versioning): 뒷자리 = 고치기, 가운데 = 큰 덩어리 **완성**(1.3.0 = 「스킬 전체 살리기」), 친구 빌드는 매번 새 번호.
- 🔴 **배포는 스킬을 다 넣은 뒤**(사장님 09-30 「스킬 다 넣으면 배포할거라」). 1.2.2 중간 배포 안 함.
- **유료 에셋 절대 금지**(free-assets-only). **스킬 이펙트는 특별함부터**(skill-vfx-from-special).
- **전부 원작대로, 우리 창작은 조합식뿐** — 원작 근거 있는 정정은 묻지 않고 한다(오늘 방어 하한 −20 삭제도 이 규칙).
- **스킬은 원작에 있는 유닛에만, 원작 스킬만** 넣는다. 채우는 순서는 **위 등급부터**(영원 → 불멸 → 초월 → 제한 → 히든 → 전설 → 희귀 → 특별 → 안흔함). 「원작엔 없는데 우리엔 있음」은 지우기 전에 사장님께 보고.
- **변화됨 스킨 = 재료 유닛 스킨 그대로**(원작 7종 중 5종이 재료와 같은 모델, Docs/research/TRANSFORMED_GRADE.md).

## 2. 09-30 한 것 (전부 main 푸시 · mp는 5c103cd7에 main 4a47eb04까지 병합·푸시, 두 창 확인은 안 함)
**스킬 발동 버그(큰 것)**
- 🔴 스킬 반경 단위: range는 원작 단위인데 세계 거리에 그대로 → 범위 스킬 4.167배(면적 17배). `SkillLevel.WorldRange`(4aad0072).
- 🔴 범위 중심 = 맞은 적(원작 133개 중 107): `SkillLevel.aoeCenter`, 시전자 7개만 1(aaeb4894).
- 스턴 91·이감 7·오라 Slow·최저 이속·같은 id 공속 버프 갱신(구현담당1). 실측 원작 일치.
- 보스 %HP 게이트 제거 + 효과마다 원작 PV 조건(260424bc), pointValue 4종 200으로(7d483620), 감수성 계수 효과별(6c775d24).
- 방깎 상한 AId1 합계 −75(159f16c0) · 방어 하한 −20 삭제(7964f612, ⚠️ 후반 피해 ↑).
- 원작능력_* 출처 검사·정정 → 값 440 중 439 원작에서 찾음. 배타 분기 45짝 → 0, 「게이트 없음」 11에셋 → 0, 영웅 스탯 11기 원작대로, 마나 재생 umpr 축(32a10cfa, 측정 전).
- 도구(재사용): Tools/audit_origin_provenance.py · audit_bundle_exclusive.py · audit_ungated.py · sync_hero_stats_from_w3u.py · sync_mana_regen_from_w3u.py. 탐침 Assets/Editor/BossPercentProbe(R50/R10, ArenaR50Lv10·Lv20) · StunSlowProbe · VfxSkillProbe.
- R50 보스(10.6M) 처치 상위: 신지우 ~6초(원작 ~7) · 김만경 ~9~13 · 최상호_영원 ~11 · 홍인창 ~11(원작 ~10) · 카마도 ~13 — 원작 대비 튀는 유닛 없음. 영웅 레벨 1/10/20 차이는 흔들림 안.

**이펙트**
- 무료 팩 55종 Assets/ThirdParty(Cartoon FX 25 · Hovl 30, Hovl 재질 URP 변환 ThirdPartyVfxUrp) — d2b4df72.
- **스킬별 이펙트 표** SkillVfxTable(특별함 이상 373 스킬 · 팩 42종, Docs/research/SKILL_VFX_MAPPING.csv) — 적중/범위/시전자 칸, MP RPC_SkillPrefabVfx(27c981d5). 🔴 **멀티 두 창 확인 아직**.

**스킨** (라이선스 전부 미확인 — 사장님 Sketchfab 확인 대기)
- 변화됨 4종 = 재료 스킨(ca1b44b8) · 다른세계 고죠(구슬 제거)·나나미(에노시마 준코)·한마 유지로(안흔함_김용태가 쓰던 것 이동)·모리야 스와코(요우무 저폴리 — ⚠️ 화풍 차이, 사장님 판단 대기, 알파 컷 AlphaCutUnits)·무면허 라이더 · 랜덤_호시노_아이(최애의 아이) · 초월위습_박은석(Kuma Slave).
- 🔴 **단독 「텍스처 연결」 메뉴 금지** — 적 모델 63·다른 유닛 .meta까지 리맵해 재질 480개를 만든다. 새 스킨은 `call ArtBinder.LinkTexturesUnits`(PendingLinkUnits 목록에 이름 추가) 또는 LinkTexturesFor. 스킨 커밋엔 Assets/Art/Materials 같이.

## 3. ⬜ 다음 (순서대로)
1. **다른세계_올마이트 스킨(Blender)**: 원본 12_다른세계/다른세계_올마이트.zip(USDZ, My Hero One's Justice 2 추출). Blender USD 임포터로 열림 · 메시 5 · 169뼈 진짜 스킨 · 🔴 뼈 이름이 숫자(n9…)라 22뼈 대응을 위치·계층으로 짜야 함 · bbox z 0.73~2.1(발 쪽 빔?) 확인 · 젊은 올마이트와 비교 렌더. 끝나면 PM: PendingLinkUnits 갱신 → 모델 배선 → LinkTexturesUnits → `units` 사진 → 커밋.
   - ✅ 안흔함_김용태 = **젊은 올마이트**(09-30 반영, CC-BY-4.0 victordavi1606 — 🔴 **크레딧 표기 필요**: sketchfab.com/3d-models/jovem-all-might-199adeacf32c4383b14841eb65fab8de). 재질 5개 텍스처 미연결(material_0.002 · 40_Mesh2 · 7_000x-Plane.003 셋) — 화면상 티 안 남, 한 번 확인. 70,959삼각형(부츠 4.4만) — 무거우면 decimate. 쿠마 PX-69는 gen_biped_skin RETIRED_SKINS로(재사용 가능).
   - ⚠️ 호시노 아이 Spine1이 0-길이 자리표시(Blender 지적) — 유니티에서 이상하면 3항 override로 다시.
2. **스킬 전수 조사(Blender)** — 시작 전(스크래치 cov/units.json 원작 유닛 314·abils.json 능력 912는 사라졌을 수 있음, cov/units.py·abils.py로 다시). 연결 키: SkillData 이름·설명의 원작 uid(h0AC)·능력 코드(A0KT) + MASTER_UID_ROSTER_MAP: 원작 전 유닛 「스킬 있음/없음」 → Docs/research/SKILL_COVERAGE_BY_GRADE.csv + 요약. 지시문은 09-30 PM→Blender 메시지 그대로(등급별 표 · 빠짐 위 등급부터 · 히든 3/22·다른세계 0/9·특수함 2/6 확인). 사장님 승인 완료.
3. **빠진 원작 스킬 채우기(구현담당1)** — 위 등급부터.
4. **마나 재생 판 측정(구현담당1)**: 징베·아오키지·우타·황정기·배성령 발동 간격 vs 원작 툴팁. 체력 재생 uhpr 스톡값 확인 뒤 LIFE 게이지에도?
5. **새 구조가 필요한 원작 스킬** → 1.3.0: 「적이 근처에 오면」(샹크스·핸콕 TriggerRegisterUnitInRange) · 배타 분기 한 굴림(지금 에셋 둘로 평균 근사) · 레벨/토글 갈래(랜덤 스킬 강화 A0KT·영웅 스킬 포인트 A0B7·히루루크 A0K9) · 각성 모드 · 비비·우솝 식 · 킹 B03P 다타 · 분신(시노부) · 장풍 직선 · 더미 이감 오라 5 · AHad 오라 vs 특성 방깎 모양.
6. **변화됨 규칙**(사장님 판단 대기): 원작은 15라운드부터·한 판 2회 한도 — 우리엔 없음.
7. **밸런스 재측정**(오늘 스킬 크게 바뀜) → **멀티 두 창 이펙트 확인** → mp 병합 → **1.3.0 배포**.
8. 스킨 남은 것: 다른세계 호시노 루비·김건부·브로리 · 특수함 6 · 히든 맥주만땅·미소야·이요한 · 랜덤 이즈미 신이치·이타도리(보류) · 영원 서민성·김정래. 사장님이 원본을 주시면 skin-workflow대로.
9. Mixamo 동작(공격 여러 벌·시전·죽음) — 사장님 다운로드 대기(~/Downloads/mixamo). 받으면 공용 Humanoid 컨트롤러에 AD/AP 공격·스킬 시전·적 죽음.

## 4. 굳은 규칙 (누적)
- **새 컴포넌트가 Awake에서 UnitIdentity/OwnedByPlayer를 캐시하면 안 된다** — 소환 뒤에 붙는다. 지연 조회로.
- **스킬 에셋 문자열이 「[」로 시작하면 YAML이 깨진다** — 생성기는 yaml_scalar로 감싼다(be502a04).
- 새 파일은 `git commit -- 경로`에 안 담긴다 → 폴더 단위 `git add` 후 `git status --untracked-files=all`에 `??` 0. zsh에선 경로 여럿을 변수 하나에 넣지 말고 배열로.
- 파이썬으로 C# 문자열 넣을 때 `\n` 주의. 7z는 RAR5를 0바이트로 푼다(bsdtar/unar는 됨).
- 원작 수치는 j·w3a 직접 디코드(Tools/w3x/w3a.py). 빈 필드 = 기반 능력 기본값(0 아님). Docs는 근거가 아니다.
- 🔴 **판정표·메모의 능력 ID도 uabi 소유 유닛 → MASTER_UID_ROSTER_MAP으로 확인**(오늘 여섯 강타가 전부 남의 유닛 것이었다). 「값이 원작에 있다」 ≠ 「그 유닛 것이다」.
- 공격 트리거 문맥의 GetTriggerUnit() = **공격받은 적**, GetAttacker() = 시전자.
- 스킨 교체는 옛 FBX·.fbx.meta를 먼저 지우고(humanDescription 잔존 → T자). glb는 부품 수·Image_N·root 회전·뼈 꼬리 확인.
- 판으로 말하기 전에 **도구가 그 행동을 실제로 할 수 있나**부터.
