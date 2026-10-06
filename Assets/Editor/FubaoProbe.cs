using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 김민준 「푸바오」 점검(10-06) — gameshot:
//   call:FubaoProbe.Arena wait:1 call:FubaoProbe.Snap(기준) ... call:FubaoProbe.ForceMana wait:6 call:FubaoProbe.Snap  → ① 찍어누르기 값(잃은 체력 2%)·③④ 발동 횟수
//   call:FubaoProbe.Toggle wait:1 call:FubaoProbe.Restore wait:8 call:FubaoProbe.Snap  → ② 포커싱오더 켜짐: 잃은 체력 많은 표적 B만 맞는가(꺼짐일 땐 가까운 A)
// 표적 셋(체력 1e6 배, 멈춤, 레인 없음): A 가까움·잃은 체력 0 / B 중간·잃은 체력 50% / C 멀리·잃은 체력 10%. 푸바오는 레인 가운데.
static class FubaoProbe
{
    static UnitIdentity fubao;
    static UnitAttacker attacker;
    static readonly List<EnemyDummy> enemies = new List<EnemyDummy>();
    static readonly List<float> lastHp = new List<float>();
    static readonly FieldInfo ManaCounter = typeof(UnitAttacker).GetField("manaGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly MethodInfo Resolve = typeof(UnitAttacker).GetMethod("ResolveSkillEffectValue", BindingFlags.NonPublic | BindingFlags.Instance);
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        UnitData d = Roster("초월_김민준_AP");
        if (spawner == null || lane == null || dummyData == null || d == null) return "❌ 준비 안 됨";

        SkillTelemetry.Enabled = true;
        SkillTelemetry.Reset();
        Vector3 c = lane.LaneCenter;
        fubao = spawner.Spawn(d, c, 0).GetComponent<UnitIdentity>();
        attacker = fubao.GetComponent<UnitAttacker>();
        enemies.Clear();
        lastHp.Clear();
        float[] missing = { 0f, 0.5f, 0.1f };      // A·B·C 잃은 체력 비율
        float[] dist = { 45f, 75f, 105f };          // 푸바오 앞쪽 거리(사거리 안)
        for (int i = 0; i < 3; i++)
        {
            GameObject go = Object.Instantiate(dummyData.prefab, c + new Vector3(0f, 0f, dist[i]), Quaternion.Euler(0f, 180f, 0f));
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            var e = go.GetComponent<EnemyDummy>();
            e.Initialize(dummyData, 1e6f);
            e.SetLane(-1);
            e.SetReplicaHp(e.MaxHp * (1f - missing[i]), e.MaxHp);
            enemies.Add(e);
            lastHp.Add(e.Hp);
        }
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        if (sel != null && fubao.TryGetComponent(out Selectable s)) sel.SelectOnly(s);
        return $"세움: {fubao.Data.DisplayName} 스킬 {fubao.Data.skills.Count}개 [{string.Join(", ", fubao.Data.skills.Select(k => k.skillName))}] · 평타 공격력 {fubao.Data.attackPower:N0} · 사거리 {attacker.AttackRange:F0} · 표적 A·B·C 잃은 체력 0%·50%·10%";
    }

    static string ForceMana()
    {
        if (attacker == null) return "❌ Arena 먼저";
        ManaCounter.SetValue(attacker, 134);   // 다음 평타가 135번째 — 찍어누르기 발동
        return "마나 게이지 134로 맞춤(다음 평타에 찍어누르기)";
    }

    static string Toggle()
    {
        if (attacker == null) return "❌ Arena 먼저";
        SkillData focus = fubao.Data.skills.FirstOrDefault(k => k.triggerType == SkillTriggerType.ActiveButton);
        bool ok = attacker.TryCastActive(focus, out string reason);
        return $"토글 시전 {(ok ? "OK" : "실패 " + reason)} → 포커싱 {(attacker.FocusLostHp ? "켜짐" : "꺼짐")}";
    }

    static string Restore()
    {
        for (int i = 0; i < enemies.Count; i++) lastHp[i] = enemies[i] != null ? enemies[i].Hp : 0f;
        return "표적 체력 기준 재설정";
    }

    static string Snap()
    {
        if (attacker == null) return "❌ Arena 먼저";
        var sb = new StringBuilder();
        string[] names = { "A(가까움·잃은 0%)", "B(중간·잃은 50%)", "C(멀리·잃은 10%)" };
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyDummy e = enemies[i];
            if (e == null) { sb.AppendLine($"   {names[i]} 없음"); continue; }
            sb.AppendLine($"   {names[i]} 체력 {e.Hp:N0}/{e.MaxHp:N0} · 이번 구간 피해 {lastHp[i] - e.Hp:N0} · 마방 배율 {e.EffectiveMagicMultiplier:F3}");
            lastHp[i] = e.Hp;
        }
        sb.AppendLine($"   포커싱 {(attacker.FocusLostHp ? "켜짐" : "꺼짐")} · 마나 게이지 {ManaCounter.GetValue(attacker)} · 발동 횟수: " + string.Join(" · ",
            fubao.Data.skills.Where(k => k != null).Select(k => $"{k.skillName.Split('—')[0].Trim()} {SkillTelemetry.CastLog.Count(c => c.unit == fubao.Data && c.skill == (string.IsNullOrEmpty(k.skillName) ? k.name : k.skillName))}")));
        SkillData crush = fubao.Data.skills[0];
        var effect = crush.levels[0].effects[0];
        sb.AppendLine("   찍어누르기 값(예상) B 대상: " + ((float)Resolve.Invoke(attacker, new object[] { effect, enemies[1], 0f })).ToString("N0")
            + $" (= 잃은 체력 {enemies[1].MaxHp - enemies[1].Hp:N0}의 2%)");
        var summons = Object.FindObjectsByType<SummonedBy>(FindObjectsSortMode.None).Where(s => s.Summoner == attacker).ToList();
        sb.AppendLine($"   소환수 {summons.Count}기" + string.Concat(summons.Select(s =>
        {
            UnitCombat combat = s.GetComponent<UnitCombat>();
            return $" [{s.name} 표적 {(combat != null && combat.CurrentTarget != null ? combat.CurrentTarget.name : "없음")} · 포커싱 따라감 {s.GetComponent<UnitAttacker>().FocusActive}]";
        })));
        return sb.ToString();
    }
}
