using System;
using System.Linq;
using UnityEditor;

/// <summary>
/// Blender로 지은 괴물·짐승·소품 FBX(Assets/Art/Monsters·Creatures·Props)의 클립 반복을 맞춘다.
///
/// 왜 필요한가 — FBX 기본 임포트는 클립의 Loop Time을 끈다. 숨쉬기(Idle_Breath, 8초)가 한 번 돌고 멈춰
/// 짐승이 굳는다. Blender 쪽이 첫 프레임 = 끝 프레임으로 지어 두었으니 반복만 켜면 이음매 없이 돈다.
///
/// Blender 쪽(Tools/blender/gen_*.py)과의 약속: 반복할 클립은 이름에 `Idle`이 들어간다.
/// 보물상자 `Open`처럼 한 번 열리고 끝 자세를 지키는 클립은 반복하지 않는다.
/// </summary>
public class BlenderClipPostprocessor : AssetPostprocessor
{
    static readonly string[] Roots = { "Assets/Art/Monsters/", "Assets/Art/Creatures/", "Assets/Art/Props/" };

    // 규칙을 바꾸면 올린다 — 올려야 이미 임포트된 FBX도 다시 돈다.
    public override uint GetVersion() => 1;

    void OnPreprocessAnimation()
    {
        // 🔴 macOS에서 유니티가 넘기는 한글 경로는 NFC가 아닐 수 있다(UnitModelPostprocessor 참고).
        string path = assetPath.Normalize(System.Text.NormalizationForm.FormC);
        if (!Roots.Any(root => path.StartsWith(root, StringComparison.Ordinal))) return;

        if (!(assetImporter is ModelImporter importer)) return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        foreach (ModelImporterClipAnimation clip in clips)
            clip.loopTime = clip.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0;

        importer.clipAnimations = clips;
    }
}
