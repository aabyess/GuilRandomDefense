using UnityEngine;

/// <summary>
/// 배포 버전 — 사장님 2026-09-26 「배포 할 때마다 1.1.0v 이런 식으로」. **여기 한 곳만 고친다.**
/// BuildBeta가 빌드 직전에 PlayerSettings.bundleVersion(= Application.version)에 넣고, 압축 파일 이름에도 쓴다.
/// 이미 보낸 0926 베타(표시 없음)가 v1.0.0이다. 기능이 늘면 가운데 자리(1.2.0), 고치기만 했으면 끝자리(1.1.1)를 올린다.
///
/// 화면 오른쪽 아래 구석에 작게 「v1.1.0」을 그린다 — 친구 피드백이 어느 빌드 것인지 사진만 보고 알 수 있게.
/// 첫 화면(멀티 NetBoot)부터 게임 끝까지 보이도록 씬과 무관하게 스스로 붙는다.
/// </summary>
public class GameVersion : MonoBehaviour
{
    public const string Number = "1.1.0";
    public static string Label => "v" + Number;

    static readonly GUIStyle Style = new GUIStyle
    {
        fontSize = 12,
        alignment = TextAnchor.LowerRight,
        normal = { textColor = new Color(1f, 1f, 1f, 0.55f) },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<GameVersion>() != null) return;
        GameObject host = new GameObject("GameVersion");
        DontDestroyOnLoad(host);
        host.AddComponent<GameVersion>();
    }

    void OnGUI()
    {
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
        Style.fontSize = Mathf.RoundToInt(12f * scale);
        GUI.Label(new Rect(0f, 0f, Screen.width - 6f * scale, Screen.height - 3f * scale), Label, Style);
    }
}
