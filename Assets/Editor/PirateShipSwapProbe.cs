using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>[히든]해적선 → 상붕카 교체 점검(10-07) — call PirateShipSwapProbe.Run : 지급처 넷이 가리키는 유닛 이름 + 해적선 참조 남은 곳.</summary>
static class PirateShipSwapProbe
{
    static string Run()
    {
        var sb = new StringBuilder();
        foreach (UnitPortal p in Object.FindObjectsByType<UnitPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var f = typeof(UnitPortal).GetField("bonusUnit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var u = f != null ? f.GetValue(p) as UnitData : null;
            if (u != null) sb.Append($"\n   ① 유닛 포탈 {p.name}: 보너스 유닛 {u.name}({u.unitName}) · 확률 {typeof(UnitPortal).GetField("bonusChancePercent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(p)}%");
        }
        var creep = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_Creep2_노루.asset");
        sb.Append($"\n   ② 크립 2단계 노루: bonusRewardUnit {(creep.bonusRewardUnit != null ? creep.bonusRewardUnit.name : "없음")} · 확률 {creep.bonusRewardChance} · 알림 「{creep.bonusRewardMessage?.Replace("\n", " ")}」");
        var opt = AssetDatabase.LoadAssetAtPath<GamblingOptionData>("Assets/Data/Gambling/Gambling_중급도박.asset");
        sb.Append($"\n   ③ 중급도박: bonusUnit {(opt.bonusUnit != null ? opt.bonusUnit.name : "없음")} · {opt.bonusChancePercent}% · 설명 「{opt.description}」");
        var ship = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Special/Unit_고대의배_h05Y.asset");
        sb.Append($"\n   ④ 고대의 배 도박 능력: {string.Join(", ", ship.gambleOptions.Select(a => a.abilityId + "→" + (a.resultUnit != null ? a.resultUnit.name : "-")))}");
        string pirate = AssetDatabase.GUIDFromAssetPath("Assets/Data/Units/Roster/해적선.asset").ToString();
        int left = 0; var who = new System.Collections.Generic.List<string>();
        foreach (string path in AssetDatabase.FindAssets("t:ScriptableObject").Select(AssetDatabase.GUIDToAssetPath))
            if (AssetDatabase.GetDependencies(path, false).Contains("Assets/Data/Units/Roster/해적선.asset") && path != "Assets/Net/NetCatalog.asset") { left++; who.Add(path); }
        sb.Append($"\n   해적선 에셋을 아직 참조하는 데이터: {left}개 {string.Join(", ", who)} (NetCatalog는 전체 목록이라 제외)");
        return sb.ToString();
    }
}
