using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 엄태웅 점검(10-06) — gameshot: call:ShopSlotProbe.Fund call:TaewoongProbe.Run
// 스킬 연결·칭호·trait · 「웅교교주」 6번 눌러 엔 소모·최대 5회 · 도박 성공률 가산(100%·0% 제외) · 강도높은트레이너 오라가 곁 아군 공격력에 닿는지.
static class TaewoongProbe
{
    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_엄태웅_AD.asset");
        var friend = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/안흔함_엄태웅.asset");
        var go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0);
        var other = spawner.Spawn(friend, lane != null ? lane.TakeSpawnPosition(friend) : Vector3.zero, 0);
        var sb = new StringBuilder();
        sb.AppendLine($"[엄태웅] 표시 {d.DisplayName} · 스킬 {d.skills.Count}(널 {d.skills.Count(s => s == null)}): " + string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + "(" + s.triggerType + ")")) + $" · trait {(d.trait == null ? "없음" : d.trait.name)}");
        var ctx = PlayerContext.Get(0);
        var hud = Object.FindFirstObjectByType<GameHud>();
        var sel = go.GetComponent<Selectable>();
        int gold0 = ctx.GoldWallet.Gold;
        for (int i = 1; i <= 6; i++)
        {
            hud.ExecuteGambleBoostOn(sel);
            sb.AppendLine($"  눌러 {i}: 횟수 {ctx.GambleBoostCount} · +{ctx.GambleBoostPercent}%p · 엔 {gold0}→{ctx.GoldWallet.Gold}");
        }
        MethodInfo eff = typeof(GamblingShop).GetMethod("EffectiveSuccessChance", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (string n in new[] { "하급도박", "중급도박", "고급도박", "다른세계 도박", "고급 유닛 생성", "목재 구입" })
        {
            var opt = AssetDatabase.LoadAssetAtPath<GamblingOptionData>($"Assets/Data/Gambling/Gambling_{n}.asset");
            if (opt != null) sb.AppendLine($"  도박 {n}: 기본 {opt.successChancePercent}% → {eff.Invoke(null, new object[] { opt, ctx })}%");
        }
        lastAttacker = other.GetComponent<UnitAttacker>();
        sb.AppendLine($"[오라 전] 곁 안흔함_엄태웅 공격력% 보너스 {lastAttacker.PercentAttackPowerBonus:F2}");
        return sb.ToString();
    }
    static UnitAttacker lastAttacker;
    static string Aura() => lastAttacker == null ? "❌ Run 먼저" : $"[오라 후] 곁 안흔함_엄태웅 공격력% 보너스 {lastAttacker.PercentAttackPowerBonus:F2}(기대 0.25 — 반경 안일 때)";
}
