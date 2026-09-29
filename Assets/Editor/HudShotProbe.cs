using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 하단 콘솔(GameHud) 사진용 선택 상태 만들기 — gameshot의 call:로 판 안에서 부른다(09-29 워크3 콘솔 개편 전후 비교).
///   call:HudShotProbe.SpawnTwelve     첫 선택 유닛 둘레에 로스터 앞쪽 유닛 11기를 더 세운다(내 것)
///   call:HudShotProbe.SelectAllMine   내 유닛 전부(최대 12) — 여러 기 카드 격자
///   call:HudShotProbe.SelectCombiner  조합식이 있는 내 유닛 한 기 — 초상화·정보·조합 결과 칸
///   call:HudShotProbe.SelectGamble    내 도박소 — 상점 9칸
/// 가상 마우스 select:는 하단 바 가장자리 유닛에서 빗나가서(09-29 두 번) 입력 경로 검증이 아닌 **화면 배치 사진**에만 이걸 쓴다.
/// </summary>
public static class HudShotProbe
{
    static readonly MethodInfo AddToSelection =
        typeof(SelectionManager).GetMethod("AddToSelection", BindingFlags.Instance | BindingFlags.NonPublic);

    static SelectionManager Selection() => Object.FindFirstObjectByType<SelectionManager>();

    static bool Mine(Selectable s) =>
        s != null && (!s.TryGetComponent(out OwnedByPlayer owner) || owner.OwnerId == LocalPlayer.LocalPlayerId);

    public static string SpawnTwelve()
    {
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        Selectable seed = Selectable.All.Where(Mine).FirstOrDefault(s => s.GetComponent<UnitIdentity>() != null);
        if (spawner == null || seed == null) return "❌ UnitSpawner/내 유닛 없음";
        UnitData[] roster = UnityEditor.AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && d.prefab != null).Take(11).ToArray();
        int n = 0;
        foreach (UnitData d in roster)
        {
            Vector3 at = seed.transform.position + new Vector3((n % 4 - 1.5f) * 12f, 0f, (n / 4 + 1) * 12f);
            if (spawner.Spawn(d, at, LocalPlayer.LocalPlayerId) != null) n++;
        }
        return $"세움 {n}기";
    }

    public static string SelectAllMine()
    {
        SelectionManager selection = Selection();
        if (selection == null) return "❌ SelectionManager 없음";
        selection.ClearSelection();
        int n = 0;
        foreach (Selectable s in Selectable.All.Where(Mine).Where(s => s.GetComponent<UnitIdentity>() != null).ToList())
        {
            AddToSelection.Invoke(selection, new object[] { s });
            n++;
        }
        return $"선택 {selection.Selected.Count}기 (후보 {n})";
    }

    public static string SelectCombiner()
    {
        SelectionManager selection = Selection();
        CombineSystem combine = Object.FindFirstObjectByType<CombineSystem>();
        if (selection == null || combine == null) return "❌ SelectionManager/CombineSystem 없음";
        Selectable pick = Selectable.All.Where(Mine).FirstOrDefault(s =>
            s.TryGetComponent(out UnitIdentity id) && id.Data != null && combine.GetRecipesStartingWith(id.Data).Count > 0);
        if (pick == null) pick = Selectable.All.Where(Mine).FirstOrDefault(s => s.GetComponent<UnitIdentity>() != null);
        if (pick == null) return "❌ 내 유닛 없음";
        selection.SelectOnly(pick);
        return $"선택 {pick.name}";
    }

    public static string SelectGamble()
    {
        SelectionManager selection = Selection();
        if (selection == null) return "❌ SelectionManager 없음";
        Selectable pick = Selectable.All.Where(Mine).FirstOrDefault(s => s.name.Contains("도박소"));
        if (pick == null) return "❌ 도박소 없음";
        selection.SelectOnly(pick);
        return $"선택 {pick.name}";
    }
}
