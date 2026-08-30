using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// GameHud는 Awake에서 uGUI 계층을 스스로 만들기 때문에 프리팹이 필요 없다.
/// 씬에 GameObject 하나 + 컴포넌트 하나만 있으면 되고, 그 한 번을 메뉴로 처리한다.
/// </summary>
public static class HudWiring
{
    const string Title = "하단 HUD";

    [MenuItem("Tools/HUD/씬에 적 체력바 추가")]
    static void AddHealthBars()
    {
        if (!EditorGuards.RequireEditMode(Title)) return;

        HealthBarLayer existing = Object.FindFirstObjectByType<HealthBarLayer>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog(Title, $"이미 씬에 있습니다: {existing.gameObject.name}", "확인");
            return;
        }

        GameObject layer = new GameObject("HealthBarLayer", typeof(HealthBarLayer));
        Undo.RegisterCreatedObjectUndo(layer, "Add HealthBarLayer");
        Selection.activeGameObject = layer;

        EditorSceneManager.MarkSceneDirty(layer.scene);
        EditorUtility.DisplayDialog(Title,
            "적 체력바를 씬에 추가했습니다.\nCanvas와 바는 실행 시 자동 생성됩니다.\n\nCmd+S 로 저장하세요.", "확인");
    }

    [MenuItem("Tools/HUD/씬에 하단 HUD 추가")]
    static void AddHud()
    {
        if (!EditorGuards.RequireEditMode(Title)) return;

        GameHud existing = Object.FindFirstObjectByType<GameHud>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog(Title,
                $"이미 씬에 있습니다: {existing.gameObject.name}\nHierarchy에서 선택해 뒀습니다.", "확인");
            return;
        }

        GameObject hud = new GameObject("GameHud", typeof(GameHud));
        Undo.RegisterCreatedObjectUndo(hud, "Add GameHud");
        Selection.activeGameObject = hud;

        EditorSceneManager.MarkSceneDirty(hud.scene);
        EditorUtility.DisplayDialog(Title,
            "GameHud를 씬에 추가했습니다.\n\nCanvas·하단 바·명령 그리드는 실행 시 자동 생성되므로\n" +
            "Hierarchy에는 GameObject 하나만 보이는 게 정상입니다.\n\nCmd+S 로 저장하세요.", "확인");
    }
}
