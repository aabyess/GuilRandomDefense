using UnityEngine;

/// <summary>문 전환 촬영용(10-19): gameshot의 click:SoloButton 경로는 클릭 간격(0.5초)이 문 닫힘보다 길어 문이 안 찍힌다.
///   플레이 중 gameshot에서(씬 전환 없이 문만 닫았다 연다). 첫 화면 인트로(WANTED 포스터, 정렬 32000)가 문 위를 덮으므로 먼저 걷는다:
///     call:DoorPreview.SkipIntro                                   — 인트로 덮개 제거
///     call:DoorPreview.CloseDoors | CloseGate | CloseClassic       — 방식을 골라 실행(MenuTheme.DoorStyle과 무관), 실시간
///     call:DoorPreview.Close                                       — 지금 MenuTheme 방식
///   🔴 정확한 연속 프레임(권장): 시간 정지 모드 — snap이 몇 초씩 걸려도 간격이 같다.
///     call:DoorPreview.SkipIntro call:DoorPreview.FrozenGate (또는 FrozenDoors/FrozenClassic: 문을 닫기 시작하되 시간은 멈춤)
///     그다음 N번 반복: call:DoorPreview.Step wait:0.15 snap:f01.png   (Step = 문 시간 0.05초 진행 · Step10 = 0.1초 · 0.15초 대기는 화면이 그려질 틈)
///     끝나면 call:DoorPreview.Normal (시간 정지 해제 — 문이 남아 있으면 이어서 흐른다)
///   느리게(실시간 배율): call:DoorPreview.Slow(0.2배) — snap이 느려 간격이 들쭉날쭉하니 Frozen을 쓸 것.
///   전체 문 시간선: 닫힘 0.55(Doors)/0.65(Gate)/0.45(Classic) → 대기 0.55 → 열림 0.8/1.0/0.95. (Doors 0.05초 40장 = 2초 = 전부)</summary>
static class DoorPreview
{
    static string Run(int style, bool frozen)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        DoorTransition.PreviewFrozen = frozen; DoorTransition.PreviewStep = 0f;
        MenuTheme.DoorStyleOverride = style;
        try { DoorTransition.CloseThen(null, expectScene: false); }
        finally { MenuTheme.DoorStyleOverride = -1; }
        return $"문 시작: {(style < 0 ? MenuTheme.DoorStyle.ToString() : ((MenuTheme.DoorKind)style).ToString())} (frozen {frozen}, timeScale {DoorTransition.PreviewTimeScale})";
    }

    static string SkipIntro()
    {
        GameObject g = GameObject.Find("[CreditsSplash]");
        if (g == null) return "인트로 없음";
        Object.Destroy(g);
        return "인트로 걷음";
    }

    static string Close() => Run(-1, false);
    static string CloseDoors() => Run((int)MenuTheme.DoorKind.Doors, false);
    static string CloseGate() => Run((int)MenuTheme.DoorKind.Portcullis, false);
    static string CloseClassic() => Run((int)MenuTheme.DoorKind.Classic, false);
    static string FrozenDoors() => Run((int)MenuTheme.DoorKind.Doors, true);
    static string FrozenGate() => Run((int)MenuTheme.DoorKind.Portcullis, true);
    static string FrozenClassic() => Run((int)MenuTheme.DoorKind.Classic, true);
    static string Step() { DoorTransition.PreviewStep += 0.05f; return "+0.05"; }
    static string Step10() { DoorTransition.PreviewStep += 0.1f; return "+0.10"; }
    static string Slow() { DoorTransition.PreviewTimeScale = 0.2f; return "문 5배 느리게"; }
    static string Normal() { DoorTransition.PreviewTimeScale = 1f; DoorTransition.PreviewFrozen = false; DoorTransition.PreviewStep = 0f; return "문 보통 속도"; }
}
