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

    static readonly GUIStyle Style = new GUIStyle
    {
        fontSize = 20,
        alignment = TextAnchor.MiddleCenter,
        normal = { textColor = Color.white },
    };

    const float BoxWidth = 520f;
    const float BoxHeight = 30f;
    const float Spacing = 4f;

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

        // 화면 위쪽 1/4 지점부터 아래로 쌓는다 — GameHud 하단 바, GameChatBox 상태줄
        // (하단 바 바로 위)과 안 겹치는 자리다.
        float y = Screen.height * 0.25f;
        for (int i = 0; i < list.Count; i++)
        {
            Rect rect = new Rect(Screen.width * 0.5f - BoxWidth * 0.5f, y, BoxWidth, BoxHeight);
            GUI.Label(rect, list[i].message, Style);
            y += BoxHeight + Spacing;
        }
    }
}
