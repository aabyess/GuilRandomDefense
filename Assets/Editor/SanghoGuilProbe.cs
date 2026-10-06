using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 최상호 구일 점검(10-06) — gameshot: call:ShopSlotProbe.Fund call:SanghoGuilProbe.Setup wait:3 call:SanghoGuilProbe.Report snap:… 
// Setup: 최상호 구일 + 전설 둘(박민석·노태현) + 희귀 하나를 레인에 세운다. Report: 오라(공격력 % = 0.2 + 0.04×전설 이상 수) · 아군 부여 · 소환(부채꼴 각도·거리·인벤토리 제외·재소환 시 시간 리셋) · 보스 배율.
static class SanghoGuilProbe
{
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static UnitIdentity sangho, legendA, legendB, rare;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        System.Func<UnitData, UnitIdentity> make = d => spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0).GetComponent<UnitIdentity>();
        sangho = make(Roster("초월_최상호_AD"));
        legendA = make(Roster("전설적인_박민석"));
        legendB = make(Roster("전설적인_노태현"));
        rare = make(Roster("희귀함_고어진"));
        return $"세움: {sangho.Data.unitName}({sangho.Data.skills.Count}스킬) · 전설 2 · 희귀 1 · 최상호 위치 {sangho.transform.position:F0}";
    }

    static string Report()
    {
        if (!Application.isPlaying || sangho == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        UnitAttacker a = sangho.GetComponent<UnitAttacker>();
        sb.AppendLine($"[오라] 최상호 공격력% {a.PercentAttackPowerBonus:F3} · 박민석(전설) {legendA.GetComponent<UnitAttacker>().PercentAttackPowerBonus:F3} · 노태현(전설) {legendB.GetComponent<UnitAttacker>().PercentAttackPowerBonus:F3} · 고어진(희귀) {rare.GetComponent<UnitAttacker>().PercentAttackPowerBonus:F3} (기대: 전설 이상 3기(최상호 포함) → 0.2+0.12=0.32)");
        var grantedField = typeof(UnitAttacker).GetField("grantedSkills", BindingFlags.NonPublic | BindingFlags.Instance);
        System.Func<UnitIdentity, int> granted = u => ((System.Collections.ICollection)grantedField.GetValue(u.GetComponent<UnitAttacker>())).Count;
        sb.AppendLine($"[부여] 박민석 빌린 스킬 {granted(legendA)} · 노태현 {granted(legendB)} · 고어진(희귀) {granted(rare)}(기대 1·1·0) · 최상호 자기 스킬 {sangho.Data.SkillCount}개");

        // 소환 — 시전 효과를 직접 부른다(평타 확률 10%를 기다리지 않는다)
        SkillData summonSkill = sangho.Data.skills.First(s => s.name.Contains("상호파집결"));
        SkillEffect effect = summonSkill.levels[0].effects[0];
        var summonFor = typeof(UnitAttacker).GetMethod("SummonFor", BindingFlags.NonPublic | BindingFlags.Instance);
        int inventoryBefore = PlayerContext.Get(0).UnitInventory.Members.Count;
        int summonsBefore = UnitIdentity.Active.Count(u => u != null && u.IsSummon);
        summonFor.Invoke(a, new object[] { effect });
        var summons = UnitIdentity.Active.Where(u => u != null && u.IsSummon).ToList();
        Vector3 forward = sangho.transform.forward; forward.y = 0f; forward.Normalize();
        sb.AppendLine($"[소환] 소환수 {summonsBefore}→{summons.Count}기 · 인벤토리 {inventoryBefore}→{PlayerContext.Get(0).UnitInventory.Members.Count}(기대 같음) · 최상호가 보는 방향 {forward:F2}");
        foreach (UnitIdentity s in summons)
        {
            Vector3 d = s.transform.position - sangho.transform.position; d.y = 0f;
            float angle = Vector3.SignedAngle(forward, d, Vector3.up);
            sb.AppendLine($"   · {s.Data.unitName}({s.Data.grade}) 거리 {d.magnitude:F1} · 각도 {angle:F0}° · TimedLife {(s.GetComponent<TimedLife>() != null)} · NavMesh {(s.GetComponent<UnityEngine.AI.NavMeshAgent>()?.isOnNavMesh)}");
        }
        var remainingField = typeof(TimedLife).GetField("remaining", BindingFlags.NonPublic | BindingFlags.Instance);
        if (summons.Count > 0) summons[0].GetComponent<TimedLife>().Begin(3f);   // 일부러 줄여 놓고 재소환이 20초로 되돌리는지 본다
        summonFor.Invoke(a, new object[] { effect });
        float after = summons.Count > 0 ? (float)remainingField.GetValue(summons[0].GetComponent<TimedLife>()) : -1f;
        int summonsAgain = UnitIdentity.Active.Count(u => u != null && u.IsSummon);
        sb.AppendLine($"[재소환] 소환수 {summonsAgain}기(기대 3 그대로) · 첫 소환수 남은 시간 3 → {after:F1}초(기대 ≈20)");

        // 보스 배율
        var factor = typeof(UnitAttacker).GetMethod("BossDamageFactor", BindingFlags.NonPublic | BindingFlags.Instance);
        EnemyDummy boss = EnemyDummy.Active.FirstOrDefault(e => e != null && e.IsBoss);
        EnemyDummy normal = EnemyDummy.Active.FirstOrDefault(e => e != null && !e.IsBoss);
        sb.AppendLine($"[보잡] 보스 {(boss != null ? "있음" : "없음")}: 배율 {(boss != null ? ((float)factor.Invoke(a, new object[] { boss })).ToString("F2") : "-")} · 일반 적 배율 {(normal != null ? ((float)factor.Invoke(a, new object[] { normal })).ToString("F2") : "-")}(기대 1.30 / 1.00) · 전설 박민석(스킬 없음) 보스 배율 {(boss != null ? ((float)factor.Invoke(legendA.GetComponent<UnitAttacker>(), new object[] { boss })).ToString("F2") : "-")}(기대 1.00)");
        return sb.ToString();
    }
    // 소환 부채꼴 사진용 — 카메라를 최상호 쪽으로 가깝게(RtsCameraController.FlyTo).
    static string FocusSangho()
    {
        if (!Application.isPlaying || sangho == null) return "❌ Setup 먼저";
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam == null) return "❌ 카메라 없음";
        cam.FlyTo(sangho.transform.position + sangho.transform.forward * 40f, 180f);
        return $"카메라 → 최상호 앞쪽 {sangho.transform.position:F0}";
    }

    // 보스 라운드(jump:10) 뒤 — 실제 보스에 대한 배율과, 최상호가 보스를 평타로 한 방 칠 때 들어가는 피해/방어 계산 전 값 비교.
    static string BossReport()
    {
        if (!Application.isPlaying || sangho == null) return "❌ Setup 먼저";
        var factor = typeof(UnitAttacker).GetMethod("BossDamageFactor", BindingFlags.NonPublic | BindingFlags.Instance);
        EnemyDummy boss = EnemyDummy.Active.FirstOrDefault(e => e != null && e.IsBoss);
        EnemyDummy normal = EnemyDummy.Active.FirstOrDefault(e => e != null && !e.IsBoss);
        UnitAttacker a = sangho.GetComponent<UnitAttacker>();
        System.Func<EnemyDummy, string> f = e => e == null ? "-" : ((float)factor.Invoke(a, new object[] { e })).ToString("F2");
        System.Func<UnitIdentity, string> g = u => boss == null ? "-" : ((float)factor.Invoke(u.GetComponent<UnitAttacker>(), new object[] { boss })).ToString("F2");
        return $"[보잡] 보스 {(boss != null ? boss.name : "없음")} 배율 {f(boss)} · 일반 적 {(normal != null ? normal.name : "없음")} 배율 {f(normal)}(기대 1.30 / 1.00) · 전설 박민석 보스 배율 {g(legendA)}(기대 1.00) · 최상호 평타 {a.AttackDamage:F0}";
    }
}
