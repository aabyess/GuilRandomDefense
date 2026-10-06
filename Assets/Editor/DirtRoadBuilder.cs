using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 레인 흙길 절차 메시(사장님 10-06 「너무 딱딱 직각 — 원랜디처럼 진짜 흙길, 모서리는 둥글게 하되 너무 둥글면 흙길 같지 않다」).
/// 옛 상자 넷(MapGenerator.BuildDecor 「흙길_위/아래/왼/오른」)을 **레인마다 메시 하나**로 바꾼다: 바깥 둥근 직사각형과 안쪽 둥근 직사각형 사이의 띠.
///  · 가운데 선(적 경로·NavMesh·순찰)은 그대로 — 이 메시는 콜라이더 없는 장식이고 NavMeshModifier.ignoreFromBuild.
///  · 모서리 반경은 띠 폭 W의 비율로 정한다(가운데 선 기준 R = 비율 × W; 바깥 Ro = R + W/2, 안쪽 Ri = max(R − W/2, 0.25R)) — 절대값을 박지 않는다.
///  · 가장자리: 폭을 길이 따라 출렁(±wobble)·가장자리 지글지글(jitter) — 양쪽 가장자리가 띠 **안쪽으로만** 물러난다(바깥 한계·필드 경계를 안 넘는다).
///  · 풀과 섞이는 경계: 가장자리 skirt 폭 안에서 알파가 0.12 → 0.3 → 0.6 → 1로 올라가는 반투명 띠 셋(같은 흙 텍스처) — 단계가 W의 10% 안이라 부드럽게 보인다.
/// 
/// 서브메시: 0 흙 본체(불투명) · 1·2·3 skirt 알파 .12/.3/.6 · 4 바퀴 자국. UV는 월드 단위(uvPerUnit — 흙 타일 하나의 세계 크기 역수)라 재질 타일은 1×1이다.
/// </summary>
public static class DirtRoadBuilder
{
    public const float SampleStep = 5f;      // 둘레 표본 간격(가장자리 지글지글 파장 ~23을 풀어낼 만큼)
    public const float Wobble = 0.12f;        // 폭 W 대비 가장자리 출렁임 최대(사장님 안 ±10~15%)
    public const float Jitter = 0.018f;       // 가장자리 지글지글(W 대비)
    public const float SkirtRatio = 0.10f;    // 풀과 섞이는 띠 한쪽 폭(W 대비)
    const int Rows = 8;                       // 폭 방향 줄: 바깥 skirt 3칸 · 본체 · 안쪽 skirt 3칸 → 줄 8개

    /// <summary>모서리 반경(가운데 선 기준) = 비율 × 띠 폭. 시안 3개: 0.5(작게) · 1.0(중간) · 1.6(크게).</summary>
    public static readonly float[] CornerRatios = { 0.5f, 1.0f, 1.6f };

    public static Mesh Build(Rect outer, Rect inner, float width, float cornerRatio, int seed, float uvPerUnit)
    {
        float centerR = cornerRatio * width;
        float spanMin = Mathf.Min(outer.width, outer.height);
        float ro = Mathf.Min(centerR + width * 0.5f, spanMin * 0.5f - 1f);
        float ri = Mathf.Min(Mathf.Max(centerR - width * 0.5f, 0.25f * centerR), Mathf.Min(inner.width, inner.height) * 0.5f - 1f);
        ro = Mathf.Max(ro, 0f); ri = Mathf.Max(ri, 0f);

        // 표본(위에서 보아 반시계): 남(서→동) · 남동 모서리 · 동(남→북) · 북동 · 북(동→서) · 북서 · 서(북→남) · 남서
        int nx = Mathf.Max(1, Mathf.CeilToInt((outer.width - 2f * ro) / SampleStep));
        int nz = Mathf.Max(1, Mathf.CeilToInt((outer.height - 2f * ro) / SampleStep));
        int nc = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * 0.5f * ro / SampleStep), 4, 32);
        var samples = new List<(int side, float f)>();
        void Side(int side, int n) { for (int k = 0; k < n; k++) samples.Add((side, k / (float)n)); }
        void Corner(int c) { for (int k = 0; k < nc; k++) samples.Add((4 + c, k / (float)nc)); }
        Side(0, nx); Corner(0); Side(1, nz); Corner(1); Side(2, nx); Corner(2); Side(3, nz); Corner(3);
        int count = samples.Count;

        Vector2 Point(Rect r, float radius, int side, float f)
        {
            float x0 = r.xMin, x1 = r.xMax, z0 = r.yMin, z1 = r.yMax;
            switch (side)
            {
                case 0: return new Vector2(Mathf.Lerp(x0 + radius, x1 - radius, f), z0);
                case 1: return new Vector2(x1, Mathf.Lerp(z0 + radius, z1 - radius, f));
                case 2: return new Vector2(Mathf.Lerp(x1 - radius, x0 + radius, f), z1);
                case 3: return new Vector2(x0, Mathf.Lerp(z1 - radius, z0 + radius, f));
                default:
                    int c = side - 4;
                    Vector2 center = c switch
                    {
                        0 => new Vector2(x1 - radius, z0 + radius), 1 => new Vector2(x1 - radius, z1 - radius),
                        2 => new Vector2(x0 + radius, z1 - radius), _ => new Vector2(x0 + radius, z0 + radius),
                    };
                    float a = (-90f + c * 90f + f * 90f) * Mathf.Deg2Rad;
                    return center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
        }

        var po = new Vector2[count]; var pi = new Vector2[count];
        for (int k = 0; k < count; k++) { po[k] = Point(outer, ro, samples[k].side, samples[k].f); pi[k] = Point(inner, ri, samples[k].side, samples[k].f); }

        // 바깥 가장자리 호 길이(UV u·잡음). 한 바퀴 끝에서 정확히 이어지게 u는 정수 타일 수로 맞춘다.
        var arc = new float[count + 1];
        for (int k = 1; k <= count; k++) arc[k] = arc[k - 1] + Vector2.Distance(po[k - 1], po[k % count]) + 0.01f;
        float total = arc[count];
        float tiles = Mathf.Max(1f, Mathf.Round(total * uvPerUnit));
        float seedF = 100f + (Mathf.Abs(seed) % 997) * 0.37f;
        float Wave(float k, float wavelength, int salt)
        {
            int cycles = Mathf.Max(1, Mathf.RoundToInt(total / wavelength));
            return Mathf.Sin(cycles * (arc[Mathf.Clamp(Mathf.RoundToInt(k), 0, count)] / total) * Mathf.PI * 2f + seedF * (1.7f + salt * 0.9f));
        }

        var verts = new List<Vector3>(); var uvs = new List<Vector2>();
        float y = MapLayout.IslandTop + 0.08f;
        var rowVertex = new int[count, Rows];
        var rutDir = new Vector2[count]; var aoArr = new float[count]; var weffArr = new float[count];
        for (int k = 0; k < count; k++)
        {
            Vector2 d = pi[k] - po[k];
            float w = d.magnitude;
            Vector2 dir = w > 1e-4f ? d / w : Vector2.up;
            // 양쪽 가장자리가 띠 안쪽으로 물러난다(0 ~ Wobble×W) — 바깥 한계·필드 경계를 못 넘는다.
            float no = 0.5f + 0.5f * (0.7f * Wave(k, 260f, 0) + 0.2f * Wave(k, 110f, 1) + 0.1f * Wave(k, 47f, 2));
            float ni = 0.5f + 0.5f * (0.7f * Wave(k, 230f, 4) + 0.2f * Wave(k, 97f, 5) + 0.1f * Wave(k, 41f, 6));
            float ao = w * (Wobble * no + Jitter * (0.5f + 0.5f * Wave(k, 23f, 3)));
            float ai = w * (Wobble * ni + Jitter * (0.5f + 0.5f * Wave(k, 19f, 7)));
            float weff = Mathf.Max(w - ao - ai, w * 0.4f);
            float s = Mathf.Min(SkirtRatio * w, weff * 0.24f);
            float[] ds = { 0f, s / 3f, 2f * s / 3f, s, weff - s, weff - 2f * s / 3f, weff - s / 3f, weff };
            for (int j = 0; j < Rows; j++)
            {
                Vector2 p = po[k] + dir * (ao + ds[j]);
                rowVertex[k, j] = verts.Count;
                verts.Add(new Vector3(p.x, y, p.y));
                uvs.Add(new Vector2(arc[k] / total * tiles, (ao + ds[j]) * uvPerUnit));
            }
            rutDir[k] = dir; aoArr[k] = ao; weffArr[k] = weff;
        }

        var core = new List<int>(); var skirtA = new List<int>(); var skirtB = new List<int>(); var skirtC = new List<int>();
        void Quad(List<int> tri, int a, int b, int c, int d) { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(d); }
        for (int k = 0; k < count; k++)
        {
            int k2 = (k + 1) % count;
            for (int j = 0; j < Rows - 1; j++)
            {
                List<int> target = j == 3 ? core : (j == 0 || j == 6) ? skirtA : (j == 1 || j == 5) ? skirtB : skirtC;
                Quad(target, rowVertex[k, j], rowVertex[k, j + 1], rowVertex[k2, j + 1], rowVertex[k2, j]);
            }
        }

        // 바퀴 자국: 본체 안 두 줄(가운데에서 ±16%·구불). 별도 정점.
        var rut = new List<int>();
        for (int side = -1; side <= 1; side += 2)
        {
            int first = verts.Count;
            for (int k = 0; k < count; k++)
            {
                float ao = aoArr[k], weff = weffArr[k];
                float wiggle = 0.03f * weff * Wave(k, 90f, side > 0 ? 8 : 9);
                float center = ao + weff * (0.5f + side * 0.16f) + wiggle;
                float hw = 0.022f * weff;
                for (int e = -1; e <= 1; e += 2)
                {
                    Vector2 p = po[k] + rutDir[k] * (center + e * hw);
                    verts.Add(new Vector3(p.x, y + 0.004f, p.y));
                    uvs.Add(new Vector2(arc[k] / total * tiles, (e > 0 ? 1f : 0f)));
                }
            }
            for (int k = 0; k < count; k++)
            {
                int k2 = (k + 1) % count;
                Quad(rut, first + k * 2, first + k * 2 + 1, first + k2 * 2 + 1, first + k2 * 2);
            }
        }

        // 앞면이 위를 보게(첫 삼각형 법선 y로 확인 — Unity는 시계 방향이 앞면).
        void FaceUp(List<int> tri)
        {
            if (tri.Count < 3) return;
            Vector3 n = Vector3.Cross(verts[tri[1]] - verts[tri[0]], verts[tri[2]] - verts[tri[0]]);
            if (n.y < 0f) for (int i = 0; i < tri.Count; i += 3) { int t = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = t; }
        }
        FaceUp(core); FaceUp(skirtA); FaceUp(skirtB); FaceUp(skirtC); FaceUp(rut);

        var mesh = new Mesh { name = "흙길", indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 5;
        mesh.SetTriangles(core, 0); mesh.SetTriangles(skirtA, 1); mesh.SetTriangles(skirtB, 2); mesh.SetTriangles(skirtC, 3); mesh.SetTriangles(rut, 4);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>메시를 Assets/Art/MapMeshes/{레인}_흙길.asset으로 저장(씬이 참조 — 다시 지어도 같은 파일을 덮는다).</summary>
    public static Mesh Save(Mesh fresh, string laneName)
    {
        const string folder = "Assets/Art/MapMeshes";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Art", "MapMeshes");
        string path = $"{folder}/{laneName}_흙길.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(fresh, path); return fresh; }
        existing.Clear();
        EditorUtility.CopySerialized(fresh, existing);
        Object.DestroyImmediate(fresh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // 재질 5개 — 본체(불투명)·skirt 셋(알파 .12/.3/.6 반투명)·바퀴 자국(어두운 반투명). 텍스처는 맵 흙(dirt)과 같다. 타일은 1×1(UV가 월드 단위).
    public static Material[] Materials(Material dirtBase)
    {
        const string folder = "Assets/Materials/Map";
        Material Make(string name, float alpha, bool transparent, Color tint, bool useTexture)
        {
            string path = $"{folder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) { m = new Material(dirtBase); AssetDatabase.CreateAsset(m, path); }
            m.SetTextureScale("_BaseMap", Vector2.one); m.SetTextureScale("_BumpMap", Vector2.one);
            m.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, alpha));
            if (!useTexture) { m.SetTexture("_BaseMap", null); m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            if (transparent)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f); m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(m);
            return m;
        }
        var result = new[]
        {
            Make("dirtroad_core", 1f, false, Color.white, true),
            Make("dirtroad_skirt_a", 0.12f, true, Color.white, true),
            Make("dirtroad_skirt_b", 0.30f, true, Color.white, true),
            Make("dirtroad_skirt_c", 0.60f, true, Color.white, true),
            Make("dirtroad_rut", 0.20f, true, new Color(0.18f, 0.11f, 0.06f), false),
        };
        AssetDatabase.SaveAssets();
        return result;
    }
}
