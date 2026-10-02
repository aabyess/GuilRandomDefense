using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 압살롬 도박(원작 h069)을 씬의 모든 도박소 unitOptions 5번째 칸에 넣는다 — 맵을 다시 만들지 않고 이미 있는 씬에 반영.
/// MapGenerator는 같은 목록(5개)을 새로 만든다. 다시 돌려도 같은 결과(이미 있으면 건너뜀).
/// 끝나면 「Tools/Net/카탈로그 다시 만들기」(새 유닛·도박 옵션이 카탈로그에 들어간다)와 씬 저장은 따로.
/// </summary>
public static class AbsalomWiring
{
    const string OptionPath = "Assets/Data/Gambling/Gambling_압살롬 도박.asset";

    [MenuItem("Tools/압살롬/도박소에 압살롬 도박 넣기")]
    public static void Wire()
    {
        GamblingOptionData option = AssetDatabase.LoadAssetAtPath<GamblingOptionData>(OptionPath);
        if (option == null) { Debug.LogError("[압살롬] 도박 옵션 에셋이 없습니다: " + OptionPath); return; }

        int changed = 0, total = 0;
        foreach (GamblingShop shop in Object.FindObjectsByType<GamblingShop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            total++;
            SerializedObject so = new SerializedObject(shop);
            SerializedProperty list = so.FindProperty("unitOptions");
            bool has = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == option) has = true;
            if (has) continue;
            // 칸 7번 = unitOptions[4]이므로 정확히 4개(하·중·고급·다른세계)일 때만 뒤에 붙인다.
            if (list.arraySize != 4) { Debug.LogWarning($"[압살롬] {shop.name}: unitOptions가 {list.arraySize}개라 건너뜀"); continue; }
            list.InsertArrayElementAtIndex(4);
            list.GetArrayElementAtIndex(4).objectReferenceValue = option;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
            changed++;
        }
        Debug.Log($"[압살롬] 도박소 {total}곳 중 {changed}곳에 압살롬 도박을 넣었습니다(씬은 저장 안 함).");
    }
}
