using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 맵 생성기가 만든 바다 위에 물결 수면을 덮는다(GuilRandomDefense/SeaWater 셰이더).
///
/// 기존 바다 오브젝트는 **지우지 않는다** — 그 콜라이더가 NavMesh의 Sea 영역(3번)을 굽는다. 보이는 것만 끄고,
/// 그 윗면 높이에 촘촘한 격자 수면을 따로 세운다. 격자가 필요한 이유는 너울이 정점을 움직이기 때문이다
/// (유니티 Plane은 10×10칸이라 1600 바다에선 칸 하나가 160이 되어 너울이 안 보인다).
///
/// 격자 수면에는 콜라이더를 달지 않는다 — 클릭·NavMesh는 기존 바다가 그대로 맡는다.
/// </summary>
public static class SeaWaterBuilder
{
    const string Folder = "Assets/Art/Sea";
    const string MaterialPath = Folder + "/SeaWater.mat";
    const string MeshPath = Folder + "/SeaGrid.asset";
    const string ShaderName = "GuilRandomDefense/SeaWater";

    // 칸 8 — 가장 짧은 너울(파장 33)이 네 칸에 걸쳐야 매끈하다. 1600 바다면 200×200칸, 정점 40,401개(16비트 인덱스 안).
    const float CellSize = 8f;

    public static string Apply(Transform parent, GameObject sea)
    {
        if (sea == null) return "\n⚠️ 바다 물결: 바다 오브젝트를 못 찾았습니다.";

        Shader shader = Shader.Find(ShaderName);
        if (shader == null) return $"\n⚠️ 바다 물결: 셰이더 {ShaderName}를 못 찾았습니다 — 유니티가 컴파일을 마친 뒤 다시 생성하세요.";

        Renderer seaRenderer = sea.GetComponentInChildren<Renderer>();
        if (seaRenderer == null) return "\n⚠️ 바다 물결: 바다에 렌더러가 없습니다.";

        Bounds bounds = seaRenderer.bounds;
        EnsureFolder();

        Mesh mesh = BuildGrid(bounds.size.x, bounds.size.z);
        AssetDatabase.DeleteAsset(MeshPath);
        AssetDatabase.CreateAsset(mesh, MeshPath);

        // 재질은 이미 있으면 그대로 쓴다 — 사장님이 인스펙터에서 색·세기를 만졌으면 그 값을 지켜야 한다.
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "SeaWater" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        GameObject surface = new GameObject("바다_물결");
        surface.transform.SetParent(parent, false);
        surface.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        surface.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;   // 수면이 섬에 그림자를 드리우면 안 된다

        // 보이는 것만 끈다 — 콜라이더(NavMesh Sea 영역)는 살린다.
        foreach (Renderer old in sea.GetComponentsInChildren<Renderer>()) old.enabled = false;

        return $"\n바다 물결: {bounds.size.x:0}×{bounds.size.z:0} 격자 수면(칸 {CellSize}), 높이 {bounds.max.y:0.##}.";
    }

    static Mesh BuildGrid(float sizeX, float sizeZ)
    {
        int cellsX = Mathf.Max(1, Mathf.CeilToInt(sizeX / CellSize));
        int cellsZ = Mathf.Max(1, Mathf.CeilToInt(sizeZ / CellSize));

        Vector3[] vertices = new Vector3[(cellsX + 1) * (cellsZ + 1)];
        for (int z = 0; z <= cellsZ; z++)
        for (int x = 0; x <= cellsX; x++)
            vertices[z * (cellsX + 1) + x] = new Vector3(-sizeX * 0.5f + sizeX * x / cellsX, 0f, -sizeZ * 0.5f + sizeZ * z / cellsZ);

        int[] triangles = new int[cellsX * cellsZ * 6];
        int t = 0;
        for (int z = 0; z < cellsZ; z++)
        for (int x = 0; x < cellsX; x++)
        {
            int i = z * (cellsX + 1) + x;
            triangles[t++] = i;
            triangles[t++] = i + cellsX + 1;
            triangles[t++] = i + 1;
            triangles[t++] = i + 1;
            triangles[t++] = i + cellsX + 1;
            triangles[t++] = i + cellsX + 2;
        }

        Mesh mesh = new Mesh { name = "SeaGrid" };
        mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        // 너울이 정점을 최대 ±0.5 움직인다 — 경계를 넉넉히 잡아야 가장자리에서 컬링으로 사라지지 않는다.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(sizeX + 4f, 4f, sizeZ + 4f));
        return mesh;
    }

    static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(Folder)) return;
        AssetDatabase.CreateFolder("Assets/Art", "Sea");
    }
}
