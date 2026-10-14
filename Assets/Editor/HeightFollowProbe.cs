using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>키 맞춘 유닛의 콜라이더·에이전트·선택 고리 반지름이 몸(렌더러 경계)을 따라가는지 수치로 본다. call HeightFollowProbe.Run</summary>
static class HeightFollowProbe
{
    static string Run()
    {
        var sb = new StringBuilder();
        foreach (string unit in ArtBinder.TierHeightUnits.Concat(new[] { "초월_이시원_AD" }))
        {
            var data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{unit}.asset");
            if (data == null || data.prefab == null) { sb.AppendLine($"❌ {unit}"); continue; }
            var g = (GameObject)Object.Instantiate(data.prefab, new Vector3(0, 9000, 0), Quaternion.identity);
            try
            {
                var col = g.GetComponent<Collider>();
                var agent = g.GetComponent<NavMeshAgent>();
                Bounds? b = null;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (b == null) b = r.bounds; else { var q = b.Value; q.Encapsulate(r.bounds); b = q; } }
                string c = col != null ? $"콜라이더 높이 {col.bounds.size.y:F1} 반경 {Mathf.Max(col.bounds.extents.x, col.bounds.extents.z):F2}" : "콜라이더 없음";
                string a = agent != null ? $"에이전트 높이 {agent.height:F1} 반경 {agent.radius:F2}" : "에이전트 없음";
                sb.AppendLine($"{unit}: 렌더 경계 높이 {(b.HasValue ? b.Value.size.y : 0):F1} · {c} · {a} · 데이터 사거리 {data.attackRange}");
            }
            finally { Object.DestroyImmediate(g); }
        }
        return sb.ToString();
    }
}
