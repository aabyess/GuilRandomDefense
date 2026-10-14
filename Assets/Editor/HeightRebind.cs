using System.Linq;
using System.Text;
using UnityEditor;

/// <summary>키 배수 표(ArtBinder.TierHeightMultipliers)에 든 유닛만 프리팹을 다시 짓는다(전체 모델 배선 금지 규칙). call HeightRebind.Run</summary>
static class HeightRebind
{
    /// <summary>초월·불멸 키 ×1.2(10-14): 초월_·불멸_ 로스터 프리팹 전부 다시 짓는다. call HeightRebind.RunTranscendImmortal</summary>
    static string RunTranscendImmortal()
    {
        var sb = new StringBuilder();
        foreach (string dir in System.IO.Directory.GetDirectories("Assets/Art/Units"))
        {
            string unit = System.IO.Path.GetFileName(dir).Normalize(System.Text.NormalizationForm.FormC);   // 맥 폴더명은 NFD일 수 있다(초월_김경현_AP가 접두 비교에서 빠졌다)
            foreach (string pre in ArtBinder.TranscendImmortalPrefixes)
                if (unit.StartsWith(pre)) { sb.AppendLine(ArtBinder.BindOneUnit(unit)); break; }
        }
        sb.AppendLine(SkinSwapImporter.RehideAll());
        return sb.ToString();
    }

    static string Run()
    {
        var sb = new StringBuilder();
        foreach (string unit in ArtBinder.TierHeightUnits) sb.AppendLine(ArtBinder.BindOneUnit(unit));
        sb.AppendLine(SkinSwapImporter.RehideAll());   // BindOneUnit이 프리팹을 새로 지으면 SkinClipHider가 사라진다
        return sb.ToString();
    }
}
