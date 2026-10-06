using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 비행 유닛(FlyingMover) 공용 탐침(10-06, 설계표 Docs/design/FLYINGMOVER_DESIGN_2026-10-06.md §8) — 넷: 초월 이재윤·초월 황준석·불멸 박은석·불멸 김용태 + 지상 대조 강재규.
// gameshot 예: call:ShopSlotProbe.Fund wait:25 call:FlyProbe.Arena wait:1 call:FlyProbe.MoveSea wait:6 call:FlyProbe.Report
//   이동 중 정지: call:FlyProbe.Stop → Report / 홀드: Hold / 공격 이동: AttackMove / 이감: Slow / 애니: Anim / 순간이동: Teleport / 추적: Chase
static class FlyProbe
{
    static readonly string[] Names = { "초월_이재윤_AD", "초월_황준석_ADAP", "불멸_박은석", "불멸_김용태" };
    static readonly List<UnitIdentity> flyers = new List<UnitIdentity>();
    static UnitIdentity ground, tank;
    static EnemyData dummyData;
    static EnemyDummy chaseTarget;
    static LaneMarker lane;
    static Vector3 seaPoint;
    static readonly Dictionary<UnitIdentity, float> baseSpeed = new Dictionary<UnitIdentity, float>();

    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        lane = LaneMarker.Get(0);
        if (spawner == null || lane == null) return "❌ 준비 안 됨";
        flyers.Clear(); baseSpeed.Clear();
        Vector3 c = lane.LaneCenter;
        int i = 0;
        foreach (string n in Names)
        {
            UnitData d = Roster(n);
            if (d == null) return $"❌ 에셋 없음 {n}";
            var id = spawner.Spawn(d, c + new Vector3(-90f + i * 60f, 0f, 0f), 0).GetComponent<UnitIdentity>();
            flyers.Add(id); i++;
            var a = id.GetComponent<UnityEngine.AI.NavMeshAgent>();
            baseSpeed[id] = a != null ? a.speed : -1f;
        }
        ground = spawner.Spawn(Roster("흔함_강재규"), c + new Vector3(0f, 0f, -60f), 0).GetComponent<UnitIdentity>();
        dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        var sb = new StringBuilder("세움:");
        foreach (var f in flyers) sb.Append($" {f.Data.DisplayName}[이동 {f.Data.movementAbility} · FlyingMover {f.GetComponent<FlyingMover>() != null} · 에이전트 {(f.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)}]");
        return sb.ToString();
    }

    static string MoveSea()
    {
        if (flyers.Count == 0) return "❌ Arena 먼저";
        int seaArea = UnityEngine.AI.NavMesh.GetAreaFromName("Sea");
        Vector3 c = lane.LaneCenter; bool found = false; seaPoint = c;
        foreach (Vector3 off in new[] { new Vector3(0f, 0f, -700f), new Vector3(0f, 0f, 700f), new Vector3(-700f, 0f, 0f), new Vector3(700f, 0f, 0f), new Vector3(0f, 0f, -1100f), new Vector3(-1100f, 0f, 0f) })
            if (UnityEngine.AI.NavMesh.SamplePosition(c + off, out UnityEngine.AI.NavMeshHit hit, 400f, 1 << seaArea)) { seaPoint = hit.position; found = true; break; }
        if (!found) return "❌ 레인 근처 바다 NavMesh 점을 못 찾음";
        foreach (var u in flyers.Concat(new[] { ground })) u.GetComponent<UnitMover>().MoveToGroundPoint(seaPoint, "탐침");
        return $"바다 점 {seaPoint:F0} (레인 중심에서 {Vector3.Distance(c, seaPoint):F0}) — 비행 넷 + 지상 강재규에게 이동 명령";
    }

    static string Where(UnitIdentity u)
    {
        var fm = u.GetComponent<FlyingMover>();
        var agent = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
        string v = fm != null ? $"비행 속도 {fm.Velocity.magnitude:F0} 도착 {fm.HasArrived}" : (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh ? $"경로 {agent.pathStatus} 속도 {agent.velocity.magnitude:F0}" : "에이전트 꺼짐");
        return $"위치 {u.transform.position:F0}(y {u.transform.position.y:F1}) · 바다 점까지 {Vector3.Distance(u.transform.position, seaPoint):F0} · {v}";
    }

    static string Report()
    {
        var sb = new StringBuilder();
        foreach (var f in flyers) sb.AppendLine($"   {f.Data.DisplayName}(비행) {Where(f)}");
        if (ground != null) sb.AppendLine($"   강재규(지상 대조) {Where(ground)}");
        return sb.ToString();
    }

    static IEnumerable<UnitCombat> Combats() => flyers.Select(f => f.GetComponent<UnitCombat>()).Where(c => c != null);
    static string Stop() { foreach (var c in Combats()) c.Stop(); return "S(정지) 넷에게 · " + string.Join(" / ", flyers.Select(Where)); }
    static string Hold() { foreach (var c in Combats()) c.SetHold(true); return "H(홀드) 넷에게"; }
    static string Unhold() { foreach (var c in Combats()) c.SetHold(false); return "홀드 해제"; }
    static string AttackMove()
    {
        UnitCommands.AttackMove(flyers.Select(f => f.GetComponent<Selectable>()).ToList(), seaPoint);
        return "공격 이동 → 바다 점(넷)";
    }
    static string Gather()
    {
        UnitCommands.Gather(flyers.Select(f => f.GetComponent<Selectable>()).ToList());
        return "모으기(넷)";
    }

    // 이감: 노태현 「아군 이속 감소」 오라 옆에 두고 agent.speed가 내려가는지
    static string Slow()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var d = Roster("초월_노태현_AP");
        var nt = spawner.Spawn(d, lane.LaneCenter + new Vector3(0f, 0f, 40f), 0); tank = nt.GetComponent<UnitIdentity>();
        var sb = new StringBuilder("[이감 점검] 노태현 세움 · agent.speed(기준 → 지금): ");
        foreach (var f in flyers) { var a = f.GetComponent<UnityEngine.AI.NavMeshAgent>(); sb.Append($"{f.Data.DisplayName} {baseSpeed[f]:F0} → {(a != null ? a.speed.ToString("F0") : "-")} / "); }
        sb.Append("노태현 스킬: " + string.Join(",", tank.Data.skills.Select(k => k.skillName.Split('—')[0].Trim())));
        return sb.ToString();
    }
    static string SlowReport()
    {
        var sb = new StringBuilder("[이감 뒤] 강재규(지상) agent.speed " + ground.GetComponent<UnityEngine.AI.NavMeshAgent>().speed.ToString("F0") + " · 노태현 거리 " + Vector3.Distance(tank.transform.position, flyers[0].transform.position).ToString("F0") + " · agent.speed: ");
        foreach (var f in flyers) { var a = f.GetComponent<UnityEngine.AI.NavMeshAgent>(); sb.Append($"{f.Data.DisplayName} {baseSpeed[f]:F0} → {(a != null ? a.speed.ToString("F0") : "-")} / "); }
        return sb.ToString();
    }

    static string DumpBonuses(System.Collections.IList list)
    {
        var sb = new StringBuilder("[");
        foreach (var o in list)
        {
            var t = o.GetType();
            sb.Append($"{t.GetField("kind").GetValue(o)}:{t.GetField("id").GetValue(o)}={t.GetField("value").GetValue(o):F2} ");
        }
        return sb.Append("]").ToString();
    }
    // 이재윤의 「디버프해제」 오라(JAEYUN_DISPEL)가 아군발 디버프를 통째로 무시시킨다 — 이감 점검 전에 이재윤을 치운다
    static string RemoveJaeyun()
    {
        var j = flyers[0]; flyers.RemoveAt(0); baseSpeed.Remove(j);
        Object.Destroy(j.gameObject);
        return "이재윤 제거(디버프해제 오라 끔)";
    }
    static string AuraDump()
    {
        var f = typeof(UnitAttacker).GetField("auraBonuses", BindingFlags.NonPublic | BindingFlags.Instance);
        var sb = new StringBuilder("[오라 레지스트리] ");
        foreach (var u in flyers.Concat(new[] { ground }))
        {
            var list = (System.Collections.IList)f.GetValue(u.GetComponent<UnitAttacker>());
            sb.Append($"{u.Data.DisplayName}: {list.Count}건 {DumpBonuses(list)} / ");
        }
        var nt = tank.GetComponent<UnitAttacker>();
        var skill = tank.Data.skills.First(k => k.skillName.StartsWith("반사회적인격"));
        sb.Append($"| 노태현 반사회적인격 트리거 {skill.triggerType} 범위 {skill.levels[0].range} 효과 {skill.levels[0].effects.Count}개 kind {skill.levels[0].effects[0].kind} target {skill.levels[0].effects[0].target} · WorldRange {skill.levels[0].WorldRange:F0}");
        return sb.ToString();
    }

    static string Anim()
    {
        var sb = new StringBuilder("[애니] ");
        foreach (var f in flyers.Concat(new[] { ground }))
        {
            var an = f.GetComponentsInChildren<Animator>().FirstOrDefault(x => x.gameObject.activeInHierarchy);
            if (an == null || an.runtimeAnimatorController == null) { sb.Append($"{f.Data.DisplayName} Animator 없음 / "); continue; }
            float sp = -1f; foreach (var p in an.parameters) if (p.name == "Speed") sp = an.GetFloat("Speed");
            var info = an.GetCurrentAnimatorClipInfo(0);
            sb.Append($"{f.Data.DisplayName} Speed {sp:F2} 클립 {(info.Length > 0 ? info[0].clip.name : "-")} / ");
        }
        return sb.ToString();
    }

    // 순간이동(TeleportToPoint) — 박은석 「구일의주인」을 비행 상태에서 쓴다
    static string Teleport()
    {
        var f = flyers.First(x => x.Data.name.Contains("박은석"));
        var skill = f.Data.skills.First(s => s.skillName.StartsWith("구일의주인"));
        Vector3 before = f.transform.position;
        Vector3 to = lane.LaneCenter + new Vector3(250f, 0f, 250f);
        bool ok = f.GetComponent<UnitAttacker>().TryCastActiveAtPoint(skill, to, out string why);
        return $"[박은석 순간이동] {(ok ? "성공" : "실패 " + why)} · {before:F0} → {f.transform.position:F0} (목표 {to:F0}) · 바다 점으로도: " + TeleportSea(f, skill);
    }
    static string TeleportSea(UnitIdentity f, SkillData skill)
    {
        var st = typeof(UnitAttacker).GetMethod("GetRuntimeState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(f.GetComponent<UnitAttacker>(), new object[] { skill });
        st.GetType().GetField("activeReadyAt", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(st, 0f);
        Vector3 before = f.transform.position;
        bool ok = f.GetComponent<UnitAttacker>().TryCastActiveAtPoint(skill, seaPoint, out string why);
        return $"{(ok ? "성공" : "실패 " + why)} {before:F0} → {f.transform.position:F0}(바다 점 {seaPoint:F0}, y {f.transform.position.y:F1})";
    }

    // 적 추적: 사거리 밖(그러나 인식 안)에 더미 적을 세워 비행 유닛이 다가가 사거리 안에서 멈추는지
    static string Chase()
    {
        GameObject go = Object.Instantiate(dummyData.prefab, lane.LaneCenter + new Vector3(0f, 0f, 110f), Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        chaseTarget = go.GetComponent<EnemyDummy>(); chaseTarget.Initialize(dummyData, 1e7f); chaseTarget.SetLane(0);
        return "추적 표적 세움 " + chaseTarget.transform.position.ToString("F0");
    }
    // 공격 이동 경로 위(바다 쪽 250)에 표적을 세운다 — 비행 넷이 가다 만나 치고, 다 치면 다시 바다 점으로 가는지
    static string OnPath()
    {
        GameObject go = Object.Instantiate(dummyData.prefab, lane.LaneCenter + new Vector3(0f, 0f, -250f), Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        chaseTarget = go.GetComponent<EnemyDummy>(); chaseTarget.Initialize(dummyData, 1e7f); chaseTarget.SetLane(0);
        return $"경로 위 표적 {chaseTarget.transform.position:F0} 체력 {chaseTarget.Hp:N0}";
    }
    static string ChaseReport()
    {
        if (chaseTarget == null) return "표적 사라짐(죽음) · " + string.Join(" / ", flyers.Select(f => f.Data.DisplayName + " 위치 " + f.transform.position.ToString("F0") + " 바다점까지 " + Vector3.Distance(f.transform.position, seaPoint).ToString("F0")));
        var sb = new StringBuilder("[추적] ");
        foreach (var f in flyers) sb.Append($"{f.Data.DisplayName} 표적까지 {Vector3.Distance(f.transform.position, chaseTarget.transform.position):F0}(사거리 {f.Data.attackRange * 1f:F0}) · 표적 체력 {chaseTarget.Hp:N0} / ");
        return sb.ToString();
    }
}
