using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 날개(10-09 사장님 확정): **유닛 등급이 정하면 자동으로 붙는다**(원작 S2는 대시보드 꾸미기지만 우리는 선택 창·세이브·해금 없음).
/// 초월 = 흰 날개(Dashboard_YH2) · 불멸 = 파란 날개(Dashboard_Wings_enthusiasm2) · 영원 = 불 날개(DashBoard_Wing3_FIre).
/// 검(YH6)·물(ChojiWing)·왜곡(IF_wing)은 프리팹만 두고 붙이지 않는다(Resources/Wings/).
/// 부착: 호스트는 UnitSpawner.Spawn, 클라는 NetReplicaBuilder.Build가 부른다 — 등급이 유닛 데이터에서 나오니 따로 동기화가 필요 없다.
/// 날개 폭 = 유닛 키 × 2.2에서 시작(blender 지침), 원점은 가슴(키의 약 0.68 높이)·등 쪽.
/// 소환 순서: 프리팹엔 UnitIdentity가 없고 데이터는 SetData가 준다 → Awake 캐시를 쓰지 않고 데이터를 인자로 받는다.
/// </summary>
public static class UnitWings
{
    /// <summary>등급 → 날개 프리팹 이름(Resources/Wings/). 여기 한 곳이 표다.</summary>
    static readonly Dictionary<UnitGrade, string> Table = new Dictionary<UnitGrade, string>
    {
        { UnitGrade.Transcendent, "Dashboard_YH2" },
        { UnitGrade.Immortal,     "Dashboard_Wings_enthusiasm2" },
        { UnitGrade.Eternal,      "DashBoard_Wing3_FIre" },
    };

    public const float SpanPerHeight = 2.2f;     // 날개 폭 ÷ 유닛 키
    public const float ChestHeightRatio = 0.80f; // 날개 뿌리(어깻죽지) 높이 ÷ 유닛 키(10-09 PM: 0.68은 허리라 낮다)
    public const string ChildName = "[날개]";

    public static string WingNameFor(UnitGrade grade) => Table.TryGetValue(grade, out string n) ? n : null;

    /// <summary>이 유닛 겉모습(루트)에 등급 날개를 붙인다. 이미 붙어 있거나 등급에 날개가 없으면 아무 일도 없다.</summary>
    public static GameObject Attach(GameObject unit, UnitData data)
    {
        if (unit == null || data == null) return null;
        string wingName = WingNameFor(data.grade);
        if (wingName == null) return null;
        if (unit.transform.Find(ChildName) != null) return null;
        GameObject prefab = Resources.Load<GameObject>("Wings/" + wingName);
        if (prefab == null) return null;   // 모델이 아직 안 들어온 빌드 — 조용히 건너뛴다

        // 유닛 키(월드): 몸 렌더러만(사거리 표시 등은 뺀다)
        Bounds bounds = default; bool any = false;
        foreach (Renderer r in unit.GetComponentsInChildren<Renderer>(false))
        {
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            if (r.GetComponentInParent<WingModel>() != null) continue;
            string n = r.gameObject.name;
            if (n.Contains("Indicator") || n.Contains("Disc") || n.Contains("Ring")) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        float height = any ? Mathf.Max(0.5f, bounds.size.y) : 2f;
        Vector3 chest = any ? new Vector3(bounds.center.x, bounds.min.y + height * ChestHeightRatio, bounds.center.z) : unit.transform.position + Vector3.up * height * ChestHeightRatio;

        GameObject wing = Object.Instantiate(prefab, unit.transform, false);
        wing.name = ChildName;
        WingModel model = wing.GetComponent<WingModel>();
        float span = model != null ? Mathf.Max(0.01f, model.restSpan) : 3.7f;
        float worldScale = height * SpanPerHeight / span;
        Vector3 parentScale = unit.transform.lossyScale;
        wing.transform.localScale = new Vector3(worldScale / Mathf.Max(0.0001f, parentScale.x), worldScale / Mathf.Max(0.0001f, parentScale.y), worldScale / Mathf.Max(0.0001f, parentScale.z));
        wing.transform.position = chest + unit.transform.rotation * (model != null ? model.chestOffset * worldScale : Vector3.zero);
        wing.transform.localRotation = Quaternion.Euler(model != null ? model.localEuler : Vector3.zero);
        // 좌우 중심 보정(10-14): 불 날개처럼 뿌리가 한쪽 끝인 초승달 모양은 몸 가운데에 두면 한쪽으로 쏠린다.
        // 휴식 자세의 렌더러 경계 중심이 유닛 좌우 축에서 가슴 중심과 같아지게 옆으로만 옮긴다(대칭 날개는 거의 0).
        Renderer[] wr = wing.GetComponentsInChildren<Renderer>(false);
        if (wr.Length > 0)
        {
            Bounds wb = wr[0].bounds; for (int i = 1; i < wr.Length; i++) wb.Encapsulate(wr[i].bounds);
            Vector3 right = unit.transform.right;
            float lateral = Vector3.Dot(chest - wb.center, right);
            wing.transform.position += right * lateral;
        }
        // 뼈가 있으면 그 뼈를 따라 움직이게(동작할 때 몸과 같이) — 월드 위치·회전·크기는 그대로 둔 채 부모만 옮긴다. 이름이 안 맞으면 루트에 둔다.
        Transform bone = FindChestBone(unit.transform);
        if (bone != null) wing.transform.SetParent(bone, true);
        return wing;
    }

    static readonly string[] BoneNames = { "upperchest", "spine2", "chest", "spine1" };
    static Transform FindChestBone(Transform root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (string key in BoneNames)
            foreach (Transform t in all)
                if (t != root && t.name.ToLowerInvariant().Contains(key) && !t.name.Contains("날개") && t.GetComponent<Renderer>() == null) return t;
        return null;
    }
}

