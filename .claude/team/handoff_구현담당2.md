# 구현담당2 인수인계 — 2026-10-07 밤 마감 (0.3.15 배포 뒤)

새 세션은 기억이 없다. 이 문서 → `.claude/NEXT_SESSION.md` 구현담당2 항목 순서. 전부 main에 커밋(푸시는 PM). 에디터 순번제(쓰기 전 「씁니다」, 끝나면 「끝났습니다」) — **소스(.cs)·Editor 스크립트 편집도 남의 gameshot을 도메인 리로드로 무효**로 만든다. 큰 편집은 scratchpad 복사본(`rsync -a Assets/Scripts <복사본>/Assets/`, 나머지 Assets는 심볼릭 링크)에서 `Tools/ui/csc_check.sh <복사본루트>`로 컴파일만 확인 → `diff -u 원본 수정본 > x.patch` → 에디터 순번이 오면 `patch -p0 파일 < x.patch`로 한 번에 적용(sed -i는 맥에서 `-i ''`).

## 오늘(10-07) 끝낸 일 (git log 제목으로 찾는다)
- **정보창**: 힘·민·지 줄(초월·영원만, 정수 내림, 정보창 오른쪽 아래 — `unitHeroStatText`) · 유닛 스킬 아이콘을 명령 카드로 이동(`FlexKind.Passive`: 5·6 → 판매 불가 등급이면 7(`SellSlotFree`) → 조합 초상 뒤 8~11, 넘치는 건 마지막 스킬 칸 설명에 이름) · 「상태:」 줄 버프·디버프 칸(`UnitAttacker.CollectStatusBadges` → `RefreshStatusBadges`, 아이콘 7종 = `Assets/Resources/UI/BuffIcons`, 런타임 `Sprite.Create`).
- **위습**: 아치 초상(PortraitStage.HasRealModel이 구 메시를 자리표시로 오인하던 것 해제 + 발광 ×0.3) · 다중 선택 위습 칸 = WispIconBaker 구슬 + 종류 등급색 · 「(Clone)」 제거.
- **조합 도우미 재료창**(RecipeHelperPanel): 칸 클릭 → 반대쪽 창(재료·보유/부족·or·아무거나·비용·라운드·채팅 조합어 = `CombineSystem.ChatPhrases` public), 재료 행 클릭 → 그 재료의 재료(뒤로), 닫기·Esc·빈 곳·같은 칸 재클릭, [조합판에서 찾기]=옛 클릭.
- **MP**: 클라 지점 지정(배성령)·적 지정(강재규) 액티브 RPC(`RPC_CastActiveAtPoint/OnEnemy`, 호스트 검증) — NetPlayer RPC가 늘어 호스트·클라 같은 빌드 필요.
- 채팅·알림 글씨 축소(PM이 최종 18/27) · 첫 화면 닉네임 사진·F5 서랍 1366×768 사진 확인(정상).
- 0.3.15 배포 전 스모크 2회(두 창·퇴치 의뢰·광폭화·솔로 R4·통합 재스모크) 전부 통과, 예외 0.
- 촬영 탐침 `Assets/Editor/HeroStatShotProbe.cs`(커밋됨): `call:HeroStatShotProbe.Spawn/SpawnYongtae/SelectYongtae/AddXp/AddBuffs/HelperOpen/HelperClickLegend|Hidden/OpenBoot/OpenGame` — gameshot의 `call:`로 부른다.

## 남은 일 / 확인 안 한 것
- **오라 잔여 3건**(PM 보류): 맵에 없는 7·시불(Ora_siki) 바위 단색 = 원본 워크3 텍스처가 이 컴퓨터에 없어 막힘(사장님께 정품 War3/CASC 위치 질문 대기) · 도플라밍고 실 32 = 일부러 뺌(되살리려면 BuildAll 재실행+반입+사진, 가치 낮음).
- **버프 아이콘 후속**: 칸 6개 넘으면 뒤는 생략 · 「공↓」(음수 공격력 오라)은 아이콘 파일이 없어 글자 칸 · 체력·팀↑·기절 칸은 사진으로 못 봤다(코드 동일) · 호버 설명은 사진 못 찍음.
- **「암살스킬(순간이동)」 긴 이름 칸**: 액티브 스킬 칸(Q)에 아이콘이 없으면 이름 글자가 칸을 꽉 채운다(클라 사진). 아이콘 지정(스킬 에셋 icon) 또는 이름 짧게 보이기.
- **사장님 눈 대기**: 조합 도우미 필터 팝업 · 타이머 시계 구슬(120px) 크기 · 가득 찬 도움소 마나 맥동(값 1000일 때만 보임).
- 재료창 재료 행 클릭 이동·「보유」(초록) 상태는 사진으로 못 찍었다(시작 자원 1기뿐) — 필요하면 유닛 여럿 세운 뒤 찍을 것.
- 제한됨 8종(`Docs/design/LIMITED_RECIPES_DESIGN_2026-10-06.md`) 사장님 답 대기. 초월·영원 Lv.1 힘·민·지는 base 0이라 0/0/0 — 원작 시작값은 PM이 w3u(ustr/uagi/uint)로 뽑아 데이터에 넣겠다고 함.

## 함정
- gameshot 도메인 리로드 오염(「무효」): 남(구현담당1·PM)이 소스를 저장·refresh하면 내 판이 끊긴다 — 매번 순번 확인, 시작 전 `refresh` 뒤 dll이 새로워진 다음 명령. select:는 겹친 유닛에 흔들리면 안 잡힌다 → 탐침 `SelectYongtae`처럼 SelectionManager.SelectOnly 직접.
- `UnitCommandSlot*` 칸 번호: 0 이동·1 홀딩·2 정지·3 공격 / 4 반복·5·6 스킬·7 판매(또는 스킬) / 8~11 조합 초상. `ReflowFlexSlots`가 5·6·7·8~11을 채운다 — 7번 판매와 스킬이 같은 칸을 쓰니 `HideSellButton`·`SellSlotFree` 규칙을 깨지 말 것.
- GameHud·UnitAttacker는 공용 — 커밋 전 `git diff -- 파일 | grep "^@@"`로 내 hunk만인지, 구현담당1과 「커밋합니다」 주고받기.
- 빌드 사본(`../GuilRandomDefense-build`)은 PM이 release 배치 빌드에 쓴다 — 쓰기 전 PM·구현담당1에게 확인, 시험 하네스는 `git show dev/g1-quest-check:Assets/Scripts/Net/NetQuestTest.cs` 같은 브랜치에서 가져다 쓴다(NetLauncher에 `-mpTestXxx` 훅 3줄, 솔로는 `PlaySolo()`에도 훅 필요). 맥 빌드: `Unity -batchmode -quit -projectPath <사본> -buildTarget OSXUniversal -executeMethod BuildBeta.Mac`(GameVersion.Number를 바꿔 앱 이름을 달리) → 두 창: 호스트 `-mpHost -mpSession ZX$RANDOM -mpToken h1 -mpAutoStart 2 -mpDifficulty Easy -mpTestXxx 20 -logFile …`, 호스트 로그에 「NetPlayer 생성」이 뜬 뒤 4초 있다가 클라 `-mpJoin -mpSession 같은코드 -mpToken c1 -mpReady -mpSaveDir …`. 끝나면 `pkill -f 앱이름`.
- 솔로(-mpSolo)는 난이도 창이 안 넘어간다 — 시험 코드에서 「쉬움」 버튼 `onClick.Invoke()` + 방어 유닛 세우기(무인이면 R2에 패배).
- `csc_check`는 `| grep -c error`(0이어야). 새 파일은 `git add -- 경로` 후 `git commit -- 경로`(.meta 포함).

## 10-08 추가분 (이 아래가 최신 — 위 10-07 내용도 유효)
- 끝낸 일(git log 제목): 조합 도우미/버프 +N·공↓ · 다중 선택 판매 한 기씩(맨 뒤=낮은 등급부터)·카드 Shift+클릭 빼기 · 공격력 「기본 +보너스」 · 정보창 정렬(공격→방어→상태) · 게이지 막대(ShownMana/Life·NetEntity.Gauge*) · 스토리 「NN. 이름」 · 스킨 조도연(메구밍)·이호준(에밀리아) · 소리(스킬 −14dB·Voices 4·슬롯 표·Coin/획득 음성 −6, AudioPrefs.SfxVolume + F10 슬라이더) · 판매 툴팁 · 조합 검색(닫으면 비움·포커스·재료 그림 클릭=그 재료 식) · 휠 줌(서랍·도우미 열림/포인터 UI 위면 0) · 초상 Tunes(조도연·배성령·임장혁) · 컷인(CutinOverlay Enabled=true, 레이어 41기 반입, 효과음 무음).
- 남은 일: ①컷인 실제 획득 경로 프레임 사진(채팅 조합·도박으로 초월 1기, 앞 구간부터) ②컷인 효과음 — 사장님이 ~/GRD_cutin_sfx 후보(슉 A/B·칭 A/B)에서 고르면 Assets/Audio/Sfx/Resources/Sfx/cutin_whoosh·cutin_ting에 wav ③blender가 머리 잘림 3~4기를 다시 뽑으면 `/usr/bin/python3 Tools/cutin/import_cutin.py`(--dry로 용량, 같으면 변경 없음) ④초상 못 고친 3기(모델 문제): 영원_윤현모(코트만·머리 없음)·희귀함_조현규(몸 일부만 큼)·히든_최윤서(흰 소용돌이 모양) — blender 몫 ⑤MP 두 창 판매·슬라이더 드래그·구 금속 테마 F10 사진 미실측.
- 함정: Resources 한글 폴더는 맥에서 NFD라 Load가 NFC 경로를 못 읽는다(컷인은 ASCII 키 c_<SHA1>) · HUD가 매 프레임 초상 무대를 되돌려 전수 탐침은 GameHud.enabled=false 필요 · gameshot call: 안의 EditorApplication.update로 입력 주입은 안 먹는다(QueueStateEvent 사용) · 모델 배선 메뉴가 다른 Mob/Roster 파일을 건드린다(커밋은 내 경로만) · 패치는 처음부터 scratchpad에만, 주제별 커밋은 `git apply --cached`(패치 헤더 경로 정리) · PIL은 `/usr/bin/python3`(−I 쓰면 안 보임).
