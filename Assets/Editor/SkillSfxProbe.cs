using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 스킬 효과음(SkillSfx, 09-30) 실판 점검 — 표(Resources/Sounds/SkillSfxTable.txt)에 걸린 로스터를 0번 레인 가운데 한 줄로 세우고
// 앞에 표적 셋씩(체력 1e6, 멈춤). 판 동안 SkillSfx.Broadcast를 세서 「어느 소리가 몇 번 골라졌나」와 실제 재생 횟수를 낸다.
// 표의 클립이 전부 Resources에서 읽히는지도 같이 본다(파일 이름 어긋남 잡기).
//   gameshot sfx.png 1 1920x1080 click?:보통 wait:2 call:SkillSfxProbe.Arena wait:20 call:SkillSfxProbe.Report
// 시작 칸을 바꾸려면 Arena 대신 Arena8·Arena16…(여덟씩 끊어 세운다).
static class SkillSfxProbe
{
    static readonly Dictionary<int, int> picked = new Dictionary<int, int>();
    static readonly List<string> clipNames = new List<string>();
    static readonly List<string> lineup = new List<string>();

    static string Arena() => ArenaFrom(0);
    static string Arena8() => ArenaFrom(8);
    static string Arena16() => ArenaFrom(16);
    static string Arena24() => ArenaFrom(24);
    static string Arena32() => ArenaFrom(32);

    static string ArenaFrom(int start)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        TextAsset text = Resources.Load<TextAsset>("Sounds/SkillSfxTable");
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        EnemyData dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        if (text == null || spawner == null || lane == null || dummyData == null) return "❌ 표·UnitSpawner·레인·표적 없음";

        Dictionary<string, UnitData> roster = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g))).Where(u => u != null)
            .GroupBy(u => Nfc(u.name)).ToDictionary(g => g.Key, g => g.First());
        clipNames.Clear();
        var missing = new List<string>();
        var names = new List<string>();   // 표 순서대로 로스터(S줄은 스킬 에셋 이름에 든 로스터)
        foreach (string raw in text.text.Split('\n'))
        {
            string[] f = raw.TrimEnd('\r').Split('\t');
            if (f[0] == "C" && f.Length >= 3)
            {
                clipNames.Add(f[1]);
                if (Resources.Load<AudioClip>("Sounds/" + f[2] + "/" + f[1]) == null) missing.Add(f[1]);
            }
            else if (f[0] == "R" && f.Length >= 2) { if (!names.Contains(Nfc(f[1]))) names.Add(Nfc(f[1])); }
            else if (f[0] == "S" && f.Length >= 2)
            {
                string owner = roster.Keys.Where(k => Nfc(f[1]).Contains(k)).OrderByDescending(k => k.Length).FirstOrDefault();
                if (owner != null && !names.Contains(owner)) names.Add(owner);
            }
        }
        List<UnitData> units = names.Where(roster.ContainsKey).Select(n => roster[n]).Skip(start).Take(8).ToList();

        Vector3 c = lane.LaneCenter;
        lineup.Clear();
        for (int k = 0; k < units.Count; k++)
        {
            Vector3 home = c + new Vector3((k - (units.Count - 1) * 0.5f) * 110f, 0f, -40f);
            for (int i = 0; i < 3; i++)
            {
                GameObject go = Object.Instantiate(dummyData.prefab, home + new Vector3((i - 1) * 25f, 0f, 60f), Quaternion.Euler(0f, 180f, 0f));
                if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
                if (go.TryGetComponent(out EnemyDummy d)) { d.Initialize(dummyData, 1e6f); d.SetLane(-1); }
            }
            spawner.Spawn(units[k], home, 0);
            lineup.Add(units[k].name);
        }
        picked.Clear();
        SkillSfx.Broadcast -= OnCast;
        SkillSfx.Broadcast += OnCast;
        if (cam != null) cam.MoveTo(c);
        return $"표 로스터 {names.Count} 중 {start}~ {units.Count}기: {string.Join(" · ", lineup)}\n"
               + $"   클립 {clipNames.Count} 중 못 읽음 {missing.Count}{(missing.Count > 0 ? ": " + string.Join(", ", missing) : "")}";
    }

    static string Nfc(string s) => s.Normalize(NormalizationForm.FormC);

    static void OnCast(int clip, float volume, Vector3 position) =>
        picked[clip] = picked.TryGetValue(clip, out int n) ? n + 1 : 1;

    static string Report()
    {
        SkillSfx.Broadcast -= OnCast;
        var sb = new StringBuilder($"스킬 효과음: 고름 {SkillSfx.CastCount} · 실제 재생 {SkillSfx.PlayCount}\n");
        foreach (var kv in picked.OrderByDescending(k => k.Value))
            sb.AppendLine($"   {(kv.Key < clipNames.Count ? clipNames[kv.Key] : kv.Key.ToString())} × {kv.Value}");
        if (picked.Count == 0) sb.AppendLine("   ⚠️ 0 — 한 번도 안 골라졌다(훅이 없거나 스킬이 안 나갔다)");
        return sb.ToString();
    }
}
