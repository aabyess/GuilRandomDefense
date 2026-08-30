using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// CombineSystem.recipes를 인스펙터에서 일일이 드래그하지 않고 채우기 위한 에디터 도구.
/// 레시피가 199개라 수작업 연결이 현실적이지 않아 메뉴로 뺐다.
/// 결과는 Console뿐 아니라 대화상자로도 알린다 — 조용히 끝나면 실패와 구분이 안 된다.
/// </summary>
public static class CombineRecipeWiring
{
    const string RecipeFolder = "Assets/Data/Recipes";
    const string Title = "조합 레시피 연결";

    [MenuItem("Tools/조합 레시피/테스트용만 연결 (흔함→안흔함 13개)")]
    static void WireTestRecipes()
    {
        if (!EditorGuards.RequireEditMode(Title)) return;

        Wire(path => System.IO.Path.GetFileName(path).StartsWith("안흔함_"), "테스트용(안흔함)");
    }

    [MenuItem("Tools/조합 레시피/전체 연결")]
    static void WireAllRecipes()
    {
        if (!EditorGuards.RequireEditMode(Title)) return;

        Wire(_ => true, "전체");
    }

    [MenuItem("Tools/조합 레시피/연결 해제")]
    static void ClearRecipes()
    {
        if (!EditorGuards.RequireEditMode(Title)) return;

        Wire(_ => false, "없음");
    }

    [MenuItem("Tools/조합 레시피/현재 상태 확인")]
    static void Diagnose()
    {
        string[] guids = AssetDatabase.FindAssets("t:CombineRecipe");
        string[] inFolder = AssetDatabase.FindAssets("t:CombineRecipe", new[] { RecipeFolder });
        CombineSystem[] systems = Object.FindObjectsByType<CombineSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        string report =
            $"프로젝트 전체 CombineRecipe 에셋: {guids.Length}개\n" +
            $"{RecipeFolder} 안에서 찾은 개수: {inFolder.Length}개\n" +
            $"열린 씬의 CombineSystem: {systems.Length}개\n";

        foreach (CombineSystem system in systems)
        {
            SerializedProperty list = new SerializedObject(system).FindProperty("recipes");
            report += $"  · {system.gameObject.name} → 현재 연결된 레시피 {list.arraySize}개\n";
        }

        Debug.Log("[조합 레시피] " + report);
        EditorUtility.DisplayDialog(Title, report, "확인");
    }

    static void Wire(System.Func<string, bool> filter, string label)
    {
        CombineSystem system = Object.FindFirstObjectByType<CombineSystem>(FindObjectsInactive.Include);
        if (system == null)
        {
            EditorUtility.DisplayDialog(Title,
                "열려 있는 씬에서 CombineSystem을 찾지 못했습니다.\n씬을 먼저 열어주세요.", "확인");
            return;
        }

        List<CombineRecipe> recipes = AssetDatabase
            .FindAssets("t:CombineRecipe", new[] { RecipeFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(filter)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<CombineRecipe>)
            .Where(recipe => recipe != null)
            .ToList();

        SerializedObject so = new SerializedObject(system);
        SerializedProperty list = so.FindProperty("recipes");
        list.ClearArray();
        for (int i = 0; i < recipes.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
        }
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
        EditorUtility.SetDirty(system);

        string message = $"{system.gameObject.name}의 Recipes를 {label} {recipes.Count}개로 채웠습니다.\n\n" +
                         "Cmd+S 로 씬을 저장하세요.";
        Debug.Log("[조합 레시피] " + message);
        EditorUtility.DisplayDialog(Title, message, "확인");
    }
}
