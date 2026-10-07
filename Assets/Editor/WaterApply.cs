using UnityEditor;
using UnityEngine;

/// <summary>
/// 바다 물 색 원작 톤(사장님 10-07, blender GRD_water) — SeaWater.mat(실제로 보이는 물, GuilRandomDefense/SeaWater 셰이더)에 원작 톤 색·무늬를 걸고,
/// sea.mat·sea_128x128.mat(Lit, 옛 바다 — 렌더러는 꺼져 있고 NavMesh용)도 spec대로 맞춘다. 부르기: call WaterApply.Apply [SetColors 인자는 코드 상수]
/// </summary>
static class WaterApply
{
    const string Folder = "Assets/Art/Sea";

    static void ImportAs(string path, bool normalMap, bool srgb)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return;
        imp.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.sRGBTexture = srgb;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.maxTextureSize = 2048;
        imp.SaveAndReimport();
    }

    public static Color MinimapSea = new Color(0.18f, 0.40f, 0.50f);   // 미니맵 바다(옛 Lit) — 실측으로 맞춘다

    public static string ApplyPublic() => Apply();

    static string Apply()
    {
        ImportAs(Folder + "/water_color_2048.png", false, true);
        ImportAs(Folder + "/water_normal_2048.png", true, false);
        ImportAs(Folder + "/water_shallow_band.png", false, true);
        var color = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/water_color_2048.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/water_normal_2048.png");

        var sea = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/SeaWater.mat");
        sea.SetTexture("_ColorMap", color);
        sea.SetTexture("_WaterBump", normal);
        // 목표 깊은 물 RGB(32,73,93)·얕은 물 띠 물쪽 (62,98,110) — 셰이더가 조명(주변광+태양)을 곱하므로 이득 DeepScale로 나눠 둔다(실화면 평균으로 맞춘다)
        sea.SetColor("_DeepColor", new Color(33.35f / 255f, 88.5f / 255f, 112.5f / 255f, 0.96f)   /* 10-07 사장님 「좀 더 연하고 밝게」 A안(한 단계): 화면 실측 평균 (48,106,126)·미니맵 (48,106,129). 옛 값(17.4,54.9,75) = (35,71,90) */);
        sea.SetColor("_ShallowColor", new Color(59f / 255f, 119.6f / 255f, 137.1f / 255f, 0.80f));
        sea.SetFloat("_Smoothness", 0.88f);   // 이 셰이더에선 낮추면 반짝임이 「넓어져」 미니맵·큰 화면이 하얗게 뜬다(10-07 실측) — 폭은 그대로 두고 세기만 줄인다
        sea.SetFloat("_SpecIntensity", 0.35f);
        sea.SetFloat("_FresnelPower", 6f);
        sea.SetFloat("_ColorMapMix", 0.85f);
        sea.SetFloat("_NormalMapStrength", 0.35f);
        sea.SetFloat("_MapWorldSize", 480f);
        sea.SetVector("_FlowA", new Vector4(0.012f, 0.006f, -0.008f, 0.010f));
        EditorUtility.SetDirty(sea);

        string log = "SeaWater.mat 색·무늬 적용";
        foreach (string m in new[] { "sea", "sea_128x128" })
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Map/{m}.mat");
            if (mat == null) continue;
            // 옛 바다(Lit)는 렌더러가 꺼져 있고 미니맵·NavMesh만 쓴다 — 2048 텍스처를 큰 판에 한 장으로 깔면 회색으로 뭉개져(10-07 실측 평균 (123,124,126)) 원작 색 한 톤으로 평평하게 둔다.
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_BumpMap", null);
            mat.SetColor("_BaseColor", new Color(MinimapSea.r, MinimapSea.g, MinimapSea.b, 1f));
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            log += $" · {m}.mat";
        }
        AssetDatabase.SaveAssets();
        return log;
    }
}
