using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 도박소 「물품 지원」(원작 h0AX, 사장님 10-08 밤 「원작대로」) — 씬 Lane*_도박소의 moneyOptions 끝에 Gambling_물품 지원을 더한다(씬 저장).
/// 호출: call GambleStarterApply.Apply (다시 불러도 안전). 끝나면 NetSetup.BuildCatalog를 불러 멀티 카탈로그에 새 옵션을 넣는다.
/// 지도 전체 재생성(MapGenerator)은 씬에서 고친 것을 되돌리니 쓰지 말고 이 도구를 쓴다(MapGenerator의 목록에도 같은 줄을 넣어 뒀다).
/// </summary>
static class GambleStarterApply
{
    const string OptionPath = "Assets/Data/Gambling/Gambling_물품 지원.asset";

    static string Apply()
    {
        var option = AssetDatabase.LoadAssetAtPath<GamblingOptionData>(OptionPath);
        if (option == null) return "❌ 물품 지원 에셋 없음";
        var shops = Object.FindObjectsByType<GamblingShop>(FindObjectsSortMode.None);
        if (shops.Length == 0) return "❌ 씬에 GamblingShop 없음(씬이 열려 있나)";
        int added = 0;
        foreach (GamblingShop shop in shops)
        {
            var so = new SerializedObject(shop);
            SerializedProperty list = so.FindProperty("moneyOptions");
            bool has = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == option) has = true;
            if (has) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = option;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(shop);
            added++;
        }
        if (added > 0) EditorSceneManager.SaveScene(shops[0].gameObject.scene);
        NetSetup.BuildCatalog();
        return $"물품 지원 연결: 도박소 {shops.Length}곳 중 {added}곳에 추가(나머지는 이미 있음) · 카탈로그 다시 만듦";
    }
}
