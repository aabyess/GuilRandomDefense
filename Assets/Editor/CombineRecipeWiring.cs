using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// CombineSystem.recipes를 인스펙터에서 일일이 드래그하지 않고 채우기 위한 에디터 도구.
/// 레시피가 199개라 수작업 연결이 현실적이지 않아 메뉴로 뺐다.
/// </summary>
public static class CombineRecipeWiring
{
    const string RecipeFolder = "Assets/Data/Recipes";

    [MenuItem("Tools/조합 레시피/테스트용만 연결 (흔함→안흔함 13개)")]
    static void WireTestRecipes()
    {
        Wire(path => System.IO.Path.GetFileName(path).StartsWith("안흔함_"), "테스트용(안흔함)");
    }

    [MenuItem("Tools/조합 레시피/전체 연결")]
    static void WireAllRecipes()
    {
        Wire(_ => true, "전체");
    }

    [MenuItem("Tools/조합 레시피/연결 해제")]
    static void ClearRecipes()
    {
        Wire(_ => false, "없음");
    }

    static void Wire(System.Func<string, bool> filter, string label)
    {
        CombineSystem system = Object.FindFirstObjectByType<CombineSystem>(FindObjectsInactive.Include);
        if (system == null)
        {
            EditorUtility.DisplayDialog("조합 레시피 연결",
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
        Debug.Log($"[조합 레시피] {label} {recipes.Count}개를 {system.name}에 연결했습니다. 씬을 저장(Cmd+S)하세요.");
    }
}
