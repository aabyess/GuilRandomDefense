using System.Collections.Generic;

/// <summary>
/// 네트 판의 좌석 배치 — 누가 어느 슬롯(레인)에 앉았는지.
/// NetPlayer가 생기고 사라질 때 채운다(호스트·클라 모두 같은 방식으로 — 둘 다 NetPlayer 복제를 본다).
///
/// 게임 씬이 로드될 때 PlayerContext.Awake가 이걸 읽어 occupied를 정한다.
/// RewardDistributor.Start가 씬 로드 즉시 PlayerContext.Occupied에 위습을 뿌리므로
/// 그보다 먼저(Awake) 정해져 있어야 한다.
///
/// Active가 false(= 러너 없음 = 싱글)면 아무도 이걸 안 읽는다 — 씬에 직렬화된 occupied 그대로.
/// </summary>
public static class MatchConfig
{
    static readonly HashSet<int> occupiedSlots = new HashSet<int>();
    static readonly Dictionary<int, PlayerSaveData> submittedSaves = new Dictionary<int, PlayerSaveData>();

    /// <summary>호스트: 그 슬롯 친구가 대기실에서 제출한 세이브(없으면 null). PersistentSave.Awake가 읽는다.</summary>
    public static PlayerSaveData SubmittedSave(int slot) => submittedSaves.TryGetValue(slot, out PlayerSaveData data) ? data : null;
    public static void SetSubmittedSave(int slot, PlayerSaveData data) => submittedSaves[slot] = data;

    public static bool Active { get; set; }

    /// <summary>대기실에서 호스트가 고른 난이도(NetGameState가 옮겨 둔다). DifficultyManager.Awake가 읽는다.</summary>
    public static DifficultyMode? Difficulty { get; set; }

    public static IReadOnlyCollection<int> OccupiedSlots => occupiedSlots;

    public static bool IsOccupied(int slot) => occupiedSlots.Contains(slot);

    public static void AddSlot(int slot) => occupiedSlots.Add(slot);

    public static void RemoveSlot(int slot) => occupiedSlots.Remove(slot);

    public static void Reset()
    {
        Active = false;
        Difficulty = null;
        occupiedSlots.Clear();
        submittedSaves.Clear();
    }
}
