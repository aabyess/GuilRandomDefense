using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 초월 임장혁 실측(10-06 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:JanghyukProbe.Setup   초월 임장혁 + 흔함 박민수 + 초월 노태현(디버프 오라 시전자)을 우리에 세운다
///   call:JanghyukProbe.Near    박민수·노태현을 임장혁 곁(수평 60)으로 Warp
///   call:JanghyukProbe.Report  임장혁·박민수·노태현의 공격력/공속 배율/스킬 피해 오라/이속/받는 디버프 개수/마나
///   call:JanghyukProbe.Trait   특성 레벨 2 효과를 시뮬레이션(이간질 레벨 2의 효과 0) — 전/후 박민수 공격력 비교용
/// </summary>
public static class JanghyukProbe
{
    static UnitAttacker Find(string asset) => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == asset);

    static void Spawn(string name, StringBuilder sb)
    {
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        sb.AppendLine($"   소환 {name} → {(go != null ? go.name : "null")}");
    }

    public static string Setup() { StringBuilder sb = new StringBuilder(); Spawn("초월_임장혁_AD", sb); Spawn("흔함_박민수", sb); Spawn("초월_노태현_AP", sb); return sb.ToString(); }

    public static string Near()
    {
        UnitAttacker j = Find("초월_임장혁_AD");
        int i = 0;
        foreach (string n in new[] { "흔함_박민수", "초월_노태현_AP" })
        {
            UnitAttacker a = Find(n);
            if (a != null && a.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.Warp(j.transform.position + new Vector3(60f, 0f, 40f * i));
            i++;
        }
        return "   박민수·노태현을 임장혁 곁으로 Warp";
    }

    static float F(object o, string prop) => (float)o.GetType().GetProperty(prop, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(o);
    static float M(object o, string method, params object[] args) => (float)o.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(o, args);

    static string Line(string label, UnitAttacker a)
    {
        if (a == null) return $"   {label}: 없음\n";
        UnityEngine.AI.NavMeshAgent agent = a.GetComponent<UnityEngine.AI.NavMeshAgent>();
        float skillBonus = (float)typeof(UnitAttacker).GetMethod("AuraBonusTotal", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, new object[] { SkillEffectKind.AllySkillDamageBonus, true });
        return $"   {label}: 공격력 {a.AttackDamage:F0} · 공속 배율 {a.CurrentAttackSpeedMultiplier:F3} · 스킬 피해 오라 ×{skillBonus:F2} · 이속 {(agent != null ? agent.speed.ToString("F1") : "-")} · 받는 아군발 디버프 {a.CountAllyDebuffs()}개\n";
    }

    public static string Report()
    {
        UnitAttacker j = Find("초월_임장혁_AD");
        if (j == null) return "❌ 임장혁 없음";
        UnitData d = j.GetComponent<UnitIdentity>().Data;
        StringBuilder sb = new StringBuilder($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + "·" + (s.skillName.Contains('—') ? s.skillName.Split('—')[1].Trim().Split('(')[0].Trim() : "")))}\n");
        sb.Append(Line("임장혁(자기)", j)).Append(Line("박민수(아군)", Find("흔함_박민수"))).Append(Line("노태현(아군·이속 디버프원)", Find("초월_노태현_AP")));
        sb.AppendLine($"   마나 최대 {d.manaMax} · 마나 오라 +{d.manaAuraRegenPerSecond}/초 반경 {d.manaAuraRange}");
        return sb.ToString();
    }
}
