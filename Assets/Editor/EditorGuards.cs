using UnityEditor;
using UnityEngine;

/// <summary>
/// 에디터 메뉴가 플레이 모드에서 실행되는 걸 막는다.
/// 플레이 중 씬 변경은 종료 시 전부 되돌아가는데, 도구는 성공 로그를 남기기 때문에
/// "분명 실행했는데 반영이 안 된다"로 이어진다. 실제로 여러 번 그랬다.
/// </summary>
public static class EditorGuards
{
    public static bool RequireEditMode(string title)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;

        EditorUtility.DisplayDialog(title,
            "플레이 중에는 실행할 수 없습니다.\n\n" +
            "플레이 중 씬 변경은 종료할 때 모두 되돌아갑니다.\n" +
            "▶ 버튼을 눌러 플레이를 끄고 다시 실행하세요.", "확인");
        return false;
    }
}
