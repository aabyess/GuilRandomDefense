using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 유물 목록 채우기(사장님 10-07 「유물은 얻은 것과 똑같은 게 중복되지 않게」) — 분실된지갑을 유물(isRelic)로 표시하고, isRelic인 ItemData 전부를 씬 RewardDistributor.relicPool에 잇는다.
/// 부르기: call RelicPoolApply.Apply (다시 불러도 안전)
/// </summary>
static class RelicPoolApply
{
    static string Apply()
    {
        var wallet = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/ItemData_L006_분실된지갑.asset");
        if (wallet != null && !wallet.isRelic) { wallet.isRelic = true; EditorUtility.SetDirty(wallet); AssetDatabase.SaveAssets(); }
        var relics = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/Data/Items" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ItemData>).Where(i => i != null && i.isRelic).OrderBy(i => i.name, System.StringComparer.Ordinal).ToList();
        var rd = Object.FindFirstObjectByType<RewardDistributor>(FindObjectsInactive.Include);
        if (rd == null) return "❌ RewardDistributor 없음";
        var so = new SerializedObject(rd);
        var list = so.FindProperty("relicPool");
        list.ClearArray();
        for (int i = 0; i < relics.Count; i++) { list.InsertArrayElementAtIndex(i); list.GetArrayElementAtIndex(i).objectReferenceValue = relics[i]; }
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(rd.gameObject.scene);
        EditorSceneManager.SaveScene(rd.gameObject.scene);
        return $"유물 {relics.Count}종 연결: {string.Join(", ", relics.Select(r => r.itemName))} · 씬 저장";
    }
}
