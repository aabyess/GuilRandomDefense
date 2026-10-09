using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 원작 스킨 교체 반입(10-09 사장님 「초월 유닛 겉모습을 원작 모델로」, 범위 (가) = 겉모습·동작·연출만, 스킬 효과·수치·이름은 그대로):
/// ~/GRD_skin_swap/&lt;유닛&gt;_&lt;원작&gt;/ (blender: model/ FBX·Textures·json · clip_map.json · scripts/ · pairing.csv)를 한 유닛씩 넣는다.
/// 절차(Docs/process/SKIN_SWAP.md): ①옛 FBX·.meta 삭제(T자 함정) ②새 FBX·Textures·clip_map.json 복사 ③Generic 임포트 + 클립 이름 붙이기(Idle·Move·Attack…·Spell·Death)
/// ④텍스처 연결 ⑤프리팹 한 개만 다시 짓기(ArtBinder.BindOneUnit) + 숨김 메시 컴포넌트 ⑥연출 대본 반입·짝 연결.
/// 호출: call SkinSwapImporter.SwapOne  (ClaudeBridge/g2_swap.txt = 「초월_김경현_AP|김경현_상디」) · SwapAll(교체목록.csv의 우리 로스터 ↔ 폴더 자동).
/// </summary>
public static class SkinSwapImporter
{
    static readonly string PackRoot = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "GRD_skin_swap");
    public static readonly string SwapScriptRoot = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "GRD_scenes/scripts_swap");
    const string UnitRoot = "Assets/Art/Units";

    [MenuItem("Tools/아트/원작 스킨 교체 반입(전부)")]
    static void Menu() => Debug.Log(SwapAll());

    public static string SwapOne()
    {
        string[] spec = File.ReadAllText("ClaudeBridge/g2_swap.txt").Trim().Split('|');
        return Swap(spec[0], Path.Combine(PackRoot, spec[1]));
    }

    public static string SwapAll()
    {
        var sb = new StringBuilder();
        string listFile = Path.Combine(PackRoot, "교체목록.csv");
        string[] lines = File.ReadAllLines(listFile);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] c = lines[i].Split(',');
            if (c.Length < 5) continue;
            string roster = c[1].Trim();
            string[] ids = c[4].Split('/').Select(x => x.Trim().ToUpperInvariant()).ToArray();
            string person = roster.Split('_').Length > 1 ? roster.Split('_')[1] : roster;
            string pack = null;
            foreach (string dir in Directory.GetDirectories(PackRoot))
            {
                string cm = Path.Combine(dir, "clip_map.json");
                if (!File.Exists(cm)) continue;
                if (!Path.GetFileName(dir).StartsWith(person + "_")) continue;
                var root = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(cm));
                string oid = root.TryGetValue("originalId", out object o) ? (o as string ?? "").ToUpperInvariant() : "";
                if (ids.Contains(oid)) { pack = dir; break; }
            }
            sb.AppendLine(pack == null ? $"❌ {roster}: 교체 폴더 못 찾음({c[4]})" : Swap(roster, pack));
        }
        return sb.ToString();
    }

    public static string Swap(string roster, string packDir)
    {
        string modelDir = Path.Combine(packDir, "model");
        string srcFbx = Directory.GetFiles(modelDir, "*.fbx").FirstOrDefault();
        string clipMapPath = Path.Combine(packDir, "clip_map.json");
        if (srcFbx == null || !File.Exists(clipMapPath)) return $"❌ {roster}: model/*.fbx 또는 clip_map.json 없음({packDir})";
        if (AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{roster}.asset") == null) return $"❌ {roster}: 로스터 없음";

        string dest = $"{UnitRoot}/{roster}";
        // ① 옛 파일 지우기 — .meta까지(옛 .meta의 뼈 매핑·클립 설정이 새 모델에 남으면 T자·엉뚱한 클립이 된다)
        if (AssetDatabase.IsValidFolder(dest))
            foreach (string guid in AssetDatabase.FindAssets("", new[] { dest })) { }
        if (AssetDatabase.IsValidFolder(dest)) AssetDatabase.DeleteAsset(dest);
        AssetDatabase.CreateFolder(UnitRoot, roster);
        AssetDatabase.CreateFolder(dest, "Textures");

        // ② 복사
        File.Copy(srcFbx, Path.Combine(Directory.GetCurrentDirectory(), $"{dest}/{roster}.fbx"), true);
        string texSrc = Path.Combine(modelDir, "Textures");
        if (Directory.Exists(texSrc))
            foreach (string t in Directory.GetFiles(texSrc)) if (!t.EndsWith(".meta")) File.Copy(t, Path.Combine(Directory.GetCurrentDirectory(), $"{dest}/Textures/{Path.GetFileName(t)}"), true);
        File.Copy(clipMapPath, Path.Combine(Directory.GetCurrentDirectory(), $"{dest}/clip_map.json"), true);
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), $"{dest}/SOURCE.txt"),
            $"원작 스킨 교체(10-09): {Path.GetFileName(packDir)} — 원작 모델 {Path.GetFileName(srcFbx)}(blender 변환, 30fps). 스킬 효과·수치·이름은 그대로.\n받은 폴더: {packDir}\n⚠️ 원작(블리자드·모델러) 저작물이라 배포 전 라이선스 판단 필요.\n");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // ③ 클립 이름 붙이기
        string fbxAsset = $"{dest}/{roster}.fbx";
        var importer = AssetImporter.GetAtPath(fbxAsset) as ModelImporter;
        if (importer == null) return $"❌ {roster}: 임포터 없음";
        var report = new StringBuilder();
        var clipMap = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(clipMapPath));
        var clips = (List<object>)clipMap["clips"];
        ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
        var chosen = new List<ModelImporterClipAnimation>();
        var hiddenByClip = new Dictionary<string, string[]>();
        var used = new HashSet<string>();
        foreach (object co in clips)
        {
            var c = (Dictionary<string, object>)co;
            string ourClip = (c["ourClip"] as string ?? "").Trim();
            if (ourClip.Length == 0 || ourClip.Contains("(") || ourClip.Contains("필요")) continue;   // 「Idle 2(변형 — 필요 없으면 생략)」은 건너뜀
            if (!used.Add(ourClip)) continue;
            string action = c["fbxAction"] as string;
            ModelImporterClipAnimation def = defaults.FirstOrDefault(d => d.takeName == action || d.takeName.EndsWith("|" + action));
            if (def == null) { report.Append($" ⚠️{ourClip}: 테이크 {action} 없음"); continue; }
            def.name = ourClip;
            bool loop = c.TryGetValue("loop", out object lo) && lo is bool lb && lb;
            def.loopTime = loop; def.loopPose = loop;
            chosen.Add(def);
            if (c.TryGetValue("hiddenMeshes", out object ho) && ho is List<object> hl && hl.Count > 0) hiddenByClip[ourClip] = hl.Select(x => x as string).ToArray();
        }
        importer.clipAnimations = chosen.ToArray();
        importer.SaveAndReimport();

        // ④ 텍스처 연결 · ⑤ 프리팹
        ArtBinder.LinkTexturesFor(roster);
        string bind = ArtBinder.BindOneUnit(roster);
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{roster}.asset");
        string prefabPath = unit != null && unit.prefab != null ? AssetDatabase.GetAssetPath(unit.prefab) : null;
        if (prefabPath != null && hiddenByClip.Count > 0)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var hider = contents.GetComponent<SkinClipHider>() ?? contents.AddComponent<SkinClipHider>();
                hider.entries = hiddenByClip.Select(kv => new SkinClipHider.Entry { clip = kv.Key, meshes = kv.Value }).ToArray();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // ⑥ 연출 대본
        string scripts = ImportScripts(roster, packDir, report);
        return $"✅ {roster} ← {Path.GetFileName(packDir)} · 클립 {string.Join("/", chosen.Select(c => c.name))}{report}\n   {bind}\n   {scripts}";
    }

    // pairing.csv 열: 우리 스킬 에셋,우리 스킬 이름,우리 발동,우리 범위,원작 연출(트리거),대본 파일,짝 근거,확신,겹침 표시
    static string ImportScripts(string roster, string packDir, StringBuilder report)
    {
        Directory.CreateDirectory(SwapScriptRoot);
        string scriptsDir = Path.Combine(packDir, "scripts");
        int copied = 0;
        if (Directory.Exists(scriptsDir))
            foreach (string f in Directory.GetFiles(scriptsDir, "*.json")) { File.Copy(f, Path.Combine(SwapScriptRoot, Path.GetFileName(f)), true); copied++; }
        // 짝 표: 확신 상·중이고 대본 파일이 있는 행만 「스킬|대본id」로 모은다(자동 검사 통과 뒤 LinkPassed가 연결)
        string pairing = Path.Combine(packDir, "pairing.csv");
        var links = new List<string>();
        if (File.Exists(pairing))
            foreach (string line in File.ReadAllLines(pairing).Skip(1))
            {
                string[] c = SplitCsv(line);
                if (c.Length < 8) continue;
                string conf = c[7].Trim();
                if (conf != "상" && conf != "중") continue;
                string script = Path.GetFileNameWithoutExtension(c[5].Trim());
                if (string.IsNullOrEmpty(script) || script == "없음") continue;
                links.Add($"{c[0].Trim()}|{script}");
            }
        string extra = "Docs/research/CINEMATIC_PAIR_EXTRA.tsv";
        var existing = File.Exists(extra) ? File.ReadAllLines(extra).ToList() : new List<string>();
        foreach (string l in links) if (!existing.Contains(l)) existing.Add(l);
        File.WriteAllLines(extra, existing);
        return $"대본 {copied}개 복사 · 짝 {links.Count}건 기록({extra}) — 자동 검사 뒤 LinkPassed";
    }

    static string[] SplitCsv(string line)
    {
        var cells = new List<string>(); var cur = new StringBuilder(); bool quoted = false;
        foreach (char ch in line.TrimStart('﻿'))
        {
            if (ch == '"') quoted = !quoted;
            else if (ch == ',' && !quoted) { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(ch);
        }
        cells.Add(cur.ToString());
        return cells.ToArray();
    }
}
