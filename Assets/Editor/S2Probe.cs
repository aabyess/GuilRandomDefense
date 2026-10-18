using UnityEngine;

// S2 아이디어(10-18) 시험 탐침 — gameshot call:S2Probe.<이름>
public static class S2Probe
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
}
