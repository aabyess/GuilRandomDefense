using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 워크3풍 UI 그림(blender가 새로 렌더 — ~/GRD_wc3_ui/, 정본 Tools/blender/gen_wc3_ui.py, 규격표 Docs/design/WC3_UI_ART_SPEC_2026-10-07.md)을
/// Assets/Resources/UI/SkinWc3/로 복사하고 9-slice 테두리·반복 설정을 건다. 호출: call Wc3SkinApply.Apply (다시 불러도 안전).
/// 납품은 2배 해상도라 코드에서 pixelsPerUnitMultiplier=2(또는 크기 지정)로 1배 크기로 쓴다. 블리자드 그림 아님 — 전부 새로 렌더한 것.
/// </summary>
static class Wc3SkinApply
{
    const string Source = "GRD_wc3_ui";
    const string Dest = "Assets/Resources/UI/SkinWc3";

    // 파일 이름(확장자 없이) → 9-slice 테두리(좌, 아래, 우, 위) px(2배 그림 기준) · 가로로 반복해 깔면 true
    static readonly (string name, float l, float b, float r, float t, bool repeat)[] Files =
    {
        ("console_bar_tile", 0, 0, 0, 0, true), ("console_bar_tile_tall", 0, 0, 0, 0, true),
        ("console_cap_left", 0, 0, 0, 0, false), ("console_cap_right", 0, 0, 0, 0, false),
        ("stone_pillar", 16, 0, 16, 0, false), ("panel_frame_stone", 32, 32, 32, 32, false),
        ("portrait_arch_frame", 0, 0, 0, 0, false), ("portrait_arch_mask", 0, 0, 0, 0, false),
        ("info_panel_frame", 56, 56, 56, 56, false), ("info_title_strip", 24, 24, 24, 24, false), ("info_level_strip", 16, 16, 16, 16, false), ("icon_slot_gold", 16, 16, 16, 16, false),
        ("inventory_title", 20, 20, 20, 20, false), ("inventory_cell", 20, 20, 20, 20, false), ("inventory_cell_filled", 20, 20, 20, 20, false),
        ("command_cell", 12, 12, 12, 12, false), ("command_cell_pressed", 12, 12, 12, 12, false), ("command_cell_hover", 12, 12, 12, 12, false), ("command_grid_frame", 40, 40, 40, 40, false),
        ("topbar_button", 28, 28, 28, 28, false), ("topbar_button_hover", 28, 28, 28, 28, false), ("topbar_button_pressed", 28, 28, 28, 28, false), ("res_cell", 28, 28, 28, 28, false),
        ("clock_orb", 0, 0, 0, 0, false), ("icon_gold", 0, 0, 0, 0, false), ("icon_wood", 0, 0, 0, 0, false), ("icon_trait", 0, 0, 0, 0, false),
        ("timer_window", 28, 28, 28, 28, false), ("scoreboard_frame", 28, 28, 28, 28, false), ("collapse_btn", 0, 0, 0, 0, false),
        ("hero_frame", 20, 20, 20, 20, false), ("bar_track", 8, 8, 8, 8, false),
        // 창 4종(blender A-2 메뉴) — 메뉴판은 모서리 돌이 커서 80(2배 기준), 단추는 둥근 돌판 36.
        ("menu_panel", 80, 80, 80, 80, false), ("menu_btn", 36, 36, 36, 36, false), ("menu_btn_hover", 36, 36, 36, 36, false), ("menu_btn_pressed", 36, 36, 36, 36, false), ("menu_btn_disabled", 36, 36, 36, 36, false),
        ("menu_divider", 0, 0, 0, 0, false),
        // 창 부품(blender A-1·A-3): 틀 모서리 돌 66, 단추·입력 30, 칩 24, 행 28, 서랍 탭 30, 툴팁 30. 채팅 입력은 IMGUI GUIStyle.border로 쓴다.
        ("win_frame", 66, 66, 66, 66, false), ("win_btn", 30, 30, 30, 30, false), ("win_btn_hover", 30, 30, 30, 30, false), ("win_btn_pressed", 30, 30, 30, 30, false), ("win_btn_selected", 30, 30, 30, 30, false), ("win_btn_disabled", 30, 30, 30, 30, false),
        ("win_search_box", 30, 30, 30, 30, false), ("win_search_box_focus", 30, 30, 30, 30, false),
        ("win_chip", 24, 24, 24, 24, false), ("win_chip_hover", 24, 24, 24, 24, false), ("win_chip_selected", 24, 24, 24, 24, false),
        ("drawer_tab", 30, 30, 30, 30, false), ("drawer_tab_hover", 30, 30, 30, 30, false),
        ("win_row", 28, 28, 28, 28, false), ("win_row_owned", 28, 28, 28, 28, false), ("win_row_dim", 28, 28, 28, 28, false),
        ("win_tooltip", 30, 30, 30, 30, false),
        ("chat_input", 40, 20, 20, 20, false), ("chat_input_focus", 40, 20, 20, 20, false), ("chat_line_band", 32, 14, 32, 14, false),
    };

    static string Apply()
    {
        string src = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), Source);
        Directory.CreateDirectory(Dest);
        int copied = 0, missing = 0;
        foreach (var f in Files)
        {
            string from = Path.Combine(src, f.name + ".png");
            if (!File.Exists(from)) { missing++; continue; }
            File.Copy(from, $"{Dest}/{f.name}.png", true);
            copied++;
        }
        AssetDatabase.Refresh();
        foreach (var f in Files)
        {
            string path = $"{Dest}/{f.name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = f.repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(f.l, f.b, f.r, f.t);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return $"워크3풍 그림 {copied}장 복사(없음 {missing}) · 위치 {Dest}";
    }
}
