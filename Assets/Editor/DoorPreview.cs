using UnityEngine;

/// <summary>문 전환 촬영용(10-19): gameshot의 click:SoloButton 경로는 클릭 간격(0.5초)이 문 닫힘(0.45초)보다 길어 문이 안 찍힌다.
///   플레이 중 gameshot에서: call:DoorPreview.Close wait:0.2 snap:door_mid.png wait:0.25 snap:door_closed.png
///   (씬 전환 없이 문만 닫았다 연다 — 로딩·게임 씬은 안 부른다.)</summary>
static class DoorPreview
{
    static string Close()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        DoorTransition.CloseThen(null, expectScene: false);
        return "문 닫기 시작(0.45초 닫힘 → 0.35초 대기 → 열림)";
    }
}
