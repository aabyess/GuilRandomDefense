using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 워크3풍 F10 메뉴 단추의 글자 꾸밈(사장님 10-07 「메뉴 UI 개선」): 평소 흰 글자(검은 그림자) · 마우스를 올리면 금빛 · 누르는 동안 2px 아래로 · 눌러도 안 되는 단추는 회색.
/// 단추 그림(menu_btn 4상태)은 Button의 SpriteSwap이 바꾼다 — 이 컴포넌트는 글자만 맡는다. 메뉴는 드물게 열려서 Update로 매 프레임 상태를 본다.
/// </summary>
public class Wc3MenuButtonText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    static readonly Color Normal = Color.white;
    static readonly Color Hover = new Color(1f, 0.84f, 0.30f);
    static readonly Color Disabled = new Color(0.62f, 0.62f, 0.62f);

    Button button;
    TMP_Text text;
    bool hover, down;
    Vector2 baseMin, baseMax;

    public void Init(Button targetButton, TMP_Text targetText)
    {
        button = targetButton;
        text = targetText;
        baseMin = text.rectTransform.offsetMin;
        baseMax = text.rectTransform.offsetMax;
        text.outlineWidth = 0.22f;                           // 검은 그림자 대신 가는 검정 외곽선(풀밭·돌판 어디서도 읽히게)
        text.outlineColor = new Color32(0, 0, 0, 255);
        text.fontStyle = FontStyles.Bold;
    }

    void OnDisable() { hover = down = false; }

    void Update()
    {
        if (text == null || button == null) return;
        bool on = button.interactable;
        text.color = !on ? Disabled : (hover ? Hover : Normal);
        Vector2 shift = down && on ? new Vector2(0f, -2f) : Vector2.zero;
        text.rectTransform.offsetMin = baseMin + shift;
        text.rectTransform.offsetMax = baseMax + shift;
    }

    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
    public void OnPointerDown(PointerEventData e) => down = true;
    public void OnPointerUp(PointerEventData e) => down = false;
}
