using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프리텐다드(Assets/Fonts)로 TMP 폰트 에셋을 굽는다(PM 지시 2026-09-23, 사장님 "하단 글씨 화질").
///
/// 왜 TMP인가: 레거시 <c>UnityEngine.UI.Text</c>는 fontSize마다 비트맵 글리프를 아틀라스에 구워
/// 쓴다. 캔버스가 확대되면(사장님 화면은 기준 1920×1080보다 큼) 그 비트맵이 그대로 늘어나
/// 뿌옇게 번진다. TMP는 SDF(부호거리장)라 한 번 구운 글리프를 **어떤 배율로도 또렷하게** 그린다.
///
/// ⚠️ 이 메뉴는 유니티 에디터가 필요하다 — PM이 브리지로 실행한다(구현담당은 직접 안 돌린다).
/// </summary>
public static class KoreanFontAssetBuilder
{
    const string Title = "한글 TMP 폰트";
    const string FontFolder = "Assets/Fonts/";
    // ⚠️ Resources 아래에 둔다 — GameHud는 런타임 코드라 AssetDatabase를 못 쓰고,
    // 씬이 맵 생성기로 다시 만들어지므로 인스펙터 참조도 못 건다. Resources.Load가 유일한 길이다.
    const string OutputFolder = "Assets/Resources/Fonts";

    // ── 아틀라스 설정 근거 ────────────────────────────────────────────────
    //
    // ① 샘플링 크기 90
    //    SDF 품질은 "구울 때의 글자 크기"가 정한다. 화면에 실제로 그려지는 픽셀보다 넉넉해야
    //    가장자리가 뭉개지지 않는다. 하단 바 글자는 캔버스 기준 16~26pt인데, 사장님 화면처럼
    //    기준 해상도(1920×1080)보다 큰 화면에서는 캔버스 배율이 1.5~2배까지 올라가 실제
    //    렌더 픽셀이 **최대 50px쯤** 된다. 90은 그 1.8배라 확대에도 여유가 있다.
    //    (더 키우면 아틀라스에 들어가는 글자 수가 줄어 동적 확장이 자주 일어난다.)
    //
    // ② 패딩 9 = 샘플링 크기의 1/10
    //    SDF는 글자 바깥으로 거리값을 적어 두는 공간이 필요하다. 이 여백이 좁으면 외곽선·
    //    그림자를 줄 때 잘린다. TMP 권장 비율이 1/10이다.
    //
    // ③ 아틀라스 1024×1024 · Dynamic
    //    한글은 완성형만 11,172자다. 전부 Static으로 구우면 아틀라스가 수십 MB가 된다.
    //    Dynamic은 **실제로 쓰인 글자만** 그때그때 아틀라스에 추가한다. 다만 런타임 첫 등장에
    //    한 프레임 끊길 수 있어, 아래 PrewarmCharacters로 **게임에 확실히 나오는 글자**
    //    (UI 문구·등급명·숫자·영문)를 미리 구워 둔다. 유닛 이름 240종은 데이터에서 직접 읽어
    //    같이 굽는다 — 이러면 실사용 중 동적 확장이 사실상 안 일어난다.
    //    1024×1024에 90pt 글리프는 대략 100자 남짓 들어가고, 부족하면 TMP가 아틀라스를
    //    한 장 더 붙인다(Multi Atlas Texture).
    const int SamplingPointSize = 90;
    const int AtlasPadding = 9;
    const int AtlasSize = 1024;

    /// <summary>
    /// TMP는 <c>TMP_Settings</c> 에셋이 프로젝트에 있어야 폰트 에셋을 만들 수 있다.
    /// 그 에셋은 "TMP Essential Resources"를 임포트해야 생기는데, 우리 프로젝트는 여태
    /// 레거시 <c>Text</c>만 써서 그 단계를 한 번도 거치지 않았다(2026-09-23 확인:
    /// Assets 아래 TMP·TextMesh Pro 관련 에셋 0건, ProjectSettings에도 참조 없음).
    /// 그래서 <c>TMP_FontAsset.CreateFontAsset</c>이 내부에서 TMP_Settings를 읽다가
    /// NullReferenceException으로 터졌다(TMP_Settings.get_clearDynamicDataOnBuild).
    ///
    /// 사람이 유니티 메뉴를 따로 누르지 않게 여기서 임포트까지 끝낸다.
    /// 경로는 하드코딩하지 않는다 — 패키지 폴더 이름에 해시가 붙어 버전마다 달라진다.
    /// </summary>
    static bool EnsureTmpEssentials(out string note)
    {
        if (TMP_Settings.instance != null) { note = "TMP 설정 이미 있음"; return true; }

        string package = FindEssentialResourcesPackage();
        if (package == null)
        {
            note = "⚠️ 'TMP Essential Resources.unitypackage'를 못 찾았습니다 — " +
                   "com.unity.ugui 패키지 경로를 확인하세요.";
            return false;
        }

        // ⚠️ ImportPackage는 **비동기**다 — 바로 뒤에서 TMP_Settings를 봐도 아직 null이다.
        // 그래서 완료 이벤트에 "이어서 폰트를 굽는다"를 걸어 두고 이번 호출은 여기서 끝낸다.
        // (이 패키지는 스크립트가 없어 도메인 리로드가 안 일어나므로 구독이 살아남는다.
        //  혹시 못 살아남아도 메뉴를 한 번 더 돌리면 되게, 아래 안내 문구를 남긴다.)
        AssetDatabase.importPackageCompleted -= ContinueAfterImport;
        AssetDatabase.importPackageCompleted += ContinueAfterImport;
        AssetDatabase.ImportPackage(package, false);   // false = 대화상자 없이 조용히

        if (TMP_Settings.instance == null)
        {
            note = $"TMP 기본 리소스를 임포트하는 중입니다({System.IO.Path.GetFileName(package)}).\n" +
                   "임포트가 끝나면 폰트 굽기가 이어서 자동 실행됩니다. " +
                   "혹시 아무 일도 안 일어나면 이 메뉴를 한 번만 더 실행하세요.";
            return false;
        }

        note = $"TMP 기본 리소스를 임포트했습니다 ({package})";
        return true;
    }

    /// <summary>
    /// TMP 기본 리소스 임포트가 끝나면 폰트 굽기를 이어서 실행한다.
    /// 한 번만 동작하고 스스로 구독을 푼다(다른 패키지 임포트에 끌려가지 않게 이름도 확인).
    /// </summary>
    static void ContinueAfterImport(string packageName)
    {
        if (!packageName.Contains("TMP Essential Resources")) return;

        AssetDatabase.importPackageCompleted -= ContinueAfterImport;
        if (TMP_Settings.instance == null)
        {
            Debug.LogWarning($"[{Title}] 임포트가 끝났는데도 TMP 설정이 없습니다 — 메뉴를 한 번 더 실행하세요.");
            return;
        }

        Debug.Log($"[{Title}] TMP 기본 리소스 임포트 완료 — 폰트 굽기를 이어서 실행합니다.");
        Build();
    }

    /// <summary>
    /// com.unity.ugui 패키지 안의 "TMP Essential Resources.unitypackage" 경로.
    /// 패키지 폴더는 `com.unity.ugui@<해시>`라 이름이 고정이 아니므로 PackageManager에
    /// 등록된 실제 경로를 물어본다. 못 읽으면 PackageCache를 훑는 것으로 한 번 더 시도한다.
    /// </summary>
    static string FindEssentialResourcesPackage()
    {
        const string leaf = "Package Resources/TMP Essential Resources.unitypackage";

        UnityEditor.PackageManager.PackageInfo info =
            UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
        if (info != null)
        {
            string direct = System.IO.Path.Combine(info.resolvedPath, leaf);
            if (System.IO.File.Exists(direct)) return direct;
        }

        string cache = System.IO.Path.Combine(
            System.IO.Directory.GetCurrentDirectory(), "Library", "PackageCache");
        if (!System.IO.Directory.Exists(cache)) return null;

        foreach (string folder in System.IO.Directory.GetDirectories(cache, "com.unity.ugui*"))
        {
            string candidate = System.IO.Path.Combine(folder, leaf);
            if (System.IO.File.Exists(candidate)) return candidate;
        }
        return null;
    }

    [MenuItem("Tools/HUD/한글 TMP 폰트 에셋 생성")]
    static void Build()
    {
        if (!EditorGuards.RequireEditMode(Title)) return;

        if (!EnsureTmpEssentials(out string tmpNote))
        {
            EditorGuards.Dialog(Title, tmpNote, "확인");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Fonts");

        int made = 0;
        StringBuilder report = new StringBuilder();
        foreach (string face in new[] { "Pretendard-Regular", "Pretendard-Bold" })
        {
            string result = BuildOne(face);
            report.Append('\n').Append(result);
            if (result.StartsWith("만들었습니다")) made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGuards.Dialog(Title, $"{tmpNote}\nTMP 폰트 에셋 {made}개.{report}", "확인");
    }

    static string BuildOne(string faceName)
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + faceName + ".ttf");
        if (source == null) return $"⚠️ {faceName}.ttf 를 못 읽었습니다.";

        string path = $"{OutputFolder}/{faceName} SDF.asset";

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
            source, SamplingPointSize, AtlasPadding, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
        if (asset == null) return $"⚠️ {faceName}: 폰트 에셋 생성에 실패했습니다.";

        asset.name = faceName + " SDF";

        // 실제로 쓰일 글자를 미리 구워 둔다 — 런타임 첫 등장에서 끊기지 않게.
        string prewarm = PrewarmCharacters();
        asset.TryAddCharacters(prewarm, out string missing);

        AssetDatabase.CreateAsset(asset, path);
        // 아틀라스 텍스처·머티리얼은 폰트 에셋의 하위 에셋으로 같이 저장해야 참조가 안 끊긴다.
        if (asset.atlasTextures != null)
            foreach (Texture2D atlas in asset.atlasTextures)
                if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
        if (asset.material != null && !AssetDatabase.Contains(asset.material))
            AssetDatabase.AddObjectToAsset(asset.material, asset);

        EditorUtility.SetDirty(asset);

        int missingCount = string.IsNullOrEmpty(missing) ? 0 : missing.Length;
        return $"만들었습니다: {path}\n  미리 구운 글자 {prewarm.Length - missingCount}자" +
               (missingCount > 0 ? $" (폰트에 없는 글자 {missingCount}자는 건너뜀)" : "");
    }

    /// <summary>
    /// 미리 구워둘 글자 모음 — 게임에 실제로 나오는 것만. 한글 11,172자를 통째로 굽지 않는
    /// 이유는 위 ③ 참고.
    /// </summary>
    static string PrewarmCharacters()
    {
        HashSet<char> set = new HashSet<char>();

        // 숫자·영문·기호(단축키 표기 "(H)", 수치, 화살표 등)
        foreach (char c in "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                           " .,:;/()[]{}+-*=%!?~<>＋→←↑↓■□●○★☆⚠️") set.Add(c);

        // 등급명 — 화면 어디서나 나온다.
        foreach (UnitGrade grade in System.Enum.GetValues(typeof(UnitGrade)))
            foreach (char c in grade.KoreanName()) set.Add(c);

        // HUD 고정 문구
        foreach (string text in new[]
        {
            "골드", "목재", "토큰", "행운의토큰", "마나", "라운드", "남은시간", "메뉴", "동맹", "대화",
            "선택된 유닛 없음", "등급", "체력", "공격력", "사거리", "공격속도", "방어력",
            "공격", "정지", "모으기", "정렬", "이동", "보유 아이템", "항법 선택", "닫기", "선택",
            "조합", "판매", "강화", "특성", "도박", "구매", "사용", "외", "종", "개", "회", "초",
            "지금은 조합할 수 없습니다.", "지금은 사용할 수 없습니다.", "클리어", "필요", "부터", "까지만",
            "사망", "게임 종료", "무적", "디버그 정보",
        })
            foreach (char c in text) set.Add(c);

        // 유닛 이름 240종 — 조합표·전시·하단 정보 칸에 그대로 나온다.
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }))
        {
            UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guid));
            if (unit == null || string.IsNullOrEmpty(unit.unitName)) continue;
            foreach (char c in unit.unitName) set.Add(c);
        }

        StringBuilder builder = new StringBuilder(set.Count);
        foreach (char c in set) builder.Append(c);
        return builder.ToString();
    }
}
