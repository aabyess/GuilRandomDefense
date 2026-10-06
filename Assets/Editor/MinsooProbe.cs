using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 초월 박민수 재능투자 실측(10-06 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:MinsooProbe.Setup    초월 박민수를 우리에 세우고 선택
///   call:MinsooProbe.Level    영웅 XP를 더해 레벨을 올린다(기본 +500 → 레벨 ~10)
///   call:MinsooProbe.Report   레벨 · 포인트 · 투자 4칸 · 공격력/공속 배율 · 시간 비례 계수
///   call:MinsooProbe.Click0~3 투자 칸(UnitCommandSlot8~11)을 진짜 클릭 경로로 누르고 알림을 적는다
/// </summary>
public static class MinsooProbe
{
    static UnitAttacker M() => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "초월_박민수_AD");

    public static string Setup()
    {
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_박민수_AD.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        UnitAttacker m = M();
        Object.FindFirstObjectByType<SelectionManager>().SelectOnly(m.GetComponent<Selectable>());
        return $"   소환 {go.name} · 레벨 {m.CharacterLevel} · 포인트 {m.TalentPointsAvailable}";
    }

    public static string Level() { M().AddHeroXp(500); return $"   XP +500 → 레벨 {M().CharacterLevel}"; }

    public static string Reselect() { Object.FindFirstObjectByType<SelectionManager>().SelectOnly(M().GetComponent<Selectable>()); return "   재선택"; }

    static T Call<T>(object o, string name) => (T)o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(o, null);
    static T Prop<T>(object o, string name) => (T)o.GetType().GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(o);

    public static string Report()
    {
        UnitAttacker m = M();
        if (m == null) return "❌ 박민수 없음";
        UnitData d = m.GetComponent<UnitIdentity>().Data;
        float growth = Call<float>(m, "GrowthDamageFactor");
        return $"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('—')[0].Trim()))}\n" +
               $"   레벨 {m.CharacterLevel} · 남은 포인트 {m.TalentPointsAvailable} · 투자 공격력 {m.GetTalent(0)} 공속 {m.GetTalent(1)} 방깎 {m.GetTalent(2)} 스턴 {m.GetTalent(3)}\n" +
               $"   공격력 {m.AttackDamage:F0} · 공속 배율 {m.CurrentAttackSpeedMultiplier:F3} · 평타 간격 {m.AttackInterval:F3} · 성장 계수 ×{growth:F2} · 기본 감금 확률 {d.skills[0].levels[0].triggerChance:F3}";
    }

    static string Click(int kind)
    {
        var notes = new System.Collections.Generic.List<string>();
        System.Action<int, string, float> hook = (pid, msg, dd) => notes.Add(msg);
        PlayerNotification.Shown += hook;
        try
        {
            Button b = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(x => x.name == "UnitCommandSlot" + (8 + kind));
            if (b == null) return "   ❌ 칸 없음";
            string label = string.Concat(b.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text)).Replace("\n", " ");
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center));
            ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            return $"   칸{8 + kind} 「{label}」 → 알림: {(notes.Count == 0 ? "(없음)" : string.Join(" / ", notes))}\n";
        }
        finally { PlayerNotification.Shown -= hook; }
    }

    public static string Click0() => Click(0);
    public static string Click1() => Click(1);
    public static string Click2() => Click(2);
    public static string Click3() => Click(3);
}
