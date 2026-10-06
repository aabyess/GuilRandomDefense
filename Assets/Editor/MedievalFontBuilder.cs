using TMPro;
using UnityEditor;
using UnityEngine;

// 제목 「G.R.D」 중세 글씨(사장님 10-06 시안 D) — OFL 무료 폰트 둘을 TMP 동적 글꼴 에셋으로 굽는다.
//   UnifrakturMaguntia(고딕 흘림체) = 제목 · MedievalSharp(중세 손글씨) = 부제 「Guil Random Defense」.
//   원본 TTF·OFL 라이선스는 Assets/Art/Fonts/Medieval. 결과는 Resources/Fonts/<이름> SDF.asset(실행 중 Resources.Load).
//   동적(Dynamic) 에셋이라 쓰는 글자가 처음 나올 때 아틀라스에 들어간다 — 영문 몇 자뿐이라 미리 굽지 않는다.
public static class MedievalFontBuilder
{
    [MenuItem("Tools/UI/중세 제목 글꼴 만들기")]
    static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        return Make("Assets/Art/Fonts/Medieval/UnifrakturMaguntia-Book.ttf", "Assets/Resources/Fonts/UnifrakturMaguntia SDF.asset") + "\n"
             + Make("Assets/Art/Fonts/Medieval/MedievalSharp.ttf", "Assets/Resources/Fonts/MedievalSharp SDF.asset");
    }

    static string Make(string ttfPath, string outPath)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null) return "❌ TTF 없음: " + ttfPath;
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null) AssetDatabase.DeleteAsset(outPath);
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (asset == null) return "❌ 글꼴 에셋 생성 실패: " + ttfPath;
        asset.name = System.IO.Path.GetFileNameWithoutExtension(outPath);
        AssetDatabase.CreateAsset(asset, outPath);
        // 아틀라스·재질은 하위 에셋으로 같이 저장해야 빌드에 실린다.
        foreach (Texture2D tex in asset.atlasTextures)
            if (tex != null) { tex.name = asset.name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, asset); }
        if (asset.material != null) { asset.material.name = asset.name + " Material"; AssetDatabase.AddObjectToAsset(asset.material, asset); }
        // 제목 글자를 미리 넣어 둔다(첫 화면에서 바로 그려지게).
        asset.TryAddCharacters("G.RDuilRandomDefense ·");
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return "✅ " + outPath;
    }
}
