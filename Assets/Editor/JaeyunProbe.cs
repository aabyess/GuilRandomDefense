using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 이재윤 「초특급인싸」 점검(10-06) — gameshot:
//   call:JaeyunProbe.Arena                          이재윤(내 레인 가운데) + 아군 1기 + 0번 레인 표적 + 1번 레인 표적(남의 레인)
//   call:JaeyunProbe.Fill59 | Fill60 | Fill70 | Fill71 + wait:3 + call:JaeyunProbe.Report   레인 적 수를 그 값에 맞추고 오라 켜짐/꺼짐·방어·이속·아군 공격력 확인
//   call:JaeyunProbe.GivePlane → call:JaeyunProbe.UsePlane → call:JaeyunProbe.UsePlane  종이비행기: 영구 정지 · 레인 −25% · 남의 레인 불변 · 두 번째 사용 거부
static class JaeyunProbe
{
    static UnitIdentity jaeyun, ally;
    static readonly List<EnemyDummy> filler = new List<EnemyDummy>();
    static EnemyDummy probeTarget, otherLaneTarget;
    static EnemyData dummyData;
    static LaneMarker lane;
    static float baseArmor;
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static EnemyDummy MakeDummy(Vector3 position, int laneIndex)
    {
        GameObject go = Object.Instantiate(dummyData.prefab, position, Quaternion.Euler(0f, 180f, 0f));
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        var e = go.GetComponent<EnemyDummy>();
        e.Initialize(dummyData, 1e3f);
        e.SetLane(laneIndex);
        return e;
    }

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        lane = LaneMarker.Get(0);
        dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        UnitData d = Roster("초월_이재윤_AD"), allyData = Roster("흔함_강재규");
        if (spawner == null || lane == null || dummyData == null || d == null) return "❌ 준비 안 됨";
        Vector3 c = lane.LaneCenter;
        jaeyun = spawner.Spawn(d, c, 0).GetComponent<UnitIdentity>();
        ally = spawner.Spawn(allyData, c + new Vector3(60f, 0f, 0f), 0).GetComponent<UnitIdentity>();
        probeTarget = MakeDummy(c + new Vector3(-60f, 0f, 90f), 0);
        otherLaneTarget = MakeDummy(c + new Vector3(-90f, 0f, 90f), 1);
        baseArmor = probeTarget.EffectiveArmor;
        filler.Clear();
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        if (sel != null && jaeyun.TryGetComponent(out Selectable s)) sel.SelectOnly(s);
        return $"세움: {jaeyun.Data.DisplayName} 스킬 {jaeyun.Data.skills.Count}개 [{string.Join(", ", jaeyun.Data.skills.Select(k => k.skillName.Split('—')[0].Trim()))}] · 이동 {jaeyun.Data.movementAbility} · trait {(jaeyun.Data.trait == null ? "없음" : jaeyun.Data.trait.name)} · 레인0 적 수 {RoundManager.LaneEnemyCount(0)} · 패배 한계 {Object.FindFirstObjectByType<RoundManager>().EnemyCountLimit} · 표적 방어 기준 {baseArmor:F1}";
    }

    static string FillTo(int target)
    {
        if (lane == null) return "❌ Arena 먼저";
        filler.RemoveAll(e => e == null);
        // 기준: 표적 하나(probeTarget)는 이미 0번 레인에 있다. 실제 레인 적 수가 target이 되도록 채우기 용 더미를 늘리거나 줄인다.
        int current = RoundManager.LaneEnemyCount(0);
        int need = target - current;
        Vector3 c = lane.LaneCenter;
        while (need > 0) { filler.Add(MakeDummy(c + new Vector3(Random.Range(-100f, 100f), 0f, Random.Range(120f, 200f)), 0)); need--; }
        while (need < 0 && filler.Count > 0) { Object.Destroy(filler[filler.Count - 1].gameObject); filler.RemoveAt(filler.Count - 1); need++; }
        return $"채움 → 목표 {target} (현재 집계 {current}, 채움용 더미 {filler.Count}기{(need < 0 ? " · ⚠️ 줄일 더미가 모자람 — 실제 적이 더 많다" : "")})";
    }

    static string Fill59() => FillTo(59);
    static string Fill60() => FillTo(60);
    static string Fill70() => FillTo(70);
    static string Fill71() => FillTo(71);

    static string Report()
    {
        if (jaeyun == null) return "❌ Arena 먼저";
        var sb = new StringBuilder();
        int limit = Object.FindFirstObjectByType<RoundManager>().EnemyCountLimit;
        int count = RoundManager.LaneEnemyCount(0);
        bool inWindow = count >= limit - 10 && count <= limit;
        sb.AppendLine($"   레인0 적 수 {count} / 한계 {limit} → 창 [{limit - 10}, {limit}] 안: {inWindow}");
        sb.AppendLine($"   표적(내 레인) 방어 {probeTarget.EffectiveArmor:F1} (기준 {baseArmor:F1}, 차이 {probeTarget.EffectiveArmor - baseArmor:F1}) · 이속 배율 {probeTarget.EffectiveSlowMultiplier:F2}");
        sb.AppendLine($"   표적(남의 1번 레인) 방어 {otherLaneTarget.EffectiveArmor:F1} · 이속 배율 {otherLaneTarget.EffectiveSlowMultiplier:F2}  ← 둘 다 기준과 같아야 한다");
        UnitAttacker allyAtk = ally.GetComponent<UnitAttacker>();
        sb.AppendLine($"   아군(강재규 흔함) 공격력 {allyAtk.AttackDamage:N0} (에셋 {ally.Data.attackPower:N0}, 비 {allyAtk.AttackDamage / ally.Data.attackPower:F2}) · 이재윤 공격력 {jaeyun.GetComponent<UnitAttacker>().AttackDamage:N0}");
        return sb.ToString();
    }

    // 비행(원숭이의민첩함, FlyingMover) 점검 — 바다 NavMesh 위 한 점을 찾아 이재윤(Flying)과 지상 아군(대조)에게 실제 우클릭 이동과 같은 경로(UnitMover.MoveToGroundPoint)로
    // 명령을 내린다. Report 뒤 wait 후 MoveReport. 이어서 FlyStop(S) · FlyHold(H) · FlyAttackMove(A+땅) · FlyFar(맵 밖 점 → 범위 자름)로 복귀 확인.
    static Vector3 seaPoint;
    static string MoveSea()
    {
        if (jaeyun == null) return "❌ Arena 먼저";
        int seaArea = UnityEngine.AI.NavMesh.GetAreaFromName("Sea");
        if (seaArea < 0) return "❌ Sea 영역 없음";
        Vector3 c = lane.LaneCenter;
        bool found = false;
        seaPoint = c;
        foreach (Vector3 off in new[] { new Vector3(0f, 0f, -700f), new Vector3(0f, 0f, 700f), new Vector3(-700f, 0f, 0f), new Vector3(700f, 0f, 0f), new Vector3(0f, 0f, -1100f), new Vector3(-1100f, 0f, 0f) })
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(c + off, out UnityEngine.AI.NavMeshHit hit, 400f, 1 << seaArea)) { seaPoint = hit.position; found = true; break; }
        }
        if (!found) return "❌ 레인 근처 바다 NavMesh 점을 못 찾음";
        string r = $"바다 점 {seaPoint:F0} (레인 중심에서 {Vector3.Distance(c, seaPoint):F0})";
        foreach (var pair in new[] { ("이재윤(비행)", jaeyun), ("강재규(지상, 대조)", ally) })
        {
            pair.Item2.GetComponent<UnitMover>().MoveToGroundPoint(seaPoint, "탐침");
            var agent = pair.Item2.GetComponent<UnityEngine.AI.NavMeshAgent>();
            r += $"\n   {pair.Item1}: 명령함 · 에이전트 켜짐 {agent.enabled} · FlyingMover {pair.Item2.GetComponent<FlyingMover>() != null}";
        }
        return r;
    }

    static string Where(UnitIdentity u)
    {
        var fm = u.GetComponent<FlyingMover>();
        var agent = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
        string v = fm != null ? $"비행 속도 {fm.Velocity.magnitude:F0} 도착 {fm.HasArrived}" : (agent.isActiveAndEnabled && agent.isOnNavMesh ? $"경로 {agent.pathStatus} 속도 {agent.velocity.magnitude:F0}" : "에이전트 꺼짐");
        return $"위치 {u.transform.position:F0}(y {u.transform.position.y:F1}) · 바다 점까지 {Vector3.Distance(u.transform.position, seaPoint):F0} · {v}";
    }

    static string MoveReport()
    {
        if (jaeyun == null) return "❌ Arena 먼저";
        return $"   이재윤(비행) {Where(jaeyun)}\n   강재규(지상) {Where(ally)}";
    }

    static string FlyStop() { jaeyun.GetComponent<UnitCombat>().Stop(); return "이재윤 S(정지) 호출 · " + Where(jaeyun); }
    static string FlyHold() { jaeyun.GetComponent<UnitCombat>().SetHold(true); return "이재윤 H(홀드) 호출 · " + Where(jaeyun); }
    static string FlyUnhold() { jaeyun.GetComponent<UnitCombat>().SetHold(false); return "이재윤 홀드 해제"; }
    static string FlyAttackMove()
    {
        // 표적 더미(probeTarget)는 레인 안 — 바다 점으로 공격 이동하면 가는 길에 만나는 적을 치고 다시 간다.
        UnitCommands.AttackMove(new List<Selectable> { jaeyun.GetComponent<Selectable>() }, seaPoint);
        return $"이재윤 공격 이동 → 바다 점 · 표적 체력 {probeTarget.Hp:N0}";
    }
    static string FlyFar()
    {
        Vector3 far = lane.LaneCenter + new Vector3(0f, 0f, 99999f);
        jaeyun.GetComponent<UnitMover>().MoveToGroundPoint(far, "탐침(맵 밖)");
        bool ok = RtsCameraController.TryGetWorldBounds(out Vector2 min, out Vector2 max);
        return $"맵 밖 점 {far:F0} 명령 → 범위 {(ok ? $"x {min.x:F0}~{max.x:F0} · z {min.y:F0}~{max.y:F0}" : "못 구함")}";
    }

    static string GivePlane()
    {
        PlayerContext me = PlayerContext.Local;
        ItemData plane = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/ItemData_R002_종이비행기.asset");
        if (me == null || me.ItemInventory == null || plane == null) return "❌ 준비 안 됨";
        me.ItemInventory.Add(plane);
        return $"종이비행기 지급 · 인벤토리 {me.ItemInventory.Items.Count}칸 · 사용 {me.ItemInventory.PaperPlaneUsed}";
    }

    static string UsePlane()
    {
        PlayerContext me = PlayerContext.Local;
        if (me == null || probeTarget == null) return "❌ Arena 먼저";
        var before = new Dictionary<EnemyDummy, float>();
        foreach (EnemyDummy e in EnemyDummy.Active) if (e != null) before[e] = e.Hp;
        bool ok = PaperPlane.TryUse(me, probeTarget, out string reason);
        var sb = new StringBuilder($"   사용 {(ok ? "성공" : "거부: " + reason)}\n");
        if (ok)
        {
            int laneCut = 0, laneUnchanged = 0, otherCut = 0;
            foreach (var pair in before)
            {
                if (pair.Key == null) continue;
                bool cut = pair.Key.Hp < pair.Value * 0.99f;
                if (pair.Key.LaneIndex == 0) { if (cut) laneCut++; else laneUnchanged++; }
                else if (cut) otherCut++;
            }
            sb.AppendLine($"   내 레인(0) 적 {laneCut + laneUnchanged}기 중 체력 깎임 {laneCut} · 안 깎임 {laneUnchanged} · 다른 레인 깎임 {otherCut}(0이어야 함)");
            sb.AppendLine($"   표적 체력 {before[probeTarget]:N0} → {probeTarget.Hp:N0} (비 {probeTarget.Hp / before[probeTarget]:F2}) · 영구 정지 {probeTarget.FrozenForever} · 스턴 상태 {probeTarget.IsStunned}");
        }
        sb.AppendLine($"   사용 완료 표지 {me.ItemInventory.PaperPlaneUsed} · 아이템 남음 {PaperPlane.HasItem(me)}");
        return sb.ToString();
    }
}
