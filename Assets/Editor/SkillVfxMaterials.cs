using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 스킬 기본 이펙트 재질(2026-09-29) — Kenney Particle Pack(CC0) 텍스처로 URP 파티클 재질을 만든다.
/// 결과는 Assets/Resources/Effects/Skill_*.mat — 실행 중 SkillVfx가 Resources로 읽는다(빌드에 셰이더가 같이 실린다).
/// 부르는 법: 브리지 `call SkillVfxMaterials.Build` 또는 메뉴 Tools/아트/스킬 이펙트 재질.
/// 텍스처는 흰색 그림이라 색은 재질이 아니라 파티클 startColor로 입힌다.
/// </summary>
public static class SkillVfxMaterials
{
    const string TextureFolder = "Assets/Art/Effects/Kenney";
    const string MeshFolder = "Assets/Art/Effects/Meshes";               // blender 세션 메시 5종(42243b67, README.md)
    const string MeshTextureFolder = "Assets/Art/Effects/Meshes/Textures";
    const string OutFolder = "Assets/Resources/Effects";

    // 이름 = SkillVfx가 읽는 재질 이름(Skill_ 뒤). 전부 더해 그리기 — 검은 배경 없이 빛처럼 겹친다.
    static readonly string[] Textures =
        { "star_09", "star_07", "slash_02", "symbol_02", "scratch_01", "circle_02", "circle_03", "magic_01", "trace_06", "twirl_01", "spark_05" };

    // 메시 이펙트 전용 텍스처(09-29 blender 세션이 새로 그림, Kenney와 같은 꼴 — 흰색, 회색값 = 알파). 재질 이름은 똑같이 Skill_<이름>.
    static readonly string[] MeshTextures = { "vfx_wall_fade", "vfx_slash_strip", "vfx_spike" };

    [MenuItem("Tools/아트/스킬 이펙트 재질")]
    public static string Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Effects");

        int made = 0;
        foreach ((string folder, string name) in System.Linq.Enumerable.Concat(
                     System.Linq.Enumerable.Select(Textures, n => (TextureFolder, n)),
                     System.Linq.Enumerable.Select(MeshTextures, n => (MeshTextureFolder, n))))
        {
            string texPath = $"{folder}/{name}.png";
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer == null) return $"❌ 텍스처 없음: {texPath}";
            if (!importer.alphaIsTransparency || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string matPath = $"{OutFolder}/Skill_{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool isNew = material == null;
            if (isNew) material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetTexture("_BaseMap", tex);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);   // Additive
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_BLENDMODE_ADD");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.One);
            if (isNew) AssetDatabase.CreateAsset(material, matPath);
            else { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
            made++;
        }
        AssetDatabase.SaveAssets();
        return $"✅ 스킬 이펙트 재질 {made}개 → {OutFolder}\n" + BuildMeshes();
    }

    // 메시 이펙트(1.2.2): FBX는 Resources 밖(blender 세션 정본 자리)이라 실행 중에 못 읽는다 → 메시만 Resources/Effects/Mesh_<이름>.asset으로 복사.
    //   FBX를 다시 뽑으면 이 메뉴를 다시 돌린다(같은 자리를 덮어쓴다). 결과에 경계를 적는다 — README 확인값:
    //   충격파_원판 y ≈ 0.002(평평) · 번개_기둥 y 0~1(세로)이면 Y-up이 맞다. 아니면 FBX 임포터 Bake Axis Conversion을 켤 것.
    public static string BuildMeshes()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MeshFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Mesh source = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh m) { source = m; break; }
            string name = System.IO.Path.GetFileNameWithoutExtension(path).Replace("VFX_", "");
            if (source == null) { sb.AppendLine($"   ❌ {path}: 메시 없음"); continue; }

            string outPath = $"{OutFolder}/Mesh_{name}.asset";
            Mesh copy = Object.Instantiate(source);
            copy.name = "Mesh_" + name;
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(outPath);
            if (existing == null) AssetDatabase.CreateAsset(copy, outPath);
            else { EditorUtility.CopySerialized(copy, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(copy); }

            Bounds b = source.bounds;
            sb.AppendLine($"   {outPath} · 삼각형 {source.triangles.Length / 3} · 경계 x {b.min.x:F3}~{b.max.x:F3} · y {b.min.y:F3}~{b.max.y:F3} · z {b.min.z:F3}~{b.max.z:F3}");
        }
        AssetDatabase.SaveAssets();
        return sb.Length > 0 ? "   메시:\n" + sb : "   ❌ 메시 FBX 없음";
    }
}
