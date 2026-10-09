using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 원작 대응 이름 채우기(사장님 10-07 「누구 매칭인지 보이게」) — Docs/research/ORIGINAL_MATCH_NAMES_2026-10-09.tsv(로스터\t원작 유닛\t원작 이름)를 UnitData.originalMatchName에 쓴다.
/// 호출: call OriginalMatchApply.Apply (다시 불러도 안전 — 표에 없는 유닛의 값은 그대로 둔다).
/// </summary>
static class OriginalMatchApply
{
    const string TablePath = "Docs/research/ORIGINAL_MATCH_NAMES_2026-10-09.tsv";

    static string Apply()
    {
        if (!File.Exists(TablePath)) return "❌ 표 없음 " + TablePath;
        int set = 0, same = 0, missing = 0;
        string[] lines = File.ReadAllLines(TablePath);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] c = lines[i].Split('\t');
            if (c.Length < 3) continue;
            var unit = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{c[0]}.asset");
            if (unit == null) { missing++; continue; }
            if (unit.originalMatchName == c[2]) { same++; continue; }
            unit.originalMatchName = c[2];
            EditorUtility.SetDirty(unit);
            set++;
        }
        AssetDatabase.SaveAssets();
        return $"원작 대응 이름: 새로 {set} · 그대로 {same} · 에셋 없음 {missing}";
    }
}
