using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 처치 골드 「+N」 떠오르는 글자 — 원작 Trig_EnemyDeath2(j:3374-3375): 골드를 준 뒤 죽은 적 머리 위에
// CreateTextTagUnitBJ(「|cffffd700 +N」) → 영구 아님 · 수명 1.50초 · 페이드 시작 0.45초 전 · 위로 45 · **그 골드를 받은 플레이어에게만** 보임
// (SetTextTagVisibility(GetLocalPlayer()==Gold_PlayerInt)). 우리 GrantKillGold는 지갑에 Add만 해서 골드가 느는 걸 볼 길이 없었다(GAP 09-27 「처치 골드 +N 글자」).
//
// 멀티: 호스트가 Show를 부르면 받은 사람이 호스트 자신이면 바로 그리고, 원격이면 Shown 이벤트로 NetGameState가 그 클라에 RPC로 넘긴다
// (PlayerNotification.Shown과 같은 길). 클라는 NetPlayer.RPC_KillGold → ShowLocal.
public static class KillGoldPopup
{
    /// <summary>MP: 원격 슬롯이 받은 골드 글자를 그 클라에 넘기려고 호스트가 구독한다. 싱글에선 구독자가 없다.</summary>
    public static event System.Action<int, Vector3, int, bool> Shown;

    /// <param name="wood">true면 청록 「+N」(원작 스토리 막타 목재 글자 — Trig_Story2, 수명 1.00·페이드 0.42·위로 60), 아니면 금색 처치 골드 글자.</param>
    public static void Show(int playerId, Vector3 worldPos, int amount, bool wood = false)
    {
        if (amount <= 0) return;
        if (playerId == LocalPlayer.LocalPlayerId) KillGoldPopupLayer.EnsureInstance().Spawn(worldPos, amount, wood);
        Shown?.Invoke(playerId, worldPos, amount, wood);
    }

    /// <summary>MP 클라: 호스트가 넘겨 준 것(이미 내 몫)을 그린다.</summary>
    public static void ShowLocal(Vector3 worldPos, int amount, bool wood = false)
    {
        if (amount > 0) KillGoldPopupLayer.EnsureInstance().Spawn(worldPos, amount, wood);
    }
}

public class KillGoldPopupLayer : MonoBehaviour
{
    // 원작 값(수명·페이드)은 그대로, 올라가는 속도 45(워크3 글자 속도 단위)는 화면 픽셀로 옮긴 어림이다 — 1.5초 동안 약 70px 오른다.
    const float Lifespan = 1.5f;
    const float FadeSeconds = 0.45f;
    const float RisePixelsPerSecond = 46f;
    // 청록 막타 글자(j Trig_Story2 CreateTextTagUnitBJ 「|cff20b2aa +N」, 높이 12): 수명 1.00 · 페이드 0.42 · 속도 60(골드 45의 4/3 → 약 61px/s).
    const float WoodLifespan = 1.0f;
    const float WoodFadeSeconds = 0.42f;
    const float WoodRisePixelsPerSecond = 61f;
    const float WoodHeadMargin = 9f;  // 골드 글자 높이 8 : 6 = 12 : 9
    const float HeadMargin = 6f;      // 적 위치(발치)에서 머리 위로 띄우는 월드 높이
    const int FontSize = 26;
    const int SortingOrder = -94;     // 이름표(-95) 바로 위, HUD 아래
    const int MaxLive = 40;           // 한 프레임에 적이 몰려 죽어도 화면이 글자로 안 덮이게

    static KillGoldPopupLayer instance;

    struct Pop
    {
        public RectTransform root;
        public TMP_Text text;
        public Vector3 world;
        public float born;
        public bool wood;
    }

    readonly List<Pop> live = new List<Pop>();
    readonly Stack<Pop> pool = new Stack<Pop>();
    RectTransform canvasRect;
    Camera cam;

    public static KillGoldPopupLayer EnsureInstance()
    {
        if (instance != null) return instance;
        GameObject host = new GameObject("KillGoldPopupLayer");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<KillGoldPopupLayer>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        GameObject canvasObject = new GameObject("KillGoldCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        canvasRect = (RectTransform)canvasObject.transform;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Spawn(Vector3 worldPos, int amount, bool wood = false)
    {
        if (live.Count >= MaxLive) Recycle(0);   // 가장 오래된 것부터 걷는다
        Pop pop = pool.Count > 0 ? pool.Pop() : Create();
        pop.world = worldPos + Vector3.up * (wood ? WoodHeadMargin : HeadMargin);
        pop.born = Time.unscaledTime;
        pop.wood = wood;
        pop.root.gameObject.SetActive(true);
        pop.text.text = wood ? $"<color=#20B2AA>+{amount}</color>" : $"<color=#FFD700>+{amount}</color>";
        pop.text.alpha = 1f;
        live.Add(pop);
        Place(live.Count - 1);
    }

    Pop Create()
    {
        GameObject root = new GameObject("KillGold", typeof(RectTransform));
        root.transform.SetParent(canvasRect, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;   // 화면 좌하단 기준 픽셀 좌표로 둔다
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(160f, 40f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        if (GameHud.UiFontAsset != null) text.font = GameHud.UiFontAsset;
        text.fontSize = FontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Bottom;
        text.raycastTarget = false;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        // 풀밭·모래 위에서도 읽히도록 검정 외곽선(UnitNameplateLayer와 같은 이유).
        text.outlineWidth = 0.28f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        return new Pop { root = rect, text = text };
    }

    void Recycle(int index)
    {
        Pop pop = live[index];
        pop.root.gameObject.SetActive(false);
        live.RemoveAt(index);
        pool.Push(pop);
    }

    void Place(int index)
    {
        if (cam == null) cam = Camera.main;
        Pop pop = live[index];
        float age = Time.unscaledTime - pop.born;
        if (cam == null) { pop.root.gameObject.SetActive(false); return; }
        Vector3 screen = cam.WorldToScreenPoint(pop.world);
        bool visible = screen.z > 0f;
        if (pop.root.gameObject.activeSelf != visible) pop.root.gameObject.SetActive(visible);
        pop.root.position = new Vector3(screen.x, screen.y + age * (pop.wood ? WoodRisePixelsPerSecond : RisePixelsPerSecond), 0f);
        float life = pop.wood ? WoodLifespan : Lifespan;
        float fade = Mathf.Clamp01((life - age) / (pop.wood ? WoodFadeSeconds : FadeSeconds));   // 마지막 구간 동안 0까지
        if (!Mathf.Approximately(pop.text.alpha, fade)) pop.text.alpha = fade;
    }

    void LateUpdate()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime - live[i].born >= (live[i].wood ? WoodLifespan : Lifespan)) { Recycle(i); continue; }
            Place(i);
        }
    }
}
