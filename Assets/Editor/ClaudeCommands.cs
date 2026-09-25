using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Claude가 사장님 대신 유니티 메뉴를 돌리고 화면을 찍어 결과를 읽는 창구(2026-09-13, 사장님: 「모델배선·맵생성 아직도 내가 해줘야 하냐」).
///
/// 두 가지로 쓴다 — 명령은 같다.
///   · 유니티가 **꺼져 있을 때**(배치모드):
///       Unity -batchmode -projectPath . -executeMethod ClaudeCommands.RunBatch -claudeCommands "menu Tools/아트/모델 배선;menu Tools/맵/원랜디 맵 생성" -quit
///     결과 → ClaudeBridge/outbox/batch.txt
///   · 유니티가 **켜져 있을 때**: ClaudeBridge/inbox/&lt;번호&gt;.txt에 한 줄에 명령 하나를 쓰면 1초 안에 집어 가서 돌리고
///     ClaudeBridge/outbox/&lt;번호&gt;.txt에 결과를 쓴다. (ClaudeBridge/는 Assets 밖이라 임포트되지 않고 git에서도 뺀다)
///
/// 명령
///   refresh                                   에셋 새로 읽기(스크립트 컴파일 포함)
///   menu &lt;메뉴 경로&gt;                          메뉴 실행. 대화상자는 뜨지 않고 내용이 결과에 실린다(EditorGuards.Dialog)
///   shot &lt;파일&gt; px py pz tx ty tz [fov]       열린 씬을 (px,py,pz)에서 (tx,ty,tz)를 보고 찍는다
///   shotobj &lt;파일&gt; &lt;오브젝트 이름&gt; [거리배율]   씬에서 그 이름의 오브젝트를 비스듬히 위에서 찍는다
///   lineup &lt;파일&gt; [폴더] [시작] [개수]        생성된 유닛 프리팹을 격자로 세워 찍는다(누운 유닛 찾기). 칸↔이름 표는 결과에
///   gameshot &lt;파일&gt; [초] [가로x세로] [super:N] [click:&lt;버튼&gt;]  플레이 모드로 들어가 버튼을 눌러 가며 **UI까지** 게임 화면을 찍고 나온다(아래 GameShot 절).
///                                             글씨·UI 판정엔 해상도를 꼭 박는다: gameshot hud.png 3 1920x1080 click:쉬움
///   navlane &lt;레인&gt; [칸 수]                   레인 섬 위 NavMesh 지도(걸을 수 있음·바다 영역·없음). 🔴 맵 배율·복셀·섬 높이를 건드린 뒤엔 반드시 돌린다
/// 사진은 ClaudeBridge/shots/에 PNG로 남는다. 명령마다 그동안의 Debug 로그(경고·오류 포함)가 결과에 실린다.
/// </summary>
[InitializeOnLoad]
public static class ClaudeCommands
{
    const string Folder = "ClaudeBridge";
    static double nextPoll;
    static string currentId;   // Poll이 지금 돌리는 inbox 번호 — gameshot이 결과를 플레이 모드 뒤로 미룰 때 쓴다

    static ClaudeCommands()
    {
        if (Application.isBatchMode) return;
        EditorApplication.update += Poll;
        // gameshot은 플레이 모드를 오가며 도메인이 다시 로드된다(EnterPlayModeOptions에서 리로드를 안 껐다) —
        // 그때마다 이 생성자가 다시 불리므로 진행 상태는 SessionState에 두고 여기서 이어 받는다.
        EditorApplication.update += TickGameShot;
        Application.logMessageReceived += CollectGameShotLog;
        NoteMidPlayReload();
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        // gameshot이 플레이에서 막 나와 결과를 아직 안 썼으면 다음 명령을 집지 않는다 — 같은 프레임에 Poll이 먼저 돌면
        // 다음 gameshot이 「앞선 gameshot이 아직 안 끝났다」로 거절됐다(2026-09-24 outbox 2048).
        if (LoadGameShot() != null) return;

        string inbox = Path.Combine(Folder, "inbox");
        if (!Directory.Exists(inbox)) return;
        string file = Directory.GetFiles(inbox, "*.txt").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
        if (file == null) return;

        string id = Path.GetFileNameWithoutExtension(file);
        string[] commands = File.ReadAllLines(file);
        File.Delete(file);   // 명령이 컴파일·도메인 리로드를 부르면 이 함수가 다시 불린다 — 두 번 돌지 않게 먼저 지운다

        currentId = id;
        string text;
        try { text = RunAll(commands); }
        finally { currentId = null; }

        // gameshot이 플레이 모드를 예약했으면 결과는 찍고 나온 뒤 한꺼번에 쓴다. 지금 쓰면 기다리는 쪽이
        // 「⏳ 진행 중」만 든 파일을 결과로 읽는다.
        GameShotJob job = LoadGameShot();
        if (job != null && job.id == id && !job.prefixReady)
        {
            job.prefix = text;
            job.prefixReady = true;
            SaveGameShot(job);
            return;
        }
        WriteResult(id, text);
    }

    public static void RunBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-claudeCommands");
        string commands = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        WriteResult("batch", RunAll(commands.Split(';')));
    }

    static void WriteResult(string id, string text)
    {
        string outbox = Path.Combine(Folder, "outbox");
        Directory.CreateDirectory(outbox);
        File.WriteAllText(Path.Combine(outbox, id + ".txt"), text, new UTF8Encoding(false));
    }

    static string RunAll(IEnumerable<string> commands)
    {
        StringBuilder result = new StringBuilder();
        foreach (string raw in commands)
        {
            string command = raw.Trim();
            if (command.Length == 0 || command.StartsWith("#")) continue;

            result.AppendLine($"▶ {command}");
            StringBuilder logs = new StringBuilder();
            Application.LogCallback collect = (message, stack, type) =>
            {
                string head = type == LogType.Log ? "" : $"[{type}] ";
                logs.AppendLine(head + (message.Length > 6000 ? message.Substring(0, 6000) + " …(잘림)" : message));
                if (type == LogType.Exception) logs.AppendLine(stack);
            };

            Application.logMessageReceived += collect;
            EditorGuards.Unattended = true;
            EditorGuards.Transcript.Clear();
            try
            {
                result.AppendLine(Run(command));
            }
            catch (Exception e)
            {
                result.AppendLine($"❌ 예외: {e}");
            }
            finally
            {
                EditorGuards.Unattended = false;
                Application.logMessageReceived -= collect;
            }

            if (EditorGuards.Transcript.Length > 0) result.AppendLine("— 대화상자 —").Append(EditorGuards.Transcript);
            if (logs.Length > 0) result.AppendLine("— 로그 —").Append(logs);
            result.AppendLine();
        }
        return result.ToString();
    }

    static string Run(string command)
    {
        string verb = command.Split(' ')[0];
        string rest = command.Substring(verb.Length).Trim();
        string[] parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        switch (verb)
        {
            case "refresh":
                AssetDatabase.Refresh();
                return "✅ 새로 읽기 요청(컴파일이 필요하면 끝난 뒤 다음 명령을 보내세요)";

            case "menu":
                return EditorApplication.ExecuteMenuItem(rest) ? $"✅ 실행: {rest}" : $"❌ 메뉴를 못 찾음: {rest}";

            // 에셋 하나를 강제로 다시 임포트한다. 파일 내용이 그대로면 refresh·touch로는 안 다시 읽는다
            // (유니티는 수정 시각이 아니라 내용 해시를 본다) — 임포트 프로세서 코드만 바꿨을 때 쓴다.
            case "reimport":
                if (AssetImporter.GetAtPath(rest) == null) return $"❌ 에셋 없음: {rest}";
                AssetDatabase.ImportAsset(rest, ImportAssetOptions.ForceUpdate);
                return $"✅ 다시 임포트: {rest}";

            case "call":
                return Call(rest);

            case "shot":
            {
                Vector3 position = new Vector3(F(parts[1]), F(parts[2]), F(parts[3]));
                Vector3 target = new Vector3(F(parts[4]), F(parts[5]), F(parts[6]));
                float fov = parts.Length > 7 ? F(parts[7]) : 50f;
                return Render(parts[0], position, target, fov, null);
            }

            case "shotobj":
                return ShootObject(parts[0], parts[1], parts.Length > 2 ? F(parts[2]) : 1.6f);

            case "lineup":
                return Lineup(parts[0], parts.Length > 1 ? parts[1] : "Assets/Prefabs/Generated",
                              parts.Length > 2 ? int.Parse(parts[2]) : 0, parts.Length > 3 ? int.Parse(parts[3]) : 40);

            case "units":
                // 둘째 인자가 숫자가 아니면 이름 거르개다 — units 사진 안흔함_박준희 → 이름에 그 글자가 든 것만(최대 [개수]).
                if (parts.Length > 1 && !int.TryParse(parts[1], out _))
                    return UnitLineup(parts[0], 0, parts.Length > 2 ? int.Parse(parts[2]) : 16, parts[1]);
                return UnitLineup(parts[0], parts.Length > 1 ? int.Parse(parts[1]) : 0, parts.Length > 2 ? int.Parse(parts[2]) : 16);

            case "inspect":
                return Inspect(rest);

            case "preview":
                return Preview(parts[0], parts[1], parts[2], parts.Length > 3 ? F(parts[3]) : 1.2f);

            // 맵에 모델 인형이 없는 유닛(초월 전시는 자리표시 상자)을 조합판 인형과 같은 Idle 자세로 정면에서 찍는다.
            // idleview <파일> <프리팹 경로> <기준 오브젝트 이름> [거리배율]
            // 유닛 모델 전체에서 투명(Surface=1)으로 임포트된 재질을 찾는다 — 인형이 유령처럼 보이는 사고(킹·이치고)를 한 번에 훑기.
            case "transparent":
                return TransparentMaterials();

            case "idleview":
                return IdleView(parts[0], parts[1], parts[2], parts.Length > 3 ? F(parts[3]) : 1.2f);

            case "bakesize":
                return BakeSize(rest);

            case "rig":
                return RigReport(rest);

            case "clipcheck":
                return ClipCheck(rest);

            case "clipsample":
                return ClipSample(rest);

            case "mats":
                return MaterialReport(rest);

            case "big":
                return BigObjects(parts[0], parts.Length > 1 ? F(parts[1]) : 30f,
                                  parts.Length > 2 ? parts[2] : null);

            case "gameshot":
                return StartGameShot(parts);

            case "navlane":
                return NavLane(parts.Length > 0 ? int.Parse(parts[0]) : 0, parts.Length > 1 ? int.Parse(parts[1]) : 48);

            default:
                return $"❌ 모르는 명령: {verb}";
        }
    }

    static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    // menu로 못 부르는 private 정적 함수용: call MapGenerator.Generate
    static string Call(string target)
    {
        int dot = target.LastIndexOf('.');
        string typeName = target.Substring(0, dot), methodName = target.Substring(dot + 1);
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
        MethodInfo method = type?.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (method == null) return $"❌ 함수를 못 찾음: {target}";
        object value = method.Invoke(null, null);
        return $"✅ 호출: {target}" + (value != null ? $" → {value}" : "");
    }

    static string ShootObject(string file, string objectName, float distanceScale)
    {
        GameObject found = FindInOpenScenes(objectName);
        if (found == null) return $"❌ 씬에서 못 찾음: {objectName}";

        Bounds? bounds = null;
        foreach (Renderer renderer in found.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
            if (bounds == null) bounds = renderer.bounds;
            else { Bounds b = bounds.Value; b.Encapsulate(renderer.bounds); bounds = b; }
        }
        Bounds area = bounds ?? new Bounds(found.transform.position, Vector3.one * 10f);

        float distance = Mathf.Max(8f, area.size.magnitude * distanceScale);
        Vector3 direction = new Vector3(0f, 0.75f, -1f).normalized;   // 게임 카메라처럼 남쪽 위에서
        return Render(file, area.center + direction * distance, area.center, 45f, null) +
               $"\n   경계 중심 {area.center} · 크기 {area.size}";
    }

    // 실행 중에만 생기는 것(스포너가 Start에서 만드는 해왕류 등)을 편집 중에 본다 — 프리팹을 기준 오브젝트의 위치·회전에
    // 잠깐 세워 찍고 바로 지운다. 오브젝트는 남지 않지만 씬의 「변경됨」 표시는 남는다(되돌리는 API가 없다).
    // preview <파일> <프리팹 경로(Assets/..., 공백 없이)> <기준 오브젝트 이름> [거리배율]
    static string TransparentMaterials()
    {
        StringBuilder sb = new StringBuilder("🔍 투명으로 임포트된 유닛 재질\n");
        int models = 0, hits = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Units" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) continue;
            models++;
            var bad = model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f)
                .Select(m => m.name).Distinct().ToList();
            if (bad.Count == 0) continue;
            hits++;
            sb.AppendLine($"   {path} · {bad.Count}개: {string.Join(", ", bad.Take(6))}");
        }
        sb.Append($"   모델 {models}개 중 {hits}개");
        return sb.ToString();
    }

    static string IdleView(string file, string prefabPath, string anchorName, float distanceScale)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return $"❌ 프리팹 없음: {prefabPath}";
        GameObject anchor = FindInOpenScenes(anchorName);
        if (anchor == null) return $"❌ 씬에서 못 찾음: {anchorName}";

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor.scene);
        try
        {
            // 기준 자리 위 공중에 띄운다 — 자리표시 상자·이웃 인형에 가리지 않게.
            instance.transform.SetPositionAndRotation(anchor.transform.position + Vector3.up * 60f, Quaternion.identity);
            MethodInfo pose = typeof(MapGenerator).GetMethod("PoseAsIdle", BindingFlags.Static | BindingFlags.NonPublic);
            pose?.Invoke(null, new object[] { instance });

            Bounds? bounds = null;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (bounds == null) bounds = renderer.bounds;
                else { Bounds b = bounds.Value; b.Encapsulate(renderer.bounds); bounds = b; }
            }
            Bounds area = bounds ?? new Bounds(anchor.transform.position, Vector3.one * 20f);

            // 유닛은 +Z를 본다 — 앞(+Z)에서 살짝 위로 찍는다.
            float distance = Mathf.Max(20f, area.size.magnitude * distanceScale);
            Vector3 direction = new Vector3(0f, 0.35f, 1f).normalized;
            return Render(file, area.center + direction * distance, area.center, 45f, null) +
                   $"\n   {prefab.name} Idle @ {anchorName} · 경계 크기 {area.size}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    static string Preview(string file, string prefabPath, string anchorName, float distanceScale)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return $"❌ 프리팹 없음: {prefabPath}";
        GameObject anchor = FindInOpenScenes(anchorName);
        if (anchor == null) return $"❌ 씬에서 못 찾음: {anchorName}";

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor.scene);
        try
        {
            instance.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);

            Bounds? bounds = null;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (bounds == null) bounds = renderer.bounds;
                else { Bounds b = bounds.Value; b.Encapsulate(renderer.bounds); bounds = b; }
            }
            Bounds area = bounds ?? new Bounds(anchor.transform.position, Vector3.one * 20f);

            float distance = Mathf.Max(20f, area.size.magnitude * distanceScale);
            Vector3 direction = new Vector3(0f, 0.75f, -1f).normalized;
            return Render(file, area.center + direction * distance, area.center, 45f, null) +
                   $"\n   {prefab.name} @ {anchorName} {anchor.transform.position} · 회전 {anchor.transform.eulerAngles.y:F0}° · 경계 중심 {area.center} · 크기 {area.size}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    // 스킨 모델이 **실제로 그려지는 크기**를 잰다 — 메시 자산 경계·렌더러 경계·뼈 퍼짐은 서로 다를 수 있다
    // (glb→fbx 변환본은 뼈 범위가 메시의 수천 배로 나와 키 맞추기가 뼈 크기에 속았다). BakeMesh는 뼈를 거친 정점이다.
    // clipcheck <모델 경로(공백 없이)>
    // 모델에 딸린 클립의 배율 커브 첫 값을 그 노드의 임포트된 정적 배율과 견준다 — 실행 때만 모델이 커지는 사고를 편집 모드에서 잡는다.
    // 2026-09-15 해적선: cm 파일 뿌리 노드 정적 배율은 ×0.01이 붙어 11.4인데 클립 키는 1140 그대로라 Animator가 켜지면 100배.
    static string ClipCheck(string assetPath)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (model == null) return $"❌ 에셋 없음: {assetPath}";

        StringBuilder sb = new StringBuilder($"🎞 {assetPath}\n");
        List<AnimationClip> clips = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToList();
        if (clips.Count == 0) return sb.Append("   클립 없음").ToString();

        foreach (AnimationClip clip in clips)
        {
            int checkedCurves = 0, bad = 0;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!binding.propertyName.StartsWith("m_LocalScale.")) continue;
                Transform node = string.IsNullOrEmpty(binding.path) ? model.transform : model.transform.Find(binding.path);
                if (node == null) continue;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0) continue;
                checkedCurves++;

                float key = curve.Evaluate(0f);
                char axis = binding.propertyName[binding.propertyName.Length - 1];
                float still = axis == 'x' ? node.localScale.x : axis == 'y' ? node.localScale.y : node.localScale.z;
                float ratio = Mathf.Abs(still) > 1e-6f ? key / still : float.PositiveInfinity;
                if (Mathf.Abs(ratio - 1f) <= 0.05f) continue;

                bad++;
                sb.AppendLine($"   🔴 {clip.name} · {(string.IsNullOrEmpty(binding.path) ? "(루트)" : binding.path)} · {binding.propertyName}: 클립 첫 값 {key:G4} vs 정적 {still:G4} (×{ratio:G3})");
            }
            sb.AppendLine($"   {clip.name}: 배율 커브 {checkedCurves}개 중 어긋남 {bad}개" + (bad == 0 ? " ✅" : ""));
        }
        return sb.ToString();
    }

    // mats <프리팹 또는 모델 경로(공백 없이)>
    // 렌더러의 재질 슬롯마다 실제로 붙은 텍스처를 적는다 — 「모델은 멀쩡한데 게임에서 회색」 사고를 눈이 아니라 숫자로 가른다
    // (2026-09-07 황정기·신문철, 09-15 아이언맨, 09-16 히소카 옷). 재질 이름과 텍스처 이름이 어떻게 짝지어졌는지도 같이 본다.
    static string MaterialReport(string assetPath)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null) return $"❌ 에셋 없음: {assetPath}";

        StringBuilder sb = new StringBuilder($"🎨 {assetPath}\n");
        foreach (Renderer renderer in asset.GetComponentsInChildren<Renderer>(true))
        {
            sb.AppendLine($"   렌더러 {renderer.name} · 슬롯 {renderer.sharedMaterials.Length}개");
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null) { sb.AppendLine("     (빈 슬롯)"); continue; }

                string textures = "";
                foreach (string slot in new[] { "_BaseMap", "_MainTex", "_BaseColorMap" })
                {
                    if (!material.HasProperty(slot)) continue;
                    Texture tex = material.GetTexture(slot);
                    if (tex != null) textures += $" · {slot}={tex.name}";
                }
                if (textures.Length == 0) textures = " · 텍스처 없음(색만)";

                Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                            : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                sb.AppendLine($"     {material.name} ({material.shader.name}) 색 ({color.r:F2}, {color.g:F2}, {color.b:F2}){textures}");
            }
        }
        return sb.ToString();
    }

    // clipsample <프리팹 경로(공백 없이)>
    // 프리팹을 미리보기 씬에 놓고 Animator에 물린 클립을 실제로 샘플링해(첫·중간 프레임) 구운 메시 크기를 전후로 잰다.
    // 커브 값만 보는 clipcheck가 못 잡는 「실행 때만 커짐」을 편집 모드에서 재현한다. 크기가 튀면 많이 움직인 노드를 적는다.
    static string ClipSample(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return $"❌ 프리팹 없음: {prefabPath}";

        Scene preview = EditorSceneManager.NewPreviewScene();
        StringBuilder sb = new StringBuilder($"🎬 {prefabPath}\n");
        try
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Animator animator = instance.GetComponentInChildren<Animator>(true);
            AnimationClip clip = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.FirstOrDefault()
                : null;
            if (clip == null) return sb.Append("   Animator·클립 없음").ToString();
            sb.AppendLine($"   Animator {animator.gameObject.name} · 컨트롤러 {animator.runtimeAnimatorController.name} · 클립 {clip.name} ({clip.length:F2}초) · isHuman {animator.isHuman}");

            Transform[] nodes = instance.GetComponentsInChildren<Transform>(true);
            var before = nodes.ToDictionary(t => t, t => (t.position, t.lossyScale));
            Vector3 size0 = BakedWorldSize(instance);
            sb.AppendLine($"   샘플 전 구운 크기 {size0}");

            foreach (float time in new[] { 0f, clip.length * 0.5f })
            {
                clip.SampleAnimation(animator.gameObject, time);
                Vector3 size = BakedWorldSize(instance);
                sb.AppendLine($"   SampleAnimation t={time:F2} 구운 크기 {size} (×{(size0.magnitude > 1e-6f ? size.magnitude / size0.magnitude : 0f):F2})");
            }

            // 실행 때 Animator와 같은 길 — PlayableGraph로 Animator에 평가한다(MapGenerator.PoseAsIdle과 같은 방식).
            // SampleAnimation(레거시)은 Generic 아바타의 루트 노드 처리가 실행 때와 다를 수 있다.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            animator.applyRootMotion = false;
            foreach (float time in new[] { 0f, clip.length * 0.5f })
            {
                UnityEngine.Playables.PlayableGraph graph = UnityEngine.Playables.PlayableGraph.Create("clipsample");
                try
                {
                    graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
                    var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "샘플", animator);
                    var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
                    playable.SetTime(time);
                    output.SetSourcePlayable(playable);
                    graph.Evaluate(0f);
                }
                finally
                {
                    if (graph.IsValid()) graph.Destroy();
                }
                Vector3 size = BakedWorldSize(instance);
                sb.AppendLine($"   Animator 평가 t={time:F2} 구운 크기 {size} (×{(size0.magnitude > 1e-6f ? size.magnitude / size0.magnitude : 0f):F2}) · Animator 노드 배율 {animator.transform.localScale}");
            }

            // 실행 때 Animator가 켜지면 먼저 하는 일 — 아바타 기본 자세로 다시 묶기(Rebind). cm 단위 파일에서 이 기본 자세가
            // 단위 변환 없이 쓰이면 뼈가 100배로 퍼질 수 있다(2026-09-15 해적선 인형 의심).
            animator.Rebind();
            animator.Update(0f);
            Vector3 rebound = BakedWorldSize(instance);
            sb.AppendLine($"   Rebind+Update 구운 크기 {rebound} (×{(size0.magnitude > 1e-6f ? rebound.magnitude / size0.magnitude : 0f):F2})");

            foreach (var moved in nodes
                .Select(t => (t, move: (t.position - before[t].position).magnitude, scale: t.lossyScale.magnitude / Mathf.Max(before[t].lossyScale.magnitude, 1e-6f)))
                .OrderByDescending(x => Mathf.Max(x.move / Mathf.Max(size0.magnitude, 1e-6f), Mathf.Abs(Mathf.Log(Mathf.Max(x.scale, 1e-6f)))))
                .Take(6))
                sb.AppendLine($"     {moved.t.name}: 이동 {moved.move:F2} · 배율 ×{moved.scale:F2}");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
        return sb.ToString();
    }

    static Vector3 BakedWorldSize(GameObject root)
    {
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, true);
                Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                foreach (Vector3 v in baked.vertices)
                {
                    Vector3 w = toWorld.MultiplyPoint3x4(v);
                    min = Vector3.Min(min, w);
                    max = Vector3.Max(max, w);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                Vector3 w = filter.transform.TransformPoint(v);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
        }
        return max.x >= min.x ? max - min : Vector3.zero;
    }

    // bakesize <모델 또는 프리팹 경로(공백 없이)>
    static string BakeSize(string assetPath)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null) return $"❌ 에셋 없음: {assetPath}";

        Scene preview = EditorSceneManager.NewPreviewScene();
        StringBuilder sb = new StringBuilder($"🔎 {assetPath}\n");
        try
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            sb.AppendLine($"   루트 배율 {instance.transform.lossyScale}");

            foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked, true);   // 배율까지 반영한 로컬 정점
                    Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                    Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    foreach (Vector3 v in baked.vertices)
                    {
                        Vector3 w = toWorld.MultiplyPoint3x4(v);
                        min = Vector3.Min(min, w);
                        max = Vector3.Max(max, w);
                    }

                    Vector3 boneMin = Vector3.positiveInfinity, boneMax = Vector3.negativeInfinity;
                    foreach (Transform bone in skin.bones)
                    {
                        if (bone == null) continue;
                        boneMin = Vector3.Min(boneMin, bone.position);
                        boneMax = Vector3.Max(boneMax, bone.position);
                    }

                    sb.AppendLine($"   {skin.name}: 정점 {baked.vertexCount} · **구운 크기** {max - min} (바닥 {min.y:F3})" +
                                  $" · 자산 경계 {skin.sharedMesh?.bounds.size} · 렌더러 경계 {skin.bounds.size}" +
                                  $" · 뼈 {skin.bones.Length}개 퍼짐 {boneMax - boneMin} · 루트뼈 {skin.rootBone?.name} · 노드 배율 {skin.transform.lossyScale}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            }

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    sb.AppendLine($"   (정적) {filter.name}: 자산 경계 {filter.sharedMesh.bounds.size} · 노드 배율 {filter.transform.lossyScale}");
            return sb.ToString();
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // 「이상한 게 크게 있다」를 이름으로 찾는다 — 루트 바로 아래 자식마다 렌더러 경계를 합쳐, 가장 긴 변이 기준보다 큰 것을 큰 순서로.
    // big <루트 이름> [최소 크기] [이름에 들어갈 글자(선택)]
    static string BigObjects(string rootName, float minSize, string nameFilter)
    {
        GameObject root = FindInOpenScenes(rootName);
        if (root == null) return $"❌ 씬에서 못 찾음: {rootName}";

        List<(string name, Bounds bounds)> found = new List<(string, Bounds)>();
        foreach (Transform child in root.transform)
        {
            if (nameFilter != null && !child.name.Contains(nameFilter)) continue;
            Bounds? bounds = null;
            foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || !renderer.enabled) continue;
                if (bounds == null) bounds = renderer.bounds;
                else { Bounds b = bounds.Value; b.Encapsulate(renderer.bounds); bounds = b; }
            }
            if (bounds == null) continue;
            Vector3 s = bounds.Value.size;
            if (Mathf.Max(s.x, s.y, s.z) >= minSize) found.Add((child.name, bounds.Value));
        }

        StringBuilder sb = new StringBuilder($"📏 {rootName} 아래 가장 긴 변 {minSize} 이상: {found.Count}개\n");
        foreach ((string name, Bounds b) in found.OrderByDescending(f => Mathf.Max(f.bounds.size.x, f.bounds.size.y, f.bounds.size.z)).Take(40))
            sb.AppendLine($"   {name} · 크기 {b.size} · 중심 {b.center}");
        return sb.ToString();
    }

    // Humanoid 아바타가 왜 안 서는지 — 유니티는 자동 매핑 실패 이유를 로그에 안 남긴다. 임포터의 매핑 결과·아바타 판정·
    // 필수 뼈 후보(이름에 pelvis/hips·spine·head·thigh·calf·foot·upperarm·forearm·hand)의 부모 사슬을 찍는다.
    // rig <모델 경로(공백 없이)>
    static string RigReport(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null) return $"❌ 모델 임포터 없음: {assetPath}";

        StringBuilder sb = new StringBuilder($"🦴 {assetPath}\n");
        HumanDescription description = importer.humanDescription;
        sb.AppendLine($"   애니메이션 타입 {importer.animationType} · 아바타 설정 {importer.avatarSetup} · 매핑된 사람 뼈 {description.human?.Length ?? 0}개 · 골격 {description.skeleton?.Length ?? 0}개");
        if (description.human != null)
            foreach (HumanBone bone in description.human.Take(24))
                sb.AppendLine($"     {bone.humanName} = {bone.boneName}");

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Avatar>().FirstOrDefault();
        sb.AppendLine(avatar == null ? "   아바타 에셋 없음" : $"   아바타 {avatar.name} · isValid {avatar.isValid} · isHuman {avatar.isHuman}");

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (model != null)
        {
            string[] keys = { "pelvis", "hips", "spine", "neck", "head", "thigh", "upleg", "calf", "leg", "foot", "clavicle", "upperarm", "forearm", "hand" };
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                string lower = t.name.ToLowerInvariant();
                if (!keys.Any(lower.Contains) || lower.Contains("finger") || lower.Contains("toe") || lower.Contains("nub")) continue;
                List<string> chain = new List<string>();
                for (Transform p = t; p != null; p = p.parent) chain.Add(p.name);
                sb.AppendLine($"     {t.name} · 로컬회전 {t.localRotation.eulerAngles} · 로컬위치 {t.localPosition} · 사슬 {string.Join(" < ", chain.Take(6))}");
            }
        }
        return sb.ToString();
    }

    // 화면에 안 보이는 원인을 사진 대신 값으로 본다 — 자식마다 위치·배율, 렌더러 켜짐, 재질·셰이더·텍스처·컷오프·컬·발광, 메시 크기.
    static string Inspect(string objectName)
    {
        GameObject found = FindInOpenScenes(objectName);
        if (found == null) return $"❌ 씬에서 못 찾음: {objectName}";

        StringBuilder sb = new StringBuilder($"🔎 {objectName} (활성 {found.activeInHierarchy})\n");
        foreach (Transform t in found.GetComponentsInChildren<Transform>(true))
        {
            int depth = 0;
            for (Transform p = t; p != null && p != found.transform; p = p.parent) depth++;
            string indent = new string(' ', 3 + 2 * depth);

            sb.Append($"{indent}{t.name} · 활성 {t.gameObject.activeSelf} · 월드 {t.position} · 배율 {t.lossyScale} · 회전 {t.rotation.eulerAngles}");
            if (t.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                sb.Append($"\n{indent}  메시 {filter.sharedMesh.name} · 정점 {filter.sharedMesh.vertexCount} · 자산 경계 {filter.sharedMesh.bounds.size}");
            if (t.TryGetComponent(out Renderer renderer))
            {
                sb.Append($"\n{indent}  렌더러 {(renderer.enabled ? "켜짐" : "꺼짐")} · 월드 경계 중심 {renderer.bounds.center} 크기 {renderer.bounds.size} · 그림자 {renderer.shadowCastingMode}");
                foreach (Material m in renderer.sharedMaterials)
                {
                    if (m == null) { sb.Append($"\n{indent}  재질 없음"); continue; }
                    Texture texture = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    string F(string prop) => m.HasProperty(prop) ? m.GetFloat(prop).ToString("0.##") : "-";
                    sb.Append($"\n{indent}  재질 {m.name} · 셰이더 {m.shader.name} · 큐 {m.renderQueue} · 텍스처 {(texture != null ? $"{texture.name} {texture.width}×{texture.height}" : "없음")}" +
                              $" · 알파클립 {F("_AlphaClip")} 컷오프 {F("_Cutoff")} · 컬 {F("_Cull")} · 표면 {F("_Surface")}" +
                              $" · 발광 {(m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor").ToString() : "-")} · 키워드 [{string.Join(",", m.shaderKeywords)}]");
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static GameObject FindInOpenScenes(string name)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
        return null;
    }

    // 프리팹을 미리보기 씬(열린 씬과 분리)에 격자로 세운다 — 맵 씬을 더럽히지 않는다.
    static string Lineup(string file, string folder, int start, int count)
    {
        List<GameObject> prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(p => p != null)
            .Skip(start).Take(count).ToList();
        if (prefabs.Count == 0) return $"❌ 프리팹 없음: {folder} ({start}번부터)";

        Scene preview = EditorSceneManager.NewPreviewScene();
        StringBuilder table = new StringBuilder();
        try
        {
            GameObject sun = new GameObject("해");
            SceneManager.MoveGameObjectToScene(sun, preview);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            const int perRow = 8;
            const float cell = 30f;
            int rows = (prefabs.Count + perRow - 1) / perRow;
            for (int i = 0; i < prefabs.Count; i++)
            {
                int row = i / perRow, column = i % perRow;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], preview);
                // 앞줄이 카메라에 가깝게: 0행이 맨 뒤(위쪽). 얼굴이 보이도록 카메라를 +Z로 두므로
                // 자리도 −X·−Z로 늘린다(위 UnitLineup 주석 참고 — 같은 이유다).
                instance.transform.position = new Vector3(-column * cell, 0f, -(rows - 1 - row) * cell);
                table.AppendLine($"   {row + 1}행 {column + 1}열 = {prefabs[i].name}");
            }

            Vector3 center = new Vector3(-(perRow - 1) * cell * 0.5f, 8f, -(rows - 1) * cell * 0.5f);
            float span = Mathf.Max(perRow, rows) * cell;
            string shot = Render(file, center + new Vector3(0f, span * 0.55f, span * 0.85f), center, 50f, preview);
            return shot + $"\n   {folder} {start}번부터 {prefabs.Count}개(위에서 본 격자, 1행이 맨 뒤):\n" + table;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // 게임에서 보이는 모습 그대로: 생성된 Unit_ 프리팹만, 맵 생성기와 같은 방법(MapGenerator.PoseAsIdle)으로 Idle을 입혀
    // **세우기 보정 없이** 앞에서 찍는다. 칸마다 실제 크기와 몸의 위쪽(골반→머리) 방향을 같이 적는다 — 누움·극소형을 숫자로도 가른다.
    static string UnitLineup(string file, int start, int count, string nameFilter = null)
    {
        List<GameObject> prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Generated" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith("Unit_"))
            // 에셋 경로의 한글은 NFC가 아닐 수 있다(skin-import-traps ①-B) — 양쪽을 맞춘 뒤 견준다.
            .Where(p => nameFilter == null || Path.GetFileNameWithoutExtension(p).Normalize(NormalizationForm.FormC)
                                                  .Contains(nameFilter.Normalize(NormalizationForm.FormC)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(p => p != null)
            .Skip(start).Take(count).ToList();
        if (prefabs.Count == 0) return nameFilter != null ? $"❌ 이름에 「{nameFilter}」가 든 Unit_ 프리팹 없음" : $"❌ Unit_ 프리팹 없음({start}번부터)";

        MethodInfo pose = typeof(MapGenerator).GetMethod("PoseAsIdle", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo measure = typeof(MapGenerator).GetMethod("TryMeasureFigure", BindingFlags.Static | BindingFlags.NonPublic);

        Scene preview = EditorSceneManager.NewPreviewScene();
        StringBuilder table = new StringBuilder();
        try
        {
            GameObject sun = new GameObject("해");
            SceneManager.MoveGameObjectToScene(sun, preview);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40f, -20f, 0f);

            const int perRow = 8;
            const float cell = 28f;
            int rows = (prefabs.Count + perRow - 1) / perRow;
            for (int i = 0; i < prefabs.Count; i++)
            {
                int row = i / perRow, column = i % perRow;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], preview);
                // 🔴 2026-09-24: **이 도구는 「앞에서 봄」이라고 찍으면서 뒤를 보여 주고 있었다.**
                //    유니티 캐릭터는 +Z를 본다(`ArtBinder.AutoUpright`가 그렇게 맞춘다). 그런데
                //    카메라도 −Z에 서서 +Z를 봤다 — **둘이 같은 쪽을 보니 등만 나온다.**
                //    유닛 프리팹 213개의 요 각을 전부 읽어 봐도 180°가 **하나도 없다**(구현담당1).
                //    즉 모델이 아니라 보는 쪽 하나가 틀렸다. 「전부 똑같이 틀린 것」이 그 단서였다.
                //    ⚠️ 이 도구로 한 「정면 사진」 판정은 **전부 뒷면을 본 것**이다(MapGenerator.cs:819 등).
                //
                //    카메라를 +Z로 옮기면서 자리도 같이 뒤집는다 — 카메라만 옮기면 라벨이 거짓말이 된다.
                //    카메라가 −Z를 보므로 **보는 사람의 오른쪽은 −X**다. 그래서 열도 −X로 늘린다.
                instance.transform.position = new Vector3(-column * cell, 0f, -row * cell);   // 1행이 맨 앞·1열이 왼쪽
                pose?.Invoke(null, new object[] { instance });

                string size = "크기 못 잼";
                if (measure != null)
                {
                    object[] args = { instance, null };
                    if ((bool)measure.Invoke(null, args))
                    {
                        Bounds b = (Bounds)args[1];
                        size = $"가로 {b.size.x:F2} · 키 {b.size.y:F2} · 앞뒤 {b.size.z:F2} · 바닥 {b.min.y:F2}";
                    }
                }

                string up = "뼈 없음";
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.isHuman)
                {
                    // 🔴 골반 자리는 Hips 뼈가 아니라 **양 허벅지 뿌리의 중점**으로 잡는다. Hips 뼈가 골반에서 떨어져 있는
                    //    리그(바운티러시 pl_ 계열의 world_joint 원점)는 몸이 똑바로 서 있어도 기울어 보인다 —
                    //    2026-09-23 히든_석성례: Hips→Head (0.25, 0.87, 0.42)였는데, 휴식 자세 사슬 계산도 (0.29, 0.82, 0.49)로 같았고
                    //    실제 몸의 축(Spine→Head)은 (0, 0.97, 0.23)이었다.
                    //    🔴 2026-09-24 정정: 그때 「정면 사진도 정상이었다(outbox 1261)」로 닫았는데 **그건 뒷면 사진이었다**
                    //       (이 도구의 카메라가 반대편이었다 — 위 주석 참고). 그리고 결론도 틀렸다:
                    //       **Hips는 진짜로 몸 밖에 있었다**(z −1.17m, 메시는 0~1.8m). 원본 리그 자체가 그랬고,
                    //       blender가 `fit_hips`로 고쳤다(f7e1a07d). 즉 **사진도 결론도 둘 다 틀렸다.**
                    //       여기서 허벅지 중점을 쓰는 것 자체는 그대로 옳다 — 오히려 그 리그가 그 근거다.
                    //    허벅지가 없으면(매핑 실패) 예전처럼 Hips로 잰다.
                    Transform leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    Transform rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                    Vector3? pelvis = leftLeg != null && rightLeg != null ? (leftLeg.position + rightLeg.position) * 0.5f
                                    : hips != null ? hips.position : (Vector3?)null;
                    if (pelvis != null && head != null)
                    {
                        Vector3 v = (head.position - pelvis.Value).normalized;
                        up = $"몸 위쪽 ({v.x:F2}, {v.y:F2}, {v.z:F2})" + (v.y > 0.8f ? " ✅" : v.y < 0.4f ? " 🔴 누움" : " 🟡 기울어짐");
                    }
                }
                table.AppendLine($"   {row + 1}행 {column + 1}열 {prefabs[i].name}: {size} · {up}");
            }

            // 🔴 틀을 **실제로 놓인 개수**에 맞춘다. perRow(8)로 잡으면 한 기만 찍을 때
            //    화면의 1/8에 콩알만 하게 나와서 얼굴도 텍스처도 못 본다 — 그 상태로
            //    「눈으로 확인했다」를 하면 아무것도 확인한 게 아니다(09-24에 실제로 그랬다).
            //    최소 2칸은 둬서 키 30짜리가 세로로 잘리지 않게 한다.
            int usedColumns = Mathf.Min(prefabs.Count, perRow);
            Vector3 center = new Vector3(-(usedColumns - 1) * cell * 0.5f, 8f, -(rows - 1) * cell * 0.5f);
            float span = Mathf.Max(Mathf.Max(usedColumns, rows), 2) * cell;
            string shot = Render(file, center + new Vector3(0f, span * 0.3f, span * 0.95f), center, 45f, preview);
            return shot + $"\n   Unit_ {start}번부터 {prefabs.Count}개(앞에서 봄, 1행이 맨 앞·1열이 왼쪽):\n" + table;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static string Render(string file, Vector3 position, Vector3 target, float fov, Scene? scene)
    {
        const int width = 1600, height = 900;
        GameObject cameraObject = new GameObject("ClaudeCamera");
        RenderTexture texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            if (scene.HasValue)
            {
                SceneManager.MoveGameObjectToScene(cameraObject, scene.Value);
                camera.scene = scene.Value;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.42f, 0.45f, 0.48f);
            }
            else
            {
                cameraObject.hideFlags = HideFlags.DontSave;
            }

            camera.transform.position = position;
            camera.transform.LookAt(target);
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 6000f;
            camera.targetTexture = texture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            string folder = Path.Combine(Folder, "shots");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, file.EndsWith(".png") ? file : file + ".png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            return $"📷 {Path.GetFullPath(path)}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    // ───────────────────────────── GameShot ─────────────────────────────
    // gameshot <파일> [초] [가로x세로] [super:N] [click:<버튼>]...
    //   예) gameshot hud.png 3 click:쉬움
    //       gameshot hud_tall.png 3 1080x1920 click:Button_Easy
    //   · [초]         마지막 클릭 뒤 찍기 전까지 기다리는 시간(기본 3)
    //   · 가로x세로    Game 뷰 해상도 고정(Game 뷰에 남는다 — 되돌리는 공개 API가 없다)
    //   · super:N      N배로 키워 찍는다(기본 1). ⚠️ 오버레이 UI는 N배로 **다시 그려지지 않고 늘려 붙는다** — 글씨가 선명해지지 않는다.
    //                  2026-09-23 실측: 779×536 Game 뷰 + super:2(1558폭)는 두 라벨 모두 계단, 1920x1080 + super:1은 매끄러웠다(outbox 1279).
    //
    // 🔴 **글씨·UI를 판정할 사진은 반드시 해상도를 박아서 찍는다**(예: 1920x1080 — HUD CanvasScaler 기준과 같다).
    //    해상도를 안 주면 지금 Game 뷰 창 크기로 찍힌다. 창이 작으면 CanvasScaler가 UI를 줄여(779폭이면 0.45배) 글자가 9픽셀로 뭉개지고,
    //    그걸 확대해 본 판정은 틀린다. 2026-09-23 하루에 두 번 틀렸다 — 「목재」가 「목제」로 보여 오타로 단정했고,
    //    같은 뭉개짐을 「상단 라벨만 TMP 폰트가 안 붙었다」로 읽었다. 둘 다 1920x1080으로 다시 찍으니 사라졌다.
    //    그래서 해상도 없이 찍으면 결과에 경고를 붙인다(아래 StartGameShot).
    //   · click:<버튼>  플레이 모드에서 그 버튼을 누른다. **게임 오브젝트 이름**(Button_Easy) 또는 **버튼 글자**(쉬움)로 찾는다.
    //                  여러 개 주면 적은 순서대로 1초 간격으로 누른다. 못 찾으면 그때 보이던 버튼 목록을 결과에 남긴다.
    //                  글자에 공백이 있으면 게임 오브젝트 이름으로 준다.
    //   · click?:<버튼> 있으면 누르고 **없으면 건너뛴다**(2초 찾고 포기, 실패로 안 끝남). 2026-09-24부터 난이도를 기억해서
    //                  기억이 있으면 난이도 창이 안 뜬다 — `click:쉬움`은 그때 「못 찾음」으로 판이 끝나니 `click?:쉬움`을 쓴다.
    //   · spawn:<유닛>  클릭이 끝난 뒤 플레이어 1(0번 레인) 적 경로 안쪽, **경로에서 사거리 절반 거리**에 그 유닛을 세운다
    //                  (Assets/Data/Units/Roster/<유닛>.asset). 기다리는 동안 0번 레인 적의 체력 감소를 0.25초마다 세서
    //                  「적 한 마리당 몇 대 맞았나」·총 피해·골드 변화를 결과에 싣는다 — 사거리·공속 검증용(2026-09-23 PM 승인 (B)).
    //                  `spawn:<유닛>@pen`이면 **실제 뽑기와 같은 자리**(LaneMarker.TakeSpawnPosition → 우리)에 세운다.
    //                  2026-09-23 이름표를 경로 안쪽(spawn: 기본 자리)에서만 확인하고 「고쳤다」고 보고했는데, 플레이어 유닛은
    //                  우리에 생겨 사장님 화면에선 여전히 안 보였다 — 시험 자리가 보는 사람 자리와 달랐다. 그래서 둘 다 화면 좌표를 찍는다.
    //                  같은 날 교훈 하나 더: **계산이 맞는지 전에 그 값이 실제로 쓰이는지 본다** — 씬에 적힌 편집 시점 카메라 값으로
    //                  계산했는데 실행하면 RtsCameraController.FocusOnLocalLane이 덮어써 헛계산이 됐다. 판정은 이 명령의 👁 줄(실행 중 실측)로.
    //   · select:<이름>  **내 유닛**(Selectable, 주인 = 나) 중 이름에 그 글자가 든 첫 것을 **좌클릭**한다.
    //   · rclick:<이름>  이름에 그 글자가 든 오브젝트(포탈 등) 자리를 **우클릭**한다(선택한 유닛에 이동 명령).
    //                  select:·rclick: 모두 「=이름」이면 정확히 그 이름만 고른다(rclick:=Lane1 → Lane1_앞치마가 아니라 레인 섬 판).
    //                  둘 다 **게임의 실제 입력 경로**를 탄다 — 가상 마우스 장치(Input System)에 누르기·떼기 이벤트를 넣어
    //                  SelectionManager·UnitMover가 평소처럼 Mouse.current를 읽고 WorldPick으로 레이캐스트한다. 내부 상태를 직접 안 만진다
    //                  (spawn:처럼 상태를 직접 만들면 실제와 갈라진다 — 09-24 Warp 결함). click:과 한 줄에 섞어 적은 순서대로 돈다.
    //                  대상이 화면 밖이거나 하단 HUD 뒤면 미니맵 클릭과 같은 RtsCameraController.MoveTo로 카메라를 먼저 옮긴다.
    //                  에디터 입력이 Game 뷰 포커스를 따지지 않게 그동안만 editorInputBehaviorInPlayMode를 바꾸고 끝나면 되돌린다.
    //   · boxselect:<이름>  내 유닛 중 이름에 그 글자가 든 것 **전부를 드래그 박스로** 고른다(누름 → 끌기 → 뗌, SelectionManager.SelectInBox 경로).
    //   · wait:<초>      다음 동작 전에 기다린다(위습이 걸어가 포탈에 들어갈 시간, 라운드가 넘어갈 시간 등).
    //   · rounds:<N>     긴 판 — 라운드 N이 끝나거나(=N+1 시작) 패배·게임오버·시간 초과까지 판을 이어 가며, **라운드가 바뀔 때마다**
    //                  🏁 지표 한 줄(적 레인/전체 · 데스카운트 · 골드 · 목재 · 내 유닛 · 위습 칸 · 스토리 · 보스 적 · 프레임 · 새 예외)과
    //                  캡처 round_NN.png를 남긴다. [초]는 무시된다(끝날 때 찍는다).
    //   · autoloop       rounds:와 함께 — 라운드가 바뀔 때마다 사람의 한 턴을 목록에 덧붙인다: 랜덤유닛 위습을 위습 칸 클릭으로 하나씩
    //                  Portal_유닛랜덤에 보냄 → 카드별 조합 버튼 시도 → 모든 유닛을 흙길 옆 모서리로(rclickpt:corner).
    //                  모서리 이동은 한 번 + 「boxselect:Unit_|far」(모서리 밖에 남은 것만) 두 번 — 레인 가운데에 생긴 조합 결과가
    //                  우리와 한 화면에 안 들어와 첫 박스에서 빠진다(09-25 i1_07: 안흔함 가동률 0%로 R3 패배).
    //                  🔴 「rounds:」만 주면 도구는 플레이하지 않는다(🖱 0줄). 사람의 한 턴은 autoloop이 붙인다.
    //   · rclickpt:corner  0번 레인 적 경로의 안쪽 모서리(경로에서 약 50 안) **땅**을 우클릭한다 — 유닛이 실제로 싸우는 자리.
    //   · call:<형.함수>  플레이 도중 그 자리에서 인자 없는 정적 함수를 불러, 돌려준 문자열을 결과에 싣는다(다른 사람 진단을 판 안에서 돌릴 때 —
    //                  예: call:ApronProbe.AgentAudit. 그 진단의 메뉴는 스스로 플레이에 들어가서 이미 플레이 중인 판에선 못 쓴다, 09-24).
    //   · cardpair       여럿 고른 상태에서 같은 종류가 2기 이상인 유닛의 카드를 누른다(한 기 선택 → 그 유닛의 조합 버튼이 뜬다).
    //   · buttons        지금 떠 있는 누를 수 있는 버튼 목록(이름「글자」)을 결과에 남긴다 — 판을 끝내지 않는다.
    //   · snap:<파일>    그 순간을 한 장 더 찍는다(해상도는 이 판의 Game 뷰 그대로).
    //                  select·rclick·boxselect·wait·buttons·snap·click은 적은 순서대로 한 줄로 돈다 — 사장님 한 판을 그대로 흉내 낼 수 있다.
    //   · combine:<레시피>  레시피(에셋 이름 「안흔함_박민수_조합」 또는 결과 유닛 이름 「안흔함_박민수」)의 재료를 **우리**에 세우고
    //                  1초 뒤 CombineSystem.TryCombine을 불러 조합한다 — 결과 유닛의 자리·레인 중심까지 거리·화면 안인지를 찍는다.
    //                  (2026-09-24 PM: 조합 결과를 레인 가운데로 옮긴 코드를 눈으로 확인할 길이 없었다.) 재료가 특정 유닛뿐인 레시피만 된다.
    //                  `spawn:<유닛>@corner`면 레인 안쪽 **모서리**(경로가 두 변으로 지나는 자리)에 대각선으로 세운다 —
    //                  원작 플레이어가 실제로 서는 자리. 모서리가 여러 개면 돌아가며 쓴다.
    //                  🔴 **에디터 촬영 전용.** 뽑기·골드를 거치지 않고 유닛을 만든다 — 게임 코드(Assets/Scripts)로 옮기면 치트가 된다.
    //                  이 파일은 Assets/Editor라 빌드에 안 들어간다. 여기 밖으로 꺼내지 말 것.
    //
    // 왜 플레이 모드인가 — shot·shotobj·idleview는 카메라를 RenderTexture로 굽는다. ScreenSpaceOverlay 캔버스는 카메라를 안 거쳐서
    // 거기 안 담긴다. 게다가 GameHud·DifficultySelectHud는 **실행 때 Awake에서 캔버스를 만든다** — 편집 모드의 Game 뷰엔 HUD가 아예 없다.
    //
    // 🔴 창 픽셀 읽기(InternalEditorUtility.ReadScreenPixel)는 시험 후 걷어냈다(2026-09-23 outbox 1264).
    //    도킹된 Game 뷰의 EditorWindow.position이 Game 뷰가 아니라 **에디터 창 왼쪽 위**를 가리켜서, 제목 표시줄·Hierarchy·
    //    「Importing assets」 진행 창까지 찍히고 정작 하단 바는 잘렸다. ScreenCapture 쪽은 Game 뷰 그대로(하단 바 포함)였다.
    //
    // 진행 — 명령 한 번이 여러 에디터 프레임과 도메인 리로드 두 번에 걸친다. 상태는 SessionState(에디터 세션 동안 리로드를
    // 넘어 남는다)에 두고, 생성자가 다시 거는 TickGameShot이 이어 받는다.
    //   entering → (플레이 모드 진입·도메인 리로드) → settling 1초(Awake·Start) → clicking → waiting [초]
    //   → capturing(파일이 다 써질 때까지) → exiting → outbox에 결과
    // 그동안 Poll은 isPlayingOrWillChangePlaymode라 다음 inbox를 안 집는다. 뒤에 넣은 명령은 찍고 나온 뒤에 차례로 돈다.

    const string GameShotKey = "ClaudeCommands.GameShot";
    const double GameShotEnterTimeout = 60, GameShotCaptureTimeout = 15, GameShotExitTimeout = 60;
    const double GameShotSettle = 1.0, GameShotClickGap = 0.5, GameShotClickSearch = 5.0, GameShotOptionalClickSearch = 2.0;
    const int GameShotMaxLogKinds = 40;

    [Serializable]
    class GameShotJob
    {
        public string id;           // outbox 번호
        public string file;         // ScreenCapture 결과(절대 경로)
        public float seconds;
        public int superSize = 1;
        public int watchRounds;          // rounds:N — 0이면 끄기
        public bool autoLoop;
        public bool storySent;           // autoloop: 안흔함을 스토리존에 보냈나 — 스토리가 깨지면 복귀포탈로 되돌린다
        public int storyFinishedAtSend;
        public bool shopTried;           // autoloop: 도박소 한 번 눌러 봤나
        public List<string> choicePicks = new List<string>();   // 도구가 흔함선택 포탈로 보낸 이름 누계 — 실제 흔함 이름 분포와 견준다(포탈이 먹으면 한 이름에 몰린다, outbox 2112)
        public int lastRoundSeen = -1;
        public int logKindsAtRound;      // 라운드 바뀔 때의 예외 종류 수 — 새로 생긴 종류만 그 라운드 줄에 적는다
        public int logCountAtRound;
        public double watchDeadline;
        public bool finishNow;
        public bool overLogged;          // 끝(패배·게임오버) 줄을 한 번 찍었나 — finishNow 뒤에 난 패배도 찍으려고 따로 둔다
        public int pointerPhase;   // select:/rclick: 한 동작 안의 단계(0 조준·카메라 → 1 누름 → 2 뗌 → 3 결과)
        public float pointerX, pointerY;
        public List<string> clicks = new List<string>();
        public List<string> spawns = new List<string>();
        public List<string> combines = new List<string>();
        public int goldAtSpawn = -1;
        public int clickIndex;
        public string stage;        // entering · settling · clicking · waiting · capturing · exiting
        public double stageSince;   // EditorApplication.timeSinceStartup — 도메인 리로드를 넘어 이어진다
        public long lastSize = -1;
        public string prefix;       // 같은 inbox 파일에서 먼저 돈 명령들의 결과(Poll이 채운다)
        // 🔴 prefix == null로 「아직 안 채움」을 가리면 안 된다 — JsonUtility는 null 문자열을 ""로 저장해서, 한 번 저장·로드하면
        //    늘 「채움」으로 보인다. 그래서 Poll이 「⏳ 진행 중」만 든 결과를 먼저 써 버렸다(2026-09-23 outbox 2002). 따로 표시한다.
        public bool prefixReady;
        public string report = "";  // 결과 본문
        public List<GameShotLog> logs = new List<GameShotLog>();
        public int droppedLogs;     // 종류 상한을 넘어 못 실은 로그 수
        public bool failed;
        public int midPlayReloads;  // 플레이 도중 도메인 리로드 횟수(아래 NoteMidPlayReload)
        public bool noCombine, noShop;   // autoloop에서 조합·상점을 뺀다 — 일부러 약한 판(보스 제한 패배 확인용)
        public int mode = (int)DifficultyMode.Normal;   // mode:<난이도> — 기본 보통(09-25 PM 지시: 기억값이 쉬움이라 판 B~F가 전부 쉬움이었다)
        public int prevSavedMode = int.MinValue;         // 사장님 기억값 — 판이 끝나면 되돌린다(MinValue = 원래 없었음)
        public int logsBeforeReload = -1;
    }

    // 같은 메시지 + 같은 스택은 한 줄로 묶어 횟수만 센다 — 똑같은 NRE 50줄이 결과를 다 먹어 다른 예외가 잘리던 것(outbox 1264)을 막는다.
    [Serializable]
    class GameShotLog
    {
        public string type;
        public string message;
        public string stack;        // 첫 몇 줄 + Assets/ 프레임
        public int count;
    }

    static GameShotJob LoadGameShot()
    {
        string json = SessionState.GetString(GameShotKey, "");
        return json.Length == 0 ? null : JsonUtility.FromJson<GameShotJob>(json);
    }

    static void SaveGameShot(GameShotJob job) => SessionState.SetString(GameShotKey, JsonUtility.ToJson(job));

    static string StartGameShot(string[] parts)
    {
        if (parts.Length == 0) return "❌ 사용법: gameshot <파일> [초] [가로x세로] [super:N] [click:<버튼>]...";
        if (Application.isBatchMode) return "❌ gameshot은 켜진 에디터에서만 된다(배치모드엔 Game 뷰가 없다)";
        if (currentId == null) return "❌ gameshot은 inbox로만 받는다(결과를 플레이 모드 뒤에 그 번호로 쓴다)";
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "❌ 이미 플레이 모드다 — 멈춘 뒤 다시 보내세요";
        if (EditorUtility.scriptCompilationFailed) return "❌ 컴파일 오류가 있어 플레이 모드에 못 들어간다(콘솔을 먼저 비우세요)";
        if (LoadGameShot() != null) return "❌ 앞선 gameshot이 아직 안 끝났다";
        // 앞 판이 중간에 죽어 실제 마우스를 끈 채 남겼을 수 있다(EnsureShotMouse) — 켜 두고 시작한다.
        foreach (Mouse m in InputSystem.devices.OfType<Mouse>().Where(m => m.name != ShotMouseName && !m.enabled).ToList())
            InputSystem.EnableDevice(m);
        foreach (Keyboard k in InputSystem.devices.OfType<Keyboard>().Where(k => !k.enabled).ToList())
            InputSystem.EnableDevice(k);

        string name = parts[0].EndsWith(".png") ? parts[0] : parts[0] + ".png";
        GameShotJob job = new GameShotJob { id = currentId, seconds = 3f };
        StringBuilder report = new StringBuilder();

        // 인자는 모양으로 가른다 — 순서를 외울 필요가 없게.
        foreach (string token in parts.Skip(1))
        {
            if (token.StartsWith("boxselect:")) job.clicks.Add("@box:" + token.Substring(10));
            else if (token.StartsWith("wait:"))
            {
                if (!float.TryParse(token.Substring(5), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w) || w < 0f)
                    return $"❌ wait: 뒤엔 초: {token}";
                job.clicks.Add("@wait:" + token.Substring(5));
            }
            else if (token == "buttons") job.clicks.Add("@buttons");
            else if (token == "cardpair") job.clicks.Add("@cardpair");
            else if (token.StartsWith("call:")) job.clicks.Add("@call:" + token.Substring(5));
            else if (token.StartsWith("rclickpt:")) job.clicks.Add("@rcpt:" + token.Substring(9));
            else if (token == "autoloop") job.autoLoop = true;
            else if (token == "nocombine") job.noCombine = true;
            else if (token == "noshop") job.noShop = true;
            else if (token.StartsWith("mode:"))
            {
                string want = token.Substring(5);
                DifficultyMode? found = null;
                foreach (DifficultyMode m in Enum.GetValues(typeof(DifficultyMode)))
                    if (m.KoreanName() == want || m.ToString().Equals(want, StringComparison.OrdinalIgnoreCase)) found = m;
                if (found == null) return $"❌ mode: 뒤엔 난이도(쉬움·보통·어려움·지옥·신·악몽): {token}";
                job.mode = (int)found.Value;
            }
            else if (token.StartsWith("rounds:"))
            {
                if (!int.TryParse(token.Substring(7), out job.watchRounds) || job.watchRounds < 1) return $"❌ rounds: 뒤엔 1 이상 정수: {token}";
            }
            else if (token.StartsWith("snap:")) job.clicks.Add("@snap:" + token.Substring(5));
            else if (token.StartsWith("select:") || token.StartsWith("rclick:"))
            {
                bool left = token.StartsWith("select:");
                string target = token.Substring(left ? 7 : 7);
                if (target.Length == 0) return $"❌ {token}: 대상 이름을 주세요";
                job.clicks.Add((left ? "@sel:" : "@rc:") + target);
            }
            else if (token.StartsWith("click?:"))
            {
                if (token.Length == 7) return "❌ click?: 뒤에 버튼 이름이나 글자를 주세요";
                job.clicks.Add("?" + token.Substring(7));   // 앞의 ?가 「없으면 건너뜀」 표시
            }
            else if (token.StartsWith("click:"))
            {
                if (token.Length == 6) return "❌ click: 뒤에 버튼 이름이나 글자를 주세요";
                job.clicks.Add(token.Substring(6));
            }
            else if (token.StartsWith("combine:"))
            {
                string recipeName = token.Substring(8);
                if (FindRecipe(recipeName) == null) return $"❌ 레시피를 못 찾음: {recipeName}(Assets/Data/Recipes의 에셋 이름이나 결과 유닛 이름)";
                job.combines.Add(recipeName);
            }
            else if (token.StartsWith("spawn:"))
            {
                string unitName = token.Substring(6).Split('@')[0];
                if (AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{unitName}.asset") == null)
                    return $"❌ 유닛 에셋 없음: Assets/Data/Units/Roster/{unitName}.asset";
                job.spawns.Add(token.Substring(6));   // 「이름@corner」 꼴 그대로 둔다 — SpawnForShot이 가른다
            }
            else if (token.StartsWith("super:"))
            {
                if (!int.TryParse(token.Substring(6), out job.superSize) || job.superSize < 1 || job.superSize > 4)
                    return $"❌ super:는 1~4: {token}";
            }
            else if (token.ToLowerInvariant().Contains('x'))
            {
                string[] wh = token.ToLowerInvariant().Split('x');
                if (wh.Length != 2 || !uint.TryParse(wh[0], out uint w) || !uint.TryParse(wh[1], out uint h) || w == 0 || h == 0)
                    return $"❌ 해상도는 가로x세로로: {token}";
                PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
                PlayModeWindow.SetCustomRenderingResolution(w, h, "Claude gameshot");
                report.AppendLine($"   Game 뷰 해상도를 {w}×{h}로 고정했다(Game 뷰에 남는다)");
            }
            else if (float.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) && s >= 0)
                job.seconds = s;
            else
                return $"❌ 모르는 인자: {token}";
        }

        if (!parts.Skip(1).Any(t => !t.StartsWith("click:") && t.ToLowerInvariant().Contains('x')))
        {
            PlayModeWindow.GetRenderingResolution(out uint viewWidth, out uint viewHeight);
            report.AppendLine($"   ⚠️ 해상도를 안 박았다 — 지금 Game 뷰 {viewWidth}×{viewHeight} 그대로 찍는다. " +
                              "글씨·UI 판정용이면 1920x1080처럼 해상도를 주고 다시 찍을 것(작은 창에선 UI가 줄어 글자가 뭉개진다).");
        }

        job.file = Path.GetFullPath(Path.Combine(Folder, "shots", name));
        Directory.CreateDirectory(Path.GetDirectoryName(job.file));
        // 같은 이름의 옛 사진이 남아 있으면 「찍혔다」로 오판한다 — 먼저 지운다.
        if (File.Exists(job.file)) File.Delete(job.file);

        job.stage = "entering";
        job.stageSince = EditorApplication.timeSinceStartup;
        if (job.watchRounds > 0) job.watchDeadline = EditorApplication.timeSinceStartup + 90 + job.watchRounds * 85;
        job.report = report.ToString();
        SaveGameShot(job);
        // 난이도를 명시적으로 — DifficultyManager.Awake가 PlayerPrefs 기억값을 자동으로 건다(판 B~F가 전부 쉬움이었다, PM 09-25).
        //    게임의 실제 경로(기억해서 시작)를 타도록 기억값을 이 판의 난이도로 써 두고, 판이 끝나면 사장님 원래 값으로 되돌린다.
        job.prevSavedMode = PlayerPrefs.HasKey(DifficultyManager.SavedModeKey) ? PlayerPrefs.GetInt(DifficultyManager.SavedModeKey) : int.MinValue;
        PlayerPrefs.SetInt(DifficultyManager.SavedModeKey, job.mode);
        PlayerPrefs.Save();
        SaveGameShot(job);
        EditorApplication.isPlaying = true;   // 이 update가 끝난 뒤에 들어간다

        string clicks = job.clicks.Count > 0 ? $" · 누를 버튼 {string.Join(" → ", job.clicks)}" : "";
        if (job.spawns.Count > 0) clicks += $" · 세울 유닛 {string.Join(", ", job.spawns)}";
        return $"⏳ 플레이 모드로 들어가 찍는다(씬 {SceneManager.GetActiveScene().name}{clicks} · 마지막 뒤 {job.seconds:F1}초 · ×{job.superSize}) — 결과는 나온 뒤 이 파일에 이어 쓴다";
    }

    static double nextGameShotTick;

    static void TickGameShot()
    {
        if (EditorApplication.timeSinceStartup < nextGameShotTick) return;
        nextGameShotTick = EditorApplication.timeSinceStartup + 0.25;

        GameShotJob job = LoadGameShot();
        if (job == null || !job.prefixReady) return;   // 아직 Poll이 명령 파일을 다 안 돌렸다

        double inStage = EditorApplication.timeSinceStartup - job.stageSince;
        // 플레이 도중 리로드가 났으면 그 판은 더 돌려도 믿을 수 없다(가상 마우스·정적 기록도 날아간다) — 바로 끝낸다(09-24 loop6).
        if (job.midPlayReloads > 0 && !job.failed && job.stage != "entering" && job.stage != "exiting")
        {
            FailGameShot(job, $"플레이 도중 도메인 리로드({job.stage} 단계) — 이 판은 무효라 여기서 끝낸다. 다른 세션이 Assets를 안 건드릴 때 다시 보낼 것");
            return;
        }
        if (job.watchRounds > 0 && EditorApplication.isPlaying && job.stage != "entering" && job.stage != "exiting" && job.stage != "capturing")
        {
            RoundWatch(job);
            job = LoadGameShot();
            if (job == null) return;
        }
        if (job.stage != "entering" && job.stage != "exiting" && !EditorApplication.isPlaying)
        {
            FailGameShot(job, $"{job.stage} 중에 플레이 모드가 끝났다(누가 멈췄거나 예외로 중단)");
            return;
        }

        switch (job.stage)
        {
            case "entering":
                if (EditorApplication.isPlaying && !EditorApplication.isPaused)
                    Advance(job, "settling");
                else if (!EditorApplication.isPlayingOrWillChangePlaymode && inStage > 2)
                    FailGameShot(job, "플레이 모드 진입이 취소됐다(컴파일 오류·저장 대화상자 등). 콘솔을 보세요");
                else if (inStage > GameShotEnterTimeout)
                    FailGameShot(job, $"{GameShotEnterTimeout}초 안에 플레이 모드에 못 들어갔다");
                break;

            case "settling":
                if (inStage >= GameShotSettle) Advance(job, job.clicks.Count > 0 ? "clicking" : job.spawns.Count + job.combines.Count > 0 ? "spawning" : "waiting");
                break;

            case "clicking":
            {
                if (job.clickIndex > 0 && inStage < GameShotClickGap) break;   // 앞 클릭의 결과가 화면에 반영될 틈
                string target = job.clicks[job.clickIndex];
                if (target.StartsWith("@call:"))
                {
                    string callResult;
                    try { callResult = Call(target.Substring(6)); }
                    catch (Exception e) { callResult = $"❌ 호출 중 예외: {e.InnerException?.Message ?? e.Message}"; }
                    job.report += $"   📞 call {target.Substring(6)}:\n{callResult}\n";
                    job.clickIndex++;
                    Advance(job, job.clickIndex < job.clicks.Count ? "clicking" : job.spawns.Count + job.combines.Count > 0 ? "spawning" : "waiting");
                    break;
                }
                if (target == "@cardpair")
                {
                    // 여럿 고른 상태에서 같은 종류가 2기 이상인 카드를 누른다 — 사람이 겹치는 카드를 보고 누르는 것과 같다(같은 흔함 둘이 흔한 조합 재료).
                    SelectionManager pairSel = UnityEngine.Object.FindFirstObjectByType<SelectionManager>();
                    var pair = pairSel == null ? null : pairSel.Selected.Where(x => x != null && x.TryGetComponent(out UnitIdentity _))
                        .GroupBy(x => x.GetComponent<UnitIdentity>().Data?.unitName).FirstOrDefault(g => g.Key != null && g.Count() >= 2);
                    if (pair == null) job.report += "   🃏 cardpair: 지금 선택에 같은 종류 2기 이상이 없다 — 건너뜀\n";
                    else
                    {
                        string pressed = ClickButton(pair.Key);   // 카드 글자 = 유닛 이름(Card0「임장혁」)
                        job.report += pressed != null ? $"   🃏 cardpair: 「{pair.Key}」×{pair.Count()} 카드를 누름 → {pressed}\n" : $"   🃏 cardpair: 「{pair.Key}」 카드를 못 찾음\n";
                    }
                    job.clickIndex++;
                    Advance(job, job.clickIndex < job.clicks.Count ? "clicking" : job.spawns.Count + job.combines.Count > 0 ? "spawning" : "waiting");
                    break;
                }
                if (target.StartsWith("@wait:") || target == "@buttons" || target.StartsWith("@snap:"))
                {
                    if (target.StartsWith("@wait:"))
                    {
                        if (inStage < float.Parse(target.Substring(6), System.Globalization.CultureInfo.InvariantCulture)) break;
                        job.report += $"   ⏱ {target.Substring(6)}초 기다림\n";
                    }
                    else if (target == "@buttons")
                        job.report += "   🔘 지금 누를 수 있는 버튼:\n" + ListButtons() + "\n";
                    else
                    {
                        string snapName = target.Substring(6);
                        string snapPath = Path.GetFullPath(Path.Combine(Folder, "shots", snapName.EndsWith(".png") ? snapName : snapName + ".png"));
                        ScreenCapture.CaptureScreenshot(snapPath);   // 프레임 끝에 비동기로 써진다 — 판이 끝날 즈음엔 파일이 있다
                        (string snapSlots, int snapTotal) = ReadWispSlots();
                        RoundManager snapRound = UnityEngine.Object.FindFirstObjectByType<RoundManager>();
                        job.report += $"   📸 중간 캡처 {snapPath}(라운드 {snapRound?.CurrentRound} · 위습 칸 「{snapSlots}」 합계 {snapTotal} · 실제 내 위습 {CountMyWisps()})\n";
                    }
                    job.clickIndex++;
                    Advance(job, job.clickIndex < job.clicks.Count ? "clicking" : job.spawns.Count + job.combines.Count > 0 ? "spawning" : "waiting");
                    break;
                }
                if (target.StartsWith("@sel:") || target.StartsWith("@rc:") || target.StartsWith("@box:") || target.StartsWith("@rcpt:"))
                {
                    if (!StepPointer(job, target, inStage)) break;   // 아직 진행 중
                    job.clickIndex++;
                    job.pointerPhase = 0;
                    SkipIfSelectionWrong(job, target);
                    Advance(job, job.clickIndex < job.clicks.Count ? "clicking" : job.spawns.Count + job.combines.Count > 0 ? "spawning" : "waiting");
                    break;
                }
                bool optional = target.StartsWith("?");
                if (optional) target = target.Substring(1);
                if (target == "@shopspend")
                {
                    job.report += SpendAtShop(job);
                    job.clickIndex++;
                    Advance(job, job.clickIndex < job.clicks.Count ? "clicking" : job.spawns.Count + job.combines.Count > 0 ? "spawning" : "waiting");
                    break;
                }
                string clicked = target == "@shopany" ? ClickFirstShopSlot(job) : ClickButton(target);
                if (clicked == null && optional && inStage > GameShotOptionalClickSearch)
                {
                    // 「없음」과 「있지만 꺼짐·못 누름」을 가른다(도박소 첫 칸이 「없음」으로만 나와 이름이 틀렸는지 흐린 건지 몰랐다, outbox 2109).
                    string why = string.Join(", ", UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(b => b.gameObject.name == target || ButtonLabel(b) == target).Take(3)
                        .Select(b => $"{b.gameObject.name}「{ButtonLabel(b)}」 켜짐 {b.IsActive()} · 누를 수 있음 {b.IsInteractable()}"));
                    job.report += $"   🖱 {job.clickIndex + 1}번째 클릭: 「{target}」 없음{(why.Length > 0 ? $"(있긴 함: {why})" : "")} — 선택 클릭이라 건너뜀\n";
                    job.clickIndex++;
                    // 위습 칸을 못 눌렀으면 바로 뒤의 우클릭도 버린다 — 안 버리면 그때 선택돼 있던 **유닛**이 포탈로 걸어갔다(outbox 2109: 노태현·강재규·황정기가 가챠섬으로).
                    if (job.clickIndex < job.clicks.Count && job.clicks[job.clickIndex].StartsWith("@rc:"))
                    {
                        job.report += $"   🖱 {job.clickIndex + 1}번째: 앞 클릭을 건너뛰어 「{job.clicks[job.clickIndex]}」도 건너뜀\n";
                        job.clickIndex++;
                    }
                    Advance(job, job.clickIndex < job.clicks.Count ? "clicking" : job.spawns.Count + job.combines.Count > 0 ? "spawning" : "waiting");
                    break;
                }
                if (clicked != null)
                {
                    job.report += $"   🖱 {job.clickIndex + 1}번째 클릭: {clicked}\n";
                    job.clickIndex++;
                    Advance(job, job.clickIndex < job.clicks.Count ? "clicking" : job.spawns.Count + job.combines.Count > 0 ? "spawning" : "waiting");
                }
                else if (inStage > GameShotClickSearch)
                {
                    FailGameShot(job, $"{GameShotClickSearch}초 동안 「{target}」 버튼을 못 찾았다. 그때 보이던 누를 수 있는 버튼:\n" + ListButtons());
                }
                break;
            }

            case "spawning":
                if (job.clickIndex > 0 && inStage < GameShotClickGap) break;   // 마지막 클릭(난이도 등)이 반영될 틈
                spawnedUnits.Clear();
                shotUnits.Clear();
                combineBefore.Clear();
                theoreticalDps = 0f;
                enemyPresentSeconds = 0f;
                lastWatchTime = watchStartTime = EditorApplication.timeSinceStartup;
                lastGold = int.MinValue;
                eventLog.Clear();
                for (int i = 0; i < job.spawns.Count; i++)
                    job.report += "   " + SpawnForShot(job.spawns[i], i, job.spawns.Count) + "\n";
                foreach (string recipeName in job.combines)
                    job.report += SpawnCombineMaterials(recipeName);
                int good = spawnedUnits.Count(u => u.onMesh && u.inRange);
                if (job.spawns.Count > 0)   // 조합 재료만 세운 판에선 배치 유효성을 따지지 않는다(「2/0기」가 찍혔다, 09-24 outbox 2054)
                job.report += $"   {(good == job.spawns.Count ? "✅" : "⚠️")} 배치 {good}/{job.spawns.Count}기가 NavMesh 위 + 경로가 사거리 안" +
                              (good == job.spawns.Count ? "" : $" — 어긋난 유닛: {string.Join(", ", spawnedUnits.Where(u => !(u.onMesh && u.inRange)).Select(u => $"{u.name}({(u.onMesh ? "" : "NavMesh 밖 ")}{(u.inRange ? "" : "사거리 밖")})"))} → **이 판의 처치·골드는 유닛 수만큼 믿으면 안 된다**") + "\n";
                job.goldAtSpawn = PlayerContext.GetOccupied(0)?.GoldWallet?.Gold ?? -1;
                hitWatch.Clear();
                roundLog.Clear();
                bossTracks.Clear();
                bossKillSignals.Clear();
                watchedRound = -1;
                maxLaneEnemies = 0;
                Advance(job, job.combines.Count > 0 ? "combining" : "waiting");
                break;

            case "combining":
                if (inStage < GameShotClickGap) break;   // 재료가 인벤토리에 올라가고 한 프레임 이상 지나게
                foreach (string recipeName in job.combines)
                    job.report += RunCombine(recipeName);
                Advance(job, "waiting");
                break;

            case "waiting":
                if (job.spawns.Count + job.combines.Count > 0) WatchLaneHits();
                if (job.watchRounds > 0 ? job.finishNow : inStage >= job.seconds)
                {
                    if (job.clicks.Any(c => c.StartsWith("@")))
                    {
                        // select:/rclick:을 쓴 판은 끝에 내 유닛 목록을 남긴다 — 위습이 포탈에 들어가 뽑기가 됐는지 여기서 본다.
                        var mine = Selectable.All.Where(x => x != null && (!x.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == LocalPlayer.LocalPlayerId))
                            .GroupBy(x => x.name).Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key);
                        job.report += $"   🧾 찍는 순간 내 유닛: {string.Join(", ", mine)}\n";
                        int wispsNow = CountMyWisps();
                        (string slotText, int slotTotal) = ReadWispSlots();
                        job.report += $"   🔢 위습 칸 「{slotText}」 합계 {slotTotal} · 실제 내 위습 {wispsNow}{(slotTotal == wispsNow ? " ✅ 일치" : " ⚠️ 다름(칸은 주기적으로 갱신)")}\n";
                        // 적과 아군 크기 비교(렌더러 경계 — 적 자리표시 상자엔 어깨가 없어 키·폭으로 잰다). 09-24 PM: 「적 상자가 유닛보다 커 보인다」.
                        EnemyDummy sampleEnemy = EnemyDummy.Active.FirstOrDefault(e => e != null && e.LaneIndex == 0)
                                                 ?? EnemyDummy.Active.FirstOrDefault(e => e != null);
                        job.report += $"   👾 그 순간 적 전체 {EnemyDummy.Active.Count(e => e != null)} · 0번 레인 {EnemyDummy.CountInLane(0)}" +
                                      (sampleEnemy != null ? $" · 잰 적의 레인 {sampleEnemy.LaneIndex}" : "") + "\n";
                        UnitIdentity sampleUnit = UnityEngine.Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None)
                            .FirstOrDefault(u => u != null && u.Data != null && u.GetComponent<Wisp>() == null &&
                                                 (!u.TryGetComponent(out OwnedByPlayer uo) || uo.OwnerId == LocalPlayer.LocalPlayerId));
                        job.report += $"   📏 크기(렌더러 경계): 적 {DescribeBody(sampleEnemy != null ? sampleEnemy.gameObject : null)} · 아군 {DescribeBody(sampleUnit != null ? sampleUnit.gameObject : null)}\n";
                        // 흔함보다 높은 등급(조합 결과)의 자리 — 조합 결과가 레인 가운데 서는지(CombineSystem → LaneCenter) 실제 경로로 본다.
                        LaneMarker laneForResult = LaneMarker.Get(0);
                        foreach (UnitIdentity higher in UnityEngine.Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None)
                                     .Where(u => u != null && u.Data != null && u.Data.grade != UnitGrade.Common &&
                                                 (!u.TryGetComponent(out OwnedByPlayer ow) || ow.OwnerId == LocalPlayer.LocalPlayerId) && u.GetComponent<Wisp>() == null))
                        {
                            Vector3 hp = higher.transform.position;
                            float fromCenter = laneForResult != null ? Vector2.Distance(new Vector2(hp.x, hp.z), new Vector2(laneForResult.LaneCenter.x, laneForResult.LaneCenter.z)) : -1f;
                            job.report += $"   🏅 흔함 위 유닛 {higher.name}({higher.Data.grade}) 위치 {hp.ToString("F0")} · 레인 중심에서 수평 {fromCenter:F1}\n";
                        }
                        SelectionManager selectionNow = UnityEngine.Object.FindFirstObjectByType<SelectionManager>();
                        if (selectionNow != null)
                            foreach (Selectable chosen in selectionNow.Selected.Where(x => x != null))
                            {
                                Vector3 at = chosen.transform.position;
                                string toTarget = lastPointerTarget != null
                                    ? $" · 마지막 대상 {lastPointerTarget.name}까지 수평 {Vector2.Distance(new Vector2(at.x, at.z), new Vector2(lastPointerTarget.transform.position.x, lastPointerTarget.transform.position.z)):F1}"
                                    : "";
                                string moving = chosen.TryGetComponent(out NavMeshAgent a) && a.isOnNavMesh ? $" · 남은 길 {a.remainingDistance:F1} · 속도 {a.velocity.magnitude:F1}" : "";
                                // 싸울 수 있는 자리인가 — 0번 레인 적 경로(가장 가까운 변)까지 거리와 그 유닛 사거리.
                                string reach = "";
                                LaneMarker laneNow = LaneMarker.Get(0);
                                WaypointPath pathNow = laneNow != null ? LanePathNear(laneNow.LaneCenter) : null;
                                if (pathNow != null && pathNow.PointCount > 1 && chosen.TryGetComponent(out UnitAttacker atk))
                                {
                                    float toPath = float.MaxValue;
                                    for (int i = 0; i + 1 < pathNow.PointCount; i++) toPath = Mathf.Min(toPath, DistanceToSegmentXZ(at, pathNow.GetPoint(i), pathNow.GetPoint(i + 1)));
                                    reach = $" · 적 경로까지 {toPath:F0} / 사거리 {atk.AttackRange:F0}{(toPath <= atk.AttackRange ? " ✅ 닿음" : " ❌ 안 닿음")}";
                                }
                                job.report += $"   📍 선택 유닛 {chosen.name} 위치 {at.ToString("F0")}{toTarget}{moving}{reach}\n";
                            }
                    }
                    if (job.spawns.Count + job.combines.Count > 0)
                    {
                        if (job.spawns.Count > 0) job.report += DescribeLaneHits(job, inStage);
                        foreach (GameObject shotUnit in shotUnits)
                            job.report += $"   👁 찍는 순간 {(shotUnit != null ? shotUnit.name : "(사라짐)")} 위치 {(shotUnit != null ? shotUnit.transform.position.ToString("F0") : "-")}: {DescribeOnScreen(shotUnit)}\n";
                    }
                    ScreenCapture.CaptureScreenshot(job.file, job.superSize);   // 이 프레임 끝에 Game 뷰(UI 포함)를 파일로 쓴다
                    Advance(job, "capturing");
                }
                break;

            case "capturing":
            {
                long size = File.Exists(job.file) ? new FileInfo(job.file).Length : -1;
                if (size > 0 && size == job.lastSize)   // 두 번 연속 크기가 같으면 다 써진 것
                {
                    job.report += "   " + DescribePng("📷", job.file) + "\n";
                    ReleaseShotMouse();   // 플레이 모드를 나가기 전에 — 가상 장치가 에디터에 남지 않게
                    Advance(job, "exiting");
                    EditorApplication.isPlaying = false;
                }
                else if (inStage > GameShotCaptureTimeout)
                {
                    FailGameShot(job, $"{GameShotCaptureTimeout}초 안에 캡처 파일이 안 생겼다 — Game 뷰 창이 닫혀 있거나 다른 탭 뒤에 숨어 있으면 " +
                                      "ScreenCapture가 아무것도 안 쓴다. Game 뷰를 보이게 둔 채 다시 보내세요");
                }
                else
                {
                    job.lastSize = size;
                    SaveGameShot(job);
                }
                break;
            }

            case "exiting":
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    FinishGameShot(job);
                else if (inStage > GameShotExitTimeout)
                {
                    job.report += $"   ⚠️ {GameShotExitTimeout}초가 지나도 플레이 모드가 안 끝났다 — 결과만 먼저 쓴다(에디터는 아직 플레이 중)\n";
                    FinishGameShot(job);
                }
                break;
        }
    }

    // navlane <레인 번호> [가로 칸 수]
    // 레인 섬 위를 격자로 내려 찍어 칸마다 걸을 수 있는지 글자 지도로 그린다.
    //
    // 🔴 **정식 검사다 — 맵 배율(WorldScale)·NavMesh 복셀·섬 높이(IslandTop)·바다 상자를 건드린 뒤엔 맵 생성 다음에 꼭 돌린다.**
    //    2026-09-23 사고: 맵을 4.167배로 키우며 굽기 무게를 줄이려고 복셀을 0.5 → 2.0으로 올렸는데(773dfe5b), 섬 윗면(y 1)과
    //    바다 윗면(y 0)의 높이 차 1은 그대로였다. 한 복셀보다 얕은 두 층을 Recast가 한 층으로 합치면서 영역 번호가 큰 쪽(Sea=3)이
    //    이겨, **레인 섬 윗면의 89%가 바다 영역**이 됐다(outbox 2026: 3000칸 중 걸을 수 있음 344). 지상 유닛은 Sea를 못 걸어
    //    레인 안에 못 들어가고 우리·가장자리 줄에서만 싸웠다. 적은 WaypointMover라 멀쩡히 돌아서 겉으론 안 보였다 —
    //    spawn: 판에서 「어디를 노려도 가장 가까운 걸을 수 있는 면이 130~260 떨어져 있다」는 곁가지 관찰로 잡혔다(outbox 2024).
    //    합격선: 섬 안쪽 땅 칸 대부분이 #, ~는 섬 밖(바다)만. 결과 첫 줄의 높이 차 경고가 없어야 한다.
    //   # 걸을 수 있음(Walkable) · ~ 바다 영역 · . NavMesh 없음 · 공백 = 섬 밖(콜라이더 없음) · E 적 경로 점 근처
    // 못 걷는 칸은 맨 위 콜라이더 이름을 세서 「무엇이 덮고 있나」를 같이 적는다.
    static string NavLane(int laneIndex, int columns)
    {
        // 🔴 편집 모드에서 맵을 막 새로 만든 직후엔 물리 엔진이 새 오브젝트의 위치를 아직 모른다 — 콜라이더 경계가 원점에 붙고
        //    레이캐스트도 원점 근처 오브젝트만 맞힌다. 2026-09-23 outbox 1305: 섬 경계 x −1~1·칸 0.0으로 2704칸이 전부
        //    「Lane1_유닛우리_칸막이1」을 맞히고 「걸을 수 있음 0%」라는 그럴듯한 숫자를 냈다. 재기 전에 반드시 동기화한다.
        Physics.SyncTransforms();

        LaneMarker lane = UnityEngine.Object.FindObjectsByType<LaneMarker>(FindObjectsSortMode.None).FirstOrDefault(l => l.LaneIndex == laneIndex);
        if (lane == null) return $"❌ {laneIndex}번 레인 LaneMarker 없음";

        // 섬 경계는 **렌더러**로 잰다(물리 동기화와 무관). 레인 섬 판 자신과 자식 중, 섬 판 중심을 품는 큰 것만.
        Bounds? island = null;
        foreach (Renderer rend in lane.GetComponents<Renderer>().Concat(lane.GetComponentsInChildren<Renderer>(true)))
        {
            if (!rend.bounds.Contains(new Vector3(lane.LaneCenter.x, rend.bounds.center.y, lane.LaneCenter.z))) continue;
            if (island == null) island = rend.bounds;
            else { Bounds b = island.Value; b.Encapsulate(rend.bounds); island = b; }
        }
        // 🔴 빈 경계면 숫자를 내지 않는다 — 조용히 틀린 값(0%)이 제일 위험하다(위 1305 사고). 레인은 수백 단위라 50 미만이면 못 찾은 것이다.
        if (island == null || island.Value.size.x < 50f || island.Value.size.z < 50f)
            return $"❌ {lane.name}(레인 {laneIndex})의 섬 경계를 못 잡았다 — {(island == null ? "렌더러 없음" : $"크기 {island.Value.size}")}. " +
                   $"LaneMarker 위치 {lane.LaneCenter}. 숫자를 내지 않고 끝낸다(빈 경계로 재면 「0%」 같은 거짓 숫자가 나온다).";

        // 🔴 앞치마·우리도 잰다(2026-09-24 PM). 레인 섬이 「필드만」이 된 뒤 새 유닛이 생기는 우리와 그 앞 앞치마는 **섬 밖**인데,
        //    예전 navlane은 섬 경계만 찍어서 앞치마를 레인간 가로벽이 통째로 덮은 사고(cc002dea, z 1089.5~1288.8 100%)를 못 봤다.
        //    이제 우리 경계(렌더러 + 첫 줄 칸 자리)까지 넓혀 찍고, 섬 안과 섬 밖(앞치마·우리)을 따로 센다.
        Bounds field = island.Value;
        Bounds area = field;
        bool hasPen = false;
        if (lane.UnitPen != null)
        {
            foreach (Renderer penRenderer in lane.UnitPen.GetComponentsInChildren<Renderer>()) { area.Encapsulate(penRenderer.bounds); hasPen = true; }
            foreach (Vector3 slot in lane.FirstRowSlotPositions()) { area.Encapsulate(slot); hasPen = true; }
        }
        area.Expand(new Vector3(area.size.x * 0.15f, 0f, area.size.z * 0.15f));   // 섬 바깥 경로까지 보이게 여유
        float step = area.size.x / columns;
        int rows = Mathf.CeilToInt(area.size.z / step);
        if (step < 1f) return $"❌ 칸 간격 {step:F2}로 재면 모든 칸이 한 자리를 찍는다 — 섬 경계 {area.size}를 다시 보세요. 숫자를 내지 않는다.";

        WaypointPath path = LanePathNear(lane.LaneCenter);
        int seaArea = NavMesh.GetAreaFromName("Sea");
        int walk = 0, sea = 0, none = 0, air = 0;
        int outWalk = 0, outSea = 0, outNone = 0;   // 섬 밖이면서 앞치마·우리 쪽(섬과 우리 사이 z)인 칸
        float apronMinZ = Mathf.Min(area.min.z, field.min.z), apronMaxZ = field.min.z;
        Dictionary<string, int> blockers = new Dictionary<string, int>();
        StringBuilder map = new StringBuilder();
        for (int row = rows - 1; row >= 0; row--)   // 위(+z)가 윗줄
        {
            for (int col = 0; col < columns; col++)
            {
                float x = area.min.x + (col + 0.5f) * step, z = area.min.z + (row + 0.5f) * step;
                Vector3 top = new Vector3(x, area.max.y + 500f, z);
                bool nearPath = false;
                if (path != null)
                    for (int i = 0; i + 1 < path.PointCount && !nearPath; i++)
                        nearPath = DistanceToSegmentXZ(new Vector3(x, 0f, z), path.GetPoint(i), path.GetPoint(i + 1)) < step * 0.5f;

                if (!Physics.Raycast(top, Vector3.down, out RaycastHit ground, 2000f, ~0, QueryTriggerInteraction.Ignore))
                {
                    map.Append(nearPath ? 'E' : ' ');
                    air++;
                    continue;
                }
                char mark;
                bool apron = hasPen && z < apronMaxZ && x >= field.min.x && x <= field.max.x;
                if (NavMesh.SamplePosition(ground.point, out NavMeshHit hit, step * 0.5f, NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.y - ground.point.y) < 6f)
                {
                    if (hit.mask == 1 << seaArea) { mark = '~'; sea++; if (apron) outSea++; }
                    else { mark = '#'; walk++; if (apron) outWalk++; }
                }
                else
                {
                    mark = '.';
                    none++;
                    if (apron) outNone++;
                    string key = ground.collider.name;
                    blockers[key] = blockers.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                map.Append(nearPath ? 'E' : mark);
            }
            map.Append('\n');
        }

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);

        // 섬 윗면과 바다 콜라이더 윗면의 높이 차 — 복셀 두 칸보다 얕으면 굽기에서 한 층으로 합쳐져 영역이 섞인다(위 🔴).
        float islandTop = lane.LaneCenter.y;
        if (Physics.Raycast(lane.LaneCenter + Vector3.up * 500f, Vector3.down, out RaycastHit centerHit, 2000f, ~0, QueryTriggerInteraction.Ignore))
            islandTop = centerHit.point.y;
        GameObject seaBox = GameObject.Find("Sea");
        float? seaTop = seaBox != null && seaBox.TryGetComponent(out Collider seaCollider) ? seaCollider.bounds.max.y : (float?)null;
        float voxel = UnityEngine.Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsSortMode.None)
            .Select(v => v.overrideVoxelSize ? v.voxelSize : settings.agentRadius / 3f).DefaultIfEmpty(0f).Max();
        string gap = seaTop == null ? "바다 콜라이더(Sea) 못 찾음"
            : $"섬 윗면 y {islandTop:F2} − 바다 윗면 y {seaTop.Value:F2} = {islandTop - seaTop.Value:F2} (복셀 {voxel:F2}의 2칸 = {voxel * 2f:F2})" +
              (islandTop - seaTop.Value < voxel * 2f ? " 🔴 얕다 — 섬과 바다가 한 층으로 합쳐져 섬이 바다 영역이 될 수 있다" : " ✅");
        int land = walk + sea + none;
        StringBuilder sb = new StringBuilder($"🗺 {lane.name}(레인 {laneIndex}) 섬 경계 x {island.Value.min.x:F0}~{island.Value.max.x:F0} · z {island.Value.min.z:F0}~{island.Value.max.z:F0} · 칸 {step:F1}\n");
        sb.AppendLine($"   높이 차: {gap} · 설계값 MapLayout.IslandTop {MapLayout.IslandTop:F2}" +
                      (Mathf.Abs(islandTop - MapLayout.IslandTop) > 0.5f ? " ⚠️ 잰 섬 윗면과 설계값이 다르다 — 재는 자리(광선이 맞힌 것)를 의심할 것" : ""));
        int outLand = outWalk + outSea + outNone;
        if (hasPen && step > 20f)
            sb.AppendLine($"   ⚠️ 칸 간격 {step:F1} — 앞치마·우리처럼 좁은 구역은 벽 칸 비중이 커져 %가 낮게 나온다(09-24: 30칸 45% · 60칸 69%, 같은 땅). 판정은 `navlane {laneIndex} 60`으로.");
        sb.AppendLine(hasPen
            ? $"   섬 밖 앞치마·우리 구역(z {area.min.z:F0}~{apronMaxZ:F0}, 섬 가로폭 안): 칸 {outLand}개 중 걸을 수 있음 {outWalk}({(outLand > 0 ? 100f * outWalk / outLand : 0):F0}%) · 바다 영역 {outSea} · NavMesh 없음 {outNone}"
            : "   ⚠️ 우리(UnitPen)를 못 찾아 섬 필드만 쟀다 — 앞치마·우리는 측정 밖");
        sb.AppendLine($"   (전체) 땅 칸 {land}개 중 걸을 수 있음 {walk}({(land > 0 ? 100f * walk / land : 0):F0}%) · 바다 영역 {sea} · NavMesh 없음 {none}({(land > 0 ? 100f * none / land : 0):F0}%) · 콜라이더 없음 {air}");
        sb.AppendLine($"   굽기 설정(에이전트 0): 반경 {settings.agentRadius} · 높이 {settings.agentHeight} · 오르기 {settings.agentClimb} · 경사 {settings.agentSlope}°");
        foreach (var surface in UnityEngine.Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsSortMode.None))
            sb.AppendLine($"   NavMeshSurface {surface.name}: 에이전트 {surface.agentTypeID} · 수집 {surface.collectObjects} · 기하 {surface.useGeometry} · 레이어 {surface.layerMask.value} · " +
                          $"복셀 {(surface.overrideVoxelSize ? surface.voxelSize.ToString() : "자동")} · 최소 영역 {surface.minRegionArea}");
        if (blockers.Count > 0)
            sb.AppendLine("   NavMesh 없는 칸의 맨 위 콜라이더: " + string.Join(" · ", blockers.OrderByDescending(b => b.Value).Take(8).Select(b => $"{b.Key} {b.Value}")));
        sb.AppendLine("   # 걸을 수 있음 · ~ 바다 영역 · . NavMesh 없음 · 공백 섬 밖 · E 적 경로(위가 +z):");
        sb.Append(map);
        return sb.ToString();
    }

    static float DistanceToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
        Vector2 AB = B - A;
        float t = AB.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(P - A, AB) / AB.sqrMagnitude) : 0f;
        return Vector2.Distance(P, A + AB * t);
    }

    // ── combine: (에디터 촬영 전용) ──
    static readonly HashSet<UnitIdentity> combineBefore = new HashSet<UnitIdentity>();

    static CombineRecipe FindRecipe(string name)
    {
        string nfc = name.Normalize(NormalizationForm.FormC);
        foreach (string guid in AssetDatabase.FindAssets("t:CombineRecipe", new[] { "Assets/Data/Recipes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CombineRecipe recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(path);
            if (recipe == null) continue;
            if (Path.GetFileNameWithoutExtension(path).Normalize(NormalizationForm.FormC) == nfc) return recipe;
            if (recipe.result != null && recipe.result.name.Normalize(NormalizationForm.FormC) == nfc) return recipe;
        }
        return null;
    }

    // 재료를 실제 뽑기 자리(우리)에 세운다 — 플레이어가 조합하는 재료도 거기서 온다.
    static string SpawnCombineMaterials(string recipeName)
    {
        CombineRecipe recipe = FindRecipe(recipeName);
        StringBuilder sb = new StringBuilder($"   🧪 조합 준비 {recipeName} → 결과 {recipe.result?.name}\n");
        foreach (RecipeIngredient ingredient in recipe.ingredients ?? new List<RecipeIngredient>())
        {
            if (ingredient.kind != IngredientKind.SpecificUnit || ingredient.unit == null)
            {
                sb.AppendLine($"      ⚠️ 재료 {ingredient.kind}는 세울 수 없다(특정 유닛만 지원) — 조합이 실패할 수 있다");
                continue;
            }
            for (int n = 0; n < Mathf.Max(1, ingredient.count); n++)
                sb.AppendLine("      " + SpawnForShot(ingredient.unit.name + "@pen", 0, 1).Replace("\n", "\n      "));
        }
        foreach (UnitIdentity unit in UnityEngine.Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None)) combineBefore.Add(unit);
        return sb.ToString();
    }

    static string RunCombine(string recipeName)
    {
        CombineRecipe recipe = FindRecipe(recipeName);
        CombineSystem system = UnityEngine.Object.FindFirstObjectByType<CombineSystem>();
        if (system == null) return "   ❌ 씬에 CombineSystem 없음\n";
        bool can = system.CanCombineNow(recipe);
        bool done = system.TryCombine(recipe);
        if (!done) return $"   ❌ 조합 실패 {recipeName}(CanCombineNow {can}) — 재료·골드·라운드 조건을 볼 것\n";

        UnitIdentity made = UnityEngine.Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None)
            .FirstOrDefault(u => !combineBefore.Contains(u) && u.Data == recipe.result);
        if (made == null) return $"   ⚠️ 조합은 성공했는데 결과 유닛({recipe.result?.name})을 못 찾음\n";
        shotUnits.Add(made.gameObject);

        LaneMarker lane = LaneMarker.Get(0);
        Vector3 center = lane != null ? lane.LaneCenter : Vector3.zero;
        Vector3 flat = made.transform.position - center;
        flat.y = 0f;
        return $"   ✅ 조합 {recipeName} → {made.name} 위치 {made.transform.position.ToString("F1")} · 레인 중심 {center.ToString("F1")}에서 수평 {flat.magnitude:F1}" +
               $"{(flat.magnitude < 20f ? " ✅ 가운데" : " ⚠️ 가운데 아님")}\n      조합 직후 {DescribeOnScreen(made.gameObject)}\n";
    }

    // ── spawn: (에디터 촬영 전용 — 위 GameShot 절 🔴) ──

    // 0번 레인(플레이어 1) 적 경로. WaveSpawner의 목록은 비공개라, 레인 섬 중심에 가장 가까운 경로를 고른다.
    static WaypointPath LanePathNear(Vector3 laneCenter)
    {
        WaypointPath best = null;
        float bestDistance = float.MaxValue;
        foreach (WaypointPath path in UnityEngine.Object.FindObjectsByType<WaypointPath>(FindObjectsSortMode.None))
        {
            if (path.PointCount == 0) continue;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < path.PointCount; i++) sum += path.GetPoint(i);
            float distance = Vector3.Distance(sum / path.PointCount, laneCenter);
            if (distance < bestDistance) { bestDistance = distance; best = path; }
        }
        return best;
    }

    // 레인 가운데에 세운 뒤, 경로에서 가장 긴 변의 가운데를 골라 **안쪽으로 사거리 절반** 들어간 자리로 옮긴다.
    // 사거리를 먼저 알아야 자리를 정할 수 있어서(ApplyStats가 맵 배율을 곱한다) 세운 뒤 읽는다.
    // 여러 기면 한 자리에 겹치지 않게 경로의 **긴 변들을 돌아가며**, 같은 변에 둘 이상이면 변을 나눠 세운다.
    static string SpawnForShot(string spec, int index, int total)
    {
        string unitName = spec.Split('@')[0];
        bool corner = spec.EndsWith("@corner");
        bool pen = spec.EndsWith("@pen");
        UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{unitName}.asset");
        UnitSpawner spawner = UnityEngine.Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        if (data == null || spawner == null || lane == null)
            return $"❌ 소환 실패 {unitName}: 에셋 {(data != null)} · UnitSpawner {(spawner != null)} · 0번 레인 {(lane != null)}";

        if (pen)
        {
            // 실제 뽑기 경로 그대로 — 자리를 받아 Spawn에 넘긴다(Spawn 안에서 NavPlacement가 NavMesh에 붙인다). 옮기지 않는다.
            Vector3 penPosition = lane.TakeSpawnPosition(data);
            GameObject penUnit = spawner.Spawn(data, penPosition, 0);
            if (penUnit == null) return $"❌ 소환 실패 {unitName}: Spawn이 null(프리팹 없음?)";
            shotUnits.Add(penUnit);
            bool penOnMesh = penUnit.TryGetComponent(out NavMeshAgent penAgent) && penAgent.isOnNavMesh;
            spawnedUnits.Add((unitName, penOnMesh, true));   // 우리는 싸우는 자리가 아니라 사거리는 따지지 않는다
            UnitAttacker penAttacker = penUnit.GetComponent<UnitAttacker>();
            if (penAttacker != null && penAttacker.AttackInterval > 0f) theoreticalDps += penAttacker.AttackDamage / penAttacker.AttackInterval;
            return $"🧍 소환 {unitName} → **우리**(TakeSpawnPosition {penPosition}) · 실제 위치 {penUnit.transform.position} · NavMesh 위 {(penOnMesh ? "✅" : "❌")}\n" +
                   $"      소환 직후 {DescribeOnScreen(penUnit)}";
        }

        GameObject unit = spawner.Spawn(data, lane.LaneCenter, 0);
        if (unit == null) return $"❌ 소환 실패 {unitName}: Spawn이 null(프리팹 없음?)";
        shotUnits.Add(unit);
        UnitAttacker attacker = unit.GetComponent<UnitAttacker>();
        float range = attacker != null ? attacker.AttackRange : 0f;

        WaypointPath path = LanePathNear(lane.LaneCenter);
        if (path == null || path.PointCount < 2) return $"⚠️ {unitName}을 레인 가운데에 세움(경로를 못 찾음) · 사거리 {range:F1}";

        // 경로의 변을 길이순으로. 짧은 이음새(모서리 꺾임)는 빼고 가장 긴 변의 절반 이상인 것만 쓴다.
        List<(Vector3 a, Vector3 b)> sides = new List<(Vector3, Vector3)>();
        for (int i = 0; i + 1 < path.PointCount; i++) sides.Add((path.GetPoint(i), path.GetPoint(i + 1)));
        sides = sides.OrderByDescending(e => Vector3.Distance(e.a, e.b)).ToList();
        float longest = Vector3.Distance(sides[0].a, sides[0].b);
        sides = sides.Where(e => Vector3.Distance(e.a, e.b) >= longest * 0.5f).ToList();
        Vector3 edge, target;
        string where;
        if (corner)
        {
            // 경로가 꺾이는 점(앞뒤 변 방향이 45° 넘게 바뀌는 점)이 모서리다. 모서리에서 레인 가운데 쪽 대각선으로 들어가
            // 두 변 모두에서 사거리 절반쯤 떨어지게 선다(대각선 거리 = 반사거리 × √2).
            List<Vector3> corners = new List<Vector3>();
            for (int i = 1; i + 1 < path.PointCount; i++)
            {
                Vector3 before = path.GetPoint(i) - path.GetPoint(i - 1), after = path.GetPoint(i + 1) - path.GetPoint(i);
                before.y = after.y = 0f;
                if (before.sqrMagnitude > 1f && after.sqrMagnitude > 1f && Vector3.Angle(before, after) > 45f) corners.Add(path.GetPoint(i));
            }
            if (corners.Count == 0) return $"❌ {unitName}: 경로에서 모서리를 못 찾음(점 {path.PointCount}개)";
            edge = corners[index % corners.Count];
            Vector3 diagonal = lane.LaneCenter - edge;
            diagonal.y = 0f;
            target = edge + diagonal.normalized * Mathf.Min(range * 0.5f * 1.4142f, diagonal.magnitude);
            where = $"{index % corners.Count + 1}번째 모서리(모서리 {corners.Count}개)";
        }
        else
        {
            (Vector3 a, Vector3 b) = sides[index % sides.Count];
            int perSide = (total + sides.Count - 1) / sides.Count;
            float along = (index / sides.Count + 1f) / (perSide + 1f);   // 한 변에 k기면 1/(k+1) 간격
            edge = Vector3.Lerp(a, b, along);
            Vector3 inward = lane.LaneCenter - edge;
            inward.y = 0f;
            target = edge + inward.normalized * Mathf.Min(range * 0.5f, inward.magnitude);
            where = $"{index % sides.Count + 1}번째 긴 변({along:P0} 지점)";
        }
        // 섬 윗면 높이를 모르니 높이 차까지 덮게 넉넉히(300) 찾되, **그 유닛의 에이전트 종류·영역**으로 찾는다 —
        // AllAreas로 찾으면 바다(Sea) 영역이나 다른 에이전트 종류의 면에 붙어 Warp가 실패한다(outbox 2018: y 2에서 못 올라감).
        string sampled = "못 찾음";
        NavMeshAgent sampleAgent = unit.GetComponent<NavMeshAgent>();
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = sampleAgent != null ? sampleAgent.agentTypeID : 0,
            areaMask = sampleAgent != null ? sampleAgent.areaMask : NavMesh.AllAreas,
        };
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 300f, filter))
        {
            sampled = $"{Vector3.Distance(hit.position, target):F1} 떨어진 곳";
            target = hit.position;
        }
        // 🔴 Warp는 **항상** 한다. 레인 가운데(첫 소환 자리)는 NavMesh 위가 아닐 수 있어 isOnNavMesh가 거짓인데, 그때 transform만 옮기면
        //    에이전트가 NavMesh 밖에 남아 UnitCombat의 추격·복귀가 「active agent … placed on a NavMesh」 오류로 멎는다
        //    (2026-09-23 outbox 2016: 641건, 유닛이 제자리에서만 때렸다). Warp는 에이전트를 새 자리의 NavMesh에 올린다.
        // 🔴 옮기는 건 게임의 「모으기(V)」 경로(UnitCombat.SnapTo)로 한다 — 복귀 지점(commandedPosition)까지 같이 옮긴다.
        //    예전엔 agent.Warp만 해서 복귀 지점이 **처음 소환한 레인 가운데**에 남았고, 유닛이 적을 쫓은 뒤 전부 레인 가운데로 돌아가
        //    한 점에 모였다(2026-09-24 outbox 2049: 모서리 5기가 찍는 순간 전부 화면 (1005, 826)). 09-23의 변·모서리 판도 같은 영향을 받았다.
        bool onMesh = false;
        if (unit.TryGetComponent(out UnitCombat combat) && unit.TryGetComponent(out NavMeshAgent agent))
            onMesh = combat.SnapTo(target) && agent.isOnNavMesh;
        else if (unit.TryGetComponent(out NavMeshAgent bareAgent)) onMesh = bareAgent.Warp(target) && bareAgent.isOnNavMesh;
        else unit.transform.position = target;

        // 경로까지 거리 = 경로의 모든 변 중 가장 가까운 것(모서리 배치에선 두 변 모두 가깝다).
        float toPath = float.MaxValue;
        for (int i = 0; i + 1 < path.PointCount; i++)
            toPath = Mathf.Min(toPath, DistanceToSegmentXZ(unit.transform.position, path.GetPoint(i), path.GetPoint(i + 1)));
        Vector3 flat = new Vector3(toPath, 0f, 0f);
        shotUnitInset = toPath;
        spawnedUnits.Add((unitName, onMesh, toPath < range));
        if (attacker != null && attacker.AttackInterval > 0f) theoreticalDps += attacker.AttackDamage / attacker.AttackInterval;
        shotUnitRange = range;
        shotUnitInterval = attacker != null ? attacker.AttackInterval : 0f;
        return $"🧍 소환 {unitName} → 플레이어 1 레인 {where}, 경로까지 {flat.magnitude:F1} · 사거리 {range:F1} · NavMesh 위 {(onMesh ? "✅" : "❌")}(가까운 면 {sampled}) · " +
               $"공격력 {attacker?.AttackDamage:F1} · 공격 간격 {attacker?.AttackInterval:F2}초 · 위치 {unit.transform.position}";
    }

    // 0번 레인 적의 체력을 0.25초마다 보고, 줄어든 횟수를 「맞은 횟수」로 센다(공격 간격이 이보다 길어 한 번 줄면 한 대다).
    // 도메인 리로드를 넘길 필요가 없다 — 리로드가 나면 판 자체가 오염 표시된다.
    class HitRecord { public float lastHp; public int hits; public float damage; public bool gone; public float maxHp; public float speed; }
    // 소환한 유닛이 경로에서 얼마나 떨어졌는지·사거리·공격 간격 — 「한 번 지나갈 때 이론상 몇 대」를 계산하는 데 쓴다.
    static float shotUnitInset, shotUnitRange, shotUnitInterval;
    // 세운 유닛마다 (이름, NavMesh 위인가, 경로가 사거리 안인가) — 판이 유효한지 결과 머리에서 바로 가린다.
    // 2026-09-23 outbox 2024: 5기 중 1기는 NavMesh 밖, 1기는 사거리 밖이라 그 판이 무효였는데 줄마다 흩어져 있어 늦게 봤다.
    static readonly List<(string name, bool onMesh, bool inRange)> spawnedUnits = new List<(string, bool, bool)>();
    // DPS·가동률(이론 = 세운 유닛의 공격력÷간격 합, 실측 = 총 피해 ÷ 레인에 적이 있던 시간)과 처치·골드 시점 기록.
    static float theoreticalDps, enemyPresentSeconds;
    static double lastWatchTime, watchStartTime;
    static int lastGold = int.MinValue;
    static readonly List<string> eventLog = new List<string>();
    static readonly List<GameObject> shotUnits = new List<GameObject>();

    // 유닛 머리(렌더러 경계 윗면 가운데)가 지금 카메라 화면 안인가 — 이름표(UnitNameplateLayer)가 쓰는 판정과 같다(screenPos.z > 0).
    static string DescribeOnScreen(GameObject unit)
    {
        Camera cam = Camera.main;
        if (cam == null) return "화면: Camera.main 없음";
        if (unit == null) return "화면: 유닛이 사라짐";
        Bounds? b = null;
        foreach (Renderer r in unit.GetComponentsInChildren<Renderer>())
        {
            if (b == null) b = r.bounds;
            else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; }
        }
        Vector3 head = b != null ? new Vector3(b.Value.center.x, b.Value.max.y, b.Value.center.z) : unit.transform.position;
        Vector3 sp = cam.WorldToScreenPoint(head);
        bool inFront = sp.z > 0f;
        bool inside = inFront && sp.x >= 0f && sp.x <= cam.pixelWidth && sp.y >= 0f && sp.y <= cam.pixelHeight;
        string verdict = inside ? "✅ 화면 안" : !inFront ? "❌ 카메라 **뒤**(screenPos.z ≤ 0 → 이름표 건너뜀)" : "❌ 화면 밖(앞이지만 가장자리 너머)";
        return $"화면: {verdict} · 머리 screenPos ({sp.x:F0}, {sp.y:F0}, z {sp.z:F1}) / 화면 {cam.pixelWidth}×{cam.pixelHeight} · 카메라 {cam.transform.position} 방향 {cam.transform.forward}";
    }
    // 라운드가 바뀌는 순간 0번 레인에 남은 적 수 — 「적이 쌓이면 레인당 70에서 패배」를 보려고.
    static int watchedRound = -1, maxLaneEnemies;
    static readonly List<string> roundLog = new List<string>();
    static readonly Dictionary<EnemyDummy, HitRecord> hitWatch = new Dictionary<EnemyDummy, HitRecord>();

    static void WatchLaneHits()
    {
        double now = EditorApplication.timeSinceStartup;
        int laneCount = EnemyDummy.CountInLane(0);
        if (laneCount > 0) enemyPresentSeconds += (float)(now - lastWatchTime);
        lastWatchTime = now;
        string at = $"{now - watchStartTime:F1}초";
        int gold = PlayerContext.GetOccupied(0)?.GoldWallet?.Gold ?? -1;
        if (lastGold != int.MinValue && gold != lastGold && eventLog.Count < 60) eventLog.Add($"{at} 골드 {lastGold}→{gold}({gold - lastGold:+0;-0})");
        lastGold = gold;
        maxLaneEnemies = Mathf.Max(maxLaneEnemies, laneCount);
        RoundManager rounds = UnityEngine.Object.FindFirstObjectByType<RoundManager>();
        if (rounds != null && rounds.CurrentRound != watchedRound)
        {
            if (watchedRound >= 0) roundLog.Add($"라운드 {watchedRound}→{rounds.CurrentRound} 때 레인 적 {laneCount}");
            watchedRound = rounds.CurrentRound;
        }

        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null || enemy.LaneIndex != 0) continue;
            if (!hitWatch.TryGetValue(enemy, out HitRecord record))
            {
                hitWatch[enemy] = new HitRecord { lastHp = enemy.Hp, maxHp = enemy.MaxHp, speed = enemy.MoveSpeed };
                continue;
            }
            if (enemy.Hp < record.lastHp - 0.001f)
            {
                record.hits++;
                record.damage += record.lastHp - enemy.Hp;
            }
            record.lastHp = enemy.Hp;
        }
        foreach (KeyValuePair<EnemyDummy, HitRecord> pair in hitWatch)
        {
            if (pair.Value.gone || (pair.Key != null && !pair.Key.IsDead)) continue;
            pair.Value.gone = true;
            // 사라진 이유를 가른다: 마지막으로 본 체력이 한 대 거리 안이었고 맞은 적이 있으면 처치, 아니면 다른 이유(흡수·레인 밖 등).
            bool likelyKill = pair.Value.hits > 0 && pair.Value.lastHp <= pair.Value.maxHp * 0.5f;
            if (eventLog.Count < 60) eventLog.Add($"{at} 적 사라짐({(likelyKill ? "처치로 보임" : "처치 아님?")}, 마지막 체력 {pair.Value.lastHp:F0}/{pair.Value.maxHp:F0}, 맞은 {pair.Value.hits}대)");
        }
    }

    static string DescribeLaneHits(GameShotJob job, double watched)
    {
        List<HitRecord> hitOnes = hitWatch.Values.Where(r => r.hits > 0).ToList();
        int gold = PlayerContext.GetOccupied(0)?.GoldWallet?.Gold ?? -1;
        string histogram = string.Join(" · ", hitOnes.GroupBy(r => r.hits).OrderBy(g => g.Key).Select(g => $"{g.Key}대 {g.Count()}마리"));
        int killed = hitOnes.Count(r => r.gone);
        string avg = hitOnes.Count > 0 ? $"{hitOnes.Average(r => r.hits):F2}" : "-";
        // 이론: 유닛이 곧은 경로에서 inset만큼 안쪽에 있으면 사거리 원이 경로를 자르는 길이는 2√(R²−d²).
        //       적이 그 길이를 지나는 시간 ÷ 공격 간격 = 한 마리를 혼자 상대할 때 최대 몇 대. 적이 몰려 오면 나눠 맞아 평균이 이보다 낮다.
        float speed = hitWatch.Count > 0 ? hitWatch.Values.Average(r => r.speed) : 0f;
        float hp = hitWatch.Count > 0 ? hitWatch.Values.Average(r => r.maxHp) : 0f;
        float chord = shotUnitRange > shotUnitInset ? 2f * Mathf.Sqrt(shotUnitRange * shotUnitRange - shotUnitInset * shotUnitInset) : 0f;
        float inRange = speed > 0f ? chord / speed : 0f;
        string theory = $"   📐 이론: 경로에서 {shotUnitInset:F1} 안쪽 · 사거리 {shotUnitRange:F1} → 사거리 안 경로 {chord:F0} · 적 이속 평균 {speed:F1} → " +
                        $"머무는 시간 {inRange:F2}초 ÷ 공격 간격 {shotUnitInterval:F2}초 = 한 마리 혼자 지나갈 때 최대 {(shotUnitInterval > 0f ? inRange / shotUnitInterval : 0f):F1}대 · 적 최대체력 평균 {hp:F0}\n";
        RoundManager roundManager = UnityEngine.Object.FindFirstObjectByType<RoundManager>();
        string rounds = $"   🏁 지금 라운드 {roundManager?.CurrentRound} · 남은시간 {roundManager?.RoundTimeLeft:F1}s · 준비 {roundManager?.PreRoundTimeLeft:F1}s · " +
                        $"0번 레인 적 지금 {EnemyDummy.CountInLane(0)} · 최대 {maxLaneEnemies} · 패배 여부 {roundManager?.IsGameOver}" +
                        (roundLog.Count > 0 ? $" · {string.Join(" · ", roundLog)}" : "") + "\n";
        float measuredDps = enemyPresentSeconds > 0f ? hitOnes.Sum(r => r.damage) / enemyPresentSeconds : 0f;
        string dps = $"   ⚔️ 이론 DPS {theoreticalDps:F1}(세운 유닛 공격력÷간격 합) · 실측 DPS {measuredDps:F1}(총 피해 ÷ 레인에 적이 있던 {enemyPresentSeconds:F1}초) · " +
                     $"가동률 {(theoreticalDps > 0f ? 100f * measuredDps / theoreticalDps : 0f):F0}%\n";
        string events = eventLog.Count > 0 ? "   🕒 처치·골드 시점: " + string.Join(" · ", eventLog) + "\n" : "   🕒 처치·골드 변화 없음\n";
        return rounds + dps + events + theory + $"   📊 {watched:F0}초 관찰(0번 레인 적 {hitWatch.Count}마리 추적): 맞은 적 {hitOnes.Count}마리 · 총 {hitOnes.Sum(r => r.hits)}대 · " +
               $"총 피해 {hitOnes.Sum(r => r.damage):F0} · 사라진(처치 추정) {killed}마리 · 골드 {job.goldAtSpawn} → {gold}\n" +
               $"   📊 적 한 마리당 맞은 횟수: 평균 {avg} · 분포 {(histogram.Length > 0 ? histogram : "없음")}\n";
    }

    // 지금 화면에 켜져 있고 누를 수 있는 버튼 중 이름이나 글자가 target인 것을 누른다. 누른 버튼 설명을 돌려준다(못 찾으면 null).
    // 실제 클릭과 같은 길(ExecuteEvents.pointerClickHandler → Button.OnPointerClick)로 보낸다 — interactable이 꺼져 있으면 안 눌린다.
    static string ClickButton(string target)
    {
        foreach (UnityEngine.UI.Button button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!button.IsActive() || !button.IsInteractable()) continue;
            string label = ButtonLabel(button);
            if (button.gameObject.name != target && label != target) continue;

            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            return $"{button.gameObject.name}「{label}」";
        }
        return null;
    }

    // 상점을 고른 뒤 명령칸 12개 중 글자가 있고 누를 수 있는 첫 칸을 누른다. 못 찾으면 칸마다 「글자·누를 수 있음」을 적는다(골드 부족으로 흐린지 가르려고).
    static readonly string[] UnitCommandLabels = { "공격", "정지", "모으기", "정렬" };
    static string ClickFirstShopSlot(GameShotJob job)
    {
        var slots = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(b => b.gameObject.name.StartsWith("UnitCommandSlot"))
            .OrderBy(b => int.TryParse(b.gameObject.name.Substring("UnitCommandSlot".Length), out int n) ? n : 99).ToList();
        UnityEngine.UI.Button pick = slots.FirstOrDefault(b => b.IsActive() && b.IsInteractable() && ButtonLabel(b).Length > 0 && !UnitCommandLabels.Contains(ButtonLabel(b)));
        if (pick == null)
        {
            string listing = string.Join(" · ", slots.Select(b => $"{b.gameObject.name.Substring("UnitCommandSlot".Length)}「{ButtonLabel(b)}」{(b.IsInteractable() ? "" : "(흐림)")}"));
            if (!job.report.EndsWith(listing + "\n")) job.report += $"   🏪 상점 칸: {listing}\n";
            return null;
        }
        int goldBefore = PlayerContext.GetOccupied(0)?.GoldWallet?.Gold ?? -1;
        var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        UnityEngine.EventSystems.ExecuteEvents.Execute(pick.gameObject, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        int goldAfter = PlayerContext.GetOccupied(0)?.GoldWallet?.Gold ?? -1;
        return $"{pick.gameObject.name}「{ButtonLabel(pick)}」(골드 {goldBefore} → {goldAfter})";
    }

    static string ButtonLabel(UnityEngine.UI.Button button)
    {
        UnityEngine.UI.Text text = button.GetComponentInChildren<UnityEngine.UI.Text>();
        if (text != null && !string.IsNullOrWhiteSpace(text.text)) return text.text.Trim();
        TMPro.TMP_Text tmp = button.GetComponentInChildren<TMPro.TMP_Text>();
        return tmp != null ? tmp.text.Trim() : "";
    }

    static string ListButtons()
    {
        var buttons = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(b => b.IsActive() && b.IsInteractable())
            .Select(b => $"      {b.gameObject.name}「{ButtonLabel(b)}」")
            .Take(40).ToList();
        return buttons.Count == 0 ? "      (없음)" : string.Join("\n", buttons);
    }

    // 생성자가 불렸다 = 도메인이 막 다시 로드됐다. 플레이 모드 진입 때의 리로드는 정상(stage가 아직 entering)이고,
    // 그 뒤 단계에서 불렸다면 **플레이 도중** 리로드다 — 에셋 임포트(다른 세션이 파일을 넣음)나 스크립트 수정이 원인이다.
    // 2026-09-23 outbox 1264: 박준희 FBX 복사가 플레이 중 임포트·리로드를 일으켜 InterludeGate.propertyBlock·
    // PirateQuestShop.slotState(둘 다 Awake에서 만드는 비직렬화 필드)가 null이 되고 NRE 50여 건이 났다. 결과에 이 표시를 붙인다.
    static void NoteMidPlayReload()
    {
        GameShotJob job = LoadGameShot();
        if (job == null || job.stage == "entering" || job.stage == "exiting") return;
        if (job.midPlayReloads == 0) job.logsBeforeReload = job.logs.Sum(l => l.count) + job.droppedLogs;
        job.midPlayReloads++;
        job.report += $"   ⚠️ {job.stage} 중에 도메인이 다시 로드됐다(플레이 도중 에셋 임포트·스크립트 변경)\n";
        SaveGameShot(job);
    }

    // ── select: / rclick: — 가상 마우스로 실제 입력 경로를 탄다 ──
    static Mouse shotMouse;
    static GameObject lastPointerTarget;   // 마지막 select:/rclick: 대상 — 찍는 순간 선택 유닛이 거기까지 얼마나 남았는지 잰다
    static Mouse previousMouse;
    static InputSettings.EditorInputBehaviorInPlayMode? previousBehavior;
    const string ShotMouseName = "ClaudeGameShotMouse";

    static void EnsureShotMouse()
    {
        if (shotMouse == null || !shotMouse.added)
        {
            shotMouse = InputSystem.devices.OfType<Mouse>().FirstOrDefault(m => m.name == ShotMouseName)
                        ?? InputSystem.AddDevice<Mouse>(ShotMouseName);
            // 🔴 새 장치는 (0,0) — 화면 왼쪽 아래 **구석**에서 시작한다. 실제 마우스를 끄고 이게 current가 된 뒤로(아래),
            //    RtsCameraController의 가장자리 밀기가 그 구석을 읽어 카메라를 계속 밀었다 — 옮긴 카메라가 다시 밀려 대상이
            //    화면 밖으로 나가고 조준이 빗나갔다(09-25 i1_23: 🎥 두 번, 조준 (1053,650)). 만들자마자 가운데로 둔다.
            Camera cam = Camera.main;
            Vector2 center = cam != null ? new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.55f) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            InputSystem.QueueStateEvent(shotMouse, new MouseState { position = center });
        }
        if (Mouse.current != shotMouse) { previousMouse = Mouse.current; shotMouse.MakeCurrent(); }
        // 🔴 실제 마우스는 판 동안 끈다(2026-09-25 i1_14). AllDeviceInputAlwaysGoesToGameView라 사람이 에디터에서 마우스를 쓰면
        //    그 이벤트가 매 프레임 Mouse.current를 빼앗아, 게임이 가상 마우스의 누름·뗌을 못 읽었다 — R1은 되고 R2부터 좌·우클릭이
        //    **전부** 안 먹었다(따로 떼어 돌린 짧은 판은 다 됐다). 에디터 창 조작은 Input System을 안 거치므로 영향이 없다.
        foreach (Mouse m in InputSystem.devices.OfType<Mouse>().Where(m => m != shotMouse && m.enabled).ToList())
        {
            InputSystem.DisableDevice(m);
            disabledMice.Add(m.deviceId);
        }
        // 실제 키보드도 끈다(09-25 판 E — 카메라가 옮긴 자리에서 (+220,+130)px 밀렸다. 사람이 WASD·방향키를 눌렀을 수 있다, PM 지시).
        //    도구는 키보드를 안 쓴다. 에디터 창 조작은 Input System을 안 거치므로 영향이 없다.
        foreach (Keyboard k in InputSystem.devices.OfType<Keyboard>().Where(k => k.enabled).ToList())
            InputSystem.DisableDevice(k);
        if (previousBehavior == null)
        {
            previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
    }

    // 끝날 때 반드시 부른다 — 가상 마우스가 남으면 사람의 실제 마우스 대신 그게 Mouse.current로 남는다.
    static readonly List<int> disabledMice = new List<int>();

    static void ReleaseShotMouse()
    {
        // 판 동안 끈 실제 마우스를 되켠다 — 도메인 리로드로 목록을 잃었을 때를 대비해 꺼진 마우스는 전부 켠다.
        foreach (Mouse m in InputSystem.devices.OfType<Mouse>().Where(m => m.name != ShotMouseName && !m.enabled).ToList())
            InputSystem.EnableDevice(m);
        foreach (Keyboard k in InputSystem.devices.OfType<Keyboard>().Where(k => !k.enabled).ToList())
            InputSystem.EnableDevice(k);
        disabledMice.Clear();
        foreach (Mouse m in InputSystem.devices.OfType<Mouse>().Where(m => m.name == ShotMouseName).ToList())
            InputSystem.RemoveDevice(m);
        shotMouse = null;
        if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        previousMouse = null;
        if (previousBehavior != null) InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior.Value;
        previousBehavior = null;
    }

    // 동작이 끝나면 가상 마우스를 화면 한가운데(월드 위, HUD·가장자리 스크롤 영역 밖)로 치운다 — UI 위에 남으면 툴팁이 떠서
    // 다음 캡처를 가린다(09-24 loop3: 「판매」 툴팁이 화면 가운데 떠 있었다).
    static void ParkShotMouse(Camera cam)
    {
        if (shotMouse == null || cam == null) return;
        QueueMouse(new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.55f), MouseButton.Left, false);
    }

    // 🔴 두 가지를 막는다(2026-09-25 i1_09 — autoloop 판에서 좌클릭·드래그가 전부 안 먹었다. 따로 떼어 돌린 판에선 다 됐다).
    //    ① 느린 판(프레임 23ms)에선 에디터 틱이 게임 프레임보다 잦아서 누름·끌기·뗌이 **한 프레임에 몰린다** →
    //       SelectionManager가 드래그를 못 보고 클릭으로 읽는다. 이벤트 사이에 게임 프레임이 하나 이상 지나게 한다(PointerFramePassed).
    //    ② AllDeviceInputAlwaysGoesToGameView라 **실제 마우스가 조금만 움직여도** Mouse.current가 그쪽으로 넘어간다 —
    //       그러면 게임은 가상 마우스의 누름을 안 읽는다. 넣을 때마다 가상 마우스를 current로 되돌린다.
    static int lastMouseQueueFrame = -1;
    static bool PointerFramePassed() => Time.frameCount > lastMouseQueueFrame;

    static void QueueMouse(Vector2 position, MouseButton button, bool down)
    {
        if (Mouse.current != shotMouse) shotMouse.MakeCurrent();
        MouseState state = new MouseState { position = position };
        if (down) state = state.WithButton(button, true);
        InputSystem.QueueStateEvent(shotMouse, state);
        lastMouseQueueFrame = Time.frameCount;
    }

    static GameObject FindPointerTarget(string spec, out string candidates)
    {
        bool left = spec.StartsWith("@sel:");
        string name = spec.Substring(left ? 5 : 4).Normalize(NormalizationForm.FormC);
        // 「=이름」이면 정확히 그 이름만(Lane1처럼 Lane1_앞치마·Lane1_Cliff와 앞부분이 겹치는 것을 가른다).
        bool exact = name.StartsWith("=");
        if (exact) name = name.Substring(1);
        bool Matches(string candidate) => exact ? candidate.Normalize(NormalizationForm.FormC) == name : candidate.Normalize(NormalizationForm.FormC).Contains(name);
        candidates = "";
        if (left)
        {
            List<Selectable> mine = Selectable.All.Where(x => x != null &&
                (!x.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == LocalPlayer.LocalPlayerId)).ToList();
            Selectable hit = mine.FirstOrDefault(x => Matches(x.name));
            if (hit == null) candidates = string.Join(", ", mine.Select(x => x.name).Distinct().Take(20));
            return hit != null ? hit.gameObject : null;
        }
        GameObject found = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && Matches(c.name))
            .Select(c => c.gameObject).FirstOrDefault();
        return found;
    }

    // 한 틱에 한 단계씩. 끝나면 true.
    // 좌클릭·박스 선택 뒤 선택이 뜻과 다르면 그 선택에 기대는 다음 동작(우클릭·명령칸)을 버린다.
    //    outbox 2111: 도박소 좌클릭이 안 먹어 유닛 5기가 선택된 채 남았고, 안흔함 박스가 안 먹어 스토리포탈 우클릭이 5기 전부를 보냈다.
    static void SkipIfSelectionWrong(GameShotJob job, string spec)
    {
        bool isSelect = spec.StartsWith("@sel:"), isBox = spec.StartsWith("@box:");
        if (!isSelect && !isBox) return;
        string want = spec.Substring(5).Normalize(NormalizationForm.FormC);
        SelectionManager selection = UnityEngine.Object.FindFirstObjectByType<SelectionManager>();
        var names = selection != null ? selection.Selected.Where(x => x != null).Select(x => x.gameObject.name.Normalize(NormalizationForm.FormC)).ToList() : new List<string>();
        if (isBox && want.EndsWith("|far")) want = want.Substring(0, want.Length - 4);
        if (job.clickIndex >= job.clicks.Count) return;
        string next = job.clicks[job.clickIndex];
        // 박스는 먼저 「드래그가 먹었나」(lastBoxPicked). 모서리 땅 우클릭(@rcpt:)은 박스에 건물(Lane1_*)이 섞여도 해가 없어
        //    대상이 한 기라도 골렸으면 보낸다 — 포탈 우클릭(@rc:)은 섞인 게 같이 가면 안 되니 전부 맞아야 한다.
        bool ok = isSelect ? names.Any(n => n.Contains(want))
                : lastBoxPicked > 0 && (next.StartsWith("@rcpt:") || names.All(n => n.Contains(want)));
        if (ok) return;
        bool dependent = next.StartsWith("@rc:") || next.StartsWith("@rcpt:") || (isSelect && (next.StartsWith("?UnitCommandSlot") || next == "?@shopany" || next == "?@shopspend"));
        if (!dependent) return;
        job.report += $"   ⛔ 「{spec}」 뒤 선택이 뜻과 다름({(names.Count > 0 ? string.Join(", ", names.Take(5)) : "없음")}) — 이 선택에 기대는 「{next}」를 버림\n";
        job.clickIndex++;
    }

    static bool StepPointer(GameShotJob job, string spec, double inStage)
    {
        if (spec.StartsWith("@box:")) return StepBox(job, spec.Substring(5), inStage);
        if (spec.StartsWith("@rcpt:")) return StepPointAt(job, spec.Substring(6), inStage);
        if (job.pointerPhase > 0 && !PointerFramePassed()) return false;   // 앞 마우스 이벤트가 게임 프레임에 먹히기 전(QueueMouse 주석)
        bool left = spec.StartsWith("@sel:");
        string label = left ? "좌클릭 select" : "우클릭 rclick";
        Camera cam = Camera.main;
        switch (job.pointerPhase)
        {
            case 0:
            {
                GameObject target = FindPointerTarget(spec, out string candidates);
                if (target == null || cam == null)
                {
                    if (inStage < GameShotClickSearch) return false;
                    // 꺼진 오브젝트까지 뒤져 「없다」와 「있지만 꺼져 있다(해금 전 등)」를 가른다.
                    string wanted = spec.Substring(left ? 5 : 4).Normalize(NormalizationForm.FormC);
                    var hidden = Resources.FindObjectsOfTypeAll<GameObject>()
                        .Where(g => g.scene.IsValid() && g.name.Normalize(NormalizationForm.FormC).Contains(wanted))
                        .Select(g => $"{g.name}(켜짐 {g.activeInHierarchy} · 콜라이더 {(g.TryGetComponent(out Collider c) ? (c.enabled ? "켜짐" : "꺼짐") : "없음")})")
                        .Take(5).ToList();
                    // 긴 판(rounds:)에선 판을 죽이지 않고 이 동작만 건너뛴다 — 같은 턴의 조합이 대상을 먼저 없앨 수 있다
                    //    (09-25 판 D: 스토리로 보낼 안흔함이 그 턴 조합으로 특별함이 돼 「못 찾음」으로 판 전체가 끝났다).
                    //    뒤따르는 선택 의존 동작(@rc·상점)은 SkipIfSelectionWrong이 버린다.
                    string why = $"{label}: 「{wanted}」 대상을 못 찾음" + (candidates.Length > 0 ? $" — 내 유닛: {candidates}" : "") +
                                 (hidden.Count > 0 ? $" — 씬에는 있음: {string.Join(", ", hidden)}" : " — 씬 어디에도 그 이름이 없다");
                    if (job.watchRounds > 0) { job.report += $"   ⚠️ {why} — 이 동작을 건너뜀\n"; job.pointerX = 0f; return true; }
                    FailGameShot(job, why);
                    return false;
                }
                lastPointerTarget = target;
                // 🔴 우클릭으로 트리거(포탈)를 겨눌 땐 bounds.center가 아니라 **원판**(transform.position)을 찍는다 — 사람이 찍는 자리다.
                //    트리거는 위습을 받으려고 키 150으로 세워 둬서 bounds.center가 공중(y 82.8)에 뜬다. 비스듬한 카메라에선 그 화면점이
                //    포탈 **뒤쪽 땅**을 찍어, 목적지가 원 가장자리(17.4/18.8)에 겨우 걸리거나 구도에 따라 원 밖 29~295로 빠졌다
                //    (2026-09-25 WispPortalProbe, outbox i1_03). 좌클릭(select:)은 몸을 맞혀야 해서 그대로 bounds.center다.
                Vector3 aim = target.TryGetComponent(out Collider col)
                    ? (!left && col.isTrigger ? target.transform.position : col.bounds.center)
                    : target.transform.position;
                Vector3 sp = cam.WorldToScreenPoint(aim);
                (float bandBottom, float bandTop) = PointerBand();
                bool visible = sp.z > 0f && sp.x > 20f && sp.x < cam.pixelWidth - 20f && sp.y > bandBottom * cam.pixelHeight + 10f && sp.y < bandTop * cam.pixelHeight - 10f;
                // 🔴 좌클릭(건물·유닛 고르기)은 **화면 가운데 영역**에 들 때만 누른다(09-25 판 D). 강화소·도박소를 y≈188에서 눌렀는데
                //    그 자리가 하단 HUD 위라 선택이 안 됐다 — 띠 판정(bandBottom)은 통과했으니 판정 기준이 화면 배율과 어긋난 것이다.
                //    원인을 쫓기보다 가운데로 카메라를 옮겨 누르는 쪽이 사람 조작과도 같다.
                if (left && visible && job.pointerX >= 0f)
                    visible = sp.x > cam.pixelWidth * 0.2f && sp.x < cam.pixelWidth * 0.8f && sp.y > cam.pixelHeight * 0.35f && sp.y < cam.pixelHeight * 0.8f;
                if (!visible)
                {
                    RtsCameraController rts = cam.GetComponent<RtsCameraController>();
                    if (rts == null) { FailGameShot(job, $"{label}: {target.name}이 화면 밖인데 카메라를 옮길 RtsCameraController가 없음"); return false; }
                    // 옮긴 뒤에도 카메라가 더 움직일 수 있다(감쇠 관성·가장자리 밀기) — 세 번까지 다시 옮긴다.
                    //    그래도 안 들어오면 **이 동작만** 건너뛴다. 긴 판 하나가 우클릭 하나 때문에 통째로 죽었다(09-25 i1_16, R3).
                    if (job.pointerX <= -3f)
                    {
                        job.report += $"   ⚠️ {label}: 카메라를 세 번 옮겨도 {target.name}이 화면 안(HUD 사이)에 안 들어옴 — 화면 좌표 {sp} · 이 동작을 건너뜀\n";
                        job.pointerX = 0f;
                        return true;
                    }
                    // 다시 옮기는 거면(앞 틱에 옮겼는데 또 밖) 카메라가 옮긴 뒤 **스스로 움직인 것**이다 — 원인 후보(키보드·가장자리 밀기)를 같이 찍는다(09-25 판 E).
                    string drift = job.pointerX < 0f
                        ? $" · 앞 이동 뒤 카메라 {cam.transform.position:F0} · 키보드축 {rts.KeyboardAxis} · 가장자리축 {rts.EdgeAxis} · 마우스 {(Mouse.current != null ? Mouse.current.position.ReadValue().ToString("F0") + " " + Mouse.current.name : "-")} · 화면 {sp:F0}"
                        : "";
                    rts.MoveTo(new Vector3(aim.x, 0f, aim.z));   // 미니맵 클릭과 같은 경로
                    job.report += $"   🎥 {target.name}이 화면 밖이라 카메라를 옮김(MoveTo {aim.ToString("F0")}){drift}\n";
                    job.pointerX = Mathf.Min(job.pointerX, 0f) - 1f;
                    SaveGameShot(job);
                    return false;
                }
                EnsureShotMouse();
                job.pointerX = sp.x; job.pointerY = sp.y;
                QueueMouse(new Vector2(sp.x, sp.y), left ? MouseButton.Left : MouseButton.Right, false);   // 먼저 그 자리로 옮기고
                job.pointerPhase = 1;
                SaveGameShot(job);
                return false;
            }
            case 1:
                QueueMouse(new Vector2(job.pointerX, job.pointerY), left ? MouseButton.Left : MouseButton.Right, true);   // 누름
                job.pointerPhase = 2;
                SaveGameShot(job);
                return false;
            case 2:
                QueueMouse(new Vector2(job.pointerX, job.pointerY), left ? MouseButton.Left : MouseButton.Right, false);  // 뗌
                job.pointerPhase = 3;
                SaveGameShot(job);
                return false;
            default:
            {
                SelectionManager selection = UnityEngine.Object.FindFirstObjectByType<SelectionManager>();
                string selected = selection != null ? string.Join(", ", selection.Selected.Where(x => x != null).Select(x => x.name)) : "(SelectionManager 없음)";
                RtsCameraController camCtl = cam != null ? cam.GetComponent<RtsCameraController>() : null;
                string axes = camCtl != null && (camCtl.KeyboardAxis != Vector2.zero || camCtl.EdgeAxis != Vector2.zero)
                    ? $" · ⚠️ 카메라 입력 중(키보드축 {camCtl.KeyboardAxis} · 가장자리축 {camCtl.EdgeAxis})" : "";
                job.report += $"   🖱 {label} 「{spec.Substring(left ? 5 : 4)}」 @ 화면 ({job.pointerX:F0}, {job.pointerY:F0}) → 지금 선택: {(selected.Length > 0 ? selected : "없음")}{axes}\n";
                ParkShotMouse(cam);
                job.pointerX = 0f;
                return true;
            }
        }
    }

    // ── rounds:/autoloop — 긴 판 ──
    static float frameSum, frameMax;
    static int frameN;

    // 가동률 — 레인에 적이 있는 동안, 내 유닛 하나하나가 ① 사거리 안에 0번 레인 적을 하나라도 둔 시간 ② 실제 표적(CurrentTarget)을 가진 시간.
    //    분모는 「레인에 적이 있던 유닛·초」(09-23 ⚔️ 줄의 「레인에 적이 있던 초」와 같은 기준). 게임 시간(Time.time)으로 적분한다.
    //    등급별로 따로 — 스토리존에 가 있는 안흔함이 흔함 값을 흐리지 않게.
    static readonly Dictionary<UnitGrade, float[]> uptime = new Dictionary<UnitGrade, float[]>();   // [분모, 사거리 안, 표적 있음, 사거리 안·표적 없음, 그때 사거리 안 적 수×초]
    static readonly Dictionary<string, float> noTargetStates = new Dictionary<string, float>();
    static readonly System.Reflection.FieldInfo CombatStateField = typeof(UnitCombat).GetField("state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    static float lastUptimeTime = -1f;

    static void SampleUptime()
    {
        float now = Time.time;
        float dt = lastUptimeTime < 0f || now < lastUptimeTime ? 0f : now - lastUptimeTime;
        lastUptimeTime = now;
        if (dt <= 0f || dt > 1f || EnemyDummy.CountInLane(0) == 0) return;
        List<Vector3> enemies = EnemyDummy.Active.Where(e => e != null && e.LaneIndex == 0).Select(e => e.transform.position).ToList();
        foreach (UnitIdentity unit in MyUnits())
        {
            if (!unit.TryGetComponent(out UnitCombat combat) || !unit.TryGetComponent(out UnitAttacker attacker)) continue;
            if (!uptime.TryGetValue(unit.Data.grade, out float[] acc)) uptime[unit.Data.grade] = acc = new float[5];
            acc[0] += dt;
            float r2 = attacker.AttackRange * attacker.AttackRange;
            Vector3 me = unit.transform.position;
            int inRange = enemies.Count(e => (e - me).sqrMagnitude <= r2);
            bool hasTarget = combat.CurrentTarget != null;
            if (inRange > 0) acc[1] += dt;
            if (hasTarget) acc[2] += dt;
            if (inRange > 0 && !hasTarget)
            {
                // 「사거리 안인데 표적 없음」 — 그때 사거리 안 적 수와 전투 상태(구현담당1 요청, 09-24). 0마리면 사거리 계산, 여럿이면 탐색·상태 문제.
                acc[3] += dt;
                acc[4] += inRange * dt;
                string st = CombatStateField?.GetValue(combat)?.ToString() ?? "?";
                noTargetStates[st] = (noTargetStates.TryGetValue(st, out float had) ? had : 0f) + dt;
            }
        }
    }

    static string UptimeText()
    {
        if (uptime.Count == 0) return "가동률 -";
        string text = "가동률(사거리 안/표적 있음, 유닛·초) " + string.Join(", ", uptime.OrderBy(k => k.Key).Select(k =>
            $"{k.Key} {100f * k.Value[1] / k.Value[0]:F0}%/{100f * k.Value[2] / k.Value[0]:F0}% ({k.Value[0]:F0})" +
            (k.Value[3] > 0f ? $"[사거리 안·표적 없음 {k.Value[3]:F0}초, 그때 사거리 안 적 평균 {k.Value[4] / k.Value[3]:F1}]" : "")));
        float noTargetTotal = noTargetStates.Values.Sum();
        if (noTargetTotal > 0f)
            text += " · 표적 없을 때 상태 " + string.Join(", ", noTargetStates.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {100f * k.Value / noTargetTotal:F0}%"));
        uptime.Clear();
        noTargetStates.Clear();
        return text;
    }

    // 흔함 선택 위습이 포탈에 왜 안 들어가나 — 위습마다 위치·목적지·길 상태·남은 거리·속도, 그리고 가장 가까운 흔함선택 포탈까지 거리.
    static string ChoiceWispText()
    {
        var portalObjs = UnityEngine.Object.FindObjectsByType<UnitPortal>(FindObjectsSortMode.None)
            .Where(p => p.gameObject.name.StartsWith("흔함선택_")).ToList();
        var portals = portalObjs.Select(p => p.transform.position).ToList();
        var wisps = UnityEngine.Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None)
            .Where(w => w != null && w.Data != null && (w.Data.wispName ?? "").Contains("흔함 선택") &&
                        (!w.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == 0)).Take(6).ToList();
        if (wisps.Count == 0) return "";
        return "      🧭 흔함 선택 위습(최대 6): " + string.Join(" | ", wisps.Select(w =>
        {
            Vector3 p = w.transform.position;
            float near = portals.Count > 0 ? portals.Min(q => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z))) : -1f;
            if (!w.TryGetComponent(out UnityEngine.AI.NavMeshAgent a)) return $"{p.ToString("F0")} 에이전트 없음 · 포탈까지 {near:F0}";
            string path = a.isOnNavMesh ? $"{a.pathStatus} 남은 {(a.hasPath ? a.remainingDistance : -1f):F0} 목적지 {a.destination.ToString("F0")}" : "NavMesh 밖";
            // 트리거가 왜 안 켜지나 — 가장 가까운 포탈의 트리거 경계와 위습 콜라이더 경계가 겹치는지, 위습에 Rigidbody가 있는지.
            UnitPortal np = portalObjs.OrderBy(q => (q.transform.position - p).sqrMagnitude).FirstOrDefault();
            Collider pc = np != null ? np.GetComponent<Collider>() : null;
            Collider wc = w.GetComponent<Collider>();
            string overlap = pc == null || wc == null ? "콜라이더 없음" :
                $"{np.gameObject.name} 트리거 y {pc.bounds.min.y:F1}~{pc.bounds.max.y:F1} 반폭 {pc.bounds.extents.x:F1} · 위습 y {wc.bounds.min.y:F1}~{wc.bounds.max.y:F1} 반폭 {wc.bounds.extents.x:F1}(켜짐 {wc.enabled}·트리거 {wc.isTrigger}) · 겹침 {pc.bounds.Intersects(wc.bounds)} · Rigidbody 위습 {w.GetComponent<Rigidbody>() != null}/포탈 {np.GetComponent<Rigidbody>() != null}";
            return $"{p.ToString("F0")} · {path} · 속도 {a.velocity.magnitude:F1}/{a.speed:F1} · 가장 가까운 흔함선택 포탈까지 {near:F0} · {overlap}";
        })) + "\n";
    }

    static bool DefeatShown()
    {
        GameObject dim = GameObject.Find("DefeatDim");   // PM 9ad8d522 패배 화면 — 켜져 있을 때만 Find에 잡힌다
        return dim != null && dim.activeInHierarchy;
    }

    // 👑 0번 레인 보스 — 등장·처치를 따로 찍는다(2026-09-25 PM 지시). 🏁 줄의 「보스 적」은 **전 레인 합계**라 R10 뒤로도 숫자가
    //    그대로여서 잡았는지 확정할 수 없었다(i1_18). 개체를 붙잡아 두고, 사라질 때 OnBossKilled가 그 라운드로 떴는지 맞춘다 —
    //    신호 없이 사라졌으면 「처치」가 아니라 「사라짐」이다(판 끝·도움소 흡수 등).
    class BossTrack { public EnemyDummy boss; public string name; public int round; public float bornAt; public float nextSample; public float lastHp = 1f; public string lastSample = ""; }
    static readonly List<BossTrack> bossTracks = new List<BossTrack>();
    static readonly List<(int round, float at)> bossKillSignals = new List<(int, float)>();
    static bool bossSignalHooked;
    static int bossWatchRound = -1;
    static void OnBossKilledSignal(int round) => bossKillSignals.Add((round, Time.time));

    static void WatchLaneBoss(GameShotJob job)
    {
        if (!bossSignalHooked) { EnemyDummy.OnBossKilled += OnBossKilledSignal; bossSignalHooked = true; }
        foreach (EnemyDummy e in EnemyDummy.Active)
        {
            if (e == null || !e.IsBoss || e.LaneIndex != 0 || bossTracks.Any(t => t.boss == e)) continue;
            bossTracks.Add(new BossTrack { boss = e, name = e.name, round = e.SpawnRound, bornAt = Time.time });
            job.report += $"   👑 R{e.SpawnRound} 0번 레인 보스 {e.name} 등장 t={Time.time:F1} · 체력 {e.HpRatio:P0}\n";
        }
        RoundManager rmNow = UnityEngine.Object.FindFirstObjectByType<RoundManager>();
        // 라운드 전환 시각 — 보스 처치가 전환과 겹치는지 보려고(09-25 PM: 판 B 73.5초·D 73.1초가 군대 세기와 무관하게 같았다).
        if (rmNow != null && rmNow.CurrentRound != bossWatchRound)
        {
            if (bossTracks.Count > 0) job.report += $"   👑 라운드 전환 R{bossWatchRound}→R{rmNow.CurrentRound} t={Time.time:F1}\n";
            bossWatchRound = rmNow.CurrentRound;
        }
        for (int i = bossTracks.Count - 1; i >= 0; i--)
        {
            BossTrack t = bossTracks[i];
            if (t.boss != null)
            {
                // 5초마다 체력 · 그때 보스를 사거리 안에 둔 내 유닛(공격자 후보 — EnemyDummy에 마지막 공격자 기록이 없다).
                if (Time.time >= t.nextSample)
                {
                    t.nextSample = Time.time + 5f;
                    t.lastHp = t.boss.HpRatio;
                    Vector3 bp = t.boss.transform.position;
                    var shooters = MyUnits().Where(u => u.TryGetComponent(out UnitAttacker at) && (u.transform.position - bp).sqrMagnitude <= at.AttackRange * at.AttackRange)
                        .GroupBy(u => u.Data.grade).Select(g => $"{g.Key} {g.Count()}");
                    t.lastSample = $"t={Time.time:F1}(등장 뒤 {Time.time - t.bornAt:F0}초) 체력 {t.lastHp:P0} · 사거리 안 내 유닛 {string.Join(", ", shooters)}";
                    job.report += $"   👑 R{t.round} 보스 {t.lastSample}{(rmNow != null ? $" · 라운드 남은 {rmNow.RoundTimeLeft:F1}" : "")}\n";
                }
                continue;
            }
            int signal = bossKillSignals.FindIndex(k => k.round == t.round);
            if (signal >= 0)
            {
                float at = bossKillSignals[signal].at;
                bossKillSignals.RemoveAt(signal);
                job.report += $"   👑 R{t.round} 보스 {t.name} **처치**(TakeDamage 사망 경로 — OnBossKilled 뜸) t={at:F1} · 등장 뒤 {at - t.bornAt:F1}초(구세계 제한 75.3)" +
                              $" · 그때 라운드 R{rmNow?.CurrentRound} 남은 {rmNow?.RoundTimeLeft:F1} · 마지막 표본 {t.lastSample}\n";
            }
            else job.report += $"   👑 R{t.round} 보스 {t.name} **사라짐**(처치 신호 없음 — TakeDamage 밖 경로: 즉시 제거·Destroy) t={Time.time:F1} · 등장 뒤 {Time.time - t.bornAt:F1}초" +
                               $" · 라운드 R{rmNow?.CurrentRound} 남은 {rmNow?.RoundTimeLeft:F1} · 마지막 표본 {t.lastSample}\n";
            bossTracks.RemoveAt(i);
        }
    }

    static string LaneBossesAlive() => bossTracks.Count == 0 ? "" :
        "   👑 끝날 때 살아 있는 0번 레인 보스: " + string.Join(", ", bossTracks.Where(t => t.boss != null)
            .Select(t => $"R{t.round} {t.name} 체력 {t.boss.HpRatio:P0} · 등장 뒤 {Time.time - t.bornAt:F1}초")) + "\n";

    // 🌊 걷는 땅(Walkable) 밖에 선 내 지상 유닛 — 09-25 step3_corner에서 박민석이 섬 왼쪽 절벽 언저리에 서 있었다(재현 안 됨).
    //    재현되면 원인을 가르려고 목적지와 발밑을 같이 찍는다: 목적지가 섬 밖이면 우클릭 지점 탓, 목적지는 섬 안인데 발밑이 아니면 길찾기·밀림 탓.
    static string OffGroundUnits()
    {
        int walkable = 1 << NavMesh.GetAreaFromName("Walkable");
        var off = MyUnits().Where(u => u.Data.movementAbility == MovementAbility.Ground).Select(u =>
        {
            bool ok = NavMesh.SamplePosition(u.transform.position, out NavMeshHit h, 3f, NavMesh.AllAreas) && (h.mask & walkable) != 0;
            if (ok) return null;
            string dest = u.TryGetComponent(out NavMeshAgent a) && a.isOnNavMesh && a.hasPath
                ? $"목적지 {a.destination:F0}({(NavMesh.SamplePosition(a.destination, out NavMeshHit d, 3f, walkable) ? "걷는 땅" : "걷는 땅 아님")}) · 경로 {a.pathStatus}"
                : "목적지 없음";
            return $"{u.name} {u.transform.position:F0} 발밑 {(NavMesh.SamplePosition(u.transform.position, out NavMeshHit f, 3f, NavMesh.AllAreas) ? $"영역마스크 {f.mask}" : "NavMesh 없음")} · {dest}";
        }).Where(x => x != null).ToList();
        return off.Count == 0 ? "" : $"      🌊 걷는 땅 밖에 선 내 지상 유닛 {off.Count}기: {string.Join(" | ", off.Take(5))}\n";
    }

    static void RoundWatch(GameShotJob job)
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f) { frameSum += dt; frameMax = Mathf.Max(frameMax, dt); frameN++; }
        SampleUptime();
        WatchLaneBoss(job);

        RoundManager rm = UnityEngine.Object.FindFirstObjectByType<RoundManager>();
        if (rm == null) return;
        PlayerContext me = PlayerContext.GetOccupied(0);
        bool dead = me != null && me.IsDead;
        bool defeatUi = DefeatShown();
        bool over = rm.IsGameOver || dead || defeatUi;
        int round = rm.CurrentRound;

        if (round != job.lastRoundSeen || (over && !job.overLogged))
        {
            string head = over ? $"💀 끝 — IsDead {dead} · IsGameOver {rm.IsGameOver} · 패배화면 {defeatUi}" : $"🏁 라운드 {job.lastRoundSeen}→{round}";
            if (job.lastRoundSeen == -1 && !over)
            {
                DifficultyManager dm = DifficultyManager.Instance;
                job.report += $"   🎚 난이도 {(dm != null ? $"{dm.Current.KoreanName()}(고름 {dm.IsModeSelected})" : "매니저 없음")} · 이 판이 건 값 {((DifficultyMode)job.mode).KoreanName()}\n";
            }
            job.report += $"   {head}: {RoundMetrics(job, rm)}\n" + ChoiceWispText() + OffGroundUnits();
            string snapPath = Path.GetFullPath(Path.Combine(Folder, "shots", over ? "round_end.png" : $"round_{round:00}.png"));
            ScreenCapture.CaptureScreenshot(snapPath);
            job.report += $"      📸 {snapPath}\n";
            job.lastRoundSeen = round;
            frameSum = frameMax = 0f; frameN = 0;

            if (over) job.overLogged = true;
            if (over || round > job.watchRounds)
            {
                job.report += LaneBossesAlive();
                job.finishNow = true;
                // 남은 예약 동작을 버린다 — 안 버리면 수십 동작을 다 소화하는 동안 판이 계속 흘러 「끝」 뒤의 일이 섞였다(09-24 outbox 2109: R10 뒤 R12 패배까지 흘렀다).
                if (job.clickIndex + 1 < job.clicks.Count) job.clicks.RemoveRange(job.clickIndex + 1, job.clicks.Count - job.clickIndex - 1);
            }
            else if (job.autoLoop && round >= 1) QueueTurn(job);
        }
        if (!job.finishNow && EditorApplication.timeSinceStartup > job.watchDeadline)
        {
            job.report += $"   ⏰ 시간 상한에 닿아 여기서 끝낸다(라운드 {round}): {RoundMetrics(job, rm)}\n";
            job.finishNow = true;
        }
        SaveGameShot(job);
    }

    static IEnumerable<UnitIdentity> MyUnits() =>
        UnityEngine.Object.FindObjectsByType<UnitIdentity>(FindObjectsSortMode.None)
            .Where(u => u != null && u.Data != null && u.GetComponent<Wisp>() == null && (!u.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == 0));

    static string RoundMetrics(GameShotJob job, RoundManager rm)
    {
        PlayerContext me = PlayerContext.GetOccupied(0);
        int gold = me?.GoldWallet?.Gold ?? -1;
        int wood = me?.ResourceWallet != null ? me.ResourceWallet.Get(ResourceType.Wood) : -1;
        var mine = MyUnits().GroupBy(u => u.Data.grade).Select(g => $"{g.Key} {g.Count()}");
        string commonNames = string.Join(" ", MyUnits().Where(u => u.Data.grade == UnitGrade.Common).GroupBy(u => u.Data.unitName)
            .OrderByDescending(g => g.Count()).Select(g => $"{g.Key}{g.Count()}"));
        string pickNames = string.Join(" ", job.choicePicks.GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}{g.Count()}"));
        (string slots, int slotTotal) = ReadWispSlots();
        string story = "";
        GameObject storyPanel = GameObject.Find("StoryPanel");
        if (storyPanel != null)
            story = string.Join(" / ", storyPanel.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text.Trim()).Where(t => t.Length > 0));
        int bosses = EnemyDummy.Active.Count(e => e != null && e.IsBoss);
        string frames = frameN > 0 ? $"{1000f * frameSum / frameN:F1}ms 평균 · {1000f * frameMax:F0}ms 최대" : "-";
        var newKinds = job.logs.Skip(job.logKindsAtRound).Where(l => l.type != "Log").Select(l => $"[{l.type}] {(l.message.Length > 60 ? l.message.Substring(0, 60) + "…" : l.message)}");
        int logTotal = job.logs.Where(l => l.type != "Log").Sum(l => l.count);   // 태그 달린 일반 로그([이동] 등)는 경고·오류가 아니다
        string logs = $"경고·오류 +{logTotal - job.logCountAtRound}건" + (newKinds.Any() ? $"(새 종류: {string.Join(" | ", newKinds)})" : "");
        job.logKindsAtRound = job.logs.Count;
        job.logCountAtRound = logTotal;
        return $"적 레인 {EnemyDummy.CountInLane(0)}/전체 {EnemyDummy.Active.Count(e => e != null)} · 데스카운트 {rm.DeathCountFor(0)} · " +
               $"골드 {gold} · 목재 {wood} · 내 유닛 {(mine.Any() ? string.Join(", ", mine) : "0")} · 위습 칸 「{slots}」 · " +
               $"스토리 「{story}」(매니저 「{StoryManager.Instance?.StatusLabel}」 깸 {StoryManager.Instance?.FinishedCount}) · 보스 적 {bosses} · 남은시간 {rm.RoundTimeLeft:F1}/준비 {rm.PreRoundTimeLeft:F1} · 프레임 {frames} · {UptimeText()} · {logs}" +
               $"\n      🎯 흔함 이름(지금) {(commonNames.Length > 0 ? commonNames : "-")} · 도구가 흔함선택으로 보낸 누계 {(pickNames.Length > 0 ? pickNames : "0")}";
    }

    static readonly (string slot, string wispName, string portal)[] GradeWispRoutes =
    {
        ("안흔함", "안흔함 위습", "Portal_안흔함"),
        ("특별함", "특별함 위습", "Portal_특별함"),
        ("희귀함", "희귀함 위습", "Portal_희귀함·특수함"),
        ("전설·히든", "전설·히든 위습", "Portal_전설·히든"),
    };
    static readonly string[] SpendShops = { "Lane1_유닛강화소", "Lane1_공격타입강화소", "Lane1_도박소" };
    const int ShopSpendClicks = 6;

    // 고른 상점에서 누를 수 있는 칸을 차례로 눌러, **골드가 줄어든 클릭만** 산 것으로 센다. 줄지 않은 칸은 한 번 누르고 넘어간다.
    static string SpendAtShop(GameShotJob job)
    {
        var slots = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(b => b.gameObject.name.StartsWith("UnitCommandSlot") && b.IsActive() && ButtonLabel(b).Length > 0 && !UnitCommandLabels.Contains(ButtonLabel(b)))
            .OrderBy(b => int.TryParse(b.gameObject.name.Substring("UnitCommandSlot".Length), out int n) ? n : 99).ToList();
        int start = PlayerContext.GetOccupied(0)?.GoldWallet?.Gold ?? -1;
        var bought = new List<string>();
        int clicks = 0;
        foreach (UnityEngine.UI.Button b in slots)
        {
            for (int k = 0; k < ShopSpendClicks && clicks < ShopSpendClicks * 2; k++)
            {
                if (!b.IsActive() || !b.IsInteractable()) break;
                int before = PlayerContext.GetOccupied(0)?.GoldWallet?.Gold ?? -1;
                var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                UnityEngine.EventSystems.ExecuteEvents.Execute(b.gameObject, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                clicks++;
                int after = PlayerContext.GetOccupied(0)?.GoldWallet?.Gold ?? -1;
                if (after >= before) break;          // 안 샀다(잠김·목재 부족·이미 최대) — 다음 칸
                bought.Add($"{ButtonLabel(b)} −{before - after}");
            }
        }
        int end = PlayerContext.GetOccupied(0)?.GoldWallet?.Gold ?? -1;
        return $"   🏪 {(bought.Count > 0 ? $"{bought.Count}번 삼: {string.Join(", ", bought.GroupBy(x => x.Split(' ')[0]).Select(g => $"{g.Key}×{g.Count()}"))}" : "산 것 없음")} · 골드 {start} → {end} · 칸 {string.Join(" · ", slots.Select(b => $"「{ButtonLabel(b)}」{(b.IsInteractable() ? "" : "(흐림)")}"))}\n";
    }

    // 사람의 한 턴 — 랜덤유닛 위습을 하나씩 포탈로, 카드별 조합 시도, 전부 모서리로.
    static void QueueTurn(GameShotJob job)
    {
        // 🔴 앞 턴이 아직 안 끝났으면 **시작 안 한 동작은 버리고** 지금 상태로 다시 짠다(09-25 판 D 재시도).
        //    한 턴(위습·조합 카드·모서리·상점 셋, 36동작 + 12초 대기)이 라운드 40초 안에 다 못 끝나 다음 턴과 겹쳤다 —
        //    앞 턴이 센 위습은 이미 쓰였고(「안흔함 없음」), 새로 받은 랜덤유닛 위습은 뒤로 밀려 칸에 4 → 8로 쌓였다.
        //    지금 하던 동작(clickIndex) 하나는 끝까지 둔다 — 누른 채 버리면 마우스가 눌린 채 남는다.
        if (job.clickIndex + 1 < job.clicks.Count)
        {
            int dropped = job.clicks.Count - job.clickIndex - 1;
            job.clicks.RemoveRange(job.clickIndex + 1, dropped);
            job.report += $"   ⏭ 앞 턴이 안 끝나 남은 {dropped}동작을 버리고 새로 짬\n";
        }
        var myWisps = UnityEngine.Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None)
            .Where(w => w != null && w.Data != null && (!w.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == 0)).ToList();
        int randomWisps = myWisps.Count(w => (w.Data.wispName ?? "").Contains("랜덤유닛"));
        List<string> turn = new List<string>();
        // 라운드가 바뀌는 순간 받은 위습은 위습 칸에 바로 안 켜진다(칸은 주기적으로 갱신) — 첫 칸을 「없음」으로 건너뛰었다(09-25 i1_16).
        if (randomWisps > 0) turn.Add("@wait:2");
        for (int i = 0; i < randomWisps; i++) { turn.Add("click?:랜덤유닛".Replace("click?:", "?")); turn.Add("@rc:Portal_유닛랜덤"); }
        // 위 등급 위습(보스·스토리 보상) — 각자의 등급 포탈로(09-25 PM 지시). 판 B는 R12에 안흔함 9·특별함 7이 칸에 쌓인 채 끝났다.
        //    칸 글자(위습 칸 표시 「안흔함 9」의 앞말)와 포탈 이름. 칸 수만큼 보낸다 — 랜덤유닛 위습과 같은 「칸 클릭 → 포탈 우클릭」.
        int gradeWisps = 0;
        foreach ((string slot, string wispName, string portal) in GradeWispRoutes)
        {
            int n = myWisps.Count(w => (w.Data.wispName ?? "") == wispName);
            for (int i = 0; i < n; i++) { turn.Add("?" + slot); turn.Add("@rc:" + portal); }
            gradeWisps += n;
        }
        if (randomWisps + gradeWisps > 0) turn.Add("@wait:12");
        for (int k = 0; k < (job.noCombine ? 0 : 3); k++)   // nocombine — 일부러 약한 판(보스 제한 패배 확인용, 09-25)
        {
            turn.Add("@box:Unit_흔함");
            turn.Add($"?Card{k}");
            turn.Add("?UnitCommandSlot12");
            turn.Add("?UnitCommandSlot13");
        }
        turn.Add("@box:Unit_");
        turn.Add("@rcpt:corner");
        // 흔함 선택 위습 — 「흔함선택_<이름>」 포탈에 넣어 그 유닛을 고른다(isPlayerChoice를 읽는 코드는 없고, 포탈 specificUnit이 길이다).
        //    짝이 안 맞는(홀수) 흔함이 있으면 그 이름을 골라 조합 재료를 채우고, 없으면 돌아가며 고른다.
        int choiceWisps = myWisps.Count(w => (w.Data.wispName ?? "").Contains("흔함 선택"));
        List<string> choicePortals = UnityEngine.Object.FindObjectsByType<UnitPortal>(FindObjectsSortMode.None)
            .Select(p => p.gameObject.name).Where(n => n.StartsWith("흔함선택_")).Distinct().OrderBy(n => n).ToList();
        var myCommonCounts = MyUnits().Where(u => u.Data.grade == UnitGrade.Common)
            .GroupBy(u => u.Data.unitName).ToDictionary(g => g.Key ?? "", g => g.Count());
        List<string> picked = new List<string>();
        for (int i = 0; i < choiceWisps && choicePortals.Count > 0; i++)
        {
            string odd = choicePortals.FirstOrDefault(n => myCommonCounts.TryGetValue(n.Substring(5), out int c) && c % 2 == 1);
            string portal = odd ?? choicePortals[(job.lastRoundSeen + i) % choicePortals.Count];
            string unit = portal.Substring(5);
            myCommonCounts[unit] = (myCommonCounts.TryGetValue(unit, out int had) ? had : 0) + 1;
            turn.Insert(0, "@rc:" + portal);
            turn.Insert(0, "?흔함 선택");
            picked.Add(unit);
            job.choicePicks.Add(unit);
        }
        if (picked.Count > 0) turn.Insert(2 * picked.Count, "@wait:12");

        // 상점 — 라운드 2부터 **매 턴** 남는 골드를 쓴다(09-25 PM 지시 — 판 B는 R12에 골드 18,422를 안 쓰고 끝났다).
        //    등급 강화소 → 공격타입 강화소 → 도박소 순으로 골라, 골드가 실제로 줄어드는 칸을 몇 번씩 누른다(@shopspend).
        //    ⚠️ 무엇을 사야 이득인지는 판단하지 않는다 — 「사람이 남는 돈을 상점에 쓴다」의 근사다. 결과는 🏪 줄의 골드 차이로 본다.
        StoryManager story = StoryManager.Instance;

        // 스토리 — 진행 중이면 안흔함을 스토리 포탈로 보내고, 그 스토리가 깨지면 복귀포탈로 되돌린다.
        string storyPlan = "";
        if (story != null && !job.storySent && story.Running != null && MyUnits().Any(u => u.Data.grade == UnitGrade.Uncommon))
        {
            job.storySent = true;
            job.storyFinishedAtSend = story.FinishedCount;
            // 박스는 모서리에 뭉친 흔함까지 같이 잡혀 실패했다(outbox 2112 ⛔) — 안흔함 한 기를 좌클릭으로 고른다. 사슬 시험엔 한 기면 된다.
            turn.Add("@sel:Unit_안흔함");
            turn.Add("@rc:Lane1_스토리포탈");
            storyPlan = $" · 안흔함 → 스토리존(「{story.StatusLabel}」)";
        }
        else if (story != null && job.storySent && story.FinishedCount > job.storyFinishedAtSend)
        {
            job.storySent = false;
            turn.Add("@sel:Unit_안흔함");
            turn.Add("@rc:스토리_복귀포탈");
            storyPlan = $" · 스토리 {story.FinishedCount - job.storyFinishedAtSend}개 깸 → 안흔함 복귀";
        }
        // 스토리존에 가 있는 동안엔 안흔함을 다시 끌어내지 않도록 흔함만 모서리로 보낸다.
        // 한 화면에 안 들어오는 무리(우리·레인 가운데의 조합 결과·모서리)는 첫 박스가 일부만 잡는다 —
        //    원작도 흔함만 고정 칸이고 조합 결과는 레인 가운데에 나와 사람이 옮긴다. 모서리 밖에 남은 것만 두 번 더 쓸어 보낸다.
        string sweep = job.storySent ? "@box:Unit_흔함" : "@box:Unit_";
        turn.Add(sweep);
        turn.Add("@rcpt:corner");
        for (int k = 0; k < 2; k++) { turn.Add(sweep + "|far"); turn.Add("@rcpt:corner"); }
        // 상점은 **맨 뒤** — 턴이 라운드 안에 못 끝나 다음 턴이 남은 동작을 버릴 때, 싸우는 자리로 옮기기보다 상점이 먼저 빠지게.
        if (job.lastRoundSeen >= 2 && !job.noShop)   // noshop — 일부러 약한 판
        {
            job.shopTried = true;
            foreach (string shop in SpendShops)
            {
                turn.Add("@sel:" + shop);
                turn.Add("?@shopspend");
            }
        }

        job.clicks.AddRange(turn);
        job.report += $"   🔁 한 턴 예약: 흔함 선택 {picked.Count}기({string.Join(", ", picked)}) · 랜덤유닛 위습 {randomWisps}기 → 포탈 · 조합 시도{storyPlan} · 모서리로 이동({turn.Count}동작)\n";
        if (job.stage == "waiting") { job.stage = "clicking"; job.stageSince = EditorApplication.timeSinceStartup; }
    }

    // 0번 레인 적 경로의 안쪽 모서리 지점 — 두 변이 만나는 곳이라 한 자리에서 두 변을 친다. inset = 경로에서 떨어질 거리.
    static bool TryCornerTarget(float inset, out Vector3 target)
    {
        target = Vector3.zero;
        LaneMarker lane = LaneMarker.Get(0);
        WaypointPath path = lane != null ? LanePathNear(lane.LaneCenter) : null;
        if (path == null || path.PointCount < 3) return false;
        for (int i = 1; i + 1 < path.PointCount; i++)
        {
            Vector3 before = path.GetPoint(i) - path.GetPoint(i - 1), after = path.GetPoint(i + 1) - path.GetPoint(i);
            before.y = after.y = 0f;
            if (before.sqrMagnitude < 1f || after.sqrMagnitude < 1f || Vector3.Angle(before, after) <= 45f) continue;
            Vector3 corner = path.GetPoint(i);
            Vector3 diagonal = lane.LaneCenter - corner;
            diagonal.y = 0f;
            target = corner + diagonal.normalized * inset * 1.4142f;
            target.y = lane.LaneCenter.y;
            return true;
        }
        return false;
    }

    // rclickpt: — 월드 지점 하나를 우클릭한다(대상 오브젝트 없이 땅). 0 조준(필요하면 카메라) → 1 누름 → 2 뗌 → 결과.
    static bool StepPointAt(GameShotJob job, string which, double inStage)
    {
        if (job.pointerPhase > 0 && !PointerFramePassed()) return false;   // 앞 마우스 이벤트가 게임 프레임에 먹히기 전(QueueMouse 주석)
        Camera cam = Camera.main;
        switch (job.pointerPhase)
        {
            case 0:
            {
                if (which != "corner" || !TryCornerTarget(50f, out Vector3 aim)) { job.report += $"   🖱 rclickpt:{which}: 지점을 못 정함 — 건너뜀\n"; return true; }
                Vector3 sp = cam.WorldToScreenPoint(aim);
                (float bandBottom, float bandTop) = PointerBand();
                bool visible = sp.z > 0f && sp.x > 20f && sp.x < cam.pixelWidth - 20f && sp.y > bandBottom * cam.pixelHeight + 10f && sp.y < bandTop * cam.pixelHeight - 10f;
                if (!visible)
                {
                    RtsCameraController rts = cam.GetComponent<RtsCameraController>();
                    if (rts == null || job.pointerX < 0f) { job.report += $"   🖱 rclickpt:{which}: {aim.ToString("F0")}이 화면에 안 들어와 건너뜀\n"; job.pointerX = 0f; return true; }
                    rts.MoveTo(new Vector3(aim.x, 0f, aim.z));
                    job.pointerX = -1f;
                    SaveGameShot(job);
                    return false;
                }
                EnsureShotMouse();
                job.pointerX = sp.x; job.pointerY = sp.y;
                QueueMouse(new Vector2(sp.x, sp.y), MouseButton.Right, false);
                job.pointerPhase = 1;
                SaveGameShot(job);
                return false;
            }
            case 1: QueueMouse(new Vector2(job.pointerX, job.pointerY), MouseButton.Right, true); job.pointerPhase = 2; SaveGameShot(job); return false;
            case 2: QueueMouse(new Vector2(job.pointerX, job.pointerY), MouseButton.Right, false); job.pointerPhase = 3; SaveGameShot(job); return false;
            default:
            {
                SelectionManager selection = UnityEngine.Object.FindFirstObjectByType<SelectionManager>();
                int n = selection != null ? selection.Selected.Count(x => x != null) : 0;
                job.report += $"   🖱 우클릭 rclickpt:{which} @ 화면 ({job.pointerX:F0}, {job.pointerY:F0}) → 선택 {n}기에 이동 명령\n";
                job.pointerX = 0f;
                ParkShotMouse(cam);
                return true;
            }
        }
    }

    // boxselect: — 그 이름 내 유닛들을 화면에서 감싸는 사각형을 드래그한다. 0 조준(필요하면 카메라) → 1 누름 → 2 끌기 → 3 뗌 → 결과.
    static Vector2 boxStart, boxEnd;
    // 마지막 boxselect가 **실제로** 고른 대상 수. 0이면 드래그가 안 먹은 것이다 — SelectionManager는 누른 자리가 uGUI 위면
    //    드래그를 통째로 버리고, 그러면 앞 동작의 선택이 그대로 남는다. 09-25 i1_07: 「Unit_」 박스가 안 먹었는데 앞의 「Unit_흔함」
    //    선택(흔함 2기)이 남아 「지금 선택」에 찍혀 성공처럼 보였고, 레인 가운데의 조합 결과는 끝까지 안 움직여 R3에 졌다.
    static int lastBoxPicked;
    static int dragFailDiags;   // 판마다 0부터 — 도메인 리로드(플레이 진입)가 정적 값을 되돌린다
    static readonly List<UnityEngine.EventSystems.RaycastResult> uiHits = new List<UnityEngine.EventSystems.RaycastResult>();

    static bool OverUi(Vector2 screen)
    {
        UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        uiHits.Clear();
        es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = screen }, uiHits);
        return uiHits.Count > 0;
    }

    // 「|far」 — 이미 모서리에 있거나 모서리로 가는 중인 유닛은 뺀다. 유닛이 우리·레인 가운데·모서리로 흩어져 한 화면에 안 들어올 때
    //    남은 무리만 다시 골라 보내는 데 쓴다(QueueTurn이 두 번 더 부른다).
    const float CornerNearRadius = 100f;
    static bool AtOrHeadingToCorner(Selectable s, Vector3 corner)
    {
        Vector2 c = new Vector2(corner.x, corner.z);
        if (Vector2.Distance(new Vector2(s.transform.position.x, s.transform.position.z), c) <= CornerNearRadius) return true;
        return s.TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh && agent.hasPath &&
               Vector2.Distance(new Vector2(agent.destination.x, agent.destination.z), c) <= CornerNearRadius;
    }

    static bool StepBox(GameShotJob job, string name, double inStage)
    {
        if (job.pointerPhase > 0 && !PointerFramePassed()) return false;   // 앞 마우스 이벤트가 게임 프레임에 먹히기 전(QueueMouse 주석)
        Camera cam = Camera.main;
        bool farOnly = name.EndsWith("|far");
        string wanted = (farOnly ? name.Substring(0, name.Length - 4) : name).Normalize(NormalizationForm.FormC);
        switch (job.pointerPhase)
        {
            case 0:
            {
                List<Selectable> targets = Selectable.All.Where(x => x != null && x.name.Normalize(NormalizationForm.FormC).Contains(wanted) &&
                    (!x.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == LocalPlayer.LocalPlayerId)).ToList();
                if (farOnly)
                {
                    if (!TryCornerTarget(50f, out Vector3 corner)) targets.Clear();
                    else targets = targets.Where(t => !AtOrHeadingToCorner(t, corner)).ToList();
                    if (targets.Count == 0)
                    {
                        job.report += $"   🖱 드래그 boxselect 「{wanted}」(모서리 밖만): 남은 유닛 없음 — 건너뜀\n";
                        lastBoxPicked = 0;
                        job.pointerX = 0f;
                        return true;
                    }
                }
                if (targets.Count == 0 || cam == null)
                {
                    if (inStage < GameShotOptionalClickSearch) return false;
                    // 판을 끝내지 않는다 — 조합으로 재료가 다 쓰이면 정상적으로 0기가 된다(09-24 loop7: 여기서 끝나 결과 🏅 줄을 못 찍었다).
                    job.report += $"   🖱 드래그 boxselect 「{wanted}」: 내 유닛이 0기라 건너뜀\n";
                    lastBoxPicked = 0;
                    job.pointerX = 0f;
                    return true;
                }
                (float bandBottom, float bandTop) = PointerBand();
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue), max = new Vector3(float.MinValue, float.MinValue);
                bool allVisible = true;
                foreach (Selectable t in targets)
                {
                    Vector3 sp = cam.WorldToScreenPoint(t.transform.position);
                    allVisible &= sp.z > 0f && sp.x > 20f && sp.x < cam.pixelWidth - 20f && sp.y > bandBottom * cam.pixelHeight + 10f && sp.y < bandTop * cam.pixelHeight - 10f;
                    min = Vector3.Min(min, sp); max = Vector3.Max(max, sp);
                }
                if (!allVisible)
                {
                    RtsCameraController rts = cam.GetComponent<RtsCameraController>();
                    if (rts == null) { FailGameShot(job, $"boxselect: 「{wanted}」 {targets.Count}기가 화면 밖인데 카메라를 옮길 수 없다"); return false; }
                    if (job.pointerX < 0f)
                    {
                        // 카메라를 한 번 옮겨도 다 안 들어오면 **보이는 것만** 고른다(판을 끝내지 않는다 — 긴 판에서 유닛이 우리·가운데·모서리로 흩어진다).
                        List<Selectable> visibleOnes = targets.Where(t => { Vector3 v = cam.WorldToScreenPoint(t.transform.position);
                            return v.z > 0f && v.x > 20f && v.x < cam.pixelWidth - 20f && v.y > bandBottom * cam.pixelHeight + 10f && v.y < bandTop * cam.pixelHeight - 10f; }).ToList();
                        if (visibleOnes.Count == 0) { job.report += $"   🖱 드래그 boxselect 「{wanted}」: {targets.Count}기가 한 화면에 없어 건너뜀\n"; lastBoxPicked = 0; job.pointerX = 0f; return true; }
                        job.report += $"   🖱 드래그 boxselect 「{wanted}」: {targets.Count}기 중 한 화면에 든 {visibleOnes.Count}기만 고름\n";
                        min = new Vector3(float.MaxValue, float.MaxValue); max = new Vector3(float.MinValue, float.MinValue);
                        foreach (Selectable t in visibleOnes) { Vector3 v = cam.WorldToScreenPoint(t.transform.position); min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
                        allVisible = true;
                    }
                }
                if (!allVisible)
                {
                    RtsCameraController rts = cam.GetComponent<RtsCameraController>();
                    Vector3 centroid = targets.Aggregate(Vector3.zero, (acc, t) => acc + t.transform.position) / targets.Count;
                    rts.MoveTo(new Vector3(centroid.x, 0f, centroid.z));
                    job.report += $"   🎥 「{wanted}」 {targets.Count}기가 다 안 보여 카메라를 옮김(MoveTo {centroid.ToString("F0")})\n";
                    job.pointerX = -1f;
                    SaveGameShot(job);
                    return false;
                }
                const float pad = 40f;
                float bottomLimit = bandBottom * cam.pixelHeight + 2f, topLimit = bandTop * cam.pixelHeight - 2f;
                float x0 = Mathf.Max(1f, min.x - pad), x1 = Mathf.Min(cam.pixelWidth - 1f, max.x + pad);
                float y0 = Mathf.Clamp(min.y - pad, bottomLimit, topLimit), y1 = Mathf.Clamp(max.y + pad * 2f, bottomLimit, topLimit);
                // 드래그는 **누른 자리**가 uGUI(이름표·체력바·HUD) 위면 통째로 버려진다(SelectionManager.ignoreCurrentPress).
                //    네 귀퉁이 중 UI가 없는 곳에서 시작한다 — 사각형은 같고 끌어가는 방향만 바뀐다.
                Vector2[] corners4 = { new Vector2(x0, y0), new Vector2(x1, y1), new Vector2(x0, y1), new Vector2(x1, y0) };
                int startIndex = Array.FindIndex(corners4, c => !OverUi(c));
                if (startIndex < 0)
                {
                    job.report += $"   ⚠️ boxselect 「{wanted}」: 사각형 네 귀퉁이가 전부 UI 위라 드래그가 안 먹을 수 있다(그래도 시도)\n";
                    startIndex = 0;
                }
                boxStart = corners4[startIndex];
                boxEnd = new Vector2(boxStart.x == x0 ? x1 : x0, boxStart.y == y0 ? y1 : y0);
                EnsureShotMouse();
                QueueMouse(boxStart, MouseButton.Left, false);
                job.pointerX = 1f;
                job.pointerPhase = 1;
                SaveGameShot(job);
                return false;
            }
            case 1: QueueMouse(boxStart, MouseButton.Left, true); job.pointerPhase = 2; SaveGameShot(job); return false;
            case 2: QueueMouse(boxEnd, MouseButton.Left, true); job.pointerPhase = 3; SaveGameShot(job); return false;   // 누른 채 끌기
            case 3: QueueMouse(boxEnd, MouseButton.Left, false); job.pointerPhase = 4; SaveGameShot(job); return false;  // 뗌
            default:
            {
                SelectionManager selection = UnityEngine.Object.FindFirstObjectByType<SelectionManager>();
                var chosen = selection != null ? selection.Selected.Where(x => x != null).GroupBy(x => x.name).Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key) : Enumerable.Empty<string>();
                // 「지금 선택」은 앞 동작이 남긴 것일 수 있다 — 이 드래그가 **바꾼** 선택인지 따로 센다.
                //    SelectInBox는 선택을 비우고 사각형 안의 내 것을 전부 담는다 → 먹었다면 「선택 = 사각형 안의 내 것」이다.
                //    어긋나면(사각형 안인데 안 골림, 사각형 밖인데 골려 있음) 드래그가 버려지고 앞 선택이 남은 것이다.
                Rect rect = Rect.MinMaxRect(Mathf.Min(boxStart.x, boxEnd.x), Mathf.Min(boxStart.y, boxEnd.y), Mathf.Max(boxStart.x, boxEnd.x), Mathf.Max(boxStart.y, boxEnd.y));
                bool InRect(Selectable x) { Vector3 v = cam.WorldToScreenPoint(x.transform.position); return v.z > 0f && rect.Contains(new Vector2(v.x, v.y)); }
                var picked = selection != null ? selection.Selected.Where(x => x != null).ToList() : new List<Selectable>();
                var mineInRect = Selectable.All.Where(x => x != null && (!x.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == LocalPlayer.LocalPlayerId) && InRect(x)).ToList();
                // ⚠️ SelectionManager.maxSelection(12)에 닿으면 사각형 안이 12기를 넘어도 12기만 골린다 — 그때 「사각형 안 = 선택」을
                //    요구하면 **먹은 드래그를 안 먹었다고** 판정한다(09-25 판 B: R5부터 전부 ⚠️, 그 탓에 모서리 이동까지 버렸다).
                int cap = selection != null && typeof(SelectionManager).GetField("maxSelection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(selection) is int m ? m : 0;
                bool full = cap > 0 && picked.Count >= cap;
                bool dragTook = picked.Count > 0 && picked.All(InRect) && (full || mineInRect.All(picked.Contains));
                lastBoxPicked = dragTook ? picked.Count(x => x.name.Normalize(NormalizationForm.FormC).Contains(wanted)) : 0;
                job.report += $"   🖱 드래그 boxselect 「{wanted}{(farOnly ? "」(모서리 밖만)" : "」")} ({boxStart.x:F0},{boxStart.y:F0})→({boxEnd.x:F0},{boxEnd.y:F0}) → 지금 선택: {string.Join(", ", chosen)}" +
                              (dragTook ? (full ? $" (선택 상한 {cap}기에 닿음 — 사각형 안 {mineInRect.Count}기)" : "") : " ⚠️ 드래그가 안 먹음(누른 자리가 UI 위?) — 앞 선택이 남은 것") + "\n";
                // 안 먹은 첫 두 번은 입력 상태를 통째로 싣는다(09-25 판 F: R6부터 드래그·우클릭이 전부 안 먹었는데 원인을 못 가렸다).
                if (!dragTook && dragFailDiags < 2)
                {
                    dragFailDiags++;
                    job.report += WispPortalProbe.InputDiag() + "\n";
                }
                ParkShotMouse(cam);
                job.pointerX = 0f;
                return true;
            }
        }
    }

    static string DescribeBody(GameObject go)
    {
        if (go == null) return "(없음)";
        Bounds? b = null;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (b == null) b = r.bounds; else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; }
        }
        if (b == null) return $"{go.name}(렌더러 없음)";
        float ground = go.transform.position.y;
        // 사람형이면 어깨 높이도 잰다 — 경계 상자는 꼬리·무기로 부푼다(09-24 PM·blender 꼬리 사례). 어깨가 몸집 비교의 기준이다.
        string shoulder = "";
        Animator anim = go.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            Transform l = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm), r = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (l != null && r != null) shoulder = $" · 어깨 높이 {(l.position.y + r.position.y) * 0.5f - ground:F1} · 어깨 너비 {Vector3.Distance(l.position, r.position):F1}";
        }
        return $"{go.name} 키 {b.Value.max.y - ground:F1}(땅 위, 렌더러 경계 윗면){shoulder} · 경계 폭 {b.Value.size.x:F1}×{b.Value.size.z:F1} · 바닥 {b.Value.min.y - ground:F1}(음수면 땅속)";
    }

    static int CountMyWisps() => UnityEngine.Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None)
        .Count(w => w != null && w.Data != null && (!w.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == LocalPlayer.LocalPlayerId));

    // HUD가 실제로 보여 주는 위습 칸(GameHud WispSlot0~17 — 종류마다 한 칸, 자식 Name·Count TMP, 0개인 종류는 칸이 꺼짐).
    // 09-24 PM이 「위습 N」 글자(WispCountText)를 이 칸 줄로 바꿨다. 반환: ("랜덤유닛 5 · …", 합계). 칸을 못 찾으면 합계 −1.
    static (string text, int total) ReadWispSlots()
    {
        List<string> parts = new List<string>();
        int total = 0;
        bool any = false;
        // 꺼진 칸도 찾는다 — 위습이 다 빠지면 칸이 전부 꺼져 「칸을 못 찾음」으로 나왔다(09-24 loop5). 꺼진 칸은 0으로 친다.
        foreach (TMPro.TMP_Text count in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (count.name != "Count" || count.transform.parent == null || !count.transform.parent.name.StartsWith("WispSlot")) continue;
            any = true;
            if (!count.gameObject.activeInHierarchy) continue;
            Transform nameNode = count.transform.parent.Find("Name");
            string label = nameNode != null && nameNode.TryGetComponent(out TMPro.TMP_Text n) ? n.text : count.transform.parent.name;
            if (int.TryParse(count.text.Trim(), out int value)) total += value;
            parts.Add($"{label} {count.text.Trim()}");
        }
        if (!any) return ("(WispSlot 칸을 못 찾음)", -1);
        return (parts.Count > 0 ? string.Join(" · ", parts) : "(켜진 칸 없음)", total);
    }

    static string ReadWispCounter() => ReadWispSlots().text;

    // 클릭해도 되는 세로 띠 — 하단 바 위·상단 바 아래(카메라 구도와 같은 HUD 실측). 못 찾으면 화면 전체.
    static (float bottom, float top) PointerBand()
    {
        Canvas.ForceUpdateCanvases();
        float bottom = 0f, top = 1f;
        foreach ((string objName, bool isBottom) in new[] { ("BottomBar", true), ("TopBar", false) })
        {
            GameObject found = GameObject.Find(objName);
            if (found == null || !(found.transform is RectTransform rect) || Screen.height <= 0) continue;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            if (isBottom) bottom = Mathf.Max(corners[0].y, corners[2].y) / Screen.height;
            else top = Mathf.Min(corners[0].y, corners[2].y) / Screen.height;
        }
        return (bottom, top);
    }

    static void Advance(GameShotJob job, string stage)
    {
        job.stage = stage;
        job.stageSince = EditorApplication.timeSinceStartup;
        job.lastSize = -1;
        SaveGameShot(job);
    }

    // 실패도 반드시 outbox에 남긴다 — 빈 결과가 제일 나쁘다. 플레이 중이면 먼저 빠져나온 뒤 쓴다.
    static void FailGameShot(GameShotJob job, string why)
    {
        job.failed = true;
        job.report += $"   ❌ {why}\n";
        if (EditorApplication.isPlaying)
        {
            Advance(job, "exiting");
            EditorApplication.isPlaying = false;
        }
        else FinishGameShot(job);
    }

    static void FinishGameShot(GameShotJob job)
    {
        ReleaseShotMouse();
        // 사장님 기억 난이도를 되돌린다(판이 바꿔 둔 것).
        if (job.prevSavedMode == int.MinValue) PlayerPrefs.DeleteKey(DifficultyManager.SavedModeKey);
        else PlayerPrefs.SetInt(DifficultyManager.SavedModeKey, job.prevSavedMode);
        PlayerPrefs.Save();
        SessionState.EraseString(GameShotKey);
        StringBuilder text = new StringBuilder(job.prefix);
        string tainted = job.midPlayReloads > 0 ? $" (⚠️ 오염 — 플레이 도중 도메인 리로드 {job.midPlayReloads}번)" : "";
        text.AppendLine((job.failed ? "▶ gameshot 결과: ❌ 실패" : "▶ gameshot 결과: ✅") + tainted);
        text.Append(job.report);
        if (job.midPlayReloads > 0)
            text.AppendLine($"   ⚠️ 리로드 전까지 경고·오류 {job.logsBeforeReload}건. 리로드는 직렬화 안 된 필드(Awake에서 만든 것)를 null로 날리고 " +
                            "Awake를 다시 안 부른다 — 그 뒤의 NullReference는 빌드에선 안 날 수 있다. 임포트가 끝난 조용한 에디터에서 다시 찍을 것.");
        if (job.logs.Count > 0)
        {
            int total = job.logs.Sum(l => l.count) + job.droppedLogs;
            text.AppendLine($"— 플레이 중 경고·오류 {total}건({job.logs.Count}종, 같은 메시지+스택은 묶음) —");
            foreach (GameShotLog log in job.logs)
            {
                text.AppendLine($"[{log.type}] ×{log.count} {log.message}");
                if (log.stack.Length > 0) text.Append(log.stack);
            }
            if (job.droppedLogs > 0) text.AppendLine($"   …종류 상한 {GameShotMaxLogKinds}을 넘은 {job.droppedLogs}건은 생략");
        }
        WriteResult(job.id, text.ToString());
    }

    // 플레이 중에 난 경고·오류를 결과에 싣는다(UI가 안 떴다면 이유가 대개 여기 있다). 일반 로그는 너무 많아 뺀다.
    // Exception·Error·Assert는 스택 첫 4줄과, 그 안에 없으면 첫 Assets/ 프레임을 붙인다 — 「어디서 터지는지」가 보이게.
    static void CollectGameShotLog(string message, string stack, LogType type)
    {
        // 일반 로그는 너무 많아 버리되, 입력 경로가 「왜 안 먹었는지」 말하는 태그 줄은 싣는다(select:/rclick: 판정용, 09-24).
        if (type == LogType.Log && !message.StartsWith("[")) return;   // 태그 달린 일반 로그([이동]·[선택]·[명령]·[도박] 등)는 싣는다 — 같은 줄은 묶인다
        GameShotJob job = LoadGameShot();
        if (job == null) return;

        // [데스] 한 줄마다 시각과 함께 본문에 바로 적는다 — 0.65초 간격·9부터 줄어드는지 보려면 묶으면 안 된다.
        if (type == LogType.Log && message.StartsWith("[데스]"))
        {
            RoundManager rm = UnityEngine.Object.FindFirstObjectByType<RoundManager>();
            job.report += $"   ☠️ t={Time.time:F2} 라운드 {rm?.CurrentRound} · {message.Substring(5).Trim()}\n";
            SaveGameShot(job);
            return;
        }

        string head = message.Length > 400 ? message.Substring(0, 400) + " …" : message;
        string frames = "";
        if (type != LogType.Warning && !string.IsNullOrEmpty(stack))
        {
            string[] lines = stack.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            List<string> keep = lines.Take(4).ToList();
            string ours = lines.FirstOrDefault(l => l.Contains("Assets/"));
            if (ours != null && !keep.Contains(ours)) keep.Add("… " + ours);
            frames = string.Concat(keep.Select(l => $"      at {l}\n"));
        }

        GameShotLog same = job.logs.FirstOrDefault(l => l.type == type.ToString() && l.message == head && l.stack == frames);
        if (same != null) same.count++;
        else if (job.logs.Count < GameShotMaxLogKinds) job.logs.Add(new GameShotLog { type = type.ToString(), message = head, stack = frames, count = 1 });
        else job.droppedLogs++;
        SaveGameShot(job);
    }

    // PNG를 열어 크기를 읽고, 16×16 격자로 떠서 단색인지 본다 — 「파일은 있는데 까맣다」를 숫자로 가른다.
    static string DescribePng(string label, string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        try
        {
            if (!texture.LoadImage(bytes)) return $"{label} ❌ PNG로 못 읽음: {path} ({bytes.Length}바이트)";
            HashSet<Color32> colors = new HashSet<Color32>();
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    colors.Add(texture.GetPixel(x * texture.width / 16, y * texture.height / 16));
            string flat = colors.Count <= 1 ? " · ⚠️ 단색 — 화면이 안 그려졌을 수 있다" : $" · 표본 색 {colors.Count}가지";
            return $"{label} {path} ({texture.width}×{texture.height}, {bytes.Length / 1024}KB{flat})";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
