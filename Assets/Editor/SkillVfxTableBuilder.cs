using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Docs/research/SKILL_VFX_MAPPING.csv → Resources/Effects/SkillVfxTable.asset (2026-09-30).
// 칸 값: 「CFXR: 이름」·「Hovl: 이름」 = Assets/ThirdParty 프리팹 · 「SkillVfx.X」 = 우리 기본 종류 ·
//        「자체메시: VFX_…」 = 그 메시를 쓰는 우리 기본 종류(원판·벽 → StunImpact, 땅 폭발 → Smash, 번개 기둥 → SpellHit, 초승달 → Hit) ·
//        빈칸·「(원작 모델 판별 불가)」 = 기본대로.
// 부르는 법: ClaudeBridge `call SkillVfxTableBuilder.Build` · 메뉴 Tools/이펙트/스킬 이펙트 표 만들기. CSV가 바뀌면 다시 돌린다.
public static class SkillVfxTableBuilder
{
    const string CsvPath = "Docs/research/SKILL_VFX_MAPPING.csv";
    const string OutPath = "Assets/Resources/Effects/SkillVfxTable.asset";
    const string PackRoot = "Assets/ThirdParty";
    const string OwnRoot = "Assets/Art/Effects/Bomb";   // 우리가 만든 프리팹(BombFxBuilder, 10-06)

    [MenuItem("Tools/이펙트/스킬 이펙트 표 만들기")]
    static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        string csvFull = Path.Combine(Application.dataPath, "..", CsvPath);
        if (!File.Exists(csvFull)) return "❌ CSV 없음: " + CsvPath;
        List<string[]> rows = ReadCsv(File.ReadAllLines(csvFull).Where(l => !l.StartsWith("#")));
        string[] header = rows[0];
        int cAsset = System.Array.IndexOf(header, "asset_path");
        int cHit = System.Array.IndexOf(header, "적중시_프리팹");
        int cArea = System.Array.IndexOf(header, "범위_지면_프리팹");
        int cCaster = System.Array.IndexOf(header, "시전자_프리팹");
        if (cAsset < 0 || cHit < 0 || cArea < 0 || cCaster < 0) return "❌ CSV 열 이름이 다름: " + string.Join(",", header);

        Dictionary<string, string> prefabByName = AssetDatabase.FindAssets("t:Prefab", new[] { PackRoot, OwnRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .GroupBy(p => Path.GetFileNameWithoutExtension(p))
            .ToDictionary(g => g.Key, g => g.First());

        SkillVfxTable table = AssetDatabase.LoadAssetAtPath<SkillVfxTable>(OutPath);
        bool isNew = table == null;
        if (isNew) table = ScriptableObject.CreateInstance<SkillVfxTable>();
        table.prefabs.Clear(); table.nativeSizes.Clear(); table.entries.Clear();

        var unknown = new HashSet<string>();
        int missingSkill = 0, withAny = 0;
        foreach (string[] r in rows.Skip(1))
        {
            if (r.Length <= cCaster) continue;
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(r[cAsset].Trim());
            if (skill == null) { missingSkill++; continue; }
            var e = new SkillVfxTable.Entry
            {
                skill = skill,
                hit = Slot(table, r[cHit], prefabByName, unknown),
                area = Slot(table, r[cArea], prefabByName, unknown),
                caster = Slot(table, r[cCaster], prefabByName, unknown),
            };
            if (!e.hit.IsSet && !e.area.IsSet && !e.caster.IsSet) continue;
            table.entries.Add(e);
            withAny++;
        }

        foreach (GameObject prefab in table.prefabs) table.nativeSizes.Add(MeasureNativeSize(prefab));

        if (isNew)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            AssetDatabase.CreateAsset(table, OutPath);
        }
        else EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        var sb = new StringBuilder();
        sb.AppendLine($"✅ 스킬 이펙트 표 {OutPath}: 스킬 {withAny}개 · 팩 프리팹 {table.prefabs.Count}종 · CSV에 있는데 에셋 없음 {missingSkill}");
        for (int i = 0; i < table.prefabs.Count; i++)
            sb.AppendLine($"   [{i}] {table.prefabs[i].name}  원래 지름 {table.nativeSizes[i]:0.00}");
        if (unknown.Count > 0) sb.AppendLine("   ⚠️ 못 알아본 칸 값: " + string.Join(" | ", unknown));
        return sb.ToString();
    }

    static SkillVfxTable.Slot Slot(SkillVfxTable table, string raw, Dictionary<string, string> prefabByName, HashSet<string> unknown)
    {
        var slot = new SkillVfxTable.Slot();
        string v = raw.Trim();
        if (v.Length == 0 || v.StartsWith("(")) return slot;
        if (v.StartsWith("SkillVfx."))
        {
            if (System.Enum.TryParse(v.Substring("SkillVfx.".Length), out SkillVfx.Kind k)) slot.kind = (int)k;
            else unknown.Add(v);
            return slot;
        }
        if (v.StartsWith("자체메시:"))
        {
            string m = v.Substring("자체메시:".Length).Trim();
            SkillVfx.Kind? k = m switch
            {
                "VFX_충격파_원판" => SkillVfx.Kind.StunImpact,
                "VFX_충격파_벽" => SkillVfx.Kind.StunImpact,
                "VFX_땅_폭발" => SkillVfx.Kind.Smash,
                "VFX_번개_기둥" => SkillVfx.Kind.SpellHit,
                "VFX_초승달_검기" => SkillVfx.Kind.Hit,
                _ => null,
            };
            if (k.HasValue) slot.kind = (int)k.Value; else unknown.Add(v);
            return slot;
        }
        int colon = v.IndexOf(':');
        string name = colon >= 0 ? v.Substring(colon + 1).Trim() : v;
        if (!prefabByName.TryGetValue(name, out string path)) { unknown.Add(v); return slot; }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        int index = table.prefabs.IndexOf(prefab);
        if (index < 0) { table.prefabs.Add(prefab); index = table.prefabs.Count - 1; }
        slot.prefab = index;
        return slot;
    }

    // 0.4초 시뮬레이션 뒤 파티클 렌더러 경계의 가로(X·Z) 지름 — 프리팹마다 원래 크기가 제각각(0.5~10)이라 이걸로 나눠 맞춘다.
    static float MeasureNativeSize(GameObject prefab)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            go.transform.position = Vector3.zero;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
                if (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null)
                    ps.Simulate(0.4f, true, true, true);
            Bounds? b = null;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                if (r is ParticleSystemRenderer psr && psr.GetComponent<ParticleSystem>().particleCount == 0) continue;
                if (b == null) b = r.bounds; else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; }
            }
            if (b == null) return 1f;
            float d = Mathf.Max(b.Value.size.x, b.Value.size.z);
            return d > 0.05f ? d : 1f;
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    // 따옴표 안 쉼표를 지키는 CSV 읽기(원작 아트 칸에 쉼표·따옴표가 섞인다).
    static List<string[]> ReadCsv(IEnumerable<string> lines)
    {
        var rows = new List<string[]>();
        foreach (string line in lines)
        {
            var cells = new List<string>();
            var cur = new StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (q)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                    else if (c == '"') q = false;
                    else cur.Append(c);
                }
                else if (c == '"') q = true;
                else if (c == ',') { cells.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
            cells.Add(cur.ToString());
            rows.Add(cells.ToArray());
        }
        return rows;
    }
}
