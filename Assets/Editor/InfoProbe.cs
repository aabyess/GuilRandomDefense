using UnityEditor;
using UnityEngine;

/// <summary>정보창 촬영(10-09): ClaudeBridge/g2_info.txt의 로스터 유닛을 세우고 고른다. gameshot x.png 2 1920x1080 click?:보통 wait:2 call:InfoProbe.Select wait:1</summary>
static class InfoProbe
{
    static string Dump()
    {
        string name = System.IO.File.ReadAllText("ClaudeBridge/g2_info.txt").Trim();
        var data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        GameObject g = (GameObject)Object.Instantiate(data.prefab);
        var sb = new System.Text.StringBuilder();
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            Material m = r.sharedMaterial;
            sb.AppendLine($"{r.name} en={r.enabled} active={r.gameObject.activeInHierarchy} bounds={r.bounds.size.ToString("F1")} mats={(m != null ? m.name + " tex=" + (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : (m.mainTexture != null ? m.mainTexture.name : "none")) + " shader=" + m.shader.name : "null")}");
        }
        var an = g.GetComponentInChildren<Animator>();
        sb.AppendLine($"animator={(an != null)} ctrl={(an != null && an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "없음")} avatar={(an != null && an.avatar != null ? an.avatar.name + " valid=" + an.avatar.isValid : "없음")} root={(an != null ? an.gameObject.name : "-")}");
        if (an != null && an.runtimeAnimatorController != null) foreach (AnimationClip ac in an.runtimeAnimatorController.animationClips) sb.AppendLine($"  clip {ac.name} len={ac.length:F2} loop={ac.isLooping} bindings={UnityEditor.AnimationUtility.GetCurveBindings(ac).Length}");
        sb.AppendLine("scale " + g.transform.GetChild(g.transform.childCount - 1).localScale);
        Object.DestroyImmediate(g);
        return sb.ToString();
    }

    static string Sample()
    {
        string name = System.IO.File.ReadAllText("ClaudeBridge/g2_info.txt").Trim();
        var data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        GameObject g = (GameObject)Object.Instantiate(data.prefab);
        var an = g.GetComponentInChildren<Animator>();
        var sb = new System.Text.StringBuilder();
        void Report(string tag)
        {
            foreach (SkinnedMeshRenderer smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = new Mesh(); smr.BakeMesh(m, true);
                sb.AppendLine($"{tag} {smr.name}: 베이크 경계 {m.bounds.size.ToString("F2")} 중심 {m.bounds.center.ToString("F2")} 정점 {m.vertexCount} 루트뼈 {(smr.rootBone != null ? smr.rootBone.name : "-")} 뼈 {smr.bones.Length}");
                Object.DestroyImmediate(m);
            }
        }
        Report("바인드");
        foreach (AnimationClip ac in an.runtimeAnimatorController.animationClips)
            if (ac.name == "Idle") { ac.SampleAnimation(an.gameObject, 0.5f); break; }
        Report("Idle0.5");
        // 뼈 스케일 점검(0이 있나)
        int zero = 0, total = 0; string first = "";
        foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
        { total++; if (t.localScale.sqrMagnitude < 1e-6f) { zero++; if (first == "") first = t.name; } }
        sb.AppendLine($"스케일 0 뼈 {zero}/{total} 첫 {first}");
        Object.DestroyImmediate(g);
        return sb.ToString();
    }

    static string Close()
    {
        string r = Select();
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        var th = typeof(RtsCameraController).GetField("targetHeight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (th != null) th.SetValue(cam, 110f);
        Vector3 p = cam.transform.position; p.y = 110f; cam.transform.position = p;
        cam.MoveTo(LaneMarker.Get(0).LaneCenter);
        return r;
    }

    static string Select()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        string name = System.IO.File.ReadAllText("ClaudeBridge/g2_info.txt").Trim();
        var data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        if (data == null || spawner == null || sel == null) return "❌ 유닛·스포너·선택 관리자 없음";
        GameObject go = spawner.Spawn(data, LaneMarker.Get(0).LaneCenter, 0);
        var s = go.GetComponent<Selectable>();
        sel.SelectOnly(s);
        return $"{name} · 원작 표시 「{data.OriginalMatchLabel}」";
    }
}
