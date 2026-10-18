# 구현담당1 인수인계 (10-18 갱신)

새 세션은 이 문서 → `.claude/NEXT_SESSION.md` → TEAM_RULES 순으로 읽는다. 에디터(main)는 꺼져 있다(10-18 마지막에 프로세스 종료 확인).
대기 작업 없음(아래 「다음 판 후보」는 PM 지시가 오면).

## 10-18: S2 2.323 재매칭(사장님 「처음부터 다시 매칭 — 능력치+스킬이 같은 원작, 최신 원랜디 파일」) — 전부 push됨
- 정본 원작: `Tools/w3x/원본_S2_2.323/ORDR_S2_2.323[C].w3x`. W3x2lni 슬크 저장본이라 w3u/w3a엔 거의 없다 → MPQ 안 `units\*.slk`(수치)·`units\campaignunitstrings.txt`·`campaignabilitystrings.txt`(한글 이름·Ubertip 툴팁)에서 읽는다. w3x 앞 512바이트(HM3W) 떼면 MPQ(`~/grd/GRD_rematch/s2.mpq`로 자동 생성). war3map.j는 난독화(rawcode `$68303341`=h03A, 함수명 변조).
- 도구(Tools/, venv `~/grd/GRD_unit_sheets/venv/bin/python` — openpyxl·pyyaml·mpyq):
  - `rematch_s2_data.py` 자료층 · `rematch_s2.py` 제안표(`~/grd/GRD_rematch/재매칭_제안.xlsx`·`재매칭_요약.md`·`rematch.json`·`effects_for_blender.csv`)
  - `rematch_s2_skills.py` 툴팁 → SkillData 효과 변환(못 옮긴 문장은 「미옮김」으로 돌려줌, 지어내지 않음)
  - `rematch_s2_apply.py plan|apply <등급…|all>|vfx` 적용기(로스터 능력치·skills·originalMatchName, `SkillData_S2_<로스터>_<능력>_<n>.asset`, `Docs/research/ORIGINAL_VFX_ASSIGN_S2재매칭.csv`, `Docs/reference/S2_ROSTER_MAP_2026-10-18.csv`)
- 정본 대응표: `Docs/reference/S2_ROSTER_MAP_2026-10-18.csv`(로스터 → S2 원작 id·상태). `rematch_s2.py`는 이 파일로 대응을 **고정**한다(적용 뒤 다시 돌리면 바뀐 수치로 DPS 근접 고르기가 달라져 원작이 뒤바뀌던 함정 — 이민형↔이타도리).
- 결과: 226기 능력치 S2 값(공격력·공속·공격타입·평타 광역, 사거리·체력은 그대로) · 118기 스킬 교체(새 SkillData 265) · 손대지 않음: 대응 없음 7·상붕카(브룩)·사양 「능력치도 고정」 6기·변화됨_박은석(합친 대응 유지). 사양 스킬(SkillData_사장님_*)은 전부 그대로.
- 새 효과 종류 `GrantRandomWisp`(SkillData.cs 맨 뒤 + `rewardWispPool`, UnitAttacker.GrantRandomWisp) — 요츠바 「떼쓰기」.
- 이펙트: PM이 원작 78종 반입(4fbe34e52) → 새 S2 스킬 116개에 연결(능력 아트·트리거 28 + 원작 유닛 트리거 근사 88, 779e1d799), SkillVfxTable 재생성. 드라이브 매칭표 「이펙트」 칸.
- 실측(신 난이도 R50 보스 허수아비 8,745만, 1기씩 60초/막타): 표 `~/grd/GRD_rematch/실측_DPS_전후_2026-10-18.md`. 랜덤 ×0.17(원작의 확률 스킬로), 다른세계 ×10(스킬 0→원작 스킬), 나머지 ×0.7~2.
  - 실측 방법: 재매칭 직전 로스터 사본을 `Assets/G1Probe/Old/<이름>__old.asset`(새 guid)로 임시로 두고 전/후 같은 판에서 잼 → 끝나면 폴더째 삭제, 커밋 금지. 탐침 원본은 세션 스크래치에만 있었다(필요하면 다시 작성: 고리 반경 3000·40기/판·EnemyDummy.Initialize(boss, 8.25)·막타 시각 기록).

## 다음 판 후보(PM 지시 대기)
- 「유지」 143기(원작·스킬 주인 그대로)의 지금 SkillData 수치 vs S2 툴팁 수치 대조: `~/grd/GRD_rematch/유지143_스킬수치_대조.csv` — 83기가 1.5배 넘게 차이(대략 추정 기준). 다음 판에 S2 값으로 맞출지.
- 미옮김 문장 유닛별 목록: `~/grd/GRD_rematch/apply_report.md`(유틸·순간이동·토큰 소환·조건부 추가 피해·마법방어력 감소 등). 원작 스킬 없음: 전설적인_김민규(브륄레 — 변신 유틸뿐).
- 미구현 표 남은 3: 김강민·윤현모(사장님 이름만 스킬)·미래를보는눈.

## 함정(10-18에 실제로 밟음)
- **에디터를 `-projectPath .`로 띄우면 `pkill -f "projectPath /Users/…"`가 안 걸린다** → 「닫음」이라 보고하고 53분 떠 있었고, 두 번째로 띄워 사장님 화면에 「another Unity instance」 창이 떴다. 띄우기 전·닫은 뒤 항상 `ps -ax | grep "[U]nity.app/Contents/MacOS/Unity -projectPath"`로 확인, 닫을 땐 PID로 kill.
- zsh: `$F`는 단어로 안 쪼개진다 → `F=("${(@f)$(...)}")` 배열. `sort -u`는 한글을 같다고 보고 줄을 합친다 → `LC_ALL=C sort -u`.
- python `%` 포맷 문자열 안 `%YAML` → `%%YAML`. 정규식 치환 문자열에 `\u` → 람다로.
- 유니티는 긴 따옴표 문자열을 여러 줄로 접는다(4칸 들여쓰기 이어짐) — 한 줄만 바꾸면 YAML이 깨진다(변화됨_박은석 originalMatchName). 적용 뒤 전수 yaml 파싱으로 확인.
- 툴팁 「대상이 라인 몬스터일 경우 …」는 본 피해에도 포인트값<200 조건을 붙일 것(빼먹으면 보스에게 현재체력 %가 들어감 — 현성현).
- 허수아비 체력을 크게(×1000) 잡으면 %체력 스킬이 부풀어 DPS 실측이 망가진다 → 실제 체력으로 막타 시각까지.
- 메인 작업폴더에서 `check_missing_assets --tracked`는 한글 파일명(NFD↔NFC) 때문에 한글 파일을 통째로 빼먹어 늘 0이 나온다 — 빌드 사본에서 돌리면 진짜 값(0.3.25 때 43개, 9월부터 있던 재질 텍스처 빈칸).

## 10-17~18 그 밖에 한 일(전부 push됨)
- 0.3.25 배포 빌드(맥·윈도우 zip, ~/Desktop/구랜디_베타) · 이재윤 히루루크→레일리(3a3b735e2) · 조합표 제한_박성호 아이템 재료 상자 → 아이템 그림(a602b1a99, MapGenerator.ShowItemIcon) · 엑셀 두 장 도구 `Tools/export_unit_sheets.py`(드라이브 「미구현 스킬 유닛」·「구랜디-원랜디 매칭」, 매칭표는 S2_ROSTER_MAP 우선·이펙트 칸).

## 이전(10-16)
- 특수함 6기 짝 스킬 · 노태현 돌발행동 700 · 강화소 원작 대조 · attackType 원작 연구 종류 64기 · 파칭코 멀티 검증(하네스 `~/grd/GRD_pachinko_mp/run_mp.sh`, 대기 루프에 `pgrep -f GRD_new.app` 금지).
- 미해결 메모: 공속 연구×타입 곱 · 모몬가 R02T · 우솝 R017.

## 10-19 밤: S2 맵 카피(브랜치 map/s2, 사장님 확인 전 — main 병합은 PM)
- 커밋 흐름: a92fdfd8c(레인 크기·간격) → 상점 세로 열 9b03d949f·1aadb851e → 창고·봉인섬·바다 틈·오로성·모서리 섬 → 장식 7종 → 바닥 줄 외형(f55690d22) → main 병합 228832f96. 표·그림·남은 것: `~/grd/GRD_s2_map/결과_1019/`(바뀐값_표.md·남은것.md).
- 상수: `MapLayout.S2OpenGap`(레인 사이 십자 대지 제거) · `OroSeongCenter`·`RumbaSoulCenter`·`PoneglyphCenter` · `ShopColumn*`(상점 열) · `Warehouses`(S2Warehouse) · `SealIslands`(SealIsland). 되돌리기: S2OpenGap=false + Zones의 OroSeongIsland 줄 삭제 + 맵 재생성.
- 함정: 코드를 고친 뒤 **refresh를 먼저**(안 하면 옛 코드로 생성돼 그림이 안 바뀐다). 설계서 좌표 일부는 레인 윗변을 2387로 잘못 잡았음(실제 2217.8).
- 장식: `StructureDresser.PlaceMapLandmarks`(배율 = 원작 배율 × 100 ÷ Scale). 한계: 얼음 바닥=물 질감 대용(gen_IceTile 미사용)·동상/화로 자리 환산 추정·레인 사이 바다 틈(비행 유닛). 못 한 것: 돌담 방 줄·둥근 섬·황금 종 자리. 최종 목록은 ~/grd/GRD_s2_map/결과_1019/남은것.md.
