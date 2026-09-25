using UnityEditor;
using UnityEngine;

/// <summary>
/// 공용 Mixamo 클립(Assets/Art/Characters/idle·walk.fbx)의 반복을 켠다.
///
/// 왜 — FBX 기본 임포트는 Loop Time을 끈다. 이 둘은 clipAnimations가 비어 기본값 그대로라
/// **걷기가 한 바퀴(약 1초) 돌고 마지막 자세로 굳었다.** 아군은 몇 초만 걸어 티가 덜 났고,
/// 레인을 쉬지 않고 도는 적은 「서 있는 채로 미끄러진다」로 보였다(사장님 09-25).
/// Character.controller의 Move·Idle 상태가 이 두 클립을 쓴다(적 전부 + Generic 아닌 아군).
///
/// 후처리기로 두지 않은 이유: 새 AssetPostprocessor를 넣으면 모델 250여 개가 다시 임포트된다.
/// 두 파일의 .meta에 반복을 한 번 적어 두면 끝이라 메뉴 한 번으로 한다(브리지 `call SharedClipLoops.Apply`).
/// </summary>
public static class SharedClipLoops
{
    static readonly string[] Paths = { "Assets/Art/Characters/walk.fbx", "Assets/Art/Characters/idle.fbx" };

    [MenuItem("Tools/아트/공용 걷기·대기 클립 반복 켜기")]
    public static string Apply()
    {
        string report = "";
        foreach (string path in Paths)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) { report += $"❌ {path} 없음\n"; continue; }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            int changed = 0;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (clip.loopTime) continue;
                clip.loopTime = true;
                changed++;
            }
            if (changed == 0) { report += $"= {path} 이미 반복\n"; continue; }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            report += $"✅ {path} 클립 {changed}개 반복 켬\n";
        }
        Debug.Log(report);
        return report;
    }
}
