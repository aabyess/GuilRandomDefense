using UnityEngine;

// 소개 영상·스크린샷 촬영 전용(2026-10-15 PM 지시). gameshot의 call:ShowcaseCalls.<이름>으로 부른다. 게임 코드는 안 바꾼다.
public static class ShowcaseCalls
{
    static string Cutin(string unit) => CutinOverlay.Play(unit) ? "컷인 걸림: " + unit : "❌ 컷인 산출물 없음: " + unit;
    public static string CutinsOff() { CutinOverlay.Enabled = false; return "컷인 끔"; }
    public static string CutinsOn() { CutinOverlay.Enabled = true; return "컷인 켬"; }
    public static string Cutin1() => Cutin("초월_노태현_AP");
    public static string Cutin2() => Cutin("초월_최상호_AD");
    public static string Cutin3() => Cutin("불멸_이이삭");

    // 카메라를 조합표 인형 무리 가운데로
    public static string CamTable()
    {
        var sp = Object.FindFirstObjectByType<RecipeDollSpawner>();
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (sp == null || cam == null) return "❌ 조합표/카메라 없음";
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (Transform t in sp.transform) { sum += t.position; n++; }
        Vector3 c = n > 0 ? sum / n : sp.transform.position;
        cam.MoveTo(c);
        return $"조합표 {n}기 중심 {c}";
    }

    public static string CamLane()
    {
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam == null) return "❌ 카메라 없음";
        cam.FocusOnLocalLane();
        return "레인으로";
    }

    // 전투 장면: 시야 1(가장 가깝게) + 내 상위 유닛·적 무리의 가운데로
    public static string CamFight()
    {
        var cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam == null) return "❌ 카메라 없음";
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var u in Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None))
            if (u != null && u.Data != null && u.GetComponent<Wisp>() == null && u.Data.grade != UnitGrade.Common) { sum += u.transform.position; n++; }
        int m = 0;
        foreach (var e in EnemyDummy.Active) if (e != null && e.LaneIndex == 0) { sum += e.transform.position; m++; }
        if (n + m == 0) return "❌ 대상 없음";
        Vector3 c = sum / (n + m);
        cam.SetViewPreset(0f);
        cam.MoveTo(c);
        return $"전투 중심 {c} (유닛 {n} · 적 {m})";
    }
}
