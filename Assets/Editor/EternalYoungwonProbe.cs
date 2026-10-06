using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>영원함 김영원 실측(10-06 구현담당3) — gameshot: call:EternalYoungwonProbe.Report (신 모드). 시전 400회: 이김(대상 최대체력 12%·스턴)/짐(자기 스턴) 비율·피해 값, 식·입력말.</summary>
public static class EternalYoungwonProbe
{
    public static string Report()
    {
        var sb = new StringBuilder();
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/영원_김영원.asset");
        var a = Object.FindFirstObjectByType<UnitSpawner>().Spawn(d, LaneMarker.Get(0).TakeSpawnPosition(d), 0).GetComponent<UnitAttacker>();
        SkillData game = d.skills[0];
        sb.AppendLine($"   「{d.unitName}」 스킬 {d.skills.Count}: {game.skillName} · 마나 {d.manaMax}");
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        var cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        var selfStunned = typeof(UnitAttacker).GetMethod("IsSelfStunned", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        int wins = 0, stunsSelf = 0; float lastDrop = 0f; int bossStuns = 0;
        for (int i = 0; i < 400; i++)
        {
            GameObject go = Object.Instantiate(data.prefab, a.transform.position + Vector3.forward * 40f, Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            EnemyDummy t = go.GetComponent<EnemyDummy>(); t.Initialize(data, 1f); t.SetLane(-1);
            float hp0 = t.Hp;
            typeof(UnitAttacker).GetField("selfStunActive", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(a, false);
            cast.Invoke(a, new object[] { game.levels[0], 0f, t, 0f });
            if ((bool)typeof(UnitAttacker).GetField("selfStunActive", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a)) stunsSelf++;
            if (t.Hp < hp0) { wins++; lastDrop = (hp0 - t.Hp) / t.MaxHp; if (t.IsStunned) bossStuns++; }
            Object.Destroy(go);
        }
        sb.AppendLine($"   400회: 이김(대상 피해) {wins}회 = {wins / 4f:F1}%(기대 50%) · 이김 때 대상 최대체력 감소 {lastDrop * 100f:F2}%(기대 12%×상성) · 이김 중 스턴 {bossStuns}회 · 자기 스턴(짐) {stunsSelf}회(기대 이김과 합쳐 400)");
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/영원_김영원.asset");
        sb.AppendLine($"   식: 재료 {recipe.ingredients.Count}종: {string.Join(", ", recipe.ingredients.Select(i => i.unit != null ? i.unit.name : "빈칸"))} · 입력말 「{recipe.chatPhrase}」 · IsChatOnly {CombineSystem.IsChatOnly(recipe)}");
        return sb.ToString();
    }
}
