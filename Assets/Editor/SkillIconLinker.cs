using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스킬 아이콘 연결(2026-10-06, 사장님 「스킬 아이콘을 정보 창에」) — Tools/skill_icons/skill_icon_map.csv(에셋 경로·원작 코드·png·확신도)를 읽어
/// ~/GRD_skill_icons/의 PNG를 Assets/Art/SkillIcons/로 복사(파일 이름의 # & [ ] ! 는 _로)·Sprite로 임포트(128px, 밉맵 없음)하고 SkillData.icon에 꽂는다.
/// 「계획」 행(에셋 경로 대신 「(예정) 유닛·스킬」)은 사장님 스킬(SkillData_사장님_*)을 스킬 이름 앞부분으로 찾아 연결한다. 확신도 「낮음」도 연결하고 결과 표에 나눠 센다.
/// 다시 불러도 안전하다(ItemIconLinker와 같은 방식). 호출: call SkillIconLinker.Link (또는 메뉴 Tools/스킬/아이콘 연결)
/// </summary>
public static class SkillIconLinker
{
    const string CsvPath = "Tools/skill_icons/skill_icon_map.csv";
    const string IconFolder = "Assets/Art/SkillIcons";
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string DefaultIconPath = "Tools/skill_icons/default_icon.txt";

    static string SourceFolder => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "GRD_skill_icons");

    [MenuItem("Tools/스킬/아이콘 연결")]
    static void LinkMenu() => Debug.Log(Link());

    static string Safe(string fileName)
    {
        var sb = new StringBuilder();
        foreach (char c in fileName) sb.Append("#&![]".IndexOf(c) >= 0 ? '_' : c);
        return sb.ToString();
    }

    // 따옴표 안 쉼표를 지키는 한 줄 CSV 파서(셀 안 줄바꿈은 이 표에 없다).
    static List<string> ParseLine(string line)
    {
        var cells = new List<string>();
        var cur = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else quoted = false; }
                else cur.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        cells.Add(cur.ToString());
        return cells;
    }

    static SkillData FindPlanned(string planned, List<SkillData> sajang, out string note)
    {
        // 「(예정) 바지사장·분노조절장애(Style : 최상호)」 → 스킬 이름 「분노조절장애」
        note = null;
        string body = planned.Replace("(예정)", "").Trim();
        int dot = body.IndexOf('·');
        string skillPart = dot >= 0 ? body.Substring(dot + 1) : body;
        int paren = skillPart.IndexOf('(');
        string core = (paren > 0 ? skillPart.Substring(0, paren) : skillPart).Trim();
        if (core.Length == 0) { note = "빈 이름"; return null; }
        SkillData found = null;
        int hits = 0;
        foreach (SkillData s in sajang)
            if (s != null && !string.IsNullOrEmpty(s.skillName) && s.skillName.StartsWith(core)) { found = s; hits++; }
        if (hits == 1) return found;
        note = hits == 0 ? "일치하는 사장님 스킬 없음" : $"{hits}개 일치(모호)";
        return null;
    }

    static string Link()
    {
        if (!File.Exists(CsvPath)) return $"❌ {CsvPath} 없음";
        if (!Directory.Exists(SourceFolder)) return $"❌ {SourceFolder} 없음";
        Directory.CreateDirectory(IconFolder);

        var sajang = new List<SkillData>();
        foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { SkillFolder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(p).StartsWith("SkillData_사장님_")) sajang.Add(AssetDatabase.LoadAssetAtPath<SkillData>(p));
        }

        // 1) 행 → (스킬, png, 확신도)
        var jobs = new List<(SkillData skill, string png, string confidence, string label)>();
        var problems = new List<string>();
        var seenPaths = new HashSet<string>();   // 표에 행이 있는 에셋 경로(아이콘없음 행 포함)
        int rows = 0, noPng = 0, missingAsset = 0, plannedMissing = 0, defaulted = 0;
        string defaultPng = File.Exists(DefaultIconPath) ? File.ReadAllText(DefaultIconPath, Encoding.UTF8).Trim() : "";
        string[] lines = File.ReadAllLines(CsvPath, Encoding.UTF8);
        for (int li = 1; li < lines.Length; li++)
        {
            if (string.IsNullOrWhiteSpace(lines[li])) continue;
            List<string> c = ParseLine(lines[li].TrimStart('﻿'));
            if (c.Count < 11) continue;
            rows++;
            if (!c[0].StartsWith("(예정)")) seenPaths.Add(c[0]);
            string png = c[7].Trim();
            if (png.Length == 0)
            {
                // 「아이콘없음」 행(게이트·더미 58행) — 칸이 비지 않게 기본 그림(Tools/skill_icons/default_icon.txt)을 꽂는다.
                if (defaultPng.Length == 0) { noPng++; continue; }
                png = defaultPng; defaulted++;
            }
            SkillData skill;
            if (c[0].StartsWith("(예정)"))
            {
                skill = FindPlanned(c[0], sajang, out string note);
                if (skill == null) { plannedMissing++; problems.Add($"{c[0]} → {note}"); continue; }
            }
            else
            {
                skill = AssetDatabase.LoadAssetAtPath<SkillData>(c[0]);
                if (skill == null) { missingAsset++; problems.Add($"{c[0]} → 에셋 없음"); continue; }
            }
            jobs.Add((skill, png, c[10].Trim(), c[0]));
        }

        // 2) PNG 복사
        int copied = 0, sourceMissing = 0;
        var wanted = new HashSet<string>();
        foreach (var j in jobs) wanted.Add(j.png);
        // 표에 행이 없는 SkillData(오늘 새로 만든 불멸·영원함·초월 등) — 칸이 비지 않게 같은 기본 그림. blender가 표 행을 추가하면 다음 Link가 진짜 그림으로 바꾼다.
        var noRow = new List<SkillData>();
        if (defaultPng.Length > 0)
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { SkillFolder }))
            {
                string ap = AssetDatabase.GUIDToAssetPath(guid);
                if (seenPaths.Contains(ap)) continue;
                var sk = AssetDatabase.LoadAssetAtPath<SkillData>(ap);
                if (sk != null) noRow.Add(sk);
            }
        if (noRow.Count > 0) wanted.Add(defaultPng);
        foreach (string png in wanted)
        {
            string source = Path.Combine(SourceFolder, png);
            if (!File.Exists(source)) { sourceMissing++; continue; }
            string target = Path.Combine(IconFolder, Safe(png));
            if (File.Exists(target)) continue;   // 이미 줄여 둔 128px 사본(Tools/skill_icons/resize_icons.py)이 있으면 원본(256px)으로 덮지 않는다
            File.Copy(source, target, true);
            copied++;
        }
        AssetDatabase.Refresh();

        // 3) Sprite 임포트 설정 + 연결
        var sprites = new Dictionary<string, Sprite>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string png in wanted)
            {
                string path = $"{IconFolder}/{Safe(png)}";
                if (!File.Exists(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.maxTextureSize != 128))
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.maxTextureSize = 128;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.SaveAndReimport();
                }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        var byConfidence = new Dictionary<string, int>();
        int linked = 0, spriteFail = 0;
        foreach (var j in jobs)
        {
            string path = $"{IconFolder}/{Safe(j.png)}";
            if (!sprites.TryGetValue(path, out Sprite sprite)) { sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); sprites[path] = sprite; }
            if (sprite == null) { spriteFail++; continue; }
            if (j.skill.icon != sprite) { j.skill.icon = sprite; EditorUtility.SetDirty(j.skill); }
            linked++;
            string key = string.IsNullOrEmpty(j.confidence) ? "(없음)" : j.confidence;
            byConfidence[key] = (byConfidence.TryGetValue(key, out int n) ? n : 0) + 1;
        }
        // 표 행 없는 것: 아이콘이 비어 있거나 이미 기본 그림이면 기본 그림(진짜 그림이 있는 건 건드리지 않는다)
        int noRowDefaulted = 0;
        if (noRow.Count > 0)
        {
            Sprite defaultSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconFolder}/{Safe(defaultPng)}");
            if (defaultSprite != null)
                foreach (SkillData sk in noRow)
                {
                    if (sk.icon != null && sk.icon != defaultSprite) continue;
                    if (sk.icon != defaultSprite) { sk.icon = defaultSprite; EditorUtility.SetDirty(sk); }
                    noRowDefaulted++;
                }
        }
        AssetDatabase.SaveAssets();

        var sb = new StringBuilder();
        sb.AppendLine($"스킬 아이콘 연결: 표 {rows}행 · 연결 {linked}개 · PNG 복사 {copied}개(원본 없음 {sourceMissing}) · 기본 그림 — 「아이콘없음」 행이라 {defaulted}개 · 「표 행이 없어서」 {noRowDefaulted}개 · PNG 없는 행 {noPng}개(글자 첫 글자로 표시) · 에셋 없음 {missingAsset} · 계획 행 못 맞춤 {plannedMissing} · 스프라이트 실패 {spriteFail}");
        sb.Append("  확신도별 연결: ");
        foreach (var kv in byConfidence) sb.Append($"{kv.Key} {kv.Value} · ");
        if (problems.Count > 0) sb.Append("\n  못 맞춘 행: " + string.Join(" | ", problems));
        return sb.ToString();
    }
}
