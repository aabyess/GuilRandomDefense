using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 초월 배성령 실측(10-06 구현담당3) — gameshot: call:SeongryeongProbe.Report (플레이 중, 신 모드).
/// ① 방무뎀: 같은 보스 둘에 같은 피해(AD 평타 경로 isAbilityDamage:false)를 방어 계수 1.0 / 0.5로 → 피해 비율(기대 약 1.76배) + 유닛의 AttackArmorScale(기대 0.5)
/// ② 끝딜: 잃은 체력 50%인 일반 적에 확실한일처리 시전 → 잃은 체력의 5% 감소 ③ 이감: 거짓된마음 → 남는 속도 0.5
/// ④ 순간이동: 암살스킬을 레인 안 지점으로 → 위치 이동·쿨 12초(두 번째 실패)·바다는 실패
/// </summary>
public static class SeongryeongProbe
{
    static EnemyDummy Make(string asset, Vector3 at, float lostFraction = 0f)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{asset}.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, 1e3f); d.SetLane(-1);
        typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(d, d.MaxHp * (1f - lostFraction));
        return d;
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_배성령_AD.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        UnitAttacker a = go.GetComponent<UnitAttacker>();
        sb.AppendLine($"   「{data.unitName}」 스킬 {data.skills.Count}: {string.Join(" / ", data.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · 방무뎀 {data.attackArmorIgnoreRatio} · 마나 {data.manaMax}");
        Vector3 c = LaneMarker.Get(0).LaneCenter + Vector3.back * 60f;

        // ① 방무뎀
        float scale = (float)typeof(UnitAttacker).GetProperty("AttackArmorScale", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
        EnemyDummy b1 = Make("Enemy_R60_정윤식", c), b2 = Make("Enemy_R60_정윤식", c + Vector3.right * 10f);
        float h1 = b1.Hp, h2 = b2.Hp;
        b1.TakeDamage(1e6f, DamageType.AD, AttackType.Normal, 0, armorIgnoreRatio: 0f, isAbilityDamage: false, armorScale: 1f);
        b2.TakeDamage(1e6f, DamageType.AD, AttackType.Normal, 0, armorIgnoreRatio: 0f, isAbilityDamage: false, armorScale: scale);
        float d1 = h1 - b1.Hp, d2 = h2 - b2.Hp;
        sb.AppendLine($"   ① 방무뎀: AttackArmorScale {scale:F2} · 보스 방어 {b1.EffectiveArmor:F1} · 피해 {d1:F0} → {d2:F0} = ×{d2 / d1:F2} (기대 약 ×1.76)");

        // ② 끝딜
        SkillData fin = data.skills.First(s => s.skillName.StartsWith("확실한일처리"));
        EnemyDummy mob = Make("Enemy_R45_이현빈", c + Vector3.forward * 12f, 0.5f);
        float lost = mob.MaxHp - mob.Hp, before = mob.Hp;
        typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, new object[] { fin.levels[0], 1e6f, mob, 0f });
        sb.AppendLine($"   ② 끝딜(잃은 체력 50% 일반 적): 체력 감소 {before - mob.Hp:F0} = 잃은 체력의 {(before - mob.Hp) / lost * 100f:F2}% (기대 5%)");

        // ③ 이감
        SkillData slow = data.skills.First(s => s.skillName.StartsWith("거짓된마음"));
        EnemyDummy mob2 = Make("Enemy_R45_이현빈", c + Vector3.left * 12f);
        typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, new object[] { slow.levels[0], 1e6f, mob2, 0f });
        sb.AppendLine($"   ③ 이감: 남는 속도 {mob2.EffectiveSlowMultiplier:F2} (기대 0.50)");

        // ④ 순간이동
        SkillData tp = data.skills.First(s => s.skillName.StartsWith("암살스킬"));
        Vector3 start = go.transform.position, dest = LaneMarker.Get(0).LaneCenter;
        bool ok1 = a.TryCastActiveAtPoint(tp, dest, out string r1);
        Vector3 moved = go.transform.position;
        bool ok2 = a.TryCastActiveAtPoint(tp, dest + Vector3.right * 40f, out string r2);
        sb.AppendLine($"   ④ 순간이동: 1차 {(ok1 ? "성공" : "실패 " + r1)} · {Vector3.Distance(start, moved):F0} 이동 → 목적지와 거리 {Vector3.Distance(moved, dest):F1} · 곧바로 2차 {(ok2 ? "성공(버그)" : "실패 " + r2)} · 칸 쿨 {tp.levels[0].cooldown}초 · 지점 클릭 필요 {tp.levels[0].needsPointClick}");
        string r3;
        bool ok3 = a.TryCastActiveAtPoint(tp, new Vector3(-100000f, 0f, -100000f), out r3);
        sb.AppendLine($"      맵 밖 먼 곳: {(ok3 ? "성공(NavMesh 가까운 점)" : "실패 " + r3)}");
        return sb.ToString();
    }
}
