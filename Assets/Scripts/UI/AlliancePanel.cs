using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 동맹 창(사장님 10-08 「동맹 (F11)」): 이 판의 다른 플레이어를 한 줄씩 — 닉네임 · [유닛 공유] 체크박스. 워크3식: 내가 B에 체크하면 <b>B가 내 유닛을 조종</b>한다(소유는 그대로 — AllianceShare).
/// 혼자 하기엔 「같이 하는 사람이 없습니다」. 상단 「동맹 (F11)」 단추와 F11 키가 연다(GameHud). 체크를 바꾸면 호스트에 요청(NetCommands.RequestSetShare) → NetGameState.ShareMasks로 돌아와 반영.
/// </summary>
public class AlliancePanel : MonoBehaviour
{
    static AlliancePanel instance;
    GameObject window;
    RectTransform listRoot;
    TMP_Text title, emptyText;
    readonly List<GameObject> rows = new List<GameObject>();
    readonly Dictionary<int, (bool value, float until)> pending = new Dictionary<int, (bool, float)>();   // 방금 눌렀는데 아직 복제가 안 돌아온 값(깜빡임 방지)
    float nextRefresh;

    public static bool IsOpen => instance != null && instance.window != null && instance.window.activeSelf;

    public static void Install(Transform hudRoot)
    {
        if (instance != null) return;
        GameObject go = new GameObject("AlliancePanel", typeof(RectTransform));
        go.transform.SetParent(hudRoot, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        instance = go.AddComponent<AlliancePanel>();
        instance.Build();
    }

    public static void Toggle()
    {
        if (instance == null) return;
        bool open = !instance.window.activeSelf;
        instance.window.SetActive(open);
        if (open) { instance.window.transform.SetAsLastSibling(); instance.Refresh(); }
    }

    static TMP_Text Label(Transform parent, string text, int size, TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text t = go.GetComponent<TextMeshProUGUI>();
        if (GameHud.UiFontAsset != null) t.font = GameHud.UiFontAsset;
        t.text = text; t.fontSize = size; t.alignment = align; t.color = color; t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform r, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    { r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax; }

    void Build()
    {
        window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(transform, false);
        RectTransform wr = (RectTransform)window.transform;
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 0.5f);
        wr.sizeDelta = new Vector2(520f, 120f);
        window.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.96f);
        Outline ol = window.AddComponent<Outline>(); ol.effectColor = new Color(0.85f, 0.68f, 0.25f, 1f); ol.effectDistance = new Vector2(2f, -2f);

        title = Label(window.transform, "동맹", 30, TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.25f));
        Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -52f), new Vector2(-10f, -8f));
        title.fontStyle = FontStyles.Bold;

        GameObject list = new GameObject("List", typeof(RectTransform));
        list.transform.SetParent(window.transform, false);
        listRoot = (RectTransform)list.transform;
        Stretch(listRoot, Vector2.zero, Vector2.one, new Vector2(16f, 58f), new Vector2(-16f, -58f));

        emptyText = Label(window.transform, "같이 하는 사람이 없습니다.", 24, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.85f));
        Stretch(emptyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 50f), new Vector2(-16f, -58f));

        GameObject close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(window.transform, false);
        RectTransform cr = (RectTransform)close.transform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0f);
        cr.sizeDelta = new Vector2(160f, 40f); cr.anchoredPosition = new Vector2(0f, 10f);
        close.GetComponent<Image>().color = new Color(0.26f, 0.32f, 0.44f, 1f);
        close.GetComponent<Button>().onClick.AddListener(() => window.SetActive(false));
        TMP_Text cl = Label(close.transform, "닫기 (F11)", 22, TextAlignmentOptions.Center, Color.white);
        Stretch(cl.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        window.SetActive(false);
    }

    void Update()
    {
        if (window == null || !window.activeSelf) return;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.25f;
        Refresh();
    }

    void Refresh()
    {
        foreach (GameObject r in rows) if (r != null) Destroy(r);
        rows.Clear();
        var others = new List<NetPlayer>();
        if (NetGameState.Instance != null)
            foreach (NetPlayer np in NetPlayer.All)
                if (np != null && np.Slot >= 0 && np.Slot != LocalPlayer.LocalPlayerId && PlayerContext.Get(np.Slot) != null) others.Add(np);
        others.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        emptyText.gameObject.SetActive(others.Count == 0);
        float height = 130f + Mathf.Max(0, others.Count) * 56f + (others.Count == 0 ? 40f : 0f);
        ((RectTransform)window.transform).sizeDelta = new Vector2(520f, height);
        for (int i = 0; i < others.Count; i++) rows.Add(BuildRow(others[i], i));
    }

    GameObject BuildRow(NetPlayer np, int index)
    {
        int slot = np.Slot;
        GameObject row = new GameObject($"Row{slot}", typeof(RectTransform));
        row.transform.SetParent(listRoot, false);
        RectTransform rr = (RectTransform)row.transform;
        rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(0f, 50f); rr.anchoredPosition = new Vector2(0f, -index * 56f);

        bool theyShareToMe = AllianceShare.IsShared(slot, LocalPlayer.LocalPlayerId);
        TMP_Text name = Label(row.transform, theyShareToMe ? $"{np.DisplayName}  <size=18><color=#9FD8A0>(내가 조종 가능)</color></size>" : np.DisplayName, 26, TextAlignmentOptions.Left, Color.white);
        Stretch(name.rectTransform, Vector2.zero, new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero);

        bool shown = AllianceShare.IsSharingTo(slot);
        if (pending.TryGetValue(slot, out var p))
        {
            if (shown == p.value || Time.unscaledTime > p.until) pending.Remove(slot); else shown = p.value;
        }

        GameObject box = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
        box.transform.SetParent(row.transform, false);
        RectTransform br = (RectTransform)box.transform;
        br.anchorMin = br.anchorMax = new Vector2(0.62f, 0.5f); br.pivot = new Vector2(0f, 0.5f); br.sizeDelta = new Vector2(34f, 34f);
        Image bg = box.GetComponent<Image>(); bg.color = new Color(0.2f, 0.22f, 0.3f, 1f);
        GameObject check = new GameObject("Check", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        Stretch((RectTransform)check.transform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        check.GetComponent<Image>().color = new Color(1f, 0.82f, 0.25f, 1f);
        Toggle toggle = box.GetComponent<Toggle>();
        toggle.targetGraphic = bg; toggle.graphic = check.GetComponent<Image>();
        toggle.SetIsOnWithoutNotify(shown);
        toggle.onValueChanged.AddListener(on =>
        {
            pending[slot] = (on, Time.unscaledTime + 1.5f);
            NetCommands.RequestSetShare(slot, on);
        });
        TMP_Text lab = Label(row.transform, "유닛 공유", 22, TextAlignmentOptions.Left, new Color(0.9f, 0.9f, 0.95f));
        Stretch(lab.rectTransform, new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(42f, 0f), Vector2.zero);
        return row;
    }
}
