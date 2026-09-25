using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 편집 화면에서 조합표 인형을 미리 세워 본다(2026-09-25). 인형은 이제 실행 때 RecipeDollSpawner가 세우므로
/// 편집 화면의 조합표는 받침만 보인다. 이 메뉴로 세운 인형은 **씬에 저장되지 않는다**(HideFlags.DontSave).
/// </summary>
public static class RecipeDollPreview
{
    const string PreviewTag = "조합표인형_미리보기";

    [MenuItem("Tools/맵/조합표 인형 미리 세우기")]
    public static string Show()
    {
        Clear();
        int total = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (RecipeDollSpawner spawner in Object.FindObjectsByType<RecipeDollSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject root = new GameObject(PreviewTag) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(spawner.transform, false);
            root.hideFlags = HideFlags.DontSave;
            foreach (GameObject doll in RecipeDollSpawner.Spawn(root.transform, spawner.Dolls, HideFlags.DontSave))
            {
                // 편집 모드에선 Animator가 자세를 안 쓴다 — 맵 생성기와 같은 길(PlayableGraph)로 Idle을 한 번 입힌다.
                MapGenerator.PoseAsIdle(doll);
                total++;
            }
        }
        watch.Stop();
        string report = $"조합표 인형 {total}기를 미리 세웠습니다({watch.Elapsed.TotalMilliseconds:F0}ms, 씬에 저장되지 않음).";
        Debug.Log("[조합표 인형] " + report);
        return report;
    }

    [MenuItem("Tools/맵/조합표 인형 미리보기 지우기")]
    public static string Clear()
    {
        var olds = Resources.FindObjectsOfTypeAll<Transform>().Where(t => t != null && t.name == PreviewTag && t.gameObject.scene.IsValid()).Select(t => t.gameObject).ToList();
        foreach (GameObject g in olds) Object.DestroyImmediate(g);
        return $"미리보기 {olds.Count}묶음을 지웠습니다.";
    }
}
