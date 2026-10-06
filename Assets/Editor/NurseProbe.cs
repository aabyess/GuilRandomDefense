using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 양재모 물리간호사 점검(10-06) — gameshot: call:ShopSlotProbe.Fund call:NurseProbe.Setup wait:2 call:NurseProbe.Report
// 공속 누적 +1%/타(상한 +100%·3초 초기화) · LIFE 게이지 스킬(적 이감·자기 공속 감소 음수) · 채팅 조합 「불살주의」.
static class NurseProbe
{
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static UnitIdentity nurse;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = Roster("초월_양재모_AD");
        nurse = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0).GetComponent<UnitIdentity>();
        return $"세움: {nurse.Data.unitName}({nurse.Data.skills.Count}스킬) 평타 공속 {nurse.Data.attackSpeed}";
    }

    static string Report()
    {
        if (!Application.isPlaying || nurse == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        UnitAttacker a = nurse.GetComponent<UnitAttacker>();
        SkillData stackSkill = nurse.Data.skills.First(s => s.name.Contains("간호학과대표"));
        SkillEffect stack = stackSkill.levels[0].effects[0];
        var apply = typeof(UnitAttacker).GetMethod("ApplySkillEffect", BindingFlags.NonPublic | BindingFlags.Instance);
        var speed = typeof(UnitAttacker).GetProperty("AttackSpeedMultiplier", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        float s0 = a.AttackSpeedStackValue;
        for (int i = 0; i < 30; i++) apply.Invoke(a, new object[] { stack, 0f, Vector3.zero, null, 0f, new System.Collections.Generic.Dictionary<object, System.Collections.Generic.HashSet<int>>() });
        sb.AppendLine($"[공속 누적] 평타 30타 → 누적 {s0:F2}→{a.AttackSpeedStackValue:F2}(기대 0.30)" + (speed != null ? $" · 공속 배율 {speed.GetValue(a)}" : ""));
        for (int i = 0; i < 100; i++) apply.Invoke(a, new object[] { stack, 0f, Vector3.zero, null, 0f, new System.Collections.Generic.Dictionary<object, System.Collections.Generic.HashSet<int>>() });
        sb.AppendLine($"[상한] 130타 → {a.AttackSpeedStackValue:F2}(기대 1.00)");
        var box = Object.FindFirstObjectByType<GameChatBox>();
        string msg = box != null ? box.TryExecuteCode(0, "불살주의") : null;
        sb.AppendLine($"[타이핑] 「불살주의」 → {msg ?? "(코드 아님)"}");
        SkillData sweat = nurse.Data.skills.First(s => s.name.Contains("다한증"));
        sb.AppendLine($"[다한증] 트리거 {sweat.triggerType} · 게이지 {sweat.levels[0].gaugeKind} {sweat.levels[0].hitCountThreshold} · 효과 {string.Join(" + ", sweat.levels[0].effects.Select(e => e.kind + " " + e.multiplier + "/" + e.duration + "s"))}");
        return sb.ToString();
    }

    // 초기화 확인용 — 3.1초 뒤 값(gameshot wait:3.2 뒤에 부른다)
    static string AfterIdle() => nurse == null ? "❌ Setup 먼저" : $"[초기화] 마지막 누적 뒤 경과 → 누적 {nurse.GetComponent<UnitAttacker>().AttackSpeedStackValue:F2}(기대 0.00, 3초 무공격 후)";
}
