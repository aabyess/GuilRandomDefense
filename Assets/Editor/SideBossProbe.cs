using System.Linq;
using System.Text;
using UnityEngine;

// 신세계 사이드보스 점검(구현담당1, 10-09). gameshot x.png 1 1920x1080 click?:신 mode:신 wait:2 jump:62 wait:14 call:SideBossProbe.Mid wait:10 call:SideBossProbe.End wait:1
static class SideBossProbe
{
    static string Describe()
    {
        var sb = new StringBuilder($"라운드 {Object.FindFirstObjectByType<RoundManager>().CurrentRound} · 사이드보스 {SideBossEncounter.Active.Count}기");
        foreach (var b in SideBossEncounter.Active)
            sb.Append($"\n   {b.name} 단계 {b.CurrentStage} · 시전 {b.CastProgress:F0} · 스턴 {b.StunGauge:F0} · 무적 {b.IsInvulnerable} · 체력 {b.GetComponent<EnemyDummy>().HpRatio * 100f:F0}%");
        var berserk = BerserkMob.Active.Where(x => x != null).ToList();
        sb.Append($"\n   광폭화 몬스터 {berserk.Count}기: " + string.Join(" · ", berserk.Select(x => $"{x.name} 크기 {x.transform.localScale.x:F1} B06B {x.GetComponent<EnemyDummy>().HasBuff("B06B")} 레인 {x.GetComponent<EnemyDummy>().LaneIndex}")));
        return sb.ToString();
    }
    static string Close()
    {
        var sb = new StringBuilder();
        foreach (var b in SideBossEncounter.Active.Where(x => x != null))
        {
            var d = b.GetComponent<EnemyDummy>();
            var rends = b.GetComponentsInChildren<Renderer>(true);
            sb.Append($"{b.name} 크기 {b.transform.localScale.x:F2} 위치 {b.transform.position} 렌더러 {rends.Length} 자식: {string.Join(",", b.GetComponentsInChildren<Transform>(true).Take(8).Select(t => t.name))} QuestMobLook {b.GetComponent<QuestMobLook>() != null}\n");
            sb.Append("   데이터 " + (d != null && d.Data != null ? d.Data.name + " 프리팹 " + UnityEditor.AssetDatabase.GetAssetPath(d.Data.prefab) : "?") + "\n");
            var cam = Object.FindFirstObjectByType<RtsCameraController>();
            if (cam != null) cam.MoveTo(b.transform.position);
        }
        return sb.Length > 0 ? sb.ToString() : "사이드보스 없음";
    }
    static string Mid() => Describe();
    static string End() => Describe();
}
