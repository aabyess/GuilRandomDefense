using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스토리 기록(S2 2.323 「-스토리」, j:49053~49086 CIz·CI0) — 스토리마다 걸린 시간 kJ · MVP kF · 퍼센트 kK.
/// 호스트·싱글에서 StoryManager.Finish가 깬 순간 Record를 부른다. 판이 바뀌면(StoryManager 인스턴스가 바뀌면) 저절로 비운다.
/// MVP = 가장 많이 때린 사람(동률이면 번호 큰 쪽 — RewardDistributor.GrantStoryContribution과 같은 규칙), % = 100 × 그 피해 / 최대 체력.
/// </summary>
public static class StoryRecords
{
    public struct Record
    {
        public float seconds;
        public int mvpPlayer;      // -1 = 기여 기록 없음
        public float mvpPercent;
    }

    static readonly Dictionary<int, Record> records = new Dictionary<int, Record>();
    static StoryManager owner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { records.Clear(); owner = null; }

    static void SyncOwner()
    {
        StoryManager now = StoryManager.Instance;
        if (now != null && !ReferenceEquals(now, owner)) { records.Clear(); owner = now; }
    }

    /// <summary>스토리 index(StoryManager 순서, 0부터)를 깼다. dead는 파괴된 스토리 적일 수 있다(관리 필드는 읽힌다).</summary>
    public static void Add(int index, float seconds, EnemyDummy dead)
    {
        if (index < 0) return;
        SyncOwner();
        var r = new Record { seconds = Mathf.Max(0f, seconds), mvpPlayer = -1 };
        if (!ReferenceEquals(dead, null) && dead.ContributionDamage != null)
        {
            float[] damage = dead.ContributionDamage;
            float maxHp = Mathf.Max(1f, dead.MaxHp);
            float best = -1f;
            foreach (PlayerContext context in PlayerContext.Occupied)
            {
                int p = context.PlayerId;
                if (p < 0 || p >= damage.Length) continue;
                if (damage[p] >= best) { best = damage[p]; r.mvpPlayer = p; }
            }
            if (r.mvpPlayer >= 0) r.mvpPercent = 100f * best / maxHp;
        }
        records[index] = r;
    }

    public static bool TryGet(int index, out Record record)
    {
        SyncOwner();
        return records.TryGetValue(index, out record);
    }
}
