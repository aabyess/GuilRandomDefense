using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 에디터 메뉴가 플레이 모드에서 실행되는 걸 막는다.
/// 플레이 중 씬 변경은 종료 시 전부 되돌아가는데, 도구는 성공 로그를 남기기 때문에
/// "분명 실행했는데 반영이 안 된다"로 이어진다. 실제로 여러 번 그랬다.
///
/// 대화상자도 여기로 모은다(2026-09-13) — Claude가 메뉴를 원격·배치모드로 돌릴 때(ClaudeCommands)
/// 확인창이 뜨면 누를 사람이 없어 멈춘다. 무인 실행 중엔 창 대신 기록만 남기고 「확인」으로 진행한다.
/// </summary>
public static class EditorGuards
{
    /// <summary>ClaudeCommands가 명령을 도는 동안 켠다.</summary>
    public static bool Unattended;

    /// <summary>무인 실행 중 뜰 뻔한 대화상자 내용 — 명령 결과에 그대로 싣는다.</summary>
    public static readonly StringBuilder Transcript = new StringBuilder();

    public static bool IsUnattended => Unattended || Application.isBatchMode;

    public static bool Dialog(string title, string message, string ok, string cancel = null)
    {
        if (IsUnattended)
        {
            Transcript.AppendLine($"[{title}] {message}");
            return true;
        }

        return cancel == null
            ? EditorUtility.DisplayDialog(title, message, ok)
            : EditorUtility.DisplayDialog(title, message, ok, cancel);
    }

    public static bool RequireEditMode(string title)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;

        Dialog(title,
            "플레이 중에는 실행할 수 없습니다.\n\n" +
            "플레이 중 씬 변경은 종료할 때 모두 되돌아갑니다.\n" +
            "▶ 버튼을 눌러 플레이를 끄고 다시 실행하세요.", "확인");
        return false;
    }
}
