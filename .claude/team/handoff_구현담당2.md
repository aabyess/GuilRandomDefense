# 구현담당2 인수인계 — 2026-10-03 (UI 원랜디스럽게 + 풀카운트·세이브 코드·e015·난이도 대화상자)

새 세션은 기억이 없다. 이 문서 → `Docs/UI_ORIGINAL_STYLE.md` → `Docs/SPEC_2026-10-03_difficulty_dialog_and_e015.md` 순으로 읽고 시작한다.

## 오늘 끝난 것 (전부 main, 푸시는 PM)
- 표시류: 스토리 막타 청록 「+N」(원작은 N=보스 비행높이 필드, 9번째부터 +10) · 클리어 칭호 13단계(PlayerDisplayName) — 커밋 a04bbbe51.
- 세이브 코드: Data/SaveCode.cs(생성·검증, 서버 DB 전환 시 이 자리만 교체) + Units/SaveCodeService.cs + 시작 화면 「세이브 코드 불러오기」 + 채팅 `-load` — 닉네임이 열쇠, 클리어 큰 쪽 채택. 프로브 Editor/SaveCodeProbe.cs.
- 풀카운트: Units/FullCountScore.cs(+ NetGameState [Networked] FullCounts) — 시작 500·신세계 진입 1250·라운드 끝 배수×(5+full_C)·보스 +300(+200 신속)·클리어 보너스. 프로브 Editor/FullCountProbe.cs(Entry/Enter60/UseReward/KillBoss).
  미구현: 랜덤전용(PV180) [조합] one_dill +8 경로(대응 미정).
- e015 신세계 보상: RewardDistributor.GrantNewWorldWisps(어려움·지옥 2기 / 신·악몽 1기, Wisp.NewWorldReward 표식) → UnitPortal이 표식 위습 소모 시 FullCountScore.OnNewWorldRewardUsed(−625×배수). 프로브로 확인(포탈 경유 자체는 함수 호출로 대체 확인).
- 난이도 선택 = 게임 안 방장 대화상자(원작 6.6초·6버튼·검정 가림막은 루트 캔버스 −50·사진 모양). 로비 난이도 버튼 제거. 기억값 자동선택 제거 — **측정 도구만** PlayerPrefs `GuilRandomDefense.ToolAutoPick`(한 판짜리)로 건너뜀(gameshot mode:가 켬, 새 토큰 `askmode`는 안 켬 = 대화상자를 그대로 봄).
- UI 개편 1단계(사진 `Docs/reference/ui/원랜디_인게임_01.png` 기준): 점수판 표화(제목·머리줄·플레이어 색 칩+칭호+닉·적 수·신세계 3열·접기) · 상단 바(좌 퀘스트 흐림·메뉴 F10·동맹·대화·항법 / 시계 / 금화·나무·고기=특성 포인트 / 구랜디, 마나는 시계 옆) · 우상단 타이머 창 스택 · 콘솔 27%·돌벽(stone_tile 타일)·미니맵 단추 5개(그림만) · 초상 아래 체력/마나 바(마나는 UnitManaTable 51개만) · 정보칸 서식(이름 별명 – 등급 / 공격 아이콘 줄 / 방어: 무적 / 상태:).
  비교 사진: `Docs/ui_mockups/compare/01~03*.jpg`(원작 왼쪽·우리 오른쪽, `Tools/ui/compare_shot.py`).
- 인프라: UiSkin(Resources/UI/Skin 슬롯, 정품으로 바꿔 끼우기) · UiSkinPostprocessor(스프라이트·9-slice) · `Tools/ui/gen_ui_skin.py`(근사 그림 22종 생성, Pillow 필요: venv) · `gen_mana_table.py`/`gen_hero_table.py`(w3u → 표 cs) · `Tools/ui/csc_check.sh`(에디터 없이 컴파일 검사 — 판 도는 중에도 안전, 이번에 에디터 대기를 크게 줄였다).

## 남은 단계 (순서대로)
1. **사장님 결정 4건 반영**(PM 전달): ①상단 마나 칸 유지(이미 그대로) ②항법 선택 버튼 유지 — 상단 버튼 맨 끝(이미 맨 끝, 최신 사진으로 재확인) ③**정보칸 사거리·공속 빼기**(GameHud ShowSingleInfo의 unitDamageText에서 「사거리/공속」 제거, 공격력 + 보너스만; DebugHud F1엔 남겨도 됨) ④**빈 플레이어 자리 「열림」 → 빈칸**(GameHud RefreshTeamPanel의 `<color=#808080>열림</color>` 제거, 색 칩만 남김). 반영 뒤 비교 사진 갱신.
2. **인벤토리 6칸** — 코드는 작업트리에 있으나 **커밋돼 있지 않았다면 이 커밋에 포함됨**: MaxItemInventorySlots 6, ItemInventory.MaxItems 6 + Add가 bool(가득 차면 거절, RewardDistributor 드랍 줄에서 안내), 엠블럼 배경(inventory_emblem) + 반투명 칸. 확인 안 된 것: 아이템을 든 채 실제 칸 모양 사진, 도박·스토리 아이템 경로가 7번째를 시도할 때 안내가 뜨는지.
3. **영웅 단추** — GameHud.BuildHeroButtons/RefreshHeroButtons/OnHeroButtonClicked + Data/UnitHeroTable.cs(원작 영웅 클래스 H+영웅 베이스 28종 → 로스터 **2개뿐**: 초월_구주호_AD · 초월_김민준_AP, PM 정의 그대로). ⚠️ **화면에 안 뜨는 문제 미해결**: 프로브(DisplayProbe.HeroState)에서 `UnitIdentity.Active`에 spawn:으로 세운 초월_김민준_AP가 안 잡혔다(Active 1개뿐 — 도구 spawn이 판 시작 뒤 지연 생성이거나 wait 2초가 모자랐을 수 있음). 먼저 wait를 늘려 Active/OwnerId/IsHero를 다시 찍고, 단추가 켜지는지 본다. 영웅 2개뿐이라 PM에게 대상 확대 여부를 다시 물을 것.
4. **방 패널(로비) 재배치** — `Docs/UI_ORIGINAL_STYLE.md` ③: 왼쪽 맵 정보+방 코드 / 가운데 슬롯 표(원작 4색·방장 표시) / 아래 채팅 / 시작·준비·나가기. 난이도 칸은 이미 정보 문구로 대체됨. NetBoot는 별도 씬이라 gameshot으로 못 찍음 — 에디터에서 NetBoot를 열어 한 번 눈으로 확인하거나 Game 뷰 캡처 수단을 새로 만들어야 한다.
5. **마지막 확인**: g1 형식 짧은 판 `gameshot … rounds:3 mode:보통`으로 mode: 자동선택이 대화상자를 건너뛰는지(구현담당1 기준선 도구 보호). 오늘 단발 판들(mode:어려움/신)은 통과.
6. 기준선은 PM이 UI 들어간 HEAD로 새로 돌린다 — 그 전에 에디터를 비워 둘 것.

## 함정 (오늘 겪음)
- 🔴 에디터는 하나를 같이 쓴다: refresh·판 전에 구현담당1·PM에게 「돌립니다」, 끝나면 「끝났습니다」. 판 중 소스 편집·refresh 금지(미컴파일이 판을 무효로 만든다). 컴파일 검사는 `Tools/ui/csc_check.sh`(에디터 불필요).
- 🔴 Canvas 자식이 부모 캔버스 정렬을 상속 — 난이도 가림막을 대화상자 캔버스 자식으로 두면 HUD까지 덮는다(루트로 둠).
- `<color>` 안 ■ 같은 도형 글자는 폰트에 없을 수 있다 → TMP `<mark>`로 칩을 그린다.
- gameshot `spawn:`은 판 시작 뒤 생성이라 바로 읽으면 없을 수 있다(wait를 둘 것).
- gameshot `askmode` = 난이도 자동선택을 끄고 대화상자를 본다. `mode:`는 도구 신호(ToolAutoPickKey)를 켜서 대화상자를 건너뛴다 — 신호는 Awake가 읽고 지운다.
- 한글 파일명 NFC/NFD: 표 생성기는 CSV 문자열과 listdir을 비교한다(지금은 일치, macOS 파일명 정규화 주의).
- 원본 w3x(`Tools/w3x/원본/*`)는 git worktree에 없다(.gitignore) — 생성기는 주 저장소 경로로 폴백하게 해 뒀다.
- 정품 워크3 UI 그림은 이 PC에 없다 → 근사 그림 + UiSkin 슬롯. 원작 그림을 새로 넣는 커밋은 `Docs/REPLACE_BEFORE_PUBLIC.md` 🔴 표에 한 줄.
- RtsCameraController는 구현담당1(카메라 담당) 영역 — MoveTo 호출만 쓴다.

## 위치
- worktree 없음(병합 후 제거). 브랜치 `ui-original`은 main에 ff 병합돼 남아 있으니 지워도 된다.
- 문서: `Docs/UI_ORIGINAL_STYLE.md` · `Docs/SPEC_2026-10-03_difficulty_dialog_and_e015.md` · `Docs/ui_mockups/`(목업·compare·skin_preview) · 사진 `Docs/reference/ui/`.
