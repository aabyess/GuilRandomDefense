using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 원작 상시 오라(2026-09-30 영원한 채우기) 실측 — 오라 유닛마다 떨어진 자리에 세우고
// 반경 안 표적·반경 밖 표적의 실효 방어·이감 배수·같은 시험 타격(물리 100만)의 실제 피해를 읽는다.
// 아군 오라는 곁에 세운 흔함 유닛과 멀리 세운 흔함 유닛의 평타 주기·공격력으로 본다.
// Step: 안쪽 표적을 반경 밖으로 옮기고 곁 아군을 멀리 보낸 뒤 다시 읽어 「나가면 풀리는가」를 본다.
// gameshot:
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:AuraProbe.Arena wait:3 call:AuraProbe.Read wait:1 call:AuraProbe.Step wait:3 call:AuraProbe.Read
static class AuraProbe
{
    // 한 배치 = 오라 유닛들(같이 세움). 같은 유닛 둘(버프 ID 같음 → 큰 것 하나)과 다른 둘(합)도 본다.
    static readonly string[][] Groups =
    {
        new[] { "영원_최상호" },                 // A0HP 이감 0.55 + A0EC 방어 −35
        new[] { "영원_윤현모" },                 // A0FF 이감 0.55
        new[] { "영원_조세민" },                 // A0IM 방어 −45 (반경 850)
        new[] { "영원_조세민", "영원_조세민" },   // 같은 버프 → −45 그대로
        new[] { "영원_조세민", "영원_최상호" },   // 다른 버프 → −80
        new[] { "영원_김정래" },                 // A16U 아군·자기 공속 +15%
        new[] { "영원_김정래", "영원_김정래" },   // 같은 버프 → +15% 그대로
        new[] { "영원_서민성" },                 // A0Y2 자신 제외 아군 공격력 +50% · A0YC 자기 +115000
    };
    const string AllyName = "흔함_강주혁";
    const float RingRadius = 2600f;   // 이웃 간격 2π·2600/8 ≈ 2040 ≫ 오라 반경 300(1250÷4.167)
    const float InDistance = 60f, OutDistance = 330f;   // 반경 900 → 216, 850 → 204, 1250 → 300 (세계 거리)

    class Slot
    {
        public string label; public Vector3 home;
        public readonly List<UnitAttacker> casters = new List<UnitAttacker>();
        public EnemyDummy inside, outside; public UnitAttacker nearAlly, farAlly;
    }
    static readonly List<Slot> slots = new List<Slot>();
    static EnemyData dummyData;

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.moveSpeed > 0f && e.name.Contains("R2"));
        UnitData allyData = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{AllyName}.asset");
        if (spawner == null || lane == null || dummyData == null || allyData == null) return "❌ UnitSpawner·레인·표적·아군 데이터 없음";
        slots.Clear();
        for (int g = 0; g < Groups.Length; g++)
        {
            var slot = new Slot { label = string.Join("+", Groups[g]), home = lane.LaneCenter + Quaternion.Euler(0f, g * 360f / Groups.Length, 0f) * Vector3.forward * RingRadius };
            for (int m = 0; m < Groups[g].Length; m++)
            {
                UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Groups[g][m]}.asset");
                GameObject go = data != null ? spawner.Spawn(data, slot.home + Vector3.right * 8f * m, 0) : null;
                if (go != null && go.TryGetComponent(out UnitAttacker a)) slot.casters.Add(a);
            }
            slot.inside = SpawnTarget(slot.home + Vector3.forward * InDistance);
            slot.outside = SpawnTarget(slot.home + Vector3.forward * OutDistance);
            GameObject near = spawner.Spawn(allyData, slot.home + Vector3.back * InDistance, 0);
            GameObject far = spawner.Spawn(allyData, slot.home + Vector3.back * OutDistance, 0);
            if (near != null) near.TryGetComponent(out slot.nearAlly);
            if (far != null) far.TryGetComponent(out slot.farAlly);
            slots.Add(slot);
        }
        return $"배치 {slots.Count} · 표적 {dummyData.name}(방어 {dummyData.armor}, 이속 {dummyData.moveSpeed}) · 안 {InDistance}/밖 {OutDistance}(세계 거리) · 아군 {AllyName}";
    }

    static EnemyDummy SpawnTarget(Vector3 at)
    {
        GameObject go = Object.Instantiate(dummyData.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        if (!go.TryGetComponent(out EnemyDummy dummy)) return null;
        dummy.Initialize(dummyData, 1e6f);
        dummy.SetLane(-1);
        return dummy;
    }

    // 같은 시험 타격(물리 Normal 100만)을 넣고 실제로 깎인 체력. 표적 체력이 ×1e6이라 float 눈금이 1024쯤 — 1000으로 치면 눈금에 묻힌다(첫 판).
    static float TestHit(EnemyDummy d)
    {
        if (d == null) return -1f;
        float before = d.Hp;
        d.TakeDamage(1e6f, DamageType.AD, AttackType.Normal, -1, 0f, false);
        return before - d.Hp;
    }

    static string Enemy(EnemyDummy d) => d == null ? "없음" : $"방어 {d.EffectiveArmor:0.0}(오라 방깎 {d.AuraArmorShred:0.#}) · 이감 {d.EffectiveSlowMultiplier:0.00} · 시험 타격 {TestHit(d):0.0}";
    static string Ally(UnitAttacker a) => a == null ? "없음" : $"평타 주기 {a.AttackInterval:0.0000} · 공격력 {a.AttackDamage:0}";

    static string Read()
    {
        var sb = new StringBuilder();
        foreach (Slot s in slots)
        {
            Vector3 c = s.casters.Count > 0 && s.casters[0] != null ? s.casters[0].transform.position : s.home;
            string Dist(Component x) => x == null ? "-" : Vector3.Distance(x.transform.position, c).ToString("0");
            sb.Append($"\n[{s.label}] 거리(안 표적 {Dist(s.inside)} · 밖 표적 {Dist(s.outside)} · 곁 아군 {Dist(s.nearAlly)} · 먼 아군 {Dist(s.farAlly)})");
            sb.Append($"\n    안 표적: {Enemy(s.inside)} / 밖 표적: {Enemy(s.outside)}");
            sb.Append($"\n    곁 아군: {Ally(s.nearAlly)} / 먼 아군: {Ally(s.farAlly)} / 오라 유닛: {string.Join(" | ", s.casters.Select(Ally))}");
        }
        return sb.ToString();
    }

    // 안쪽 표적과 곁 아군을 반경 밖으로 옮긴다(오라는 다음 틱에 걷혀야 한다).
    static string Step()
    {
        foreach (Slot s in slots)
        {
            if (s.inside != null) s.inside.transform.position = s.home + Vector3.left * (OutDistance + 60f);
            if (s.nearAlly != null)
            {
                if (s.nearAlly.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.enabled = false;
                s.nearAlly.transform.position = s.home + Vector3.right * (OutDistance + 60f);
            }
        }
        return "안 표적·곁 아군을 반경 밖으로 옮김";
    }
}
