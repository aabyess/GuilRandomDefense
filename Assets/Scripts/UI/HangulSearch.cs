using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 조합 검색용 한글 부분 일치(공백 무시) + 초성 검색(ㄱㅈㄱ → 강재규). 유닛마다 검색 글자(친구 이름·별명·에셋 이름·스킨 별명)를 한 번 만들어 캐시한다.
/// </summary>
public static class HangulSearch
{
    const string Choseongs = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
            if (!char.IsWhiteSpace(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    public static string Choseong(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
            sb.Append(c >= 0xAC00 && c <= 0xD7A3 ? Choseongs[(c - 0xAC00) / 588] : c);
        return sb.ToString();
    }

    public static bool IsChoseongQuery(string normalizedQuery)
    {
        if (normalizedQuery.Length == 0) return false;
        foreach (char c in normalizedQuery)
            if (Choseongs.IndexOf(c) < 0) return false;
        return true;
    }

    /// <summary>정규화된 query가 정규화된 text에 들어 있나(초성 질의면 text의 초성에서도 찾는다).</summary>
    public static bool Matches(string normalizedQuery, string normalizedText)
    {
        if (normalizedQuery.Length == 0) return true;
        if (normalizedText.Length == 0) return false;
        if (normalizedText.Contains(normalizedQuery)) return true;
        return IsChoseongQuery(normalizedQuery) && Choseong(normalizedText).Contains(normalizedQuery);
    }

    static readonly Dictionary<UnitData, string[]> unitTexts = new Dictionary<UnitData, string[]>();

    /// <summary>유닛의 검색 글자들(정규화): 사람이름+별명, 별명, 에셋 이름 뒤쪽(희귀함_강재규 → 강재규)과 통째, 스킨 별명(쉼표 구분).</summary>
    public static string[] TextsOf(UnitData unit)
    {
        if (unit == null) return System.Array.Empty<string>();
        if (unitTexts.TryGetValue(unit, out string[] cached)) return cached;
        var list = new List<string>();
        void Add(string s) { string n = Normalize(s); if (n.Length > 0 && !list.Contains(n)) list.Add(n); }
        Add(unit.DisplayName);
        Add(unit.unitName);
        Add(unit.name);
        int underscore = unit.name.IndexOf('_');
        if (underscore >= 0 && underscore + 1 < unit.name.Length) Add(unit.name.Substring(underscore + 1));
        if (!string.IsNullOrEmpty(unit.skinAlias))
            foreach (string alias in unit.skinAlias.Split(',')) Add(alias);
        string[] result = list.ToArray();
        unitTexts[unit] = result;
        return result;
    }

    public static bool UnitMatches(string normalizedQuery, UnitData unit)
    {
        foreach (string text in TextsOf(unit))
            if (Matches(normalizedQuery, text)) return true;
        return false;
    }
}
