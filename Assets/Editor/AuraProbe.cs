using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>오라 이펙트(안 B) 실화면 확인용(구현담당2, 10-20) — gameshot call:
///   call:AuraProbe.Setup(오라 유닛 1기 + 받는 아군 셋을 우리에 세움) · call:AuraProbe.Cam(높이) 대신 CamClose/CamBase(가까이 130 / 기본 374 근처) · call:AuraProbe.Report(AuraVfx 상태·살아 있는 입자 수)</summary>
public static class AuraProbe
{
    static string Spawn(string unit)
    {
        MethodInfo m = typeof(ClaudeCommands).GetMethod("SpawnForShot", BindingFlags.Static | BindingFlags.NonPublic);
        return (m?.Invoke(null, new object[] { unit + "@pen", 0, 1 }) as string ?? "❌ SpawnForShot 없음").Split('\n')[0];
    }

    public static string Setup()
    {
        var sb = new StringBuilder();
        foreach (string n in new[] { "초월_노태현_AP", "흔함_양재모", "흔함_강재규", "흔함_문필환" }) sb.AppendLine(Spawn(n));
        // 받는 아군 셋을 오라 유닛 바로 곁(범위 144 안)에 모은다 — 우리(pen)는 슬롯이 넓어 범위 밖일 수 있다.
        GameObject src = GameObject.Find("Unit_초월_노태현_AP(Clone)");
        string[] allies = { "양재모", "강재규", "문필환" };
        Vector3[] offsets = { new Vector3(-55f, 0f, -10f), new Vector3(55f, 0f, -10f), new Vector3(0f, 0f, -60f) };
        for (int i = 0; i < allies.Length && src != null; i++)
        {
            GameObject a = GameObject.Find("Unit_흔함_" + allies[i] + "(Clone)");
            if (a == null) continue;
            Vector3 to = src.transform.position + offsets[i];
            if (a.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.enabled) agent.Warp(to); else a.transform.position = to;
        }
        return sb.ToString();
    }

    static string Cam(float height)
    {
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        GameObject g = GameObject.Find("Unit_초월_노태현_AP(Clone)");
        if (cam == null || g == null) return "❌ 카메라/노태현 없음";
        FieldInfo target = typeof(RtsCameraController).GetField("targetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        target?.SetValue(cam, height);
        Vector3 p = cam.transform.position; p.y = height; cam.transform.position = p;
        cam.MoveTo(g.transform.position);
        return $"카메라 높이 {height} → 노태현 {g.transform.position}";
    }
    public static string CamClose() => Cam(130f);
    public static string CamBase() => Cam(374f);

    public static string Report()
    {
        GameObject g = GameObject.Find("Unit_초월_노태현_AP(Clone)");
        bool src = AuraVfx.SourceState(g != null ? g.transform : null, out UnitGrade gr, out float range);
        var sb = new StringBuilder($"소스 {src} 등급 {gr} 범위 {range:F0}");
        foreach (string n in new[] { "양재모", "강재규", "문필환" })
        {
            GameObject a = GameObject.Find("Unit_흔함_" + n + "(Clone)");
            sb.Append($" · {n} 받는 상태 {(a != null ? AuraVfx.ReceiverState(a.transform) : -1)}");
        }
        int particles = 0;
        foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) if (ps.name == "ring_magic" || ps.name == "dot_soft" || ps.name == "ring_shock") particles += ps.particleCount;
        return sb + $" · 오라 계열 입자 {particles}";
    }
}
