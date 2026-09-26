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
///
/// 버전(2026-09-26 사장님 「배포할 때마다 1.1.0v 이런 식으로」): 배포 전에 <see cref="GameVersion.Number"/>를 올린다 —
/// 기능이 늘면 가운데 자리, 고치기만 했으면 끝자리. 빌드가 그 값을 PlayerSettings.bundleVersion(= Application.version)에 넣고,
/// 친구에게 줄 압축 파일 이름을 로그에 찍는다(맥 .app은 실행 권한이 깨지지 않게 ditto로 손으로 압축한다).
/// </summary>
public static class BuildBeta
{
    // 맥용(2026-09-25 사장님 「맥용으로도」) — 인텔·애플실리콘 둘 다 도는 유니버설 앱. 서명·공증을 안 하므로
    // 받은 친구는 처음 한 번 「우클릭 → 열기」나 `xattr -dr com.apple.quarantine GuilRandomDefense.app`가 필요하다.
    // 쓰는 법: -buildTarget OSXUniversal -executeMethod BuildBeta.Mac
    [MenuItem("Tools/빌드/맥 베타 빌드")]
    public static void Mac()
    {
        // 유니버설(인텔+애플실리콘). OSXStandalone 네임스페이스는 맥 빌드 모듈이 있을 때만 생겨서 직접 쓰면
        // 모듈 없는 환경(compile_check 포함)에서 컴파일이 깨진다 — 문자열 설정으로 넣는다.
        EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX), "Architecture", "x64ARM64");
        Build(BuildTarget.StandaloneOSX, Path.Combine("Builds", "Mac"), $"구랜디 {GameVersion.Label}.app");
    }

    [MenuItem("Tools/빌드/윈도우 베타 빌드")]
    // 파일 이름에 버전을 넣는다(09-26 사장님 「구랜디2 이런 식 말고 1.1.0v 이런 식으로」 — 같은 zip을 두 번 풀면 맥이 「GuilRandomDefense 2.app」을 만들어
    //    어느 게 새 판인지 몰랐다). ⚠️ productName(GuilRandomDefense)은 그대로 둔다 — 세이브 폴더(persistentDataPath)가 그 이름이라 바꾸면 기록이 사라진다.
    public static void Windows() => Build(BuildTarget.StandaloneWindows64, Path.Combine("Builds", "Windows", $"구랜디 {GameVersion.Label}"), "구랜디.exe");

    static void Build(BuildTarget target, string relDir, string fileName)
    {
        PlayerSettings.bundleVersion = GameVersion.Number;
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
        string platform = target == BuildTarget.StandaloneOSX ? "맥" : "윈도우";
        Debug.Log($"[베타 빌드] {GameVersion.Label} · 압축 이름 구랜디_베타_{platform}_{GameVersion.Label}.zip");
        Debug.Log($"[베타 빌드] {target} {summary.result} · {summary.totalSize / (1024 * 1024)}MB · 오류 {summary.totalErrors} · 경고 {summary.totalWarnings} · {summary.totalTime}");
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
