using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 10-09 실측 묶음(구현담당1): 채팅 조합 입력말 · 임준성 보스 60초 · 해적선 비행 우클릭.
//   gameshot x.png 1 1920x1080 click?:쉬움 mode:쉬움 wait:2 call:Misc1009Probe.Chat
//   gameshot x.png 1 1920x1080 click?:신 mode:신 wait:2 call:Misc1009Probe.JunSetup wait:1 call:Misc1009Probe.Mark wait:60 call:Misc1009Probe.JunReport
//   gameshot x.png 1 1920x1080 click?:쉬움 mode:쉬움 wait:2 call:Misc1009Probe.ShipSetup wait:12 call:Misc1009Probe.ShipReport
static class Misc1009Probe
{
    static string Chat()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var system = Object.FindFirstObjectByType<CombineSystem>();
        var sb = new StringBuilder();
        foreach (string text in new[] { "최윤서 조합", "최윤서조합", "노윤서 조합", "페로나조합", "perona", "이이삭 조합", "김이삭 조합", "푸은서조합", "여은서조합", "아무말 조합" })
        {
            string r = system.TryCombineByChat(0, text);
            sb.AppendLine($"「{text}」 → {(r == null ? "코드 아님(null)" : "코드 인식: " + r.Replace("\n", " / "))}");
        }
        var recipe = system.Recipes.FirstOrDefault(x => x != null && x.name == "히든_최윤서");
        sb.Append("화면 코드 줄(DisplayCodes): " + (recipe != null ? CombineSystem.DisplayCodes(recipe) : "식 없음"));
        return sb.ToString();
    }

    static UnitAttacker jun; static UnitData junData; static EnemyDummy boss; static double dealt; static float markT;
    static readonly FieldInfo HpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
    static string JunSetup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        junData = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/제한_임준성.asset");
        var bossData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        SkillTelemetry.Reset(); SkillTelemetry.Enabled = true;
        jun = spawner.Spawn(junData, c, 0).GetComponent<UnitAttacker>();
        var bg = Object.Instantiate(bossData.prefab, c + new Vector3(0f, 0f, 22f), Quaternion.identity);
        if (bg.TryGetComponent(out WaypointMover m)) m.enabled = false;
        boss = bg.GetComponent<EnemyDummy>(); boss.Initialize(bossData, 1f); boss.SetLane(0);
        boss.DifficultyArmorBonus = DifficultyTable.BossArmorBonus(DifficultyManager.Instance.Current);
        HpField.SetValue(boss, 1e9f);
        EditorApplication.update -= JunTick; EditorApplication.update += JunTick;
        return $"세움 · 임준성 마나 최대 {jun.ShownManaMax} · 스킬 {string.Join(" / ", junData.skills.Select(s => s.skillName.Split('(')[0]))}";
    }
    static void JunTick() { if (!Application.isPlaying || boss == null) { EditorApplication.update -= JunTick; return; } dealt += 1e9f - boss.Hp; HpField.SetValue(boss, 1e9f); }
    static string Mark() { markT = Time.time; dealt = 0; return "표식"; }
    static string JunReport()
    {
        EditorApplication.update -= JunTick;
        float span = Time.time - markT;
        var shred = typeof(EnemyDummy).GetField("armorShred", BindingFlags.NonPublic | BindingFlags.Instance);
        return $"{span:F0}초 · 평타 {jun.BasicHitCount}타 · 보스 누적 피해 {dealt:N0} · 보스 방깎 {(shred != null ? shred.GetValue(boss) : "?")} · 마나 {jun.ShownManaNow}\n" + SkillTelemetry.Report(new[] { junData });
    }

    static GameObject ship;
    static string ShipSetup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/해적선.asset");
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        ship = spawner.Spawn(data, c, 0);
        var flying = ship.GetComponent<FlyingMover>();
        var mover = ship.GetComponent<UnitMover>();
        Vector3 sea = c + new Vector3(-1500f, 0f, 0f);   // 레인 섬 왼쪽 바다
        mover.MoveToGroundPoint(sea);
        return $"해적선 세움 {c:F0} · FlyingMover {(flying != null)} · NavMeshAgent {ship.GetComponent<UnityEngine.AI.NavMeshAgent>()?.enabled} · 목표 {sea:F0}";
    }
    static string ShipReport()
    {
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        var p = ship.transform.position;
        return $"12초 뒤 위치 {p:F0} · 출발점에서 {Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z)):F0} 이동";
    }
}
