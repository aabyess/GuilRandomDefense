using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>영원함 조세민 실측(10-06 구현담당3) — gameshot: call:EternalSeminProbe.Report (신 모드). 라이브콘서트 3회 시전 시 아군 공속(+25%→+30%→+35%), 마방깍·증폭·스턴, 식 지연 180초 + 실제 지연 조합(1초로 줄여 재료 소모·지연 생성 확인은 별도 시나리오).</summary>
public static class EternalSeminProbe
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

    public static string Report()
    {
        var sb = new StringBuilder();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/영원_조세민.asset");
        UnitData other = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/희귀함_구주호.asset");
        var ally = spawner.Spawn(other, LaneMarker.Get(0).TakeSpawnPosition(other), 0).GetComponent<UnitAttacker>();
        var a = spawner.Spawn(d, LaneMarker.Get(0).TakeSpawnPosition(d), 0).GetComponent<UnitAttacker>();
        sb.AppendLine($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · 마나 {d.manaMax} · 마젠 {d.manaAuraRegenPerSecond}/초");
        var cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        SkillData concert = d.skills.First(s => s.skillName.StartsWith("라이브콘서트"));
        float b0 = ally.CurrentAttackSpeedMultiplier;
        var speeds = new System.Collections.Generic.List<string>();
        for (int i = 0; i < 3; i++)
        {
            cast.Invoke(a, new object[] { concert.levels[0], concert.levels[0].WorldRange, null, 0f });
            speeds.Add($"{ally.CurrentAttackSpeedMultiplier / b0:F2}");
        }
        sb.AppendLine($"   라이브콘서트 3회: 아군 공속 비 {string.Join(" → ", speeds)} (기대 ×1.25 → ×1.30 → ×1.35, 같은 버프 id라 최대 하나 — 마지막 값 기준)");
        EnemyDummy t = Make(a.transform.position + Vector3.forward * 30f);
        float hp0 = t.Hp;
        foreach (string key in new[] { "폭언", "말의상처", "프레임씌우기" })
        {
            SkillData s = d.skills.First(x => x.skillName.StartsWith(key));
            cast.Invoke(a, new object[] { s.levels[0], 0f, t, 0f });
        }
        sb.AppendLine($"   폭언·말의상처·프레임씌우기 시전: 스턴 {t.IsStunned}");
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/영원_조세민.asset");
        sb.AppendLine($"   식: 재료 {recipe.ingredients.Count}종: {string.Join(", ", recipe.ingredients.Select(i => i.unit != null ? i.unit.name : "빈칸"))} · 입력말 「{recipe.chatPhrase}」 · 지연 {recipe.resultDelaySeconds}초 · IsChatOnly {CombineSystem.IsChatOnly(recipe)}");
        return sb.ToString();
    }

    // 지연 생성 시나리오(gameshot: call:EternalSeminProbe.CombineStart wait:5 call:EternalSeminProbe.CombineEnd) — 지연을 2초로 줄여(플레이 중 메모리에서만, 끝에 원래 값 복구) 재료 5종을 세워 실제 TryCombine.
    static float savedDelay; static int savedSave, unitsBefore, kingsBefore;
    static int Kings() => UnitIdentity.Active.Count(u => u != null && u.Data != null && u.Data.name == "영원_조세민");
    static int CountMine() => UnitIdentity.Active.Count(u => u != null && u.OwnerId == 0 && !u.IsSummon);

    public static string CombineStart()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/영원_조세민.asset");
        savedDelay = recipe.resultDelaySeconds;
        recipe.resultDelaySeconds = 2f;
        savedSave = recipe.requiredSaveCount; recipe.requiredSaveCount = 0;   // 세이브 횟수 문턱은 이 시나리오에서만 치운다(끝에 복구)
        PlayerContext.Local.ResourceWallet.Add(ResourceType.Wood, 10);
        kingsBefore = Kings();
        foreach (var ing in recipe.ingredients) spawner.Spawn(ing.unit, LaneMarker.Get(0).TakeSpawnPosition(ing.unit), 0);
        unitsBefore = CountMine();
        var cs = Object.FindFirstObjectByType<CombineSystem>();
        bool ok = cs.TryCombine(recipe);
        int after = CountMine();
        return $"   TryCombine {ok} · 내 유닛 {unitsBefore} → {after}(재료 5 소모 기대 −5) · 영원_조세민 {kingsBefore} → {Kings()}기(기대: 같음 = 아직 안 나옴)";
    }

    public static string CombineEnd()
    {
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/영원_조세민.asset");
        recipe.resultDelaySeconds = savedDelay; recipe.requiredSaveCount = savedSave;
        return $"   2초+ 뒤: 영원_조세민 {kingsBefore} → {Kings()}기(기대 +1) · 지연 {recipe.resultDelaySeconds}초·세이브 문턱 {recipe.requiredSaveCount} 복구";
    }
}
