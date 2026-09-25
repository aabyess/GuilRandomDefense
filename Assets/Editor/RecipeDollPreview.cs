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
            // 씬 오브젝트(스포너) 아래에 붙이면 씬이 「바뀜」으로 표시된다(09-25 실측 — 무인 도구와 닫을 때 저장 묻기에 걸린다).
            // 인형은 월드 값으로 서므로 부모가 필요 없다 — 씬 루트의 DontSave 묶음으로 둔다.
            GameObject root = new GameObject(PreviewTag) { hideFlags = HideFlags.DontSave };
            foreach (GameObject doll in RecipeDollSpawner.Spawn(root.transform, spawner.Dolls, HideFlags.DontSave))
            {
                // 편집 모드에선 Animator가 자세를 안 쓴다 — 맵 생성기와 같은 길(PlayableGraph)로 Idle을 한 번 입힌다.
                MapGenerator.PoseAsIdle(doll);
                total++;
            }
        }
        watch.Stop();
        string report = $"조합표 인형 {total}기를 미리 세웠습니다({watch.Elapsed.TotalMilliseconds:F0}ms, 씬에 저장되지 않음 · 씬 바뀜 표시 {UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty}).";
        Debug.Log("[조합표 인형] " + report);
        return report;
    }

    /// <summary>씬 바뀜 표시를 되돌린다 — 디스크의 SampleScene을 다시 연다(저장 안 한 변경은 버린다). 브리지 점검용.</summary>
    public static string ReopenDiscard()
    {
        string path = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
        return $"{path}를 다시 열었습니다 · 씬 바뀜 표시 {UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty}.";
    }

    [MenuItem("Tools/맵/조합표 인형 미리보기 지우기")]
    public static string Clear()
    {
        var olds = Resources.FindObjectsOfTypeAll<Transform>().Where(t => t != null && t.name == PreviewTag && t.gameObject.scene.IsValid()).Select(t => t.gameObject).ToList();
        foreach (GameObject g in olds) Object.DestroyImmediate(g);
        // 미리보기는 DontSave라 씬 파일엔 안 들어가지만, 씬을 「바뀜」으로 표시하면 무인 맵 생성이 「저장 안 된 씬」으로 멈춘다 — 그 상태를 같이 알린다.
        return $"미리보기 {olds.Count}묶음을 지웠습니다 · 씬 바뀜 표시 {UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty}.";
    }
}
