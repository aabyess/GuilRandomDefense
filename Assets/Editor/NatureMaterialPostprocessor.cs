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
    const string BuildingRoot = "Assets/Art/Buildings/";   // 스토리 건물 13종(2026-09-12)
    const string CreatureRoot = "Assets/Art/Creatures/";   // 물범·노루·양(2026-09-12)
    const string PropRoot = "Assets/Art/Props/";           // 보물상자·금화더미·보물표시(2026-09-12)
    const string StructureRoot = "Assets/Art/Structures/"; // 캠프파이어·상점·포탈·창고·받침·부두 등 구조물(2026-09-13)
    // 배 유닛 두 척(2026-09-13, Blender) — Units 아래지만 사람 스킨이 아니라 Blender 규칙(텍스처·_잎카드·_발광)을 따른다.
    static readonly string[] ShipRoots = { "Assets/Art/Units/고대의배/", "Assets/Art/Units/해적선/" };
    // 텍스처는 자기 종류 폴더의 Textures에서 찾는다(자연물·벽은 Nature/Textures, 괴물은 Monsters/Textures).
    static readonly string[] TextureFolders =
        { "Assets/Art/Nature/Textures", "Assets/Art/Walls/Textures", "Assets/Art/Monsters/Textures",
          "Assets/Art/Buildings/Textures", "Assets/Art/Creatures/Textures", "Assets/Art/Props/Textures",
          "Assets/Art/Structures/Textures" };
    const string LeafCardSuffix = "_잎카드";   // 잎 카드·지느러미 막 — 양면 + 알파 컷
    const string EmissiveSuffix = "_발광";     // 스스로 빛남 — 색 텍스처를 Emission에도(`_발광_잎카드`면 둘 다)
    // 이름 규칙이 생기기 전에 커밋된 발광 재질 — 다시 내보내지 않고 이름으로 처리한다(화난 건물의 붉은 창).
    static readonly string[] EmissiveNames = { "건물_창_분노", "건물_커튼월_분노" };

    // 규칙을 바꾸면 올린다 — 올려야 이미 임포트된 FBX도 다시 돈다.
    // 1 → 2 (2026-09-12): 거대 해왕류(Assets/Art/Monsters)를 대상에 추가.
    // 2 → 3 (2026-09-12): Blender 복제 번호(.001) 꼬리를 떼고 매칭 — 이미 임포트된 침엽수_02·활엽수_가을도 다시 돌게.
    // 3 → 4 (2026-09-13): Structures·배 유닛 대상 추가, `_발광` 규칙, FBX 옆 Textures 폴더 우선 탐색.
    public override uint GetVersion() => 4;

    // URP의 기본 재질 설명 처리(셰이더를 Lit로, 색을 FBX 기본색으로)가 먼저 돈 뒤에 덧붙인다.
    public override int GetPostprocessOrder() => 100;

    // 🔴 macOS에서 유니티가 넘기는 한글 경로·이름은 NFC가 아닐 수 있다(UnitModelPostprocessor 참고).
    static string Nfc(string s) => s?.Normalize(System.Text.NormalizationForm.FormC);

    static bool Applies(string path)
    {
        string nfc = Nfc(path);
        return nfc != null && (nfc.StartsWith(NatureRoot) || nfc.StartsWith(WallRoot) ||
                               nfc.StartsWith(MonsterRoot) || nfc.StartsWith(BuildingRoot) ||
                               nfc.StartsWith(CreatureRoot) || nfc.StartsWith(PropRoot) ||
                               nfc.StartsWith(StructureRoot) || ShipRoots.Any(nfc.StartsWith));
    }

    void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] animations)
    {
        if (!Applies(assetPath)) return;

        // Blender는 한 번에 여러 나무를 지으면 같은 재질을 `C_침엽_껍질.001`처럼 복제해 이름 뒤에 번호를 붙인다.
        // 그대로 비교하면 텍스처(C_침엽_껍질.png)도 못 찾고 `_잎카드` 꼬리도 안 맞아 잎이 네모 판으로 나온다
        // (2026-09-12 실측: 침엽수_02·활엽수_가을이 이 상태로 들어왔다). 번호 꼬리는 떼고 비교한다.
        string materialName = StripBlenderDuplicateSuffix(Nfc(description.materialName));
        string texturePath = FindTexturePath(materialName, SiblingTextureFolder(assetPath));
        Texture2D texture = null;
        if (texturePath != null)
        {
            // 텍스처가 나중에 바뀌어도 이 FBX가 다시 임포트되게 묶는다.
            context.DependsOnSourceAsset(texturePath);

            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);   // 색은 텍스처가 담는다 — 곱해지면 어두워진다
            }
        }

        if (materialName == null) return;

        if (materialName.EndsWith(LeafCardSuffix))
            MakeLeafCard(material);

        // `포탈_막_호박_발광_잎카드`처럼 두 꼬리가 겹칠 수 있다 — 잎카드 꼬리를 떼고 발광 꼬리를 본다.
        string withoutLeafCard = materialName.EndsWith(LeafCardSuffix)
            ? materialName.Substring(0, materialName.Length - LeafCardSuffix.Length)
            : materialName;
        if (withoutLeafCard.EndsWith(EmissiveSuffix) || EmissiveNames.Contains(materialName))
            MakeEmissive(material, texture);
    }

    // 스스로 빛나는 면(불씨·붉게 달아오른 창·포탈 막·등불). 색 텍스처를 그대로 발광 지도로 쓴다 —
    // 텍스처가 이미 「빛나는 곳만 밝게」 구워져 있어서(불씨 발광 면적 10% 안팎) 따로 마스크가 필요 없다.
    static void MakeEmissive(Material material, Texture2D texture)
    {
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.SetColor("_EmissionColor", Color.white);
        if (texture != null) material.SetTexture("_EmissionMap", texture);
    }

    // FBX 바로 옆 Textures 폴더. 배 유닛처럼 유닛마다 폴더가 따로인 경우를 위해 먼저 본다.
    static string SiblingTextureFolder(string path)
    {
        string dir = System.IO.Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(dir) ? null : dir.Replace('\\', '/') + "/Textures";
    }

    // "이름.001" → "이름". 점 뒤가 숫자 세 자리일 때만 뗀다 — 이름 자체에 점이 들어간 경우를 안 망가뜨리게.
    static string StripBlenderDuplicateSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return System.Text.RegularExpressions.Regex.Replace(name, @"\.\d{3}$", "");
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
    static string FindTexturePath(string materialName, string siblingFolder)
    {
        if (string.IsNullOrEmpty(materialName)) return null;

        // FBX 옆 폴더는 유니티가 준 실제 경로에서 뽑았으니, 한글 폴더 이름의 정규화(NFC/NFD) 차이에 안 걸린다.
        string[] folders = new[] { siblingFolder }.Concat(TextureFolders)
            .Where(folder => !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
            .Distinct()
            .ToArray();
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
        if (!imported.Any(path => TextureFolders.Any(folder => Nfc(path).StartsWith(folder + "/")) ||
                                  ShipRoots.Any(root => Nfc(path).StartsWith(root + "Textures/")))) return;

        string[] roots = new[] { "Assets/Art/Nature", "Assets/Art/Walls", "Assets/Art/Monsters", "Assets/Art/Buildings",
                                 "Assets/Art/Creatures", "Assets/Art/Props", "Assets/Art/Structures" }
            .Concat(ShipRoots.Select(root => root.TrimEnd('/')))
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
