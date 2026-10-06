using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 전설 스킬 점검(10-06) — gameshot: call:ShopSlotProbe.Fund call:LegendProbe.Setup wait:30 call:LegendProbe.Report
// Setup: 전설 9기(대응 신규 4 + 복제 5)를 세운다. Report: 스킬 연결(널 0)·유닛 필드 · 드래곤 오라(아군 공격력 %)·노태현 지대·킹 산성탄·드래곤 스톰프 실제 발동 결과.
static class LegendProbe
{
    static readonly string[] Names = { "김용태", "노태현", "양재모", "구주호", "김건", "김민규", "김민준", "김정래", "박병규", "임건웅", "백기현", "이재윤", "신문철", "박민석", "신지우", "임채현", "이일중" };
    static UnitIdentity[] units;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        units = Names.Select(n =>
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/전설적인_{n}.asset");
            return spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0).GetComponent<UnitIdentity>();
        }).ToArray();
        return $"세움 {units.Length}기";
    }

    static string Report()
    {
        if (!Application.isPlaying || units == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        foreach (UnitIdentity u in units)
        {
            var sk = u.Data.skills ?? new System.Collections.Generic.List<SkillData>();
            int nulls = sk.Count(s => s == null);
            sb.AppendLine($"[{u.Data.name}] 스킬 {sk.Count}(널 {nulls}) · 평타 {u.Data.attackType} · 크리 {u.Data.critChance} · LIFE게이지 {u.Data.lifeGaugeMax} · 마나오라 {u.Data.manaAuraRegenPerSecond}/{u.Data.manaAuraRange} :: " + string.Join(" / ", sk.Where(s => s != null).Select(s => s.skillName.Split('—')[0].Trim() + "(" + s.triggerType + ")")));
        }
        UnitIdentity dragon = units[0], nota = units[1], king = units[2];
        UnitAttacker da = dragon.GetComponent<UnitAttacker>();
        sb.AppendLine($"[드래곤 오라 A04K] 아군 공격력% — 드래곤 {da.PercentAttackPowerBonus:F2} · 노태현 {nota.GetComponent<UnitAttacker>().PercentAttackPowerBonus:F2} · 킹 {king.GetComponent<UnitAttacker>().PercentAttackPowerBonus:F2}(기대 0.25 — 반경 800 안일 때)");

        var enemies = EnemyDummy.Active.Where(e => e != null && !e.IsBoss && e.PointValue < 200f).ToList();
        sb.AppendLine($"[적] 일반 {enemies.Count}기");
        if (enemies.Count >= 5)
        {
            var hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (EnemyDummy e in enemies.Take(6)) hpField.SetValue(e, 1e9f);   // 죽지 않게 체력을 크게(피해 측정용)
            tested = enemies.Take(6).ToList();
            hpBefore = tested.Select(e => e.Hp).ToArray();
            var cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
            System.Action<UnitIdentity, string, EnemyDummy> fire = (u, key, t) =>
            {
                SkillData sk = u.Data.skills.First(x => x.skillName.Contains(key));
                cast.Invoke(u.GetComponent<UnitAttacker>(), new object[] { sk.levels[0], sk.levels[0].WorldRange, t, 0f });
            };
            UnitIdentity By(string n) => units.First(u => u.Data.name == "전설적인_" + n);
            armorBefore = tested.Select(e => e.EffectiveArmor).ToArray();
            fire(By("김용태"), "Legend3", tested[0]);      // 스톰프
            fire(By("양재모"), "Legend33", tested[1]);     // 산성탄
            fire(By("임건웅"), "A08M", tested[2]);         // 독
            fire(By("양재모"), "A10U", tested[3]);         // 화재
            summonsBefore = UnitIdentity.Active.Count(u => u != null && u.IsSummon);
            fire(By("구주호"), "Legend27", tested[4]);     // 소환(효과 chance 1 · 1/3)
            fire(By("신문철"), "Legend4 1/17", tested[4]); // 시키 소환체
            fire(By("이일중"), "A0U7", tested[4]);         // 시노부 분신
            sb.AppendLine($"[즉시] 스톰프 대상: 체력Δ {hpBefore[0] - tested[0].Hp:F0} · 스턴 {tested[0].IsStunned} · 산성탄 대상: 체력Δ {hpBefore[1] - tested[1].Hp:F0} · 방어 {armorBefore[1]:F1}→{tested[1].EffectiveArmor:F1}(기대 −34) · 소환체 {summonsBefore}→{UnitIdentity.Active.Count(u => u != null && u.IsSummon)}기(슈가 1~2 + 시키 1 + 시노부 1 기대 3~4)");
        }
        return sb.ToString();
    }

    static System.Collections.Generic.List<EnemyDummy> tested;
    static float[] hpBefore, armorBefore;
    static int summonsBefore;

    // 3.2초 뒤: 산성탄 지대·독·화재 DoT와 노태현 지대(0.4초마다 33,333)·샹크스 지대 누적 피해 — gameshot wait:3.2 뒤
    static string Later()
    {
        if (tested == null || tested.Count < 5) return "❌ 적 부족";
        var sb = new StringBuilder();
        string[] label = { "스톰프 대상", "산성탄 대상(85,000 + 지대 초당 42,500×2.7)", "독 대상(초당 200,000×6)", "화재 대상(초당 250,000×5)", "소환 대상" };
        for (int i = 0; i < 5; i++) sb.AppendLine($"[+3.2초] {label[i]}: 누적 체력Δ {hpBefore[i] - tested[i].Hp:F0} · 이속 배율 {tested[i].EffectiveSlowMultiplier:F2}");
        var far = tested.Count > 5 ? tested[5] : null;
        sb.AppendLine($"[참고] 아무 스킬 안 맞은 적(노태현 0.4초 지대 33,333 · 백기현 0.2초 지대 12,500 영향권이면 누적): 체력Δ {(far != null ? (hpBefore[5] - far.Hp).ToString("F0") : "-")}");
        int summons = UnitIdentity.Active.Count(u => u != null && u.IsSummon);
        sb.AppendLine($"[소환체] 지금 {summons}기 · " + string.Join(", ", UnitIdentity.Active.Where(u => u != null && u.IsSummon).Select(u => u.Data.unitName)));
        return sb.ToString();
    }

    // 노태현 지대 — 짧게 기다린 뒤 일반 적 체력 변화(gameshot wait:2 뒤)
    static string ZoneCheck()
    {
        if (units == null) return "❌ Setup 먼저";
        var tracked = EnemyDummy.Active.Where(e => e != null && !e.IsBoss).Take(5).ToList();
        return $"[노태현 지대] 가까운 적 {tracked.Count}기 체력 {string.Join(", ", tracked.Select(e => $"{e.Hp:F0}/{e.MaxHp:F0}"))}";
    }
}
