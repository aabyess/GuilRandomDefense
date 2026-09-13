using UnityEditor;

/// <summary>
/// 포탈 마법진 텍스처(Assets/Art/Structures/Textures/포탈_마법진_*)의 임포트 설정.
///
/// 문자 띠·아래 고리는 머리카락 같은 선과 작은 글자를 4096에 그렸는데, 유니티 기본 최대 크기(2048)로 반씩 줄어
/// 선이 끊겼다(2026-09-13 inspect 실측: 4096 원본이 2048×2048로 들어옴). 비스듬한 게임 카메라에서 뭉개지지 않게
/// 이방성 필터도 올린다.
/// </summary>
public class MagicCircleTextureImporter : AssetPostprocessor
{
    const string Prefix = "Assets/Art/Structures/Textures/포탈_마법진_";

    public override uint GetVersion() => 1;

    void OnPreprocessTexture()
    {
        // 🔴 macOS에서 유니티가 넘기는 한글 경로는 NFC가 아닐 수 있다(UnitModelPostprocessor 참고).
        string path = assetPath.Normalize(System.Text.NormalizationForm.FormC);
        if (!path.StartsWith(Prefix, System.StringComparison.Ordinal)) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.maxTextureSize = 4096;   // 원본이 2048이면 그대로 2048
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
    }
}
