using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 첫 화면 시안(blender, 사장님 10-06 확정 「디자인 좋다 이대로 가자」) 재료를 Assets로 들인다. 호출: call LobbyArtSetup.Apply (다시 불러도 안전).
///  · ~/GRD_lobby_art/{bg_1920x1080,bg_2560x1440}.png, ui/{btn_normal,btn_hover,btn_pressed,menu_frame,title_plate}.png → Assets/Resources/UI/Lobby/ (UI는 Sprite, 9-slice 테두리: 단추 좌우·상하 40 · 메뉴 틀 좌우 64·상하 160, 제목 판은 통짜)
///  · ~/GRD_lobby_art/fonts/{songmyung,nanummyeongjo} → Assets/Fonts/Lobby/ (OFL.txt 동봉) + TMP 폰트 에셋 Assets/Resources/Fonts/{SongMyung,NanumMyeongjo-ExtraBold} SDF (Dynamic, 로비 글자 미리 굽기)
/// 출처: 모두 SIL OFL 1.1 폰트(github.com/google/fonts) — Docs/CREDITS.md 참고.
/// </summary>
static class LobbyArtSetup
{
    const string ArtFolder = "Assets/Resources/UI/Lobby";
    const string FontSourceFolder = "Assets/Fonts/Lobby";
    const string FontAssetFolder = "Assets/Resources/Fonts";
    static string Home => System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

    static void Copy(string source, string target, StringBuilder log)
    {
        if (!File.Exists(source)) { log.AppendLine($"   ⚠️ 원본 없음: {source}"); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(source).Length) return;
        File.Copy(source, target, true);
        log.AppendLine($"   복사 {Path.GetFileName(target)}");
    }

    static void ImportSprite(string path, Vector4 border /* left, bottom, right, top */)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    static void ImportBackground(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    static string Apply()
    {
        StringBuilder log = new StringBuilder();
        string art = Path.Combine(Home, "GRD_lobby_art");
        Directory.CreateDirectory(ArtFolder);
        Directory.CreateDirectory(FontSourceFolder);
        foreach (string name in new[] { "bg_1920x1080.png", "bg_2560x1440.png" }) Copy(Path.Combine(art, name), $"{ArtFolder}/{name}", log);
        foreach (string name in new[] { "btn_normal.png", "btn_hover.png", "btn_pressed.png", "menu_frame.png", "title_plate.png" }) Copy(Path.Combine(art, "ui", name), $"{ArtFolder}/{name}", log);
        Copy(Path.Combine(art, "fonts/songmyung/SongMyung-Regular.ttf"), $"{FontSourceFolder}/SongMyung-Regular.ttf", log);
        Copy(Path.Combine(art, "fonts/songmyung/OFL.txt"), $"{FontSourceFolder}/OFL-SongMyung.txt", log);
        Copy(Path.Combine(art, "fonts/nanummyeongjo/NanumMyeongjo-ExtraBold.ttf"), $"{FontSourceFolder}/NanumMyeongjo-ExtraBold.ttf", log);
        Copy(Path.Combine(art, "fonts/nanummyeongjo/OFL.txt"), $"{FontSourceFolder}/OFL-NanumMyeongjo.txt", log);
        AssetDatabase.Refresh();

        ImportBackground($"{ArtFolder}/bg_1920x1080.png");
        ImportBackground($"{ArtFolder}/bg_2560x1440.png");
        foreach (string name in new[] { "btn_normal", "btn_hover", "btn_pressed" }) ImportSprite($"{ArtFolder}/{name}.png", new Vector4(40f, 40f, 40f, 40f));
        ImportSprite($"{ArtFolder}/menu_frame.png", new Vector4(64f, 160f, 64f, 160f));
        ImportSprite($"{ArtFolder}/title_plate.png", Vector4.zero);

        log.AppendLine(BuildFont("SongMyung-Regular", "SongMyung SDF"));
        log.AppendLine(BuildFont("NanumMyeongjo-ExtraBold", "NanumMyeongjo-ExtraBold SDF"));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // 로비에 나오는 글자(첫 화면·같이 하기·대기실) — 나머지는 Dynamic이 쓸 때 채운다.
    const string LobbyChars = "구랜디혼자하기같이세이브코드불러오기닉네임이름을입력하세요방만들기또는참가뒤로복사준비취소시작나가기보내기비어있음중완료방장친구에게할말펼치기접기다시난도모드선택한뒤창에서고릅니다 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:;/()[]-+!?~…·";

    static string BuildFont(string face, string assetName)
    {
        string path = $"{FontAssetFolder}/{assetName}.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null) return $"   폰트 에셋 이미 있음: {assetName}";
        Font source = AssetDatabase.LoadAssetAtPath<Font>($"{FontSourceFolder}/{face}.ttf");
        if (source == null) return $"   ⚠️ {face}.ttf 를 못 읽었다";
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (asset == null) return $"   ⚠️ {assetName} 생성 실패";
        asset.name = assetName;
        asset.TryAddCharacters(LobbyChars, out string missing);
        Directory.CreateDirectory(FontAssetFolder);
        AssetDatabase.CreateAsset(asset, path);
        if (asset.atlasTextures != null)
            foreach (Texture2D atlas in asset.atlasTextures)
                if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
        if (asset.material != null && !AssetDatabase.Contains(asset.material)) AssetDatabase.AddObjectToAsset(asset.material, asset);
        EditorUtility.SetDirty(asset);
        return $"   폰트 에셋 만듦: {assetName} (미리 구운 글자 {LobbyChars.Length - (string.IsNullOrEmpty(missing) ? 0 : missing.Length)}자" + (string.IsNullOrEmpty(missing) ? "" : $", 폰트에 없는 글자 「{missing}」") + ")";
    }
}
