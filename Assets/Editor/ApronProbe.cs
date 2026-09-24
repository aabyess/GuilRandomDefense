using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

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
    const string JobKey = PlayKey + ".job";
    const double Settle = 1.5;          // Awake·Start와 시작 구도가 자리 잡을 시간
    const double PlayTimeout = 90;

    [MenuItem("Tools/진단/플레이해서 픽셀 찍기")]
    static void PickPixelsInPlay() => RunInPlay("pixels");

    /// <summary>
    /// NavMesh 질의(<see cref="NavMesh.SamplePosition"/>)는 **플레이 중에만** 뜻이 있다 —
    /// 편집 모드에서는 NavMeshSurface가 자기 데이터를 아직 안 얹었다. 그래서 이것도 플레이로 돈다.
    /// </summary>
    [MenuItem("Tools/진단/플레이해서 앞치마 NavMesh 구멍 찍기")]
    static void NavHolesInPlay() => RunInPlay("navholes");

    /// <summary>길찾기 기준값이 서로 맞는지 잰다(PM 지시 2026-09-24). 값은 아무것도 안 바꾼다.</summary>
    [MenuItem("Tools/진단/플레이해서 길찾기 기준값 재기")]
    static void AgentAuditInPlay() => RunInPlay("agent");

    static void RunInPlay(string job)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorGuards.Dialog(Title, "이미 플레이 중입니다 — 멈추고 다시 부르세요.", "확인"); return; }
        if (EditorUtility.scriptCompilationFailed)
        { EditorGuards.Dialog(Title, "컴파일 오류가 있어 플레이 모드에 못 들어갑니다.", "확인"); return; }

        SessionState.SetString(JobKey, job);
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

            string job = SessionState.GetString(JobKey, "pixels");
            Finish(job == "navholes" ? NavHoles() : job == "agent" ? AgentAudit() : Sweep());
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

    // ── 앞치마 NavMesh 구멍 ──────────────────────────────────────────────────
    //
    // navlane은 구멍 칸의 **맨 위 콜라이더 하나**만 찍는다. 그래서 09-24에 「구멍의 맨 위가
    // 앞치마 바닥」이라는, 그 자체로는 아무것도 못 가리는 답이 나왔다 — 바닥이 맨 위라는 건
    // 그 광선이 벽을 안 스쳤다는 뜻일 뿐, 벽이 없다는 뜻이 아니다.
    // 그래서 여기서는 **칸을 덮는 콜라이더를 전부** 센다. 얇은 것도 상자로 훑으면 걸린다.
    //
    // 그리고 판정을 NavMesh에 직접 묻는다(`SamplePosition`). 「걸을 수 있나」는 굽힌 결과가
    // 정답이고, 콜라이더를 보고 추론하면 침식(에이전트 반지름)을 빼먹는다.

    const float HoleStep = 10f;             // 칸 한 변. 앞치마 578×199 → 58×20칸
    const float HoleProbeHeight = 6f;       // 바닥 위로 이만큼까지 훑는다(벽 높이 5.5를 덮는다)

    static string NavHoles()
    {
        StringBuilder sb = new StringBuilder();
        NavMeshBuildSettings agent = NavMesh.GetSettingsByIndex(0);
        sb.AppendLine($"앞치마 x {Apron.xMin:0.#}~{Apron.xMax:0.#} · z {Apron.yMin:0.#}~{Apron.yMax:0.#} · " +
                      $"칸 {HoleStep:0.#} · 복셀 {MapLayout.NavMeshVoxelSize:0.#} · " +
                      $"에이전트 반지름 {agent.agentRadius:0.##} 높이 {agent.agentHeight:0.##}");

        Dictionary<string, int> blamed = new Dictionary<string, int>();
        Dictionary<string, Vector3> blamedSize = new Dictionary<string, Vector3>();
        List<string> firstFew = new List<string>();
        int land = 0, holes = 0;
        Collider[] found = new Collider[32];

        for (float x = Apron.xMin + HoleStep * 0.5f; x < Apron.xMax; x += HoleStep)
        {
            for (float z = Apron.yMin + HoleStep * 0.5f; z < Apron.yMax; z += HoleStep)
            {
                land++;
                Vector3 p = new Vector3(x, MapLayout.IslandTop, z);
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, HoleStep * 0.5f, NavMesh.AllAreas) &&
                    Mathf.Abs(hit.position.y - p.y) < 3f)
                    continue;

                holes++;
                // 칸을 덮는 콜라이더 **전부**. 맨 위 하나만 보면 얇은 벽을 놓친다.
                int n = Physics.OverlapBoxNonAlloc(
                    p + Vector3.up * (HoleProbeHeight * 0.5f),
                    new Vector3(HoleStep * 0.5f, HoleProbeHeight * 0.5f, HoleStep * 0.5f),
                    found, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);

                List<string> names = new List<string>();
                for (int i = 0; i < n; i++)
                {
                    Collider c = found[i];
                    // 바닥 판은 구멍의 원인이 아니라 바닥이다. 서 있는 것만 범인 후보로 센다.
                    bool standing = c.bounds.max.y > MapLayout.IslandTop + 0.5f;
                    string key = System.Text.RegularExpressions.Regex.Replace(c.gameObject.name, @"\d+", "#");
                    names.Add(standing ? key : "(바닥)" + key);
                    if (!standing) continue;
                    blamed.TryGetValue(key, out int k);
                    blamed[key] = k + 1;
                    blamedSize[key] = c.transform.lossyScale;
                }
                if (firstFew.Count < 12)
                    firstFew.Add($"  x {x:0.#} z {z:0.#} → {(names.Count == 0 ? "콜라이더 없음" : string.Join(", ", names))}");
            }
        }

        // ⚠️ 「몇 칸」은 칸 크기 없이는 뜻이 없다. 09-24에 같은 NavMesh를 두고 「14칸」과 「2칸」이
        //    나왔고, 갈린 것은 결함이 아니라 **해상도**였다. 그래서 개수마다 기준과 비율을 붙인다.
        string basis = $"칸 {HoleStep:0.#} 기준";
        sb.AppendLine($"땅 {land}칸({basis}) 중 NavMesh 없음 {holes}칸 = 앞치마의 {(land == 0 ? 0f : 100f * holes / land):0.##}%");
        sb.AppendLine("구멍을 덮는 **서 있는** 콜라이더 (많은 순):");
        if (blamed.Count == 0) sb.AppendLine("  없음 — 서 있는 것이 아니라 다른 이유다");
        foreach (KeyValuePair<string, int> e in blamed.OrderByDescending(e => e.Value))
        {
            Vector3 s = blamedSize[e.Key];
            float thin = Mathf.Min(s.x, s.z);
            sb.AppendLine($"  {e.Value,4}칸({basis}, 앞치마의 {100f * e.Value / Mathf.Max(1, land):0.##}%)  {e.Key}" +
                          $"  크기 {s.x:0.##}×{s.y:0.##}×{s.z:0.##}" +
                          $"  얇은 쪽 {thin:0.##} = 복셀 {thin / MapLayout.NavMeshVoxelSize:0.##}칸" +
                          $"  (반지름 {agent.agentRadius:0.##} 침식까지 치면 길이 {thin + agent.agentRadius * 2f:0.##} 만큼 막힌다)");
        }
        sb.AppendLine("구멍 칸 몇 개 (좌표 → 그 자리 콜라이더 전부):");
        foreach (string line in firstFew) sb.AppendLine(line);
        return sb.ToString();
    }

    // ── 길찾기 기준값 재기 ────────────────────────────────────────────────────
    //
    // 세 값이 서로 다른 것을 잰다(PM 지시 2026-09-24). **축이 둘이라 섞으면 안 된다:**
    //  · 굽기 `agentRadius` — NavMesh가 벽에서 물러나는 폭. **통로 폭을 정한다.** 바꾸면 다시 구워야 한다.
    //  · 런타임 `NavMeshAgent.radius` — 회피 계산의 제 몸 크기. 통로를 안 좁힌다.
    //    ⚠️ UnitMover가 회피를 **끄므로**(사장님 지시 「겹치게」) 지금은 이 값이 작동할 자리가 없다.
    //
    // 통로 여유는 다시 굽지 않고 잰다: `FindClosestEdge`가 주는 가장자리까지 거리에 **지금 굽기
    // 반지름을 더하면** 그 자리의 진짜 통로 반폭이다. 가장 좁은 곳이 굽기 반지름의 상한이다.

    const float AuditStep = 12f;
    const float BodyHeightForOverhead = 30f;   // 유닛 키 상한. 이만큼 위에 뭐가 있으면 뚫고 지나간다

    static string AgentAudit()
    {
        StringBuilder sb = new StringBuilder();
        NavMeshBuildSettings bake = NavMesh.GetSettingsByIndex(0);

        sb.AppendLine("■ 기준값 셋");
        sb.AppendLine($"  굽기   반지름 {bake.agentRadius:0.##} · 높이 {bake.agentHeight:0.##} · " +
                      $"오르기 {bake.agentClimb:0.##} · 복셀 {MapLayout.NavMeshVoxelSize:0.##}");

        // 실제로 돌고 있는 에이전트에서 읽는다 — 프리팹 값이 아니라 **지금 화면의 값**이다.
        //
        // 🔴 단위 함정(09-24 PM 지적): `NavMeshAgent.radius/height`는 **로컬값**이고, 에이전트는
        //    트랜스폼 배율을 그대로 탄다. 위습은 프리팹이 `0.28f / WispScale`로 적혀 있어서
        //    로컬 0.0056이 찍히는데, 배율 50을 타서 **월드에선 0.28**이다. 그걸 굽기 반지름 0.5
        //    옆에 나란히 찍으면 「유닛이 굽기의 1/50이다」라는 틀린 결론이 나온다.
        //    → **월드값(로컬값)** 순서로 찍는다. 기준이 다른 두 숫자를 나란히 두지 않는다.
        //
        // 그리고 유닛과 위습은 **아예 다른 물건**이라 갈라서 찍는다. 09-24에 씬에 유닛이 0기이고
        // 위습만 5기였는데 한 줄로 합쳐 찍어서, 위습 수치가 유닛 수치로 읽혔다.
        NavMeshAgent[] agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        NavMeshAgent[] wisps = agents.Where(a => a.GetComponentInParent<Wisp>() != null).ToArray();
        NavMeshAgent[] units = agents.Except(wisps).ToArray();

        AppendAgents(sb, "유닛", units);
        AppendAgents(sb, "위습", wisps);
        if (units.Length == 0)
            sb.AppendLine("  🔴 유닛 0기 — 유닛에 대한 반지름·회피는 이번 판에서 **확인되지 않았습니다.**");

        // ── 통로 여유: 굽기 반지름을 얼마까지 올릴 수 있나
        //    필드와 앞치마를 **갈라서** 낸다. 09-24에 최소 반폭 0.7이 전부 z 1143~1275(우리
        //    칸막이)에서 나왔다 — 칸막이 하나가 맵 전체의 상한을 정하고 있는지 보려면 갈라야 한다.
        sb.AppendLine("■ 통로 여유 (가장자리까지 거리 + 지금 굽기 반지름 = 진짜 통로 반폭)");
        Dictionary<string, List<(float half, Vector3 at)>> byArea =
            new Dictionary<string, List<(float, Vector3)>> { ["필드"] = new List<(float, Vector3)>(), ["앞치마"] = new List<(float, Vector3)>() };

        foreach ((string area, Vector3 p) in AuditSamples())
        {
            if (!TryGround(p, out Vector3 at)) continue;
            if (!NavMesh.FindClosestEdge(at, out NavMeshHit edge, NavMesh.AllAreas)) continue;
            byArea[area].Add((edge.distance + bake.agentRadius, at));
        }

        string[] names = { "<2", "2~5", "5~10", "10~20", "20~40", "40 이상" };
        foreach (KeyValuePair<string, List<(float half, Vector3 at)>> area in byArea)
        {
            List<(float half, Vector3 at)> list = area.Value;
            sb.AppendLine($"  [{area.Key}] NavMesh 위 표본 {list.Count}개 (칸 {AuditStep:0.#} 기준)");
            if (list.Count == 0) continue;

            float[] buckets = new float[6];
            foreach ((float half, Vector3 _) in list)
                buckets[half < 2f ? 0 : half < 5f ? 1 : half < 10f ? 2 : half < 20f ? 3 : half < 40f ? 4 : 5]++;
            for (int i = 0; i < buckets.Length; i++)
                sb.AppendLine($"      반폭 {names[i],-7} {buckets[i],6}개 ({100f * buckets[i] / list.Count:0.#}%)");
            sb.AppendLine($"      **최소 반폭 {list.Min(t => t.half):0.##}** = 이 구역만 보면 굽기 반지름 상한");
            foreach ((float half, Vector3 at) in list.OrderBy(t => t.half).Take(5))
                sb.AppendLine($"        {half:0.##}  x {at.x:0.#} z {at.z:0.#}");
        }

        // ── 머리 위: 굽기 높이 2로 깔린 NavMesh 밑에 낮은 지붕이 있나
        sb.AppendLine($"■ 머리 위 (NavMesh 위 {BodyHeightForOverhead:0.#} 안에 뭐가 있나 = 키 {BodyHeightForOverhead:0.#}짜리가 뚫고 지나간다)");
        Dictionary<string, int> overhead = new Dictionary<string, int>();
        int checkedPoints = 0;
        foreach ((string _, Vector3 p) in AuditSamples())
        {
            if (!TryGround(p, out Vector3 at)) continue;
            checkedPoints++;
            // 🔴 바닥 판 윗면(8.0)보다 **확실히 위**에서 쏜다. 09-24에 0.5만 띄우고 쐈더니
            //    바다로 스냅된 표본(y≈0)에서 광선이 앞치마 **아랫면**을 맞혀, 바닥이 범인으로
            //    찍혔다. TryGround가 그 표본을 걸러내지만 시작 높이도 같이 올려 둔다.
            Vector3 from = new Vector3(at.x, MapLayout.IslandTop + 1f, at.z);
            if (!Physics.Raycast(from, Vector3.up, out RaycastHit hit, BodyHeightForOverhead)) continue;
            string key = System.Text.RegularExpressions.Regex.Replace(hit.collider.gameObject.name, @"\d+", "#");
            overhead.TryGetValue(key, out int k);
            overhead[key] = k + 1;
        }
        sb.AppendLine($"  표본 {checkedPoints}개(칸 {AuditStep:0.#} 기준) 중 머리 위에 뭔가 있는 칸 " +
                      $"{overhead.Values.Sum()}개 = {(checkedPoints == 0 ? 0f : 100f * overhead.Values.Sum() / checkedPoints):0.##}%");
        foreach (KeyValuePair<string, int> e in overhead.OrderByDescending(e => e.Value).Take(10))
            sb.AppendLine($"    {e.Value,5}칸 ({100f * e.Value / Mathf.Max(1, checkedPoints):0.##}%)  {e.Key}");
        if (overhead.Count == 0) sb.AppendLine("    없음");

        return sb.ToString();
    }

    /// <summary>한 무리의 에이전트를 **월드 단위로** 찍는다. 로컬값은 괄호에 같이 둔다.</summary>
    static void AppendAgents(StringBuilder sb, string label, NavMeshAgent[] group)
    {
        if (group.Length == 0) { sb.AppendLine($"  {label} 0기"); return; }

        NavMeshAgent a = group[0];
        float scale = Mathf.Max(a.transform.lossyScale.x, a.transform.lossyScale.z);
        int avoiding = group.Count(g => g.obstacleAvoidanceType != ObstacleAvoidanceType.NoObstacleAvoidance);

        string body = "";
        Renderer r = a.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            Bounds b = r.bounds;   // bounds는 이미 월드다
            body = $" · 몸 {b.size.x:0.##}×{b.size.y:0.##}×{b.size.z:0.##} → 충돌 반지름 ≈ {Mathf.Max(b.size.x, b.size.z) * 0.5f:0.##}";
        }

        sb.AppendLine($"  {label} {group.Length}기 · 반지름 **{a.radius * scale:0.##}**(로컬 {a.radius:0.####} × 배율 {scale:0.##})" +
                      $" · 높이 **{a.height * scale:0.##}**(로컬 {a.height:0.####})" +
                      $" · 회피 켜진 것 {avoiding}/{group.Length}" +
                      $"{(avoiding == 0 ? " 🔴 전부 꺼짐 — 반지름이 작동할 자리가 없다" : "")}{body}");
    }

    /// <summary>레인1 섬 + 앞치마를 격자로 훑는다. 두 검사가 같은 표본을 써야 견줄 수 있다.</summary>
    static IEnumerable<(string area, Vector3 at)> AuditSamples()
    {
        MapLayout.Island lane = MapLayout.Lanes[0];
        Rect field = Rect.MinMaxRect(lane.center.x - lane.size.x * 0.5f, lane.center.y - lane.size.y * 0.5f,
                                     lane.center.x + lane.size.x * 0.5f, lane.center.y + lane.size.y * 0.5f);
        foreach ((string name, Rect area) in new[] { ("필드", field), ("앞치마", Apron) })
            for (float x = area.xMin + AuditStep * 0.5f; x < area.xMax; x += AuditStep)
                for (float z = area.yMin + AuditStep * 0.5f; z < area.yMax; z += AuditStep)
                    yield return (name, new Vector3(x, MapLayout.IslandTop, z));
    }

    /// <summary>
    /// **섬 높이의** NavMesh로만 스냅한다.
    ///
    /// 🔴 `SamplePosition`은 반경 안에서 가장 가까운 것을 준다. 섬 윗면이 8이고 바다가 0이라
    ///    반경 12로 부르면 섬에 NavMesh가 없는 칸이 **바다로 스냅된다**(8 떨어져 있으니 들어온다).
    ///    09-24에 머리 위 검사가 바다로 스냅된 자리에서 위로 쏴, 앞치마 **아랫면**을 맞히고
    ///    「바닥이 머리 위에 있다」는 읽을 수 없는 답을 냈다. 통로 여유 분포도 같이 오염됐다.
    /// </summary>
    static bool TryGround(Vector3 p, out Vector3 at)
    {
        at = p;
        if (!NavMesh.SamplePosition(p, out NavMeshHit hit, AuditStep, NavMesh.AllAreas)) return false;
        if (Mathf.Abs(hit.position.y - MapLayout.IslandTop) > 2f) return false;
        at = hit.position;
        return true;
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
