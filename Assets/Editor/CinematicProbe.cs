using System.Reflection;
using UnityEngine;

/// <summary>연출 재생 촬영(10-09): 0번 레인 가운데에 시전자(작은 표지)와 대상(적 셋)을 두고 대본을 재생한다.
///   ClaudeBridge/g2_scene.txt = 대본 id.  gameshot x.png 1 1920x1080 click?:보통 wait:2 call:CinematicProbe.Play wait:0.5 snap:a.png ...</summary>
static class CinematicProbe
{
    static string Play()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        string id = System.IO.File.ReadAllText("ClaudeBridge/g2_scene.txt").Trim();
        var script = Resources.Load<CinematicScript>("Cinematics/" + id);
        if (script == null) return "❌ 대본 없음 " + id;
        LaneMarker lane = LaneMarker.Get(0);
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        Vector3 c = lane.LaneCenter;
        Vector3 caster = c + new Vector3(0f, 0f, -20f), target = c + new Vector3(0f, 0f, 40f);
        EnemyData normal = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        for (int i = 0; i < 3; i++)
        {
            GameObject go = Object.Instantiate(normal.prefab, target + new Vector3((i - 1) * 22f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
            if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var e = go.GetComponent<EnemyDummy>(); e.Initialize(normal, 1e6f); e.SetLane(-1);
        }
        FieldInfo th = typeof(RtsCameraController).GetField("targetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        float h = float.TryParse(System.IO.File.Exists("ClaudeBridge/g2_scene_h.txt") ? System.IO.File.ReadAllText("ClaudeBridge/g2_scene_h.txt").Trim() : "", out float hh) ? hh : 150f;
        if (th != null) th.SetValue(cam, h);
        Vector3 p = cam.transform.position; p.y = h; cam.transform.position = p;
        cam.MoveTo((caster + target) * 0.5f);
        SkillCinematic.Play(script, caster, target);
        Time.timeScale = float.TryParse(System.IO.File.Exists("ClaudeBridge/g2_scene_ts.txt") ? System.IO.File.ReadAllText("ClaudeBridge/g2_scene_ts.txt").Trim() : "", out float ts) ? ts : 0.5f;
        return $"{id} 재생 · 이벤트 {script.events.Count} · {script.duration:F1}s";
    }
}
