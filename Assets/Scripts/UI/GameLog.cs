using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 테스터 로그 파일(사장님 10-06 「테스트하는 사람들한테 보낼 건데 게임하면 로그가 적혀서 분석할 수 있게」).
/// 실행할 때마다 새 파일 하나: {persistentDataPath}/Logs/GRD_{날짜_시각}_{버전}.log
///   · 머리: 버전·OS·기기·화면·언어
///   · 게임의 모든 Debug 로그(경고·오류·예외는 스택까지)와 씬 전환
///   · 30초마다 상태 한 줄(씬·라운드·난이도·엔·목재·마나·레인 적 수·FPS)
/// 파일은 최근 20개만 남기고, 한 파일은 20MB에서 멈춘다. 설정 창 [로그 폴더 열기]로 테스터가 바로 찾는다.
/// 에디터에서는 만들지 않는다(에디터 로그가 따로 있다).
/// </summary>
public class GameLog : MonoBehaviour
{
    const int KeepFiles = 20;
    const long MaxBytes = 20L * 1024 * 1024;
    const float SnapshotSeconds = 30f;

    public static string Folder => Path.Combine(Application.persistentDataPath, "Logs");
    public static string CurrentFile { get; private set; }

    static StreamWriter writer;
    static readonly object gate = new object();
    static long written;
    float nextSnapshot;
    float fpsAccum; int fpsFrames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (Application.isEditor || writer != null) return;
        try
        {
            Directory.CreateDirectory(Folder);
            CleanupOld();
            CurrentFile = Path.Combine(Folder, $"GRD_{DateTime.Now:yyyyMMdd_HHmmss}_{GameVersion.Label}.log");
            writer = new StreamWriter(CurrentFile, false, new UTF8Encoding(false)) { AutoFlush = true };
            WriteRaw($"# 구랜디(G.R.D) 로그 {GameVersion.Label} · 시작 {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                     $"# OS {SystemInfo.operatingSystem} · 기기 {SystemInfo.deviceModel} · CPU {SystemInfo.processorType} · RAM {SystemInfo.systemMemorySize}MB · GPU {SystemInfo.graphicsDeviceName}\n" +
                     $"# 화면 {Screen.width}×{Screen.height} {(Screen.fullScreen ? "전체" : "창")} · 언어 {Application.systemLanguage}\n");
            Application.logMessageReceivedThreaded += OnLog;
            SceneManager.sceneLoaded += (s, m) => Line("씬", $"{s.name}({s.buildIndex}) 로드");
            Application.quitting += () => { Line("종료", "게임 종료"); lock (gate) { writer?.Dispose(); writer = null; } };
            var host = new GameObject("[GameLog]");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideInHierarchy;
            host.AddComponent<GameLog>();
        }
        catch (Exception e) { Debug.LogWarning("[로그] 로그 파일을 만들지 못했습니다: " + e.Message); writer = null; }
    }

    static void CleanupOld()
    {
        try
        {
            FileInfo[] files = new DirectoryInfo(Folder).GetFiles("GRD_*.log");
            Array.Sort(files, (a, b) => b.CreationTimeUtc.CompareTo(a.CreationTimeUtc));
            for (int i = KeepFiles - 1; i < files.Length; i++) files[i].Delete();
        }
        catch { }
    }

    static void OnLog(string message, string stack, LogType type)
    {
        string tag = type == LogType.Log ? "로그" : type == LogType.Warning ? "경고" : type == LogType.Assert ? "단언" : type == LogType.Exception ? "예외" : "오류";
        bool withStack = type == LogType.Exception || type == LogType.Error || type == LogType.Assert;
        Line(tag, withStack && !string.IsNullOrEmpty(stack) ? message + "\n    " + stack.Replace("\n", "\n    ").TrimEnd() : message);
    }

    /// <summary>로그 파일에 한 줄(어느 스레드에서나).</summary>
    public static void Line(string tag, string text)
    {
        WriteRaw($"[{DateTime.Now:HH:mm:ss.fff}] [{tag}] {text}\n");
    }

    static void WriteRaw(string s)
    {
        lock (gate)
        {
            if (writer == null || written > MaxBytes) return;
            try
            {
                writer.Write(s);
                written += s.Length * 2;
                if (written > MaxBytes) writer.Write("# 파일이 20MB를 넘어 여기서 기록을 멈춥니다.\n");
            }
            catch { }
        }
    }

    void Update()
    {
        fpsAccum += Time.unscaledDeltaTime; fpsFrames++;
        if (Time.unscaledTime < nextSnapshot) return;
        nextSnapshot = Time.unscaledTime + SnapshotSeconds;
        try
        {
            var sb = new StringBuilder();
            sb.Append($"씬 {SceneManager.GetActiveScene().name}");
            RoundManager rm = FindFirstObjectByType<RoundManager>();
            if (rm != null) sb.Append($" · 라운드 {rm.CurrentRound}");
            if (DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected) sb.Append($" · 난이도 {DifficultyManager.Instance.Current}");
            PlayerContext me = PlayerContext.Get(LocalPlayer.LocalPlayerId);
            if (me != null)
            {
                if (me.GoldWallet != null) sb.Append($" · 엔 {me.GoldWallet.Gold}");
                if (me.ResourceWallet != null) sb.Append($" · 목재 {me.ResourceWallet.Get(ResourceType.Wood)} · 마나 {me.ResourceWallet.Get(ResourceType.Mana)}");
            }
            sb.Append($" · 적 {EnemyDummy.Active.Count}");
            if (fpsFrames > 0) sb.Append($" · FPS {fpsFrames / Mathf.Max(0.001f, fpsAccum):0}");
            fpsAccum = 0f; fpsFrames = 0;
            Line("상태", sb.ToString());
        }
        catch (Exception e) { Line("상태", "기록 실패: " + e.Message); }
    }

    /// <summary>설정 창 [로그 폴더 열기].</summary>
    public static void OpenFolder()
    {
        try { Directory.CreateDirectory(Folder); Application.OpenURL("file://" + Folder.Replace("\\", "/")); } catch { }
    }
}
