using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 첫 실행 제작진 화면(사장님 2026-10-06 「유니티 로고 다음에 제작자·스폰서·PD 정보 간단히」).
/// 유니티 시작 로고(PlayerSettings 스플래시) 다음, 첫 화면(NetBoot) 위에 검은 판으로 몇 초 띄웠다가 흐려지며 사라진다.
/// 실행당 한 번만(첫 화면으로 돌아와도 다시 안 뜬다). 클릭·아무 키로 바로 넘긴다. 에디터에서 게임 씬으로 바로 들어가면 안 뜬다.
/// 문구는 <see cref="Lines"/> 한 곳만 고친다.
/// </summary>
public class CreditsSplash : MonoBehaviour
{
    static readonly (string role, string name)[] Lines =
    {
        ("제작", "최상호"),
        ("스폰서", "노무현"),
        ("PD", "임장혁"),
    };
    // 배경음악(첫 화면 곡) 출처 — CC BY 4.0은 이름을 밝히는 게 조건이다.
    const string MusicCredit = "Music: \"Adventure\" by Alexander Nakarada (CreatorChords) · CC BY 4.0";

    const float FadeIn = 0.6f, Hold = 2.6f, FadeOut = 0.8f;
    const int TitleSceneIndex = 0;   // NetBoot — 배포판 첫 씬

    static bool shown;
    CanvasGroup group;
    float t;
    bool skipping;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (shown || SceneManager.GetActiveScene().buildIndex != TitleSceneIndex) return;
        shown = true;
        new GameObject("[CreditsSplash]").AddComponent<CreditsSplash>();
    }

    void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // 첫 화면 메뉴·로딩 위
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();   // 뒤의 메뉴 버튼이 눌리지 않게 막는다
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(transform, false);
        Stretch((RectTransform)bg.transform);
        bg.GetComponent<Image>().color = Color.black;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Bold SDF");
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(transform, false);
        Stretch((RectTransform)text.transform);
        if (font != null) text.font = font;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 46f;
        text.lineSpacing = 30f;
        text.color = new Color(0.93f, 0.86f, 0.66f);   // 첫 화면 금빛 글자와 같은 결
        var sb = new System.Text.StringBuilder("<size=72>구랜디</size>\n\n");
        foreach ((string role, string name) in Lines)
            sb.Append("<color=#A89878><size=34>").Append(role).Append("</size></color>   ").Append(name).Append('\n');
        sb.Append("\n<size=22><color=#7A7060>").Append(MusicCredit).Append("</color></size>");
        text.text = sb.ToString();
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (!skipping && t > 0.2f && ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)))
        {
            skipping = true;
            t = Mathf.Max(t, FadeIn + Hold);   // 바로 흐려지기로
        }
        if (t < FadeIn) group.alpha = t / FadeIn;
        else if (t < FadeIn + Hold) group.alpha = 1f;
        else if (t < FadeIn + Hold + FadeOut)
        {
            group.alpha = 1f - (t - FadeIn - Hold) / FadeOut;
            group.blocksRaycasts = false;
        }
        else Destroy(gameObject);
    }
}
