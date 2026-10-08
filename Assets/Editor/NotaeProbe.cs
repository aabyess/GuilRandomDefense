using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 노태현 AP 재사양(사장님 10-08) 실측 탐침(구현담당2) — gameshot:
//   gameshot x.png 2 1920x1080 click?:쉬움 wait:2 call:NotaeProbe.Setup wait:1 call:NotaeProbe.Direct wait:20 call:NotaeProbe.Live
// Direct = 스킬 ①~⑤를 결정론으로 한 번씩 불러(CastSkillLevel 직접) 적 체력 감소를 잰다. Live = 실제 평타로 돈 N초의 시전 횟수·게이지.
static class NotaeProbe
{
    const float Refill = 1e9f;
    static UnitAttacker nt;
    static UnitData data;
    static EnemyDummy e1, e2, eFar, boss;
    static GameObject allyGo;
    static float allyBaseSpeed;
    static float liveStart;
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly FieldInfo HpField = typeof(EnemyDummy).GetField("hp", NP);

    static EnemyDummy MakeEnemy(EnemyData d, Vector3 at)
    {
        GameObject go = Object.Instantiate(d.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
        var e = go.GetComponent<EnemyDummy>();
        e.Initialize(d, 1f); e.SetLane(0);
        HpField.SetValue(e, Refill);
        return e;
    }

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_노태현_AP.asset");
        EnemyData normal = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        EnemyData bossData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        UnitData allyData = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/흔함_강주혁.asset");
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        SkillTelemetry.Reset(); SkillTelemetry.Enabled = true;
        nt = spawner.Spawn(data, c, 0).GetComponent<UnitAttacker>();
        allyGo = spawner.Spawn(allyData, c + new Vector3(30f, 0f, 0f), 0);
        var ag = allyGo.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) allyBaseSpeed = allyData.moveSpeed;
        e1 = MakeEnemy(normal, c + new Vector3(0f, 0f, 15f));
        e2 = MakeEnemy(normal, c + new Vector3(0f, 0f, 35f));      // e1에서 20 — 범위 300(원작)/WorldScale 안
        eFar = MakeEnemy(normal, c + new Vector3(0f, 0f, 400f));   // 아주 멀다 — 범위·오라 밖
        boss = MakeEnemy(bossData, c + new Vector3(30f, 0f, 20f));
        return $"세움 · WorldScale {WorldScale.Value} · 범위300→{300f / WorldScale.Value:F1} · 오라600→{600f / WorldScale.Value:F1} · e1 PV {e1.PointValue} boss PV {boss.PointValue} isBoss {boss.IsBoss} · 스킬 {data.skills.Count}개 [{string.Join(", ", data.skills.Select(s => s.skillName.Split(' ')[0]))}]";
    }

    static void Refill_(params EnemyDummy[] es) { foreach (var e in es) if (e != null) HpField.SetValue(e, Refill); }
    static double Drop(EnemyDummy e) => Refill - e.Hp;

    static void CastLevel(int skillIndex, EnemyDummy primary)
    {
        SkillData s = data.skills[skillIndex];
        MethodInfo cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", NP);
        cast.Invoke(nt, new object[] { s.levels[0], s.levels[0].WorldRange, primary, nt.AttackDamage });
    }

    static void SetBonusCache(bool off)
    {
        var byLane = (Dictionary<int, float>)typeof(UnitAttacker).GetField("laneAreaBonusByLane", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var at = typeof(UnitAttacker).GetField("laneAreaBonusAt", BindingFlags.NonPublic | BindingFlags.Static);
        if (off) { byLane.Clear(); at.SetValue(null, Time.time); }   // 0.5초 캐시를 비워 둔 채 고정 → 계수 1
        else at.SetValue(null, -10f);                                  // 다시 계산 → 0.15
    }

    static string Direct()
    {
        if (nt == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        var allEs = new[] { e1, e2, eFar, boss };
        nt.AddBuff(UnitAttacker.YoonseoBuffId, 0f);   // 방어 무시(최윤서 강화) — 낮은 방어 편차 없이 공칭값에 가깝게
        sb.Append($"\n공격력 {nt.AttackDamage:F0} · 라인 범위 계수(켜짐) {UnitAttacker.LaneAreaDamageFactor(0):F3}");

        // ① 반사회적인격: 공격력×30% + 300,000 마법, 범위 300 안 적 모두
        Refill_(allEs); CastLevel(0, e1);
        sb.Append($"\n① 반사회적인격 · e1 {Drop(e1):N0} · e2 {Drop(e2):N0}(범위 안) · 멀리 {Drop(eFar):N0}(범위 밖 0이어야) · 기대 e1 ≈ {nt.AttackDamage * 0.3f + 300000f:N0}");

        // ① 라인 범위 계수 켜짐/꺼짐 비교(사장님 10-08: 범위 피해 전부 +15%)
        SetBonusCache(true); Refill_(allEs); CastLevel(0, e1); double a1off = Drop(e1);
        SetBonusCache(false); Refill_(allEs); CastLevel(0, e1); double a1on = Drop(e1);
        sb.Append($"\n① 계수 끔 {a1off:N0} · 켬 {a1on:N0} · 비율 {a1on / System.Math.Max(1, a1off):F4}(1.15 기대 — ①도 범위)");
        // 다른 유닛 범위 스킬: 로스터에서 target Enemies Damage 효과가 든 스킬 하나를 찾아 그 유닛을 세워 같은 식으로
        UnitData other = null; int otherIdx = -1;
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }))
        {
            var ud = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guid));
            if (ud == null || ud == data || ud.skills == null) continue;
            for (int k = 0; k < ud.skills.Count && other == null; k++)
            {
                var sk = ud.skills[k];
                if (sk == null || sk.levels == null || sk.levels.Count == 0 || sk.triggerType == SkillTriggerType.Aura) continue;
                if (sk.levels[0].range > 0f && sk.levels[0].effects.Any(e => e != null && e.kind == SkillEffectKind.Damage && e.target == SkillTargetKind.Enemies && e.basis == SkillEffectBasis.Flat && e.multiplier > 0f && e.zoneTickInterval <= 0f && e.lineLength <= 0f && e.hitCount <= 1)) { other = ud; otherIdx = k; }
            }
            if (other != null) break;
        }
        if (other != null)
        {
            var spawner = Object.FindFirstObjectByType<UnitSpawner>();
            var ou = spawner.Spawn(other, LaneMarker.Get(0).LaneCenter + new Vector3(0f, 0f, -20f), 0).GetComponent<UnitAttacker>();
            SkillData osk = other.skills[otherIdx];
            MethodInfo cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", NP);
            SetBonusCache(true); Refill_(allEs); cast.Invoke(ou, new object[] { osk.levels[0], osk.levels[0].WorldRange, e1, ou.AttackDamage }); double oOff = Drop(e1);
            SetBonusCache(false); Refill_(allEs); cast.Invoke(ou, new object[] { osk.levels[0], osk.levels[0].WorldRange, e1, ou.AttackDamage }); double oOn = Drop(e1);
            sb.Append($"\n다른 유닛 범위 스킬 {other.name}/{osk.skillName.Split(' ')[0]} · 끔 {oOff:N0} · 켬 {oOn:N0} · 비율 {oOn / System.Math.Max(1, oOff):F4}(1.15 기대)");
        }
        else sb.Append("\n다른 유닛 범위 스킬 못 찾음");

        // ③ 가리지않는수단과방법: 단일 300,000 마법 + 2초 스턴, 보스·광폭화 ×1.3
        Refill_(allEs); CastLevel(2, e1);
        double n3 = Drop(e1); bool stunE1 = e1.IsStunned;
        Refill_(allEs); CastLevel(2, boss);
        double b3 = Drop(boss);
        sb.Append($"\n③ 수단과방법 · 일반 {n3:N0}(스턴 {stunE1}) · 보스 {b3:N0}(스턴 {boss.IsStunned}) · 보스/일반 = {b3 / System.Math.Max(1, n3):F3}(1.3 기대) · e2 {Drop(e2):N0}(단일이라 0이어야)");

        // ④ 시너지폭발: 300,000 마법 + 700,000 범위(+15%), 라인 계수 켜짐/꺼짐 비교
        SetBonusCache(true); Refill_(allEs); CastLevel(3, e1);
        double off = Drop(e1); double offE2 = Drop(e2);
        SetBonusCache(false); Refill_(allEs); CastLevel(3, e1);
        double on = Drop(e1); double onE2 = Drop(e2);
        sb.Append($"\n④ 시너지폭발 · 오라 끔 e1 {off:N0} · 오라 켬 e1 {on:N0} · 비율 {on / System.Math.Max(1, off):F4}(기대 {(300000.0 + 700000.0 * 1.15) / 1000000.0:F4}) · e2 끔 {offE2:N0} 켬 {onE2:N0}");

        // ④(a) 평타 광역: 라인 계수 켜짐/꺼짐
        MethodInfo splash = typeof(UnitAttacker).GetMethod("ApplyAttackSplash", NP);
        SetBonusCache(true); Refill_(allEs); splash?.Invoke(nt, new object[] { e1 });
        double sOff = Drop(e2);
        SetBonusCache(false); Refill_(allEs); splash?.Invoke(nt, new object[] { e1 });
        double sOn = Drop(e2);
        sb.Append($"\n④(a) 평타 광역 e2 · 끔 {sOff:N0} · 켬 {sOn:N0} · 비율 {sOn / System.Math.Max(1, sOff):F4}(1.15 기대)");

        // ⑤ 돌발행동: 일반=현재체력 15%(e1은 Refill=1e9라 1.5e8), 보스=300,000 고정
        Refill_(allEs); CastLevel(4, e1);
        double d5 = Drop(e1); double d5b = Drop(boss); Refill_(allEs); CastLevel(4, boss);
        sb.Append($"\n⑤ 돌발행동(발동 직접) · 일반 e1 {d5:N0}(1e9×15%≈1.5e8 기대) · e2 {Drop(e2):N0}(범위 안 같이) · 보스 {Drop(boss):N0}(300,000 기대)");

        // ⑤ 게이지 로직: 99 + 평타 1 → 100 → 발동 → 0
        typeof(UnitAttacker).GetField("lifeGaugeCounter", NP).SetValue(nt, 99);
        typeof(UnitAttacker).GetField("lifeGaugeInitialized", NP).SetValue(nt, true);
        int before = SkillTelemetry.CastLog.Count;
        typeof(UnitAttacker).GetMethod("TryCastOnHitSkill", NP).Invoke(nt, new object[] { e1 });
        var fired = SkillTelemetry.CastLog.Skip(before).Select(c => c.skill.Split(' ')[0]).ToList();
        sb.Append($"\n⑤ 게이지 99→평타 1회 → 발동 스킬 [{string.Join(", ", fired)}] · 게이지 지금 {nt.ShownLifeNow}/{nt.ShownLifeMax}");
        Refill_(allEs);
        return sb.ToString();
    }

    static string Aura()
    {
        if (nt == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        var agent = allyGo.GetComponent<UnityEngine.AI.NavMeshAgent>();
        sb.Append($"② 하체부실 오라 · 적 e1 이속 배율 {e1.EffectiveSlowMultiplier:F2}(0.50 기대) · 보스 {boss.EffectiveSlowMultiplier:F2} · 멀리 {eFar.EffectiveSlowMultiplier:F2}(1.00 기대)");
        sb.Append($" · 아군 속도 {(agent != null ? agent.speed : -1f):F2} / 기본 {allyBaseSpeed:F2}(절반 기대) · 노태현 본인 속도 {(nt.TryGetComponent(out UnityEngine.AI.NavMeshAgent na) ? na.speed : -1f):F2}/{data.moveSpeed:F2}(변함 없어야)");
        return sb.ToString();
    }

    static string LiveStart() { liveStart = Time.time; SkillTelemetry.Reset(); SkillTelemetry.Enabled = true; return "실제 평타 시작"; }

    static string Live()
    {
        if (nt == null) return "❌ Setup 먼저";
        float span = Time.time - liveStart;
        var casts = SkillTelemetry.CastLog.Where(c => c.unit == data).GroupBy(c => c.skill.Split(' ')[0]).Select(g => $"{g.Key} {g.Count()}회").ToList();
        int hits = SkillTelemetry.HitsOf(data);
        return $"실제 {span:F1}초 · 평타 {hits}타 · 시전 [{string.Join(", ", casts)}] · 게이지 {nt.ShownLifeNow}/{nt.ShownLifeMax} · 라인 범위 계수 {UnitAttacker.LaneAreaDamageFactor(0):F2}";
    }

    // ── 신 R60 보스 앞 N초 피해(TranscendBossProbe와 같은 방식 — double 누적·표적 1e9 매 프레임 채움·신 방어 가산). 노태현 한 기만. ──
    // gameshot: ... click?:신 mode:신 wait:2 call:NotaeProbe.BossSetup wait:1 call:NotaeProbe.BossMark wait:60 call:NotaeProbe.BossReport
    static UnitAttacker bossUnit; static EnemyDummy bossTarget; static double bossDealt, bossDealtMark; static float bossMarkT; static int bossHits0;
    static string BossSetup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_노태현_AP.asset");
        EnemyData bd = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        Vector3 p = LaneMarker.Get(0).LaneCenter + new Vector3(-400f, 0f, 200f);
        SkillTelemetry.Reset(); SkillTelemetry.Enabled = true;
        var go = spawner.Spawn(data, p, 0);
        bossUnit = go.GetComponent<UnitAttacker>();
        var ag = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null && ag.enabled) ag.Warp(p);
        float armorBonus = DifficultyTable.BossArmorBonus(DifficultyManager.Instance.Current);
        bossTarget = MakeEnemy(bd, p + new Vector3(0f, 0f, 22f));
        bossTarget.DifficultyArmorBonus = armorBonus;
        bossDealt = 0;
        EditorApplication.update -= BossTick; EditorApplication.update += BossTick;
        return $"세움 · 난이도 {DifficultyManager.Instance.Current} · 보스 실효방어 {bossTarget.EffectiveArmor:F1}";
    }
    static void BossTick()
    {
        if (!Application.isPlaying || bossTarget == null) { EditorApplication.update -= BossTick; return; }
        bossDealt += Refill - bossTarget.Hp; HpField.SetValue(bossTarget, Refill);
    }
    static string BossMark() { bossMarkT = Time.time; bossDealtMark = bossDealt; bossHits0 = bossUnit.BasicHitCount; return "표식"; }
    static string BossReport()
    {
        float span = Mathf.Max(0.01f, Time.time - bossMarkT);
        double dmg = bossDealt - bossDealtMark;
        string ch = string.Join(" ", SkillTelemetry.ChannelsOf(data).Select(c => $"{c} {SkillTelemetry.DamageOf(data, c):N0}"));
        var casts = SkillTelemetry.CastLog.Where(c => c.unit == data).GroupBy(c => c.skill.Split(' ')[0]).Select(g => $"{g.Key} {g.Count()}회");
        EditorApplication.update -= BossTick;
        return $"게임 시간 {span:0.0}초 · 난이도 {DifficultyManager.Instance.Current} · {data.name} 공격력 {bossUnit.AttackDamage:F0} 간격 {bossUnit.AttackInterval:F3}초 · 평타 {bossUnit.BasicHitCount - bossHits0}타 · 방어 {bossTarget.EffectiveArmor:F1} · 피해 {dmg:N0} ({dmg / span:N0}/초) · 채널(누계) {ch} · 시전 [{string.Join(", ", casts)}]";
    }
}
