using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 친구 베타 테스트용 빌드(2026-09-25, 사장님 「윈도우용」). 싱글 플레이 판이다 — 멀티는 구현담당2의 mp 브랜치.
/// 쓰는 법(유니티를 열지 않고, 이 프로젝트의 별도 사본에서):
///   Unity -batchmode -quit -projectPath <사본> -buildTarget Win64 -executeMethod BuildBeta.Windows -logFile <로그>
/// 결과: <프로젝트>/Builds/Windows/GuilRandomDefense.exe (+ _Data 폴더). 친구에게는 Builds/Windows 폴더를 통째로 압축해 준다.
/// 개발 빌드가 아니므로 G(위습)·F1/F2(디버그 창·조합 치트)가 꺼진다.
/// </summary>
public static class BuildBeta
{
    // 맥용(2026-09-25 사장님 「맥용으로도」) — 인텔·애플실리콘 둘 다 도는 유니버설 앱. 서명·공증을 안 하므로
    // 받은 친구는 처음 한 번 「우클릭 → 열기」나 `xattr -dr com.apple.quarantine GuilRandomDefense.app`가 필요하다.
    // 쓰는 법: -buildTarget OSXUniversal -executeMethod BuildBeta.Mac
    [MenuItem("Tools/빌드/맥 베타 빌드")]
    public static void Mac()
    {
        UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.x64ARM64;
        Build(BuildTarget.StandaloneOSX, Path.Combine("Builds", "Mac"), "GuilRandomDefense.app");
    }

    [MenuItem("Tools/빌드/윈도우 베타 빌드")]
    public static void Windows() => Build(BuildTarget.StandaloneWindows64, Path.Combine("Builds", "Windows"), "GuilRandomDefense.exe");

    static void Build(BuildTarget target, string relDir, string fileName)
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string outDir = Path.Combine(Directory.GetCurrentDirectory(), relDir);
        Directory.CreateDirectory(outDir);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outDir, fileName),
            target = target,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"[베타 빌드] {target} {summary.result} · {summary.totalSize / (1024 * 1024)}MB · 오류 {summary.totalErrors} · 경고 {summary.totalWarnings} · {summary.totalTime}");
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
