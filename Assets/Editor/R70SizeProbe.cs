using System.Linq;
using UnityEditor;
using UnityEngine;

// R70 라인몹·신지우 크기 촬영(구현담당1, 10-16) — 실제 WaveSpawner.SpawnRound 경로로 Wave_Round70을 스폰.
// gameshot r70_new.png 8 1920x1080 call:ShopSlotProbe.Fund call:R70SizeProbe.Place wait:2 call:R70SizeProbe.SpawnNew wait:14
// 변경 전 값 비교: SpawnNew 대신 SpawnOld(visualScale 1·1로 스폰 뒤 즉시 원복 — 메모리만, 에셋 파일은 안 건드림)
static class R70SizeProbe
{
    static readonly string[] Units = { "흔함_강재규", "흔함_문필환", "안흔함_이호준", "희귀함_김경현", "흔함_노태현" };

    static string PlaceBottom() => PlaceAt(false);
    static string Place() => PlaceAt(true);
    static string PlaceAt(bool left)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sp = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        int n = 0;
        foreach (string u in Units)
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{u}.asset");
            if (d == null) continue;
            sp.Spawn(d, left ? new Vector3(-1880f, 8f, 1400f + 26f * n) : new Vector3(-1790f + 26f * n, 8f, 1345f), 0); n++;   // 라인몹 왼쪽 세로 줄(x≈-1915) 바로 안쪽 — 지나가는 몹과 크기 비교
        }
        return $"유닛 {n}기 세움";
    }

    static string Spawn(bool old)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var mob = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R70_라인몹.asset");
        var boss = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R70_신지우.asset");
        float m0 = mob.visualScale, b0 = boss.visualScale;
        if (old) { mob.visualScale = 1f; boss.visualScale = 1f; }
        var wave = AssetDatabase.LoadAssetAtPath<WaveData>("Assets/Data/Waves/Wave_Round70.asset");
        Object.FindFirstObjectByType<WaveSpawner>().SpawnRound(wave);
        restore = () => { mob.visualScale = m0; boss.visualScale = b0; };
        return $"스폰 {(old ? "변경 전(1·1)" : "변경 후")} · 라인몹 {m0} 보스 {b0}";
    }
    static System.Action restore;
    static string SpawnNew() => Spawn(false);
    static string SpawnOld() => Spawn(true);
    static string Restore() { restore?.Invoke(); restore = null; return "원복"; }
    // 현 코드에서 라인몹 visualScale은 자리표시 「몸」에만 곱해져 스킨엔 안 걸린다 — 「걸렸다면 이렇게 보인다」 재현: 스킨 형제 노드에 같은 배율을 직접 곱한다.
    static string EmulateModelScale()
    {
        int n = 0;
        foreach (var e in EnemyDummy.Active.Where(x => x != null && x.Data != null && x.Data.name == "Enemy_R70_라인몹").ToList())
        {
            var vr = (Transform)typeof(EnemyDummy).GetField("visualRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(e);
            foreach (Transform c in e.transform)
                if (c != vr && c.GetComponentInChildren<Renderer>() != null) { c.localScale *= e.Data.visualScale; n++; }
        }
        return $"스킨 노드 {n}개에 ×0.6 직접 적용";
    }

    // 스토리 건물(visualScale 50)은 몸에 렌더러가 있어 스킨 노드 규칙에 안 걸려야 한다 — skinNode 비어 있음·visualRoot만 배율.
    static string StoryCheck()
    {
        var sb = new System.Text.StringBuilder();
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        foreach (string n in new[] { "Enemy_Story01_하이츠", "Enemy_R70_라인몹", "Enemy_R70_신지우", "Enemy_R01_박진웅" })
        {
            var ed = AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{n}.asset");
            var go = Object.Instantiate(ed.prefab, c + new Vector3(0f, 0f, 30f), Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var e = go.GetComponent<EnemyDummy>(); e.Initialize(ed, 1f);
            var vr = (Transform)typeof(EnemyDummy).GetField("visualRoot", bf).GetValue(e);
            var sk = (Transform)typeof(EnemyDummy).GetField("skinNode", bf).GetValue(e);
            var b = new Bounds(go.transform.position, Vector3.zero); bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            sb.AppendLine($"{n} visualScale {ed.visualScale} · 루트 {go.transform.localScale.x:F2} · visualRoot {(vr != null ? vr.localScale.x.ToString("F3") : "null")} · skinNode {(sk != null ? sk.name + " " + sk.localScale.x.ToString("F3") : "없음")} · 경계 높이 {b.size.y:F1}");
            Object.Destroy(go);
        }
        return sb.ToString();
    }

    static string Report()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var e in EnemyDummy.Active.Where(x => x != null && x.Data != null && x.Data.name.Contains("R70")).Take(6))
        {
            var vr = (Transform)typeof(EnemyDummy).GetField("visualRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(e);
            var b = new Bounds(e.transform.position, Vector3.zero); bool first = true;
            foreach (var r in e.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            sb.AppendLine($"{e.name} 데이터 {e.Data.name} visualScale {e.Data.visualScale} · 루트 {e.transform.localScale.x:F2} · visualRoot {(vr != null ? vr.localScale.x.ToString("F3") : "null")} · 경계 높이 {b.size.y:F1}");
        }
        return sb.ToString();
    }
}
