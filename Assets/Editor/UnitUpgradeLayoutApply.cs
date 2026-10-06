using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 유닛강화소 칸 재배치 + 히든 합치기 + 특수함 합치기(사장님 10-06 — 「히든은 전설적인 강화와 같이」 「특수함이랑 제한됨 같이 업글됨」).
/// 호출: call UnitUpgradeLayoutApply.Apply (다시 불러도 안전). 전설적인 트랙 targetGrades에 Hidden · 제한됨 트랙에 Superior 추가 · 「히든 강화」 트랙 삭제 ·
/// 씬의 유닛강화소 tracks 순서를 새로 정한다 · NetCatalog 재생성(NetSetup.BuildCatalog).
/// 칸 순서(논리 0~7)는 GameHud.UnitCommandResultSlotOrder로 화면 칸이 된다: 0~3 = 윗줄(Q W E R), 4~7 = 둘째 줄(A S D F).
/// </summary>
static class UnitUpgradeLayoutApply
{
    const string Folder = "Assets/Data/UnitUpgrades";
    // 논리 순서(상점은 윗줄도 칸이다 — GameHud.ShopSlotOrder): Q W E R = 흔함·안흔함 · 특별함 · 희귀함 · 전설·히든 / A S D F = 랜덤유닛 · 제한됨·특수함 · 불멸 · 초월.
    static readonly string[] Order =
    {
        "흔함·안흔함 강화", "특별함 강화", "희귀함 강화", "전설적인 강화",
        "랜덤유닛 강화", "제한됨 강화", "불멸 강화", "초월 강화",
    };

    static UnitUpgradeTrackData Load(string name) => AssetDatabase.LoadAssetAtPath<UnitUpgradeTrackData>($"{Folder}/UnitUpgrade_{name}.asset");

    static string Apply()
    {
        var legendary = Load("전설적인 강화");
        var limited = Load("제한됨 강화");
        var hidden = Load("히든 강화");
        if (legendary == null || limited == null) return "❌ 전설적인·제한됨 트랙 에셋 없음";

        if (!legendary.targetGrades.Contains(UnitGrade.Hidden)) legendary.targetGrades.Add(UnitGrade.Hidden);
        legendary.trackName = "전설·히든 강화";
        EditorUtility.SetDirty(legendary);
        if (!limited.targetGrades.Contains(UnitGrade.Superior)) limited.targetGrades.Add(UnitGrade.Superior);
        limited.trackName = "제한됨·특수함 강화";
        EditorUtility.SetDirty(limited);

        var tracks = new List<UnitUpgradeTrackData>();
        foreach (string n in Order)
        {
            var t = Load(n);
            if (t == null) return $"❌ 트랙 에셋 없음: {n}";
            tracks.Add(t);
        }

        int shops = 0;
        foreach (UnitUpgradeShop shop in Object.FindObjectsByType<UnitUpgradeShop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(shop);
            SerializedProperty prop = so.FindProperty("tracks");
            bool isUnitShop = false;
            for (int i = 0; i < prop.arraySize; i++) if (prop.GetArrayElementAtIndex(i).objectReferenceValue == legendary) isUnitShop = true;
            if (!isUnitShop) continue;   // 다른세계·영원함 강화소는 건드리지 않는다
            prop.ClearArray();
            for (int i = 0; i < tracks.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = tracks[i];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(shop);
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
            shops++;
        }

        if (hidden != null) AssetDatabase.DeleteAsset($"{Folder}/UnitUpgrade_히든 강화.asset");
        AssetDatabase.SaveAssets();
        NetSetup.BuildCatalog();
        EditorSceneManager.SaveOpenScenes();
        return $"유닛강화소 {shops}곳 칸 {tracks.Count}개로 재배치 · 전설·히든({legendary.targetGrades.Count}등급) · 제한됨·특수함({limited.targetGrades.Count}등급) · 히든 강화 트랙 {(hidden != null ? "삭제" : "이미 없음")}";
    }
}
