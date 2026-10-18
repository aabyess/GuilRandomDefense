using UnityEngine;

/// <summary>문 전환 촬영용(10-19): gameshot의 click:SoloButton 경로는 클릭 간격(0.5초)이 문 닫힘보다 길어 문이 안 찍힌다.
///   플레이 중 gameshot에서(씬 전환 없이 문만 닫았다 연다):
///     call:DoorPreview.CloseDoors | CloseGate | CloseClassic   — 방식을 골라 실행(MenuTheme.DoorStyle과 무관)
///     call:DoorPreview.Close                                    — 지금 MenuTheme 방식
///   느리게(연속 프레임용): call:DoorPreview.Slow(0.2배) 를 먼저 부르면 전환이 5배 느려져 snap 간격 0.25초 = 문 시간 0.05초. call:DoorPreview.Normal 로 되돌린다.
///   전체 시간선(문 시간): 닫힘 0.55(Doors)/0.65(Gate)/0.45(Classic) → 대기 0.55 → 열림 0.8/1.0/0.95.</summary>
static class DoorPreview
{
    static string Run(int style)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        MenuTheme.DoorStyleOverride = style;
        try { DoorTransition.CloseThen(null, expectScene: false); }
        finally { MenuTheme.DoorStyleOverride = -1; }
        return $"문 시작: {(style < 0 ? MenuTheme.DoorStyle.ToString() : ((MenuTheme.DoorKind)style).ToString())} (timeScale {DoorTransition.PreviewTimeScale})";
    }

    static string Close() => Run(-1);
    static string CloseDoors() => Run((int)MenuTheme.DoorKind.Doors);
    static string CloseGate() => Run((int)MenuTheme.DoorKind.Portcullis);
    static string CloseClassic() => Run((int)MenuTheme.DoorKind.Classic);
    static string Slow() { DoorTransition.PreviewTimeScale = 0.2f; return "문 5배 느리게"; }
    static string Normal() { DoorTransition.PreviewTimeScale = 1f; return "문 보통 속도"; }
}
