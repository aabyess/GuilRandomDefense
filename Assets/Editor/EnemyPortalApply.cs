using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// 적 출발점 바닥 포탈(사장님 10-07) — blender 납품 enemy_portal.fbx(돌판 + 문양)로 프리팹을 만들고, 4개 레인 출발점(MapLayout.LaneLoop[0])에 장식으로 놓는다.
/// 부르기: call EnemyPortalApply.Info (오브젝트 이름 확인) · call EnemyPortalApply.MakePrefab · call EnemyPortalApply.Place (씬에 4개, 다시 불러도 안전)
/// 맵 재생성 때는 MapGenerator.BuildEnemyPortals가 같은 함수(PlaceAll)를 부른다.
/// </summary>
public static class EnemyPortalApply
{
    const string Folder = "Assets/Art/EnemyPortal";
    const string PrefabPath = Folder + "/EnemyPortal.prefab";
    public const float Diameter = 56f;   // 흙길 폭(≈70) 안에 들어가고 보스도 서는 크기
    const string ContainerName = "적출발_포탈";

    static string Info()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/enemy_portal.fbx");
        if (fbx == null) return "❌ fbx 없음";
        var sb = new StringBuilder("enemy_portal.fbx 자식:");
        foreach (Renderer r in fbx.GetComponentsInChildren<Renderer>(true)) sb.Append($"\n   {r.name} · 경계 {r.bounds.size:F3} 중심 {r.bounds.center:F3} · 재질 {r.sharedMaterials.Length}");
        return sb.ToString();
    }

    static Material MakeMaterial(string path, Material source, Texture2D tex)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path); mat = AssetDatabase.LoadAssetAtPath<Material>(path); }
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", Vector2.one);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static string MakePrefab()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/enemy_portal.fbx");
        var stoneTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/portal_stone.png");
        var glyphTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/portal_glyph.png");
        if (fbx == null || stoneTex == null || glyphTex == null) return "❌ fbx·텍스처 없음";
        // 돌: URP Lit 한 장(타일 돌), 문양: 기존 Additive 파티클 머티리얼 복사(알파 PNG)
        var addSource = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Effects/Sphere/Materials/!flare35_!flare35_g0_L0_add.mat");
        Material glyphMat = MakeMaterial(Folder + "/EnemyPortalGlyph.mat", addSource, glyphTex);
        var stoneMat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/EnemyPortalStone.mat");
        if (stoneMat == null)
        {
            stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(stoneMat, Folder + "/EnemyPortalStone.mat");
        }
        stoneMat.SetTexture("_BaseMap", stoneTex);
        stoneMat.SetColor("_BaseColor", new Color(0.55f, 0.56f, 0.62f, 1f));   // 문양 빛이 돋보이게 돌을 어둡게
        stoneMat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(stoneMat);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "EnemyPortal";
        int glyphs = 0;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
        {
            bool isGlyph = r.name.Contains("문양") || r.name.ToLower().Contains("glyph");
            r.sharedMaterials = r.sharedMaterials.Select(_ => isGlyph ? glyphMat : stoneMat).ToArray();
            r.shadowCastingMode = isGlyph ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            if (isGlyph) { if (r.GetComponent<EnemyPortalSpin>() == null) r.gameObject.AddComponent<EnemyPortalSpin>(); glyphs++; }
            foreach (Collider c in r.GetComponents<Collider>()) Object.DestroyImmediate(c);
        }
        var mod = instance.AddComponent<NavMeshModifier>();
        mod.ignoreFromBuild = true;
        mod.applyToChildren = true;
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        return $"프리팹 {PrefabPath} · 문양 {glyphs}개에 회전·깜빡임 · 돌 {stoneMat.name} · 문양 {glyphMat.name}(Additive)";
    }

    /// <summary>레인마다 출발점 한 개. MapGenerator(재생성)와 Place(지금 씬)가 같이 쓴다. 이미 있으면 지우고 다시 놓는다.</summary>
    public static int PlaceAll(Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return 0;
        foreach (GameObject old in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(g => g.name.StartsWith(ContainerName + "_")).ToArray())
            Object.DestroyImmediate(old);
        int placed = 0;
        for (int i = 0; i < MapLayout.Lanes.Length; i++)
        {
            Vector3 start = MapLayout.LaneLoop(MapLayout.Lanes[i])[0];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"{ContainerName}_{i + 1}";
            go.transform.position = new Vector3(start.x, MapLayout.IslandTop, start.z);
            go.transform.localScale = Vector3.one * Diameter;
            placed++;
        }
        return placed;
    }

    static string Place()
    {
        Transform parent = GameObject.Find("레인간_세로벽")?.transform.parent;
        if (parent == null) return "❌ 맵 부모를 못 찾음";
        int n = PlaceAll(parent);
        EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        EditorSceneManager.SaveScene(parent.gameObject.scene);
        return $"적 출발점 포탈 {n}개를 놓음(지름 {Diameter}) · 씬 저장";
    }
}
