using UnityEditor;
using UnityEngine;

// Assets/Resources/Cutin 아래 텍스처(Tools/cutin/import_cutin.py가 만든 컷인 레이어)는 UI용 투명 PNG다 —
// 밉맵·읽기 끔, 압축은 Crunch(빌드 용량을 줄이려고, PM 10-08 「Crunch로 가고 50MB 안쪽 실측」).
public class CutinTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Cutin/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = true;
        importer.compressionQuality = 60;
    }
}
