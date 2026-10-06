# 구현담당1 인수인계 — 비어 있음 (10-06 밤, 큐 전부 끝)

직전 세션이 받은 큐(김만경 → 구주호 → 김경현 → 불멸 8종 → 강화소 재배치·상점 QWER → 유닛회유)를 전부 커밋했다. 남은 일은 PM 지시를 기다린다.
함정만 남긴다:
- 씬 커밋은 `git show HEAD:… ` 본문에서 내 hunk만 바꾼 파일을 `git hash-object -w` → `git update-index --cacheinfo`로 올린다(작업 트리 SampleScene에 남의 직렬화 잔여물). 경로 없이 `git commit`하기 전 `git diff --cached --name-only`로 남이 올린 것 확인.
- 상점은 이제 윗줄 Q W E R도 칸이다(GameHud.ShopSlotOrder). 새 상점 칸을 만들 땐 논리 0~11 = Q W E R / A S D F / Z X C V.
- 유닛회유 회유 유닛 모델은 슈가 장난감 프리팹(임시, 「Unit_흔함_강재규」로 보임) — 사장님이 모델을 정하면 Summon_회유_적.prefab만 바꾼다.
- 미실측: 마나 스킬(정윤식·이승우·고도현)·범퍼 범위 실피해·라인딜·보잡·유닛삭제·순간이동 발동·방무뎀·폭뎀, 노획물 180초 실주기.
