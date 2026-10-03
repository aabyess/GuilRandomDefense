using UnityEditor;
using UnityEngine;

// Assets/Resources/UI/Skin/ 아래 PNG를 스프라이트로 들여오고, 이름 끝이 「_9s」인 것은 9-slice 테두리를 건다(UiSkin이 읽는다).
// 테두리 값은 gen_ui_skin.py가 그린 그림 기준(픽셀) — 정품으로 바꿔 끼울 땐 같은 이름으로 두고 여기 값만 맞춘다.
public class UiSkinPostprocessor : AssetPostprocessor
{
    const string Folder = "Assets/Resources/UI/Skin/";

    static int BorderFor(string fileName)
    {
        switch (fileName)
        {
            case "console_cell_frame_9s": return 20;
            case "dialog_panel_9s": return 16;
            case "multiboard_frame_9s": return 4;
            case "timer_frame_9s": return 4;
            case "button_navy_9s": return 4;
            case "button_navy_hover_9s": return 4;
            case "topbar_button_9s": return 4;
            case "topbar_resource_9s": return 4;
            default: return 0;
        }
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        int border = BorderFor(name);
        importer.spriteBorder = new Vector4(border, border, border, border);
    }
}
