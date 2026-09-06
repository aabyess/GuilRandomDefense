#!/usr/bin/env python3
"""01번 영웅 스탯 — primaryStat 필드를 초월함 24기(이미 배정된 성장치 기준)에 채운다.

⚠️ 처음부터 다시 배정하지 않는다. `c80c531`(01번 실제 점화)이 이미 초월함 25기의
strengthPerLevel/agilityPerLevel/intelligencePerLevel을 원작 분포(STR7:AGI8:INT10)로
배정해뒀다 — 그 커밋을 다시 덮어쓰면 DPS 순위 기반 배정과 타시기(김민준)·야마토(구주호)
예외 곡선이 사라진다. 이 스크립트는 "이미 있는 성장치 중 어느 게 0.85(주스탯)인가"만
읽어서 새 필드(primaryStat)로 옮겨 적을 뿐, base*/​*PerLevel 값 자체는 절대 안 건드린다.

⚠️ `초월_김민준_AP`(타시기 대응, 성장 0.42/1.70/0.42=AGI 지배)는 이 스크립트에서 뺐다 —
그의 스킬(SkillData_게이트_초월_김민준_AP)이 CasterStrength(basis 9)를 읽어서, "스킬이
읽는 스탯=주스탯"이라는 PM 규칙①과 "이미 배정된 성장곡선=AGI 지배"라는 기존 데이터가
서로 어긋난다. 원작 타시기 자체가 이 어긋남을 갖고 있었다(upra=AGI인데 스킬은 STR을
읽음) — 어느 쪽을 UnitData.primaryStat으로 볼지는 PM 확인 후 별도 처리한다.

영원(grade=9) 8종도 이 스크립트에서 뺐다 — `c80c531`이 "원작 영원한 등급 스탯 보유자가
1기(루피 기어5)뿐이라 우리 8기 전부에 뿌리면 없는 축을 만드는 것"이라며 의도적으로
안 건드렸다. PM이 이번에 33기(초월+영원)를 요청해 그 판단을 뒤집을지 확인이 필요하다.
"""
import re
import glob

ROSTER = "Assets/Data/Units/Roster/*.asset"

EXCLUDE = {
    "초월_김민준_AP.asset",  # 타시기 대응, upra-vs-스킬basis 충돌 — PM 확인 대기
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

    print(f"\n제외(예외 처리 대기, PM 확인 필요): {sorted(EXCLUDE)}")
    print(f"제외(영원 {len(skipped_grade9)}종, c80c531이 의도적으로 비워둠 — PM 확인 필요):")
    for path in skipped_grade9:
        print(f"  {path}")


if __name__ == "__main__":
    main()
