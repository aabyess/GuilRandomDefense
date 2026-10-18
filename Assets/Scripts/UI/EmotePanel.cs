using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이모티콘 6종(원랜디 S2 2.323, 사장님 10-18) — 사양 ~/grd/GRD_s2_ideas/spec_ui.md 1절(j:9176~9358).
///   · 왼쪽 위 단추 → 고르기 창(3×2 칸 + 「끄기」 목록) → 고르면 전원 화면의 「보낸 사람 자리」 모서리에 5초(튀어나옴·통통·쪼그라듦 곡선) + 소리.
///   · 이름은 안 뜬다(원작도 숨김) — 자리(0·2 왼쪽, 1·3 오른쪽 / 0·1 위 30%, 2·3 위 60%)로 누군지 안다. 쿨다운 없음. 같은 사람이 또 보내면 처음부터.
///   · 끄기는 보는 PC마다 따로(저장 안 함, 판마다 다시 켜짐) — 끈 사람 것은 그림·소리 둘 다 안 나온다.
/// 그림·소리: Resources/Emoji/emote1~6(png), emote(mp3). 없으면 글자 이모티콘으로 대신한다(원작 그림 사용은 사장님 확인 대기).
/// 멀티: 클라 NetPlayer.RPC_Emote → 호스트 NetGameState.RPC_Emote(전원). 싱글은 바로.
/// </summary>
public class EmotePanel : MonoBehaviour
{
    public const int Count = 6;
    const float Seconds = 5f;
    static readonly string[] FallbackText = { "파이팅!", "GG", "운 존나 없네", "ㅋㅋ", "?", "화났다!" };
    static readonly string[] FallbackShort = { "파이팅!", "GG", "운 없네", "ㅋㅋ", "?", "화남" };

    static EmotePanel instance;
    Canvas canvas;
    RectTransform toggleButton, picker;
    readonly RectTransform[] shown = new RectTransform[4];
    readonly float[] shownAt = new float[4];
    readonly bool[] muted = new bool[4];
    readonly TMP_Text[] muteLabels = new TMP_Text[4];
    readonly Image[] muteBg = new Image[4];
    readonly Sprite[] sprites = new Sprite[Count];
    AudioClip clip;
    AudioSource audioSource;
    bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("EmotePanel");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<EmotePanel>();
    }

    void Awake()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        canvasGo.SetActive(false);
    }

    void LoadAssets()
    {
        if (loaded) return;
        loaded = true;
        for (int i = 0; i < Count; i++)
        {
            Texture2D t = Resources.Load<Texture2D>($"Emoji/emote{i + 1}");
            if (t != null) sprites[i] = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
        }
        clip = Resources.Load<AudioClip>("Emoji/emote");
        Build();
    }

    static TMP_Text Label(Transform parent, string text, float size, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text t = go.GetComponent<TextMeshProUGUI>();
        if (GameHud.UiFontAsset != null) t.font = GameHud.UiFontAsset;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.enableWordWrapping = false;
        var r = t.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        return t;
    }

    static RectTransform Box(Transform parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    // 그림 칸(그림이 없으면 글자)
    void Face(RectTransform parent, int k, float textSize)
    {
        if (sprites[k] != null)
        {
            var img = parent.gameObject.GetComponent<Image>();
            img.sprite = sprites[k]; img.color = Color.white; img.preserveAspect = true;
        }
        else
        {
            parent.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 0.85f);
            TMP_Text t = Label(parent, FallbackShort[k], textSize, new Color(1f, 0.86f, 0.2f));
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = true; t.fontSizeMin = 10f; t.fontSizeMax = textSize;
        }
    }

    void Build()
    {
        Transform root = canvas.transform;
        // 왼쪽 위 단추(원작 (110,120) 60×60, 그림 = emote1)
        toggleButton = Box(root, "EmoteButton", Color.white, new Vector2(0f, 1f), new Vector2(80f, -96f), new Vector2(60f, 60f));
        Face(toggleButton, 0, 16f);
        toggleButton.gameObject.AddComponent<Button>().onClick.AddListener(TogglePicker);
        toggleButton.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.82f, 0.25f, 0.9f);

        // 고르기 창(원작 520×212, 가운데에서 78 아래)
        picker = Box(root, "Picker", new Color(0f, 0f, 0f, 0.78f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(520f, 212f));
        TMP_Text title = Label(picker, "Choose Emote", 18, Color.white);
        var tr = title.rectTransform; tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(0.6f, 1f); tr.offsetMin = new Vector2(10f, -32f); tr.offsetMax = new Vector2(0f, -4f);
        for (int k = 0; k < Count; k++)
        {
            int col = k % 3, row = k / 3;
            RectTransform cell = Box(picker, "Cell" + k, new Color(1f, 1f, 1f, 0.08f), new Vector2(0f, 1f), new Vector2(14f + col * 104f, -38f - row * 84f), new Vector2(96f, 78f));
            RectTransform face = Box(cell, "Face", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 66f));
            Face(face, k, 22f);
            int pick = k;
            cell.gameObject.AddComponent<Button>().onClick.AddListener(() => Send(pick));
        }
        // 끄기 목록(원작 「Mute Emotes」 176×160, 자리 (330,38)) — 보는 PC마다 따로
        RectTransform mute = Box(picker, "Mute", new Color(1f, 1f, 1f, 0.06f), new Vector2(0f, 1f), new Vector2(330f, -38f), new Vector2(176f, 160f));
        TMP_Text mt = Label(mute, "Mute Emotes", 16, Color.white);
        var mr = mt.rectTransform; mr.anchorMin = new Vector2(0f, 1f); mr.anchorMax = Vector2.one; mr.offsetMin = new Vector2(0f, -26f); mr.offsetMax = Vector2.zero;
        for (int p = 0; p < 4; p++)
        {
            int slot = p;
            RectTransform row = Box(mute, "Row" + p, new Color(0f, 0f, 0f, 0.6f), new Vector2(0f, 1f), new Vector2(4f, -28f - p * 32f), new Vector2(168f, 28f));
            row.gameObject.AddComponent<Button>().onClick.AddListener(() => { muted[slot] = !muted[slot]; if (muted[slot] && shown[slot] != null) shown[slot].gameObject.SetActive(false); RefreshMute(); });
            muteBg[p] = row.GetComponent<Image>();
            muteLabels[p] = Label(row, "", 15, Color.white);
        }
        picker.gameObject.SetActive(false);
        RefreshMute();
    }

    void RefreshMute()
    {
        for (int p = 0; p < 4; p++)
        {
            if (muteLabels[p] == null) continue;
            PlayerContext c = PlayerContext.Get(p);
            bool here = c != null && c.IsOccupied;
            string who = here ? PlayerDisplayName.RawNickname(p) : $"플레이어 {p + 1}";
            muteLabels[p].text = $"{who}  {(muted[p] ? "<color=#FF6060>OFF</color>" : "<color=#80FF80>ON</color>")}";
            muteBg[p].color = muted[p] ? new Color(0.35f, 0.35f, 0.35f, 0.8f) : new Color(0f, 0f, 0f, 0.6f);
        }
    }

    void TogglePicker()
    {
        bool open = !picker.gameObject.activeSelf;
        picker.gameObject.SetActive(open);
        if (open) { picker.SetAsLastSibling(); RefreshMute(); }
    }

    /// <summary>내가 고른 이모티콘을 보낸다(쿨다운 없음).</summary>
    public static void Send(int k)
    {
        if (instance == null || k < 0 || k >= Count) return;
        if (instance.picker != null) instance.picker.gameObject.SetActive(false);
        int me = LocalPlayer.LocalPlayerId;
        if (GamePause.IsNetworked)
        {
            if (GameAuthority.IsServer) NetGameState.Instance?.RPC_Emote((byte)me, (byte)k);
            else NetPlayer.Local?.RPC_Emote((byte)k);
            return;
        }
        Show(me, k);
    }

    /// <summary>(slot, k)를 그 사람 자리에 띄운다 — 각자(받은 쪽 전원).</summary>
    public static void Show(int slot, int k)
    {
        if (instance == null || slot < 0 || slot > 3 || k < 0 || k >= Count) return;
        instance.LoadAssets();
        if (instance.muted[slot]) return;
        if (instance.shown[slot] != null) Destroy(instance.shown[slot].gameObject);   // 같은 사람이 또 보내면 처음부터
        float x = (slot % 2 == 0) ? 116f : 1920f - 116f;
        float y = (slot < 2) ? -324f : -648f;
        RectTransform r = Box(instance.canvas.transform, "Emote" + slot, Color.white, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(176f, 132f));
        r.pivot = new Vector2(0.5f, 0.5f);
        r.GetComponent<Image>().raycastTarget = false;
        instance.Face(r, k, 40f);
        if (instance.sprites[k] == null) { TMP_Text t = r.GetComponentInChildren<TMP_Text>(); if (t != null) t.text = FallbackText[k]; }
        r.gameObject.AddComponent<CanvasGroup>();
        instance.shown[slot] = r;
        instance.shownAt[slot] = Time.unscaledTime;
        if (instance.clip != null) { instance.audioSource.Stop(); instance.audioSource.PlayOneShot(instance.clip, GameSound.DefaultVolume); }
    }

    // 원작 크기 곡선(Ben j:9058~9087)·투명도 곡선(Beo j:9088~9107)
    static float ScaleAt(float t)
    {
        if (t < 0.183f) return Mathf.Lerp(0f, 1.195f, t / 0.183f);
        if (t < 0.334f) return Mathf.Lerp(1.195f, 1f, (t - 0.183f) / 0.151f);
        if (t < 0.667f) return 1f;
        if (t < 0.832f) return Mathf.Lerp(1f, 1.0625f, (t - 0.667f) / 0.165f);
        if (t < 1f) return Mathf.Lerp(1.0625f, 1f, (t - 0.832f) / 0.168f);
        if (t < 4.333f) return 1f;
        if (t < 4.667f) return Mathf.Lerp(1f, 1.195f, (t - 4.333f) / 0.334f);
        if (t < 4.853f) return 1.195f;
        return Mathf.Lerp(1.195f, 0f, (t - 4.853f) / 0.147f);
    }

    static float AlphaAt(float t)
    {
        if (t < 0.334f) return t / 0.334f;
        if (t < 4.333f) return 1f;
        if (t < 4.667f) return Mathf.Lerp(1f, 0.481f, (t - 4.333f) / 0.334f);
        if (t < 4.853f) return 0.481f;
        return Mathf.Lerp(0.481f, 0f, (t - 4.853f) / 0.147f);
    }

    void Update()
    {
        // 판(GameHud)이 있을 때만 보인다 — 첫 화면·대기실엔 없다
        bool inGame = GameHud.UiFontAsset != null && FindFirstObjectByType<GameHud>() != null;
        if (canvas.gameObject.activeSelf != inGame)
        {
            canvas.gameObject.SetActive(inGame);
            if (inGame) { LoadAssets(); System.Array.Clear(muted, 0, muted.Length); RefreshMute(); }   // 판마다 다시 켜짐(원작)
        }
        for (int s = 0; s < 4; s++)
        {
            if (shown[s] == null) continue;
            float t = Time.unscaledTime - shownAt[s];
            if (t >= Seconds) { Destroy(shown[s].gameObject); shown[s] = null; continue; }
            shown[s].localScale = Vector3.one * ScaleAt(t);
            shown[s].GetComponent<CanvasGroup>().alpha = AlphaAt(t);
        }
    }
}
