using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 초월 강재규 실측(10-06 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:JaegyuProbe.Setup   초월 강재규 + 노태현(아군 디버프 오라 시전자)을 우리에 세운다
///   call:JaegyuProbe.Near    노태현을 강재규 곁으로 Warp(오라 반경 안)
///   call:JaegyuProbe.Report  체력 게이지 · 자기 스턴 · 받는 아군발 디버프 개수 · 피해 증가 배율
///   call:JaegyuProbe.Stun    만성피로를 직접 한 번 터뜨려(자기 스턴 3초) 끝난 뒤 게이지가 가득 차는지 본다
/// </summary>
public static class JaegyuProbe
{
    static UnitAttacker Jae() => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "초월_강재규_AP");
    static UnitAttacker Notae() => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "초월_노태현_AP");

    static void Spawn(string name, StringBuilder sb)
    {
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        sb.AppendLine($"   소환 {name} → {(go != null ? go.name : "null")}");
    }

    public static string Setup() { StringBuilder sb = new StringBuilder(); Spawn("초월_강재규_AP", sb); Spawn("초월_노태현_AP", sb); return sb.ToString(); }

    public static string Near()
    {
        UnitAttacker j = Jae(), n = Notae();
        if (j == null || n == null) return "❌ 유닛 없음";
        bool ok = n.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.Warp(j.transform.position + new Vector3(60f, 0f, 0f));
        return $"   노태현을 강재규 곁으로 Warp {(ok ? "✅" : "❌")}";
    }

    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

    public static string Report()
    {
        UnitAttacker j = Jae();
        if (j == null) return "❌ 강재규 없음";
        UnitData d = j.GetComponent<UnitIdentity>().Data;
        float factor = (float)typeof(UnitAttacker).GetMethod("AllyDebuffDamageFactor", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(j, null);
        return $"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim() + (s.skillName.Contains('—') ? "·" + s.skillName.Split('—')[1].Trim().Split('(')[0].Trim() : "")))}\n" +
               $"   체력 게이지 {Get<int>(j, "lifeGaugeCounter")}/{d.lifeGaugeMax} · 자기 스턴 {(j.IsSelfStunned ? "중" : "아님")} · 받는 아군발 디버프 {j.CountAllyDebuffs()}개 → 피해 ×{factor:F2}";
    }

    public static string Stun()
    {
        UnitAttacker j = Jae();
        MethodInfo m = typeof(UnitAttacker).GetMethod("BeginSelfStun", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(j, new object[] { 3f });
        return "   자기 스턴 3초 시작\n" + Report();
    }
}
