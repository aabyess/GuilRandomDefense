using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 범퍼(범위 전체 체력 비례) 실측(10-06 구현담당3) — gameshot call:BumperProbe.Minseok / Muncheol / Taehun / Juho 로 부른다(플레이 중).
/// 표적 1 · 반경 안 2(반경 50%) · 반경 밖 1(반경 150%)을 세우고 그 스킬 레벨0을 표적 중심으로 한 번 시전해 체력 감소 %를 낸다.
/// </summary>
public static class BumperProbe
{
    static EnemyDummy Make(Vector3 at)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, 1e3f); d.SetLane(-1);
        return d;
    }

    static string Run(string arg)
    {
        string[] p = arg.Split('|');
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{p[0]}.asset");
        SkillData skill = data.skills.FirstOrDefault(s => s != null && s.skillName.Contains(p[1]));
        if (skill == null) return $"❌ 스킬 없음: {p[1]}";
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        UnitAttacker atk = go.GetComponent<UnitAttacker>();
        SkillLevel level = skill.levels[0];
        float r = level.WorldRange;
        Vector3 c = LaneMarker.Get(0).LaneCenter + Vector3.back * 60f;
        EnemyDummy t = Make(c), a = Make(c + Vector3.right * r * 0.5f), b = Make(c + Vector3.left * r * 0.5f), far = Make(c + Vector3.forward * r * 1.5f);
        var all = new[] { t, a, b, far };
        float[] before = all.Select(e => e.Hp).ToArray();
        typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(atk, new object[] { level, r, t, 0f });
        var sb = new StringBuilder($"   [{data.unitName}] {skill.skillName.Split('—')[0].Trim()} range {level.range}(월드 {r:F1}) 표적중심 1회 시전 → 체력 감소 %: ");
        string[] names = { "표적", "안1", "안2", "밖" };
        for (int i = 0; i < 4; i++) sb.Append($"{names[i]} {(1f - all[i].Hp / before[i]) * 100f:F3}% ");
        foreach (var e in all) Object.Destroy(e.gameObject);
        Object.Destroy(go);
        return sb.ToString();
    }

    public static string Minseok() => Run("초월_박민석_ADAP|흑인");
    public static string Muncheol() => Run("초월_신문철_AP|사고뭉치");
    public static string Taehun() => Run("초월_이태훈_AP|명치적중");
    public static string Juho() => Run("초월_구주호_AD|오라오라");

    // 10-06 채팅 전용 정정 실측 — 등급별 식 수와 IsChatOnly 수(영원·히든·초월·불멸만 전부, 다른세계는 0이어야 한다) + 다른세계 식 하나·영원함 식 하나가 첫 재료 유닛의 조합 버튼 목록에 뜨는지.
    public static string ChatOnly()
    {
        var sb = new StringBuilder();
        var recipes = UnityEditor.AssetDatabase.FindAssets("t:CombineRecipe", new[] { "Assets/Data" }).Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>(UnityEditor.AssetDatabase.GUIDToAssetPath(g))).Where(r => r != null && r.result != null).ToList();
        foreach (var grp in recipes.GroupBy(r => r.result.grade).OrderBy(g => (int)g.Key))
            sb.AppendLine($"   {grp.Key}: 식 {grp.Count()} · 채팅 전용 {grp.Count(CombineSystem.IsChatOnly)}");
        CombineSystem cs = Object.FindFirstObjectByType<CombineSystem>();
        foreach (UnitGrade g in new[] { UnitGrade.OtherWorld, UnitGrade.Eternal })
        {
            CombineRecipe r = recipes.FirstOrDefault(x => x.result.grade == g);
            if (r == null) { sb.AppendLine($"   {g}: 식 없음"); continue; }
            sb.AppendLine($"   {g} 예시 식 「{r.name}」 IsChatOnly={CombineSystem.IsChatOnly(r)}");
            var fu = typeof(CombineSystem).GetMethod("FirstUnitIngredient", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            UnitData first = fu != null ? (UnitData)fu.Invoke(fu.IsStatic ? null : cs, new object[] { r }) : null;
            sb.AppendLine($"      첫 재료 {(first != null ? first.name : "?")}의 조합 버튼 목록에 {(first != null && cs != null && cs.GetRecipesStartingWith(first).Contains(r) ? "뜸" : "안 뜸")}");
        }
        return sb.ToString();
    }
}
