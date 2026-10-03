using System.IO;
using System.Text;
using UnityEngine;

// 세이브 코드 쓰는 쪽·읽는 쪽의 게임 연결부(코드 문자열 자체는 SaveCode). 원작 SavePlayer 끝(j:14337~14352)·OnChatLoad(j:115198~115225)에 대응.
//   저장: 판 끝에 코드를 알림으로 보여 주고(원작은 채팅 줄) 클립보드에 복사하고 Save/codes/<닉>_<클리어>.txt에 원작 Preload 파일과 같은 줄을 쓴다.
//   불러오기: 열쇠(닉네임)가 맞는 코드를 풀어 내 player_0.json과 합친다(클리어 횟수 큰 쪽).
public static class SaveCodeService
{
    public const float NoticeSeconds = 60f;

    static string CodesDir => Path.Combine(Path.GetDirectoryName(PersistentSave.PathFor(0)) ?? "", "codes");

    /// <summary>판 끝 저장 직후 이 PC 주인에게 코드를 보여 준다. slot = 이 PC의 플레이어 번호(알림이 그 사람에게만 뜬다).</summary>
    public static string PresentAfterRun(int slot, PlayerSaveData data)
    {
        string nick = PlayerDisplayName.RawNickname(slot);
        string code = SaveCode.Encode(nick, data);
        PlayerNotification.Show(slot, $"<color=#FFD700>세이브 코드 (닉네임 「{(nick.Length == 0 ? "(없음)" : nick)}」 전용)</color>", NoticeSeconds);
        PlayerNotification.Show(slot, $"<color=#00FFFF>{code}</color>", NoticeSeconds);
        try
        {
            GUIUtility.systemCopyBuffer = code;
            PlayerNotification.Show(slot, "코드를 클립보드에 복사했습니다. 다른 PC에서는 「세이브 코드 불러오기」 또는 채팅 -load 코드 로 이어 하세요.", NoticeSeconds);
        }
        catch (System.Exception e) { Debug.LogWarning($"SaveCode: 클립보드 복사 실패 {e.Message}"); }
        WriteCodeFile(nick, code, data);
        Debug.Log($"[세이브코드] 슬롯 {slot} 닉 「{nick}」 클리어 {data.cumulativeClearCount} 누적 {data.cumulativePlayPoint} 베스트 {data.bestRunPoint} → {code}");
        return code;
    }

    // 원작 PreloadGenEnd("ORD11\\ord11_<닉>_<횟수>.txt")의 내용 — 닉네임 / 클리어 회수 / 누적 포인트 / -load 코드.
    static void WriteCodeFile(string nick, string code, PlayerSaveData data)
    {
        try
        {
            Directory.CreateDirectory(CodesDir);
            string safe = nick;
            foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            if (safe.Length == 0) safe = "noname";
            var sb = new StringBuilder();
            sb.AppendLine(" 닉네임 " + nick);
            sb.AppendLine(" 클리어 회수 = " + data.cumulativeClearCount);
            sb.AppendLine(" 누적 플레이 포인트 = " + data.cumulativePlayPoint + "점");
            sb.AppendLine(" 베스트 점수 = " + data.bestRunPoint + "점");
            sb.AppendLine("-load " + code);
            File.WriteAllText(Path.Combine(CodesDir, $"{safe}_{data.cumulativeClearCount}.txt"), sb.ToString());
        }
        catch (System.Exception e) { Debug.LogWarning($"SaveCode: 코드 파일 쓰기 실패 {e.Message}"); }
    }

    /// <summary>
    /// 불러오기. nickname = 이 PC에서 쓰는 닉네임(코드 열쇠). 성공이면 파일(player_0.json)을 병합해 쓰고 true.
    /// onApplied: 싱글 판 중이면 메모리의 세이브도 같이 바꾸려는 훅(없으면 null).
    /// </summary>
    public static bool Load(string nickname, string code, out string message, System.Action<PlayerSaveData> onApplied = null)
    {
        if (!SaveCode.TryDecode(nickname, code, out PlayerSaveData fromCode, out _, out string reason))
        {
            message = "로드 실패! " + reason;
            return false;
        }

        PlayerSaveData file = PersistentSave.ReadOwnSave();
        bool codeWins = SaveCode.CodeWins(file, fromCode);
        PlayerSaveData result = codeWins ? fromCode : file;
        if (codeWins)
        {
            try { PersistentSave.WriteOwnSave(fromCode); }
            catch (System.Exception e) { message = $"로드 실패! 파일을 쓰지 못했습니다({e.Message})"; return false; }
            onApplied?.Invoke(fromCode);
        }

        message = $"로드 성공! 클리어 회수 = {result.cumulativeClearCount} · 누적 플레이 점수 = {result.cumulativePlayPoint} · 베스트 점수 = {result.bestRunPoint}"
                  + (codeWins ? "" : "\n(이 PC의 기록이 같거나 더 커서 그대로 둡니다)");
        Debug.Log($"[세이브코드] 불러오기 {(codeWins ? "코드 채택" : "파일 유지")}: 코드 클리어 {fromCode.cumulativeClearCount}/누적 {fromCode.cumulativePlayPoint} · 파일 클리어 {file.cumulativeClearCount}/누적 {file.cumulativePlayPoint}");
        return true;
    }
}
