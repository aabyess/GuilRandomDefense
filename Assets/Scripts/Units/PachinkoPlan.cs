using UnityEngine;

/// <summary>
/// 30R 보스 파칭코(사장님 10-14 신규) — 연출 길 한 판. 호스트(솔로)가 결과를 **먼저** 정하고 길을 뽑아 전원에게 같은 값을 보낸다(실제 파칭코 방식).
/// 정본 시안: https://claude.ai/artifact/Y1Lwz6UjzAzchRANw58ACR 의 plan() — 가중치·분기를 그대로 옮겼다. 값은 전부 <see cref="PachinkoSettings"/>(사장님 답 대기).
/// </summary>
public enum PachinkoHold : byte { White, Blue, Green, Red, Gold }
public enum PachinkoBg : byte { None, Green, Red, Gold }
public enum PachinkoCut : byte { None, Blue, Red, Gold }

/// <summary>사장님이 정할 값(사장님 10-14 확정: 777 5% · 세 같은 숫자 20% · 리치 꽝 35% · 꽝 40%, 플레이어마다 따로 뽑는다). 바꿀 땐 여기만.</summary>
public static class PachinkoSettings
{
    /// <summary>이 라운드 보스를 잡는 순간 한 번(판당 1회).</summary>
    public const int TriggerRound = 30;
    /// <summary>777 = 랜덤유닛 직접 지급.</summary>
    public const float JackpotChance = 0.05f;
    /// <summary>7을 뺀 세 같은 숫자 = 전설·히든 위습.</summary>
    public const float WinChance = 0.20f;
    /// <summary>리치(3▢3)만 걸렸다 빗나감 = 희귀함 위습. 나머지(1-JackpotChance-WinChance-이 값 = 40%)는 그냥 꽝 = 특별함 위습.</summary>
    public const float ReachMissChance = 0.35f;
    /// <summary>자기 PUSH 시간(초) — 각자 자기 버튼, 이 안에 한 번 누르면 자기 줄만 정지, 안 누르면 끝에 자동.</summary>
    public const float PushSeconds = 5f;
    public const int WispCountPerPlayer = 1;
    /// <summary>결과 화면이 떠 있는 시간.</summary>
    public const float WinResultSeconds = 5.5f;
    public const float JackpotExtraSeconds = 2.5f;
    public const float ReviveExtraSeconds = 2.0f;
    public const float MissResultSeconds = 2.6f;
    /// <summary>오버레이가 없을 때(임시 경로) 로그만 찍고 결과까지 가는 시간.</summary>
    public const float NoOverlayDelay = 0.5f;
    /// <summary>Re:ゼロ 테마(사장님 10-14): 룰렛 배경 영상 길이 — 꽝 35초 · 당첨 39.1초(마지막 「見事」 직후 당첨 표시). 영상 끝에서 결과.</summary>
    public const float RzSpinMissSeconds = 35f, RzSpinWinSeconds = 39.1f;
    /// <summary>Re:ゼロ 꽝: 화면 깨짐 0.8초 + 꽝 영상 6.8초 + 여유.</summary>
    public const float RzMissResultSeconds = 0.8f + 6.8f + 1.0f;
}

[System.Serializable]
public struct PachinkoPlan
{
    public bool win, reach, revive, jackpot;
    public byte left, center, right;   // 1~9
    public PachinkoHold hold;
    public byte yokoku;                // 0~3 (継続 ×N+1)
    public byte theme;                 // 0=東京喰種 1=Re:ゼロ(테마 투표 결과, PachinkoOverlay.Theme)
    public PachinkoBg bg;
    public PachinkoCut cut;

    /// <summary>네트워크용 25비트 포장.</summary>
    public int Pack() =>
        (win ? 1 : 0) | (reach ? 2 : 0) | (revive ? 4 : 0) | (jackpot ? 1 << 25 : 0) | (theme & 1) << 26 |
        (left & 15) << 3 | (center & 15) << 7 | (right & 15) << 11 |
        ((int)hold & 7) << 15 | (yokoku & 3) << 18 | ((int)bg & 3) << 20 | ((int)cut & 7) << 22;

    public static PachinkoPlan Unpack(int v) => new PachinkoPlan
    {
        win = (v & 1) != 0, reach = (v & 2) != 0, revive = (v & 4) != 0, jackpot = (v & (1 << 25)) != 0, theme = (byte)((v >> 26) & 1),
        left = (byte)((v >> 3) & 15), center = (byte)((v >> 7) & 15), right = (byte)((v >> 11) & 15),
        hold = (PachinkoHold)((v >> 15) & 7), yokoku = (byte)((v >> 18) & 3), bg = (PachinkoBg)((v >> 20) & 3), cut = (PachinkoCut)((v >> 22) & 7),
    };

    public override string ToString() =>
        $"{(jackpot ? "777대당첨" : win ? "당첨" : reach ? "리치꽝" : "꽝")}{(revive ? "(부활)" : "")} {left}{center}{right} 리치 {reach} 보류 {hold} 예고 {yokoku} 배경 {bg} 컷인 {cut}";

    /// <summary>
    /// 시안 plan()의 C# 이식(10-14 사양: 777 / 세 같은 숫자(7 제외) / 리치 꽝 / 그냥 꽝 네 결과).
    /// mode: null=확률 그대로, "jackpot"=777, "win"=당첨(강한 금 길), "revive"=꽝인 척 부활 당첨, "reach"=리치 꽝(빨강까지 갔다가), "miss"=그냥 꽝.
    /// </summary>
    public static PachinkoPlan Roll(string mode = null)
    {
        bool jackpot, win, reach;
        if (mode == null)
        {
            float r = Random.value;
            jackpot = r < PachinkoSettings.JackpotChance;
            win = !jackpot && r < PachinkoSettings.JackpotChance + PachinkoSettings.WinChance;
            reach = !jackpot && !win && r < PachinkoSettings.JackpotChance + PachinkoSettings.WinChance + PachinkoSettings.ReachMissChance;
        }
        else { jackpot = mode == "jackpot"; win = mode == "win" || mode == "revive"; reach = mode == "reach"; }
        var p = new PachinkoPlan { jackpot = jackpot, win = win || jackpot, reach = reach || win || jackpot, theme = (byte)PachinkoOverlay.Theme };

        int n;
        if (jackpot) n = 7;
        else if (win) { do { n = Random.Range(1, 10); } while (n == 7); }
        else n = Random.Range(1, 10);
        int other; do { other = Random.Range(1, 10); } while (other == n);
        p.left = (byte)n;
        p.right = (byte)(p.reach ? n : other);
        p.center = (byte)(p.win ? n : (p.reach ? (n % 9) + 1 : Random.Range(1, 10)));
        if (!p.reach && p.left == p.right) p.right = (byte)other;

        if (jackpot)
        {
            p.hold = Pick(PachinkoHold.Green, 20, PachinkoHold.Red, 40, PachinkoHold.Gold, 40);
            p.yokoku = (byte)PickInt(2, 30, 3, 70);
            p.bg = Pick(PachinkoBg.Red, 30, PachinkoBg.Gold, 70);
            p.cut = Pick(PachinkoCut.Red, 20, PachinkoCut.Gold, 80);
        }
        else if (win)
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
            case "jackpot": p.hold = PachinkoHold.Gold; p.yokoku = 3; p.bg = PachinkoBg.Gold; p.cut = PachinkoCut.Gold; break;
            case "win": p.hold = PachinkoHold.Red; p.yokoku = 3; p.bg = PachinkoBg.Gold; p.cut = PachinkoCut.Gold; p.revive = false; break;
            case "revive": p.hold = PachinkoHold.Blue; p.yokoku = 1; p.bg = PachinkoBg.Green; p.cut = PachinkoCut.Blue; p.revive = true; break;
            case "reach": p.hold = PachinkoHold.Red; p.yokoku = 2; p.bg = PachinkoBg.Red; p.cut = PachinkoCut.Red; break;
            case "miss": p.hold = PachinkoHold.White; p.yokoku = 0; p.bg = PachinkoBg.None; p.cut = PachinkoCut.None; p.reach = false; break;
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
        if (theme == 1) return (win ? PachinkoSettings.RzSpinWinSeconds : PachinkoSettings.RzSpinMissSeconds) - PachinkoSettings.PushSeconds;   // 영상 끝에서 PUSH 5초가 닫히고 결과
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
    public float ResultSeconds() => theme == 1 && !win ? PachinkoSettings.RzMissResultSeconds : win ? PachinkoSettings.WinResultSeconds + (revive ? PachinkoSettings.ReviveExtraSeconds : 0f) + (jackpot ? PachinkoSettings.JackpotExtraSeconds : 0f) : PachinkoSettings.MissResultSeconds;
}
