using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 영원함 이지원 실측(10-06 구현담당3) — gameshot: call:EternalJiwonProbe.Setup wait:3 call:EternalJiwonProbe.Report (신 모드).
/// 축제개최(마나 스킬)를 표적 중심으로 시전해 표적·반경 안 1·반경 밖 1의 피해/스턴, 평타 수 0 vs 500일 때 피해 비율(기대 ×1.5), 아군 공속 +15%, 전시회 이감 오라, 노획물 지급, 재료·입력말.
/// </summary>
public static class EternalJiwonProbe
{
    static UnitAttacker a, ally;
    static EnemyDummy slowTarget;

    static EnemyDummy Make(Vector3 at)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, 1e3f); d.SetLane(-1);
        return d;
    }

    public static string Setup()
    {
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/영원_이지원.asset");
        UnitData other = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/희귀함_구주호.asset");
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        a = spawner.Spawn(d, LaneMarker.Get(0).TakeSpawnPosition(d), 0).GetComponent<UnitAttacker>();
        ally = spawner.Spawn(other, LaneMarker.Get(0).TakeSpawnPosition(other), 0).GetComponent<UnitAttacker>();
        slowTarget = Make(a.transform.position + Vector3.forward * 40f);   // 전시회 오라 반경(850/4.167≈204) 안
        return "   세움: 이지원 + 아군 구주호 + 이감 확인용 적";
    }

    static float Speed(UnitAttacker u) => (float)typeof(UnitAttacker).GetProperty("AttackSpeedMultiplier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(u);

    static string Cast(SkillData skill, int hits, out float damage, out float stun)
    {
        typeof(UnitAttacker).GetProperty("BasicHitCount").GetSetMethod(true).Invoke(a, new object[] { hits });
        Vector3 c = LaneMarker.Get(0).LaneCenter + Vector3.back * 80f;
        float r = skill.levels[0].WorldRange;
        EnemyDummy t = Make(c), inside = Make(c + Vector3.right * r * 0.5f), outside = Make(c + Vector3.forward * r * 1.6f);
        float before = t.Hp, ib = inside.Hp, ob = outside.Hp;
        typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, new object[] { skill.levels[0], r, t, 0f });
        damage = before - t.Hp;
        stun = t.IsStunned ? 1f : 0f;
        string s = $"표적 {damage:F0} · 안 {ib - inside.Hp:F0} · 밖 {ob - outside.Hp:F0} · 스턴(표적/안/밖) {t.IsStunned}/{inside.IsStunned}/{outside.IsStunned}";
        foreach (var e in new[] { t, inside, outside }) Object.Destroy(e.gameObject);
        return s;
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        UnitData d = a.GetComponent<UnitIdentity>().Data;
        sb.AppendLine($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · 마나 {d.manaMax} · 마젠 {d.manaAuraRegenPerSecond}/초({d.manaAuraRange}) · 스플래시 {d.attackSplashRadius}");
        SkillData fest = d.skills.First(s => s.skillName.StartsWith("축제개최"));
        float speedBefore = Speed(ally);
        string r0 = Cast(fest, 0, out float d0, out _);
        string r500 = Cast(fest, 500, out float d500, out _);
        sb.AppendLine($"   축제개최 평타 0타: {r0}");
        sb.AppendLine($"   축제개최 평타 500타: {r500} → 피해 비 ×{d500 / d0:F2} (기대 ×1.50)");
        sb.AppendLine($"   아군 구주호 공속 ×{speedBefore:F2} → ×{Speed(ally):F2} (기대 ×1.15 곱)");
        sb.AppendLine($"   전시회 이감 오라(0.25초 틱 뒤): 적 남는 속도 {slowTarget.EffectiveSlowMultiplier:F2} (기대 0.70)");
        SkillData trade = d.skills.First(s => s.skillName.StartsWith("작품매매"));
        PlayerContext ctx = PlayerContext.Local;
        int before = ctx.ItemInventory.Items.Count();
        typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, new object[] { trade.levels[0], 0f, null, 0f });
        sb.AppendLine($"   작품매매 시전 1회: 아이템 {before} → {ctx.ItemInventory.Items.Count()} (기대 +1, 노획물 아이템)");
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/영원_이지원.asset");
        sb.AppendLine($"   식: 재료 {recipe.ingredients.Count}종: {string.Join(", ", recipe.ingredients.Select(i => i.unit != null ? i.unit.name : "빈칸"))} · 입력말 「{recipe.chatPhrase}」 · IsChatOnly {CombineSystem.IsChatOnly(recipe)}");
        return sb.ToString();
    }
}
