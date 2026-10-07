using UnityEngine;

/// <summary>
/// 배포 버전 — 사장님 2026-09-26 「배포 할 때마다 1.1.0v 이런 식으로」. **여기 한 곳만 고친다.**
/// BuildBeta가 빌드 직전에 PlayerSettings.bundleVersion(= Application.version)에 넣고, 압축 파일 이름에도 쓴다.
/// 🔴 2026-09-30 사장님 「베타 버전이니 맨 앞은 1이 아니라 0」 — 이미 나간 1.0.0·1.1.x·1.2.1은 0.0.0·0.1.x·0.2.1로 읽는다(정식 출시가 1.0.0).
/// 큰 덩어리가 **완성**되면 가운데 자리(0.3.0 = 스킬 전체 살리기), 고치기만 했으면 끝자리(0.2.2)를 올린다. 나가는 빌드는 매번 새 번호.
///
/// 화면 오른쪽 아래 구석에 작게 「v1.1.0」을 그린다 — 친구 피드백이 어느 빌드 것인지 사진만 보고 알 수 있게.
/// 첫 화면(멀티 NetBoot)부터 게임 끝까지 보이도록 씬과 무관하게 스스로 붙는다.
/// </summary>
public class GameVersion : MonoBehaviour
{
    public const string Number = "0.3.13";
    // 베타 표시(사장님 10-07 「누구 매칭인지 보이게」) — 유닛 획득 알림 끝 「(원작: 징베)」·정보창 이름 줄 끝 「원작 징베」. 정식 출시 땐 false로 끈다(데이터 UnitData.originalMatchName은 그대로 둔다).
    public const bool BetaShowOriginalMatch = true;
    // 사장님 표기 그대로 「1.1.0v」(09-26 두 번 — 「1.1.0v 이런식으로」). 앱·압축 파일 이름에도 이 글자를 쓴다(BuildBeta).
    public static string Label => Number + "v";

    static readonly GUIStyle Style = new GUIStyle
    {
        fontSize = 14,
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
        // 1280×720 창에서 12 × 0.67 = 8px라 겨우 읽혔다(09-26 구현담당2) → 14px 밑으로는 안 줄인다.
        float scale = Mathf.Max(1f, Screen.height / 1080f);
        Style.fontSize = Mathf.RoundToInt(14f * scale);
        // 게임 화면에선 하단 바 윗선 바로 위 오른쪽 구석(09-29 — 바 안 구석은 명령 카드 금테와 겹쳤다). 첫 화면처럼 바가 없으면 화면 구석.
        // 10-06 PM 「0.3.7v가 판매 칸 모서리에 겹친다」 — 바 높이가 0.22보다 커져서(명령 카드 4×3) 고정 비율은 어긋났다. 바 RectTransform의 실제 윗선을 읽는다.
        float bottom = HasBottomBar() ? BarTopFromTop() : Screen.height;
        GUI.Label(new Rect(0f, 0f, Screen.width - 8f * scale, bottom - 4f * scale), Label, Style);
    }

    // GameHud 하단 바 높이(화면 비율) — GameChatBox·PlayerNotification과 같은 값.
    const float BottomBarFraction = 0.22f;
    GameObject bottomBar;
    float nextBarLookup;

    // 하단 바 윗선의 화면 위쪽 기준 y(OnGUI 좌표). 오버레이 캔버스라 월드 모서리가 곧 화면 픽셀이다. 못 읽으면 옛 비율.
    float BarTopFromTop()
    {
        if (bottomBar != null && bottomBar.transform is RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);   // 0 좌하 · 1 좌상 · 2 우상 · 3 우하
            float top = Screen.height - corners[1].y;
            if (top > 0f && top < Screen.height) return top;
        }
        return Screen.height * (1f - BottomBarFraction);
    }

    bool HasBottomBar()
    {
        if (bottomBar == null && Time.unscaledTime >= nextBarLookup)
        {
            nextBarLookup = Time.unscaledTime + 1f;   // 씬이 바뀌면 다시 찾는다(초당 한 번만)
            bottomBar = GameObject.Find("BottomBar");
        }
        return bottomBar != null && bottomBar.activeInHierarchy;
    }
}
