using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 영원함 김정래 실측(10-06 구현담당3) — gameshot: call:EternalJeonglaeProbe.Setup wait:4 call:EternalJeonglaeProbe.Report (신 모드).
/// 오라(아군 공속·공격력, 적 방깍 −30), 최고의연설(아군 공격력 +20%·적 방깍 −9), 카리스마(회유 시전 → 회유 유닛 수·피해 계수 +0.1%/기), 언변 셋의 시전 결과, 식·입력말.
/// </summary>
public static class EternalJeonglaeProbe
{
    static UnitAttacker a, ally;
    static EnemyDummy boss;
    static float armorBefore, allyAdBefore, allySpeedBefore;

    static EnemyDummy Make(string asset, Vector3 at)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{asset}.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, 1e3f); d.SetLane(-1);
        return d;
    }

    public static string Setup()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/영원_김정래.asset");
        UnitData other = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/희귀함_구주호.asset");
        ally = spawner.Spawn(other, LaneMarker.Get(0).TakeSpawnPosition(other), 0).GetComponent<UnitAttacker>();
        allyAdBefore = ally.AttackDamage; allySpeedBefore = ally.CurrentAttackSpeedMultiplier;
        a = spawner.Spawn(d, LaneMarker.Get(0).TakeSpawnPosition(d), 0).GetComponent<UnitAttacker>();
        boss = Make("Enemy_R60_정윤식", a.transform.position + Vector3.forward * 40f);
        armorBefore = boss.EffectiveArmor;
        return "   세움: 김정래 + 아군 구주호 + 보스(오라 확인용)";
    }

    static void Cast(SkillData s, EnemyDummy t)
    {
        typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, new object[] { s.levels[0], s.levels[0].WorldRange, t, 0f });
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        UnitData d = a.GetComponent<UnitIdentity>().Data;
        sb.AppendLine($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + (s.skillName.Contains("—") ? "·" + s.skillName.Split('—')[1].Trim().Split(' ')[0] : "")))} · 마나 {d.manaMax}");
        sb.AppendLine($"   오라(0.25초 틱 뒤): 아군 공속 ×{allySpeedBefore:F2} → ×{ally.CurrentAttackSpeedMultiplier:F2}(기대 ×1.15) · 아군 공격력 {allyAdBefore:F0} → {ally.AttackDamage:F0}(기대 ×1.10) · 보스 방어 {armorBefore:F1} → {boss.EffectiveArmor:F1}(기대 −30)");

        var list = (System.Collections.IEnumerable)typeof(UnitAttacker).GetField("auraBonuses", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ally);
        var dump = new StringBuilder();
        foreach (object b in list) { var t0 = b.GetType(); dump.Append($"[{t0.GetField("kind").GetValue(b)} {t0.GetField("id").GetValue(b)} {t0.GetField("value").GetValue(b)}] "); }
        sb.AppendLine($"   아군 구주호 오라 목록: {dump}");
        SkillData speech = d.skills.First(s => s.skillName.StartsWith("최고의연설"));
        float adNow = ally.AttackDamage, armorNow = boss.EffectiveArmor;
        Cast(speech, boss);
        sb.AppendLine($"   최고의연설 시전: 아군 공격력 {adNow:F0} → {ally.AttackDamage:F0}(기대 ×{(1.20f + 0.10f) / 1.10f:F2}: +20%가 더해짐) · 보스 방어 {armorNow:F1} → {boss.EffectiveArmor:F1}(기대 −9)");

        SkillData charisma = d.skills.First(s => s.skillName.StartsWith("카리스마"));
        var factor = typeof(UnitAttacker).GetMethod("DamagePassiveFactor", BindingFlags.NonPublic | BindingFlags.Instance);
        float f0 = (float)factor.Invoke(a, new object[] { boss });
        EnemyDummy mobA = Make("Enemy_R45_이현빈", a.transform.position + Vector3.forward * 25f), mobB = Make("Enemy_R45_이현빈", a.transform.position + Vector3.back * 25f);
        Cast(charisma, mobA); Cast(charisma, mobB);
        float f2 = (float)factor.Invoke(a, new object[] { boss });
        var recruits = (System.Collections.IList)typeof(UnitAttacker).GetField("recruits", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
        sb.AppendLine($"   카리스마(회유 시전 2회): 회유 유닛 {recruits.Count}기 · 피해 계수 {f0:F3} → {f2:F3}(기대 +0.002/2기) · 회유 대상 적 둘 {(mobA == null || mobA.IsDead || !mobA.gameObject.activeInHierarchy ? "사라짐" : "남음")}/{(mobB == null || mobB.IsDead || !mobB.gameObject.activeInHierarchy ? "사라짐" : "남음")}");

        EnemyDummy t = Make("Enemy_R45_이현빈", a.transform.position + Vector3.left * 30f);
        foreach (string key in new[] { "언변 — 발동 스턴", "언변 — 발동 이감" }) Cast(d.skills.First(s => s.skillName == key), t);
        sb.AppendLine($"   언변 스턴·이감 시전: 스턴 {t.IsStunned} · 남는 속도 {t.EffectiveSlowMultiplier:F2}(기대 True / 0.50)");
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/영원_김정래.asset");
        sb.AppendLine($"   식: 재료 {recipe.ingredients.Count}종: {string.Join(", ", recipe.ingredients.Select(i => i.unit != null ? i.unit.name : "빈칸"))} · 입력말 「{recipe.chatPhrase}」 · IsChatOnly {CombineSystem.IsChatOnly(recipe)}");
        return sb.ToString();
    }
}
