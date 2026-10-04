using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 아이템 아이콘 연결(2026-10-04) — 원작 맵에서 뽑은 PNG(~/GRD_motion_trial/아이템아이콘/&lt;코드&gt;_&lt;이름&gt;.png)를
/// Assets/Art/Items/로 복사해 Sprite로 임포트하고, 같은 코드(I0xx)의 ItemData.icon에 꽂는다. 다시 불러도 안전하다.
/// 호출: call ItemIconLinker.LinkItemIcons (또는 메뉴 Tools/아이템/아이콘 연결)
/// </summary>
public static class ItemIconLinker
{
    const string ItemFolder = "Assets/Data/Items";
    const string IconFolder = "Assets/Art/Items";

    static string SourceFolder => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "GRD_motion_trial", "아이템아이콘");

    [MenuItem("Tools/아이템/아이콘 연결")]
    static void LinkMenu() => Debug.Log(LinkItemIcons());

    static string LinkItemIcons()
    {
        Directory.CreateDirectory(IconFolder);

        int copied = 0;
        if (Directory.Exists(SourceFolder))
        {
            foreach (string source in Directory.GetFiles(SourceFolder, "I*.png"))
            {
                string target = Path.Combine(IconFolder, Path.GetFileName(source));
                if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(source).Length) continue;
                File.Copy(source, target, true);
                copied++;
            }
            AssetDatabase.Refresh();
        }

        int linked = 0, noIcon = 0;
        var missing = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData", new[] { ItemFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null) continue;
            // 에셋 이름 ItemData_I003_명검-흑도슈스이 → 코드 I003
            string[] parts = Path.GetFileNameWithoutExtension(path).Split('_');
            string code = parts.Length > 1 ? parts[1] : "";
            string[] pngs = code.Length == 0 ? new string[0] : Directory.GetFiles(IconFolder, code + "_*.png");
            if (pngs.Length == 0) { noIcon++; missing.Add(code); continue; }

            string pngPath = pngs[0].Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sprite == null) { noIcon++; missing.Add(code + "(스프라이트 로드 실패)"); continue; }
            if (item.icon != sprite) { item.icon = sprite; EditorUtility.SetDirty(item); }
            linked++;
        }
        AssetDatabase.SaveAssets();
        return $"아이템 아이콘: 복사 {copied}개 · 연결 {linked}개 · 아이콘 없음(글자만) {noIcon}개" + (missing.Count > 0 ? $" — {string.Join(", ", missing)}" : "");
    }
}
