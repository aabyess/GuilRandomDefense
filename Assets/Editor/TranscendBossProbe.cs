using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 5기 「신」 보스 앞 10초 피해 재측정(10-08) — AttackBossProbe 개정판.
// 고친 것: ① 피해를 프레임마다 (채운 값 − 남은 값)을 double로 누적(1e13 표적의 float 눈금에 평타가 묻히던 것 · 표적은 1e9로 낮추고 매 프레임 채움)
//          ② 표적이 죽지 않게 매 프레임 채움(죽으면 평타가 끊긴다) ③ 보스는 isBoss·레인 0·신 난이도 방어 가산(DifficultyTable.BossArmorBonus) ④ 스킬 채널별 피해(SkillTelemetry)도 함께.
// gameshot: gameshot x.png 1 1920x1080 click?:신 mode:신 wait:2 call:TranscendBossProbe.Setup wait:1 call:TranscendBossProbe.Mark wait:10 call:TranscendBossProbe.Report
static class TranscendBossProbe
{
    static readonly string[] Names = { "초월_박민석_ADAP", "초월_박민수_AD", "초월_엄태웅_AD", "초월_최상호_AP", "초월_이재윤_AD" };
    const float Refill = 1e9f;
    class Pair { public UnitAttacker unit; public EnemyDummy boss; public UnitData data; public double dealt, dealtAtMark; public int hits0; public bool marked; }
    static readonly List<Pair> pairs = new List<Pair>();
    static float lastT, markT, endT;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        EnemyData boss = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        if (spawner == null || boss == null || LaneMarker.Get(0) == null) return "❌ 준비 실패";
        var hpField = typeof(EnemyDummy).GetField("hp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var maxField = typeof(EnemyDummy).GetProperty("MaxHp");
        pairs.Clear(); SkillTelemetry.Reset(); SkillTelemetry.Enabled = true;
        float armorBonus = DifficultyTable.BossArmorBonus(DifficultyManager.Instance.Current);
        int i = 0;
        foreach (string n in Names)
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
            if (d == null) { i++; continue; }
            Vector3 p = LaneMarker.Get(0).LaneCenter + new Vector3(-400f + i * 200f, 0f, 200f);
            var go = spawner.Spawn(d, p, 0);
            var u = go.GetComponent<UnitAttacker>();
            var ag = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null && ag.enabled) ag.Warp(p);
            GameObject bg = Object.Instantiate(boss.prefab, p + new Vector3(0f, 0f, 22f), Quaternion.identity);
            if (bg.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var e = bg.GetComponent<EnemyDummy>(); e.Initialize(boss, 1f); e.SetLane(0); e.DifficultyArmorBonus = armorBonus;
            hpField.SetValue(e, Refill);
            pairs.Add(new Pair { unit = u, boss = e, data = d });
            i++;
        }
        lastT = Time.time;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return $"세움 {pairs.Count}기 · 난이도 {DifficultyManager.Instance.Current} · 보스 {boss.name} 실효방어 {(pairs.Count > 0 ? pairs[0].boss.EffectiveArmor : 0):F1} (가산 {armorBonus})";
    }

    static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        var hpField = typeof(EnemyDummy).GetField("hp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var p in pairs)
        {
            if (p.boss == null) continue;
            p.dealt += Refill - p.boss.Hp;
            hpField.SetValue(p.boss, Refill);
        }
    }

    static string Mark()
    {
        markT = Time.time;
        foreach (var p in pairs) { p.dealtAtMark = p.dealt; p.hits0 = p.unit.BasicHitCount; p.marked = true; }
        return "표식";
    }

    static string Report()
    {
        endT = Time.time;
        float span = Mathf.Max(0.01f, endT - markT);
        var sb = new StringBuilder($"\n게임 시간 {span:0.0}초 · 난이도 {DifficultyManager.Instance.Current}");
        foreach (var p in pairs)
        {
            double dmg = p.dealt - p.dealtAtMark;
            string ch = string.Join(" ", SkillTelemetry.ChannelsOf(p.data).Select(c => $"{c} {SkillTelemetry.DamageOf(p.data, c):N0}"));
            sb.Append($"\n{p.data.name,-18} 공격력 {p.unit.AttackDamage,9:F0} · 간격 {p.unit.AttackInterval:F3}초 · 평타 {p.unit.BasicHitCount - p.hits0}타 · 방어 {p.boss.EffectiveArmor:F1} · 피해 {dmg:N0} ({dmg / span:N0}/초) · 채널(누계) {ch}");
        }
        EditorApplication.update -= Tick;
        return sb.ToString();
    }
}
