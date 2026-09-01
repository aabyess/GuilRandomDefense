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
    [SerializeField] SelectionManager selectionManager;

    SelectionManager Selection => selectionManager != null
        ? selectionManager
        : selectionManager = FindFirstObjectByType<SelectionManager>();

    GoldWallet Wallet => goldWallet != null ? goldWallet : PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
    UnitInventory Inventory => unitInventory != null ? unitInventory : PlayerContext.Local != null ? PlayerContext.Local.UnitInventory : null;
    Warehouse Warehouse => warehouse != null ? warehouse : PlayerContext.Local != null ? PlayerContext.Local.Warehouse : null;

    // 정식 HUD가 골드·라운드·적 수·인벤토리를 모두 보여주므로 기본은 접어둔다.
    // 지우지 않고 남기는 이유: 유닛 스탯이 적용됐는지, 사거리 안인지 같은 건
    // 화면만 봐서는 알 수 없고, 실제로 이 패널로 여러 번 원인을 찾았다.
    bool visible = false;

    void Update()
    {
        if (Keyboard.current == null) return;

        // 정식 HUD가 골드·라운드·적 수를 이미 보여준다. 화면을 덮는 게 거슬릴 때 F1로 접는다.
        if (Keyboard.current.f1Key.wasPressedThisFrame)
            visible = !visible;

        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            TryCombineFirst();
        }
    }

    // OnGUI는 한 프레임에 두 번(Layout/Repaint) 불리고 그 안에서 두 곳이 이 목록을 쓴다.
    // 그대로 두면 프레임당 4번, 레시피 199개를 매번 훑는다.
    const float RecipeCacheInterval = 0.4f;

    readonly List<CombineRecipe> cachedRecipes = new List<CombineRecipe>();
    float nextRecipeCacheTime;

    List<CombineRecipe> CachedRecipes()
    {
        if (combineSystem == null) return cachedRecipes;
        if (Time.unscaledTime < nextRecipeCacheTime) return cachedRecipes;

        nextRecipeCacheTime = Time.unscaledTime + RecipeCacheInterval;

        // GetAvailableRecipes는 재사용 버퍼를 돌려준다 — 들고 있으려면 복사해야 한다.
        cachedRecipes.Clear();
        cachedRecipes.AddRange(combineSystem.GetAvailableRecipes());
        return cachedRecipes;
    }

    void TryCombineFirst()
    {
        if (combineSystem == null) return;

        List<CombineRecipe> available = CachedRecipes();
        if (available.Count == 0) return;

        combineSystem.TryCombine(available[0]);
    }

    RtsCameraController cameraRef;

    RtsCameraController CameraRef => cameraRef != null
        ? cameraRef
        : cameraRef = FindFirstObjectByType<RtsCameraController>();

    void OnGUI()
    {
        if (!visible)
        {
            GUI.Label(new Rect(10, 10, 200, 20), "F1: 디버그 정보");
            return;
        }

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

        // 방향키와 가장자리 밀기가 정말 같은 속도인지 확인용. 둘 다 같은 CurrentSpeed로 이어져야 한다.
        if (CameraRef != null)
        {
            GUILayout.Label($"카메라 높이 {CameraRef.transform.position.y:F0} " +
                            $"(높이 배율 {CameraRef.CurrentSpeed / Mathf.Max(0.01f, CameraRef.KeyboardAxis.magnitude + CameraRef.EdgeAxis.magnitude):F0} 기준)");
            GUILayout.Label($"카메라 입력  방향키 {CameraRef.KeyboardAxis}  가장자리 {CameraRef.EdgeAxis}");
            GUILayout.Label($"카메라 속도  {CameraRef.CurrentSpeed:F0}/초");
        }

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
            foreach (CombineRecipe recipe in CachedRecipes())
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

        DrawSelectionPanel();
    }

    // 선택한 대상의 실제 런타임 스탯을 보여준다. 스탯이 붙지 않았거나 사거리가 모자라서
    // 적이 안 죽는 경우를 화면에서 바로 구분하기 위한 임시 패널이다.
    void DrawSelectionPanel()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 330, 10, 320, 300));

        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count == 0)
        {
            GUILayout.Label("선택된 대상 없음 (좌클릭으로 선택)");
        }
        else
        {
            Selectable first = selection.Selected[0];
            if (first == null)
            {
                GUILayout.Label("선택 대상이 사라졌습니다");
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label($"선택: {first.name}  (총 {selection.Selected.Count}개)");

            if (first.TryGetComponent(out UnitAttacker attacker))
            {
                float distance = attacker.DistanceToClosestEnemy();
                GUILayout.Label($"공격력 {attacker.AttackDamage}  사거리 {attacker.AttackRange}  간격 {attacker.AttackInterval:F2}s");
                GUILayout.Label(float.IsPositiveInfinity(distance)
                    ? "가장 가까운 적: 없음"
                    : $"가장 가까운 적: {distance:F1}m  →  {(distance <= attacker.AttackRange ? "사거리 안 (때리는 중)" : "사거리 밖")}");

                if (attacker.AttackDamage <= 0f)
                    GUILayout.Label("⚠️ 공격력이 0이라 절대 죽지 않습니다");
            }
            else
            {
                GUILayout.Label("⚠️ UnitAttacker가 없습니다 — 위습이거나 공격 못 하는 오브젝트");
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("필드 몹 체력 (최대 8마리)");
        int shown = 0;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (shown++ >= 8) break;
            GUILayout.Label($"  hp {enemy.Hp:F1}");
        }

        GUILayout.EndArea();
    }

    // 위습은 인벤토리가 아니라 필드에 존재하는 유닛이라, 씬에서 내 소유 위습을 직접 세어 표시한다.
    // OnGUI는 한 프레임에 두 번(Layout/Repaint) 불리므로, 여기서 씬 전체를 훑으면 프레임당 2회 전수 조사가 된다.
    // 디버그 표시라 초당 4회면 충분하다.
    const float WispCountInterval = 0.25f;

    readonly Dictionary<string, int> wispCounts = new Dictionary<string, int>();
    float nextWispCountTime;

    Dictionary<string, int> CountOwnedWisps()
    {
        if (Time.unscaledTime < nextWispCountTime) return wispCounts;
        nextWispCountTime = Time.unscaledTime + WispCountInterval;

        Dictionary<string, int> counts = wispCounts;
        counts.Clear();
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
