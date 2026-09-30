# 원작 스킬 전수 조사 스크립트 (2026-09-30, Blender 세션 스크래치에서 옮김)

`Docs/research/SKILL_COVERAGE_BY_GRADE.csv/.md`와 `IMMORTAL_FILL_LIST.md`를 만든 읽기 전용 스크립트다.
스크래치(세션이 끝나면 사라지는 폴더)에 있던 것을 **그대로** 옮겼다 — 경로가 스크래치 기준으로 박혀 있을 수 있으니
돌리기 전에 각 파일 머리의 경로부터 볼 것. 로스터·스킬 에셋에는 쓰지 않는다(쓰는 코드가 있으면 돌리지 말 것).

- jparse·orig·ours·join·md: 원작 314 유닛 × 우리 SkillData 대조표(그물: uabi · UnitAddAbility · HashAttack 평타 트리거 · TriggerExecute 폐쇄)
- imm_*: 불멸 등급 「채울 목록」용 덤프(유닛별 능력 필드·트리거 본문·우리 쪽 에셋)
- units.py·abils.py: 원작 유닛·능력 표(json) 다시 만들기
- roster_side.py: md.py가 읽는 `roster_side.json`(로스터 쪽 대응·스킬 유무) 생성. `--diff expected/roster_side.json`으로 09-30 21:01 출력과 비교
