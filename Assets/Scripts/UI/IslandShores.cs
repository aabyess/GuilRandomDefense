using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 섬 가장자리에 입체감(사장님 지시 2026-09-30 「원랜디는 맵 자체가 섬 — 우리도 섬처럼 3D 느낌을」).
///
/// 지금까지 섬은 잔디 판 + 곧게 선 바위 상자(「이름_Cliff」)여서, 내려다보는 카메라에선 옆면이 안 보여 납작한 판으로 읽혔다.
/// 실행 때 그 상자를 끄고 대신 섬 둘레를 따라 **바깥으로 퍼지며 내려가는 울퉁불퉁한 절벽**과 **물속 얕은 턱**을 세운다:
///   · 절벽 — 잔디 끝에서 바깥 <see cref="ShoreWidth"/>까지 계단지게 내려가 수면에 닿는다(위에서 봐도 바위 띠가 보인다).
///   · 물속 턱 — 수면 아래로 완만히 내려가는 모래 바닥. 바다 셰이더(SeaWater)가 깊이로 색을 내므로
///     섬 둘레에 청록 얕은 물과 물거품 띠가 저절로 생긴다.
///
/// 순수 장식이다: 콜라이더 없음, NavMesh(씬에 구워져 있다)와 무관, 맵 재생성 불필요(CombineBoardHeaders와 같은 방식).
/// 맵 루트 밖에 둔다 — MinimapCamera.FitToMap이 맵 아래 렌더러 경계로 범위를 잡는다.
/// 모양은 섬 이름에서 뽑은 씨앗으로 정해져 매번 같다(멀티 양쪽 화면도 같다).
/// </summary>
public static class IslandShores
{
    const string CliffSuffix = "_Cliff";
    const float SegmentLength = 6f;      // 둘레를 이 길이로 끊는다
    const int CornerSteps = 5;           // 모퉁이를 둥글게 도는 칸 수
    public const float ShoreWidth = 10.5f;   // 잔디 끝 → 수면까지 바깥 폭(섬 사이 간격 34.6의 1/3 안쪽)

    // 단면: (잔디 끝에서 바깥으로, 높이 = 섬 윗면에서 아래로, 흔들림 폭). 앞 여섯이 절벽, 뒤 셋이 물속 턱.
    // 높이는 「섬 윗면 기준」이라 IslandTop이 바뀌어도 수면(y=0)에 맞춰 비례로 다시 선다.
    static readonly Vector3[] Profile =
    {
        new Vector3(0.0f, 0.045f, 0.0f),
        new Vector3(1.2f, 0.10f, 0.4f),
        new Vector3(3.5f, 0.38f, 1.3f),
        new Vector3(6.5f, 0.68f, 2.0f),
        new Vector3(9.0f, 0.92f, 2.4f),
        new Vector3(10.5f, 1.05f, 2.4f),   // 수면 바로 아래
        new Vector3(14.0f, 1.20f, 3.0f),
        new Vector3(18.5f, 1.65f, 3.5f),
        new Vector3(21.0f, 2.20f, 3.5f),
    };
    const float MaxSpread = 2.6f;        // 트인 바다 쪽에서 단면을 이만큼까지 넓힌다(절벽 띠 27, 물속 턱 끝 55쯤)
    const float RidgeHeight = 3.2f;      // 테두리 바위 둔덕이 잔디보다 솟는 높이(유닛 키 30의 1/9 — 시야를 안 가린다)
    const float RidgeRoomMin = 6f;       // 이웃 섬이 이보다 가까우면 둔덕을 안 세운다
    const int CliffBands = 5;   // Profile 0~5 사이 다섯 띠가 절벽(바위), 나머지가 물속 턱(모래)

    static readonly Color SandTint = new Color(0.93f, 0.86f, 0.66f);

    /// <summary>세운 섬 수(테스트용).</summary>
    public static int Built { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        Built = 0;
        // 먼저 섬을 다 모은다 — 절벽 폭은 「이웃 섬까지의 빈 바다」로 정한다(트인 바다 쪽은 넓게, 이웃과 붙은 변은 접는다).
        var boxes = new List<MeshRenderer>();
        var rects = new List<Rect>();
        foreach (MeshRenderer box in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!box.name.EndsWith(CliffSuffix, System.StringComparison.Ordinal)) continue;
            Transform t = box.transform;
            Vector3 scale = t.lossyScale;
            if (t.position.y + scale.y * 0.5f <= 0.5f) continue;
            // 상자는 잔디보다 양변 합 CliffOverhang(3.5)만큼 넓다 — 잔디 크기는 상자에서 되돌려 잰다.
            float sizeX = scale.x - CliffOverhangBothSides, sizeZ = scale.z - CliffOverhangBothSides;
            if (sizeX <= 4f || sizeZ <= 4f) continue;
            boxes.Add(box);
            rects.Add(new Rect(t.position.x - sizeX * 0.5f, t.position.z - sizeZ * 0.5f, sizeX, sizeZ));
        }
        GameObject root = boxes.Count > 0 ? new GameObject("[IslandShores]") : null;
        for (int b = 0; b < boxes.Count; b++)
        {
            MeshRenderer box = boxes[b];
            Transform t = box.transform;
            float top = t.position.y + t.lossyScale.y * 0.5f;   // 상자 윗면 = 잔디 밑면
            Material rock = box.sharedMaterial;
            float tilesPerUnit = rock != null ? rock.mainTextureScale.x / Mathf.Max(1f, rects[b].width) : 0.03f;
            Build(root.transform, box.name.Substring(0, box.name.Length - CliffSuffix.Length),
                  new Vector3(t.position.x, top, t.position.z), rects[b].width, rects[b].height, rock, tilesPerUnit, rects, b);
            box.enabled = false;
            Built++;
        }
        if (Built > 0) Debug.Log($"[섬 가장자리] 절벽·물속 턱 {Built}개 섬");
    }

    const float CliffOverhangBothSides = 3.5f;   // MapLayout.CliffOverhang(에디터 쪽 상수)와 같은 값

    static void Build(Transform parent, string islandName, Vector3 topCenter, float sizeX, float sizeZ, Material rock, float tilesPerUnit,
                      List<Rect> islands, int self)
    {
        // 둘레를 따라 「기준점 + 바깥 방향」을 늘어놓는다. 변에서는 기준점이 움직이고, 모퉁이에서는 방향이 90° 돈다.
        var bases = new List<Vector2>();
        var normals = new List<Vector2>();
        float hx = sizeX * 0.5f, hz = sizeZ * 0.5f;
        Vector2[] corners = { new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz), new Vector2(-hx, -hz) };
        Vector2[] edgeNormals = { new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(0f, -1f) };
        for (int e = 0; e < 4; e++)
        {
            Vector2 from = corners[e], to = corners[(e + 1) % 4];
            int steps = Mathf.Max(1, Mathf.RoundToInt((to - from).magnitude / SegmentLength));
            for (int i = 0; i < steps; i++)
            {
                bases.Add(Vector2.Lerp(from, to, i / (float)steps));
                normals.Add(edgeNormals[e]);
            }
            Vector2 next = edgeNormals[(e + 1) % 4];
            for (int i = 0; i < CornerSteps; i++)
            {
                bases.Add(to);
                normals.Add(Vector2.Lerp(edgeNormals[e], next, i / (float)CornerSteps).normalized);
            }
        }
        int count = bases.Count;

        // 흔들림 — 둘레를 한 바퀴 돌면 제자리로 오는 사인 넷(큰 굴곡) + 점마다 잔 흔들림. 씨앗은 섬 이름.
        var random = new System.Random(StableHash(islandName));
        float[] phase = new float[4];
        int[] waves = new int[4];
        for (int k = 0; k < 4; k++)
        {
            phase[k] = (float)random.NextDouble() * Mathf.PI * 2f;
            waves[k] = Mathf.Max(2, Mathf.RoundToInt(count / (9f + 13f * (float)random.NextDouble() + k * 6f)));
        }
        float[] swell = new float[count];
        float[] jitter = new float[count * Profile.Length];
        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f, s = 0f;
            for (int k = 0; k < 4; k++) s += Mathf.Sin(a * waves[k] + phase[k]) / (1f + k * 0.6f);
            swell[i] = s / 2.4f;   // 대략 −1~1
        }
        for (int i = 0; i < jitter.Length; i++) jitter[i] = (float)random.NextDouble() * 2f - 1f;

        // 점마다 「바깥으로 얼마나 퍼질 수 있나」 — 가장 가까운 이웃 섬까지 거리의 0.45(둘이 마주 퍼져도 가운데 물길이 남는다).
        float profileEnd = Profile[Profile.Length - 1].x + Profile[Profile.Length - 1].z;
        float[] room = new float[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 world = new Vector2(topCenter.x, topCenter.z) + bases[i];
            float nearest = MaxSpread * profileEnd / 0.45f;
            for (int o = 0; o < islands.Count; o++)
            {
                if (o == self) continue;
                Rect other = islands[o];
                float dx = Mathf.Max(other.xMin - world.x, 0f, world.x - other.xMax);
                float dz = Mathf.Max(other.yMin - world.y, 0f, world.y - other.yMax);
                nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dz * dz));
            }
            room[i] = nearest;
        }
        // 폭이 점마다 뚝뚝 끊기지 않게 둘레를 따라 고른다(앞뒤 여섯 점 가운데 가장 좁은 쪽으로).
        float[] spread = new float[count];
        float[] ridge = new float[count];
        for (int i = 0; i < count; i++)
        {
            float least = room[i], sum = 0f;
            for (int k = -6; k <= 6; k++)
            {
                float r = room[((i + k) % count + count) % count];
                least = Mathf.Min(least, r);
                sum += r;
            }
            float smooth = Mathf.Lerp(least, sum / 13f, 0.35f);
            spread[i] = Mathf.Clamp(smooth * 0.45f / profileEnd, 0.02f, MaxSpread);
            // 바위 둔덕은 트인 바다 쪽에만 — 이웃과 붙은 변(레인 ↔ 앞치마)에 서면 땅 한가운데 담이 된다.
            ridge[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RidgeRoomMin, RidgeRoomMin * 3f, smooth));
        }

        Vector3[,] ring = new Vector3[Profile.Length, count];
        for (int r = 0; r < Profile.Length; r++)
        {
            Vector3 p = Profile[r];
            for (int i = 0; i < count; i++)
            {
                float j = jitter[r * count + i];
                float outward = (p.x + p.z * (swell[i] * 0.75f + j * 0.35f)) * spread[i];
                float height = topCenter.y * (1f - p.y);
                // 가운데 띠는 높이도 흔들어 턱(선반)이 지게 한다. 맨 위 줄은 잔디 밑에 딱 붙인다.
                if (r >= 2 && r <= 4) height += j * topCenter.y * 0.07f;
                // 잔디 바로 밖 둘째 줄은 잔디보다 솟은 바위 둔덕 — 위에서 봐도 섬 테두리가 턱으로 읽히고 그림자가 진다.
                if (r == 1)
                {
                    float bump = 0.35f + 0.65f * Mathf.Max(0f, swell[(i + count / 3) % count]) + 0.25f * Mathf.Abs(j);
                    height = topCenter.y + RidgeHeight * bump * ridge[i] - (1f - ridge[i]) * topCenter.y * 0.06f;
                }
                Vector2 xz = bases[i] + normals[i] * Mathf.Max(0f, outward);
                ring[r, i] = new Vector3(topCenter.x + xz.x, height, topCenter.z + xz.y);
            }
        }

        Material sand = null;
        Material cliff = null;
        if (rock != null)
        {
            cliff = new Material(rock) { name = rock.name + "_둘레" };
            cliff.mainTextureScale = Vector2.one;
            sand = new Material(cliff) { name = rock.name + "_물속턱" };
            if (sand.HasProperty("_BaseColor")) sand.SetColor("_BaseColor", SandTint);
        }
        AddBands(parent, islandName + "_절벽", ring, 0, CliffBands, count, cliff, tilesPerUnit, true);
        AddBands(parent, islandName + "_물속턱", ring, CliffBands, Profile.Length - 1, count, sand, tilesPerUnit, false);
    }

    // 띠마다 네모를 따로 세운다(정점을 안 나눠 쓴다) — 면이 각져야 바위로 읽힌다.
    static void AddBands(Transform parent, string name, Vector3[,] ring, int fromBand, int toBand, int count,
                         Material material, float tilesPerUnit, bool castShadows)
    {
        int quads = (toBand - fromBand) * count;
        var vertices = new Vector3[quads * 4];
        var uvs = new Vector2[quads * 4];
        var triangles = new int[quads * 6];
        int v = 0, t = 0;
        for (int r = fromBand; r < toBand; r++)
        {
            float along = 0f;
            for (int i = 0; i < count; i++)
            {
                int n = (i + 1) % count;
                Vector3 a = ring[r, i], b = ring[r, n], c = ring[r + 1, n], d = ring[r + 1, i];
                float step = Vector3.Distance(a, b);
                float down = Vector3.Distance(a, d), downNext = Vector3.Distance(b, c);
                vertices[v] = a; vertices[v + 1] = b; vertices[v + 2] = c; vertices[v + 3] = d;
                float v0 = r * 3.1f;   // 띠마다 무늬가 어긋나게
                uvs[v] = new Vector2(along, v0) * tilesPerUnit;
                uvs[v + 1] = new Vector2(along + step, v0) * tilesPerUnit;
                uvs[v + 2] = new Vector2(along + step, v0 + downNext) * tilesPerUnit;
                uvs[v + 3] = new Vector2(along, v0 + down) * tilesPerUnit;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                v += 4; t += 6; along += step;
            }
        }
        Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
    }

    // string.GetHashCode는 실행마다 다를 수 있다 — 두 PC가 같은 모양을 보려면 직접 센다.
    static int StableHash(string text)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in text) hash = hash * 31 + c;
            return hash;
        }
    }
}
