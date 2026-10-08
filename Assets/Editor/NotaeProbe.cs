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

    // 노태현 스킬 ①~⑤ 결정론 점검(폭발형 개정 뒤): 효과별 explosive 표시·피해.
    static string Direct()
    {
        if (nt == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        var allEs = new[] { e1, e2, eFar, boss };
        nt.AddBuff(UnitAttacker.YoonseoBuffId, 0f);
        sb.Append($"\n공격력 {nt.AttackDamage:F0} · 라인 폭증 레벨 {UnitAttacker.LaneExplosiveAmpLevels(0)} · e1 A11S {e1.PercentDamageTakenMultiplier:F2} · 보스 A11S {boss.PercentDamageTakenMultiplier:F2}");
        string[] names = { "①반사회적인격", "②하체부실(오라)", "③수단과방법", "④시너지폭발", "⑤돌발행동" };
        for (int i = 0; i < data.skills.Count; i++)
        {
            var sk = data.skills[i];
            var ex = sk.levels[0].effects.Where(e => e.kind == SkillEffectKind.Damage).Select(e => e.explosive ? "폭발형" : "일반").ToList();
            sb.Append($"\n{names[i]} · 피해 효과 {ex.Count}개 [{string.Join(", ", ex)}]");
        }
        foreach (int i in new[] { 0, 2, 3 })
        {
            Refill_(allEs); CastLevel(i, e1); double d = Drop(e1);
            Refill_(allEs); CastLevel(i, boss); double b = Drop(boss);
            sb.Append($"\n{names[i]} 실제 피해 · 일반 적 {d:N0} · 보스 {b:N0}");
        }
        Refill_(allEs); CastLevel(4, e1); sb.Append($"\n⑤ 돌발행동(직접) · 일반 e1 {Drop(e1):N0}");
        Refill_(allEs); CastLevel(4, boss); sb.Append($" · 보스 {Drop(boss):N0}");
        Refill_(allEs);
        return sb.ToString();
    }

    // (가) 배율표 — 같은 적에게 일반 마뎀(AP·Spells)과 폭발형 하나씩 100만을 실제 경로(DealSkillDamage)로 넣고, 마깎·마뎀증폭·폭뎀증폭을 하나씩 켠다.
    static EnemyDummy tableEnemy;
    static double Hit(EnemyDummy e, bool explosive, DamageType dt = DamageType.AP)
    {
        HpField.SetValue(e, Refill);
        var effect = new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.SingleTarget, multiplier = 1000000f, damageType = dt, attackType = AttackType.Spells, explosive = explosive };
        typeof(UnitAttacker).GetMethod("DealSkillDamage", NP).Invoke(nt, new object[] { effect, e, 0f });
        return Refill - e.Hp;
    }
    static string Table()
    {
        if (nt == null) return "❌ Setup 먼저";
        var sb = new StringBuilder($"라인 폭증 레벨 {UnitAttacker.LaneExplosiveAmpLevels(0)} (노태현 {(nt != null ? "있음" : "없음")})");
        EnemyData normal = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        EnemyData bossData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        Vector3 c = LaneMarker.Get(0).LaneCenter + new Vector3(0f, 0f, 600f);
        foreach (var (label, ed) in new[] { ("일반 적(R01)", normal), ("보스(R60)", bossData) })
        {
            sb.Append($"\n── {label} · 신 난이도 방어가산 {(ed.isBoss ? DifficultyTable.BossArmorBonus(DifficultyManager.Instance.Current) : 0f):F1} ──");
            string[] steps = { "기준", "마깎 +10레벨", "마뎀증폭 +10레벨", "폭뎀증폭 +3레벨" };
            for (int k = 0; k < steps.Length; k++)
            {
                var e = MakeEnemy(ed, c + new Vector3(k * 10f, 0f, 0f));
                if (ed.isBoss) e.DifficultyArmorBonus = DifficultyTable.BossArmorBonus(DifficultyManager.Instance.Current);
                if (k == 1) e.AddAegrStack(10);
                if (k == 2) e.AddAisrStack(10);
                if (k == 3) e.AddA11SStack(3);
                double gen = Hit(e, false), exp = Hit(e, true), adGen = Hit(e, false, DamageType.AD);
                sb.Append($"\n{steps[k],-14} · 마저항 전체 {e.EffectiveMagicMultiplier:F3} 기본 {e.BaseMagicResistMultiplier:F3} · A11S {e.PercentDamageTakenMultiplier:F3} → 일반마뎀(AP) {gen:N0} · 폭발형(AP) {exp:N0} · 일반마뎀(AD+방어) {adGen:N0}");
                Object.Destroy(e.gameObject);
            }
        }
        return sb.ToString();
    }

    static string KillNotae()
    {
        foreach (var u in UnitIdentity.Active.ToArray()) if (u != null && u.Data == data) Object.Destroy(u.gameObject);
        return "노태현 제거";
    }

    // (다) 신 보통 웨이브 하나 — 실제 몹이 받는 마저항·A11S·실제 배율. gameshot: ... mode:신 click:... wait:(몹이 나올 때까지) call:NotaeProbe.WaveReport
    static string WaveReport()
    {
        var sb = new StringBuilder($"난이도 {DifficultyManager.Instance.Current} · 적 {EnemyDummy.Active.Count}기");
        EnemyDummy m = null;
        foreach (var e in EnemyDummy.Active) if (e != null && !e.IsBoss) { m = e; break; }
        if (m == null) return sb.Append(" · 일반 몹 없음").ToString();
        if (nt == null) { data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_노태현_AP.asset"); nt = Object.FindFirstObjectByType<UnitAttacker>(); }
        var aegrLv = typeof(EnemyDummy).GetProperty("AegrBaseLevel", NP);
        sb.Append($"\n{m.name} · EnemyData 마방계수 {m.Data.magicArmorMultiplier:F3} · 난이도 Aegr 레벨 오프셋 {Mathf.RoundToInt((m.BaseMagicResistMultiplier - m.Data.magicArmorMultiplier) / 0.01f)} · Aegr 기본 레벨 {aegrLv?.GetValue(m)} · 마저항 기본 {m.BaseMagicResistMultiplier:F3} · 전체(마깎·증폭 포함) {m.EffectiveMagicMultiplier:F3} · A11S 계수 {m.PercentDamageTakenMultiplier:F3} · 방어 {m.EffectiveArmor:F1}");
        double gen = Hit(m, false), exp = Hit(m, true);
        sb.Append($"\n실제 100만 → 일반 마뎀(AP) {gen:N0} · 폭발형(AP) {exp:N0}");
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
        return $"실제 {span:F1}초 · 평타 {hits}타 · 시전 [{string.Join(", ", casts)}] · 게이지 {nt.ShownLifeNow}/{nt.ShownLifeMax} · 라인 폭증 레벨 {UnitAttacker.LaneExplosiveAmpLevels(0)}";
    }

    // ── 신 R60 보스 앞 N초 피해(TranscendBossProbe와 같은 방식 — double 누적·표적 1e9 매 프레임 채움·신 방어 가산). 6기(박민석·박민수·엄태웅·최상호AP·이재윤·노태현). ──
    // gameshot: ... click?:신 mode:신 wait:2 call:NotaeProbe.BossSetup wait:1 call:NotaeProbe.BossMark wait:60 call:NotaeProbe.BossReport
    static readonly string[] BossNames = { "초월_박민석_ADAP", "초월_박민수_AD", "초월_엄태웅_AD", "초월_최상호_AP", "초월_이재윤_AD", "초월_노태현_AP" };
    class BossPair { public UnitAttacker unit; public EnemyDummy boss; public UnitData data; public double dealt, dealtMark; public int hits0; }
    static readonly List<BossPair> bossPairs = new List<BossPair>();
    static float bossMarkT;

    static string BossSetup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        EnemyData bd = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R60_정윤식.asset");
        SkillTelemetry.Reset(); SkillTelemetry.Enabled = true;
        float armorBonus = DifficultyTable.BossArmorBonus(DifficultyManager.Instance.Current);
        bossPairs.Clear();
        int i = 0;
        foreach (string n in BossNames)
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
            if (d == null) { i++; continue; }
            Vector3 p = LaneMarker.Get(0).LaneCenter + new Vector3(-400f + i * 200f, 0f, 200f);
            var go = spawner.Spawn(d, p, 0);
            var ag = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null && ag.enabled) ag.Warp(p);
            var e = MakeEnemy(bd, p + new Vector3(0f, 0f, 22f));
            e.DifficultyArmorBonus = armorBonus;
            bossPairs.Add(new BossPair { unit = go.GetComponent<UnitAttacker>(), boss = e, data = d });
            i++;
        }
        EditorApplication.update -= BossTick; EditorApplication.update += BossTick;
        return $"세움 {bossPairs.Count}기 · 난이도 {DifficultyManager.Instance.Current} · 보스 실효방어 {(bossPairs.Count > 0 ? bossPairs[0].boss.EffectiveArmor : 0):F1} · 마방 {(bossPairs.Count > 0 ? bossPairs[0].boss.EffectiveMagicMultiplier : 0):F3} · A11S {(bossPairs.Count > 0 ? bossPairs[0].boss.PercentDamageTakenMultiplier : 0):F3}";
    }
    static void BossTick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= BossTick; return; }
        foreach (var p in bossPairs) { if (p.boss == null) continue; p.dealt += Refill - p.boss.Hp; HpField.SetValue(p.boss, Refill); }
    }
    static string BossMark() { bossMarkT = Time.time; foreach (var p in bossPairs) { p.dealtMark = p.dealt; p.hits0 = p.unit.BasicHitCount; } return "표식"; }
    static string BossReport()
    {
        float span = Mathf.Max(0.01f, Time.time - bossMarkT);
        EditorApplication.update -= BossTick;
        var sb = new StringBuilder($"게임 시간 {span:0.0}초 · 난이도 {DifficultyManager.Instance.Current}");
        foreach (var p in bossPairs)
        {
            double dmg = p.dealt - p.dealtMark;
            string ch = string.Join(" ", SkillTelemetry.ChannelsOf(p.data).Select(c => $"{c} {SkillTelemetry.DamageOf(p.data, c):N0}"));
            sb.Append($"\n{p.data.name,-18} 피해 {dmg:N0} ({dmg / span:N0}/초) · 평타 {p.unit.BasicHitCount - p.hits0}타 · 채널(누계) {ch}");
        }
        return sb.ToString();
    }
}
