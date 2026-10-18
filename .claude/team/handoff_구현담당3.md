# 구현담당3 인수인계 (2026-10-18) — S2(최신 원랜디 2.323) 규칙 9종 + 맵 목표물

사장님 지시 「S2 아이디어 중 수고 큰 것만 빼고 다 넣자」. 근거 ~/grd/GRD_s2_ideas/ideas.md, 원작 j Tools/w3x/원본_S2_2.323/풀린것/war3map.j. 전부 main 커밋, 실측·사진(~/grd/GRD_patch_0326/<기능>/)까지 끝.

| 기능 | 파일 | 쓰는 법(채팅) |
|---|---|---|
| 자원 전송 | ResourceTransfer.cs | 남의 유닛 선택 + 「-골드/-금/-돈 N」 「-목재 N」 |
| 운세 | S2Fortune.cs | 「-운세」 |
| 데비백파이트 | S2DebeBack.cs | 내 특별함 선택 + 「-데비백」 |
| 경단먹이기 | S2Dango.cs | 「-경단」 → 상대 「-수락/-거절」 |
| 보물상자·원피스 | S2Treasure.cs | 라인몹 처치 1%(사장님 확정) |
| 진화체 | S2Evolution.cs·EvolutionPortal.cs·S2Silhouette.cs·Resources/Wisps/Wisp_진화체 | 시작 진화 위습 → 포탈 → 152킬 |
| 오로성 저주 | OroSeong.cs | 신·악몽 자동 |
| 에그헤드 | Resources/S2/Story14_에그헤드·Enemy_Story14_에그헤드, StoryManager.AppendS2Stories | 신·악몽 13번 뒤 120초 |
| 모드 토글 | GameModes.cs·DifficultySelectHud | 난이도 창 아래 토글 |
| 맵 목표물 | S2MapObjectives.cs·S2Objectives.cs | 스토리존 섬 7시/1시 |
| 판당 상태 초기화 | S2Evolution.ResetAllMatchState(씬 로드마다) | |
| 탐침 | Assets/Editor/S2Probe.cs (`call:S2Probe.X`) | |

## 남은 일
1. **진화 포탈 실측 안 함**: S2Evolution.EnsurePortal()(Portal_유닛랜덤 복제 → 금화 포탈 x·유닛랜덤 z 자리)이 aaf… 이후 커밋됨, 에디터에서 위치·위습 들어가는지(진짜 클릭 rclick:Portal_진화체) 확인 필요. 납작 원기둥 트리거=구 함정은 복제라 같은 모양.
2. **원작 모델 반입(PM이 나에게 맡김, 에디터 판 필요)**: ~/grd/GRD_s2_models/ — 오로성 Saint_*(→ Resources/S2/Saint_<이름>.prefab, ArtBinder 경로·none 층 알파) · 황금 종 `D__modelPR (10)`·룸바 영혼 `D__SmallLight`·엘리베이터 gen_ElevatorPuzzle(장식 3개, 미구현)·에그헤드 gen_Egghead(Enemy_Story14 프리팹 교체, 키 76·바닥 45) · 진화 포탈 바닥 gen_kineticfield. S2MapObjectives는 지금 스토리 건물 프리팹 임시.
3. 베리: S2Treasure가 Berry.Grant 사용 중. 황금 종·영혼 보상은 구현담당2 미션판(S2Objectives 이벤트 구독).
4. 니카 유닛이 로스터에 없어 오로성 에메트 퇴치는 이름 「니카」 유닛이 생길 때 작동(OroSeong.OnUnitSpawned).
5. 오로성 점수판 줄(OroSeong.ScoreboardLine)은 구현담당2가 -점수판에 넣기로(확인 필요).
6. 이세계 기본값: GameModes.OtherWorldDefaultOn=true(원작은 꺼짐) — 사장님 결정 대기.

## 함정
- gameshot 직전 소스·Editor 스크립트를 고치면 「플레이 도중 도메인 리로드」로 무효 → 고친 뒤 한 번 더 보낸다. 판 도중 소스 편집 금지.
- 알림 문구에 `\n`을 넣으면 줄이 겹친다 → S2Notify.Show(줄별).
- NavMesh는 바다까지 한 덩어리 — 섬 경계를 NavMesh에서 못 읽는다. 스토리존 크기는 MapLayout 상수(180×150×4.167).
- 판당 static 상태는 새 판에 남는다 → ResetAllMatchState에 등록.
- csc_check_editor는 낡은 dll이라 새 타입을 못 찾는 오류가 난다(무시, 유니티 컴파일이 정본).
