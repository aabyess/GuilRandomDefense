using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 초월 노태현 「여동생살해자」 실측(10-06 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:NotaehyunProbe.Setup      초월 노태현 + 흔함 박민수(아군)를 우리에 세우고 노태현을 선택
///   call:NotaehyunProbe.Report     스킬 목록 · 폭발증폭 배율 · 방어 무시(강화 전) · 아군 이속(디버프 중)
///   call:NotaehyunProbe.Click      「최윤서 강화」 칸(UnitCommandSlot6)을 진짜 클릭 경로로 누르고 알림을 적는다
///   call:NotaehyunProbe.SpawnYoonseo  히든 최윤서 한 기를 세운다
///   call:NotaehyunProbe.Reselect   노태현 다시 선택(칸 갱신)
/// </summary>
public static class NotaehyunProbe
{
    static UnitAttacker Notae() => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "초월_노태현_AP");
    static UnitAttacker Ally() => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "흔함_박민수");

    static void Spawn(string name, StringBuilder sb)
    {
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        GameObject go = spawner.Spawn(data, lane.TakeSpawnPosition(data), 0);
        sb.AppendLine($"   소환 {name} → {(go != null ? go.name : "null")} 위치 {(go != null ? go.transform.position.ToString("F0") : "")}");
    }

    static void Select(StringBuilder sb)
    {
        UnitAttacker n = Notae();
        SelectionManager sel = Object.FindFirstObjectByType<SelectionManager>();
        if (n == null || sel == null) { sb.AppendLine("   ❌ 선택 실패"); return; }
        sel.SelectOnly(n.GetComponent<Selectable>());
        sb.AppendLine($"   선택 {n.name}");
    }

    public static string Setup()
    {
        StringBuilder sb = new StringBuilder();
        Spawn("초월_노태현_AP", sb); Spawn("흔함_박민수", sb); Select(sb);
        return sb.ToString();
    }

    // 아군을 노태현 곁(수평 60)으로 옮긴다 — 오라 반경 600/WorldScale 안으로 들어가야 이속 감소가 걸린다.
    public static string MoveAlly()
    {
        UnitAttacker n = Notae(), ally = Ally();
        if (n == null || ally == null) return "❌ 유닛 없음";
        Vector3 to = n.transform.position + new Vector3(60f, 0f, 0f);
        bool warped = ally.TryGetComponent(out NavMeshAgent agent) && agent.Warp(to);
        return $"   아군을 노태현 곁으로 Warp {(warped ? "✅" : "❌")} → 거리 {Vector3.Distance(n.transform.position, ally.transform.position):F1} · 오라 반경(월드) {600f / WorldScale.Value:F1}";
    }

    public static string Reselect() { StringBuilder sb = new StringBuilder(); Select(sb); return sb.ToString(); }
    public static string SpawnYoonseo() { StringBuilder sb = new StringBuilder(); Spawn("히든_최윤서", sb); Select(sb); return sb.ToString(); }

    static T Call<T>(object target, string method, params object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return (T)m.Invoke(target, args);
    }

    public static string Report()
    {
        UnitAttacker n = Notae(), ally = Ally();
        if (n == null) return "❌ 노태현 없음";
        UnitData data = n.GetComponent<UnitIdentity>().Data;
        StringBuilder sb = new StringBuilder($"   유닛 「{data.unitName}」 스킬 {data.skills.Count}: {string.Join(" / ", data.skills.Select(s => s.skillName.Split('—')[0].Trim()))}\n");
        sb.AppendLine($"   스플래시 반경 {data.attackSplashRadius} · 마나게이지 최대 {data.manaMax}");
        sb.AppendLine($"   폭발증폭 배율 {Call<float>(n, "SplashDamageFactor", data):F2}");
        SkillEffect burst = data.skills.First(s => s.name.Contains("돌발행동")).levels[0].effects[0];
        sb.AppendLine($"   방어 무시(돌발행동) 강화 {(n.YoonseoEnhanced ? "켜짐" : "꺼짐")} → {Call<float>(n, "SkillArmorIgnore", burst):F1}");
        if (ally != null && ally.TryGetComponent(out NavMeshAgent agent))
            sb.AppendLine($"   아군 박민수 이속 {agent.speed:F1} (기본 {ally.GetComponent<UnitIdentity>().Data.moveSpeed:F1}) · 노태현과 거리 {Vector3.Distance(n.transform.position, ally.transform.position):F1}");
        PlayerContext me = PlayerContext.Local;
        sb.AppendLine($"   내 최윤서 {me.UnitInventory.Members.Count(m => m != null && m.Data != null && m.Data.unitName == "최윤서")}기 · 유닛 {me.UnitInventory.Units.Count}");
        return sb.ToString();
    }

    public static string Click()
    {
        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        var notes = new List<string>();
        System.Action<int, string, float> hook = (pid, msg, d) => notes.Add(msg);
        PlayerNotification.Shown += hook;
        StringBuilder sb = new StringBuilder();
        try
        {
            Button b = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(x => x.name == "UnitCommandSlot6");
            string label = b == null ? "" : string.Concat(b.GetComponentsInChildren<Text>().Select(t => t.text)) + string.Concat(b.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text));
            if (b == null) return "   ❌ UnitCommandSlot6 없음";
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center));
            ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            sb.AppendLine($"   칸 「{label.Replace("\n", " ")}」 interactable {b.interactable} → 알림: {(notes.Count == 0 ? "(없음)" : string.Join(" / ", notes))}");
        }
        finally { PlayerNotification.Shown -= hook; }
        return sb.ToString() + Report();
    }
}
