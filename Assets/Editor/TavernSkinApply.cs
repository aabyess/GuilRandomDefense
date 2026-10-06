using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 선술집 나무 톤 UI 그림(blender 10-06, ~/GRD_tavern_ui · 정본 Tools/blender/gen_tavern_ui.py)을 Assets/Resources/UI/SkinTavern/에 복사하고
/// 9-slice 테두리(README 표)를 스프라이트 .meta에 건다. 호출: call TavernSkinApply.Apply (다시 불러도 안전).
/// 옛 돌 그림(Resources/UI/Skin)은 그대로 둔다 — UiSkin.Theme로 되돌릴 수 있다.
/// </summary>
static class TavernSkinApply
{
    const string Source = "~/GRD_tavern_ui";
    const string Dest = "Assets/Resources/UI/SkinTavern";

    // 파일 이름(확장자 없이) → 9-slice 테두리 (좌, 아래, 우, 위) — README는 (좌,위,우,아래) 순서라 아래·위를 바꿔 적는다.
    static readonly (string name, Vector4 border)[] Files =
    {
        ("bar_bottom_9slice",   new Vector4(0, 8, 0, 44)),
        ("bar_bottom_tile512",  Vector4.zero),
        ("cell_big_9slice",     new Vector4(40, 40, 40, 40)),
        ("cell_small_9slice",   new Vector4(18, 18, 18, 18)),
        ("btn_normal_9slice",   new Vector4(24, 24, 24, 24)),
        ("btn_hover_9slice",    new Vector4(24, 24, 24, 24)),
        ("btn_pressed_9slice",  new Vector4(24, 24, 24, 24)),
        ("drawer_9slice",       new Vector4(112, 112, 112, 112)),
        ("drawer_nameplate",    Vector4.zero),
        ("chip_normal_9slice",  new Vector4(12, 12, 12, 12)),
        ("chip_selected_9slice", new Vector4(12, 12, 12, 12)),
        ("row_parchment_9slice", new Vector4(24, 24, 24, 24)),
        ("resource_plate_9slice", new Vector4(18, 18, 18, 18)),
    };

    static string Apply()
    {
        string src = Source.Replace("~", System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));
        if (!Directory.Exists(src)) return $"❌ 원본 폴더 없음: {src}";
        Directory.CreateDirectory(Dest);
        int copied = 0, bordered = 0;
        foreach (var (name, border) in Files)
        {
            string from = Path.Combine(src, name + ".png"), to = $"{Dest}/{name}.png";
            if (!File.Exists(from)) return $"❌ 파일 없음: {from}";
            File.Copy(from, to, true);
            copied++;
        }
        AssetDatabase.Refresh();
        foreach (var (name, border) in Files)
        {
            string path = $"{Dest}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return $"❌ 임포터 없음: {path}";
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;   // 가는 리벳·그을림이 뭉개지지 않게
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            if (border != Vector4.zero) bordered++;
        }
        return $"선술집 그림 {copied}장 복사 · 9-slice 테두리 {bordered}장 · 위치 {Dest}";
    }
}
