using UnityEngine;

/// <summary>
/// 30R 보스 파칭코(사장님 10-14 신규) — 연출 길 한 판. 호스트(솔로)가 결과를 **먼저** 정하고 길을 뽑아 전원에게 같은 값을 보낸다(실제 파칭코 방식).
/// 정본 시안: https://claude.ai/artifact/Y1Lwz6UjzAzchRANw58ACR 의 plan() — 가중치·분기를 그대로 옮겼다. 값은 전부 <see cref="PachinkoSettings"/>(사장님 답 대기).
/// </summary>
public enum PachinkoHold : byte { White, Blue, Green, Red, Gold }
public enum PachinkoBg : byte { None, Green, Red, Gold }
public enum PachinkoCut : byte { None, Blue, Red, Gold }

/// <summary>사장님이 정할 값(사장님 10-14 재확정: 777 5% · 세 같은 숫자 25% · 리치 꽝 40% · 꽝 30%, 플레이어마다 따로 뽑는다). 바꿀 땐 여기만.</summary>
/// <summary>영상형 파칭코 테마 하나의 값(PachinkoSettings.VideoThemes). 영상 시각은 모두 영상 안의 초.</summary>
public sealed class VideoThemeSpec
{
    public int theme; public string key, title, voteName, voteDesc, art, videoFile;
    public float rouletteStart, rouletteStop, pushAt, videoEnd;
    public bool coverSubtitles;   // 영상에 구워진 자막이 아래에 있어 룰렛 뒤에 어두운 띠를 깐다
    public Color cardColor, fillColor, barColor, pickColor;
}

public static class PachinkoSettings
{
    /// <summary>이 라운드 보스를 잡는 순간 한 번(판당 1회).</summary>
    public const int TriggerRound = 30;
    /// <summary>777 = 랜덤유닛 직접 지급.</summary>
    public const float JackpotChance = 0.05f;
    /// <summary>7을 뺀 세 같은 숫자 = 전설·히든 위습.</summary>
    public const float WinChance = 0.25f;
    /// <summary>리치(3▢3)만 걸렸다 빗나감 = 희귀함 위습. 나머지(1-JackpotChance-WinChance-이 값 = 30%)는 그냥 꽝 = 특별함 위습.</summary>
    public const float ReachMissChance = 0.40f;
    /// <summary>자기 PUSH 시간(초) — 각자 자기 버튼, 이 안에 한 번 누르면 자기 줄만 정지, 안 누르면 끝에 자동.</summary>
    public const float PushSeconds = 5f;
    public const int WispCountPerPlayer = 1;
    /// <summary>결과 화면이 떠 있는 시간.</summary>
    public const float WinResultSeconds = 5.5f;
    public const float JackpotExtraSeconds = 2.5f;
    public const float ReviveExtraSeconds = 2.0f;
    public const float MissResultSeconds = 2.0f;
    /// <summary>오버레이가 없을 때(임시 경로) 로그만 찍고 결과까지 가는 시간.</summary>
    public const float NoOverlayDelay = 0.5f;
    /// <summary>Re:ゼロ 테마(사장님 10-14): 룰렛 배경 영상 길이 — 꽝 35초 · 당첨 39.1초(마지막 「見事」 직후 당첨 표시). 영상 끝에서 결과.</summary>
    public const float RzSpinMissSeconds = 35f, RzSpinWinSeconds = 39.1f;
    /// <summary>Re:ゼロ 꽝: 화면 깨짐 0.8초 + 꽝 영상 6.8초 + 여유.</summary>
    public const float RzMissResultSeconds = 0.8f + 6.8f + 1.0f;

    // ───── 영상형 테마(사장님 10-16): 영상 한 편을 처음부터 재생하고 그 위에 룰렛을 얹는다. 테마별 값은 VideoThemeSpec 한 덩어리(아래 VideoThemes). ─────
    // 흐름: 영상 0:00 → rouletteStart 룰렛 회전 → rouletteStop 숫자 정지·판정 → (같은 숫자 셋 = 크리티컬 보너스) 영상 계속 → pushAt PUSH(PushSeconds) → videoEnd 영상 끝. 꽝이면 rouletteStop에서 끝.
    /// <summary>
    /// 보상(사장님 10-16 확정, 같은 숫자 셋 = 크리티컬 · 777 포함): PUSH 성공 = 제일 좋은 보상(777 보상, 랜덤유닛) · PUSH 실패 = 당첨 보상(전설·히든 위습).
    /// 꽝은 기존대로(리치 꽝 = 희귀함 위습 · 그냥 꽝 = 특별함 위습). 확률표(같은 숫자 30%)는 그대로라 PUSH 성공 시 대당첨이 30%가 된다.
    /// false로 두면 옛 방식(777만 랜덤유닛, 그 밖의 같은 숫자는 당첨, PUSH 실패는 한 단계 낮은 리치꽝 보상)이다.
    /// </summary>
    public const bool VideoSuccessIsTopReward = true;
    /// <summary>영상형 테마 PUSH 성공 확률(사장님 10-16: 5초 안에 눌러도 51%만 성공, 안 누르면 무조건 실패). 호스트가 plan을 굴릴 때 미리 정해 plan.pushWin에 넣어 전원이 같은 결과를 본다.</summary>
    public const float VideoPushSuccessChance = 0.51f;
    /// <summary>영상형 테마 결과 화면이 떠 있는 시간(크리티컬 PUSH 성공 / 실패 · 꽝).</summary>
    public const float VideoSuccessResultSeconds = 5.5f, VideoFailResultSeconds = 3.0f, VideoMissResultSeconds = 3.0f;
    /// <summary>한 슬롯의 파칭코가 이 값(초)보다 더 지체되면 호스트가 그 슬롯을 강제로 끝낸다(끊김·멈춤으로 전원 정지가 길어지는 것 방지).</summary>
    public const float StallSafetySeconds = 25f;

    /// <summary>theme 값(2~)에 대응하는 영상형 테마. 영상형이 아니면 null.</summary>
    public static VideoThemeSpec VideoTheme(int theme)
    {
        foreach (VideoThemeSpec v in VideoThemes) if (v.theme == theme) return v;
        return null;
    }
    public static VideoThemeSpec VideoThemeByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        key = key.ToLowerInvariant();
        foreach (VideoThemeSpec v in VideoThemes) if (key.StartsWith(v.key.Substring(0, 2))) return v;
        return null;
    }

    public static readonly VideoThemeSpec[] VideoThemes =
    {
        new VideoThemeSpec
        {
            theme = 2, key = "fire", title = "炎炎ノ消防隊", voteName = "불꽃", voteDesc = "영상 룰렛 · 같은 숫자면 크리티컬 · PUSH", art = "炎",
            videoFile = "fireforce_play.mp4", rouletteStart = 90f, rouletteStop = 106f, pushAt = 220f, videoEnd = 225f, coverSubtitles = true,
            cardColor = new Color(0.5f, 0.2f, 0.02f, 0.95f), fillColor = new Color(1f, 0.8f, 0.2f, 0.55f), barColor = new Color(1f, 0.6f, 0.1f), pickColor = new Color(1f, 0.75f, 0.25f),
        },
        new VideoThemeSpec
        {
            theme = 3, key = "slime", title = "転生したらスライムだった件", voteName = "전생슬", voteDesc = "영상 룰렛 · 같은 숫자면 크리티컬 · PUSH", art = "スライム",
            videoFile = "tensura_play.mp4", rouletteStart = 70f, rouletteStop = 80f, pushAt = 221.19f - 5f, videoEnd = 221.19f, coverSubtitles = false,
            cardColor = new Color(0.04f, 0.3f, 0.5f, 0.95f), fillColor = new Color(0.4f, 0.85f, 1f, 0.55f), barColor = new Color(0.2f, 0.7f, 0.95f), pickColor = new Color(0.55f, 0.88f, 1f),
        },
    };
}

[System.Serializable]
public struct PachinkoPlan
{
    public bool win, reach, revive, jackpot;
    public byte left, center, right;   // 1~9
    public PachinkoHold hold;
    public byte yokoku;                // 0~3 (継続 ×N+1)
    public bool pushWin;               // 영상형 테마: 크리티컬일 때 PUSH를 누르면 성공하는 판인가(호스트가 미리 정함, 안 누르면 어차피 실패)
    public byte theme;                 // 0=東京喰種 1=Re:ゼロ 2=불꽃 소방대 3=전생슬(테마 투표 결과, PachinkoOverlay.Theme)
    public PachinkoBg bg;
    public PachinkoCut cut;

    /// <summary>네트워크용 25비트 포장.</summary>
    public int Pack() =>
        (win ? 1 : 0) | (reach ? 2 : 0) | (revive ? 4 : 0) | (jackpot ? 1 << 25 : 0) | (pushWin ? 1 << 28 : 0) | (theme & 3) << 26 |
        (left & 15) << 3 | (center & 15) << 7 | (right & 15) << 11 |
        ((int)hold & 7) << 15 | (yokoku & 3) << 18 | ((int)bg & 3) << 20 | ((int)cut & 7) << 22;

    public static PachinkoPlan Unpack(int v) => new PachinkoPlan
    {
        win = (v & 1) != 0, reach = (v & 2) != 0, revive = (v & 4) != 0, jackpot = (v & (1 << 25)) != 0, pushWin = (v & (1 << 28)) != 0, theme = (byte)((v >> 26) & 3),
        left = (byte)((v >> 3) & 15), center = (byte)((v >> 7) & 15), right = (byte)((v >> 11) & 15),
        hold = (PachinkoHold)((v >> 15) & 7), yokoku = (byte)((v >> 18) & 3), bg = (PachinkoBg)((v >> 20) & 3), cut = (PachinkoCut)((v >> 22) & 7),
    };

    public override string ToString() =>
        $"{(jackpot ? "777대당첨" : win ? "당첨" : reach ? "리치꽝" : "꽝")}{(revive ? "(부활)" : "")} {left}{center}{right} 리치 {reach} 보류 {hold} 예고 {yokoku} 배경 {bg} 컷인 {cut}";

    /// <summary>
    /// 시안 plan()의 C# 이식(10-14 사양: 777 / 세 같은 숫자(7 제외) / 리치 꽝 / 그냥 꽝 네 결과).
    /// mode: null=확률 그대로, "jackpot"=777, "win"=당첨(강한 금 길), "revive"=꽝인 척 부활 당첨, "reach"=리치 꽝(빨강까지 갔다가), "miss"=그냥 꽝.
    /// </summary>
    /// <summary>개발·시험용: -pachinkoPush success|fail로 PUSH 성공/실패를 고정한다(null이면 확률).</summary>
    public static bool? ForcePush;

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
        p.pushWin = ForcePush ?? (Random.value < PachinkoSettings.VideoPushSuccessChance);

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
        VideoThemeSpec vt = PachinkoSettings.VideoTheme(theme);
        if (vt != null) return win ? vt.pushAt : vt.rouletteStop;   // 꽝은 판정 시각에 끝(PUSH 없음)
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

    /// <summary>이 plan의 PUSH 창 길이(초). 불꽃 꽝은 PUSH가 없다.</summary>
    public float PushSeconds() => PachinkoSettings.VideoTheme(theme) != null && !win ? 0f : PachinkoSettings.PushSeconds;

    /// <summary>PUSH 뒤(또는 시간 끝) 결과 화면이 떠 있는 시간.</summary>
    public float ResultSeconds(bool pushed = true) => PachinkoSettings.VideoTheme(theme) != null ? (win ? (pushed ? PachinkoSettings.VideoSuccessResultSeconds : PachinkoSettings.VideoFailResultSeconds) : PachinkoSettings.VideoMissResultSeconds) : theme == 1 && !win ? PachinkoSettings.RzMissResultSeconds : win ? PachinkoSettings.WinResultSeconds + (revive ? PachinkoSettings.ReviveExtraSeconds : 0f) + (jackpot ? PachinkoSettings.JackpotExtraSeconds : 0f) : PachinkoSettings.MissResultSeconds;
}
