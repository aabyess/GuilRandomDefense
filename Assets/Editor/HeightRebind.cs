using System.Linq;
using System.Text;
using UnityEditor;

/// <summary>키 배수 표(ArtBinder.TierHeightMultipliers)에 든 유닛만 프리팹을 다시 짓는다(전체 모델 배선 금지 규칙). call HeightRebind.Run</summary>
static class HeightRebind
{
    static string Run()
    {
        var sb = new StringBuilder();
        foreach (string unit in ArtBinder.TierHeightUnits) sb.AppendLine(ArtBinder.BindOneUnit(unit));
        sb.AppendLine(SkinSwapImporter.RehideAll());   // BindOneUnit이 프리팹을 새로 지으면 SkinClipHider가 사라진다
        return sb.ToString();
    }
}
