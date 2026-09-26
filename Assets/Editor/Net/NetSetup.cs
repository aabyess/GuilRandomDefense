using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 멀티 뼈대 에셋을 만든다(한 번 돌리면 되고, 다시 돌려도 같은 결과).
///   Assets/Net/NetPlayer.prefab   — NetworkObject + NetPlayer, FusionPrefab 라벨(Fusion 프리팹 표에 오른다)
///   Assets/Scenes/NetBoot.unity   — NetLauncher 하나
///   빌드 세팅                      — [0] NetBoot, [1] SampleScene
///
/// 배치모드: Unity -batchmode -projectPath <mp> -executeMethod NetSetup.Build -quit
/// ⚠️ Fusion의 Tools/Fusion/Rebuild Prefab Table은 **프로젝트 프리팹 전부**의 라벨을 만진다 — 쓰지 않고
///    우리 프리팹에만 라벨을 단다.
/// </summary>
public static class NetSetup
{
    const string NetFolder = "Assets/Net";
    const string PlayerPrefabPath = NetFolder + "/NetPlayer.prefab";
    const string GameStatePrefabPath = NetFolder + "/NetGameState.prefab";
    const string EntityPrefabPath = NetFolder + "/NetEntity.prefab";
    const string CatalogPath = NetFolder + "/NetCatalog.asset";
    const string BootScenePath = "Assets/Scenes/NetBoot.unity";
    const string GameScenePath = "Assets/Scenes/SampleScene.unity";
    const string FusionPrefabLabel = "FusionPrefab";

    [MenuItem("Tools/Net/멀티 뼈대 만들기")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(NetFolder)) AssetDatabase.CreateFolder("Assets", "Net");

        BuildNetworkPrefab<NetPlayer>(PlayerPrefabPath);
        BuildNetworkPrefab<NetGameState>(GameStatePrefabPath);
        BuildNetworkPrefab<NetEntity>(EntityPrefabPath, go =>
        {
            // 거울은 위치·회전에 스케일까지 싣는다(보스 등 실물 루트 스케일이 다르다).
            NetworkTransform nt = go.AddComponent<NetworkTransform>();
            nt.SyncScale = true;
        });
        BuildCatalog();
        BuildBootScene();
        SetBuildScenes();

        AssetDatabase.SaveAssets();
        Debug.Log("[MP] NetSetup.Build 완료");
    }

    static void BuildNetworkPrefab<T>(string path, System.Action<GameObject> extra = null) where T : NetworkBehaviour
    {
        GameObject temp = new GameObject(typeof(T).Name);
        temp.AddComponent<NetworkObject>();
        extra?.Invoke(temp);
        temp.AddComponent<T>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);

        string[] labels = AssetDatabase.GetLabels(prefab);
        if (!labels.Contains(FusionPrefabLabel))
            AssetDatabase.SetLabels(prefab, labels.Append(FusionPrefabLabel).ToArray());

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    static void BuildBootScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        // ⚠️ 프리팹은 NewScene **뒤에** 불러온다. NewScene(Single)이 안 쓰는 에셋을 내리면서 앞서 불러 둔
        //    프리팹 참조를 죽인다 — SerializedProperty로도, 직접 대입으로도 {fileID: 0}이 저장됐다(09-25 실측).
        NetworkObject playerPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(PlayerPrefabPath);
        NetworkObject gameStatePrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(GameStatePrefabPath);
        NetworkObject entityPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(EntityPrefabPath);
        NetCatalog catalog = AssetDatabase.LoadAssetAtPath<NetCatalog>(CatalogPath);

        GameObject launcherGo = new GameObject("NetLauncher");
        NetLauncher launcher = launcherGo.AddComponent<NetLauncher>();
        launcher.EditorSetup(playerPrefab, gameStatePrefab, entityPrefab, catalog, 1);
        EditorUtility.SetDirty(launcher);
        if (playerPrefab == null || gameStatePrefab == null || entityPrefab == null || catalog == null)
            throw new System.InvalidOperationException("[MP] NetBoot: NetPlayer 프리팹 참조가 비었습니다 — 씬을 저장하지 않습니다.");

        EditorSceneManager.SaveScene(scene, BootScenePath);
    }

    /// <summary>UnitData·EnemyData·WispData 전부를 GUID 순으로 카탈로그에 적는다. 로스터 에셋은 안 건드린다.</summary>
    [MenuItem("Tools/Net/카탈로그 다시 만들기")]
    public static void BuildCatalog()
    {
        NetCatalog catalog = AssetDatabase.LoadAssetAtPath<NetCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<NetCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.units = LoadAllSorted<UnitData>();
        catalog.enemies = LoadAllSorted<EnemyData>();
        catalog.wisps = LoadAllSorted<WispData>();
        catalog.gamblingOptions = LoadAllSorted<GamblingOptionData>();
        catalog.traits = LoadAllSorted<UnitTraitData>();
        catalog.gradeTracks = LoadAllSorted<UnitUpgradeTrackData>();
        catalog.attackTypeTracks = LoadAllSorted<AttackTypeUpgradeTrackData>();
        catalog.rerollAbilities = LoadAllSorted<UniqueRerollAbilityData>();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MP] 카탈로그: 유닛 {catalog.units.Count} · 적 {catalog.enemies.Count} · 위습 {catalog.wisps.Count} · 도박 {catalog.gamblingOptions.Count} · 특성 {catalog.traits.Count} · 강화 {catalog.gradeTracks.Count}+{catalog.attackTypeTracks.Count} · 지문 {catalog.Fingerprint}");
    }

    static System.Collections.Generic.List<T> LoadAllSorted<T>() where T : ScriptableObject
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .OrderBy(guid => guid, System.StringComparer.Ordinal)
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null)
            .ToList();
    }

    static void SetBuildScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(BootScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };
    }

    /// <summary>macOS 빌드. 배치모드: -executeMethod NetSetup.BuildMac -mpOut &lt;경로.app&gt;</summary>
    public static void BuildMac()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-mpOut");
        string output = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("../GuilRandomDefense-mp-build/GRD.app");

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[MP] 빌드 결과: {report.summary.result}, 오류 {report.summary.totalErrors}, 크기 {report.summary.totalSize / (1024 * 1024)}MB → {output}");
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
