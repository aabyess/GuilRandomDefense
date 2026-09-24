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
                // 앞줄이 카메라에 가깝게: 0행이 맨 뒤(위쪽)
                instance.transform.position = new Vector3(column * cell, 0f, (rows - 1 - row) * cell);
                table.AppendLine($"   {row + 1}행 {column + 1}열 = {prefabs[i].name}");
            }

            Vector3 center = new Vector3((perRow - 1) * cell * 0.5f, 8f, (rows - 1) * cell * 0.5f);
            float span = Mathf.Max(perRow, rows) * cell;
            string shot = Render(file, center + new Vector3(0f, span * 0.55f, -span * 0.85f), center, 50f, preview);
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
                instance.transform.position = new Vector3(column * cell, 0f, row * cell);   // 1행이 맨 앞
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
                    //    실제 몸의 축(Spine→Head)은 (0, 0.97, 0.23), 정면 사진도 정상이었다(outbox 1261).
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

            Vector3 center = new Vector3((perRow - 1) * cell * 0.5f, 8f, (rows - 1) * cell * 0.5f);
            float span = perRow * cell;
            string shot = Render(file, center + new Vector3(0f, span * 0.3f, -span * 0.95f), center, 45f, preview);
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
    //                  둘 다 **게임의 실제 입력 경로**를 탄다 — 가상 마우스 장치(Input System)에 누르기·떼기 이벤트를 넣어
    //                  SelectionManager·UnitMover가 평소처럼 Mouse.current를 읽고 WorldPick으로 레이캐스트한다. 내부 상태를 직접 안 만진다
    //                  (spawn:처럼 상태를 직접 만들면 실제와 갈라진다 — 09-24 Warp 결함). click:과 한 줄에 섞어 적은 순서대로 돈다.
    //                  대상이 화면 밖이거나 하단 HUD 뒤면 미니맵 클릭과 같은 RtsCameraController.MoveTo로 카메라를 먼저 옮긴다.
    //                  에디터 입력이 Game 뷰 포커스를 따지지 않게 그동안만 editorInputBehaviorInPlayMode를 바꾸고 끝나면 되돌린다.
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
    const double GameShotSettle = 1.0, GameShotClickGap = 1.0, GameShotClickSearch = 5.0, GameShotOptionalClickSearch = 2.0;
    const int GameShotMaxLogKinds = 40;

    [Serializable]
    class GameShotJob
    {
        public string id;           // outbox 번호
        public string file;         // ScreenCapture 결과(절대 경로)
        public float seconds;
        public int superSize = 1;
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

        string name = parts[0].EndsWith(".png") ? parts[0] : parts[0] + ".png";
        GameShotJob job = new GameShotJob { id = currentId, seconds = 3f };
        StringBuilder report = new StringBuilder();

        // 인자는 모양으로 가른다 — 순서를 외울 필요가 없게.
        foreach (string token in parts.Skip(1))
        {
            if (token.StartsWith("select:") || token.StartsWith("rclick:"))
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
        job.report = report.ToString();
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
                if (target.StartsWith("@sel:") || target.StartsWith("@rc:"))
                {
                    if (!StepPointer(job, target, inStage)) break;   // 아직 진행 중
                    job.clickIndex++;
                    job.pointerPhase = 0;
                    Advance(job, job.clickIndex < job.clicks.Count ? "clicking" : job.spawns.Count + job.combines.Count > 0 ? "spawning" : "waiting");
                    break;
                }
                bool optional = target.StartsWith("?");
                if (optional) target = target.Substring(1);
                string clicked = ClickButton(target);
                if (clicked == null && optional && inStage > GameShotOptionalClickSearch)
                {
                    job.report += $"   🖱 {job.clickIndex + 1}번째 클릭: 「{target}」 없음 — 선택 클릭이라 건너뜀\n";
                    job.clickIndex++;
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
                if (inStage >= job.seconds)
                {
                    if (job.clicks.Any(c => c.StartsWith("@")))
                    {
                        // select:/rclick:을 쓴 판은 끝에 내 유닛 목록을 남긴다 — 위습이 포탈에 들어가 뽑기가 됐는지 여기서 본다.
                        var mine = Selectable.All.Where(x => x != null && (!x.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == LocalPlayer.LocalPlayerId))
                            .GroupBy(x => x.name).Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key);
                        job.report += $"   🧾 찍는 순간 내 유닛: {string.Join(", ", mine)}\n";
                        SelectionManager selectionNow = UnityEngine.Object.FindFirstObjectByType<SelectionManager>();
                        if (selectionNow != null)
                            foreach (Selectable chosen in selectionNow.Selected.Where(x => x != null))
                            {
                                Vector3 at = chosen.transform.position;
                                string toTarget = lastPointerTarget != null
                                    ? $" · 마지막 대상 {lastPointerTarget.name}까지 수평 {Vector2.Distance(new Vector2(at.x, at.z), new Vector2(lastPointerTarget.transform.position.x, lastPointerTarget.transform.position.z)):F1}"
                                    : "";
                                string moving = chosen.TryGetComponent(out NavMeshAgent a) && a.isOnNavMesh ? $" · 남은 길 {a.remainingDistance:F1} · 속도 {a.velocity.magnitude:F1}" : "";
                                job.report += $"   📍 선택 유닛 {chosen.name} 위치 {at.ToString("F0")}{toTarget}{moving}\n";
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
        }
        if (Mouse.current != shotMouse) { previousMouse = Mouse.current; shotMouse.MakeCurrent(); }
        if (previousBehavior == null)
        {
            previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
    }

    // 끝날 때 반드시 부른다 — 가상 마우스가 남으면 사람의 실제 마우스 대신 그게 Mouse.current로 남는다.
    static void ReleaseShotMouse()
    {
        foreach (Mouse m in InputSystem.devices.OfType<Mouse>().Where(m => m.name == ShotMouseName).ToList())
            InputSystem.RemoveDevice(m);
        shotMouse = null;
        if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        previousMouse = null;
        if (previousBehavior != null) InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior.Value;
        previousBehavior = null;
    }

    static void QueueMouse(Vector2 position, MouseButton button, bool down)
    {
        MouseState state = new MouseState { position = position };
        if (down) state = state.WithButton(button, true);
        InputSystem.QueueStateEvent(shotMouse, state);
    }

    static GameObject FindPointerTarget(string spec, out string candidates)
    {
        bool left = spec.StartsWith("@sel:");
        string name = spec.Substring(left ? 5 : 4).Normalize(NormalizationForm.FormC);
        candidates = "";
        if (left)
        {
            List<Selectable> mine = Selectable.All.Where(x => x != null &&
                (!x.TryGetComponent(out OwnedByPlayer o) || o.OwnerId == LocalPlayer.LocalPlayerId)).ToList();
            Selectable hit = mine.FirstOrDefault(x => x.name.Normalize(NormalizationForm.FormC).Contains(name));
            if (hit == null) candidates = string.Join(", ", mine.Select(x => x.name).Distinct().Take(20));
            return hit != null ? hit.gameObject : null;
        }
        GameObject found = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && c.name.Normalize(NormalizationForm.FormC).Contains(name))
            .Select(c => c.gameObject).FirstOrDefault();
        return found;
    }

    // 한 틱에 한 단계씩. 끝나면 true.
    static bool StepPointer(GameShotJob job, string spec, double inStage)
    {
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
                    FailGameShot(job, $"{label}: 「{wanted}」 대상을 못 찾음" + (candidates.Length > 0 ? $" — 내 유닛: {candidates}" : "") +
                                      (hidden.Count > 0 ? $" — 씬에는 있음: {string.Join(", ", hidden)}" : " — 씬 어디에도 그 이름이 없다"));
                    return false;
                }
                lastPointerTarget = target;
                Vector3 aim = target.TryGetComponent(out Collider col) ? col.bounds.center : target.transform.position;
                Vector3 sp = cam.WorldToScreenPoint(aim);
                (float bandBottom, float bandTop) = PointerBand();
                bool visible = sp.z > 0f && sp.x > 20f && sp.x < cam.pixelWidth - 20f && sp.y > bandBottom * cam.pixelHeight + 10f && sp.y < bandTop * cam.pixelHeight - 10f;
                if (!visible)
                {
                    RtsCameraController rts = cam.GetComponent<RtsCameraController>();
                    if (rts == null) { FailGameShot(job, $"{label}: {target.name}이 화면 밖인데 카메라를 옮길 RtsCameraController가 없음"); return false; }
                    if (job.pointerX < 0f) { FailGameShot(job, $"{label}: 카메라를 옮겨도 {target.name}이 화면 안(HUD 사이)에 안 들어옴 — 화면 좌표 {sp}"); return false; }
                    rts.MoveTo(new Vector3(aim.x, 0f, aim.z));   // 미니맵 클릭과 같은 경로
                    job.report += $"   🎥 {target.name}이 화면 밖이라 카메라를 옮김(MoveTo {aim.ToString("F0")})\n";
                    job.pointerX = -1f;   // 한 번만 옮긴다 — 다음 틱에도 안 보이면 실패
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
                job.report += $"   🖱 {label} 「{spec.Substring(left ? 5 : 4)}」 @ 화면 ({job.pointerX:F0}, {job.pointerY:F0}) → 지금 선택: {(selected.Length > 0 ? selected : "없음")}\n";
                job.pointerX = 0f;
                return true;
            }
        }
    }

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
        if (type == LogType.Log && !(message.StartsWith("[이동]") || message.StartsWith("[선택]") || message.StartsWith("[명령]"))) return;
        GameShotJob job = LoadGameShot();
        if (job == null) return;

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
