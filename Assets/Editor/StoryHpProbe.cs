using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 스토리 원작 체력 실측(PM 10-06 21db12e19) — gameshot:
//  ① 클리어 시간: call:StoryHpProbe.Begin(특별함 3기를 스토리존에 세우고 Time.timeScale 올림, 클리어·등장 시각 기록) → wait:N → call:StoryHpProbe.Log → … → call:StoryHpProbe.End
//  ② 4억 체력바: call:StoryHpProbe.Force13(스토리 13을 곧바로 세우고 카메라를 그쪽으로) → wait:1 → snap:
//  ③ 기여도 %: call:StoryHpProbe.Contribution(스토리 13에 30%·30%·40%를 나눠 때려 ContributionDamage/최대체력 비를 읽는다)
//  ④ 보스 제한시간 창: call:StoryHpProbe.JumpBoss → wait:6 → snap:
static class StoryHpProbe
{
    static readonly List<string> events = new List<string>();
    static float t0;
    static string lastRunning = "";
    static StoryManager Sm => StoryManager.Instance;
    static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);

    static void Tick()
    {
        if (!Application.isPlaying || Sm == null) return;
        string now = Sm.Running != null ? Sm.Running.storyName : "";
        if (now == lastRunning) return;
        EnemyDummy e = Field<EnemyDummy>(Sm, "activeEnemy");
        events.Add(now.Length > 0
            ? $"{Time.time - t0:F1}s(게임) 등장 {now} 체력 {(e != null ? e.MaxHp : 0f):N0} · 방어 {(e != null ? e.EffectiveArmor : 0f):F0}"
            : $"{Time.time - t0:F1}s(게임) 클리어/사라짐 {lastRunning}");
        lastRunning = now;
    }

    static string Begin()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var specials = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(u => u != null && u.grade == UnitGrade.Special && u.prefab != null && !u.isSystemUnit)
            .OrderBy(u => u.attackPower).ToList();
        if (specials.Count < 3 || spawner == null || lane == null) return "❌ 준비 안 됨";
        // 중앙값 근처 3기(서로 다른 유닛)
        int mid = specials.Count / 2;
        var picks = new[] { specials[mid - 1], specials[mid], specials[mid + 1] };
        Transform sp = (Transform)typeof(StoryManager).GetField("spawnPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Sm);
        Vector3 at = sp != null ? sp.position : Sm.transform.position;
        var sb = new StringBuilder("세움: ");
        for (int i = 0; i < 3; i++)
        {
            spawner.Spawn(picks[i], at + new Vector3(30f + i * 14f, 0f, -20f), 0);
            sb.Append($"{picks[i].DisplayName}(공격력 {picks[i].attackPower:N0}·공속 {picks[i].attackSpeed:F2}·사거리 {picks[i].attackRange:F0}) ");
        }
        t0 = Time.time; lastRunning = ""; events.Clear();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Time.timeScale = 20f;
        return sb + $"· 스토리존 {at:F0} · timeScale 20";
    }

    static string Log() => $"게임시간 {Time.time - t0:F1}s · 진행 {(Sm.Running != null ? Sm.Running.storyName + " 체력비 " + (Field<EnemyDummy>(Sm, "activeEnemy")?.HpRatio ?? 0f).ToString("P1") : "(없음/대기)")}\n   " + string.Join("\n   ", events);
    static string End() { Time.timeScale = 1f; EditorApplication.update -= Tick; return "timeScale 1 복귀 · " + Log(); }

    static EnemyDummy Spawned13;
    static string Force13() => ForceN(13);
    static string Force2() => ForceN(2);
    static string Force3() => ForceN(3);
    static string ForceN(int order)
    {
        var stories = Field<List<StoryData>>(Sm, "stories");
        StoryData s13 = stories.FirstOrDefault(s => s != null && s.order == order);
        if (s13 == null) return $"❌ 스토리 {order} 없음";
        typeof(StoryManager).GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Sm, new object[] { s13 });
        Spawned13 = Field<EnemyDummy>(Sm, "activeEnemy");
        Transform sp = (Transform)typeof(StoryManager).GetField("spawnPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Sm);
        Vector3 at = sp != null ? sp.position : Sm.transform.position;
        Object.FindFirstObjectByType<RtsCameraController>()?.MoveTo(at);
        return $"스토리 {order} 세움: 체력 {Spawned13.MaxHp:N0} · 방어 {Spawned13.EffectiveArmor:F0} · 자리 {at:F0}";
    }

    static string Contribution()
    {
        if (Spawned13 == null) return "❌ Force13 먼저";
        var sb = new StringBuilder();
        float max = Spawned13.MaxHp;
        foreach (float frac in new[] { 0.3f, 0.3f })
        {
            Spawned13.TakeDamage(max * frac, DamageType.AP, AttackType.Spells, 0);
            sb.AppendLine($"   {frac:P0} 가함 → 체력 {Spawned13.Hp:N0}/{max:N0} · 기여 {Spawned13.ContributionDamage[0]:N0} = {100f * Spawned13.ContributionDamage[0] / max:F2}% (표시 정수 {(int)(100f * Spawned13.ContributionDamage[0] / max)}%)");
        }
        // 작은 타격 수천 번(float 누적 오차 확인): 0.01%씩 400번 = 4%
        for (int i = 0; i < 400; i++) Spawned13.TakeDamage(max * 0.0001f, DamageType.AP, AttackType.Spells, 0);
        sb.AppendLine($"   +0.01%×400 → 기여 {Spawned13.ContributionDamage[0]:N0} = {100f * Spawned13.ContributionDamage[0] / max:F3}% (기대 64.0%) · 체력 {Spawned13.Hp:N0}");
        return sb.ToString();
    }

    static string HpText() => $"정보창 식 그대로: 체력: {Mathf.Max(0f, Spawned13.Hp):F0} / {Spawned13.MaxHp:F0} · N0: {Spawned13.Hp:N0} / {Spawned13.MaxHp:N0} · 실제 최대 {Spawned13.MaxHp:R}";

    static string JumpBoss()
    {
        var rm = Object.FindFirstObjectByType<RoundManager>();
        bool ok = rm.DebugJumpToRound(10);
        return $"R10 점프 {ok}";
    }
    static string BossTimer()
    {
        var rm = Object.FindFirstObjectByType<RoundManager>();
        bool has = rm.TryGetExtraTimer(out bool nw, out float s);
        return $"보스 제한시간 창 {has} · 남은 {s:F1}s";
    }
}
