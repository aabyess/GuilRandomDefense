using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상위 등급(초월·전설·불멸·히든) 공격력·공속 원작 재이식 적용(사장님 10-07 「원작 값으로 전부 다시」). 표: Docs/research/ATTACK_REINSTALL_TABLE_2026-10-07.tsv
/// (Tools/attack_reinstall_table.py가 war3map_new.w3u를 직접 디코드해 만든다: 공격력 = ua1b+ua1d×(ua1s+1)/2 · 공속 = 1/ua1c · 대응 없는 유닛은 지금 값을 그 등급 원작 [최소,최대]로 자름).
/// 호출: call AttackReinstallApply.Apply (다시 불러도 안전 — 표 값으로 덮어쓴다). 사거리·스킬·두유찬 공속(구현담당3 값)은 건드리지 않는다. 1/1000 이내 차이는 안 쓴다.
/// </summary>
static class AttackReinstallApply
{
    const string Table = "Docs/research/ATTACK_REINSTALL_TABLE_2026-10-07.tsv";

    static string Apply()
    {
        if (!File.Exists(Table)) return "❌ 표 없음";
        var sb = new StringBuilder();
        int changed = 0, same = 0, missing = 0;
        string[] lines = File.ReadAllLines(Table, Encoding.UTF8);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] c = lines[i].Split('\t');
            if (c.Length < 7) continue;
            var unit = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{c[1]}.asset");
            if (unit == null) { missing++; sb.Append($"없음 {c[1]} · "); continue; }
            float newAp = float.Parse(c[5], System.Globalization.CultureInfo.InvariantCulture);
            float newSp = float.Parse(c[6], System.Globalization.CultureInfo.InvariantCulture);
            bool apChanged = Mathf.Abs(unit.attackPower - newAp) > Mathf.Max(1f, newAp * 0.001f);
            bool spChanged = Mathf.Abs(unit.attackSpeed - newSp) > newSp * 0.001f;
            if (!apChanged && !spChanged) { same++; continue; }
            if (apChanged) unit.attackPower = newAp;
            if (spChanged) unit.attackSpeed = newSp;
            EditorUtility.SetDirty(unit);
            changed++;
        }
        AssetDatabase.SaveAssets();
        return $"공격력·공속 재이식: 바꿈 {changed}기 · 그대로 {same}기 · 에셋 없음 {missing} {sb}";
    }
}
