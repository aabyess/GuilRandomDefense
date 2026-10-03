using System.Collections.Generic;

// 자동 생성(Tools/ui/gen_hero_table.py) — 원작에서 영웅 클래스(H 아이디 + 영웅 베이스)인 유닛에 대응하는 로스터 에셋 이름. 손으로 고치지 말 것.
public static class UnitHeroTable
{
    static readonly HashSet<string> Names = new HashSet<string>
    {
        "초월_구주호_AD",
        "초월_김민준_AP",
    };

    public static bool IsHero(UnitData data) => data != null && Names.Contains(data.name);
}
