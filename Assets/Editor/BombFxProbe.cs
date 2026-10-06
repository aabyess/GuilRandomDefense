using UnityEngine;

// 폭탄 폭발 이펙트 사진(10-06 PM) — gameshot: call:TaewoongProbe.BombRun 뒤 call:BombFxProbe.Focus → wait·snap.
//   BombRun이 세운 엄태웅 쪽으로 카메라를 당긴다. 이펙트가 떴는지는 풀(SkillVfx 루트)의 켜진 파티클 수로도 센다.
static class BombFxProbe
{
    static string Focus()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        GameObject fx = GameObject.Find("Bomb_Explosion_Blender(Clone)");
        if (cam == null || fx == null) return $"❌ 카메라 {(cam != null)} · 이펙트 {(fx != null)}";
        cam.FlyTo(fx.transform.position, 260f);
        int alive = 0;
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) alive += ps.particleCount;
        var sb = new System.Text.StringBuilder($"카메라 → 폭탄 {fx.transform.position:F0} · 배율 {fx.transform.localScale.x:F0} · 켜진 파티클 {alive}\n");
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            sb.AppendLine($"  {ps.name}: 수 {ps.particleCount} · 재생 {ps.isPlaying} · 보임 {r.isVisible} · 경계 {r.bounds.center:F0}±{r.bounds.extents:F0} · 재질 {(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "없음")} · 활성 {ps.gameObject.activeInHierarchy}");
        }
        return sb.ToString();
    }

    // 편집 모드 미리보기: 열린 씬 (−1555, 9, 1580)에 배율 174로 놓고 t초 시뮬레이션 — 뒤이어 shot으로 찍는다. Clear로 지운다.
    static GameObject preview;
    static string Preview(float t)
    {
        Clear();
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Effects/Bomb/Bomb_Explosion_Blender.prefab");
        preview = Object.Instantiate(prefab, new Vector3(-1555f, 9f, 1580f), Quaternion.identity);
        preview.name = "BombPreview";
        preview.transform.localScale = Vector3.one * 174f;
        var root = preview.GetComponent<ParticleSystem>();
        root.Simulate(t, true, true, true);
        int n = 0;
        foreach (ParticleSystem ps in preview.GetComponentsInChildren<ParticleSystem>()) n += ps.particleCount;
        return $"미리보기 t={t} 파티클 {n}";
    }
    static string P02() => Preview(0.2f);
    static string P05() => Preview(0.5f);
    static string P10() => Preview(1.0f);
    static string Clear()
    {
        if (preview != null) Object.DestroyImmediate(preview);
        var old = GameObject.Find("BombPreview");
        if (old != null) Object.DestroyImmediate(old);
        return "지움";
    }

    // 실제 판: 폭탄을 터뜨리는 같은 프레임에 게임 시간을 1/20로 늦춘다(gameshot 단계 사이가 폭발보다 길다). Normal로 되돌린다.
    static string BombSlow()
    {
        var m = typeof(TaewoongProbe).GetMethod("BombRun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        string r = (string)m.Invoke(null, null);
        Time.timeScale = 0.05f;
        return r + "\n" + Focus();
    }
    static string Normal() { Time.timeScale = 1f; return "시간 1"; }
}
