using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 초상 전수 탐침(구현담당2 10-08): 플레이 중 gameshot의 call:PortraitSurvey.Start 로 시작 — 로스터 유닛 프리팹을 하나씩 초상 무대에 세워
// ClaudeBridge/portraits/<이름>.png 로 저장한다(모자·소품에 머리 윗선이 끌려 얼굴이 안 잡힌 것을 눈으로 고르려고).
// 읽기는 게임과 같은 WaitForEndOfFrame 코루틴에서 한다(편집기 update에서 읽으면 검은 칸이 나왔다).
public static class PortraitSurvey
{
    class Runner : MonoBehaviour { }
    static int done, total;
    public static int Limit;
    public static string StartTwo() { Limit = 2; return Start(); }

    public static string Start()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var queue = new List<UnitData>();
        foreach (string g in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }))
        {
            var u = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g));
            if (u != null && u.prefab != null && !File.Exists(Path.Combine("ClaudeBridge", "portraits", u.name + ".png"))) queue.Add(u);   // 이미 찍은 건 건너뜀
        }
        string only = Path.Combine("ClaudeBridge", "portrait_names.txt");   // 있으면 이 이름들만(한 줄에 하나, NFC) 다시 찍는다
        if (File.Exists(only)) { var names = new HashSet<string>(File.ReadAllLines(only)); queue = new List<UnitData>(); foreach (string g in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })) { var u = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)); if (u != null && u.prefab != null && names.Contains(u.name.Normalize(System.Text.NormalizationForm.FormC))) queue.Add(u); } }
        queue.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (Limit > 0 && queue.Count > Limit) queue = queue.GetRange(1, Limit);
        total = queue.Count; done = 0;
        Directory.CreateDirectory(Path.Combine("ClaudeBridge", "portraits"));
        PortraitStage.CloseUp = true;
        var hud = Object.FindFirstObjectByType<GameHud>();
        if (hud != null) hud.enabled = false;   // HUD가 매 프레임 초상 무대를 자기 선택으로 되돌린다 — 전수 동안 끈다
        var host = new GameObject("[PortraitSurvey]", typeof(Runner)).GetComponent<Runner>();
        host.StartCoroutine(Run(queue));
        return $"✅ 시작 {queue.Count}기";
    }

    public static string Progress() => $"✅ {done}/{total}";

    static IEnumerator Run(List<UnitData> queue)
    {
        var endOfFrame = new WaitForEndOfFrame();
        foreach (UnitData unit in queue)
        {
            GameObject go = Object.Instantiate(unit.prefab, new Vector3(0f, -500f, 0f), Quaternion.identity);
            PortraitStage.Show(go);
            for (int i = 0; i < 16; i++) yield return endOfFrame;   // Idle 한 번 + 맞춤 3단계
            SaveTexture(PortraitStage.Texture, Path.Combine("ClaudeBridge", "portraits", unit.name + ".png"));
            Object.Destroy(go);
            PortraitStage.Show(null);
            done++;
            yield return null;
        }
    }

    static void SaveTexture(RenderTexture rt, string path)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.Destroy(tex);
    }
}
