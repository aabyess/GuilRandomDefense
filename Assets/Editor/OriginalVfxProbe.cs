using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>원작 이펙트 시범 촬영(10-08): 0번 레인 가운데에 시범 프리팹을 한 줄로 세우고 다시 재생한다. gameshot call:OriginalVfxProbe.Show 뒤 wait:·snap:으로 찍는다.</summary>
public static class OriginalVfxProbe
{
    static readonly string[] Names = { "az_firering1a", "Mdx_Effect_Railgun", "EmpyreanSigil4" };
    static readonly GameObject[] spawned = new GameObject[3];

    static float MaxXZ(GameObject g)
    {
        Vector3 saved = g.transform.localScale; g.transform.localScale = Vector3.one;
        Bounds? b = null;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) { if (b == null) b = r.bounds; else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; } }
        g.transform.localScale = saved;
        return b.HasValue ? Mathf.Max(b.Value.size.x, b.Value.size.z, b.Value.size.y) : 1f;
    }

    public static string Diag()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < spawned.Length; i++)
        {
            if (spawned[i] == null) continue;
            OriginalVfxPlayer pl = spawned[i].GetComponent<OriginalVfxPlayer>();
            sb.AppendLine($"{Names[i]} active={spawned[i].activeSelf} playing={pl.IsPlaying} pos={spawned[i].transform.position}");
            foreach (OriginalVfxPlayer.Layer l in pl.layers)
            {
                if (l.renderer == null) { sb.AppendLine("  렌더러 null"); continue; }
                MaterialPropertyBlock mb = new MaterialPropertyBlock(); l.renderer.GetPropertyBlock(mb);
                Material m = l.renderer.sharedMaterial;
                Mesh mesh = l.renderer is SkinnedMeshRenderer sm ? sm.sharedMesh : l.renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null) { Vector2[] uv = mesh.uv; Vector2 lo = new Vector2(1e9f, 1e9f), hi = new Vector2(-1e9f, -1e9f); foreach (Vector2 u in uv) { lo = Vector2.Min(lo, u); hi = Vector2.Max(hi, u); } sb.AppendLine($"  [{mesh.name}] 정점 {mesh.vertexCount} 삼각형 {mesh.triangles.Length / 3} uv {lo}~{hi} bounds {mesh.bounds.size}"); }
                sb.AppendLine($"  {l.renderer.name} en={l.renderer.enabled} go={l.renderer.gameObject.activeInHierarchy} b={l.renderer.bounds.center}/{l.renderer.bounds.size} shader={(m != null ? m.shader.name : "-")} tex={(m != null && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "none")} color={mb.GetColor("_BaseColor")} st={mb.GetVector("_BaseMap_ST")} uvKeys={l.uvTimes.Length} aKeys={l.alphaTimes.Length}");
            }
        }
        return sb.ToString();
    }

    public static string Solid()
    {
        if (spawned[1] == null) return "없음";
        OriginalVfxPlayer pl = spawned[1].GetComponent<OriginalVfxPlayer>();
        Material solid = new Material(Shader.Find("Universal Render Pipeline/Unlit")); solid.color = Color.magenta;
        foreach (OriginalVfxPlayer.Layer l in pl.layers) { l.renderer.sharedMaterial = solid; l.renderer.SetPropertyBlock(null); }
        pl.enabled = false;   // 알파 곡선이 색을 덮지 않게
        return "마젠타 불투명로 교체, 재생기 끔";
    }

    public static string Raw()
    {
        if (spawned[1] == null) return "없음";
        OriginalVfxPlayer pl = spawned[1].GetComponent<OriginalVfxPlayer>();
        pl.enabled = false;
        foreach (OriginalVfxPlayer.Layer l in pl.layers) l.renderer.SetPropertyBlock(null);
        return "MPB 제거 — 재질 그대로(알파 1·ST 기본)";
    }

    public static string FrameA() => Frame(-0.2117f);
    public static string FrameB() => Frame(-0.5965f);
    static string Frame(float v)
    {
        if (spawned[1] == null) return "없음";
        OriginalVfxPlayer pl = spawned[1].GetComponent<OriginalVfxPlayer>();
        pl.enabled = false;
        foreach (OriginalVfxPlayer.Layer l in pl.layers)
        {
            MaterialPropertyBlock mb = new MaterialPropertyBlock();
            mb.SetColor("_BaseColor", Color.white);
            mb.SetVector("_BaseMap_ST", new Vector4(1f, 1f, 0f, v));
            l.renderer.SetPropertyBlock(mb);
        }
        return "고정 프레임 v=" + v;
    }

    public static string FrameC()
    {
        if (spawned[1] == null) return "없음";
        OriginalVfxPlayer pl = spawned[1].GetComponent<OriginalVfxPlayer>();
        pl.enabled = false;
        StringBuilder sb = new StringBuilder();
        foreach (OriginalVfxPlayer.Layer l in pl.layers)
        {
            l.renderer.SetPropertyBlock(null);
            Material m = l.renderer.material;   // 인스턴스
            m.SetTextureOffset("_BaseMap", new Vector2(0f, -0.2117f));
            m.SetColor("_BaseColor", Color.white);
            Mesh mesh = l.renderer is SkinnedMeshRenderer sm ? sm.sharedMesh : null;
            sb.AppendLine($"mat {m.name} q={m.renderQueue} cull={m.GetFloat("_Cull")} surf={m.GetFloat("_Surface")} blend={m.GetFloat("_Blend")} src={m.GetFloat("_SrcBlend")} dst={m.GetFloat("_DstBlend")} keywords={string.Join(",", m.shaderKeywords)} colors={(mesh != null && mesh.colors32.Length > 0 ? mesh.colors32[0].ToString() : "없음")} tex={m.GetTexture("_BaseMap")?.width}x{m.GetTexture("_BaseMap")?.height} wrap={m.GetTexture("_BaseMap")?.wrapMode}");
        }
        return sb.ToString();
    }

    // 편집 모드 진단: 모델 하나를 세워 재생 시각별 경계·뼈 스케일을 낸다. 부르는 법: call OriginalVfxProbe.EditDiag (모델 이름은 Docs/…가 아니라 아래 고정)
    public static string EditDiag()
    {
        StringBuilder sb = new StringBuilder();
        foreach (string name in new[] { "E_DTRing100", "dtbluenoringblend", "BY_Wood_GongChengSiPai_33" })
        {
            GameObject prefab = Resources.Load<GameObject>("Effects/Original/" + name);
            if (prefab == null) { sb.AppendLine(name + " 없음"); continue; }
            GameObject go = Object.Instantiate(prefab); go.hideFlags = HideFlags.HideAndDontSave;
            OriginalVfxPlayer pl = go.GetComponent<OriginalVfxPlayer>();
            sb.AppendLine($"{name}: 클립 {(pl.clip != null ? pl.clip.name + " " + pl.clip.length.ToString("F2") + "s legacy=" + pl.clip.legacy : "없음")} duration {pl.duration} animator={(pl.animator != null)} controller={(pl.animator != null && pl.animator.runtimeAnimatorController != null)} avatar={(pl.animator != null && pl.animator.avatar != null)}");
            foreach (float f in new[] { 0f, 0.3f, 0.6f })
            {
                pl.SampleAt(pl.duration * f);
                Bounds? b = null; string bone = "";
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) { if (b == null) b = r.bounds; else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; } }
                SkinnedMeshRenderer smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null && smr.rootBone != null) bone = $" rootBone={smr.rootBone.name} lossy={smr.rootBone.lossyScale} bones={smr.bones.Length} localBounds={smr.localBounds.size} sharedMesh={smr.sharedMesh?.name}";
                sb.AppendLine($"   t={f:0.0}: 경계 {(b.HasValue ? b.Value.size.ToString("F2") : "-")}{bone}");
            }
            Object.DestroyImmediate(go);
        }
        return sb.ToString();
    }

    public static string Show()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        LaneMarker lane = LaneMarker.Get(0);
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (lane == null || cam == null) return "❌ 레인·카메라 없음";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < Names.Length; i++)
        {
            if (spawned[i] == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Effects/Original/" + Names[i]);
                if (prefab == null) { sb.Append($"❌ {Names[i]} 프리팹 없음 "); continue; }
                spawned[i] = Object.Instantiate(prefab, lane.LaneCenter + new Vector3((i - 1) * 60f, 1f, 0f), Quaternion.identity);
            }
            OriginalVfxPlayer player = spawned[i].GetComponent<OriginalVfxPlayer>();
            spawned[i].transform.localScale = Vector3.one * (40f / Mathf.Max(1f, MaxXZ(spawned[i])));   // 프리팹 원래 지름 → 40 세계 단위
            player.Restart();
            Bounds? b = null;
            foreach (Renderer r in spawned[i].GetComponentsInChildren<Renderer>()) { if (b == null) b = r.bounds; else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; } }
            sb.Append($"{Names[i]} 경계 {(b.HasValue ? b.Value.size.ToString("F2") : "없음")} 층 {player.layers.Length}\n");
        }
        FieldInfo target = typeof(RtsCameraController).GetField("targetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        if (target != null) target.SetValue(cam, 160f);
        Vector3 p = cam.transform.position; p.y = 160f; cam.transform.position = p;
        cam.MoveTo(lane.LaneCenter);
        Time.timeScale = 0.05f;   // 촬영용 — 짧은 이펙트가 찍히기 전에 끝나지 않게 느리게
        return sb.ToString();
    }
}

/// <summary>항법 10라운드 자동 선택 확인(10-08): 라운드 10을 시작시켜 미선택자가 연합세력이 되는지.</summary>
public static class NavAutoProbe
{
    public static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        PlayerContext me = PlayerContext.Local;
        if (rm == null || me == null || me.NavigationState == null) return "❌ RoundManager·내 NavigationState 없음";
        string before = me.NavigationState.HasChosen ? me.NavigationState.Choice.ToString() : "미선택";
        MethodInfo m = typeof(RoundManager).GetMethod("ForceDefaultNavigation", BindingFlags.Instance | BindingFlags.NonPublic);
        if (m == null) return "❌ ForceDefaultNavigation 없음";
        m.Invoke(rm, null);
        return $"항법 {before} → {me.NavigationState.Choice}";
    }
}
