using System.Reflection;
using UnityEngine;
using UnityEditor;

// 피해 유형별 실제 피해 계측(사장님 10-15 정의 점검). 배치모드: -executeMethod DamageTypeProbe.Run
public static class DamageTypeProbe
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        var go = new GameObject("probe");
        var d = go.AddComponent<EnemyDummy>();
        var data = ScriptableObject.CreateInstance<EnemyData>();
        typeof(EnemyDummy).GetField("data", F).SetValue(d, data);
        var mit = typeof(EnemyDummy).GetMethod("MitigatedDamage", F);
        var armorF = typeof(EnemyData).GetField("armor", F);
        var hpF = typeof(EnemyDummy).GetField("hp", F);
        sb.AppendLine("피해 1000, 마저항 기본 1.0 / 마깎 0.2");
        foreach (float shred in new[] { 0f, 0.2f })
        {
            typeof(EnemyDummy).GetField("magicArmorShred", F).SetValue(d, shred);
            foreach (float armor in new[] { 0f, 100f, -20f })
            {
                armorF.SetValue(data, armor);
                float M(DamageType t, bool ability, float ign, float scale, bool ex) =>
                    (float)mit.Invoke(d, new object[] { 1000f, t, AttackType.Unassigned, ign, ability, scale, ex });
                hpF.SetValue(d, 1e9f);
                typeof(EnemyDummy).GetField("isDead", F).SetValue(d, false);
                d.TakeDamage(1000f, DamageType.AD, AttackType.Unassigned, -1, fixedDamage: true);
                float fixedDealt = 1e9f - d.Hp;
                sb.AppendLine($"마깎{shred} 방어{armor}: 물리평타(AD) {M(DamageType.AD, false, 0, 1, false):0} | AP평타 {M(DamageType.AP, false, 0, 1, false):0} | AD스킬 {M(DamageType.AD, true, 0, 1, false):0} | 방무60% {M(DamageType.AD, true, .6f, 1, false):0} | 방무100% {M(DamageType.AD, true, 1f, 1, false):0} | 배성령50%(armorScale) {M(DamageType.AD, false, 0, .5f, false):0} | 마법(AP스킬) {M(DamageType.AP, true, 0, 1, false):0} | 폭발형AD {M(DamageType.AD, true, 0, 1, true):0} | 폭발형AD+방무60% {M(DamageType.AD, true, .6f, 1, true):0} | 폭발형AP {M(DamageType.AP, true, 0, 1, true):0} | 고정 {fixedDealt:0}");
            }
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "grd/GRD_damage_probe/result.txt"), sb.ToString());
        Debug.Log("[DamageTypeProbe]\n" + sb);
    }
}
