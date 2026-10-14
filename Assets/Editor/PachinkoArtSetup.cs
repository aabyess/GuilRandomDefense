using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 파칭코 오버레이 그림·폰트 임포트 규칙 + 일본어 TMP 폰트 에셋(Dela Gothic One, OFL, 동적 아틀라스).
/// Assets/Resources/Pachinko/ 아래 PNG는 UI 스프라이트(밉맵 없음)로 임포트한다. 메뉴: Tools/UI/파칭코 폰트 에셋 만들기 · call PachinkoArtSetup.MakeFont
/// </summary>
class PachinkoArtPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Pachinko/") || !assetPath.EndsWith(".png")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.maxTextureSize = 2048;
        ti.textureCompression = TextureImporterCompression.Compressed;
    }
}

static class PachinkoArtSetup
{
    const string FontAssetPath = "Assets/Resources/Pachinko/DelaGothicOne SDF.asset";
    const string FontFile = "Assets/Resources/Pachinko/Fonts/DelaGothicOne-Regular.ttf";

    [MenuItem("Tools/UI/파칭코 폰트 에셋 만들기")]
    static void Menu() => Debug.Log(MakeFont());

    public static string MakeFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontFile);
        if (font == null) return "❌ 폰트 파일 없음: " + FontFile;
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null) return "이미 있음: " + FontAssetPath;
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = "DelaGothicOne SDF";
        AssetDatabase.CreateAsset(fa, FontAssetPath);
        if (fa.material != null) { fa.material.name = "DelaGothicOne SDF Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
        if (fa.atlasTextures != null) foreach (Texture2D t in fa.atlasTextures) if (t != null) { t.name = "DelaGothicOne SDF Atlas"; AssetDatabase.AddObjectToAsset(t, fa); }
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return "✅ " + FontAssetPath;
    }

    /// <summary>플레이 모드 없이 오버레이 레이아웃 확인(빠른 점검): 필요한 스프라이트가 전부 스프라이트로 로드되는지.</summary>
    public static string CheckSprites()
    {
        var sb = new System.Text.StringBuilder();
        string[] names = { "machine_frame", "UI/push_button_normal", "UI/push_button_pressed", "UI/keycap_space", "UI/reward_band",
            "Logos/logo_reach", "Logos/logo_keizoku", "Logos/logo_gekiatsu", "Logos/logo_fukkatsu", "Logos/logo_ooatari", "Logos/logo_atari", "Logos/logo_zannen" };
        foreach (string n in names) sb.AppendLine($"{(Resources.Load<Sprite>("Pachinko/" + n) != null ? "✅" : "❌")} {n}");
        for (int i = 1; i <= 9; i++) sb.AppendLine($"{(Resources.Load<Sprite>($"Pachinko/Symbols/symbol_{i}") != null ? "✅" : "❌")} symbol_{i} / blur {(Resources.Load<Sprite>($"Pachinko/Symbols/symbol_{i}_blur") != null ? "✅" : "❌")}");
        return sb.ToString();
    }
}
