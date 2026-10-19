using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 이펙트 개편(안 B) 확인용 촬영 도구 — gameshot의 call:로 부른다(구현담당2, 10-19).
///   call:VfxStyleProbe.Close     ShowcaseDefault 뒤 진열 첫 표적(스턴) 위로 카메라를 가까이(높이 90) — 반짝이·꼬리가 읽히는지 본다
///   call:VfxStyleProbe.CloseN    같은 방식으로 n번째 표적(Close2·Close3·Close4 = 이감·방깎·적중)
/// </summary>
public static class VfxStyleProbe
{
    static string CloseOn(int index, float height)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        object r = typeof(VfxProbe).GetMethod("ShowcaseDefault", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        FieldInfo field = typeof(ClaudeCommands).GetField("showcase", BindingFlags.Static | BindingFlags.NonPublic);
        if (!(field?.GetValue(null) is List<EnemyDummy> list) || list.Count <= index || list[index] == null) return "❌ 진열 표적 없음 " + r;
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam == null) return "❌ 카메라 없음";
        FieldInfo target = typeof(RtsCameraController).GetField("targetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        target?.SetValue(cam, height);
        Vector3 p = cam.transform.position; p.y = height; cam.transform.position = p;   // 높이를 먼저 — MoveTo가 현재 높이로 시선 오프셋을 계산한다
        cam.MoveTo(list[index].transform.position);
        return $"가까이 {index}번 표적 높이 {height}";
    }
    public static string Close() => CloseOn(0, 90f);
    public static string Close2() => CloseOn(1, 90f);
    public static string Close3() => CloseOn(2, 90f);
    public static string Close4() => CloseOn(3, 90f);
}
