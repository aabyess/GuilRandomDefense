using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 타입이 바뀐 유닛(초월 강재규_AP 공성→일반)이 보스·일반몹에 새 배율로 들어가는지(구현담당1, 10-16)
// gameshot: call:AttackTypeFollowProbe.Run
static class AttackTypeFollowProbe
{
    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sb = new StringBuilder();
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_강재규_AP.asset");
        sb.AppendLine($"초월_강재규_AP attackType = {d.attackType} (원작 연구 R01M 일반)");
        var hpF = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        foreach (string path in new[] { "Assets/Data/Enemies/Enemy_SideBoss66_빅맘.asset", "Assets/Data/Enemies/Enemy_R01_박진웅.asset" })
        {
            EnemyData ed = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            foreach (AttackType at in new[] { AttackType.Siege, d.attackType })
            {
                var go = Object.Instantiate(ed.prefab, c + new Vector3(0f, 0f, 15f), Quaternion.identity);
                if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
                var e = go.GetComponent<EnemyDummy>(); e.Initialize(ed, 1f); e.SetLane(0);
                hpF.SetValue(e, 1e9f);
                float h0 = e.Hp;
                e.TakeDamage(10000f, DamageType.AD, at, -1, armorIgnoreRatio: 0f, isAbilityDamage: false, armorScale: 1f);
                sb.AppendLine($"{ed.name} 공격 {at}: Δ{h0 - e.Hp:F0}");
                Object.Destroy(go);
            }
        }
        return sb.ToString();
    }
}
