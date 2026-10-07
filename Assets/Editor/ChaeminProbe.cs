using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 임채민 청렴결백 실측(10-07) — gameshot call:ChaeminProbe.Setup wait:40 call:ChaeminProbe.Report.
/// 임채민 + 아군 둘(고도현=마나 게이지, 양재모) + 신 보스(R60 정윤식 ×8.25). 아군 하나를 축복의땅으로 지정한 뒤 40초: 예베시간 공속 배율 · 지상낙원 마젠 가동률 ·
/// 축복의땅 체젠 가동률(지정 아군) · 천벌 발동 횟수와 보스 스턴·체력 감소.
/// </summary>
static class ChaeminProbe
{
    static UnitAttacker chaemin, allyA, allyB;
    static EnemyDummy boss;
    static float t0, lastT;
    static float manaOn, lifeOn, dispelOn, dt;
    static readonly MethodInfo auraTotal = typeof(UnitAttacker).GetMethod("AuraBonusTotal", BindingFlags.NonPublic | BindingFlags.Instance);
    static float Total(UnitAttacker a, SkillEffectKind k, bool product) => a == null ? 0f : (float)auraTotal.Invoke(a, new object[] { k, product });

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData bossData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        Vector3 home = lane.LaneCenter + Vector3.forward * 3000f;
        GameObject go = Object.Instantiate(bossData.prefab, home + Vector3.forward * 6f, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        boss = go.GetComponent<EnemyDummy>(); boss.Initialize(bossData, 8.25f); boss.SetLane(-1);
        SkillTelemetry.Reset(); SkillTelemetry.Enabled = true;
        UnitData Load(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
        chaemin = spawner.Spawn(Load("초월_임채민_AP"), home, 0).GetComponent<UnitAttacker>();
        allyA = spawner.Spawn(Load("불멸_고도현"), home + Vector3.right * 2f, 0).GetComponent<UnitAttacker>();
        allyB = spawner.Spawn(Load("초월_양재모_AD"), home + Vector3.left * 2f, 0).GetComponent<UnitAttacker>();
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(rm, false);
        SkillData pick = chaemin.GetComponent<UnitIdentity>().Data.skills.First(s => s != null && s.skillName.Contains("지정"));
        bool ok = chaemin.TryCastActiveOnAlly(pick, allyA.GetComponent<UnitIdentity>(), out string why);
        t0 = lastT = Time.time; manaOn = lifeOn = dispelOn = dt = 0f;
        EditorApplication.update -= Sample; EditorApplication.update += Sample;
        return $"임채민 {chaemin != null} · 아군 {allyA.name}·{allyB.name} · 지정 {(ok ? "성공" : "실패: " + why)} · 보스 체력 {boss.MaxHp:0}";
    }

    static void Sample()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Sample; return; }
        float d = Time.time - lastT; lastT = Time.time; if (d <= 0f || d > 1f) return;
        dt += d;
        if (Total(allyA, SkillEffectKind.ManaRegenBuff, false) > 0f) manaOn += d;
        if (Total(allyA, SkillEffectKind.LifeRegenBuff, false) > 0f) lifeOn += d;
        if (Total(allyA, SkillEffectKind.DispelAllyDebuffs, false) > 0f) dispelOn += d;
    }

    static string Report()
    {
        EditorApplication.update -= Sample;
        var sb = new StringBuilder($"\n게임 시간 {Time.time - t0:0.0}초 · 표본 {dt:0.0}초");
        sb.Append($"\n   예베시간: 고도현 공속 배율 {Total(allyA, SkillEffectKind.AttackSpeedBuffPercent, true):0.000} · 양재모 {Total(allyB, SkillEffectKind.AttackSpeedBuffPercent, true):0.000} · 임채민 자신 {Total(chaemin, SkillEffectKind.AttackSpeedBuffPercent, true):0.000}");
        sb.Append($"\n   지상낙원(마젠4 3초): 고도현 가동률 {manaOn / Mathf.Max(0.1f, dt):P0} · 지금 양재모 마젠 {Total(allyB, SkillEffectKind.ManaRegenBuff, false)} · 임채민 자신 {Total(chaemin, SkillEffectKind.ManaRegenBuff, false)}");
        sb.Append($"\n   축복의땅(지정=고도현): 체젠 가동률 {lifeOn / Mathf.Max(0.1f, dt):P0} · 디버프해제 가동률 {dispelOn / Mathf.Max(0.1f, dt):P0} · 지정 안 한 양재모 체젠 {Total(allyB, SkillEffectKind.LifeRegenBuff, false)}");
        var casts = SkillTelemetry.CastLog.Where(c => c.unit == chaemin.GetComponent<UnitIdentity>().Data).ToList();
        foreach (var g in casts.GroupBy(c => c.skill)) sb.Append($"\n   발동 {g.Key}: {g.Count()}회");
        sb.Append($"\n   보스 남은 체력 {boss.HpRatio:P2}(−{(1f - boss.HpRatio) * 100f:0.00}%) · 스턴 중 {(boss.IsStunned ? "예" : "아니오")}");
        return sb.ToString();
    }
}
