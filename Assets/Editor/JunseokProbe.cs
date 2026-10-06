using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 황준석 「공짜집착증」 점검(10-06) — gameshot:
//   call:JunseokProbe.Before → call:JunseokProbe.Arena → wait:2 → call:JunseokProbe.Report → call:JunseokProbe.Remove → wait:1 → call:JunseokProbe.AfterRemove
static class JunseokProbe
{
    static UnitIdentity unit, near, far;
    static EnemyDummy normal, story;
    static float goldPlusBefore;
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static EnemyDummy MakeDummy(EnemyData ed, Vector3 position)
    {
        GameObject go = Object.Instantiate(ed.prefab, position, Quaternion.Euler(0f, 180f, 0f));
        if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
        var e = go.GetComponent<EnemyDummy>();
        e.Initialize(ed, 1e3f);
        e.SetLane(0);
        return e;
    }

    static string Before()
    {
        PlayerContext me = PlayerContext.Local;
        if (me == null || me.GoldWallet == null) return "❌ PlayerContext 없음";
        goldPlusBefore = me.GoldWallet.GoldPlus;
        return $"소환 전 GoldPlus {goldPlusBefore:F2}";
    }

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var all = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" }).Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g))).Where(e => e != null && e.prefab != null).ToList();
        EnemyData normalData = all.FirstOrDefault(e => !e.isBoss && e.pointValue < 200f && e.name.Contains("R2"));
        EnemyData storyData = all.FirstOrDefault(e => e.pointValue >= 200f);
        UnitData d = Roster("초월_황준석_ADAP"), a = Roster("흔함_강재규");
        if (spawner == null || lane == null || normalData == null || storyData == null || d == null) return $"❌ 준비 안 됨(일반 {normalData != null} · PV≥200 {storyData != null})";
        Vector3 c = lane.LaneCenter;
        unit = spawner.Spawn(d, c, 0).GetComponent<UnitIdentity>();
        near = spawner.Spawn(a, c + new Vector3(60f, 0f, 0f), 0).GetComponent<UnitIdentity>();
        far = spawner.Spawn(a, c + new Vector3(0f, 0f, -1500f), 0).GetComponent<UnitIdentity>();   // 오라 반경 5000 안·화면 밖
        normal = MakeDummy(normalData, c + new Vector3(-40f, 0f, 80f));
        story = MakeDummy(storyData, c + new Vector3(-80f, 0f, 80f));
        return $"세움: {unit.Data.DisplayName} 스킬 {unit.Data.skills.Count}개 · 이동 {unit.Data.movementAbility} · 비행컴포넌트 {unit.GetComponent<FlyingMover>() != null} · 일반 적 {normalData.name} PV {normalData.pointValue} · PV≥200 적 {storyData.name} PV {storyData.pointValue} 보스 {storyData.isBoss}";
    }

    static string Report()
    {
        if (unit == null) return "❌ Arena 먼저";
        PlayerContext me = PlayerContext.Local;
        var sb = new StringBuilder();
        sb.AppendLine($"   ① GoldPlus {goldPlusBefore:F2} → {me.GoldWallet.GoldPlus:F2} (차이 {me.GoldWallet.GoldPlus - goldPlusBefore:F2}, 기대 +0.20)");
        float baseSpeed = unit.Data.attackSpeed;
        var u = unit.GetComponent<UnitAttacker>(); var n = near.GetComponent<UnitAttacker>(); var f = far.GetComponent<UnitAttacker>();
        sb.AppendLine($"   ② 공속 배율 황준석 {u.CurrentAttackSpeedMultiplier:F3} · 가까운 아군 {n.CurrentAttackSpeedMultiplier:F3} · 1500 떨어진 아군 {f.CurrentAttackSpeedMultiplier:F3}  ← 셋 다 1.20이어야 한다");
        MethodInfo m = typeof(UnitAttacker).GetMethod("StoryDamageFactor", BindingFlags.Instance | BindingFlags.NonPublic);
        float onStory = m != null ? (float)m.Invoke(u, new object[] { story }) : -1f;
        float onNormal = m != null ? (float)m.Invoke(u, new object[] { normal }) : -1f;
        sb.AppendLine($"   ③ 스토리 피해 배율 PV≥200 적 {onStory:F2}(기대 1.30) · 일반 적 {onNormal:F2}(기대 1.00)");
        var agent = unit.GetComponent<UnityEngine.AI.NavMeshAgent>();
        sb.AppendLine($"   ④ 비행: 에이전트 켜짐 {agent.enabled} · FlyingMover {unit.GetComponent<FlyingMover>() != null} · 이동 {unit.Data.movementAbility}");
        return sb.ToString();
    }

    static string Remove()
    {
        Object.Destroy(unit.gameObject);
        return "황준석 제거(파괴)";
    }

    static string AfterRemove()
    {
        PlayerContext me = PlayerContext.Local;
        return $"   제거 뒤 GoldPlus {me.GoldWallet.GoldPlus:F2} (소환 전 {goldPlusBefore:F2} — 같아야 한다, 중복 합산·잔류 없음)";
    }
}
