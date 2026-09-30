using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 「시전자가 사라지면 걸어 둔 스턴이 안 풀린다」 재현(2026-09-30) — 스턴을 매 타 거는 유닛(영원_최상호 A0G3 1.2초)을 세워
// 표적이 스턴에 걸린 걸 확인하고, 유닛을 없앤 뒤(조합·판매·패배 처리와 같은 Destroy) 스턴 시간보다 오래 기다려 다시 읽는다.
// gameshot:
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:CasterVanishProbe.Arena wait:4 call:CasterVanishProbe.Read call:CasterVanishProbe.Kill wait:6 call:CasterVanishProbe.Read
static class CasterVanishProbe
{
    static readonly List<EnemyDummy> targets = new List<EnemyDummy>();
    static GameObject caster;

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.moveSpeed > 0f && e.name.Contains("R2"));
        UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/영원_최상호.asset");
        if (spawner == null || lane == null || dummyData == null || data == null) return "❌ 준비 실패";
        targets.Clear();
        Vector3 home = lane.LaneCenter + Vector3.forward * 2000f;
        for (int i = 0; i < 3; i++)
        {
            GameObject go = Object.Instantiate(dummyData.prefab, home + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 4f, Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            if (go.TryGetComponent(out EnemyDummy dummy)) { dummy.Initialize(dummyData, 1e6f); dummy.SetLane(-1); targets.Add(dummy); }
        }
        caster = spawner.Spawn(data, home, 0);
        return $"표적 {targets.Count} · 시전자 {(caster != null ? caster.name : "없음")}";
    }

    static string Read() =>
        $"시전자 {(caster != null ? "있음" : "없음")} · " + string.Join(" / ", targets.Select((t, i) => t == null ? $"표적{i} 없음" : $"표적{i} 스턴 {t.IsStunned} · 이감 {t.EffectiveSlowMultiplier:0.00} · 방어 {t.EffectiveArmor:0.0}"));

    static string Kill()
    {
        if (caster == null) return "시전자 없음";
        Object.Destroy(caster);
        return "시전자 Destroy";
    }
}
