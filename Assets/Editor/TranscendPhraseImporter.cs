using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 수식어(채팅 코드) 일괄 쓰기(2026-10-06) — Tools/transcend_phrases.csv(조합식 에셋 이름, 결과 유닛 이름, commandId, chatPhrase, 출처)를 읽어
/// 초월 조합식의 `CombineRecipe.chatPhrase`에 쓴다. 채팅에 이 문구(공백 무시)를 치면 그 식이 조합된다(CombineSystem.TryCombineByChat).
/// 임시 값은 원작 수식어(war3map.j Eternal_* 한글 문구 27개, Nika 제외)를 고정 시드(20261006)로 섞어 배정했다 — 출처 열 「원작 임시」.
/// 사장님 표가 오면 CSV의 chatPhrase 열을 고치고 출처를 「사장님」으로 바꾼 뒤 Apply 한 번이면 된다. chatPhrase 칸을 비우면 그 식은 영문 코드(commandId)만 받는다.
/// 같은 문구가 두 식에 걸리거나(채팅이 모호) 다른 조합식의 코드와 겹치면 그 줄은 쓰지 않고 알린다.
/// 호출: call TranscendPhraseImporter.Apply (또는 메뉴 Tools/유닛/초월 수식어 적용). 다시 불러도 안전하다.
/// </summary>
public static class TranscendPhraseImporter
{
    const string CsvPath = "Tools/transcend_phrases.csv";
    const string RecipeFolder = "Assets/Data/Recipes";

    [MenuItem("Tools/유닛/초월 수식어 적용")]
    static void ApplyMenu() => Debug.Log(Apply());

    static string Norm(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(" ", "").Replace("\t", "").ToLowerInvariant();

    static string Apply()
    {
        if (!File.Exists(CsvPath)) return $"⚠️ {CsvPath}가 없습니다.";

        var recipes = new Dictionary<string, CombineRecipe>();
        foreach (string guid in AssetDatabase.FindAssets("t:CombineRecipe", new[] { RecipeFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CombineRecipe recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(path);
            if (recipe != null) recipes[Path.GetFileNameWithoutExtension(path)] = recipe;
        }

        // 다른 식이 이미 쓰는 문구(commandId 양쪽) — 수식어가 이것과 겹치면 모호하다.
        var taken = new Dictionary<string, string>();
        foreach (var pair in recipes)
        {
            if (string.IsNullOrEmpty(pair.Value.commandId)) continue;
            foreach (string part in pair.Value.commandId.Split('/'))
            {
                string p = Norm(part);
                if (p.Length > 0 && !taken.ContainsKey(p)) taken[p] = pair.Key;
            }
        }

        List<List<string>> rows = ParseCsv(File.ReadAllText(CsvPath, Encoding.UTF8));
        var seen = new Dictionary<string, string>();
        int written = 0, cleared = 0, unchanged = 0, noRecipe = 0, nonTranscend = 0, clash = 0;
        var problems = new List<string>();
        for (int i = 1; i < rows.Count; i++)   // 0번은 머리글
        {
            List<string> row = rows[i];
            if (row.Count < 4 || row[0].Length == 0) continue;
            if (!recipes.TryGetValue(row[0], out CombineRecipe recipe)) { noRecipe++; problems.Add($"{row[0]}: 식 없음"); continue; }
            if (recipe.result == null || recipe.result.grade != UnitGrade.Transcendent) { nonTranscend++; problems.Add($"{row[0]}: 초월 식이 아님"); continue; }

            string value = row[3].Trim();
            string key = Norm(value);
            if (key.Length > 0)
            {
                if (seen.TryGetValue(key, out string other)) { clash++; problems.Add($"{row[0]}: 「{value}」가 {other}와 겹침 — 안 씀"); continue; }
                if (taken.TryGetValue(key, out string owner) && owner != row[0]) { clash++; problems.Add($"{row[0]}: 「{value}」가 {owner}의 코드와 겹침 — 안 씀"); continue; }
                seen[key] = row[0];
            }

            if (recipe.chatPhrase == value) { unchanged++; continue; }
            SerializedObject so = new SerializedObject(recipe);
            so.FindProperty("chatPhrase").stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(recipe);
            if (value.Length > 0) written++; else cleared++;
        }
        AssetDatabase.SaveAssets();
        return $"초월 수식어: 썼음 {written} · 비움 {cleared} · 이미 같음 {unchanged} · 식 없음 {noRecipe} · 초월 아님 {nonTranscend} · 겹침 {clash}" +
               (problems.Count > 0 ? " — " + string.Join(" / ", problems) : "");
    }

    // 따옴표·쉼표·줄바꿈을 아는 최소 CSV 읽기(SkinAliasImporter와 같다).
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
