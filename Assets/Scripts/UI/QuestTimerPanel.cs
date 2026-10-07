using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퇴치 의뢰 타이머 창(2026-10-07, QUEST_AUDIT A8) — 원작 CreateTimerDialogBJ(「해적단 퇴치 : 」 등)는 **구매자에게만** 보인다.
/// 호스트/솔로: PirateQuestManager가 가진 내 진행 중 의뢰의 남은 시간. MP 클라: 호스트가 NetPlayer.QuestTimerText에 실어 보낸 글.
/// 스스로 붙는다(BugNotepad와 같은 방식) — GameHud는 건드리지 않는다. 진행 중인 의뢰가 없으면 숨는다.
/// </summary>
public class QuestTimerPanel : MonoBehaviour
{
    static QuestTimerPanel instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("QuestTimerPanel");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<QuestTimerPanel>();
    }

    GameObject root;
    TMP_Text text;
    float nextCheck;
    string shown = "";

    void Awake()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var rootGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(canvasGo.transform, false);
        root = rootGo;
        var rt = rootGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -232f);   // 우상단 「현재레벨」·카운트 판 바로 아래
        rt.sizeDelta = new Vector2(330f, 90f);
        var bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.07f, 0.72f);
        bg.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(rootGo.transform, false);
        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(10f, 6f); tr.offsetMax = new Vector2(-10f, -6f);
        text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 22f; text.color = new Color(1f, 0.86f, 0.35f); text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        root.SetActive(false);
    }

    /// <summary>내 의뢰 타이머 글(없으면 빈 글) — 호스트/솔로는 매니저, MP 클라는 NetPlayer.</summary>
    static string Current()
    {
        if (GameAuthority.IsServer)
            return PirateQuestManager.Instance != null ? PirateQuestManager.Instance.TimerText(LocalPlayer.LocalPlayerId) : "";
        return NetPlayer.Local != null ? NetPlayer.Local.QuestTimerText.ToString() : "";
    }

    void Update()
    {
        if (text != null && GameHud.UiFontAsset != null && text.font != GameHud.UiFontAsset) text.font = GameHud.UiFontAsset;
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.25f;
        string now = FindFirstObjectByType<GameHud>() != null ? Current() : "";
        if (now == shown) return;
        shown = now;
        root.SetActive(!string.IsNullOrEmpty(now));
        if (!string.IsNullOrEmpty(now))
        {
            text.text = now;
            int lines = 1; foreach (char c in now) if (c == '\n') lines++;
            ((RectTransform)root.transform).sizeDelta = new Vector2(330f, 20f + lines * 30f);
        }
    }
}
