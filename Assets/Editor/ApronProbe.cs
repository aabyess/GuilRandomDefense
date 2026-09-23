using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 「앞치마 위 하얀 가시밭」 진단(2026-09-24, 구현담당1).
///
/// 씬 파일을 손으로 파싱해서는 앞치마 위에 울타리 285장·상점 8채 말고 **아무것도 없다**는
/// 결론까지만 나왔다. 그런데 사진에서는 가시가 잔디 지평선을 넘는다 — 평면 텍스처는
/// 그럴 수 없으니 서 있는 물체다. 둘 중 하나가 틀렸다는 뜻이라, 유니티에게 직접 묻는다.
///
/// 레인1 앞치마 상자 안에서 **바닥보다 높이 솟은 렌더러**를 전부 찍는다. 파싱이 옳았다면
/// 울타리와 상점만 나오고, 그렇다면 가시는 실행 중에 생기는 것이거나 셰이딩이다.
/// 반대로 모르는 이름이 쏟아지면 그게 범인이다.
/// </summary>
public static class ApronProbe
{
    const string Title = "앞치마 진단";

    // 씬에서 잰 레인1 앞치마(578.2 × 199.4). 윗면은 IslandTop = 8.
    static readonly Rect Apron = Rect.MinMaxRect(-1911.6f, 1089.4f, -1333.4f, 1288.8f);
    const float FloorTop = 8.3f;          // 바닥 장식(두께 0.1, 윗면 8.1)보다 위만 센다

    [MenuItem("Tools/진단/앞치마 위에 선 것 찍기")]
    static void Probe()
    {
        Dictionary<string, int> byName = new Dictionary<string, int>();
        Dictionary<string, Vector2> heights = new Dictionary<string, Vector2>();   // (최저, 최고)
        int total = 0;

        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Bounds b = renderer.bounds;
            if (b.max.y <= FloorTop) continue;                       // 바닥에 누운 것은 뺀다
            if (!Apron.Overlaps(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z))) continue;

            // 이름은 가장 위의 **씬 오브젝트**로 묶는다 — 프리팹 속 부품 이름(몸·Object_9)으로
            // 흩어지면 몇 개인지가 안 보인다.
            Transform top = renderer.transform;
            while (top.parent != null && top.parent.name != "Map") top = top.parent;

            string key = System.Text.RegularExpressions.Regex.Replace(top.name, @"\d+", "#");
            byName.TryGetValue(key, out int n);
            byName[key] = n + 1;
            float lo = b.min.y, hi = b.max.y;
            if (heights.TryGetValue(key, out Vector2 h)) heights[key] = new Vector2(Mathf.Min(h.x, lo), Mathf.Max(h.y, hi));
            else heights[key] = new Vector2(lo, hi);
            total++;
        }

        List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(byName);
        rows.Sort((a, b) => b.Value.CompareTo(a.Value));

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"레인1 앞치마({Apron.width:0.#}×{Apron.height:0.#}) 안에서 y>{FloorTop} 인 렌더러: {total}개");
        sb.AppendLine("(이름 / 개수 / 아랫변~윗변)");
        foreach (KeyValuePair<string, int> row in rows)
        {
            Vector2 h = heights[row.Key];
            sb.AppendLine($"{row.Value,6}  {row.Key}   y {h.x:0.##}~{h.y:0.##}");
        }

        Debug.Log("[앞치마 진단] " + sb);
        EditorGuards.Dialog(Title, sb.ToString(), "확인");
    }
}
