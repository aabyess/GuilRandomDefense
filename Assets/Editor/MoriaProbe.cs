using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 모리아(특별함_임채준) 그림자그림자 열매(A113) 판 안 점검 — gameshot:
//   call:MoriaProbe.Setup wait:40 call:MoriaProbe.Report snap:...
// Setup: 부활 확률을 100으로 올려 실제 평타 경로(UnitAttacker.TryRaiseOnKill)가 도는지 본다(에셋 값은 Report가 되돌린다).
// Report: 좀비 수·주인·모델 확인 → 좀비×3 + 흔함_강재규 → 압살롬 조합(CombineSystem.TryCombine, 실제 경로).
static class MoriaProbe
{
    const string MoriaPath = "Assets/Data/Units/Roster/특별함_임채준.asset";
    static float oldChance = -1f;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var moria = AssetDatabase.LoadAssetAtPath<UnitData>(MoriaPath);
        oldChance = moria.raiseOnKillChancePercent;
        moria.raiseOnKillChancePercent = 100f;
        // spawn: 토큰은 모든 동작 뒤에 서므로 직접 세운다 — 0번 레인 첫 긴 변 50% 지점(경로까지 56, 사거리 안; spawn: 실측 좌표).
        GameObject placed = Object.FindFirstObjectByType<UnitSpawner>().Spawn(moria, new Vector3(-1622.5f, 9f, 1356.56f), 0);
        return $"모리아 {moria.unitName} 공 {moria.attackPower} · 공속 {moria.attackSpeed} · 사거리 {moria.attackRange} · 평타타입 {moria.attackType} · 스킬 {(moria.skill != null ? moria.skill.name : "없음")} · 부활 {moria.raiseOnKillUnit?.name} {oldChance}% → 시험용 100% (Report가 되돌림) · 영구={moria.raiseOnKillLifetimeSeconds == 0} · 세움 {(placed != null ? placed.transform.position.ToString("F0") : "실패")}";
    }

    static System.Collections.Generic.List<UnitIdentity> Mine(string unitName) =>
        UnitIdentity.Active.Where(u => u != null && u.Data != null && u.Data.unitName == unitName && u.OwnerId == 0).ToList();

    static string Report()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sb = new StringBuilder();
        var moria = AssetDatabase.LoadAssetAtPath<UnitData>(MoriaPath);
        if (oldChance >= 0f) { moria.raiseOnKillChancePercent = oldChance; sb.AppendLine($"부활 확률 {oldChance}%로 되돌림"); }

        var zombies = Mine("좀비");
        sb.AppendLine($"[좀비] 내 좀비 {zombies.Count}기 (모리아 처치로 생긴 것)");
        foreach (UnitIdentity z in zombies.Take(4))
            sb.AppendLine($"  {z.gameObject.name} 위치 {z.transform.position:F0} · 등급 {z.Data.grade} · 렌더러 {z.GetComponentsInChildren<Renderer>().Length}개");

        var combine = Object.FindFirstObjectByType<CombineSystem>();
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/특별함_압살롬_조합.asset");
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        if (combine == null || recipe == null || spawner == null) return sb + "❌ 조합기·레시피·스포너 없음";
        sb.AppendLine($"[조합기] 레시피 목록 번호 {combine.IndexOfRecipe(recipe)} (−1이면 씬에 연결 안 됨)");
        if (zombies.Count < 3) return sb + $"❌ 좀비 {zombies.Count}기뿐이라 조합 시험 못 함";

        var nami = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/흔함_강재규.asset");
        LaneMarker lane = LaneMarker.Get(0);
        spawner.Spawn(nami, lane != null ? lane.TakeSpawnPosition(nami) : Vector3.zero, 0);
        int absalomBefore = Mine("압살롬").Count, zBefore = Mine("좀비").Count, namiBefore = Mine(nami.unitName).Count;
        bool can = combine.CanCombineNow(recipe);
        bool ok = combine.TryCombine(recipe);
        sb.AppendLine($"[A029 조합] 가능={can} · TryCombine={ok} · 좀비 {zBefore}→{Mine("좀비").Count} · {nami.unitName} {namiBefore}→{Mine(nami.unitName).Count} · 압살롬 {absalomBefore}→{Mine("압살롬").Count}");
        if (!ok) sb.AppendLine("  부족: " + string.Join(" / ", combine.DescribeShortage(recipe)));
        return sb.ToString();
    }
}
