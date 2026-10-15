using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 최상호 AD 「킹카가될뻔했던사나이」 새 사양(10-15) 점검 — gameshot: ... call:SanghoGuilProbe.Setup wait:3 call:SanghoGuilProbe.Report wait:1 call:SanghoGuilProbe.Skills
//   (실제 평타 확률: call:SanghoGuilProbe.LiveStart wait:30 call:SanghoGuilProbe.Live)
// Setup: 최상호 + 전설 둘(박민석·노태현) + 희귀 하나 + 랜덤유닛 하나(안 세는 등급) + 일반 적 2·보스 1.
// Report: 오라 공격력%(0.2 + 0.05×왕 등급 수) · 적 이감 · 소환(셋 동시·5초·소환 중 재소환 불가·소멸 뒤 다시 소환).
// Skills: 스킬1~3을 CastSkillLevel로 직접 불러 일반 적·보스의 체력 감소·방어깎기를 잰다(보스 전용 스킬3은 일반 적엔 안 나가야 한다).
static class SanghoGuilProbe
{
    const float Refill = 1e9f;
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly FieldInfo HpField = typeof(EnemyDummy).GetField("hp", NP);
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static UnitIdentity sangho, legendA, legendB, rare, randomUnit;
    static EnemyDummy normal, boss;
    static float liveStart;

    static EnemyDummy MakeEnemy(string path, Vector3 at)
    {
        EnemyData d = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
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
        LaneMarker lane = LaneMarker.Get(0);
        System.Func<UnitData, UnitIdentity> make = d => spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0).GetComponent<UnitIdentity>();
        SkillTelemetry.Reset(); SkillTelemetry.Enabled = true;
        sangho = make(Roster("초월_최상호_AD"));
        legendA = make(Roster("전설적인_박민석"));
        legendB = make(Roster("전설적인_노태현"));
        rare = make(Roster("희귀함_고어진"));
        Vector3 c = lane.LaneCenter;
        normal = MakeEnemy("Assets/Data/Enemies/Enemy_R01_박진웅.asset", c + new Vector3(0f, 0f, 15f));
        boss = MakeEnemy("Assets/Data/Enemies/Enemy_R60_정윤식.asset", c + new Vector3(30f, 0f, 20f));
        return $"세움: {sangho.Data.unitName}({sangho.Data.skills.Count}스킬) · 전설 2 · 희귀 1 · 최상호 {sangho.transform.position:F0} · 일반 PV {normal.PointValue} · 보스 PV {boss.PointValue}";
    }

    static string Report()
    {
        if (!Application.isPlaying || sangho == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        UnitAttacker a = sangho.GetComponent<UnitAttacker>();
        System.Func<UnitIdentity, string> pct = u => u.GetComponent<UnitAttacker>().PercentAttackPowerBonus.ToString("F3");
        sb.AppendLine($"[오라] 공격력% 최상호 {pct(sangho)} · 박민석(전설) {pct(legendA)} · 노태현(전설) {pct(legendB)} · 고어진(희귀) {pct(rare)} (기대: 왕 등급 3기(최상호 초월+전설 2) → 0.2+0.15=0.35, 희귀도 아군이라 받음)");
        sb.AppendLine($"[이감] 일반 적 이감 배수 {normal.EffectiveSlowMultiplier:F2} · 보스 {boss.EffectiveSlowMultiplier:F2} (기대 0.65 — 범위 850 안)");

        SkillData summonSkill = sangho.Data.skills.First(s => s.name.Contains("상호파집결"));
        SkillEffect effect = summonSkill.levels[0].effects[0];
        var summonFor = typeof(UnitAttacker).GetMethod("SummonFor", NP);
        int inv0 = PlayerContext.Get(0).UnitInventory.Members.Count;
        summonFor.Invoke(a, new object[] { effect });
        var summons = UnitIdentity.Active.Where(u => u != null && u.IsSummon).ToList();
        sb.AppendLine($"[소환] 소환수 {summons.Count}기(기대 3) · 인벤토리 {inv0}→{PlayerContext.Get(0).UnitInventory.Members.Count}(기대 같음) · 수명 {summonSkill.levels[0].effects[0].summonLifetime}초 · [{string.Join(", ", summons.Select(s => s.Data.unitName))}]");
        var remainingField = typeof(TimedLife).GetField("remaining", NP);
        if (summons.Count > 0) summons[0].GetComponent<TimedLife>().Begin(2f);   // 일부러 줄여 놓고 재소환이 시간을 되돌리지 않는지 본다
        summonFor.Invoke(a, new object[] { effect });
        float after = summons.Count > 0 ? (float)remainingField.GetValue(summons[0].GetComponent<TimedLife>()) : -1f;
        int again = UnitIdentity.Active.Count(u => u != null && u.IsSummon);
        sb.AppendLine($"[소환 중 재소환] 소환수 {again}기(기대 3 그대로) · 첫 소환수 남은 시간 2 → {after:F1}초(기대 2 그대로 — 되돌리지 않음)");
        return sb.ToString();
    }

    static double Drop(EnemyDummy e) => Refill - e.Hp;
    static void Refill_(params EnemyDummy[] es) { foreach (var e in es) if (e != null) HpField.SetValue(e, Refill); }

    static string Skills()
    {
        if (sangho == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        UnitAttacker a = sangho.GetComponent<UnitAttacker>();
        MethodInfo cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", NP);
        string[] names = { "수장(오라)", "후계자 스킬1", "후계자 스킬2", "후계자 스킬3", "집결", "왕의자질(오라)" };
        sb.Append($"공격력 {a.AttackDamage:F0} · 일반 적 방어 {normal.EffectiveArmor:F1} · 보스 방어 {boss.EffectiveArmor:F1}");
        foreach (int i in new[] { 1, 2, 3 })
        {
            SkillData s = sangho.Data.skills[i];
            SkillLevel lv = s.levels[0];
            foreach (var (label, e) in new[] { ("일반 적", normal), ("보스", boss) })
            {
                Refill_(normal, boss);
                float armor0 = e.Aid1Shred;
                bool primaryOk = SkillGate(lv, e);
                double before = e.Hp;
                if (primaryOk) cast.Invoke(a, new object[] { lv, lv.WorldRange, e, a.AttackDamage });
                sb.Append($"\n{names[i]} → {label}: 판정 {(primaryOk ? "통과" : "건너뜀(보스 전용)")} · 체력 −{Drop(e):N0} · 방깎 누적 {armor0:F0}→{e.Aid1Shred:F0}(상한 {EnemyDummy.Aid1ShredCap:F0})");
            }
        }
        // 아머브레이크 상한 — 스킬1을 90번 불러 75에서 멈추는지
        Refill_(normal, boss);
        SkillLevel l1 = sangho.Data.skills[1].levels[0];
        for (int k = 0; k < 90; k++) cast.Invoke(a, new object[] { l1, l1.WorldRange, normal, a.AttackDamage });
        sb.Append($"\n[아머브레이크 90회] 일반 적 방깎 {normal.Aid1Shred:F0}(기대 75) · 방어 {normal.EffectiveArmor:F1}");
        Refill_(normal, boss);
        return sb.ToString();
    }

    // primaryTargetCondition 판정(평타 대상이 조건을 못 채우면 스킬 자체를 건너뛴다)을 그대로 따라 한다.
    static bool SkillGate(SkillLevel lv, EnemyDummy target)
    {
        var m = typeof(UnitAttacker).GetMethod("PassesPointValueCondition", BindingFlags.NonPublic | BindingFlags.Static);
        return (bool)m.Invoke(null, new object[] { lv.primaryTargetCondition, lv.primaryTargetConditionValue, target });
    }

    static string LiveStart() { liveStart = Time.time; SkillTelemetry.Reset(); SkillTelemetry.Enabled = true; return "실제 평타 시작"; }

    static string Live()
    {
        if (sangho == null) return "❌ Setup 먼저";
        UnitData d = sangho.Data;
        float span = Time.time - liveStart;
        var casts = SkillTelemetry.CastLog.Where(c => c.unit == d).GroupBy(c => c.skill.Split(' ')[0] + " " + c.skill.Split(' ').ElementAtOrDefault(1)).Select(g => $"{g.Key} {g.Count()}회").ToList();
        int hits = SkillTelemetry.HitsOf(d);
        int summons = UnitIdentity.Active.Count(u => u != null && u.IsSummon);
        return $"실제 {span:F1}초 · 평타 {hits}타 · 시전 [{string.Join(", ", casts)}] · 지금 소환수 {summons}기 · 공격력% {sangho.GetComponent<UnitAttacker>().PercentAttackPowerBonus:F3}";
    }

    // 소환 부채꼴 사진용 — 카메라를 최상호 쪽으로 가깝게.
    static string FocusSangho()
    {
        if (!Application.isPlaying || sangho == null) return "❌ Setup 먼저";
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam == null) return "❌ 카메라 없음";
        cam.FlyTo(sangho.transform.position + sangho.transform.forward * 40f, 180f);
        return $"카메라 → 최상호 앞쪽 {sangho.transform.position:F0}";
    }
}
