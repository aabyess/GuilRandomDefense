using System.Collections.Generic;
using UnityEngine;

// 화면 알림 — 원작 `DisplayTimedTextToForce`(플레이어 하나에게만, 몇 초간)를 옮긴 것.
// "눌렀는데 아무 일도 안 일어난다"로 보이는 실패(포인트 부족, 재고 없음, 도박 실패 등)를
// 화면에 띄운다. GameHud(uGUI)와 별도 파일·별도 렌더 경로(IMGUI OnGUI)로 뒀다 —
// DebugHud·GameChatBox와 같은 관례다. 구현담당3이 지금 GameHud.cs를 만지고 있어서
// 겹치지 않게 이 파일은 그쪽을 참조하지 않는다(PM 지시, 2026-09-05).
//
// ⚠️ 여기로 보낼지 Debug.Log에 남길지 가르는 기준: **플레이어가 화면에서 보고 행동을
// 바꿀 수 있는가**다. "포인트가 모자라다"·"재고가 없다"·"도박에 실패했다"는 플레이어가
// 다음에 뭘 할지(기다린다, 다른 걸 산다) 바뀐다 — 알림. "에셋을 못 찾았다"·"컴포넌트가
// 안 붙어 있다" 같은 배선 오류는 플레이어가 봐도 할 수 있는 게 없다 — Debug.Log(또는
// LogWarning)로 남긴다.
public static class PlayerNotification
{
    /// <summary>playerId에게만 보이는 알림을 띄운다. duration은 원작 관례대로 대개
    /// 3~6초, 중요한 건 10초.</summary>
    public static void Show(int playerId, string message, float duration = 4f)
    {
        PlayerNotificationHud.EnsureInstance().Enqueue(playerId, message, duration);
    }
}

public class PlayerNotificationHud : MonoBehaviour
{
    static PlayerNotificationHud instance;

    public static PlayerNotificationHud EnsureInstance()
    {
        if (instance != null) return instance;

        instance = Object.FindFirstObjectByType<PlayerNotificationHud>();
        if (instance == null)
        {
            GameObject host = new GameObject("PlayerNotificationHud");
            Object.DontDestroyOnLoad(host);
            instance = host.AddComponent<PlayerNotificationHud>();
        }
        return instance;
    }

    struct Entry
    {
        public string message;
        public float expiresAt;
    }

    // ⚠️ 플레이어별로 큐를 따로 둔다 — 원작 `udg_Player_Group_Number[플레이어]`처럼
    // "그 사람에게만" 뜨는 구조를 그대로 옮긴 것이다. 지금은 로컬 1인이라 다른 플레이어
    // 큐가 화면에 안 뜨는 게 안 보이지만(전부 같은 콘솔), 나중에 멀티가 붙어도 이 구조를
    // 그대로 쓰면 된다 — OnGUI가 LocalPlayer.LocalPlayerId 큐만 그린다.
    readonly Dictionary<int, List<Entry>> queues = new Dictionary<int, List<Entry>>();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Enqueue(int playerId, string message, float duration)
    {
        if (!queues.TryGetValue(playerId, out List<Entry> list))
        {
            list = new List<Entry>();
            queues[playerId] = list;
        }
        list.Add(new Entry { message = message, expiresAt = Time.unscaledTime + Mathf.Max(0.1f, duration) });
    }

    // 2026-09-26 사장님 「획득으로 유닛이나 돈 가운데에 글이 뜨던데 미니맵 위에 텍스트 나오게 해줘」 — 원작 워크래프트 글자
    // 알림 자리(왼쪽 아래, 명령 콘솔 바로 위)로 옮겼다. 전엔 화면 가운데 위 1/4에서 아래로 쌓여 전장을 가렸다.
    // 왼쪽 정렬 · **새 줄이 맨 아래**, 옛 줄은 위로 밀린다(워크래프트와 같다). 바닥선은 하단 바 윗선인데, 미니맵 위
    // 위습 칸(GameHud.BuildWispSlots)이 떠 있으면 그 위로 올린다 — 칸은 위습이 있을 때만 켜지므로 매 프레임 본다.
    static readonly GUIStyle Style = new GUIStyle
    {
        fontSize = 20,
        alignment = TextAnchor.MiddleLeft,
        richText = true,   // 유닛 획득 알림이 등급색(<color>)을 쓴다(2026-09-26). 기존 알림엔 태그가 없어 그대로다.
        normal = { textColor = Color.white },
    };
    static readonly GUIStyle ShadowStyle = new GUIStyle(Style) { normal = { textColor = new Color(0f, 0f, 0f, 0.85f) } };
    static readonly System.Text.RegularExpressions.Regex ColorTag = new System.Text.RegularExpressions.Regex("</?color[^>]*>");

    const float BoxWidth = 760f;
    const float BoxHeight = 30f;
    const float Spacing = 2f;
    const float LeftMargin = 20f;
    const float BottomGap = 8f;
    // 하단 바를 못 찾을 때의 예비값 — GameHud 하단 바가 화면 아래 22%다(GameChatBox와 같은 값).
    const float FallbackBottomHudFraction = 0.22f;
    // 채팅 상태줄(GameChatBox — 하단 바 바로 위 28px)을 비워 둔다.
    const float ChatLineReserve = 32f;

    RectTransform bottomBar;
    RectTransform[] wispRows;   // 위습 칸 줄마다 첫 칸(WispSlot0·WispSlot9)

    void OnGUI()
    {
        if (!queues.TryGetValue(LocalPlayer.LocalPlayerId, out List<Entry> list) || list.Count == 0) return;

        // 만료된 항목은 Repaint 때만 걷어낸다 — OnGUI가 프레임당 Layout·Repaint 두 번
        // 불리는데, 두 번 다 지우면 그 자체는 무해하지만 굳이 두 배로 돌 이유가 없다.
        if (Event.current.type == EventType.Repaint)
        {
            float now = Time.unscaledTime;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].expiresAt <= now) list.RemoveAt(i);
        }

        if (list.Count == 0) return;

        // 글자 크기는 1080 기준 20 — IMGUI는 CanvasScaler를 안 타서 작은 창에서 글자가 상대적으로 커진다.
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
        Style.fontSize = ShadowStyle.fontSize = Mathf.RoundToInt(20f * scale);
        float lineHeight = BoxHeight * scale;

        float y = BaselineY() - lineHeight;
        for (int i = list.Count - 1; i >= 0 && y > 0f; i--)
        {
            Rect rect = new Rect(LeftMargin * scale, y, BoxWidth * scale, lineHeight);
            // 전장 위에 바로 쓰이므로 그림자를 깐다(색 태그를 뗀 검정 글자를 1px 어긋나게).
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), ColorTag.Replace(list[i].message, ""), ShadowStyle);
            GUI.Label(rect, list[i].message, Style);
            y -= lineHeight + Spacing;
        }
    }

    /// <summary>알림 맨 아래 줄의 바닥(GUI 좌표, 위가 0). 하단 바 윗선 − 채팅줄, 위습 칸이 떠 있으면 그 윗선.</summary>
    float BaselineY()
    {
        if (bottomBar == null)
        {
            GameObject found = GameObject.Find("BottomBar");
            bottomBar = found != null ? found.transform as RectTransform : null;
            if (bottomBar != null && bottomBar.parent != null)
                wispRows = new[] { bottomBar.parent.Find("WispSlot0") as RectTransform, bottomBar.parent.Find("WispSlot9") as RectTransform };
        }

        float baseline = Screen.height * (1f - FallbackBottomHudFraction);
        if (bottomBar != null) baseline = Mathf.Min(baseline, TopEdgeGuiY(bottomBar));
        baseline -= ChatLineReserve;
        if (wispRows != null)
            foreach (RectTransform row in wispRows)
                if (row != null && row.gameObject.activeInHierarchy) baseline = Mathf.Min(baseline, TopEdgeGuiY(row));
        return baseline - BottomGap;
    }

    // 오버레이 캔버스라 월드 모서리 = 화면 픽셀(아래가 0). GUI는 위가 0이라 뒤집는다.
    static readonly Vector3[] corners = new Vector3[4];
    static float TopEdgeGuiY(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        return Screen.height - corners[1].y;
    }
}
