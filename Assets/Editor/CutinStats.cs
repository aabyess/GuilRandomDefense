using UnityEditor;
using UnityEngine;

// 컷인 텍스처 GPU 메모리 합(압축 포맷 기준) — gameshot/메뉴 call:CutinStats.Report. 빌드 파일 크기(Crunch)는 Library/Artifacts 파일로 따로 잰다.
public static class CutinStats
{
    public static string Report()
    {
        long gpu = 0, count = 0, px = 0;
        var util = typeof(Editor).Assembly.GetType("UnityEditor.TextureUtil").GetMethod("GetStorageMemorySizeLong");
        var fmt = new System.Collections.Generic.Dictionary<string, int>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Cutin" }))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (tex == null) continue;
            count++; px += (long)tex.width * tex.height;
            gpu += (long)util.Invoke(null, new object[] { tex });
            string f = tex.format.ToString(); fmt[f] = fmt.TryGetValue(f, out int n) ? n + 1 : 1;
        }
        var parts = new System.Text.StringBuilder();
        foreach (var kv in fmt) parts.Append($"{kv.Key}×{kv.Value} ");
        return $"✅ 컷인 텍스처 {count}장 · {px / 1e6f:0.0} Mpx · 압축 포맷 {parts}· GPU 메모리 합 {gpu / 1048576f:0.0} MB";
    }
}
