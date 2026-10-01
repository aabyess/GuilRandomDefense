using System.Linq;
using UnityEditor;
using UnityEngine;

// 고유 동작 보여 주기(2026-10-01 사장님 「게임으로 열어서 보여줄래」) — 유닛 하나를 레인 가운데 세우고 앞에 표적 셋(멈춤·체력 큼),
// 카메라를 그 유닛 가까이 비스듬히 붙인다. gameshot의 snap:을 짧은 간격으로 이어 찍어 움직임 사진(GIF)을 만든다.
//   gameshot m.png 1 1280x720 click?:보통 wait:2 call:MotionShowProbe.Sangho wait:1.5 snap:m00 wait:0.12 snap:m01 …
// 비교용으로 공용 동작을 쓰는 유닛(Common)도 같은 자리에 세울 수 있다.
static class MotionShowProbe
{
    static string Sangho() => Show("영원_최상호");
    static string Common() => Show("영원_조세민");

    // 고유 동작 10종(2026-10-01) — 한 판에서 차례로 부른다(앞서 세운 유닛·표적은 Show가 치운다).
    static string Munpil() => Show("영원_문필환");
    static string Kimgun() => Show("초월_김건_AP");
    static string Juhyuk() => Show("초월_강주혁_AP");
    static string Janghyuk() => Show("초월_임장혁_AD");
    static string Seungwoo() => Show("전설적인_이승우");
    static string Juho() => Show("희귀함_구주호");
    static string Jeongbeom() => Show("특별함_이정범");
    static string Minsu() => Show("특별함_박민수");
    static string Yewon() => Show("특별함_박예원");
    static string Hyeongyu() => Show("희귀함_조현규");

    static readonly System.Collections.Generic.List<GameObject> shown = new System.Collections.Generic.List<GameObject>();

    static string Show(string unitName)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        foreach (GameObject old in shown) if (old != null) Object.Destroy(old);
        shown.Clear();
        foreach (UnitIdentity prev in Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None)) Object.Destroy(prev.gameObject);
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        UnitData data = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(u => u != null && u.name.Normalize(System.Text.NormalizationForm.FormC) == unitName);
        EnemyData dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        if (spawner == null || lane == null || data == null || dummyData == null) return $"❌ 준비물 없음({unitName})";

        Vector3 home = lane.LaneCenter;
        for (int i = 0; i < 3; i++)
        {
            GameObject go = Object.Instantiate(dummyData.prefab, home + new Vector3((i - 1) * 22f, 0f, 55f), Quaternion.Euler(0f, 180f, 0f));
            shown.Add(go);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            if (go.TryGetComponent(out EnemyDummy d)) { d.Initialize(dummyData, 1e9f); d.SetLane(-1); }
        }
        spawner.Spawn(data, home, 0);

        // 카메라: RTS 조종을 끄고 유닛 앞쪽 비스듬한 위에서 가까이 — 키 30 유닛이 화면 세로 절반쯤 차게.
        Camera cam = Camera.main;
        RtsCameraController rts = Object.FindFirstObjectByType<RtsCameraController>();
        if (rts != null) rts.enabled = false;
        if (cam != null)
        {
            Vector3 focus = home + new Vector3(0f, 18f, 20f);
            cam.transform.position = focus + new Vector3(-55f, 45f, -75f);
            cam.transform.LookAt(focus);
        }
        return $"{unitName} 세움 · 표적 3 · 카메라 가까이";
    }
}
