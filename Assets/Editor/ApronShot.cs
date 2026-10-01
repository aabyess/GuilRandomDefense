using UnityEngine;

// 앞치마(상점 줄) 쪽 사진용 카메라(2026-10-01) — 레인 0 앞치마 왼쪽 모서리가 보이게 남서쪽 위에서 비스듬히.
static class ApronShot
{
    static string Frame()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        LaneMarker lane = LaneMarker.Get(0);
        Camera cam = Camera.main;
        RtsCameraController rts = Object.FindFirstObjectByType<RtsCameraController>();
        if (lane == null || cam == null) return "❌ 레인·카메라 없음";
        if (rts != null) rts.enabled = false;
        Vector3 focus = lane.LaneCenter + new Vector3(-260f, 0f, -330f);
        cam.transform.position = focus + new Vector3(60f, 330f, -260f);
        cam.transform.LookAt(focus);
        return "앞치마 왼쪽 모서리 구도";
    }
}
