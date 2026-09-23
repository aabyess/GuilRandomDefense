using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
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

    // 🔴 처음엔 8.3으로 뒀다가 크게 헤맸다. 「서 있는 것만 보겠다」고 8.3을 넘겼더니
    //    앞치마 바로 위에 깔린 바닥(윗면 8.1)이 통째로 빠졌고, 「위에 아무것도 없다」는
    //    답이 나왔다. 찾는 물건을 거르는 문턱을 세우면 안 된다 — 앞치마 윗면(8.0)부터 전부 본다.
    const float FloorTop = 8.0f;

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

    // ── 픽셀에 직접 묻기 ──────────────────────────────────────────────────────
    //
    // 좌표 역산·씬 파싱·색칠은 전부 **간접**이었고 09-24에 셋 다 틀린 길로 갔다.
    // 직접 묻는 방법은 하나다 — 카메라에서 그 픽셀 방향으로 광선을 쏴서 맞는 것을 본다.
    //
    // ⚠️ `Physics.Raycast`는 못 쓴다. 맵 장식은 콜라이더를 일부러 떼기 때문에(NavMesh를
    //    망치므로) 물리로는 하나도 안 잡힌다. `HandleUtility.PickGameObject`는 GUI 문맥이
    //    있어야 한다. 그래서 렌더러 경계상자로 후보를 추리고, 읽을 수 있는 메시는
    //    **삼각형까지** 맞혀서 거리를 잡는다. 읽을 수 없는 메시는 경계상자 거리로 두고
    //    「경계」라고 표시한다 — 속으면 안 되니까 어느 쪽인지 꼭 찍는다.
    //
    // 플레이 중에도 돌아간다. 사진이 플레이 모드면 **플레이 중에 돌려야** 같은 화면이다 —
    // 프로퍼티 블록이나 저장 안 한 변경은 플레이 모드에서 사라지기 때문이다.

    [MenuItem("Tools/진단/픽셀이 무엇인지 찍기")]
    static void PickPixels()
    {
        string report = Sweep();
        Debug.Log("[픽셀 질의] " + report);
        EditorGuards.Dialog(Title, report, "확인");
    }

    // ── 플레이해서 찍기 ───────────────────────────────────────────────────────
    //
    // 🔴 편집 모드에서 쏘면 **헛것을 쏜다.** 시작 구도는 `RtsCameraController.FrameLaneAndPen`이
    //    플레이 시작 때 정하는데(그 함수 주석 :109 — 「계산이 맞는지 보기 전에 그 값이 실제로
    //    쓰이는지 본다」), 편집 모드의 카메라는 MapGenerator가 적어 둔 다른 자리(높이 216.7·z 1432.4)에
    //    있다. 거기서는 찾는 z 1278~1288이 **카메라 뒤**라 열한 점을 다 쏴도 못 맞힌다.
    //
    // 그렇다고 자세를 베껴 적으면 카메라 규칙이 바뀔 때 또 어긋난다. 그리고 브리지는 플레이 중에
    // 명령을 안 집어서 사람이 플레이 중에 메뉴를 누를 수도 없다. 그래서 **진단이 스스로 플레이에
    // 들어갔다 나온다** — gameshot과 같은 방식(SessionState는 도메인 리로드를 넘어 남는다).
    // 그러면 카메라도 HUD도 화면 크기도 **사진과 똑같은 진짜 실행 상태**다. 베낄 숫자가 없다.

    const string PlayKey = "ApronProbe.Play";
    const double Settle = 1.5;          // Awake·Start와 시작 구도가 자리 잡을 시간
    const double PlayTimeout = 90;

    [MenuItem("Tools/진단/플레이해서 픽셀 찍기")]
    static void PickPixelsInPlay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorGuards.Dialog(Title, "이미 플레이 중입니다 — 멈추고 다시 부르세요.", "확인"); return; }
        if (EditorUtility.scriptCompilationFailed)
        { EditorGuards.Dialog(Title, "컴파일 오류가 있어 플레이 모드에 못 들어갑니다.", "확인"); return; }

        SessionState.SetInt(PlayKey, 1);
        SessionState.SetFloat(PlayKey + ".since", 0f);
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Rehook()
    {
        // 플레이 모드 진입은 도메인을 다시 올린다. 그때 이 콜백을 다시 걸어야 이어진다.
        if (SessionState.GetInt(PlayKey, 0) != 0) EditorApplication.update += Tick;
    }

    static void Tick()
    {
        int stage = SessionState.GetInt(PlayKey, 0);
        if (stage == 0) { EditorApplication.update -= Tick; return; }

        float since = SessionState.GetFloat(PlayKey + ".since", 0f);
        double now = EditorApplication.timeSinceStartup;

        if (stage == 1)
        {
            if (!Application.isPlaying)
            {
                if (since == 0f) SessionState.SetFloat(PlayKey + ".since", (float)now);
                else if (now - since > PlayTimeout) Finish("❌ 플레이 모드에 못 들어갔습니다.");
                return;
            }
            if (since == 0f || SessionState.GetInt(PlayKey + ".playing", 0) == 0)
            {
                SessionState.SetInt(PlayKey + ".playing", 1);
                SessionState.SetFloat(PlayKey + ".since", (float)now);
                return;
            }
            if (now - since < Settle) return;

            Finish(Sweep());
            return;
        }
    }

    static void Finish(string report)
    {
        SessionState.SetInt(PlayKey, 0);
        SessionState.SetInt(PlayKey + ".playing", 0);
        EditorApplication.update -= Tick;

        Debug.Log("[픽셀 질의] " + report);
        try
        {
            string dir = System.IO.Path.Combine("ClaudeBridge", "outbox");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "probe.txt"), report,
                new System.Text.UTF8Encoding(false));
        }
        catch (System.Exception e) { Debug.LogWarning("[픽셀 질의] 결과 파일을 못 썼습니다: " + e.Message); }

        if (Application.isPlaying) EditorApplication.ExitPlaymode();
    }

    /// <summary>화면 가운데 세로선을 훑어 각 점이 무엇인지 돌려준다. 대화창을 안 띄운다(플레이 중에 막힌다).</summary>
    static string Sweep()
    {
        Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (cam == null) return "카메라를 못 찾았습니다.";

        StringBuilder sb = new StringBuilder();
        // ⚠️ 「편집 중」이면 카메라가 시작 구도가 아니다 — 아래 좌표를 사진과 맞춰 보면 안 된다.
        sb.AppendLine($"카메라 {cam.transform.position} 방향 {cam.transform.forward} / fov {cam.fieldOfView} / " +
                      $"{cam.pixelWidth}×{cam.pixelHeight} / " +
                      $"{(Application.isPlaying ? "플레이 중 ✅" : "🔴 편집 중 — 시작 구도가 아니라 사진과 다른 화면입니다")}");

        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        // 화면 아래에서 위로 훑는다. 09-24에 찾던 가시 줄은 화면 높이의 **39% 근처**(월드 z≈1280,
        // 우리 북쪽 열린 변)에 있었다 — 25%·50%·75% 세 점만 쏘면 그 사이로 빠져나간다.
        // 촘촘히 훑어야 「어느 행부터 무엇이 바뀌는지」가 보인다.
        foreach (float v in new[] { 0.20f, 0.25f, 0.30f, 0.34f, 0.37f, 0.39f, 0.41f, 0.44f, 0.50f, 0.60f, 0.75f })
        {
            Vector3 screen = new Vector3(cam.pixelWidth * 0.5f, cam.pixelHeight * v, 0f);
            Ray ray = cam.ScreenPointToRay(screen);
            sb.AppendLine($"── 화면 가운데 · 높이 {v * 100:0}%  (픽셀 {screen.x:0},{screen.y:0})");

            List<(float dist, Renderer r, bool exact)> hits = new List<(float, Renderer, bool)>();
            foreach (Renderer renderer in all)
            {
                if (!renderer.bounds.IntersectRay(ray, out float boundsDist)) continue;
                hits.Add(MeshHit(renderer, ray, out float meshDist)
                    ? (meshDist, renderer, true)
                    : (boundsDist, renderer, false));
            }
            hits.Sort((a, b) => a.dist.CompareTo(b.dist));

            if (hits.Count == 0) { sb.AppendLine("   아무것도 안 맞음"); continue; }
            for (int i = 0; i < Mathf.Min(2, hits.Count); i++)
            {
                (float dist, Renderer r, bool exact) = hits[i];
                Vector3 p = ray.GetPoint(dist);
                Material m = r.sharedMaterial;
                // 맞은 자리의 월드 x·z를 같이 찍는다 — 「어느 오브젝트냐」와 「어디냐」가 같이 나와야
                // 사진의 행과 맞춰 볼 수 있다(호모그래피로 잰 값과 대조하려면 z가 필요하다).
                sb.AppendLine($"   {i + 1}. {r.gameObject.name}{(exact ? "" : "(경계)")}" +
                              $"  맞은 자리 x={p.x:0.#} y={p.y:0.##} z={p.z:0.#}" +
                              $"  재질 {(m == null ? "없음" : m.name)}");
            }
        }

        return sb.ToString();
    }

    /// <summary>읽을 수 있는 메시면 삼각형까지 맞혀 정확한 거리를 준다.</summary>
    static bool MeshHit(Renderer renderer, Ray ray, out float distance)
    {
        distance = 0f;
        if (!renderer.TryGetComponent(out MeshFilter filter)) return false;
        Mesh mesh = filter.sharedMesh;
        if (mesh == null || !mesh.isReadable) return false;

        Transform t = renderer.transform;
        Ray local = new Ray(t.InverseTransformPoint(ray.origin), t.InverseTransformDirection(ray.direction));

        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        float best = float.MaxValue;
        for (int i = 0; i < tris.Length; i += 3)
        {
            if (!RayTriangle(local, verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]], out float d)) continue;
            if (d < best) best = d;
        }
        if (best == float.MaxValue) return false;

        // 로컬 거리는 배율이 섞여 있으니 월드로 되돌려 잰다.
        distance = Vector3.Distance(ray.origin, t.TransformPoint(local.GetPoint(best)));
        return true;
    }

    // Möller–Trumbore. 양면 다 맞힌다 — 안쪽에서 보는 면도 화면에 나올 수 있다.
    static bool RayTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0f;
        Vector3 ab = b - a, ac = c - a;
        Vector3 pv = Vector3.Cross(ray.direction, ac);
        float det = Vector3.Dot(ab, pv);
        if (Mathf.Abs(det) < 1e-8f) return false;

        float inv = 1f / det;
        Vector3 tv = ray.origin - a;
        float u = Vector3.Dot(tv, pv) * inv;
        if (u < 0f || u > 1f) return false;

        Vector3 qv = Vector3.Cross(tv, ab);
        float w = Vector3.Dot(ray.direction, qv) * inv;
        if (w < 0f || u + w > 1f) return false;

        distance = Vector3.Dot(ac, qv) * inv;
        return distance > 0f;
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
        // 🔴 저장을 빼먹어서 첫 시험이 헛돌았다(09-24). 플레이 모드는 씬을 **디스크에서 다시 읽는다** —
        //    저장 안 한 재질 교체는 실행하는 순간 사라지고, 사진은 바꾸기 전과 똑같이 나온다.
        EditorSceneManager.SaveOpenScenes();
        EditorGuards.Dialog(Title,
            $"{done}개 면을 민무늬 단색으로 바꾸고 **씬을 저장**했습니다.\n" +
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
        EditorSceneManager.SaveOpenScenes();
        EditorGuards.Dialog(Title,
            $"{done}개 면을 되돌리고 씬을 저장했습니다.\n\n" +
            "⚠️ 타일링은 맵을 다시 생성해야 제 값이 됩니다(기본 재질로 돌려놨습니다).", "확인");
    }
}
