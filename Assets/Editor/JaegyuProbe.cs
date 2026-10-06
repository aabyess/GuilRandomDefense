using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 초월 강재규 실측(10-06 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:JaegyuProbe.Setup   초월 강재규 + 노태현(아군 디버프 오라 시전자)을 우리에 세운다
///   call:JaegyuProbe.Near    노태현을 강재규 곁으로 Warp(오라 반경 안)
///   call:JaegyuProbe.Report  체력 게이지 · 자기 스턴 · 받는 아군발 디버프 개수 · 피해 증가 배율
///   call:JaegyuProbe.Stun    만성피로를 직접 한 번 터뜨려(자기 스턴 3초) 끝난 뒤 게이지가 가득 차는지 본다
/// </summary>
public static class JaegyuProbe
{
    static UnitAttacker Jae() => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "초월_강재규_AP");
    static UnitAttacker Notae() => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "초월_노태현_AP");

    static void Spawn(string name, StringBuilder sb)
    {
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        sb.AppendLine($"   소환 {name} → {(go != null ? go.name : "null")}");
    }

    public static string Setup() { StringBuilder sb = new StringBuilder(); Spawn("초월_강재규_AP", sb); Spawn("초월_노태현_AP", sb); return sb.ToString(); }

    public static string Near()
    {
        UnitAttacker j = Jae(), n = Notae();
        if (j == null || n == null) return "❌ 유닛 없음";
        bool ok = n.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.Warp(j.transform.position + new Vector3(60f, 0f, 0f));
        return $"   노태현을 강재규 곁으로 Warp {(ok ? "✅" : "❌")}";
    }

    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

    public static string Report()
    {
        UnitAttacker j = Jae();
        if (j == null) return "❌ 강재규 없음";
        UnitData d = j.GetComponent<UnitIdentity>().Data;
        float factor = (float)typeof(UnitAttacker).GetMethod("AllyDebuffDamageFactor", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(j, null);
        return $"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + (s.skillName.Contains('—') ? "·" + s.skillName.Split('—')[1].Trim().Split('(')[0].Trim() : "")))}\n" +
               $"   체력 게이지 {Get<int>(j, "lifeGaugeCounter")}/{d.lifeGaugeMax} · 자기 스턴 {(j.IsSelfStunned ? "중" : "아님")} · 받는 아군발 디버프 {j.CountAllyDebuffs()}개 → 피해 ×{factor:F2}";
    }

    public static string Stun()
    {
        UnitAttacker j = Jae();
        MethodInfo m = typeof(UnitAttacker).GetMethod("BeginSelfStun", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(j, new object[] { 3f, false });
        return "   자기 스턴 3초 시작\n" + Report();
    }

    // ---- 10-06 2차(마나 끝딜 실측, 구현담당3) ----
    static EnemyDummy finisherTarget;

    /// 0번 레인 가운데에 표적 1마리(보스 데이터면 isBoss true)를 세우고 체력을 최대의 40%로(잃은 체력 60%) 만든 뒤 강재규를 곁에 둔다. 이 표적은 움직이지 않는다.
    static string Target(string enemyAsset)
    {
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{enemyAsset}.asset");
        if (lane == null || data == null || data.prefab == null) return $"❌ 레인·적 없음 {enemyAsset}";
        Vector3 c = lane.LaneCenter;
        GameObject go = Object.Instantiate(data.prefab, c + Vector3.forward * 8f, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy dummy = go.GetComponent<EnemyDummy>();
        dummy.Initialize(data, 1e9f); dummy.SetLane(-1);
        typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(dummy, dummy.MaxHp * 0.4f);
        finisherTarget = dummy;
        UnitAttacker j = Jae();
        if (j != null && j.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.Warp(c + Vector3.back * 30f);
        return $"   표적 {enemyAsset}(isBoss {data.isBoss}) 체력 {dummy.Hp:F0}/{dummy.MaxHp:F0} (잃은 체력 {dummy.MaxHp - dummy.Hp:F0})";
    }
    public static string TargetNormal() => Target("Enemy_R45_이현빈");
    public static string TargetBoss() => Target("Enemy_R60_정윤식");

    static int ManaGauge(UnitAttacker a) => (int)typeof(UnitAttacker).GetField("manaGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
    public static string Mana124()
    {
        UnitAttacker j = Jae();
        typeof(UnitAttacker).GetField("manaGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(j, 124);
        typeof(UnitAttacker).GetField("manaGaugeInitialized", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(j, true);
        return "   강재규 마나 게이지 → 124";
    }
    public static string Check()
    {
        UnitAttacker j = Jae();
        return $"   마나 게이지 {ManaGauge(j)}/125 · 체력 게이지 {Get<int>(j, "lifeGaugeCounter")}/40 · 자기 스턴 {(j.IsSelfStunned ? "중" : "아님")} · 표적 체력 {finisherTarget.Hp:F0}/{finisherTarget.MaxHp:F0} (잃은 체력 {finisherTarget.MaxHp - finisherTarget.Hp:F0})";
    }

    /// 마나 124 + 체력 39를 같은 순간에 → 다음 평타 하나에 끝딜(마나)과 간잽이·만성피로(체력)가 동시에 나가고 두 게이지가 서로 안 엉키는지.
    public static string Both()
    {
        UnitAttacker j = Jae();
        foreach (string f in new[] { "manaGaugeInitialized", "lifeGaugeInitialized" }) typeof(UnitAttacker).GetField(f, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(j, true);
        typeof(UnitAttacker).GetField("manaGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(j, 124);
        typeof(UnitAttacker).GetField("lifeGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(j, 39);
        return "   강재규 마나 게이지 → 124 · 체력 게이지 → 39";
    }
}
