# 구현담당2 인수인계 — 2026-10-07 밤 (워크3풍 UI 0.3.14 완료)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md` 구현담당2 항목 순서. 전부 main 커밋(푸시는 PM). 에디터 순번제(쓰기 전 「씁니다」, 끝나면 「끝났습니다」) — **소스(.cs)·Editor 스크립트 편집도 남의 gameshot을 도메인 리로드로 무효**로 만든다. 큰 편집은 `scratchpad` 복사본에서 `csc_check.sh <복사본루트>`(Assets/Scripts만 복사, 나머지 Assets는 심볼릭 링크 — **Assets/Editor는 링크 말고 복사**, 안 그러면 실제 파일이 바뀐다)로 컴파일만 확인했다가 한 순번에 적용했다.

## 이번에 끝난 것 (git log 제목으로 찾는다)
- **워크3풍 UI 전체**: 그림은 blender가 `~/GRD_wc3_ui/`에 2배 해상도로 납품 → `Wc3SkinApply`(Editor, `call Wc3SkinApply.Apply`)가 `Assets/Resources/UI/SkinWc3/`로 복사+9-slice 설정(표 `Files`). 코드는 `UiSkin.Wc3(name)`·`ApplyWc3(image,name,ppum=2)`·`Wc3Has`·`Wc3Texture`(IMGUI) · 스위치 `UiSkin.Wc3Active`(그림 없으면 옛 C 금속 모양으로 물러남) · GameHud `Wc3Console` 프로퍼티.
  - 콘솔: 톱니 돌 타일 바(285px 고정)·칸 돌 틀 덮개(`wc3FrameOverlays`→`BringWc3FramesToFront`)·정보창 틀+위 띠 둘+아머/공격 금칸(`BuildWc3InfoDeco`)·초상 아치(Mask+틀, `PortraitStage.CloseUp`로 상반신 0.36)·돌 기둥·인벤토리 제목+6칸·명령 칸 4상태(`SetCommandSlotColor` 곱 틀)·상단 단추/시계 구슬(120px)/자원 칸·타이머/점수판 틀.
  - 창: F10 메뉴 세로 단추(`BuildGameMenuCardWc3`, `Wc3MenuButtonText`)·F5 서랍·조합 도우미·채팅 입력(IMGUI 9-slice)·알림 줄 띠.
- **도움소 마나 표시**: 정보칸 파란 막대+「마나 N / 상한」(`SetPortraitManaOnly`), 건물 머리 위 막대(`ShopNameplateLayer`, 가득 차면 맥동), `SupportShop.ManaOwner`.
- 원작 비교 사진: `ClaudeBridge/shots/cmp_console.png`·`cmp_top.png`·`cmp_timer.png`(합성 스크립트 scratchpad `cmp.py` — 원작 `/tmp/.../ref_original_ui.png`는 사라질 수 있다).

- **유닛 이름 바꾸기 19기**(18736b262·b55d220de, 표 `Docs/reference/UNIT_RENAMES_2026-10-07.md`): 에셋명 불변, 인물 이름 `UnitData.personOverride`(맨 뒤 필드)·칭호 `unitName`. 적용 `call UnitRenameApply.Apply`(Editor, 재실행 안전)·실측 `UnitRenameProbe.Chat/Search/Spawn푸은서`. 히든 채팅 입력말은 새 이름만(`CombineSystem.ChatPhrases`가 이름 바뀐 유닛의 옛 에셋명 입력말 제외). 「최윤서 강화」→「노윤서 강화」(소모 대상 키는 에셋명 `히든_최윤서`)·특성강화 traitName 16개 새 이름. ⚠️ `Tools/low_grade_data.py` 등 생성기를 돌리면 옛 이름으로 되돌아간다 → 돌린 뒤 UnitRenameApply 재실행. 안 고친 것: 적 이름(Enemy_R*_이름 — 원작 적, 별개)·스킬 desc 개발 메모(액티브 아닌 건 플레이어에게 안 보임)·F5 검색의 옛 이름 별칭(에셋명 검색).

## 남은 일 / 확인 안 한 것
- 정보창 오른쪽 반: 힘·민첩·지능(초월·영원만)은 아직 안 넣음 — 데이터 필드가 있는지부터.
- 좌우 캡(`console_cap_*`)은 미니맵이 가장자리라 안 씀. 영웅 칸(`hero_frame`·`bar_track`)·`win_tab`·`win_close_btn`·`win_scroll_*`·점수판 `score_*` 부품은 반입만 하고 코드엔 안 썼다.
- F5 서랍 맨 위 줄 정렬(`TopPad`)은 1920×1080에서만 봄 — 1366×768 사진 필요. 도우미 필터 팝업·타이머 시계 구슬 크기는 사장님 눈으로.
- 가득 찬 도움소 마나 맥동은 값이 1000일 때만 보여 사진 못 찍음.
- **제한됨 8종**(사장님 답 대기): `Docs/design/LIMITED_RECIPES_DESIGN_2026-10-06.md`.

## 함정
- gameshot 이름 충돌·도메인 리로드 오염(「무효」) — 매번 새 id, 남이 쓰는 동안 소스 편집 금지.
- `UnitCommandSlot*` 칸 번호: 0 이동·1 홀딩·2 정지·3 공격 / 4 반복·5·6 스킬·7 판매 / 8~11 조합 초상(구현담당1 d3f188298).
- GameHud는 공용 — 커밋 전 `git diff -- 파일 | grep "^@@"`로 내 hunk만인지 확인, 구현담당1과 「커밋합니다」 주고받기.
- `csc_check`는 `| grep -c error`(0이어야).
