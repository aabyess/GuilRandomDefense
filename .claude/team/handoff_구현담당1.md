# 구현담당1 인수인계 (10-07 마무리)

## 끝난 일 (전부 커밋, main)
- 초월 23·불멸 8·회유·강화소 재배치/가격/보너스·FlyingMover 확인·아이콘·일시정지(GamePause, 938200efa)·MP 1차 점검(NetEntity.EnhanceLevel, d36fbddc5).
- 상위 등급 공격력·공속 원작 재이식 적용(872e1ef70, 표 Docs/research/ATTACK_REINSTALL_TABLE_2026-10-07.tsv, 도구 AttackReinstallApply).
- 초월 25종 합계 DPS 표(bd1c63e52): Docs/research/TRANSCEND_DPS_AFTER_REINSTALL_2026-10-07.md + Tools/transcend_dps_table.py. 컨셉 유닛 결과: 황준석·김민준·최상호 AD(보스)·최상호 AP(몹)가 하위 25%, 조성진·임채민은 상위.

## 남은 일
- MP 2차 미실측 넷: ① 토토 성공 분기(GameHud.TestTotoRoll) ② 재접속(-mpDropAt/-mpRejoinAfter)에서 회유 유닛·강화 레벨·비행 유닛 위치 유지 ③ MP 회유 전투 ④ 비행 유닛 Stop/Hold 클라 RPC.
  - 🔴 이 테스트용 빌드 사본 변경(NetMainTest2.cs, GameHud TestTotoRoll, NetLauncher -mpTestMain2)은 **사라졌다**: 빌드 사본(../GuilRandomDefense-build)이 다른 세션 때문에 dev/minsu-voice로 바뀌어 있고 내 파일이 없다. 다시 하려면 main의 NetMainTest.cs(d36fbddc5)를 바탕으로 새로 쓴다.
- 이전 미실측: 마나 스킬(정윤식·이승우·고도현)·범퍼 범위 실피해·라인딜·보잡·유닛삭제·순간이동 발동·방무뎀·폭뎀, 노획물 180초 실주기.
- 업그레이드 트랙 설명 에셋에 플레이어에게 보이는 낡은 내부 문구가 남아 있다(확인 필요).
- DPS 표의 한계: 스턴·방깍·오라·버프·DoT·소환 제외(지원형 낮게 나옴), 최상호 AP 절대공격 공속비례 ×1~4 미반영, 엄태웅 폭탄제조(쿨<2초 버튼) 제외.

## 위치
- 내 미커밋 없음. 작업 트리의 Assets 변경(DuyuchanApply·두유찬 에셋·GameHud·NetGameState·RewardDistributor·StoryManager·폰트/이펙트 머티리얼)은 다른 세션 것 — 건드리지 말 것.
- 빌드 사본 ../GuilRandomDefense-build 는 현재 dev/minsu-voice(남의 브랜치). 내 dev/mp-check2 는 3cd616180에 깨끗하게 남아 있음(미커밋 변경 없음).

## 함정
- 커밋은 `git add -- 경로` + `git commit -- 경로`. 씬 커밋은 `git show HEAD:…` 본문에서 내 hunk만 바꾼 파일을 hash-object -w → update-index --cacheinfo (SampleScene에 남의 직렬화 잔여물, URP 조명 474bcb49… 제외).
- 상점 논리 칸 0~11 = Q W E R / A S D F / Z X C V (GameHud.ShopSlotOrder).
- 회유 모델은 임시(슈가 장난감 프리팹) — 사장님이 정하면 Summon_회유_적.prefab만 교체.
- 새 UnitData/ItemData를 만들면 NetSetup.BuildCatalog 재생성(호스트·클라 빌드 동일).
- csc 확인은 `| grep -c " error"` (tail -1은 오류를 숨김).
