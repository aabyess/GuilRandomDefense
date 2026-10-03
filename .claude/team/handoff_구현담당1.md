# 구현담당1 인수인계 (10-03 저녁, 직전 세션 작성) — 읽고 남은 일부터 이어간다. 다 끝내면 이 파일을 비우고 커밋.

## 오늘 끝낸 것 (참고만)
- 정의문 사슬: 3c74f5ad5·f22ea4fde (문 파괴 → 3제독 → 버프). 노란원숭이·푸른꿩 포함 실측 끝. 3제독은 고정 표적(원작 JASS에 이동 명령 0건, 이속 150은 기본 AI 몫 — PM에 보고, 움직이게 하자는 말 없으면 그대로).

## 남은 일 ① 카메라 구도(사장님 「원랜디처럼」) — 아직 main에 안 넣음
- 참고 사진: Docs/reference/ui/원랜디_카메라구도.png. 사장님 기준 = 「사진과 같은 느낌」. **최종 1장 비교 사진(원작 사진과 나란히) 필수.**
- 계산 결과(PM 승인): 섬이 화면 가로에서 차지하는 비율은 지금도 비슷(흙길 고리 67% vs 사진 59%, 필드 가장자리 포함 ~74%). 진짜 차이는 **원근** — 사진은 안쪽 구조물 가로 폭이 위아래 거의 같음(원근비 ≈0.9~1), 우리는 FOV 60°·피치 50°라 위쪽이 아래의 0.56배.
  필드 폭 781 = 화면 74% → D·tan(FOV/2) ≈ 297. **1차: FOV 30°·피치 56°(워크3 AoA 304°) → D≈1108·높이 ≈850·원근비 ≈0.7.**
- 코드는 브랜치 **camera-original**(커밋 4916fb62e)에 보관: RtsCameraController.FrameLaneAndPen 맨 앞에서 FOV=startFov(30)·피치=startPitch(56) 설정, SerializeField 2개. `git show camera-original:Assets/Scripts/UI/RtsCameraController.cs`로 꺼내 main에 적용(diff +14줄뿐).
- **다음 시도 조건**: 적용 뒤 사진과 비교해 원근이 아직 눈에 띄게 다르면 maxHeight(MapGenerator.SetUpCamera의 420×Scale=1750)를 올려 FOV 20° 안팎(사진 수준 0.9는 FOV≈14°·높이≈1900)까지. PM 허락 받음. 같이 볼 것: 그림자 거리·안개·far clip(4167)·미니맵 시야틀(MinimapCamera.DescribeMapFit)이 높이에 맞게 따라가는지, 멀리서 유닛 이름표·체력바 크기.
- **적용 순서**: 구현담당2(UI 합치기·실측)가 「끝났습니다」 한 뒤. refresh 두 번 → gameshot(해상도 1600x900 박기)로 시작 구도 사진 → 원작 사진과 비교.
- **측정 도구 확인(필수)**: 새 구도에서도 autoloop의 클릭 좌표·RtsCameraController.MoveTo가 동작하는지 짧은 판으로 확인(안 되면 ClaudeCommands도 고칠 것). 시작 구도가 FOV·피치를 바꾸니 rclickpt:corner·boxselect·select: 화면 투영이 달라진다.
- 커밋까지(푸시는 PM).

## 남은 일 ② 오늘 밤 새 기준선 g1_290~299 (사장님: UI 들어간 HEAD에서) — PM의 「기준선 시작」 신호 뒤
- 끈 판 290·292·294·296·298 / 켠 판(support pirate) 291·293·295·297·299.
- 판당 `gameshot b<번호> 75 1600x900 rounds:60 autoloop keeppen sell aim:전설 bosschase mode:보통`(켠 판은 뒤에 ` support pirate`)를 `ClaudeBridge/inbox/g1_<번호>.txt`에. 체인은 /tmp/g1_chain.sh 꼴(없으면 새로): 번호마다 outbox 지우고 inbox 쓰고 `until outbox에 「gameshot 결과」`까지 기다린 뒤 20초 쉬고 다음, 로그 ClaudeBridge/g1_chain.log. `nohup … & disown`(맥엔 setsid 없음). **gameshot outbox는 판이 끝난 뒤에야 생긴다**(안 생겼다고 죽은 게 아님).
- 판 중엔 Assets 쓰기·소스 편집·refresh 금지 — 다른 세션에 먼저 알릴 것. 중단 신호 `touch ClaudeBridge/STOP`.
- 끝나면 통과율·패배 라운드·원인 갈래를 BALANCE 문서 §10(새 기준선)에 정리 + 새로 들어간 것(압살롬 도박·좀비/모리아 100%·10초·정의문 사슬·막타 목재 글자)이 도구 판에 영향 있는지 한 줄씩. 이전 체인은 g1_290이 R30쯤에서 PM이 중단 — 결과 0판.

## 함정 (오늘)
- Monitor에 `tail -F 없는파일`을 걸면 즉시 실패 — 파일 생길 때까지 도는 while 루프로.
- 새 .cs를 Assets에 넣으면 남의 refresh에 같이 컴파일되니 완결된 상태로만. 남의 판 중엔 편집 금지.
- 에셋 YAML을 손으로 만들 때 prefab fileID는 **그때의 정본 에셋에서 복사**(프리팹이 재생성되면 fileID가 바뀐다 — 정의문 3제독 첫 판 소환 실패 원인).
- 씬 커밋: URP 블록(474bcb49…)은 `git hash-object -w`한 정리본을 `update-index --cacheinfo`로 스테이징(경로 없는 commit) — 이 세션은 그 방식으로 3c74f5ad5를 올림.
