using UnityEditor;

/// <summary>
/// Assets/Textures/Map 안의 텍스처 임포트 설정을 고정한다.
/// 노멀맵은 Normal Map 타입으로 읽어야 조명이 맞는다 — 기본값(Default)으로 들어오면
/// 파란 이미지를 그대로 색으로 칠해버린다. 사람이 매번 인스펙터에서 바꾸는 건 잊기 쉽다.
/// </summary>
public class MapTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Textures/Map/")) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.anisoLevel = 4;

        if (assetPath.EndsWith("_normal.png"))
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
        }
    }
}
