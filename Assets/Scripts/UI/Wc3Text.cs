using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 원작(워크3) 문자열 → 우리 알림 줄. 워크3 색 코드 |cffRRGGBB … |r를 &lt;color=#RRGGBB&gt; … &lt;/color&gt;로 바꾸고,
/// 줄바꿈으로 나눠 **줄마다 닫힌 태그**로 돌려준다(알림 칸은 한 줄씩 그린다). 원작은 색이 줄을 넘어 이어지기도 해서
/// (예: TRIGSTR_8904 청록이 네 줄) 열린 색을 다음 줄 앞에 다시 연다.
/// </summary>
public static class Wc3Text
{
    static readonly Regex Token = new Regex(@"\|[cC][0-9a-fA-F]{2}([0-9a-fA-F]{6})|\|[rR]");

    public static List<string> ToRichLines(string wc3)
    {
        var lines = new List<string>();
        string open = null;   // 지금 열린 색(RRGGBB)
        foreach (string raw in (wc3 ?? "").Replace("\r", "").Split('\n'))
        {
            var sb = new StringBuilder();
            if (open != null) sb.Append("<color=#").Append(open).Append('>');
            int last = 0;
            foreach (Match m in Token.Matches(raw))
            {
                sb.Append(raw, last, m.Index - last);
                last = m.Index + m.Length;
                if (m.Groups[1].Success)
                {
                    if (open != null) sb.Append("</color>");
                    open = m.Groups[1].Value.ToUpperInvariant();
                    sb.Append("<color=#").Append(open).Append('>');
                }
                else if (open != null)
                {
                    sb.Append("</color>");
                    open = null;
                }
            }
            sb.Append(raw, last, raw.Length - last);
            if (open != null) sb.Append("</color>");
            string line = sb.ToString().Trim();
            if (line.Length > 0 && line != "<color=#" + open + "></color>") lines.Add(line);
        }
        return lines;
    }
}
