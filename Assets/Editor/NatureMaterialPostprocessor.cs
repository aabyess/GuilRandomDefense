using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 자연물·벽 FBX(Assets/Art/Nature, Assets/Art/Walls)의 재질에 텍스처를 붙이고, 잎 카드는 양면·알파 컷으로 만든다.
///
/// 왜 필요한가 — 사장님이 나무를 사실적인 「C 스타일」로 확정했다(2026-09-12). C 나무는 잎이 한 장 한 장의
/// 평면(잎 카드)이고 잎 모양이 텍스처의 투명도로 잘려야 보인다. FBX 기본 임포트는 색만 가져오고
/// 투명도·양면을 안 켜서, 잎이 네모난 판으로 보이거나 뒷면에서 사라진다.
///
/// Blender 쪽(Tools/blender/gen_nature.py)과의 약속:
/// - 텍스처는 Assets/Art/Nature/Textures/에 **재질 이름과 같은 파일명**(.png)으로 둔다.
/// - 이름 끝이 `_잎카드`인 재질만 양면 + 알파 컷. 나머지는 불투명.
/// 약속 밖의 재질(텍스처가 없는 저폴리 벽·바위 등)은 건드리지 않는다 — 유니티 기본 임포트 그대로 색만 쓴다.
/// </summary>
public class NatureMaterialPostprocessor : AssetPostprocessor
{
    const string NatureRoot = "Assets/Art/Nature/";
    const string WallRoot = "Assets/Art/Walls/";
    const string MonsterRoot = "Assets/Art/Monsters/";
    // 텍스처는 자기 종류 폴더의 Textures에서 찾는다(자연물·벽은 Nature/Textures, 괴물은 Monsters/Textures).
    static readonly string[] TextureFolders = { "Assets/Art/Nature/Textures", "Assets/Art/Monsters/Textures" };
    const string LeafCardSuffix = "_잎카드";   // 잎 카드·지느러미 막 — 양면 + 알파 컷

    // 규칙을 바꾸면 올린다 — 올려야 이미 임포트된 FBX도 다시 돈다.
    // 1 → 2 (2026-09-12): 거대 해왕류(Assets/Art/Monsters)를 대상에 추가.
    public override uint GetVersion() => 2;

    // URP의 기본 재질 설명 처리(셰이더를 Lit로, 색을 FBX 기본색으로)가 먼저 돈 뒤에 덧붙인다.
    public override int GetPostprocessOrder() => 100;

    // 🔴 macOS에서 유니티가 넘기는 한글 경로·이름은 NFC가 아닐 수 있다(UnitModelPostprocessor 참고).
    static string Nfc(string s) => s?.Normalize(System.Text.NormalizationForm.FormC);

    static bool Applies(string path)
    {
        string nfc = Nfc(path);
        return nfc != null && (nfc.StartsWith(NatureRoot) || nfc.StartsWith(WallRoot) || nfc.StartsWith(MonsterRoot));
    }

    void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] animations)
    {
        if (!Applies(assetPath)) return;

        string materialName = Nfc(description.materialName);
        string texturePath = FindTexturePath(materialName);
        if (texturePath != null)
        {
            // 텍스처가 나중에 바뀌어도 이 FBX가 다시 임포트되게 묶는다.
            context.DependsOnSourceAsset(texturePath);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);   // 색은 텍스처가 담는다 — 곱해지면 어두워진다
            }
        }

        if (materialName != null && materialName.EndsWith(LeafCardSuffix))
            MakeLeafCard(material);
    }

    // 잎 카드: 투명한 부분을 잘라내고(알파 컷) 앞뒤 양쪽을 다 그린다.
    // 반투명(블렌딩) 대신 알파 컷을 쓰는 이유 — 잎 수백 장이 겹칠 때 정렬 문제가 없고 그림자도 잎 모양으로 진다.
    static void MakeLeafCard(Material material)
    {
        material.SetFloat("_Surface", 0f);          // 불투명
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.doubleSidedGI = true;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
    }

    // 파일명과 재질 이름을 NFC로 맞춰 비교한다. LoadAssetAtPath에 이름을 그대로 붙이면 자모 분리 때문에 조용히 못 찾는다.
    static string FindTexturePath(string materialName)
    {
        if (string.IsNullOrEmpty(materialName)) return null;

        string[] folders = TextureFolders.Where(AssetDatabase.IsValidFolder).ToArray();
        if (folders.Length == 0) return null;

        return AssetDatabase.FindAssets("t:Texture2D", folders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path => Nfc(System.IO.Path.GetFileNameWithoutExtension(path)) == materialName);
    }

    // 텍스처가 FBX보다 **늦게** 들어오면(처음 받았을 때 흔하다) 위의 의존성이 아직 없어서 FBX가 다시 안 돈다.
    // 텍스처 폴더에 뭔가 새로 들어오면 자연물·벽 FBX를 한 번 다시 임포트해 붙인다. FBX 임포트는 PNG를
    // 건드리지 않으므로 되돌아와서 무한히 돌지 않는다.
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (!imported.Any(path => TextureFolders.Any(folder => Nfc(path).StartsWith(folder + "/")))) return;

        string[] roots = new[] { "Assets/Art/Nature", "Assets/Art/Walls", "Assets/Art/Monsters" }
            .Where(AssetDatabase.IsValidFolder).ToArray();
        if (roots.Length == 0) return;

        string[] models = AssetDatabase.FindAssets("t:Model", roots)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string model in models)
                AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }
}
