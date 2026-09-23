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
///   gameshot &lt;파일&gt; [초] [가로x세로] [super:N] [click:&lt;버튼&gt;]  플레이 모드로 들어가 버튼을 눌러 가며 **UI까지** 게임 화면을 찍고 나온다(아래 GameShot 절).
///                                             글씨·UI 판정엔 해상도를 꼭 박는다: gameshot hud.png 3 1920x1080 click:쉬움
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
    const double GameShotSettle = 1.0, GameShotClickGap = 1.0, GameShotClickSearch = 5.0;
    const int GameShotMaxLogKinds = 40;

    [Serializable]
    class GameShotJob
    {
        public string id;           // outbox 번호
        public string file;         // ScreenCapture 결과(절대 경로)
        public float seconds;
        public int superSize = 1;
        public List<string> clicks = new List<string>();
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
            if (token.StartsWith("click:"))
            {
                if (token.Length == 6) return "❌ click: 뒤에 버튼 이름이나 글자를 주세요";
                job.clicks.Add(token.Substring(6));
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
                if (inStage >= GameShotSettle) Advance(job, job.clicks.Count > 0 ? "clicking" : "waiting");
                break;

            case "clicking":
            {
                if (job.clickIndex > 0 && inStage < GameShotClickGap) break;   // 앞 클릭의 결과가 화면에 반영될 틈
                string target = job.clicks[job.clickIndex];
                string clicked = ClickButton(target);
                if (clicked != null)
                {
                    job.report += $"   🖱 {job.clickIndex + 1}번째 클릭: {clicked}\n";
                    job.clickIndex++;
                    Advance(job, job.clickIndex < job.clicks.Count ? "clicking" : "waiting");
                }
                else if (inStage > GameShotClickSearch)
                {
                    FailGameShot(job, $"{GameShotClickSearch}초 동안 「{target}」 버튼을 못 찾았다. 그때 보이던 누를 수 있는 버튼:\n" + ListButtons());
                }
                break;
            }

            case "waiting":
                if (inStage >= job.seconds)
                {
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
        if (type == LogType.Log) return;
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
