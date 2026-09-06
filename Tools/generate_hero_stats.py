#!/usr/bin/env python3
"""01번 영웅 스탯 — primaryStat 필드를 초월함 24기(이미 배정된 성장치 기준)에 채운다.

⚠️ 처음부터 다시 배정하지 않는다. `c80c531`(01번 실제 점화)이 이미 초월함 25기의
strengthPerLevel/agilityPerLevel/intelligencePerLevel을 원작 분포(STR7:AGI8:INT10)로
배정해뒀다 — 그 커밋을 다시 덮어쓰면 DPS 순위 기반 배정과 타시기(김민준)·야마토(구주호)
예외 곡선이 사라진다. 이 스크립트는 "이미 있는 성장치 중 어느 게 0.85(주스탯)인가"만
읽어서 새 필드(primaryStat)로 옮겨 적을 뿐, base*/​*PerLevel 값 자체는 절대 안 건드린다.

✅ `초월_김민준_AP`(타시기 대응, 성장 0.42/1.70/0.42=AGI 지배)는 스크립트 밖에서 직접
`primaryStat: 2`(Agility)를 채웠다(PM 확정, 2026-09-07) — 그의 스킬(SkillData_게이트_
초월_김민준_AP)이 CasterStrength(basis 9)를 읽는 것과는 **별개 축**이다.
`UnitData.primaryStat`은 원작 `upra`(주 능력치, 엔진 StrAttackBonus×800·
AgiAttackSpeedBonus가 구동하는 대상)에 대응하고, 스킬이 어느 스탯을 basis로 읽는지는
"그 유닛이 그 스탯 값을 실제로 갖고 있는가"라는 다른 질문이다 — 타시기는 upra=AGI
이면서 스킬은 STR을 읽는 원작 그대로의 구조이지, 어긋남이 아니다("모양이 같아도
대응은 아니다"). **우선순위 규칙**: PM 규칙①("스킬이 읽는 스탯=주스탯")은 주스탯이
안 알려진 유닛을 추정하는 휴리스틱일 뿐이다 — **이미 원작 성장곡선이 박혀 있으면
곡선이 이긴다.** 이 스크립트가 "성장치에서 유도"만 하는 이유가 정확히 이거다.

✅ 영원(grade=9) 8종은 **의도적으로 비워둔 채로 확정**됐다(PM 재확인, 2026-09-07) —
원작 실측이 초월함 38+영원 1=39기 중 영원 등급에서 스탯을 가진 건 **1기(루피
기어피프스)뿐**이다. 우리 영원 8기 중 누가 그 1기에 대응하는지는 이름 매핑이
이미 불가능으로 닫힌 문제라 정할 수 없고, 8기 전부에 뿌리면 원작에 없는 축을
7기에 만드는 것이라 "전부 원작대로" 원칙에 어긋난다. **다음 사람이 "영원이 비었네"
하고 채우지 말 것** — 이 스크립트도, UnitData.primaryStat 주석도 이 이유를 못
박아둔다.
"""
import re
import glob

ROSTER = "Assets/Data/Units/Roster/*.asset"

EXCLUDE = {
    # 타시기 대응. primaryStat=Agility(그의 성장곡선과 일치)를 이미 asset에 직접
    # 넣었다(PM 확정) — 이 스크립트는 재실행해도 그 파일을 다시 안 건드린다.
    "초월_김민준_AP.asset",
}

STAT_FIELDS = ["strengthPerLevel", "agilityPerLevel", "intelligencePerLevel"]
STAT_NAMES = ["Strength", "Agility", "Intelligence"]
PRIMARY_STAT_VALUE = {"Strength": 1, "Agility": 2, "Intelligence": 3}


def main():
    updated = []
    skipped_grade9 = []

    for path in sorted(glob.glob(ROSTER)):
        text = open(path, encoding="utf-8").read()
        grade = int(re.search(r"^  grade: (\d+)", text, re.M).group(1))
        name = path.split("/")[-1]

        if grade == 9:
            skipped_grade9.append(path)
            continue
        if grade != 7:
            continue
        if name in EXCLUDE:
            continue
        if re.search(r"^  primaryStat: ", text, re.M):
            continue  # 이미 있으면 손 안 댐(재실행 안전)

        values = []
        for field in STAT_FIELDS:
            m = re.search(rf"^  {field}: (.*)$", text, re.M)
            values.append(float(m.group(1)) if m else 0.0)

        dominant_index = max(range(3), key=lambda i: values[i])
        stat = STAT_NAMES[dominant_index]

        if not text.endswith("\n"):
            text += "\n"
        text += f"  primaryStat: {PRIMARY_STAT_VALUE[stat]}\n"
        open(path, "w", encoding="utf-8").write(text)
        updated.append((path, stat, values))

    print(f"primaryStat 채움: {len(updated)}개 (기존 성장치에서 그대로 유도, 값 변경 없음)")
    for path, stat, values in updated:
        print(f"  {path.split('/')[-1]:30s} {values} -> {stat}")

    print(f"\n제외(타시기 대응, primaryStat=Agility로 이미 직접 배정됨): {sorted(EXCLUDE)}")
    print(f"제외(영원 {len(skipped_grade9)}종 — 원작 표본 1기·이름매핑 불가로 의도적 공백, PM 확정):")
    for path in skipped_grade9:
        print(f"  {path}")


if __name__ == "__main__":
    main()
