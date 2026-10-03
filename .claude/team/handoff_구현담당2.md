# 구현담당2 인수인계 — 2026-10-03 밤 (UI 원랜디스럽게 2·분홍 오라·정면 점검)

새 세션은 기억이 없다. 이 문서 → `Docs/UI_ORIGINAL_STYLE.md` → 필요하면 `Docs/SPEC_2026-10-03_difficulty_dialog_and_e015.md` 순으로 읽는다. 전부 main에 커밋됨(푸시는 PM).

## 오늘 끝난 것
- **정보칸·점수판**: 사거리·공속 뺌(공격력+보너스만, F1 DebugHud엔 남음) · 빈 플레이어 자리 「열림」 글자 뺌(색 칩만).
- **영웅 단추**: 정상 점등(원래도 코드는 맞았음 — 탐침이 spawn보다 먼저 돌았던 것). 대상은 로스터 2개(초월_구주호_AD·초월_김민준_AP) 그대로(PM 확정). 채팅 줄 시작 x를 20→76(`PlayerNotification.LeftMargin`)으로 밀어 겹침 해소.
- **미니맵 오른쪽 둥근 단추 5개 제거**.
- **초상 상반신** (`PortraitStage`): 전신을 맞춘 뒤 3차 보정으로 머리 꼭대기~허리(`BustFraction` 0.5)만 칸에 맞춤. 머리 꼭대기는 「몸통 두께 행」(`BodyRowFraction` 0.15)으로 잡아 칼·지팡이가 상자 윗면을 올리는 모델에 안 속음. 사람형(Humanoid)은 항상, 비사람형은 세로로 길쭉(`StandingAspect` 1.35)할 때만(노트북은 전신 유지). Head·Hips 뼈 대신 픽셀 비율. Animator 오브젝트 회전은 유닛 루트 기준 상대 회전으로 보존(무해). `[초상] …localRotation` 진단 로그가 남아 있음(에디터/디버그 빌드만).
- **둥근 UI**: `UiSkin.RoundFill/RoundRing`(코드 생성 9-slice, **PPU 100 필수** — 1이면 모서리가 100배로 늘어 거대한 타원이 된다) → `GameHud.CreatePanel`(색 있는 패널 자동 둥글게, 예외 이름 `SquarePanelNames`: BottomBar·TopBar·GameMenu·Fill·Border)·`AddPanelBorder`·`AddConsoleFrame` 일괄. 반지름: `UiSkin.DefaultRadius` 8, 콘솔 칸 틀 10(`AddConsoleFrame`). 더 둥글게 = 이 값만 키움. 스킨 PNG 8종(topbar·topbar_resource·button_navy(+hover)·multiboard·timer·dialog_panel·console_cell_frame)은 `Tools/ui/gen_ui_skin.py`로 둥글게 재생성(`round_mask`·`rounded_panel`), `UiSkinPostprocessor` 9-slice 테두리 4→9. **전/후 비교 사진은 안 만듦.**
- **위습 칸 3D 아이콘** (`UI/WispIconBaker.cs`): 살아 있는 위습이 처음 보일 때 한 번 구워 Sprite 캐시(직교 카메라·레이어 31·−12000 높이·128px). 위습 모델은 프리팹 하나(플레이어 색)라 종류 구분은 배경 등급색. 이름 글자 칸(Name)은 없앰 → 마우스 올리면 툴팁(`OnWispSlotHover`), 개수는 흰 글씨+외곽선. 칸 오브젝트 `WispSlot{i}` 자식 = Icon·Count. (구현담당1 autoloop이 칸 글자로 찾던 것은 알려 줌 — 도구가 고치는 중.)
- **분홍 오라(HandsAura2)**: Purple_Glow·Zap1_Red 새 근사 텍스처 적용. Zap1_Red는 g1 둘째 층으로 새로 들어감(원인: 생성기가 층 첫 장만 읽었음). 값은 `Tools/sphere_art/effects_overrides.json`(g1 intensity 1.0 · g1_L1 Zap 0.7 · g0_L1 Yellow_Glow `drop`), 크기 `SphereArtTable.TranscendentArts` scale **1.0**(1.5는 판 폭이 정강이의 1.6배로 너무 컸음, 1.3 세기는 흰색으로 포화). 비교 사진 `Docs/ui_mockups/compare/07_pink_aura.jpg`. 한계: 원작은 판이 2~3겹 촘촘, 우리는 12장 1겹.
- **flatten 생성기** (`Tools/sphere_art/flatten_effects.py`): 메시 층이 여럿이고 그림이 다르면 층마다 부품(`<부품>_L1`), 같은 그림의 혼합+가산 짝은 가산 하나. 손값은 `effects_overrides.json`(`{별칭:{부품:{intensity|drop|…}}}`)에서 덮어씀 — 이 json은 생성기가 안 지운다. 빌더(`SphereArtBuilder`)에 `intensity`(가산 재질 색 배율) 필드. 층 여럿인 메시는 전체 34 effects.json·51 메시 부품 중 3개뿐(HandsAura2 g0·g1, 초승달 베기 — 후자는 같은 그림이라 문제 없음). 재생성: `NOCOPY=1 AURA=1 python3 flatten_effects.py <폴더> <별칭> <별칭>` → 유니티 `call SphereArtBuilder.BuildAll` → refresh. ⚠️ BuildAll은 Sphere 프리팹을 전부 다시 써서 diff가 크다(내용 동일) — 필요한 것만 `git add`.
- **정면 점검 도구** `Assets/Editor/FacingAudit.cs`(읽기 전용, `call FacingAudit.Run` → `ClaudeBridge/outbox/facing_audit.txt`, 결과 표 `Docs/facing_audit_2026-10-03.txt`): 328개 중 +Z 287 · −Z 0 · 옆 7 · 못 잼 34. ⚠️ Humanoid 아닌 모델은 뼈 이름 L/R로 재서 **좌우가 뒤바뀔 수 있다**(박민수는 −X로 나왔지만 실제는 +X).
- **박민수 정면**: `ArtBinder.FacingFixes`(세운 뒤 Y 보정, `ModelAdjustments`에 적으면 AutoUpright가 꺼지므로 따로) 에 `특별함_박민수 −90`. 프리팹은 회전값만 직접 고쳐 커밋. 나머지 5(임건웅·이일중·김용태·최영민·조현규)는 정면 사진상 정상(20~50° 비스듬할 뿐), 고치지 않음. 사진 `compare/09_facing_six.png`·`10_park_fixed.png`.
- 탐침(`Assets/Editor/DisplayProbe.cs`): `AuraCloseup`·`CloseupPark`·`CloseupFront`(이름은 `ClaudeBridge/probe_name.txt`)·`SelectPark/Bae/Yoo/KimTY`·`SelectLater(이름)`·`HeroState`. gameshot `call:`은 **spawn보다 먼저** 도니 EditorApplication.update로 예약하는 방식을 쓴다.

## 남은 확인 / 일
1. **아이템 6칸 모습**: 아이템을 든 사진을 못 찍음(도박·스토리 아이템 경로가 7번째를 시도할 때 안내가 뜨는지도 미확인).
2. **로비(NetBoot) 방 패널 재배치**: PM 보류. `Docs/UI_ORIGINAL_STYLE.md` ③. NetBoot는 별도 씬이라 gameshot 불가 — 에디터에서 열어 눈으로.
3. **위습 여러 종류가 같이 보이는 사진**: 시작엔 「랜덤유닛」 한 종류뿐이고 위습 종류를 세우는 도구가 없다. 필요하면 도구부터.
4. 둥근 UI 전/후 비교 사진(필요하다고 하면). 모서리가 은은해서 더 크게 원하면 반지름 값만.
5. 분홍 오라: 판 밀도(2~3겹)는 모델 정점 수 문제라 못 맞춤 — 사장님이 더 원하면 판을 복제 배치하는 방안 검토.
6. 영웅 단추: 대상 2개 그대로. 늘리자는 말이 나오면 `UnitHeroTable`(gen_hero_table.py).

## 함정
- 🔴 에디터 하나를 같이 쓴다: refresh·판·Assets 쓰기 전에 「씁니다」, 끝나면 「끝났습니다」. 남의 판 중 소스 편집·refresh 금지. 컴파일 검사는 `Tools/ui/csc_check.sh [작업폴더]`(Assembly-CSharp만 — Editor 폴더는 안 봄; Editor 파일은 Assembly-CSharp-Editor.rsp로 따로 검사).
- 🔴 **모델 배선 메뉴(`Tools/아트/모델 배선`)를 돌리면 Generated 프리팹 400여 개 + Data 에셋 수백 개 + 씬의 fileID가 통째로 뒤섞인다**(내용은 같은데 diff 700개). 필요한 변경만 골라 커밋하고 나머지는 `git checkout -- Assets/Prefabs Assets/Data`로 되돌린다. 한 프리팹의 값만 바꾸면 되면 YAML을 직접 고치는 편이 깔끔했다(박민수). 같은 변경을 `ArtBinder`에도 넣어 다음 배선과 일관되게.
- Unity `Sprite.Create` 9-slice는 **PPU를 캔버스 reference(100)와 맞춰야** 모서리 크기가 맞는다.
- gameshot: `spawn:`·`select:` 순서 — select는 spawn보다 먼저. 사진 확인에서 「화면 위」와 「월드 방향」을 헷갈렸다(박민수: 카메라가 −Z면 +Z 정면 유닛은 뒷모습). 판독하기 전에 카메라가 어디서 보는지부터.
- 생성기 json을 손으로 고치지 말 것(다시 뽑으면 사라짐) — 손값은 overrides json.
- 판 중 스크린샷 경로: `ClaudeBridge/shots/<이름>.png`, 결과 로그 `ClaudeBridge/outbox/<이름>.txt`. 비교 사진은 `/usr/bin/python3`(Pillow 있음) + `Tools/ui/compare_shot.py`.
- 동시 판 여러 개를 inbox에 넣으면 순서대로 돈다(각 ~40~60초). 긴 대기는 `run_in_background`+until 루프(foreground sleep 연쇄는 막힘).

## 위치
- worktree 없음(정리함). 사진: `Docs/ui_mockups/compare/04~10`.
- 문서: `Docs/UI_ORIGINAL_STYLE.md` · `Docs/facing_audit_2026-10-03.txt` · 근사 텍스처 생성기 `Tools/blender/gen_aura_approx_tex.py`.
