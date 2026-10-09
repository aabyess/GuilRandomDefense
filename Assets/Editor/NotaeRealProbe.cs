using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 노태현 「최윤서 강화」 실제 버튼 경로 검증(구현담당1, 10-09). gameshot x.png 1 1920x1080 click?:쉬움 mode:쉬움 wait:2 call:NotaeRealProbe.Setup wait:2 [call:NotaeRealProbe.Click wait:2] call:NotaeRealProbe.Fight wait:40 call:NotaeRealProbe.Report
static class NotaeRealProbe
{
    static UnitAttacker nt;
    static UnitData data;
    static EnemyDummy boss;
    static readonly FieldInfo HpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
    static double dealt; static float lastT;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_노태현_AP.asset");
        var yoon = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/히든_최윤서.asset");
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        SkillTelemetry.Reset(); SkillTelemetry.Enabled = true;
        var go = spawner.Spawn(data, c, 0);
        nt = go.GetComponent<UnitAttacker>();
        spawner.Spawn(yoon, c + new Vector3(30f, 0f, 0f), 0);
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        sel.SelectOnly(go.GetComponent<Selectable>());
        return $"세움 · 최윤서 인벤토리 {PlayerContext.Get(0).UnitInventory.Members.Count(m => m != null && m.Data != null && m.Data.name == "히든_최윤서")}기 · 강화 {nt.YoonseoEnhanced} · 스킬 {string.Join(" / ", data.skills.Select(s => s.skillName.Split(' ')[0] + (s.levels.Count > 0 && !string.IsNullOrEmpty(s.levels[0].requiredBuffId) ? "[게이트 " + s.levels[0].requiredBuffId + "]" : "")))}";
    }

    // 실제 버튼 핸들러(OnYoonseoClicked)를 그대로 부른다 — 선택 1기 노태현 + 최윤서 소모.
    static string Click()
    {
        var hud = Object.FindFirstObjectByType<GameHud>();
        var m = typeof(GameHud).GetMethod("OnYoonseoClicked", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(hud, null);
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        sel.SelectOnly(nt.GetComponent<Selectable>());   // 정보창 갱신
        return $"버튼 눌림 → 강화 {nt.YoonseoEnhanced} · 버프 {nt.HasBuff(UnitAttacker.YoonseoBuffId)} · 최윤서 남음 {PlayerContext.Get(0).UnitInventory.Members.Count(x => x != null && x.Data != null && x.Data.name == "히든_최윤서")}기";
    }

    static string Fight()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        EnemyData bossData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        GameObject bg = Object.Instantiate(bossData.prefab, c + new Vector3(0f, 0f, 22f), Quaternion.identity);
        if (bg.TryGetComponent(out WaypointMover mv)) mv.enabled = false;
        boss = bg.GetComponent<EnemyDummy>(); boss.Initialize(bossData, 1f); boss.SetLane(0);
        boss.DifficultyArmorBonus = DifficultyTable.BossArmorBonus(DifficultyManager.Instance.Current);
        HpField.SetValue(boss, 1e9f); dealt = 0; lastT = Time.time;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return $"전투 시작 · 강화 {nt.YoonseoEnhanced} · 보스 실효방어 {boss.EffectiveArmor:F0}";
    }
    static void Tick() { if (!Application.isPlaying || boss == null) { EditorApplication.update -= Tick; return; } dealt += 1e9f - boss.Hp; HpField.SetValue(boss, 1e9f); }

    static string Report()
    {
        EditorApplication.update -= Tick;
        var sb = new StringBuilder($"강화 {nt.YoonseoEnhanced} · 평타 {nt.BasicHitCount}타 · 보스 누적 피해 {dealt:N0} · 채널:");
        foreach (var ch in SkillTelemetry.ChannelsOf(data)) sb.Append($"\n   {ch} {SkillTelemetry.DamageOf(data, ch):N0}");
        foreach (var g in SkillTelemetry.CastLog.Where(e => e.unit == data).GroupBy(e => e.skill)) sb.Append($"\n   시전 {g.Key}: {g.Count()}회");
        var skill = data.skills[0]; int lvIdx = 0;
        sb.Append($"\n   반사회적인격 현재 레벨 인덱스(승급 버프 {skill.levelUpBuffId} 보유 {nt.HasBuff(skill.levelUpBuffId)}) · 레벨 {skill.levels.Count}개 · 레벨2 고정피해 효과 {skill.levels[1].effects.Count(e => e.fixedDamage)}개 값 {skill.levels[1].effects.Where(e => e.fixedDamage).Select(e => e.multiplier).FirstOrDefault():N0}");
        return sb.ToString();
    }
}
