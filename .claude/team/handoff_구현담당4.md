# handoff — 구현담당4 (시작 흐름 성 테마 · VFX 스타일 · UI 성능, 10-20 마감) — 이 역할은 더 안 띄움(사장님 10-20)

작업 폴더: worktree `../GuilRandomDefense-ui`, 브랜치 `ui/castle`(main 기준). push·병합은 PM. 산출물·사진: `~/grd/GRD_title_castle/`(README.md·촬영법.md·페이지문구.md·비교_*.png·music/).

## 끝난 것 (커밋, 오래된 순)
014ec46bd MenuTheme 스위치 · 992f4bf65/886b6880c/b856cfff4 LobbyCastle 그림 · f72e4d888 DoorCastle · 9eb171be3 SettingsCastle · 5d868e16c/9e1cfabcb 인트로(돌벽·못) · 5011e57bf/c9d618712 UX(으뜸 단추·대기실 손님 안내) · def16170c 로딩 화면 · 727da1ed4 DoorPreview(문 촬영용).
스위치: `Assets/Scripts/UI/MenuTheme.cs`의 `Current`(기본 Castle). Tavern 값은 종전 그대로(셀프 리뷰로 색 26종 대조 완료). 그림 정본: `Tools/ui/gen_castle_{lobby,door,settings,loading}.py` + `castle_art.py`(PIL, /usr/bin/python3), `.meta`는 `make_metas.py`(옛 .meta 복사+GUID 새로).

## 추가로 끝난 것 (밤 후반)
cc909ae2e·735b3604b·8d7b9272c 문 전환 개선: `DoorTransition`에 방식 스위치(`MenuTheme.CastleDoorStyle` = Doors(A, 기본)/Portcullis(B), Tavern=Classic 종전). 성문 그림 `DoorCastle/gate_frame·gate_grate`(정본 `gen_castle_gate.py`), Kenney CC0 소리 둘 추가, 촬영 도구 `DoorPreview`(SkipIntro·Frozen*/Step 시간 정지 스텝·Slow·Normal)와 `Tools/ui/strip.py`·`sim_gate.py`. 4차 촬영으로 A·B 둘 다 PM 통과. 비교 카드 `~/grd/GRD_title_castle/비교_문전환.png` + gif 셋.

## 남은 것
1. (문 전환 실화면은 PM이 4차까지 촬영 완료 — 아래는 재촬영이 필요할 때의 방법) 에디터 창에서 `refresh` 두 번 후 `gameshot door.png 1 1920x1080 scene:NetBoot call:DoorPreview.Close wait:0.2 snap:door_mid.png wait:0.25 snap:door_closed.png`, Tavern(MenuTheme 한 줄)도 같은 명령 → before/after 카드 `비교_문전환.png` 추가.
2. **대기실 실화면**(방 만들기, Photon 연결 필요): 자리 줄 색·태그·손님 안내 한 줄 위치 확인.
3. **음악 교체**(사장님이 고르면): mp3/ogg를 `Assets/Audio/Music/Resources/Music/`에 넣고(.meta는 유니티가), `GameBgm.cs` 32행 Cue 경로·볼륨(`music/loudness.txt`의 Cue vol), `CreditsSplash.cs` 25행 `MusicCredit` 표기(CC BY 출처) 교체. 옛 곡 파일은 지우지 않는다.
4. 실화면에서 거슬리면 해당 `gen_castle_*.py`를 고쳐 다시 돌리고(`make_metas.py`는 있는 .meta를 건드리지 않아 GUID 유지) 같은 파일명으로 커밋.

## 함정
- 문 촬영은 인트로(정렬 32000)가 문(31000)을 덮는다 → `DoorPreview.SkipIntro` 먼저. snap이 장당 ~0.7초라 실시간/Slow로는 프레임 간격이 안 맞는다 → `Frozen*`+`Step`.
- 문 열림의 전체 화면 번쩍임은 짧고 약하게(≈0.08초·30%) — 틈 빛을 크게 키우면 화면이 하얘진다(10-19 1·2차 촬영에서 발생).
- worktree엔 Library가 없어 `Tools/compile_check.sh`가 그대로는 안 돈다 — 스크래치에 사본을 만들어 `Library/ScriptAssemblies`를 메인 폴더 것으로 가리키고 ROOT를 worktree로 바꿔 돌렸다.
- PIL은 `/usr/bin/python3`에만 있다(`python3`=homebrew엔 없음). `-I` 옵션을 주면 PIL이 안 잡힌다.
- BSD sed는 `sed -i ''`. 긴 `&&` 체인에서 앞이 실패해도 줄바꿈 뒤 명령은 돈다 — 커밋 전에 `git status`로 확인.
- gameshot `click:SoloButton`은 클릭 간격 0.5초 > 문 닫힘 0.45초라 문이 안 찍힌다(코드 문제 아님) → DoorPreview.Close.
- MenuTheme의 Castle 그림이 없으면 Tavern 그림으로 물러나므로 「성인 줄 알았는데 선술집」이 조용히 나올 수 있다 — 폴더 이름·.meta 확인.
- Tavern(영상 배경)은 title_loop.webm 첫 장이 늦게 뜬다 — 촬영 [초]를 3으로.
- 새 .meta GUID는 uuid로 만든다(check_meta.py는 PM이 푸시 전에 돌림).

## 10-20 갱신 (마감) — 이후 나온 일
- **성 테마 후속**: 문 A(쇠문) 확정·느리게(닫힘 1.0s·열림 1.0s, 로딩은 완전히 닫힌 뒤), 열림 곡선·번쩍임 수정(PM 후4 촬영 통과), 음악 = Elyvilon 「Noonday Feast of the Jolly Friar」(CC BY 4.0, 성 테마만; 파일 `title_castle_noonday_feast.mp3`, 크레딧 CreditsSplash). 0.3.25 기반 main에 cherry-pick 병합됨. 위 「남은 것」 3번(음악)은 끝남.
- **VFX 스타일**: 사장님이 B(워크3풍) 선택, 스턴도 B. 산출물 `~/grd/GRD_vfx_graph/스타일/`(README·안A/B/C 시안·스턴 비교·tools/gen_B_textures.py) · 텍스처 세트 `~/grd/GRD_vfx_graph/재료_B/`(README에 사용처·블렌드·스턴 B 수치). 구현담당2에게 인계됨 — 텍스처 수정은 `gen_B_textures.py`(/usr/bin/python3)를 고쳐 재생성.
- **UI 성능(ui/perf, 13커밋, main 병합·0.3.37)**: 이름표·체력바 머리 높이 캐시와 풀 SetActive 제거, GameHud 정보칸 토글·문자열 생략, 미니맵 12→6Hz+LeanRender, 초상 카메라 패스 축소, 조합 도우미·동맹창 재갱신 생략, OnGUI Repaint만 등. 표 `~/grd/GRD_ui_perf/README.md`. ⚠️ 수치는 코드 추정(실측은 구현담당2 A/B). 미니맵 수면이 이상하면 `MinimapCamera.LeanRender=false`.
- **패치노트 0.3.37 초안**(6b8cd749d) · **아침 보고 재료** `~/grd/GRD_night_report_1020/`(페이지문구.md·사장님_결정목록.md·img/ 34장).
- 워크트리: `../GuilRandomDefense-ui`(ui/castle), `../GuilRandomDefense-uiperf`(ui/perf), `../GuilRandomDefense-notes`(notes/0337) — 모두 병합 끝, 정리해도 됨.

## 남은 것 (갱신)
1. 대기실 실화면(방 만들기, Photon) 확인 — 아직 안 봄.
2. 명령 카드 「액티브 스킬」 칸 문자열은 아직 매 프레임 생성(작음). 다중 선택 시 판매 칸 GetComponent 루프(작음).
3. UI 성능 실측 수치(0.3.35 vs 0.3.37)는 구현담당2 결과 대기 → 아침 보고 표에 반영.
