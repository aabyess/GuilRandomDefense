using System;
using System.Linq;
using UnityEditor;

/// <summary>
/// Blender로 지은 괴물·짐승·소품 FBX(Assets/Art/Monsters·Creatures·Props)의 클립 반복을 맞춘다.
///
/// 왜 필요한가 — FBX 기본 임포트는 클립의 Loop Time을 끈다. 숨쉬기(Idle_Breath, 8초)가 한 번 돌고 멈춰
/// 짐승이 굳는다. Blender 쪽이 첫 프레임 = 끝 프레임으로 지어 두었으니 반복만 켜면 이음매 없이 돈다.
///
/// Blender 쪽(Tools/blender/gen_*.py)과의 약속: 반복할 클립은 이름에 `Idle`이 들어간다.
/// 보물상자 `Open`처럼 한 번 열리고 끝 자세를 지키는 클립은 반복하지 않는다.
/// </summary>
public class BlenderClipPostprocessor : AssetPostprocessor
{
    // 배 유닛(Units/고대의배·해적선, 2026-09-13)도 Blender 클립 「…|Idle_Bob」이라 같은 규칙을 탄다.
    static readonly string[] Roots = { "Assets/Art/Monsters/", "Assets/Art/Creatures/", "Assets/Art/Props/",
                                       "Assets/Art/Units/고대의배/", "Assets/Art/Units/해적선/",
                                       // 히든 배 둘(2026-09-30) — Idle·Move 반복(MoveLoopRoots). 버전은 안 올림: 새 파일 둘뿐.
                                       "Assets/Art/Units/히든_맥주만땅/", "Assets/Art/Units/히든_미소야/", "Assets/Art/Units/영원_서민성/",
                                       // 영원_최상호(2026-09-30 고유 동작 시범) — 원본 Idle·Move 반복.
                                       "Assets/Art/Units/영원_최상호/",
                                       "Assets/Art/Units/영원_문필환/",
                                       "Assets/Art/Units/초월_김건_AP/",
                                       "Assets/Art/Units/초월_강주혁_AP/",
                                       "Assets/Art/Units/초월_임장혁_AD/",
                                       "Assets/Art/Units/전설적인_이승우/",
                                       "Assets/Art/Units/희귀함_구주호/",
                                       "Assets/Art/Units/특별함_이정범/",
                                       "Assets/Art/Units/특별함_박민수/",
                                       "Assets/Art/Units/특별함_박예원/",
                                       "Assets/Art/Units/희귀함_조현규/",
                                       // 적 셋(2026-10-01 고유 동작) — Idle·Move 반복.
                                       "Assets/Art/Enemies/주영호/", "Assets/Art/Enemies/왕승환/", "Assets/Art/Enemies/김민준안경/",
                                       // 고유 동작 확대(2026-10-01) — 황길라·고도현은 Move 클립 없음(Idle만 반복).
                                       "Assets/Art/Units/특수함_황길라/", "Assets/Art/Units/영원_윤현모/", "Assets/Art/Units/불멸_고도현/", "Assets/Art/Units/초월_배성령_AD/", "Assets/Art/Units/초월_양재모_AD/", "Assets/Art/Units/제한_이유범/", "Assets/Art/Units/제한_최영민/", "Assets/Art/Units/전설적인_김건/", "Assets/Art/Units/전설적인_이시원/", "Assets/Art/Units/전설적인_김민준/", "Assets/Art/Units/전설적인_김용태/", "Assets/Art/Units/전설적인_박은석/", "Assets/Art/Units/전설적인_백기현/", "Assets/Art/Units/전설적인_신지우/", "Assets/Art/Units/전설적인_이일중/", "Assets/Art/Units/전설적인_임건웅/",
                                       // 고유 동작 3묶음 + 적 13(10-01)
                                       "Assets/Art/Units/히든_석성례/", "Assets/Art/Units/히든_여은서/", "Assets/Art/Units/히든_전주연/", "Assets/Art/Units/히든_한나웅/", "Assets/Art/Units/희귀함_노수신/", "Assets/Art/Units/희귀함_노태현/", "Assets/Art/Units/희귀함_양재모/", "Assets/Art/Units/희귀함_이승우/", "Assets/Art/Units/특별함_김태영/", "Assets/Art/Units/특별함_송형성/", "Assets/Art/Units/특별함_이지원/", "Assets/Art/Units/특별함_이현빈/", "Assets/Art/Units/특별함_임채준/", "Assets/Art/Units/특별함_정승준/", "Assets/Art/Enemies/반항아_이승우/", "Assets/Art/Enemies/배병욱/", "Assets/Art/Enemies/인홍진/", "Assets/Art/Enemies/조도연/", "Assets/Art/Enemies/문채홍/", "Assets/Art/Enemies/유시은/", "Assets/Art/Enemies/정다희/", "Assets/Art/Enemies/이태훈/", "Assets/Art/Enemies/간보는_김용태/", "Assets/Art/Enemies/울부짖는_노태현/", "Assets/Art/Enemies/윤현모/", "Assets/Art/Enemies/임준성/", "Assets/Art/Enemies/박민수/",
                                       // 스토리8 다크 영(10-03 사장님 「둘 다 합쳐서」) — 원본 대기 4.12초 반복. 건물 폴더는 납작해 파일 이름으로.
                                       "Assets/Art/Buildings/Story08_사이버넷.fbx",
                                       // 고유 동작 5묶음(10-03) — 조세민·손오공·박도진은 Move 없음(Idle만 반복).
                                       "Assets/Art/Units/특별함_조세민/", "Assets/Art/Units/랜덤_손오공/", "Assets/Art/Units/히든_전유라/",
                                       "Assets/Art/Enemies/박도진/", "Assets/Art/Enemies/서희원/", "Assets/Art/Enemies/강민호/" };

    static readonly string[] MoveLoopRoots = { "Assets/Art/Units/히든_맥주만땅/", "Assets/Art/Units/히든_미소야/", "Assets/Art/Units/영원_서민성/",
                                               "Assets/Art/Units/영원_최상호/",
                                               "Assets/Art/Units/영원_문필환/",
                                               "Assets/Art/Units/초월_김건_AP/",
                                               "Assets/Art/Units/초월_강주혁_AP/",
                                               "Assets/Art/Units/초월_임장혁_AD/",
                                               "Assets/Art/Units/전설적인_이승우/",
                                               "Assets/Art/Units/희귀함_구주호/",
                                               "Assets/Art/Units/특별함_이정범/",
                                               "Assets/Art/Units/특별함_박민수/",
                                               "Assets/Art/Units/특별함_박예원/",
                                               "Assets/Art/Units/희귀함_조현규/",
                                               "Assets/Art/Enemies/주영호/", "Assets/Art/Enemies/왕승환/", "Assets/Art/Enemies/김민준안경/",
                                               "Assets/Art/Units/영원_윤현모/", "Assets/Art/Units/초월_배성령_AD/", "Assets/Art/Units/초월_양재모_AD/", "Assets/Art/Units/제한_이유범/", "Assets/Art/Units/제한_최영민/", "Assets/Art/Units/전설적인_김건/", "Assets/Art/Units/전설적인_이시원/", "Assets/Art/Units/전설적인_김민준/", "Assets/Art/Units/전설적인_김용태/", "Assets/Art/Units/전설적인_박은석/", "Assets/Art/Units/전설적인_백기현/", "Assets/Art/Units/전설적인_신지우/", "Assets/Art/Units/전설적인_이일중/", "Assets/Art/Units/전설적인_임건웅/",
                                       // 고유 동작 3묶음 + 적 13(10-01)
                                       "Assets/Art/Units/히든_석성례/", "Assets/Art/Units/히든_여은서/", "Assets/Art/Units/히든_전주연/", "Assets/Art/Units/히든_한나웅/", "Assets/Art/Units/희귀함_노수신/", "Assets/Art/Units/희귀함_노태현/", "Assets/Art/Units/희귀함_양재모/", "Assets/Art/Units/희귀함_이승우/", "Assets/Art/Units/특별함_김태영/", "Assets/Art/Units/특별함_송형성/", "Assets/Art/Units/특별함_이지원/", "Assets/Art/Units/특별함_이현빈/", "Assets/Art/Units/특별함_임채준/", "Assets/Art/Units/특별함_정승준/", "Assets/Art/Enemies/반항아_이승우/", "Assets/Art/Enemies/배병욱/", "Assets/Art/Enemies/인홍진/", "Assets/Art/Enemies/조도연/", "Assets/Art/Enemies/문채홍/", "Assets/Art/Enemies/유시은/", "Assets/Art/Enemies/정다희/", "Assets/Art/Enemies/이태훈/", "Assets/Art/Enemies/간보는_김용태/", "Assets/Art/Enemies/울부짖는_노태현/", "Assets/Art/Enemies/윤현모/", "Assets/Art/Enemies/임준성/", "Assets/Art/Enemies/박민수/",
                                               // 고유 동작 5묶음(10-03) — Move 있는 셋.
                                               "Assets/Art/Units/히든_전유라/", "Assets/Art/Enemies/서희원/", "Assets/Art/Enemies/강민호/" };

    // 규칙을 바꾸면 올린다 — 올려야 이미 임포트된 FBX도 다시 돈다.
    // 1 → 2 (2026-09-13): 배 유닛 폴더 추가.
    public override uint GetVersion() => 2;

    void OnPreprocessAnimation()
    {
        // 🔴 macOS에서 유니티가 넘기는 한글 경로는 NFC가 아닐 수 있다(UnitModelPostprocessor 참고).
        string path = assetPath.Normalize(System.Text.NormalizationForm.FormC);
        if (!Roots.Any(root => path.StartsWith(root, StringComparison.Ordinal))) return;

        if (!(assetImporter is ModelImporter importer)) return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        // 히든 배 둘은 Move 클립(끄덕임 60프레임)도 가졌다 — 이동하는 내내 돌아야 하니 같이 반복한다.
        bool loopMove = MoveLoopRoots.Any(root => path.StartsWith(root, StringComparison.Ordinal));
        foreach (ModelImporterClipAnimation clip in clips)
            clip.loopTime = clip.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (loopMove && clip.name.IndexOf("Move", StringComparison.OrdinalIgnoreCase) >= 0);

        importer.clipAnimations = clips;
    }
}
