# 구현담당2 인수인계 — 2026-10-06 밤 (선택 위습·조합판/뽑기섬 간격·바지사장·스킬 아이콘)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md`의 구현담당2 항목 → 필요하면 아래 문서 순서로 읽는다. 전부 main에 커밋됨(푸시는 PM).

## 오늘 끝난 것 (커밋 해시는 git log에서 제목으로 찾는다)
- **선택 위습 공간**: ① 흔함 칸 위습을 지름×1.25 격자로 깔기(`RewardDistributor.FindSpreadSlot`, 포탈 접촉은 캡슐 가로 거리−월드 반지름으로 직접 잼 — OverlapSphere 버퍼·ClosestPoint 함정) ② 흔함선택 포탈 판정 r13.5→18(`ChoicePortalReach.cs`, 런타임) ③ **흔함선택 포탈은 「이 포탈을 겨눈 위습」만 먹는다**(`UnitPortal.TryConsume`+`AimedAtMe`, Enter+Stay — 스쳐 가다 엉뚱한 포탈에 먹히던 것, 진짜 클릭 6/6) ④ B안: 위습 생성 자리↔포탈 줄 29→60, 흔함 칸 깊이 74.4→108.75(`MapGenerator.ChoiceWispSpawnGap`·`GachaChoiceRowExtraZ`).
- **조합판 줄 앞뒤 간격 ×1.3**(`MapGenerator.CombineRowPitch`, RowPitch는 조합판 짓는 동안만, `MapLayout.CombineSizeZ` 유도). ×1.4는 바다 끝 노출로 철회(남쪽 여유 211, 카메라 한계 −3372).
- **뽑기섬 랜덤유닛 전시**: 한 줄 7→5칸(`DisplayColumns`, 옆 ×1.4), 줄 사이 ×1.3(`DisplayRowPitchZ`), 섬 깊이 유도식 `GachaDisplayExtraZ`(= (줄수+빈줄)×배율−3)×24 → 726×1325, 여유 63.
- **초월 최상호 바지사장**(초월_최상호_AP) 구현·실측 완료: 새 구조 둘 — `SkillTriggerType.ActiveButton`(UnitAttacker.TryCastActive/ActiveSkill/쿨, 명령 카드 5번 칸·단축키 **Q**(S는 정지), MP는 `NetHudAction.CastActive`) · 「공속 비례」(`SkillEffect.attackSpeedScale/Cap`, UnitAttacker.AttackSpeedScaleFactor). 적용 도구 `Assets/Editor/BajisajangApply.cs`, 점검 `BajisajangProbe.cs`(Setup→SelectIt→Report1~3·HoverIcon). 설계표 `Docs/design/BAJISAJANG_DESIGN_2026-10-06.md`. 구 도플 스킬 10개는 유닛에서만 뗐다(SkillVfxTable·Tools 참조 때문에 에셋 안 지움).
- **스킬 아이콘**: `Assets/Editor/SkillIconLinker.cs`(call SkillIconLinker.Link — 표 `Tools/skill_icons/skill_icon_map.csv` 701행 → 620개 연결: 확신도 높음 283·보통 59·낮음 278, PNG 없는 78행은 글자 첫 글자) · 128px 사본 `Tools/skill_icons/resize_icons.py`(`/usr/bin/python3`, -I 쓰면 Pillow 못 찾음) → `Assets/Art/SkillIcons/`(248장) · GameHud 정보 창 오른쪽 아래 스킬 아이콘 줄(최대 10, 호버 툴팁은 상점 칸 툴팁 재사용, 「디버프」 이름 에셋은 빨간 테두리). 사진 `Docs/ui_mockups/compare/25_skill_icons_tooltip_cmdcard.png`.
- **명령 카드 칸 배치(10-06)**: 이동(0)·정지(1) 칸 제거(우클릭 이동·M/S 키는 그대로, `IsRemovedCommandSlot`). 1번=특성강화(맵 위에 뜨던 패널을 카드 칸으로 옮김 — `SyncTraitSlot`), 2=홀드, 3=공격, 4=모으기, 5=**액티브(Q)**, 6=최윤서 강화(구현담당3), 7=판매, 8~11=조합 결과(박민수 재능투자도 8~11). 0번은 비어 있다.

## 남은 일 (우선순위)
1. 🔴 **이 마지막 GameHud 변경(특성강화 1번 칸)이 미커밋일 수 있다** — `git status`로 확인. 컴파일 오류 0(csc_check·csc_check_editor grep error 0).
2. **스킬 아이콘 Link 다시**: 구현담당1이 새로 만든 SkillData(Summon·전설 신규 ~25개)·소환 유닛이 커밋되면 `call SkillIconLinker.Link` 한 번 더(다시 불러도 안전). 못 맞춘 계획 행 2건(강재규·공증 오라 참고 / 양재모·로키포트)은 사장님 스킬 에셋 이름이 달라서 — 표 행을 고치거나 이름 맞추기. 사진: 초월 최상호 구일 선택 화면(PM 요청 5번)은 아직 안 찍음.
3. **김민준 푸바오(초월_김민준_AP, 마딜, 타이핑 「외동판다!」)** — PM 큐 7번, 설계표 먼저. 재료 김민준(전설적인_김민준)+여은서 마빡이+장하민 캐나다갱단+박민수 루키인싸+김정래 프로그래머+초월위습. 스킬 이름 순서: 찍어누르기(끝딜 잃은체력 2%) · 포커싱오더(활성화 **토글**: 켜면 본인+소환수만 잃은 체력 많은 적 우선 — ActiveButton 틀에 토글 모드 필요) · 특출난분석력(적 마방 −6% — 엔진에 마법방어 축 있는지 확인, 구현담당1 박민석 「마방깍」과 맞출 것) · 산하동료호출(소환수 1기, 평타 10%·20초, 구현담당1 `SummonUnit` 재사용, 소환 유닛 제안 필요). 디버프 「외동」은 `Assets/Data/UnitSkills/SkillData_공용_디버프_외동.asset` 하나를 그 유닛 skills에 그대로(효과 0, 자리만). 사장님 확정: 포커싱 = 김민준 본인+소환수만.
4. **이재윤 초특급인싸(초월_이재윤_AD, 물딜, 타이핑 「원숭이왕」)** — PM 큐 9번. 재료 석성례 팔방미인+임채현 호모사피엔스+서아인 사이코패스+박예원 성대결절+김경현+초월위습. 스킬: 인싸Lv.Max(스턴 1초)·체육특기생(재윤)(현재체력 단일)·91사단의지휘자(공격력 오라 맵 전체)·긍정의힘(아군 디버프 해제 — 구현담당3 디버프 틀)·원숭이의민첩함(공중이동)·내면의악(치밀한수작=방깎·낯선공포=이속감소: 유닛 수가 [최대−10, 최대]일 때 켜지는 오라). 유물 **종이비행기**(스토리 7 자리, 원작 버스터콜 1/20 → 종이비행기 1/20; 적 하나 보스 포함 영구 정지 + 전체 체력 25%·1회·아이템은 남음; 영구 정지된 보스는 끝까지 멈춤). 버스터콜 에셋은 지우지 말고 스토리 7 보상에서만 빼기(구현담당3 `BrokenMirrorApply` 방식). **`Apply`에 `unit.trait = null;`을 꼭 넣을 것**(Trait_초월_이재윤_AD 교체 스킬 원작007_H08V가 슬롯 0을 덮음 — PM·사장님 결정, 구현담당1이 박민석·구일에서 쓴 방식). 「최대 유닛 카운트」 = 인구 상한(FOOD_USED) 값을 찾아 쓸 것. 설계표 → PM 확인 → 구현.
5. 확인 못 한 것: 바지사장 멀티 클라 시전(코드만), 이름표 겹침(「박민석 빽빽이」+「바지사장」 한 줄로 붙음 — 가까이 선 두 유닛 이름표 겹침, 기존 문제일 수 있음 보고만), 선택 위습 멀티(주인 4명) 격자, 포탈 판정 r18이 원판보다 1.33배 커서 원 밖 클릭도 먹음(PM이 사장님께 여쭘), 다른 포탈(랜덤·자원 등)의 「스치면 먹힘」 결함(보고만).

## 함정
- 🔴 **에디터는 하나를 같이 쓴다**(구현담당1·3·PM). refresh·gameshot 전 「씁니다」, 끝나면 「끝났습니다」. **소스 파일을 고치면(미컴파일) 남의 판이 자동 컴파일로 리로드돼 무효가 된다** — 판 도는 중엔 소스 편집 금지. 내 판이 「플레이 도중 도메인 리로드」로 무효가 되면 다른 세션의 Apply 에셋 임포트·소스 편집이 원인 — 조용해진 뒤(outbox 100초 idle, inbox 비어 있음) 다시.
- gameshot: `snap:`을 한 판에 여러 번 쓰면 마지막 카메라 그림으로 겹친다(판마다 한 장). 클릭 이름에 공백이 있으면 `click:` 못 읽음 → 오브젝트 이름(`WispSlot0`)으로. `rclick:`은 가상 마우스 진짜 우클릭.
- 커밋: `git add -u -- <폴더>`나 `git add Assets/Data`처럼 폴더째 올리면 남의 에셋이 섞인다. **내 파일 경로만** `git add -- 경로` + `git commit -- 경로`. 필요하면 `git diff --cached --name-only -z | xargs -0 git commit -m ... --`.
- RepairCombineBoard(구현담당1 영역): DryRun으로 「새 발자국 안 남의 것 0」 확인 → Repair → 씬 커밋(URP 조명 블록 guid 474bcb49853aa 새로 생긴 것 없나 확인). 섬 크기 유도식은 MapLayout(GachaExtraZ·CombineSizeZ)에 있다 — 리터럴 3(줄+빈 줄)·26(가장 깊은 열 행)·14(랜덤유닛 종)는 보고문 「깊이 N/M」이 어긋남을 고발한다.
- 바다 판은 ±3334 한 장(MapLayout.SeaSize) — 조합판 남쪽 여유 211이 한계(더 벌리려면 바다 판 확장 공사: SeaWaterBuilder·NavMesh·미니맵).
- OverlapSphere 버퍼(16)는 맵 콜라이더로 차서 포탈이 잘린다 · ClosestPoint는 포탈의 비균등 배율(지름×0.5×지름)에서 어긋난다 → 가로 거리에서 월드 반지름을 빼서 잰다.
- SkillData·SkillEffect·SkillTriggerType enum은 **맨 뒤에만** 추가(직렬화). 새 필드가 생기면 에디터가 모든 SkillData 에셋을 재직렬화한다(대량 diff는 정상).

## 위치
- 문서: `Docs/design/BAJISAJANG_DESIGN_2026-10-06.md` · 사진 `Docs/ui_mockups/compare/11~25` · 원작 초월 수치표 `Docs/research/TRANSCEND_SKILL_REFERENCE_2026-10-06.md`(구현담당1).
- 점검 도구: `Assets/Editor/WispSpaceProbe.cs`(선택 위습·카메라 Focus*) · `ShopSlotProbe.cs`(도움소 칸 레이캐스트; PM이 Fund·ClickAll 추가) · `BajisajangProbe.cs`.
