using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 영원함 구현 실측(10-06 구현담당3) — gameshot: call:EternalProbe.Setup wait:3 call:EternalProbe.Report (신 모드).
/// 윤현모(이름만)·킹카: 스킬 목록·칭호·입력말(ChatPhrases에 정규화된 말이 있나)·IsChatOnly·재료 수, 킹카 방깍 오라(적 방어 −40)·마나/체력 게이지 값.
/// </summary>
public static class EternalProbe
{
    static EnemyDummy boss;
    static float armorBefore;

    static EnemyDummy Make(Vector3 at)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, 1e3f); d.SetLane(-1);
        return d;
    }

    public static string Setup()
    {
        UnitData king = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/영원_최상호.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(king, LaneMarker.Get(0).TakeSpawnPosition(king), 0);
        Vector3 at = go.transform.position + Vector3.forward * 30f;
        boss = Make(at);
        armorBefore = boss.EffectiveArmor;
        return $"   킹카 세움 · 보스 방어(오라 전) {armorBefore:F1}";
    }

    static string Describe(string unitName, string[] recipeNames)
    {
        var sb = new StringBuilder();
        UnitData u = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{unitName}.asset");
        sb.AppendLine($"   「{u.unitName}」 스킬 {u.skills.Count}: {string.Join(" / ", u.skills.Select(s => s.skillName.Split('—')[0].Trim()))} · 마나 {u.manaMax} · 체력 게이지 {u.lifeGaugeMax} · trait {(u.trait == null ? "없음" : u.trait.name)}");
        var chat = typeof(CombineSystem).GetMethod("ChatPhrases", BindingFlags.NonPublic | BindingFlags.Static);
        var norm = typeof(CombineSystem).GetMethod("NormalizePhrase", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (string rn in recipeNames)
        {
            var r = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>($"Assets/Data/Recipes/{rn}.asset");
            var phrases = ((System.Collections.Generic.IEnumerable<string>)chat.Invoke(null, new object[] { r })).ToList();
            string want = (string)norm.Invoke(null, new object[] { r.chatPhrase });
            sb.AppendLine($"     식 {rn}: 재료 {r.ingredients.Count}종 · chatPhrase 「{r.chatPhrase}」 → 정규화 {(phrases.Contains(want) ? "입력말 목록에 있음" : "❌ 없음")} · IsChatOnly {CombineSystem.IsChatOnly(r)}");
        }
        return sb.ToString();
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        sb.Append(Describe("영원_윤현모", new[] { "영원_윤현모" }));
        sb.Append(Describe("영원_최상호", new[] { "영원_최상호", "영원_최상호_바지사장" }));
        if (boss != null) sb.AppendLine($"   킹카 방깍 오라: 보스 방어 {armorBefore:F1} → {boss.EffectiveArmor:F1} (기대 −40)");
        return sb.ToString();
    }
}
