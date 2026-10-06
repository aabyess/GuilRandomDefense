using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>첫 화면(NetBoot) 촬영용 — gameshot은 열린 씬에서 플레이를 시작하므로 씬을 바꿔 끼운다(10-06 구현담당3). 찍은 뒤 SampleScene으로 꼭 되돌린다.</summary>
public static class G3SceneMenu
{
    [MenuItem("Tools/Claude/씬 열기: NetBoot")]
    static void OpenNetBoot() => EditorSceneManager.OpenScene("Assets/Scenes/NetBoot.unity", OpenSceneMode.Single);

    [MenuItem("Tools/Claude/씬 열기: SampleScene")]
    static void OpenSample() => EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
}
