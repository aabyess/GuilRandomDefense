using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스킨 캐릭터 별칭 일괄 쓰기(2026-10-04) — Tools/skin_alias.csv(유닛 에셋명, 한국어 캐릭터명, 영문/별칭, 근거, 확신도)를 읽어
/// 로스터 UnitData의 `skinAlias`(쉼표 구분)에 쓴다. 조합 검색(「쵸파」「미호크」「고죠」)이 읽는다.
/// 확신도 「높음」·「중간」만 쓴다 — 「모름」은 **비워 둔다**(틀린 이름이 검색에 걸리는 게 더 나쁘다).
/// 호출: call SkinAliasImporter.Apply (또는 메뉴 Tools/유닛/스킨 별칭 적용). 다시 불러도 안전하다.
/// 필드 `public string skinAlias;`는 UnitData에 구현담당2의 recipe-search 브랜치가 선언한다 — 없으면 이 도구가 알리고 멈춘다.
/// </summary>
public static class SkinAliasImporter
{
    const string CsvPath = "Tools/skin_alias.csv";
    const string RosterFolder = "Assets/Data/Units";

    [MenuItem("Tools/유닛/스킨 별칭 적용")]
    static void ApplyMenu() => Debug.Log(Apply());

    static string Apply()
    {
        if (!File.Exists(CsvPath)) return $"⚠️ {CsvPath}가 없습니다.";

        var byName = new Dictionary<string, UnitData>();
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData", new[] { RosterFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (unit != null) byName[Path.GetFileNameWithoutExtension(path)] = unit;
        }

        int written = 0, cleared = 0, unchanged = 0, noUnit = 0, noField = 0;
        var missing = new List<string>();
        List<List<string>> rows = ParseCsv(File.ReadAllText(CsvPath, Encoding.UTF8));
        for (int i = 1; i < rows.Count; i++)   // 0번은 머리글
        {
            List<string> row = rows[i];
            if (row.Count < 5 || row[0].Length == 0) continue;
            if (!byName.TryGetValue(row[0], out UnitData unit)) { noUnit++; missing.Add(row[0]); continue; }

            string value = "";
            if (row[4] == "높음" || row[4] == "중간")
            {
                var names = new List<string>();
                foreach (string raw in (row[1] + "," + row[2]).Split(','))
                {
                    string n = raw.Trim();
                    if (n.Length > 0 && !names.Contains(n)) names.Add(n);
                }
                value = string.Join(",", names);
            }

            SerializedObject so = new SerializedObject(unit);
            SerializedProperty prop = so.FindProperty("skinAlias");
            if (prop == null) { noField++; continue; }
            if (prop.stringValue == value) { unchanged++; continue; }
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unit);
            if (value.Length > 0) written++; else cleared++;
        }
        AssetDatabase.SaveAssets();
        if (noField > 0 && written + cleared + unchanged == 0)
            return "⚠️ UnitData에 skinAlias 필드가 없습니다 — 구현담당2의 recipe-search 브랜치(필드 선언)를 먼저 병합하세요.";
        return $"스킨 별칭: 썼음 {written} · 비움({"모름"}) {cleared} · 이미 같음 {unchanged} · 유닛 없음 {noUnit}" +
               (noField > 0 ? $" · 필드 없음 {noField}" : "") + (missing.Count > 0 ? $" — {string.Join(", ", missing)}" : "");
    }

    // 따옴표·쉼표·줄바꿈을 아는 최소 CSV 읽기.
    static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else quoted = false; }
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add(row); row = new List<string>();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
