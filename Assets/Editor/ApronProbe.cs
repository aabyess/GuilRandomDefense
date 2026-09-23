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

    // 바닥이 희끄무레한 게 「텍스처가 안 보이는 것」인지 「보이는데 밝은 것」인지 가리려면
    // 유니티가 **실제로 쓰는 값**을 봐야 한다. 재질 파일을 읽어서 추측하면 프로퍼티 블록이
    // 안 보이고, 화면 색만 보면 조명과 못 가른다. 둘 다 한 줄에 찍는다.
    //
    // ⚠️ 프로퍼티 블록은 씬에 저장되지 않는다 — 이 메뉴는 **맵을 생성한 그 세션에서** 돌려야
    //    의미가 있다. 씬을 다시 연 뒤에 돌리면 ST가 비어 있는 게 정상이고, 그 자체가 답이다.
    static readonly string[] Floors =
    {
        "Lane1_상점바닥", "Lane1_유닛우리_바닥", "Lane1_흙길_아래", "Lane1_앞치마", "Lane1",
    };

    [MenuItem("Tools/진단/바닥 재질 실제값 찍기")]
    static void DumpFloors()
    {
        StringBuilder sb = new StringBuilder();
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (string name in Floors)
        {
            GameObject go = GameObject.Find(name);
            if (go == null || !go.TryGetComponent(out Renderer renderer))
            {
                sb.AppendLine($"── {name}: 못 찾음");
                continue;
            }

            Material mat = renderer.sharedMaterial;
            Vector3 size = go.transform.localScale;
            Texture baseMap = mat != null ? mat.GetTexture("_BaseMap") : null;
            Texture bump = mat != null ? mat.GetTexture("_BumpMap") : null;

            renderer.GetPropertyBlock(block);
            Vector4 st = block.isEmpty ? new Vector4(-1f, -1f, 0f, 0f) : block.GetVector("_BaseMap_ST");

            sb.AppendLine($"── {name}  크기 {size.x:0.#}×{size.z:0.#}");
            sb.AppendLine($"   재질 {(mat == null ? "없음" : mat.name)} / 셰이더 {(mat == null ? "-" : mat.shader.name)}");
            sb.AppendLine($"   _BaseColor {(mat == null ? Color.clear : mat.GetColor("_BaseColor"))}" +
                          $"  _BaseMap {(baseMap == null ? "🔴없음" : baseMap.name + " " + baseMap.width + "px")}" +
                          $"  _BumpMap {(bump == null ? "없음" : bump.name)}" +
                          $"  _NORMALMAP {(mat != null && mat.IsKeywordEnabled("_NORMALMAP") ? "켜짐" : "꺼짐")}");

            if (block.isEmpty)
                sb.AppendLine("   프로퍼티 블록 🔴비어 있음 — 타일링이 재질의 값으로 돌아갔다");
            else
                sb.AppendLine($"   _BaseMap_ST 반복 {st.x:0.##}×{st.y:0.##}" +
                              $"  → 타일 한 변 {(st.x > 0 ? size.x / st.x : 0f):0.##} × {(st.y > 0 ? size.z / st.y : 0f):0.##} 월드");
        }

        Debug.Log("[바닥 재질] " + sb);
        EditorGuards.Dialog(Title, sb.ToString(), "확인");
    }

    // ── 색칠 시험 ────────────────────────────────────────────────────────────
    //
    // 여기까지 와서도 안 풀린 것은 **「사진의 그 픽셀이 정말 그 오브젝트냐」**다.
    // 좌표로 맞춰 보긴 했지만, 그건 「거기 그게 있다」이지 「그게 보인다」가 아니다.
    // 면 넷에 서로 다른 **민무늬 단색**을 입히고 한 장 찍으면 셋이 한꺼번에 갈린다:
    //   · 바닥이 그 색으로 바뀌면  → 픽셀의 주인이 확정되고,
    //   · 줄무늬가 사라지면        → 무늬의 출처는 **텍스처**다(민무늬인데 줄이 남을 리 없다),
    //   · 줄무늬가 남으면          → 텍스처가 아니라 **그 면 자체이거나 그 위의 무엇**이다.
    //
    // 단색 재질은 텍스처도 노멀맵도 없으므로 타일링·밉맵·이방성 필터가 전부 빠진다.
    static readonly (string name, string key, Color color)[] Trial =
    {
        ("Lane1_상점바닥",   "rock", new Color(0.90f, 0.10f, 0.10f)),   // 빨강
        ("Lane1_유닛우리_바닥", "dirt", new Color(0.10f, 0.35f, 0.95f)),   // 파랑
        ("Lane1_앞치마",     "lane", new Color(0.95f, 0.10f, 0.85f)),   // 자홍
        ("Lane1_흙길_아래",   "dirt", new Color(1.00f, 0.85f, 0.10f)),   // 노랑
    };

    [MenuItem("Tools/진단/바닥 색칠 시험 — 켜기")]
    static void TrialOn()
    {
        int done = 0;
        foreach ((string name, string _, Color color) in Trial)
        {
            GameObject go = GameObject.Find(name);
            if (go == null || !go.TryGetComponent(out Renderer renderer)) continue;

            Material flat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            flat.SetColor("_BaseColor", color);
            flat.SetFloat("_Smoothness", 0f);
            string path = $"Assets/Materials/Map/시험_{name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(flat, path);

            Undo.RecordObject(renderer, "색칠 시험");
            renderer.sharedMaterial = flat;
            renderer.SetPropertyBlock(null);        // 타일링도 같이 뗀다 — 민무늬니 쓸 데가 없다
            done++;
        }
        AssetDatabase.SaveAssets();
        EditorGuards.Dialog(Title,
            $"{done}개 면을 민무늬 단색으로 바꿨습니다.\n" +
            "상점바닥=빨강 · 우리바닥=파랑 · 앞치마=자홍 · 흙길=노랑\n\n" +
            "같은 자리에서 한 장 찍어 주세요. 끝나면 「바닥 색칠 시험 — 되돌리기」.", "확인");
    }

    [MenuItem("Tools/진단/바닥 색칠 시험 — 되돌리기")]
    static void TrialOff()
    {
        int done = 0;
        foreach ((string name, string key, Color _) in Trial)
        {
            GameObject go = GameObject.Find(name);
            if (go == null || !go.TryGetComponent(out Renderer renderer)) continue;

            Material original = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Map/{key}.mat");
            if (original == null) continue;

            Undo.RecordObject(renderer, "색칠 시험 되돌리기");
            renderer.sharedMaterial = original;
            AssetDatabase.DeleteAsset($"Assets/Materials/Map/시험_{name}.mat");
            done++;
        }
        AssetDatabase.SaveAssets();
        EditorGuards.Dialog(Title,
            $"{done}개 면을 되돌렸습니다.\n\n" +
            "⚠️ 타일링(프로퍼티 블록)은 안 돌아옵니다 — 맵을 다시 생성해야 제 값이 됩니다.", "확인");
    }
}
