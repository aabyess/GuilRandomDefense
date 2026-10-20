# 구현담당1 인수인계 (10-16 밤 갱신)

대기 작업 없음. 새 세션은 이 문서 → `.claude/NEXT_SESSION.md` → TEAM_RULES 순으로 읽는다. 에디터(main)는 꺼져 있다.

최근 완료(10-16, 전부 push됨): 특수함 6기 짝 스킬(f5a2fd1a2) · 노태현 초월 돌발행동 범위 700(04c057ecc) · 강화소 원작 대조·유닛 전용 연구 13트랙·랜덤/다른세계 버킷(787c5e85c·12c1e8d24) · 다른세계 가제 트랙 정리(d4a7b4219) · attackType 원작 연구 종류로 64기(d1e771b5b).
참고 산출: Docs/reference/{SKILL_STAT_SOURCE_AUDIT_2026-10-15,UPGRADE_TRACK_AUDIT_2026-10-16,ATTACKTYPE_FOLLOW_2026-10-16}.csv, Tools/{skill_stat_source_audit,upgrade_track_audit,apply_*_1016}.py.
미해결 메모: 공속 연구×타입 곱(원작은 가산 추정, 이번엔 그대로) · 모몬가 R02T 플래그형 · 우솝 R017(레벨 0) · 파칭코 MP 실측·0.3.20/21 런타임 실측은 PM 지시 대기.

## 10-16 밤 추가: 파칭코 멀티 검증(전부 push됨)
- 수정: 호스트 슬롯 plan·PUSH가 클라 관전 모니터에 안 가던 버그(3816eea55·2333282a4), 시험 훅 `-pachinkoAutoPush 초`·`-pachinkoVote 테마 초`·시험 인자 즉시 파싱, 벽시계 로그(f8a6d5698). R70 라인몹 크기 `visualScaleAffectsSkin` 옵트인(eabc0261b).
- 실측 결과: Roll 10만 회 확률 정상(Assets/Editor/PachinkoPlanSample.cs) · 두 창 ①③④⑤⑥ 통과 · 클라 모니터가 약 0.5초 늦음(plan 수신 기준, PM이 「고치지 않는다」).
- 하네스: `~/grd/GRD_pachinko_mp/run_mp.sh <이름> <코드> "<호스트 인자>" "<클라 인자>" [총초] [클라종료초]`. **빌드 앱은 PM이 지웠다** → 다시 빌드: 빌드 사본 `../GuilRandomDefense-build`에서 main ff 후 `Unity -batchmode -quit -projectPath ../GuilRandomDefense-build -buildTarget OSXUniversal -executeMethod BuildBeta.Mac`, Builds/Mac/GuRandi 0.3.24v.app을 ~/grd/GRD_pachinko_mp/GRD_new.app으로 복사.
- 함정: 대기 루프에 `pgrep -f GRD_new.app` 금지(자기 명령줄에 걸려 안 끝남) → `pgrep -x`나 앱 PID. 끝난 뒤 남은 앱은 `pkill -f "GRD_new.app/Contents/MacOS"`. 산출물은 ~/grd/GRD_<이름>/ 에만. screencapture 금지(게임 안 캡처만). PachinkoOverlay.cs는 구현담당2 몫.
- 대기 지시(PM): 구현담당2 병합 때마다 4테마 핵심 시점 두 창 재확인→시트 ~/grd/GRD_pachinko_test/shots/<테마>_<커밋>.png.

## 10-20 낮~밤: 되돌림 이후 · 맵 장식 · 피드백 (최신, 이게 위 10-19 절을 대체)
- **방침(사장님 10-19~20)**: 구버전 원랜디를 따른다 — S2(신작)는 유닛 스킨·능력치만 남기고 맵·퀘스트·이벤트는 되돌림. main = 0.3.25 트리 + 성 테마(ui/castle). 옛 S2 전부는 `s2/parked` 브랜치, S2 맵 카피는 `map/s2` 브랜치에 보관(지우지 말 것).
- **내 브랜치**: `map/selectwisp`(= origin/main에 ff로 계속 따라감, 마지막 0.3.38 152bbacd1). 작업 전 항상 `git fetch && git merge origin/main`(씬 충돌 방지). 메인 폴더 에디터는 내 것이고 구현담당2에게 「에디터 넘김/반납」으로 번갈아 씀.
- **끝난 일(전부 main에 들어감)**: ⑤ 영원 전용 강화 8트랙·히든 R001·변화됨·이충민 R022(구버전 w3q 값) · ① 희귀함 포탈 특수함 확률 1/36(`MapGenerator GachaBands`+씬 값) · 선택 위습 구역 개편(blender 부스 모델 5종 `Assets/Art/Structures/선택위습_*`, BoothRing·UnitPortal.Granted) · 상점 6채 S2 모델→**옛 모델로 되돌림**(`StructureDresser.UseS2ShopModels=false`, S2 모델·받침대·정렬 코드는 스위치 분기에 보관) · 옛 맵에 S2 장식 6종(`OldMapDecor`·`PlaceOldMapLandmarks`; 고잉메리 (−2175,1450)·룸바 (−2230,1800) 1.5배) · 물범 섬 장식 제거(`BareIslands`) · 섬 돌 테두리·창고 문·복귀 포탈 원단(`IslandStoneBorder`, 스위치 `IslandStoneBorderOn`) · 바닥 포인트(`FloorAccentsOn`) · 조합 단추 다중 선택 수정(GameHud) · VFX Graph 패키지 · 쿨다운 캐시 탐침 검증(CooldownEquivProbe) · perf/1020b 검증.
- **되돌려진 것(건드리지 말 것)**: 레인 바닥 디자인(돌길·모래·웅덩이) — 사장님 「너무 구리다」로 폐기, 재료만 `~/grd/GRD_oldmap_lane` 보관. **옛 흙길·잔디 유지.**
- **스위치 표·사진 경로**: `~/grd/GRD_s2_map/결과_1019/밤샘_맵_정리.md`(커밋 해시·끄는 법).
- **남은 일/후보**: 십자 대지 모서리 기둥(보류, `십자대지기둥_시안.png`) · 0.3.38 이후 PM 지시 대기 · 노태현 돌발행동은 코드상 범위 전체 적용 확인(사장님 「주변에 여럿인데 한 마리만」 답 대기) · 이재윤 50% 스킬은 이미 PV<200(보스·스토리·퀘스트 제외)라 변경 없음.
- **함정(이번에 밟음)**: ① 씬 파일을 밖에서 git으로 바꾸면 에디터가 확인 창에서 멈춤 → 에디터를 끄고 바꾼 뒤 재시동 ② **kill 전에 `ps -o pid,command -p <PID>`로 `-projectPath /Users/sang/GitHub/GuilRandomDefense`(메인 폴더, 끝에 -build 없음)인지 확인** — `ps | grep Unity | head -1`로 빌드 배치 유니티를 죽인 적 있음 ③ gameshot 중 refresh·컴파일은 도메인 리로드로 판 무효 ④ 맵 전체 재생성은 저장된 NavMesh를 바꾸니 장식만이면 피하기(메뉴 「옛 맵에 S2 장식 얹기」·「섬 돌 테두리 얹기」 같은 부분 메뉴) ⑤ navlane 앞치마 %는 격자가 렌더러 경계에 따라 움직여 비교 불가 → 정식 도구 「앞치마 NavMesh 구멍 찍기」 ⑥ 새 .cs의 .meta를 에디터가 로컬에서 만들면 남의 guid와 충돌 — main에 올라온 meta를 쓸 것 ⑦ 임시 탐침은 G1* 접두로 만들고 커밋 전에 삭제.
- **에디터 사진 도구**: ClaudeBridge/inbox에 `shot <파일> px py pz tx ty tz fov`(정적), `gameshot …`(플레이, `call:<클래스.함수>`). 내 접두 g1s2_. outbox 결과 파일을 기다릴 때 연쇄 sleep는 막히니 for 루프+짧은 sleep.

