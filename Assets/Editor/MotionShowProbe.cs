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
    // 카메라를 지정 방향 가까이로 — 부가 이펙트 사진용(유닛은 home에 서 있다)
    static string CamAt(float dx, float dy, float dz)
    {
        UnitIdentity u = Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None).FirstOrDefault();
        if (u == null || Camera.main == null) return "❌ 유닛/카메라 없음";
        Vector3 focus = u.transform.position + Vector3.up * 16f;
        Camera.main.transform.position = focus + new Vector3(dx, dy, dz);
        Camera.main.transform.LookAt(focus);
        return $"카메라 {dx},{dy},{dz}";
    }
    static string CamBack() => CamAt(0f, 14f, -50f);
    static string CamFront() => CamAt(0f, 14f, 50f);
    static string CamSide() => CamAt(55f, 10f, 0f);
    // 공격 모션 강제 — 「공격」 때 보이는 부품 확인용
    static string AttackAll() { foreach (CharacterAnimator c in Object.FindObjectsByType<CharacterAnimator>(FindObjectsSortMode.None)) if (c.GetComponent<UnitIdentity>() != null) c.PlayAttack(); return "공격 모션"; }
    // 로스터 이름을 ClaudeBridge/probe_roster.txt 첫 줄에서 읽어 세운다 — 상시 오라 사진용(명령에 인자를 못 넘겨서)
    static string ShowFile() => Show(System.IO.File.ReadAllLines(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "ClaudeBridge/probe_roster.txt"))[0].Trim().Normalize(System.Text.NormalizationForm.FormC));
    static string KimAp() => Show("초월_김민준_AP");
    static string ShinAp() => Show("초월_신문철_AP");
    static string JiEter() => Show("영원_이지원");
    static string HjsAd() => Show("초월_황준석_ADAP");
    static string BaeAd() => Show("초월_배성령_AD");
    static string ParkAd() => Show("초월_박민석_ADAP");
    static string GiAd() => Show("초월_박기찬_AD");
    // 프리팹 계층 덤프 — 부가 이펙트 위치 진단(경로는 DumpPath)
    static string DumpPath = "Assets/Resources/Effects/Sphere/sang_ad_a_body.prefab";
    static string DumpSang() => Dump("Assets/Resources/Effects/Sphere/sang_ad_a_body.prefab");
    static string Dump(string path)
    {
        GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (g == null) return "없음 " + path;
        var sb = new System.Text.StringBuilder();
        foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
        {
            Renderer r = t.GetComponent<Renderer>();
            sb.AppendLine($"{t.name} 로컬 {t.localPosition:F2} 회전 {t.localEulerAngles:F0} 배율 {t.localScale:F2}" + (r != null ? $" 렌더러 경계 {r.bounds.min:F1}~{r.bounds.max:F1}" : ""));
        }
        return sb.ToString();
    }
    static string KimMan() => Show("초월_김만경_AD");
    static string SangAd() => Show("초월_최상호_AD");
    static string Keyjaru() => Show("초월_구주호_AD");
    // 스킬 중 부품 확인용 — 모든 UnitSphereArt에 스킬 신호를 6초 준다
    static string PulseAll() { foreach (UnitSphereArt a in Object.FindObjectsByType<UnitSphereArt>(FindObjectsSortMode.None)) a.PulseSkill(6f); return "스킬 신호 6초"; }
    static string AuraHidden() => Show("히든_이요한");
    static string AuraChar() => Show("전설적인_백기현");
    static string AuraTrans() => Show("초월_임장혁_AD");
    // PM 10-01 고유 동작 확대 확인용
    static string N_Hyunmo() => Show("영원_윤현모");
    static string N_Dohyun() => Show("불멸_고도현");
    static string N_Sungryung() => Show("초월_배성령_AD");
    static string N_Jaemo() => Show("초월_양재모_AD");
    static string N_Youngmin() => Show("제한_최영민");
    static string N_Yongtae() => Show("전설적인_김용태");
    static string N_Eunseok() => Show("전설적인_박은석");
    static string N_Gilla() => Show("특수함_황길라");
    static string N_Jeonggi() => Show("특별함_황정기");
    static string N_Jaegyu() => Show("안흔함_강재규");

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
