using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 영원함 강화소 = 유닛 전용 트랙 8종(사장님 10-06: 「원랜디는 해당 유닛만 업글한다」, (가) 대표값 확정). 설계: Docs/design/ETERNAL_UNIT_UPGRADE_DESIGN_2026-10-06.md.
/// 호출: call EternalUpgradeApply.Apply (다시 불러도 안전). 트랙 8개(UnitUpgrade_영원_{유닛}.asset, targetUnit=그 유닛) 만들고 · 옛 「영원함 강화」(등급 공통) 에셋 삭제 ·
/// 씬 Lane*_영원함강화소의 tracks를 8개로 바꾼다(씬 저장). 끝나면 call NetSetup.BuildCatalog로 멀티 카탈로그를 다시 만들 것.
/// 값 = 원작 영원한 전용 트랙 8종(R007·R00X·R013·R014·R015·R016·R02O·R032, w3q 디코드)의 대표값: 공속 L1 +15%·렙당 +15%, 공격력 렙당 +4,500, 21렙, 첫 110엔 + 렙당 80엔.
/// 유닛마다 값을 달리하려면(원작 8값 배정) 이 표만 바꿔 다시 부르면 된다.
/// </summary>
static class EternalUpgradeApply
{
    const string Folder = "Assets/Data/UnitUpgrades";
    const string OldPath = Folder + "/UnitUpgrade_영원함 강화.asset";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // 영원함 유닛 8종(로스터 이름) — (공속 L1, 공속/렙, 공격력 L1, 공격력/렙). 지금은 전원 대표값.
    static readonly string[] Units = { "영원_김영원", "영원_조세민", "영원_이지원", "영원_문필환", "영원_서민성", "영원_김정래", "영원_윤현모", "영원_최상호" };
    const float SpeedL1 = 0.15f, SpeedPerLevel = 0.15f, PowerL1 = 4500f, PowerPerLevel = 4500f;

    static string Apply()
    {
        var tracks = new List<UnitUpgradeTrackData>();
        foreach (string name in Units)
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
            if (unit == null) return $"❌ 유닛 없음: {name}";
            string path = $"{Folder}/UnitUpgrade_{name}.asset";   // 로스터 이름 기준 — 칭호(unitName)가 바뀌어도 파일이 안 바뀐다
            var track = AssetDatabase.LoadAssetAtPath<UnitUpgradeTrackData>(path);
            if (track == null)
            {
                track = ScriptableObject.CreateInstance<UnitUpgradeTrackData>();
                AssetDatabase.CreateAsset(track, path);
            }
            track.trackName = unit.unitName;
            track.description = $"[영원함] {unit.unitName} 전용 강화 — 이 유닛 한 종만 강화합니다(원작 영원한 강화소: 유닛마다 연구가 따로, 그 유닛에만 적용). 유닛이 없어도 살 수 있습니다. 원작 영원한 전용 트랙 8종의 대표값(R015·R016 근처): 공속 +15%·렙당 +15%, 공격력 렙당 +4,500, 21렙, 첫 110엔·이후 렙당 80엔.";
            track.targetGrades = new List<UnitGrade>();
            track.targetUnit = unit;
            track.maxLevel = 21;
            track.costBase = 110;
            track.costGrowthPerLevel = 80f;
            track.statLevel1Multiplier = SpeedL1;
            track.statGrowthPerLevel = SpeedPerLevel;
            track.statLevel1Bonus = PowerL1;
            track.statBonusGrowthPerLevel = PowerPerLevel;
            track.slotColor = UnitGrade.Eternal.Color();
            track.hasOriginalResearch = true;
            EditorUtility.SetDirty(track);
            tracks.Add(track);
        }
        AssetDatabase.SaveAssets();

        bool deleted = AssetDatabase.LoadAssetAtPath<UnitUpgradeTrackData>(OldPath) != null && AssetDatabase.DeleteAsset(OldPath);

        var scene = EditorSceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int shops = 0;
        foreach (UnitUpgradeShop shop in Object.FindObjectsByType<UnitUpgradeShop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!shop.name.Contains("영원함강화소")) continue;
            var so = new SerializedObject(shop);
            SerializedProperty list = so.FindProperty("tracks");
            list.ClearArray();
            for (int i = 0; i < tracks.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = tracks[i];
            }
            so.ApplyModifiedProperties();
            shops++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return $"영원함 유닛 전용 트랙 {tracks.Count}개 · 옛 등급 트랙 {(deleted ? "삭제" : "없음/삭제 실패")} · 씬 영원함강화소 {shops}곳 갱신 · 씬 저장 {saved}";
    }
}
