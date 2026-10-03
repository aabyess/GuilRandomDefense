using System.IO;
using System.Text;
using UnityEngine;

// 세이브 코드 점검 — 순수 함수는 편집 모드에서도 된다(call:SaveCodeProbe.Run), 판 안 표시는 call:SaveCodeProbe.PlayFinish(gameshot).
static class SaveCodeProbe
{
    static string Run()
    {
        var sb = new StringBuilder();
        string saved = PersistentSave.SaveRootOverride;
        string tmp = Path.Combine(Path.GetTempPath(), "grd_savecode_probe_" + System.Guid.NewGuid().ToString("N").Substring(0, 6));
        PersistentSave.SaveRootOverride = tmp;
        try
        {
            var d = new PlayerSaveData { cumulativePlayPoint = 1234, cumulativeClearCount = 37, bestRunPoint = 13, playerLevel = 2 };
            string code = SaveCode.Encode("길", d);
            sb.AppendLine($"코드 예시(닉 「길」, 누적1234·클리어37·베스트13·레벨2): {code}  (길이 {code.Length})");
            string big = SaveCode.Encode("MaxLen_12abc", new PlayerSaveData { cumulativePlayPoint = 999999, cumulativeClearCount = 300, bestRunPoint = 99999, playerLevel = 30 });
            sb.AppendLine($"큰 값 예시: {big}  (길이 {big.Length})");
            sb.AppendLine($"0 값 예시: {SaveCode.Encode("", new PlayerSaveData())}");

            sb.AppendLine(Check("같은 닉 해독", SaveCode.TryDecode("길", code, out var r, out var f, out var m) && r.cumulativePlayPoint == 1234 && r.cumulativeClearCount == 37 && r.bestRunPoint == 13 && r.playerLevel == 2, $"{f} {m}"));
            sb.AppendLine(Check("소문자·공백·O/I 혼동 입력 허용", SaveCode.TryDecode("길", "  " + code.ToLowerInvariant().Replace("0", "o") + " ", out r, out f, out m) && r.cumulativeClearCount == 37, $"{f} {m}"));
            sb.AppendLine(Check("배틀태그 '#' 꼬리는 열쇠에서 뺀다", SaveCode.TryDecode("길#1234", code, out r, out f, out m), $"{f} {m}"));
            bool ok = SaveCode.TryDecode("다른닉", code, out r, out f, out m);
            sb.AppendLine(Check("다른 닉네임 → 실패(불일치)", !ok && f == SaveCode.Failure.NicknameMismatch, $"{f} 「{m}」"));
            ok = SaveCode.TryDecode("길", "GRD1-" + Flip(code.Substring(5)), out r, out f, out m);
            sb.AppendLine(Check("글자 하나 틀림 → 실패(손상)", !ok && f == SaveCode.Failure.Corrupted, $"{f} 「{m}」"));
            ok = SaveCode.TryDecode("길", code.Substring(0, code.Length - 3), out r, out f, out m);
            sb.AppendLine(Check("잘린 코드 → 실패", !ok, $"{f} 「{m}」"));
            ok = SaveCode.TryDecode("길", "hello world", out r, out f, out m);
            sb.AppendLine(Check("엉뚱한 글 → 실패(형식)", !ok && f == SaveCode.Failure.Malformed, $"{f} 「{m}」"));
            int flips = 0, caught = 0;
            for (int i = 5; i < code.Length; i++)
            {
                if (code[i] == '-') continue;
                string alt = code.Substring(0, i) + (code[i] == 'A' ? 'B' : 'A') + code.Substring(i + 1);
                flips++;
                if (!SaveCode.TryDecode("길", alt, out r, out f, out m) || r.cumulativeClearCount != 37 || r.cumulativePlayPoint != 1234) caught++;
            }
            sb.AppendLine(Check($"글자 하나씩 바꾼 {flips}가지 모두 걸러짐", caught == flips, $"{caught}/{flips}"));

            // 파일 병합 규칙
            PersistentSave.WriteOwnSave(new PlayerSaveData { cumulativeClearCount = 50, cumulativePlayPoint = 500 });
            bool loaded = SaveCodeService.Load("길", code, out string msg);
            sb.AppendLine(Check("파일(클리어50) > 코드(37) → 파일 유지", loaded && PersistentSave.ReadOwnSave().cumulativeClearCount == 50, msg.Replace("\n", " ")));
            PersistentSave.WriteOwnSave(new PlayerSaveData { cumulativeClearCount = 10, cumulativePlayPoint = 9999 });
            loaded = SaveCodeService.Load("길", code, out msg);
            var after = PersistentSave.ReadOwnSave();
            sb.AppendLine(Check("파일(클리어10) < 코드(37) → 코드 채택", loaded && after.cumulativeClearCount == 37 && after.cumulativePlayPoint == 1234, msg.Replace("\n", " ")));
            loaded = SaveCodeService.Load("길님", code, out msg);
            sb.AppendLine(Check("닉네임 다르게 불러오기 → 실패 + 파일 안 바뀜", !loaded && PersistentSave.ReadOwnSave().cumulativeClearCount == 37, msg));
        }
        finally { PersistentSave.SaveRootOverride = saved; }
        return sb.ToString();
    }

    static string Flip(string s)
    {
        char[] a = s.ToCharArray();
        for (int i = 0; i < a.Length; i++) if (a[i] != '-') { a[i] = a[i] == 'Z' ? 'Y' : 'Z'; break; }
        return new string(a);
    }

    static string Check(string label, bool ok, string detail) => $"  {(ok ? "✅" : "❌")} {label} — {detail}";

    // 판 안: 로컬 플레이어의 FinishRun을 불러 코드 알림·클립보드·파일을 실제 경로로 만든다. 판 끝 저장은 파일을 쓰므로 임시 폴더로 돌린다.
    static string PlayFinish()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        PlayerContext me = PlayerContext.Get(0);
        string tmp = Path.Combine(Path.GetTempPath(), "grd_savecode_probe_play");
        PersistentSave.SaveRootOverride = tmp;
        NetPlayer.SaveNickname("길동이");
        me.PersistentSave.Data.cumulativeClearCount = 36;
        me.PersistentSave.Data.cumulativePlayPoint = 400;
        me.PersistentSave.AddSessionPoints(7);
        me.PersistentSave.FinishRun(true);
        string[] files = Directory.Exists(Path.Combine(tmp, "codes")) ? Directory.GetFiles(Path.Combine(tmp, "codes")) : new string[0];
        var sb = new StringBuilder();
        // 채팅 -load 경로(GameChatBox.HandleLoadCommand): 같은 닉(성공) · 다른 닉으로 바꾼 뒤(실패)
        var handle = typeof(GameChatBox).GetMethod("HandleLoadCommand", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        string code = GUIUtility.systemCopyBuffer;
        string ok = (string)handle.Invoke(null, new object[] { me, code });
        NetPlayer.SaveNickname("딴사람");
        string bad = (string)handle.Invoke(null, new object[] { me, code });
        NetPlayer.SaveNickname("길동이");
        string broken = (string)handle.Invoke(null, new object[] { me, code.Substring(0, 7) + (code[7] == 'Z' ? 'Y' : 'Z') + code.Substring(8) });
        PlayerNotification.Show(0, "<color=#00FF00>[-load 같은 닉] " + ok.Replace("\n", " ") + "</color>", 60f);
        PlayerNotification.Show(0, "<color=#FF6060>[-load 다른 닉] " + bad + "</color>", 60f);
        PlayerNotification.Show(0, "<color=#FF6060>[-load 글자 하나 틀림] " + broken + "</color>", 60f);
        sb.AppendLine($"-load 같은 닉: {ok}\n-load 다른 닉: {bad}\n-load 손상: {broken}");
        sb.AppendLine($"클립보드: {GUIUtility.systemCopyBuffer}");
        foreach (string f in files) sb.AppendLine($"코드 파일 {Path.GetFileName(f)}:\n{File.ReadAllText(f)}");
        return sb.ToString();
    }
}
