using UnityEngine;

// S2 아이디어(10-18) 시험 탐침 — gameshot call:S2UiProbe.<이름>
public static class S2UiProbe
{
    public static string BerryGrant()
    {
        int id = LocalPlayer.LocalPlayerId;
        int before = Berry.Get(id);
        bool ok = Berry.Grant(id, 1, "시험");
        return $"베리 {before} → {Berry.Get(id)} (지급 {ok}, 이번 판 {Berry.Session(id)})";
    }
    // 하단 정보창 사진용: 내 흔함 유닛 옆에 등급이 다른 유닛 둘을 세운다
    public static string SpawnInfoUnits()
    {
        UnitSpawner sp = Object.FindFirstObjectByType<UnitSpawner>();
        if (sp == null) return "스포너 없음";
        Vector3 at = Vector3.zero;
        foreach (UnitIdentity u in Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None)) { at = u.transform.position; break; }
        string made = "";
        string[] paths = { "Assets/Data/Units/Roster/초월_양재모_AD.asset", "Assets/Data/Units/Roster/전설_" };
        UnitData a = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>(paths[0]);
        if (a != null) { sp.Spawn(a, at + new Vector3(60f, 0f, 0f), LocalPlayer.LocalPlayerId); made += a.name + " "; }
        foreach (string g in UnityEditor.AssetDatabase.FindAssets("t:UnitData 전설적인_", new[] { "Assets/Data/Units/Roster" }))
        {
            UnitData b = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
            if (b == null) continue;
            sp.Spawn(b, at + new Vector3(-60f, 0f, 0f), LocalPlayer.LocalPlayerId); made += b.name; break;
        }
        return "세움: " + made;
    }
    // 미션판(기록지침) 시험: 퀘스트 배정 → 공유 미션 둘·개인 하나 완료 → 하급 도박 실패 3회(진행도) → 창 열기
    public static string BoardDemo()
    {
        S2Missions.EnsureAssigned();
        S2Missions.Complete(S2Missions.Mission.GoldenBell, 0);
        S2Missions.Complete(S2Missions.Mission.JusticeGate, 0);
        S2Missions.Complete(S2Missions.Mission.FastUnique, LocalPlayer.LocalPlayerId);
        int me = LocalPlayer.LocalPlayerId;
        var v = S2Missions.View(me);
        string q = "";
        for (int k = 0; k < 3; k++) q += (v.questIds[k] >= 0 ? S2Missions.Quests[v.questIds[k]].name : "-") + " ";
        MissionBoardPanel.Open();
        return $"공유 {S2Missions.SharedCount}/8 · 퀘스트 {q}· 베리 {Berry.Get(me)}";
    }
    public static string BoardOpen() { MissionBoardPanel.Open(); return "창 열기"; }
    public static string BoardReroll0() { S2Missions.RequestReroll(0); var v = S2Missions.View(LocalPlayer.LocalPlayerId); return "리롤 0 → " + (v.questIds[0] >= 0 ? S2Missions.Quests[v.questIds[0]].name : "-") + $" 사용={v.rerollUsed}"; }
    // 진행도 시험: 내 퀘스트 셋 중 진행을 올릴 수 있는 것에 맞춰 통계를 올린다
    public static string BoardProgress()
    {
        int me = LocalPlayer.LocalPlayerId;
        for (int n = 0; n < 2; n++) { RunStats.OnCommonSold(me); RunStats.OnWoodGamble(me, true); RunStats.OnChoiceWispUsed(me); RunStats.OnStoryMvp(me); }
        var v = S2Missions.View(me);
        string q = "";
        for (int k = 0; k < 3; k++) q += (v.questIds[k] >= 0 ? S2Missions.Quests[v.questIds[k]].name + "=" + v.questProgress[k] : "-") + " ";
        return "진행 " + q;
    }
    // 포네그리프: 공유 8개 완료 → 해금 → 전설 유닛 세워 해독 시작
    public static string PoneDemo()
    {
        foreach (S2Missions.Mission m in System.Enum.GetValues(typeof(S2Missions.Mission))) if (S2Missions.Missions[(int)m].shared) S2Missions.Complete(m);
        SpawnInfoUnits();
        UnitIdentity legend = null;
        foreach (UnitIdentity u in Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None)) if (u.Data != null && u.Data.name.StartsWith("전설적인_")) { legend = u; break; }
        PoneglyphSite.TryStart(LocalPlayer.LocalPlayerId, legend);
        MissionBoardPanel.Open();
        return $"해금 {S2Missions.HiddenUnlocked} · 해독 남은 {PoneglyphSite.SecondsLeft:F0}초 · 유닛 {(legend != null ? legend.name : "없음")}";
    }
    // 이모티콘: 네 자리에 하나씩 + 고르기 창
    public static string EmoteDemo()
    {
        EmotePanel.Show(0, 0); EmotePanel.Show(1, 1); EmotePanel.Show(2, 3); EmotePanel.Show(3, 5);
        return "이모티콘 네 자리";
    }
    public static string ScoreCmd() => "점수판: " + StatsChatCommands.TryHandle(LocalPlayer.LocalPlayerId, "-점수판");
    public static string StoryCmd() => "스토리: " + StatsChatCommands.TryHandle(LocalPlayer.LocalPlayerId, "-스토리");
    public static string BoardClose() { if (MissionBoardPanel.IsOpen) MissionBoardPanel.Toggle(); return "창 닫기"; }
}
