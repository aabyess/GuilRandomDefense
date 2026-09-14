using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 유닛 스킨 텍스처(Assets/Art/Units·Characters) 중 노멀맵을 Normal Map 타입으로 읽는다.
///
/// 왜 — 스킨 FBX는 재질을 FBX 안의 설명대로 만든다(materialImportMode 2). FBX가 노멀맵 칸에 텍스처를
/// 물려 두면 유니티는 그 텍스처를 **Default(sRGB 켬)** 그대로 _BumpMap에 꽂는다. 법선이 틀려 조명이
/// 얼룩덜룩해진다(2026-09-14 안흔함_강주혁 코알라 인형이 밝고 어두운 반점투성이).
/// 같은 상태였던 것: 김용태 Body/Hair_Normal · 이재윤 NormalMap1~3 · 황정기 173_Norm · 흔함_강재규 *_Normal_DirectX.
///
/// 이름 규칙: 파일명(확장자 뺀)에서 normal·norm·nrm(+map·숫자)이 단어로 서 있을 때만.
/// 🔴 `abnormal_pattern`(원피스 계열 명암 텍스처)은 단어 안에 든 것이라 걸리면 안 된다.
/// DirectX 규약 노멀맵(이름에 directx)은 초록 채널이 반대라 뒤집는다 — 유니티는 OpenGL 규약.
/// </summary>
public class UnitTextureImporter : AssetPostprocessor
{
    static readonly string[] Roots = { "Assets/Art/Units/", "Assets/Art/Characters/" };
    static readonly Regex NormalName = new Regex(@"(^|[_\-\s.])(normal|norm|nrm)(map)?\d*([_\-\s.]|$)", RegexOptions.IgnoreCase);

    void OnPreprocessTexture()
    {
        if (!IsUnitNormalMap(assetPath)) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.NormalMap;
        importer.sRGBTexture = false;
        importer.flipGreenChannel = assetPath.ToLowerInvariant().Contains("directx");
    }

    public static bool IsUnitNormalMap(string path)
    {
        // 한글 경로는 macOS에서 NFC가 아니다(UnitModelPostprocessor 참고) — 영문 접두만 보므로 비교는 안전하지만 이름은 맞춘다.
        string nfc = path.Normalize(System.Text.NormalizationForm.FormC);
        if (!Roots.Any(r => nfc.StartsWith(r))) return false;
        string stem = System.IO.Path.GetFileNameWithoutExtension(nfc);
        return NormalName.IsMatch(stem);
    }

    // 이미 들어와 있는 노멀맵은 전처리가 다시 안 돌므로 이것으로 다시 읽힌다(브리지: call UnitTextureImporter.ReimportNormalMaps).
    public static string ReimportNormalMaps()
    {
        string[] paths = AssetDatabase.FindAssets("t:Texture2D", Roots.Select(r => r.TrimEnd('/')).ToArray())
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsUnitNormalMap)
            .ToArray();
        foreach (string p in paths)
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        return $"노멀맵 {paths.Length}개 다시 읽음: " + string.Join(", ", paths.Select(System.IO.Path.GetFileName));
    }
}
