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
    const string OutFolder = "Assets/Resources/Effects";

    // 이름 = SkillVfx가 읽는 재질 이름(Skill_ 뒤). 전부 더해 그리기 — 검은 배경 없이 빛처럼 겹친다.
    static readonly string[] Textures =
        { "star_09", "star_07", "slash_02", "symbol_02", "scratch_01", "circle_02", "circle_03", "magic_01", "trace_06", "twirl_01", "spark_05" };

    [MenuItem("Tools/아트/스킬 이펙트 재질")]
    public static string Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Effects");

        int made = 0;
        foreach (string name in Textures)
        {
            string texPath = $"{TextureFolder}/{name}.png";
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
        return $"✅ 스킬 이펙트 재질 {made}개 → {OutFolder}";
    }
}
