using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 맥 F키 대책(사장님 10-08): 맥은 F키가 밝기·볼륨이라 fn을 같이 눌러야 한다 → Option(Alt)+숫자를 같은 기능에 대응시킨다.
/// F5=⌥5 · F10=⌥0 · F11=⌥-. F12는 키 코드가 없어 대응 키 없음. Input System만 쓴다.
/// </summary>
public static class HotkeyAlias
{
    public static bool IsMac => Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;

    public static bool AltHeld(Keyboard kb) => kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);

    /// <summary>F키 또는 Option+대응 숫자가 이번 프레임에 눌렸나. alt 대응은 입력칸에 글을 쓰는 중엔 끈다(⌥5가 글자로 들어간다).</summary>
    public static bool Pressed(Keyboard kb, Key fKey, bool allowAlt = true)
    {
        if (kb == null) return false;
        if (kb[fKey].wasPressedThisFrame) return true;
        if (!allowAlt || !AltHeld(kb)) return false;
        Key alt = AltKeyFor(fKey);
        return alt != Key.None && kb[alt].wasPressedThisFrame;
    }

    static Key AltKeyFor(Key f)
    {
        switch (f)
        {
            case Key.F1: return Key.Digit1;
            case Key.F2: return Key.Digit2;
            case Key.F3: return Key.Digit3;
            case Key.F4: return Key.Digit4;
            case Key.F5: return Key.Digit5;
            case Key.F6: return Key.Digit6;
            case Key.F7: return Key.Digit7;
            case Key.F8: return Key.Digit8;
            case Key.F9: return Key.Digit9;
            case Key.F10: return Key.Digit0;
            case Key.F11: return Key.Minus;
            default: return Key.None;   // F12 등
        }
    }

    /// <summary>라벨용: 맥에서만 「F10 / ⌥0」, 그 밖엔 「F10」.</summary>
    public static string Label(string f)
    {
        if (!IsMac) return f;
        switch (f)
        {
            case "F5": return "F5 / Opt+5";
            case "F10": return "F10 / Opt+0";
            case "F11": return "F11 / Opt+-";
            default: return f;
        }
    }

    public const string MacHint = "F키가 안 먹으면 fn 키를 같이 누르거나 시스템 설정 > 키보드 > 「F1, F2 등을 표준 기능 키로 사용」을 켜세요. 또는 Option+숫자: Opt+5 조합 검색 · Opt+0 메뉴 · Opt+- 동맹";
}
