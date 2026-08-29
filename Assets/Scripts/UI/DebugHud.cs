using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 임시 디버그용, 정식 UI는 M7에서 교체 예정.
public class DebugHud : MonoBehaviour
{
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] UnitInventory unitInventory;
    [SerializeField] CombineSystem combineSystem;
    [SerializeField] RoundManager roundManager;
    [SerializeField] Warehouse warehouse;

    GoldWallet Wallet => goldWallet != null ? goldWallet : PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
    UnitInventory Inventory => unitInventory != null ? unitInventory : PlayerContext.Local != null ? PlayerContext.Local.UnitInventory : null;
    Warehouse Warehouse => warehouse != null ? warehouse : PlayerContext.Local != null ? PlayerContext.Local.Warehouse : null;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            TryCombineFirst();
        }
    }

    void TryCombineFirst()
    {
        if (combineSystem == null) return;

        List<CombineRecipe> available = combineSystem.GetAvailableRecipes();
        if (available.Count == 0) return;

        combineSystem.TryCombine(available[0]);
    }

    void OnGUI()
    {
        GoldWallet wallet = Wallet;
        UnitInventory inventory = Inventory;

        GUILayout.BeginArea(new Rect(10, 10, 320, 400));

        GUILayout.Label($"골드: {(wallet != null ? wallet.Gold.ToString() : "-")}");

        if (roundManager != null)
        {
            GUILayout.Label($"라운드: {roundManager.CurrentRound}  남은시간: {roundManager.RoundTimeLeft:F1}s");
            GUILayout.Label($"데스카운트: {roundManager.DeathCount}{(roundManager.IsGameOver ? " (게임 종료)" : "")}");
        }

        GUILayout.Label($"필드 몹 수: {EnemyDummy.Active.Count}");

        Warehouse warehouseRef = Warehouse;
        GUILayout.Label($"창고: {(warehouseRef != null ? warehouseRef.Stored.Count.ToString() : "-")}개");

        GUILayout.Space(10);
        GUILayout.Label("인벤토리");

        if (inventory != null)
        {
            Dictionary<UnitData, int> counts = CountByUnit(inventory.Units);

            foreach (KeyValuePair<UnitData, int> entry in counts)
            {
                GUILayout.Label($"{entry.Key.unitName} x{entry.Value}");
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("조합 가능한 레시피 (V키: 첫 번째 조합)");

        if (combineSystem != null)
        {
            foreach (CombineRecipe recipe in combineSystem.GetAvailableRecipes())
            {
                string resultName = recipe.result != null ? recipe.result.unitName : "?";
                GUILayout.Label($"[{recipe.commandId}] → {resultName}");
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("보유 위습 (G키: 테스트용 위습 1개 지급)");

        foreach (KeyValuePair<string, int> entry in CountOwnedWisps())
        {
            GUILayout.Label($"{entry.Key} x{entry.Value}");
        }

        GUILayout.EndArea();
    }

    // 위습은 인벤토리가 아니라 필드에 존재하는 유닛이라, 씬에서 내 소유 위습을 직접 세어 표시한다.
    // 임시 디버그 표시라 FindObjectsByType을 쓰지만, 위습 수는 소수(개당 1~수 마리)라 부담이 크지 않다.
    Dictionary<string, int> CountOwnedWisps()
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        int localPlayerId = LocalPlayer.LocalPlayerId;

        foreach (Wisp wisp in FindObjectsByType<Wisp>(FindObjectsSortMode.None))
        {
            if (wisp.Data == null) continue;
            if (wisp.TryGetComponent(out OwnedByPlayer owner) && owner.OwnerId != localPlayerId) continue;

            string name = wisp.Data.wispName;
            counts.TryGetValue(name, out int count);
            counts[name] = count + 1;
        }

        return counts;
    }

    Dictionary<UnitData, int> CountByUnit(IReadOnlyList<UnitData> units)
    {
        Dictionary<UnitData, int> counts = new Dictionary<UnitData, int>();

        foreach (UnitData unit in units)
        {
            if (unit == null) continue;

            counts.TryGetValue(unit, out int count);
            counts[unit] = count + 1;
        }

        return counts;
    }
}
