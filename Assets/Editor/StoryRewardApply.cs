using UnityEditor;
using UnityEngine;

/// <summary>
/// 스토리 보상 원작 맞추기 3건(사장님 10-06) — 원작 war3map.j Trig_Story_reward8·9·11·12·13(13499~13632행). 호출: call StoryRewardApply.Apply (다시 불러도 안전).
///  ① 11·12·13번 클리어: 영웅 전원 경험치 +300(StoryData.heroXpToAllHeroes) ② 8번: 「도움소 잠금」 항법 생존자에게 [히든]실버즈 레일리(h05X) 1기(StoryData.supportLockBonusUnit)
///  ③ 9번 클리어 뒤에만 다른세계 유닛 도박 열림(원작 R02P = h06E/H0AW의 ureq — w3u 디코드 확인): 도박 옵션 requiresUnlock + 9번 StoryData.unlockGamblingOptions.
/// h05X → 우리 로스터 대응은 Docs/reference/MASTER_UID_ROSTER_MAP.csv 「h05X,희귀함_박기찬」(스킨 별명 「실버즈 레일리」는 희귀함_이승우에도 있다 — 모델만).
/// </summary>
static class StoryRewardApply
{
    const string StoriesFolder = "Assets/Data/Stories";
    const string OtherWorldGamble = "Assets/Data/Gambling/Gambling_다른세계 도박.asset";
    const string RayleighUnit = "Assets/Data/Units/Roster/희귀함_박기찬.asset";

    static StoryData Story(string name) => AssetDatabase.LoadAssetAtPath<StoryData>($"{StoriesFolder}/{name}.asset");

    static string Apply()
    {
        var gamble = AssetDatabase.LoadAssetAtPath<GamblingOptionData>(OtherWorldGamble);
        var rayleigh = AssetDatabase.LoadAssetAtPath<UnitData>(RayleighUnit);
        StoryData s8 = Story("Story08_사이버넷"), s9 = Story("Story09_7탄약창"), s11 = Story("Story11_日本"), s12 = Story("Story12_코드잇"), s13 = Story("Story13_쉬었음");
        if (gamble == null || rayleigh == null || s8 == null || s9 == null || s11 == null || s12 == null || s13 == null)
            return $"❌ 에셋 없음(도박 {gamble != null} · 레일리 {rayleigh != null} · 스토리 8 {s8 != null} · 9 {s9 != null} · 11 {s11 != null} · 12 {s12 != null} · 13 {s13 != null})";

        foreach (StoryData s in new[] { s11, s12, s13 }) { s.heroXpToAllHeroes = 300; EditorUtility.SetDirty(s); }
        s8.supportLockBonusUnit = rayleigh; EditorUtility.SetDirty(s8);
        s9.unlockGamblingOptions = new System.Collections.Generic.List<GamblingOptionData> { gamble }; EditorUtility.SetDirty(s9);

        gamble.requiresUnlock = true;
        gamble.unlockRound = 0;   // 보스 처치 해금 아님 — 스토리 9번 보상이 GamblingProgress.Unlock을 직접 부른다
        gamble.unlockHint = "스토리 9번(어인섬) 클리어 후 해금";
        EditorUtility.SetDirty(gamble);
        AssetDatabase.SaveAssets();
        NetSetup.BuildCatalog();
        return $"스토리 보상 적용: 11·12·13 영웅 경험치 300 · 8번 도움소 잠금 보너스 {rayleigh.DisplayName} · 9번 다른세계 도박 해금({gamble.optionName}, 잠금 문구 「{gamble.unlockHint}」)";
    }
}
