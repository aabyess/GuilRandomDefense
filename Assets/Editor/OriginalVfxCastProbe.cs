using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>원작 이펙트 직접 시전 근접 촬영(PM 10-08): 유닛 하나와 적 셋을 세우고 카메라를 바짝 당긴 뒤 그 유닛의 스킬을 CastSkillLevel로 직접 부른다(확률·쿨 없이).
///   ClaudeBridge/g2_cast.txt 한 줄 = 유닛에셋이름|스킬이름에 든 글자  (예: 불멸_고도현|약처방)
///   gameshot x.png 1 1920x1080 click?:보통 wait:6 call:OriginalVfxCastProbe.Setup wait:7 call:OriginalVfxCastProbe.Cast wait:0.3 snap:a.png wait:0.4 snap:b.png</summary>
static class OriginalVfxCastProbe
{
    static UnitAttacker caster; static UnitData data; static SkillData skill; static EnemyDummy e1;
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        string[] spec = System.IO.File.ReadAllText("ClaudeBridge/g2_cast.txt").Trim().Split('|');
        data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{spec[0]}.asset");
        if (data == null) return "❌ 유닛 없음 " + spec[0];
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0); Vector3 c = lane.LaneCenter;
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        EnemyData normal = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        var so = new SerializedObject(data);
        var list = new System.Collections.Generic.List<SkillData>();
        if (so.FindProperty("skill")?.objectReferenceValue is SkillData s1) list.Add(s1);
        var many = so.FindProperty("skills");
        if (many != null && many.isArray) for (int i = 0; i < many.arraySize; i++) if (many.GetArrayElementAtIndex(i).objectReferenceValue is SkillData s2) list.Add(s2);
        skill = list.FirstOrDefault(s => s != null && (s.name.Contains(spec[1]) || (s.skillName != null && s.skillName.Contains(spec[1]))));
        if (skill == null) return $"❌ 스킬 못 찾음({spec[1]}) — 후보: {string.Join(", ", list.Select(s => s.name))}";
        caster = spawner.Spawn(data, c, 0).GetComponent<UnitAttacker>();
        for (int i = 0; i < 3; i++)
        {
            GameObject go = Object.Instantiate(normal.prefab, c + new Vector3((i - 1) * 25f, 0f, 55f), Quaternion.Euler(0f, 180f, 0f));
            if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var e = go.GetComponent<EnemyDummy>(); e.Initialize(normal, 1e6f); e.SetLane(-1); if (i == 1) e1 = e;
        }
        FieldInfo target = typeof(RtsCameraController).GetField("targetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        if (target != null) target.SetValue(cam, 110f);
        Vector3 p = cam.transform.position; p.y = 110f; cam.transform.position = p;
        cam.MoveTo(c + new Vector3(0f, 0f, 25f));
        return $"{data.name} · 스킬 {skill.name} · 레벨 {skill.levels.Count}";
    }

    static string Cast()
    {
        if (caster == null || skill == null) return "❌ Setup 먼저";
        SkillLevel level = skill.levels[0];
        MethodInfo cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", NP);
        bool before = SkillVfx.BeginCast(data, skill);
        cast.Invoke(caster, new object[] { level, level.WorldRange, e1, caster.AttackDamage });
        SkillVfx.EndCast(before);
        Time.timeScale = 0.2f;   // 한 번 터지는 이펙트가 찍히기 전에 끝나지 않게
        return $"시전 {skill.name} (범위 {level.WorldRange:F0})";
    }
}
