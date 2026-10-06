using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>영원함 문필환 실측(10-06 구현담당3) — gameshot: call:EternalPilhwanProbe.Report (신 모드). 광폭화 몹(B06B) 피해 계수, 범퍼 반경, 공용 문 디버프 공속, 식·입력말.</summary>
public static class EternalPilhwanProbe
{
    static EnemyDummy Make(Vector3 at, float hpScale = 1e3f)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, hpScale); d.SetLane(-1);
        return d;
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/영원_문필환.asset");
        float speedBase = d.attackSpeed;
        var a = spawner.Spawn(d, LaneMarker.Get(0).TakeSpawnPosition(d), 0).GetComponent<UnitAttacker>();
        sb.AppendLine($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName))} · 마나 {d.manaMax}");
        var factor = typeof(UnitAttacker).GetMethod("DamagePassiveFactor", BindingFlags.NonPublic | BindingFlags.Instance);
        EnemyDummy plain = Make(a.transform.position + Vector3.forward * 40f), berserk = Make(a.transform.position + Vector3.back * 40f);
        berserk.AddBuff("B06B", 30f);
        sb.AppendLine($"   광폭화: 일반 적 피해 계수 {(float)factor.Invoke(a, new object[] { plain }):F3} · 광폭화 몹(B06B) {(float)factor.Invoke(a, new object[] { berserk }):F3} (기대 1.000 / 1.500)");
        SkillData bite = d.skills.First(s => s.skillName.StartsWith("깨물기"));
        Vector3 c = LaneMarker.Get(0).LaneCenter + Vector3.back * 90f;
        float r = bite.levels[0].WorldRange;
        EnemyDummy t = Make(c), inside = Make(c + Vector3.right * r * 0.5f), outside = Make(c + Vector3.forward * r * 1.6f);
        float tb = t.Hp, ib = inside.Hp, ob = outside.Hp;
        typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, new object[] { bite.levels[0], r, t, 0f });
        sb.AppendLine($"   깨물기(범퍼 현재체력 2%): 표적 {(1 - t.Hp / tb) * 100f:F2}% · 반경 안 {(1 - inside.Hp / ib) * 100f:F2}% · 밖 {(1 - outside.Hp / ob) * 100f:F2}% (기대 ≈2%×상성·방무 / 같은 값 / 0)");
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/영원_문필환.asset");
        sb.AppendLine($"   식: 재료 {recipe.ingredients.Count}종: {string.Join(", ", recipe.ingredients.Select(i => i.unit != null ? i.unit.name : "빈칸"))} · 입력말 「{recipe.chatPhrase}」 · IsChatOnly {CombineSystem.IsChatOnly(recipe)}");
        return sb.ToString();
    }
}
