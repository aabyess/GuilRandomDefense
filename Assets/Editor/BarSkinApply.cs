using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 하단 UI 시안 그림(Tools/ui/gen_bar_variants.py 산출 a_/b_/c_ bar·edge·cell)을 Resources/UI/SkinBar/에 임포트 설정한다(그림은 스크립트가 이 폴더에 직접 쓴다).
/// 호출: call BarSkinApply.Apply (다시 불러도 안전). 사장님 10-07 선택 = C 금속(c_*) — 시안 A·B는 gen_bar_variants.py로 다시 뽑을 수 있다.
/// </summary>
static class BarSkinApply
{
    const string Dir = "Assets/Resources/UI/SkinBar";

    static string Apply()
    {
        if (!Directory.Exists(Dir)) return $"❌ 폴더 없음: {Dir} (먼저 /usr/bin/python3 Tools/ui/gen_bar_variants.py)";
        AssetDatabase.Refresh();
        int n = 0;
        foreach (string path in Directory.GetFiles(Dir, "c_*.png"))
        {
            string asset = path.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
            if (importer == null) continue;
            bool cell = asset.EndsWith("_cell.png");
            bool edge = asset.EndsWith("_edge.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = cell ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;   // 타일로 까는 그림(bar·edge)은 반복
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            float cellBorder = asset.Contains("/b_") ? 12f : 10f;   // 그림 쪽 테두리(gen_bar_variants.py: A·C 10px, B 12px)
            importer.spriteBorder = cell ? new Vector4(cellBorder, cellBorder, cellBorder, cellBorder) : Vector4.zero;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            n++;
        }
        return $"하단 바 그림 {n}장 임포트";
    }
}
