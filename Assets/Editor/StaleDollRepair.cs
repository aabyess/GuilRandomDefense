using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 10-14 배포판 「교체 스킨 유닛이 안 보임」 수리. 맵 생성기가 씬에 구워 둔 전시 인형(DollIdle — 초월·불멸 전시, 흔함선택 표식, 랜덤 전시)은
/// 프리팹 연결이 없는 복제본이라, 스킨 교체로 FBX 안 메시가 바뀌면 옛 메시 참조가 끊겨 빈 받침이 된다(0.3.17: 이태훈·박은석·양재모·임채민·박민석·문필환 6기, 메시 NULL 61개).
/// 에디터에선 「없는 메시」 경고 없이 조용히 안 보여 못 잡았다. 메시 NULL 인형만 같은 자리·이름·방향으로 지금 프리팹에서 다시 세운다.
/// 쓰는 법: Unity -batchmode -quit -projectPath . -executeMethod StaleDollRepair.Run  (Check만 하려면 StaleDollRepair.Check — 끊긴 인형이 있으면 종료 코드 1)
/// </summary>
public static class StaleDollRepair
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    static string onlyModel;   // 지정하면 메시 끊김과 무관하게 그 모델(자식 이름)의 인형만 다시 세운다(키 배수 바뀐 뒤)

    /// <summary>흔함_강재규(쵸파) 키 배수 변경(10-14) 뒤: 씬의 쵸파 인형을 새 배수로 다시 세운다.</summary>
    public static void RebuildChopper() { onlyModel = "흔함_강재규"; Run(); }

    static List<DollIdle> FindStale()
    {
        var stale = new List<DollIdle>();
        foreach (DollIdle doll in Object.FindObjectsByType<DollIdle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (onlyModel != null)
            {
                foreach (Transform c in doll.transform) if (c.name == onlyModel) { stale.Add(doll); break; }
                continue;
            }
            bool broken = doll.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s => s.sharedMesh == null)
                       || doll.GetComponentsInChildren<MeshFilter>(true).Any(f => f.sharedMesh == null);
            if (broken) stale.Add(doll);
        }
        return stale;
    }

    /// <summary>배치 촬영(ClaudeCommands shotobj) 앞에서 씬을 연다: call StaleDollRepair.Open</summary>
    public static void Open() => EditorSceneManager.OpenScene(ScenePath);

    public static void HideGiant()
    {
        EditorSceneManager.OpenScene(ScenePath);
        GameObject g = GameObject.Find("불멸_거인");
        foreach (Transform c in g.transform) if (c.name.StartsWith("불멸_")) c.gameObject.SetActive(false);
    }

    /// <summary>10-14 솔플 신세계 보상 위습(RewardDistributor.newWorldWisp) 배선을 씬에 기록·저장한다.</summary>
    public static void WireNewWorldWisp()
    {
        EditorSceneManager.OpenScene(ScenePath);
        typeof(MapGenerator).GetMethod("RepairSaveRewardWisps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        var d = Object.FindFirstObjectByType<RewardDistributor>(FindObjectsInactive.Include);
        var w = new SerializedObject(d).FindProperty("newWorldWisp").objectReferenceValue;
        var so = new SerializedObject(d);
        Debug.Log($"[신세계보상 배선] newWorldWisp = {(w == null ? "NULL" : w.name)} · pachinkoReachMissWisp = {so.FindProperty("pachinkoReachMissWisp").objectReferenceValue?.name ?? "NULL"} · pachinkoMissWisp = {so.FindProperty("pachinkoMissWisp").objectReferenceValue?.name ?? "NULL"}");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        if (Application.isBatchMode) EditorApplication.Exit(w == null ? 1 : 0);
    }

    public static void Check()
    {
        EditorSceneManager.OpenScene(ScenePath);
        List<DollIdle> stale = FindStale();
        foreach (DollIdle d in stale) Debug.LogError($"[인형 점검] 메시 끊김: {d.transform.parent?.name}/{d.name}");
        Debug.Log($"[인형 점검] 끊긴 인형 {stale.Count}기");
        if (Application.isBatchMode) EditorApplication.Exit(stale.Count == 0 ? 0 : 1);
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath);
        // 모델 자식 이름(= 프리팹 이름에서 Unit_을 뗀 것)으로 로스터 UnitData를 찾는다 — 동명 유닛(16쌍)이 있어 unitName으로는 못 고른다.
        var byModel = new Dictionary<string, UnitData>();
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }))
        {
            UnitData u = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guid));
            if (u == null || u.prefab == null) continue;
            string model = u.prefab.name.StartsWith("Unit_") ? u.prefab.name.Substring(5) : u.prefab.name;
            byModel[model] = u;
        }
        MethodInfo place = typeof(MapGenerator).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .First(m => m.Name == "TryPlaceUnitModel" && m.GetParameters().Length == 7);
        float height = (float)typeof(MapGenerator).GetField("DisplayFigureHeight", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();

        int fixedCount = 0; var failed = new List<string>();
        foreach (DollIdle doll in FindStale())
        {
            Transform t = doll.transform;
            UnitData unit = null;
            foreach (Transform child in t) if (byModel.TryGetValue(child.name, out unit)) break;
            if (unit == null) { failed.Add($"{t.name}(UnitData 못 찾음)"); continue; }
            Transform parent = t.parent; string name = t.name; Vector3 oldPos = t.position; Vector3 ground = new Vector3(oldPos.x, 0f, oldPos.z); int sibling = t.GetSiblingIndex();
            float yaw = Mathf.DeltaAngle(0f, t.eulerAngles.y - 180f);
            Object.DestroyImmediate(t.gameObject);
            object[] args = { parent, name, ground, unit, height, null, yaw };
            if ((bool)place.Invoke(null, args))
            {
                // 옛 루트 y = 바닥 높이 + (발을 바닥에 붙이려 올린 몫). 새 인형을 바닥 y=0에 세웠을 때의 올린 몫을 빼면 바닥 높이가 나온다.
                Transform placed = ((GameObject)args[5]).transform;
                placed.position += Vector3.up * (oldPos.y - placed.position.y);
                placed.SetSiblingIndex(sibling);
                fixedCount++;
                Debug.Log($"[인형 수리] {name} ← {unit.prefab.name} (yaw {yaw:F0})");
            }
            else failed.Add($"{name}(다시 세우기 실패)");
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        onlyModel = null;
        int left = FindStale().Count;
        Debug.Log($"[인형 수리] 다시 세움 {fixedCount}기 · 실패 {failed.Count}" + (failed.Count > 0 ? " " + string.Join(", ", failed) : "") + $" · 남은 끊김 {left}");
        if (Application.isBatchMode) EditorApplication.Exit(left == 0 && failed.Count == 0 ? 0 : 1);
    }
}
