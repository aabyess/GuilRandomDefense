using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 박민석 외동의악마 점검(10-06) — gameshot: call:ShopSlotProbe.Fund call:MinseokProbe.Setup jump:10 wait:5 call:MinseokProbe.Report
// 데이터 읽기 · 유닛삭제(가까운 일반 적 1기만·보스/스토리 제외·일반 적 없으면 게이트) · 마방깍 스택 · 채팅 「만년공복」.
static class MinseokProbe
{
    static UnitIdentity unit;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_박민석_ADAP.asset");
        unit = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0).GetComponent<UnitIdentity>();
        var sb = new StringBuilder($"세움: {unit.Data.unitName}(스킬 {unit.Data.skills.Count}): ");
        foreach (SkillData s in unit.Data.skills) sb.Append($"[{s.skillName.Split('—')[0].Trim()} {s.triggerType}]");
        return sb.ToString();
    }

    static string Report()
    {
        if (!Application.isPlaying || unit == null) return "❌ Setup 먼저";
        var sb = new StringBuilder();
        UnitAttacker a = unit.GetComponent<UnitAttacker>();
        var nearest = typeof(UnitAttacker).GetMethod("NearestNormalEnemy", BindingFlags.NonPublic | BindingFlags.Instance);
        var kill = typeof(UnitAttacker).GetMethod("KillNearestNormalEnemy", BindingFlags.NonPublic | BindingFlags.Instance);
        var bossFactor = typeof(UnitAttacker).GetMethod("BossDamageFactor", BindingFlags.NonPublic | BindingFlags.Instance);
        var all = EnemyDummy.Active.Where(e => e != null).ToList();
        int bosses = all.Count(e => e.IsBoss), story = all.Count(e => !e.IsBoss && e.PointValue >= 200f), normal = all.Count(e => e.PointValue < 200f && !e.IsBoss);
        sb.AppendLine($"[적] 전체 {all.Count} = 보스 {bosses} · 스토리(PV≥200 비보스) {story} · 일반 {normal}");
        // 가장 가까운 일반 적 하나가 실제로 죽는지, 보스·스토리가 안 죽는지(범위 무한)
        int bossAlive0 = all.Count(e => e.IsBoss);
        EnemyDummy target = (EnemyDummy)nearest.Invoke(a, new object[] { 1e9f });
        sb.AppendLine($"[대상] 가장 가까운 일반 적 = {(target != null ? target.name + " PV " + target.PointValue : "없음")}");
        int before = EnemyDummy.Active.Count(e => e != null);
        kill.Invoke(a, new object[] { 1e9f });
        // 사망 처리는 프레임 끝일 수 있어 Hp로도 본다
        sb.AppendLine($"[유닛삭제] 적 {before} → {EnemyDummy.Active.Count(e => e != null)} · 대상 체력 {(target != null ? target.Hp.ToString("F0") : "-")} (기대: 한 기만 죽음/체력 0 이하) · 보스 {bossAlive0}→{EnemyDummy.Active.Count(e => e != null && e.IsBoss)}(기대 같음)");
        // 범위 0 근처에서 일반 적이 없을 때 게이트
        var has = typeof(UnitAttacker).GetMethod("HasNormalEnemyInRange", BindingFlags.NonPublic | BindingFlags.Instance);
        sb.AppendLine($"[게이트] 범위 1(바로 앞 아무도 없음) 안 일반 적 있나 = {has.Invoke(a, new object[] { 1f })}(기대 False)");
        if (bosses > 0)
            sb.AppendLine($"[보잡] 보스 배율 {((float)bossFactor.Invoke(a, new object[] { all.First(e => e.IsBoss) })):F2}(기대 1.30)");
        var box = Object.FindFirstObjectByType<GameChatBox>();
        sb.AppendLine($"[타이핑] 「만년공복」 → {(box != null ? box.TryExecuteCode(0, "만년공복") : null) ?? "(코드 아님)"}");
        var debuff = unit.Data.skills.FirstOrDefault(s => s.skillName == "외동");
        sb.AppendLine($"[외동 디버프] 스킬 목록에 {(debuff != null ? "있음 · 효과 " + debuff.levels[0].effects.Count : "없음")}(기대 있음 · 효과 0)");
        return sb.ToString();
    }
}
