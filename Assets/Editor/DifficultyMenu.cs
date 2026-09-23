using UnityEditor;
using UnityEngine;

/// <summary>
/// 기억해 둔 난이도를 지운다 — 다음 재생 때 선택 창이 다시 뜬다.
///
/// 사장님 「난이도 왜 자꾸 뜨는거야 / 계속 뜨는 버그도 수정해봐」(2026-09-24)로
/// DifficultyManager가 고른 난이도를 기억하게 됐다. 원작은 한 판 = 한 번 고르기라
/// 판마다 묻는 게 맞지만, 우리는 시험하느라 재생을 수십 번 누른다.
/// 다른 난이도로 시험하고 싶을 때 이 메뉴를 한 번 누르면 된다.
/// </summary>
public static class DifficultyMenu
{
    const string Title = "난이도";

    [MenuItem("Tools/게임/난이도 다시 묻기")]
    static void ForgetSavedMode()
    {
        if (!PlayerPrefs.HasKey(DifficultyManager.SavedModeKey))
        {
            EditorGuards.Dialog(Title, "기억해 둔 난이도가 없습니다 — 다음 재생 때 선택 창이 뜹니다.", "확인");
            return;
        }

        int saved = PlayerPrefs.GetInt(DifficultyManager.SavedModeKey);
        string name = System.Enum.IsDefined(typeof(DifficultyMode), saved)
            ? ((DifficultyMode)saved).KoreanName()
            : $"알 수 없는 값({saved})";

        PlayerPrefs.DeleteKey(DifficultyManager.SavedModeKey);
        PlayerPrefs.Save();

        EditorGuards.Dialog(Title, $"기억해 둔 난이도({name})를 지웠습니다.\n다음 재생 때 선택 창이 다시 뜹니다.", "확인");
    }
}
