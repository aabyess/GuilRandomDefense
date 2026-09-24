using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// HUD 다섯 가지(적 체력바·사이드보스 바·유닛 이름표·하단 HUD·패배 알림)는 전부 같은 모양이다 —
/// Awake에서 uGUI 계층을 스스로 만들기 때문에 프리팹이 필요 없고, 씬에 GameObject 하나 +
/// 컴포넌트 하나만 있으면 된다.
///
/// 🔴 메뉴만 두면 "아무도 안 돌려서 씬에 0개"가 된다 — 2026-09-24, 이름표가 만든 뒤 한 번도
///    화면에 안 나온 첫 번째 이유가 그거였다. 그래서 만드는 판단을 <see cref="Ensure{T}"/> 하나로
///    모으고, 맵 생성이 끝날 때 <see cref="EnsureAll"/>로 다섯을 **보장**한다. 메뉴는 남겨두되
///    같은 함수를 쓰므로 이미 있으면 절대 두 번 만들지 않는다.
/// </summary>
public static class HudWiring
{
    const string Title = "하단 HUD";

    /// <summary>
    /// 씬에 <typeparamref name="T"/>가 있으면 그대로 두고, 없으면 GameObject 하나를 만든다.
    /// 만들었으면 true. 찾기는 비활성 오브젝트까지 포함해야 한다 —
    /// 꺼둔 채로 남아 있는 걸 못 보면 같은 게 두 개가 되고 이름표가 두 번 그려진다.
    /// </summary>
    public static bool Ensure<T>(string objectName, out GameObject obj) where T : Component
    {
        T existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (existing != null)
        {
            obj = existing.gameObject;
            return false;
        }

        obj = new GameObject(objectName, typeof(T));
        Undo.RegisterCreatedObjectUndo(obj, "Add " + objectName);
        EditorSceneManager.MarkSceneDirty(obj.scene);
        return true;
    }

    /// <summary>
    /// 맵 생성이 끝날 때 호출한다. 다섯을 보장하고 보고문 한 줄을 돌려준다.
    /// ⚠️ 씬 저장보다 **먼저** 불러야 새로 만든 게 같이 저장된다.
    /// </summary>
    public static string EnsureAll()
    {
        string report =
            Ensured<HealthBarLayer>("체력바", "HealthBarLayer") + " · " +
            Ensured<SideBossBarLayer>("보스바", "SideBossBarLayer") + " · " +
            Ensured<UnitNameplateLayer>("이름표", "UnitNameplateLayer") + " · " +
            Ensured<GameHud>("하단", "GameHud") + " · " +
            // 2026-09-24 추가 — 졌는데 진 줄 모르는 상태였다(DefeatOverlay 주석 참고).
            Ensured<DefeatOverlay>("패배", "DefeatOverlay");
        return "\nHUD: " + report;
    }

    static string Ensured<T>(string label, string objectName) where T : Component
    {
        return label + (Ensure<T>(objectName, out _) ? " ➕새로 만듦" : " ✅");
    }

    /// <summary>
    /// 메뉴 네 개가 공유하는 껍데기. 판단은 <see cref="Ensure{T}"/>에만 있다.
    /// (다섯째 <see cref="DefeatOverlay"/>는 메뉴가 없다 — 맵 생성이 보장하면 충분해서
    ///  일부러 안 만들었다. 그래서 여기는 「넷」이 맞다.)
    /// </summary>
    static void AddViaMenu<T>(string objectName, string createdMessage) where T : Component
    {
        if (!EditorGuards.RequireEditMode(Title)) return;

        bool created = Ensure<T>(objectName, out GameObject obj);
        Selection.activeGameObject = obj;
        EditorGuards.Dialog(Title,
            created
                ? createdMessage + "\n\nCmd+S 로 저장하세요."
                : $"이미 씬에 있습니다: {obj.name}\nHierarchy에서 선택해 뒀습니다.",
            "확인");
    }

    [MenuItem("Tools/HUD/씬에 적 체력바 추가")]
    static void AddHealthBars()
    {
        AddViaMenu<HealthBarLayer>("HealthBarLayer",
            "적 체력바를 씬에 추가했습니다.\nCanvas와 바는 실행 시 자동 생성됩니다.");
    }

    // 신세계 사이드보스 캐스팅바·스턴게이지바·무적 표시(ORIGINAL_BOSS_COMBAT_SPEC.md, PM 지시
    // 2026-09-06) — 사이드보스가 R62·66·71에만, 플레이어별로 나타나는 조건부라서 씬에 미리
    // 놓기보다 이렇게 한 번 추가해두고 런타임에 필요한 만큼만 켜는 쪽이 자연스럽다.
    [MenuItem("Tools/HUD/씬에 사이드보스 바 추가")]
    static void AddSideBossBars()
    {
        AddViaMenu<SideBossBarLayer>("SideBossBarLayer",
            "사이드보스 캐스팅바·스턴게이지바·무적 표시를 씬에 추가했습니다.\n" +
            "Canvas와 바는 실행 시 자동 생성되며, 사이드보스가 없는 동안은 아무것도 안 뜹니다.");
    }

    // 플레이어 유닛 머리 위 이름표(PM 지시 2026-09-23)
    [MenuItem("Tools/HUD/씬에 유닛 이름표 추가")]
    static void AddUnitNameplates()
    {
        AddViaMenu<UnitNameplateLayer>("UnitNameplateLayer",
            "유닛 이름표를 씬에 추가했습니다.\nCanvas와 라벨은 실행 시 자동 생성됩니다.");
    }

    [MenuItem("Tools/HUD/씬에 하단 HUD 추가")]
    static void AddHud()
    {
        AddViaMenu<GameHud>("GameHud",
            "GameHud를 씬에 추가했습니다.\n\nCanvas·하단 바·명령 그리드는 실행 시 자동 생성되므로\n" +
            "Hierarchy에는 GameObject 하나만 보이는 게 정상입니다.");
    }
}
