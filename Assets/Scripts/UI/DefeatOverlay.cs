using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패배했을 때 화면 한가운데에 알린다.
///
/// 🔴 왜 만들었나 — **졌는데 진 줄 모른다.**
/// 2026-09-24 긴 판(구현담당2, outbox 2107)에서 라운드 4에 졌는데, 화면에 나온 건
/// 오른쪽 위 팀 칸의 작은 「플레이어 1 | 사망」 한 줄뿐이었다. 골드 몰수·유닛 소멸·
/// 레인 정리는 설계대로 도는데(RoundManager.HandlePlayerDefeated) **사장님은 자기가 졌다는 걸
/// 알 방법이 없었다.** 게임오버 표시는 F1 디버그 창에만 있었다(DebugHud.cs:173).
/// 고장이 아니라 **아직 안 만든 것**이라, 여기서 만든다.
///
/// 무엇을 적는가: 「졌다」만으로는 부족하다. 사장님이 다음 판에 다르게 두려면
/// **왜 졌는지(레인 적 70)와 무엇을 잃었는지(골드·유닛)와 무엇이 남았는지(위습)**를 알아야 한다.
/// 셋 다 RoundManager가 실제로 하는 일과 같은 말로 적는다 — 화면과 코드가 다른 말을 하면
/// 그게 다음 사람을 속인다.
///
/// GameHud와 분리된 자기완결 컴포넌트다(DifficultySelectHud와 같은 결) — 맵 생성이 씬에
/// 하나 넣어 두면 그 뒤로는 스스로 뜨고 스스로 숨는다.
/// </summary>
public class DefeatOverlay : MonoBehaviour
{
    // GameHud(0)·난이도 선택(100)보다 위. 패배는 다른 무엇보다 먼저 보여야 한다.
    const int SortingOrder = 200;

    GameObject panel;
    TMP_Text titleText;
    TMP_Text detailText;
    bool shown;

    void Awake()
    {
        BuildUI();
    }

    void Update()
    {
        PlayerContext local = PlayerContext.Local;
        bool dead = local != null && local.IsDead;

        // 이미 띄웠으면 다시 만들지 않는다 — 패배는 되돌아오지 않는다(데스카운트는 누적식이라
        // 70 아래로 내려가도 회복하지 않는다. RoundManager 주석 참고).
        if (!dead || shown) return;

        shown = true;
        Fill();
        panel.SetActive(true);
    }

    void Fill()
    {
        // RoundManager는 싱글턴이 아니라 씬 오브젝트다. 패배는 한 판에 한 번이라
        // 그때 한 번만 찾으면 된다(매 프레임 찾지 않는다).
        RoundManager rounds = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        int round = rounds != null ? rounds.CurrentRound : 0;
        bool allDead = rounds != null && rounds.IsGameOver;

        titleText.text = allDead ? "게임 오버" : "패배";

        // ⚠️ 여기 적는 숫자·문장은 RoundManager가 **실제로 하는 일**과 같아야 한다.
        //    「유닛을 잃었습니다」라고 써 놓고 안 잃으면 그 화면이 거짓말을 한다.
        detailText.text =
            $"라운드 {round}\n\n" +
            "레인에 적이 너무 많아 데스카운트가 0이 됐습니다.\n" +
            "골드를 잃고, 레인의 내 유닛과 적이 사라졌습니다.\n" +
            "<color=#9FD5FF>위습은 그대로 남아 있습니다.</color>\n\n" +
            (allDead
                ? "<size=80%>플레이 모드를 껐다 켜면 새 판이 시작됩니다.</size>"
                : "<size=80%>다른 플레이어가 남아 있어 게임은 계속됩니다.</size>");
    }

    void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 버튼이 없으니 GraphicRaycaster를 안 붙인다 — 붙이면 화면을 덮은 검은 판이
        // 그 아래 게임 클릭을 전부 먹는다. 패배 뒤에도 화면은 둘러볼 수 있어야 한다.

        // 화면 전체를 어둡게. 게임 화면이 비쳐 보이되 「끝났다」가 읽히는 정도로만 덮는다.
        RectTransform dim = CreatePanel(transform, "DefeatDim", new Color(0f, 0f, 0f, 0.55f));
        SetAnchors(dim, Vector2.zero, Vector2.one);
        panel = dim.gameObject;

        RectTransform box = CreatePanel(dim, "DefeatBox", new Color(0.10f, 0.03f, 0.05f, 0.92f));
        SetAnchors(box, new Vector2(0.30f, 0.33f), new Vector2(0.70f, 0.67f));

        titleText = CreateLabel(box, "DefeatTitle", "패배");
        SetAnchors((RectTransform)titleText.transform, new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.95f));
        titleText.fontSize = 64f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.95f, 0.35f, 0.35f);

        detailText = CreateLabel(box, "DefeatDetail", "");
        SetAnchors((RectTransform)detailText.transform, new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.64f));
        detailText.fontSize = 22f;
        detailText.color = new Color(0.92f, 0.92f, 0.92f);

        panel.SetActive(false);
    }

    static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;   // 위 GraphicRaycaster 주석과 같은 이유
        return obj.GetComponent<RectTransform>();
    }

    static TMP_Text CreateLabel(Transform parent, string name, string content)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
        text.text = content;
        // 레거시 Text가 아니라 TMP를 쓰는 이유는 화질이다(사장님 지적 2026-09-23
        // "하단에 글씨 화질이 안 좋은데") — 레거시는 크기별 비트맵을 구워 확대하면 번진다.
        if (GameHud.UiFontAsset != null) text.font = GameHud.UiFontAsset;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
