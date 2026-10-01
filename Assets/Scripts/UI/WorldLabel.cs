using TMPro;
using UnityEngine;

// 발판 월드 글자 — 원작 Trig_udongText(j:3659-3674)가 CreateTextTagLocBJ로 포탈·존 자리에 영구로 띄우는 안내 글자
// (「유닛 랜덤」·「금화랜덤 (15+현재라운드x12~x35)」·「스토리 존 입장」 …). 우리 맵엔 포탈이 어떤 일을 하는지 쓴 글이 없었다(GAP 09-27).
// 씬에 박히는 컴포넌트(MapGenerator가 붙인다) — 글자·색·높이만 직렬화하고 TMP는 실행 때 만든다(씬 크기·폰트 참조 안 늘림).
// 카메라를 바라보고(빌보드), 멀면 흐려진다. 월드 크기가 고정이라 카메라 줌에 따라 커지고 작아진다(워크3 글자 태그는 화면 크기 고정이지만
// 우리 RTS 카메라는 높이가 크게 변해서 월드 글자가 더 안정적이다 — 읽히지 않으면 UnitNameplateLayer식 화면 글자로 바꿀 수 있다).
public class WorldLabel : MonoBehaviour
{
    [SerializeField, TextArea] string text = "";
    [SerializeField] Color color = new Color(1f, 0.51f, 0f, 1f);   // 원작 |cffFF8200
    [SerializeField] float fontSize = 18f;                          // 월드 단위 글자 크기
    [SerializeField] float fadeStart = 260f;
    [SerializeField] float fadeEnd = 520f;

    TextMeshPro label;
    Camera cam;

    public void Configure(string labelText, Color labelColor, float size)
    {
        text = labelText;
        color = labelColor;
        fontSize = size;
    }

    void Awake()
    {
        GameObject child = new GameObject("Text", typeof(TextMeshPro));
        child.transform.SetParent(transform, false);
        label = child.GetComponent<TextMeshPro>();
        // 글자는 한글 SDF(GameHud.UiFontAsset)가 준비된 뒤에 넣는다 — 먼저 넣으면 기본 폰트에 한글이 없어 글자마다 경고 + 네모(□)가 된다(09-27 gameshot 26건).
        label.text = "";
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = color;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);
        label.raycastTarget = false;
    }

    void LateUpdate()
    {
        if (label == null) return;
        // GameHud의 한글 SDF가 늦게 준비될 수 있다 — 준비되면 한 번만 바꿔 끼운다.
        if (GameHud.UiFontAsset == null) return;
        if (label.font != GameHud.UiFontAsset) label.font = GameHud.UiFontAsset;
        if (label.text != text) label.text = text;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        transform.rotation = cam.transform.rotation;   // 빌보드
        float distance = Vector3.Distance(cam.transform.position, transform.position);
        float alpha = Mathf.Clamp01(1f - (distance - fadeStart) / Mathf.Max(0.01f, fadeEnd - fadeStart));
        Color shown = color;
        shown.a = alpha;
        if (label.color != shown) label.color = shown;
        if (label.enabled != alpha > 0.01f) label.enabled = alpha > 0.01f;
    }
}
