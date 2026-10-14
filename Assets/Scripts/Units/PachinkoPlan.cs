using UnityEngine;

/// <summary>
/// 30R 보스 파칭코(사장님 10-14 신규) — 연출 길 한 판. 호스트(솔로)가 결과를 **먼저** 정하고 길을 뽑아 전원에게 같은 값을 보낸다(실제 파칭코 방식).
/// 정본 시안: https://claude.ai/artifact/Y1Lwz6UjzAzchRANw58ACR 의 plan() — 가중치·분기를 그대로 옮겼다. 값은 전부 <see cref="PachinkoSettings"/>(사장님 답 대기).
/// </summary>
public enum PachinkoHold : byte { White, Blue, Green, Red, Gold }
public enum PachinkoBg : byte { None, Green, Red, Gold }
public enum PachinkoCut : byte { None, Blue, Red, Gold }

/// <summary>사장님이 정할 값(답 대기 — 기본은 PM 추천). 바꿀 땐 여기만.</summary>
public static class PachinkoSettings
{
    /// <summary>이 라운드 보스를 잡는 순간 한 번(판당 1회).</summary>
    public const int TriggerRound = 30;
    /// <summary>당첨 확률.</summary>
    public const float WinChance = 1f / 81f;
    /// <summary>꽝 중 리치가 걸리는 비율.</summary>
    public const float ReachChanceWhenMiss = 1f / 3f;
    /// <summary>PUSH 버튼 시간(초).</summary>
    public const float PushSeconds = 4f;
    /// <summary>true면 전원, false면 보스를 잡은 사람(처치자)만. 지금은 전원만 구현.</summary>
    public const bool RewardAllPlayers = true;
    /// <summary>당첨 보상: 전설·히든 위습 몇 기씩.</summary>
    public const int WispCountPerPlayer = 1;
    /// <summary>결과 화면(대당첨/꽝)이 떠 있는 시간 — 지나면 정지를 풀고 보상을 준다.</summary>
    public const float WinResultSeconds = 5.5f;
    public const float ReviveExtraSeconds = 2.0f;
    public const float MissResultSeconds = 2.6f;
    /// <summary>오버레이가 없을 때(임시 경로) 로그만 찍고 결과까지 가는 시간.</summary>
    public const float NoOverlayDelay = 0.5f;
}

[System.Serializable]
public struct PachinkoPlan
{
    public bool win, reach, revive;
    public byte left, center, right;   // 1~9
    public PachinkoHold hold;
    public byte yokoku;                // 0~3 (継続 ×N+1)
    public PachinkoBg bg;
    public PachinkoCut cut;

    /// <summary>네트워크용 25비트 포장.</summary>
    public int Pack() =>
        (win ? 1 : 0) | (reach ? 2 : 0) | (revive ? 4 : 0) |
        (left & 15) << 3 | (center & 15) << 7 | (right & 15) << 11 |
        ((int)hold & 7) << 15 | (yokoku & 3) << 18 | ((int)bg & 3) << 20 | ((int)cut & 7) << 22;

    public static PachinkoPlan Unpack(int v) => new PachinkoPlan
    {
        win = (v & 1) != 0, reach = (v & 2) != 0, revive = (v & 4) != 0,
        left = (byte)((v >> 3) & 15), center = (byte)((v >> 7) & 15), right = (byte)((v >> 11) & 15),
        hold = (PachinkoHold)((v >> 15) & 7), yokoku = (byte)((v >> 18) & 3), bg = (PachinkoBg)((v >> 20) & 3), cut = (PachinkoCut)((v >> 22) & 7),
    };

    public override string ToString() =>
        $"{(win ? "당첨" : "꽝")}{(revive ? "(부활)" : "")} {left}{center}{right} 리치 {reach} 보류 {hold} 예고 {yokoku} 배경 {bg} 컷인 {cut}";

    /// <summary>
    /// 시안 plan()의 C# 이식. mode: null=확률 그대로, "win"=당첨(강한 금 길), "revive"=꽝인 척 부활, "miss"=꽝(짧은 리치), "reach"=빨강까지 가서 꽝.
    /// </summary>
    public static PachinkoPlan Roll(string mode = null)
    {
        var p = new PachinkoPlan();
        int n = Random.Range(1, 10);
        int other; do { other = Random.Range(1, 10); } while (other == n);
        bool win, reach;
        if (mode == null) { win = Random.value < PachinkoSettings.WinChance; reach = win || Random.value < PachinkoSettings.ReachChanceWhenMiss; }
        else { win = mode == "win" || mode == "revive"; reach = mode != "none"; }
        p.win = win; p.reach = reach; p.revive = false;
        p.left = (byte)n;
        p.right = (byte)(reach ? n : other);
        p.center = (byte)(win ? n : (reach ? (n % 9) + 1 : Random.Range(1, 10)));
        if (!reach && p.left == p.right) p.right = (byte)other;

        if (win)
        {
            p.hold = Pick(PachinkoHold.Blue, 15, PachinkoHold.Green, 25, PachinkoHold.Red, 40, PachinkoHold.Gold, 20);
            p.yokoku = (byte)PickInt(0, 15, 1, 20, 2, 30, 3, 35);
            p.bg = Pick(PachinkoBg.Green, 20, PachinkoBg.Red, 50, PachinkoBg.Gold, 30);
            p.cut = Pick(PachinkoCut.Blue, 20, PachinkoCut.Red, 45, PachinkoCut.Gold, 35);
            p.revive = p.cut == PachinkoCut.Blue && Random.value < 0.7f;
        }
        else if (reach)
        {
            p.hold = Pick(PachinkoHold.White, 30, PachinkoHold.Blue, 40, PachinkoHold.Green, 25, PachinkoHold.Red, 5);
            p.yokoku = (byte)PickInt(0, 55, 1, 30, 2, 12, 3, 3);
            p.bg = Pick(PachinkoBg.None, 55, PachinkoBg.Green, 35, PachinkoBg.Red, 10);
            p.cut = Pick(PachinkoCut.Blue, 85, PachinkoCut.Red, 15);
        }
        else
        {
            p.hold = Pick(PachinkoHold.White, 80, PachinkoHold.Blue, 20);
            p.yokoku = 0; p.bg = PachinkoBg.None; p.cut = PachinkoCut.None;
        }

        switch (mode)
        {
            case "win": p.hold = PachinkoHold.Red; p.yokoku = 3; p.bg = PachinkoBg.Gold; p.cut = PachinkoCut.Gold; p.revive = false; break;
            case "revive": p.hold = PachinkoHold.Blue; p.yokoku = 1; p.bg = PachinkoBg.Green; p.cut = PachinkoCut.Blue; p.revive = true; break;
            case "reach": p.hold = PachinkoHold.Red; p.yokoku = 2; p.bg = PachinkoBg.Red; p.cut = PachinkoCut.Red; break;
            case "miss": p.hold = PachinkoHold.White; p.yokoku = 0; p.bg = PachinkoBg.None; p.cut = PachinkoCut.Blue; break;
        }
        return p;
    }

    static T Pick<T>(T a, int wa, T b, int wb, T c = default, int wc = 0, T d = default, int wd = 0)
    {
        float r = Random.value * (wa + wb + wc + wd);
        if ((r -= wa) <= 0f) return a;
        if ((r -= wb) <= 0f) return b;
        if (wc > 0 && (r -= wc) <= 0f) return c;
        if (wd > 0) return d;
        return a;
    }

    static int PickInt(params int[] pairs)
    {
        float total = 0f; for (int i = 1; i < pairs.Length; i += 2) total += pairs[i];
        float r = Random.value * total;
        for (int i = 0; i < pairs.Length; i += 2) { r -= pairs[i + 1]; if (r <= 0f) return pairs[i]; }
        return pairs[0];
    }

    /// <summary>
    /// PUSH 창이 열리기까지의 연출 시간(초) — 시안 타이밍을 그대로 옮긴 값. 오버레이와 호스트가 같은 길이를 쓴다(오버레이는 이 값에 맞춰 PUSH를 띄울 것).
    /// 보류 0.9 + 세 줄 정지 2.4 + (리치면: 리치 콜 1.3 + 예고 N×1.05 + SP 배경 단계×0.75 + 슬롯 1.8(빨강 이상) + 컷인 1.6).
    /// </summary>
    public float PreludeSeconds()
    {
        float t = 0.9f + 2.4f;
        if (!reach) return t + 0.4f;
        t += 1.3f + yokoku * 1.05f;
        if (bg != PachinkoBg.None)
        {
            t += ((int)bg) * 0.75f;                 // Green=1 · Red=2 · Gold=3 단계
            if (bg != PachinkoBg.Green) t += 1.8f;  // 사장님 애니메이션 자리
        }
        if (cut != PachinkoCut.None) t += 1.6f;
        return t;
    }

    /// <summary>PUSH 뒤(또는 시간 끝) 결과 화면이 떠 있는 시간.</summary>
    public float ResultSeconds() => win ? PachinkoSettings.WinResultSeconds + (revive ? PachinkoSettings.ReviveExtraSeconds : 0f) : PachinkoSettings.MissResultSeconds;
}
