using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// RoundManager.rounds를 Assets/Data/Waves 폴더 내용으로 채운다.
/// 라운드가 늘어날 때마다 인스펙터에서 순서대로 끌어다 넣는 건 실수하기 쉬워서 메뉴로 뺐다.
/// </summary>
public static class WaveWiring
{
    const string WaveFolder = "Assets/Data/Waves";
    const string Title = "웨이브 연결";

    [MenuItem("Tools/웨이브/라운드 에셋 연결")]
    static void WireRounds()
    {
        RoundManager manager = Object.FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            EditorUtility.DisplayDialog(Title, "열린 씬에서 RoundManager를 찾지 못했습니다.", "확인");
            return;
        }

        List<WaveData> waves = AssetDatabase
            .FindAssets("t:WaveData", new[] { WaveFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<WaveData>)
            .Where(wave => wave != null)
            .OrderBy(wave => wave.roundNumber)
            .ToList();

        // roundNumber가 겹치면 RoundManager가 먼저 찾은 쪽만 쓰므로 조용히 한 라운드가 사라진다.
        string duplicates = string.Join(", ", waves.GroupBy(w => w.roundNumber)
            .Where(g => g.Count() > 1).Select(g => g.Key.ToString()));

        Fill(new SerializedObject(manager), "rounds", waves);

        WaveSpawner spawner = Object.FindFirstObjectByType<WaveSpawner>(FindObjectsInactive.Include);
        if (spawner != null)
            Fill(new SerializedObject(spawner), "waves", waves);

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        string message = $"라운드 {waves.Count}개를 연결했습니다 " +
                         $"(라운드 {string.Join(", ", waves.Select(w => w.roundNumber))}).\n\n" +
                         (duplicates.Length > 0 ? $"⚠️ roundNumber 중복: {duplicates}\n\n" : "") +
                         "Cmd+S 로 씬을 저장하세요.";
        Debug.Log("[웨이브] " + message);
        EditorUtility.DisplayDialog(Title, message, "확인");
    }

    static void Fill(SerializedObject so, string propertyName, List<WaveData> waves)
    {
        SerializedProperty list = so.FindProperty(propertyName);
        list.ClearArray();
        for (int i = 0; i < waves.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
        }
        so.ApplyModifiedProperties();
    }
}
