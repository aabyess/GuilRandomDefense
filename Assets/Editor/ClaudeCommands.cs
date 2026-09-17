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
/// 사진은 ClaudeBridge/shots/에 PNG로 남는다. 명령마다 그동안의 Debug 로그(경고·오류 포함)가 결과에 실린다.
/// </summary>
[InitializeOnLoad]
public static class ClaudeCommands
{
    const string Folder = "ClaudeBridge";
    static double nextPoll;

    static ClaudeCommands()
    {
        if (!Application.isBatchMode) EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;

        string inbox = Path.Combine(Folder, "inbox");
        if (!Directory.Exists(inbox)) return;
        string file = Directory.GetFiles(inbox, "*.txt").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
        if (file == null) return;

        string id = Path.GetFileNameWithoutExtension(file);
        string[] commands = File.ReadAllLines(file);
        File.Delete(file);   // 명령이 컴파일·도메인 리로드를 부르면 이 함수가 다시 불린다 — 두 번 돌지 않게 먼저 지운다

        WriteResult(id, RunAll(commands));
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
    static string UnitLineup(string file, int start, int count)
    {
        List<GameObject> prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Generated" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith("Unit_"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(p => p != null)
            .Skip(start).Take(count).ToList();
        if (prefabs.Count == 0) return $"❌ Unit_ 프리팹 없음({start}번부터)";

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
                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (hips != null && head != null)
                    {
                        Vector3 v = (head.position - hips.position).normalized;
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
}
