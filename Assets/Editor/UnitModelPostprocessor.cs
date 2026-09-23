using UnityEditor;
using UnityEngine;

// Assets/Art/Units/<유닛이름>/ 아래로 들어온 모델은 임포트 설정을 자동으로 맞춘다.
//
// 왜 필요한가 — 스킨을 240종까지 받을 예정이라, 하나씩 손으로 Rig 탭을 고치는 건 무리다.
// 손으로 할 때 매번 걸리던 둘을 여기서 자동으로 잡는다:
//   ① Animation Type이 Generic으로 들어온다 → Humanoid여야 Mixamo류 리타게팅이 붙는다
//   ② Strip Bones가 켜져 있다 → 애니메이션에 안 쓰이는 뼈를 지운다. 나중에 외부
//      애니메이션을 붙일 때 필요한 뼈가 이미 없어져 있다
//
// ⚠️ 이미 임포트된 모델은 안 건드린다(사용자가 손으로 조정한 값을 덮지 않기 위해).
//    OnPreprocessModel은 최초 임포트와 Reimport 때만 돈다.
public class UnitModelPostprocessor : AssetPostprocessor
{
    const string UnitModelRoot = "Assets/Art/Units/";

    // Humanoid로 세우면 안 되는 모델 — 폴더 이름(=유닛 이름)으로 지정한다.
    //
    // Humanoid는 유니티가 뼈 이름을 보고 사람 골격(Hips·Spine·LeftArm…)에 매핑하는 방식이다.
    // 뼈 이름이 표준이 아니면 아바타 생성이 조용히 실패하고, 모델이 아예 안 움직인다.
    // 그런 모델은 Generic으로 두고 **그 모델 자신의 애니메이션**을 쓴다 — 공유 클립
    // (idle/walk/attack) 리타게팅은 못 받지만, 애초에 받을 수 있는 골격이 아니다.
    //
    // 2026-09-07 조사(PM):
    //   안흔함_황정기 — Mixamo로 리깅해 와서 이 목록에서 뺐다(mixamorig 표준 뼈 34개).
    //   안흔함_박준희 — SCP-049(블렌더 리그)였다가 사이렌헤드로 교체. 뼈 이름이
    //                   Hips_53·Spine_44·LeftArm_21 꼴(표준 + 숫자 접미어)이라 Humanoid로
    //                   시도한다. 유니티가 아바타를 못 만들면 여기 다시 넣으면 된다.
    //   안흔함_김수빈 — Hips도 무릎도 없는 판이었으나 2026-09-07 Rigify 리그판으로
    //                   교체해 이 목록에서 뺐다(Hips·thigh·shin·forearm 전부 있음).
    // Mixamo로 리깅해 오면 이 목록에서 빼야 한다(그때 mixamorig: 접두어가 붙는다).
    // 🔴 흔함_노태현은 여기 있었다가 **뺐다**(2026-09-07).
    //    「사람 골격에 한 개도 매핑 안 된다(0개)」고 보고 넣었는데, 그 0개 자체가
    //    아래 조기 반환이 만든 낡은 .meta였다. 조기 반환을 없애고 다시 임포트하니
    //    필수 뼈 15개를 전부 잡았다(어깨·발가락·눈까지 21개). Humanoid로 잘 선다.
    //    ⚠️ 교훈: 「매핑 0개」를 리그 탓으로 보기 전에 **다시 임포트부터** 시킬 것.
    static readonly string[] GenericRigUnits =
    {
        //   안흔함_강재규 — 재규어(네 발 짐승)다. 사람 골격 자체가 없으니 Humanoid가 성립하지
        //                  않는다. 뼈 739개가 Maya Advanced Skeleton 이름 규칙(Chest_M·
        //                  IndexToe1_L 꼴)이고, 자기 애니메이션이 한 벌 들어 있다.
        "안흔함_강재규",

        // 안흔함_이호준(좀비) — 세 번 뒤집혔다. 기록을 남긴다.
        //   ① 「손목 뼈 없음」으로 Generic → ② 유니티가 28개를 매핑해서 Humanoid로 복귀 →
        //   ③ 그런데 유니티 **자신의 검증**(avatar.isValid)이 그 아바타를 무효로 판정했다
        //      (2026-09-08 05:03 로그, OnPostprocessModel "아바타를 못 만들었다").
        //      매핑 표가 있어도 뼈 계층·회전이 사람 골격 규칙에 안 맞으면 아바타가 안 선다.
        //      자체 Mixamo 클립이 있으니 Generic이 맞다. 이번 근거는 유니티 판정이다.
        "안흔함_이호준",

        // 배 유닛 두 척(2026-09-13, Blender) — 선체·돛 뼈대(Root·Hull·Sail_*·Flag)라 사람 골격이 아니다.
        // 자기 클립 「<이름>_뼈대|Idle_Bob」(8초 흔들림)이 유일한 동작이다. 폴더 이름은 FBX 이름과 같다.
        "고대의배",
        "해적선",

        // 특별함_노건완(노가리, 잉어 — 2026-09-14) — 물고기라 사람 골격이 없다. 자기 Idle 클립(헤엄·꼬리짓)만 쓴다.
        "특별함_노건완",

        // 특별함_김정래(프로그래머, 노트북 — 2026-09-15) — 뼈 없는 소품. Humanoid로 들이면 「아바타를 못 만들었다」 오류만 난다.
        "특별함_김정래",

        // 전설적인_임장혁(짱스파단장, 메노스 그란데 — 2026-09-17) — 팔다리 없는 원추형 망토라 사람 골격이 없다.
        // 척추·머리·자락 뼈 8개와 자기 Idle 클립(흔들림, fix_unit_fbx.py synth_idle)만 쓴다.
        "전설적인_임장혁",

        // 제한_김강민(오리모토 리카 — 2026-09-17) — 허리 아래가 다리 없는 꼬리라 사람 골격이 성립하지 않는다.
        // 척추·팔·꼬리 뼈 20개와 자기 Idle 클립(호흡·꼬리 물결, gen_scan_rig.py synth_idle)만 쓴다.
        "제한_김강민",

        // 불멸_신지우(빅맘 — 2026-09-22) — 발끝까지 덮는 통 기모노라 다리·오른팔을 뼈별로 가를 메시 경계가 없어
        // Humanoid 필수 뼈 가중치가 불가능했다. 강체 5뼈(Hips·Spine1·Head·양 Shoulder)와 자기 Idle 클립(호흡)만 쓴다.
        "불멸_신지우",

        // 영원_김영원(두꺼비 — 2026-09-22) — 웅크린 레스트라 유니티 자동 매핑이 Hips를 Armature로 잡았다(다리를 펴도 동일).
        // 원본 뼈 22개를 그대로 두고 자기 Idle 클립(호흡)만 쓴다.
        "영원_김영원",

        // 히든_이동엽(라분, 고래 — 2026-09-22) — 사람 골격이 없다. 몸통·머리·꼬리 5뼈와 자기 Idle(둥실·꼬리 물결)만 쓴다.
        "히든_이동엽",

        // 히든_뻬꼼(포치타, 체인소 강아지 — 2026-09-23) — 네 발 짐승이라 사람 골격이 없다.
        // 몸통·머리·꼬리·네 다리 7뼈와 자기 Idle(숨·꼬리 흔들기)만 쓴다.
        "히든_뻬꼼",

        // 랜덤_모몬가(아인즈 울 고운 — 2026-09-23) — 로브가 바닥까지 닫힌 원뿔이라 다리 메시가 아예 없다.
        // 필수 뼈에 실을 정점이 없어 빅맘과 같은 처리. 몸통·가슴·머리·양팔 5뼈와 자기 Idle만 쓴다.
        "랜덤_모몬가",
    };

    // 원본 단위가 달라 유니티에 몇 cm짜리로 들어오는 모델 — 임포트 배율로 먼저 키운다(폴더 이름 = 유닛 이름).
    //
    // 왜 여기서 — ArtBinder.FitToHeight는 1000배 넘게 키워야 하면 「잘못 잰 것」으로 보고 건너뛴다
    // (2026-09-08 이호준 7,062배 폭주를 막은 안전장치). 진짜로 작은 모델은 그 문턱에 걸려 점처럼 남는다.
    // 파일을 다시 지을 수 없는 모델만 여기 적는다 — 나머지는 blender가 fix_unit_fbx.py로 실제 크기로 다시 짓는다.
    static readonly (string unit, float scale)[] ImportScales =
    {
        // 사이렌헤드(glb→fbx assimp 변환본, 원본 glb 없음). 09-13 원격 preview 실측: 게임 안 크기 0.07×0.04×0.21.
        ("안흔함_박준희", 100f),
    };

    // 이 숫자를 올리면 유니티가 Assets/Art/Units 아래 모델을 **전부 다시 임포트**한다.
    // 위의 규칙을 고쳤는데 이미 임포트된 모델에 반영이 안 될 때 올린다.
    //
    // 1 → 2 (2026-09-07): 조기 반환을 없애 교체된 스킨의 아바타를 다시 만들게 했다.
    //                     기존 .meta에 남아 있던 옛 뼈 매핑을 씻어내야 T자가 풀린다.
    // 2 → 3 (2026-09-07): 안흔함_강재규(재규어)를 Generic으로 뺐다.
    // 3 → 4 (2026-09-07): 흔함_노태현을 Generic에서 되돌렸다(재임포트하니 잘 매핑됐다).
    // 4 → 5 (2026-09-07): 안흔함_이호준(좀비)을 Generic으로 뺐다.
    // 5 → 6 (2026-09-08): 한글 경로 NFC 정규화 · 옛 humanDescription 초기화 · 이호준 Humanoid 복귀.
    // 6 → 7 (2026-09-08): 이호준 Generic 확정(유니티 아바타 검증 실패 실측).
    // 7 → 8 (2026-09-13): 배 유닛 고대의배·해적선을 Generic으로 추가.
    // 8 → 9 (2026-09-13): ImportScales(박준희 100배) 추가.
    // 9 → 10 (2026-09-15): 노트북 소품 특별함_김정래를 Generic으로 추가.
    public override uint GetVersion() => 10;

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(UnitModelRoot)) return;

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.globalScale = ImportScaleFor(assetPath);

        if (IsGenericRigUnit(assetPath))
        {
            // Generic이어도 뼈는 남긴다 — 자기 애니메이션이 그 뼈를 쓴다.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            // Humanoid로 한 번 잘못 들어갔다 오면 .meta에 importAnimation:0이 남는다.
            // Generic은 자기 애니메이션이 유일한 동작이라 반드시 켠다.
            importer.importAnimation = true;
            KeepBones(importer);
            return;
        }

        // ⚠️ 예전엔 여기서 "이미 Humanoid면 그대로 둔다"고 조기 반환했다. 그게 T자 자세의
        //    진짜 원인이었다(2026-09-07). 같은 경로의 모델 파일만 새 스킨으로 갈아끼우면
        //    .meta는 살아남는다 — 그 안의 humanDescription(뼈 매핑)은 **이전 모델의 것**이다.
        //    animationType이 이미 Human이라 조기 반환하니 아바타가 영영 안 다시 만들어지고,
        //    없는 뼈를 가리키는 아바타로는 리타게팅이 조용히 실패해 바인드 포즈(T자)로 선다.
        //    실측: 안흔함_황정기 FBX엔 mixamorig 뼈가 34개 있는데 메타 매핑은 4개뿐이었다.
        //
        //    이 프로세서는 스킨 240종을 자동으로 세우는 게 목적이라 Rig 탭을 손으로 만지지
        //    않는다. 예외가 필요하면 GenericRigUnits로 뺀다. 그래서 매번 다시 만든다.
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;

        // 🔴 .meta에 뼈 매핑(humanDescription)이 이미 있으면 유니티는 그걸 **사람이 정한 값**으로
        //    보고 자동 매핑을 건너뛴다. avatarSetup을 CreateFromThisModel로 둬도 마찬가지다.
        //    2026-09-08 로그 실측: 황정기가 재임포트됐는데도 이전 모델의 뼈 4개를 그대로 물고
        //    "아바타를 못 만들었다"로 끝났다. 매핑을 비워야 현재 파일로 다시 잡는다.
        //    우리는 Rig 탭을 손으로 안 만지므로(240종 자동화) 지울 값은 없다.
        HumanDescription fresh = importer.humanDescription;
        fresh.human = new HumanBone[0];
        fresh.skeleton = new SkeletonBone[0];
        importer.humanDescription = fresh;

        // 모델에 딸려 온 애니메이션은 안 가져온다.
        //
        // 왜 — Humanoid 유닛은 공용 컨트롤러(Character.controller)의 Idle/Walk/Attack을
        // 리타게팅해서 쓴다. 모델 자체 클립까지 들어오면 그게 이기고, 부스·조합판 인형이
        // 엉뚱한 자세로 선다(2026-09-07 김경현이 Mixamo의 FreeRunning 발차기 자세로 섰다).
        // Generic 쪽은 정반대로 자기 애니메이션이 유일한 동작이라 위에서 건드리지 않는다.
        importer.importAnimation = false;

        KeepBones(importer);
    }

    // 아바타가 실제로 만들어졌는지 확인한다.
    //
    // 왜 — Humanoid 매핑 실패는 **조용하다**. 임포트는 성공하고, 프리팹도 생기고, 컨트롤러도
    // 붙는다. 다만 유닛이 T자로 서 있을 뿐이다. 부스에 240개가 서 있으면 어느 게 실패한
    // 건지 눈으로 못 고른다. 그래서 임포트 시점에 이름을 찍어 준다.
    void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith(UnitModelRoot)) return;
        if (IsGenericRigUnit(assetPath)) return;

        ModelImporter importer = (ModelImporter)assetImporter;
        if (importer.animationType != ModelImporterAnimationType.Human) return;

        Animator animator = root.GetComponent<Animator>();
        if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            return;

        Debug.LogError(
            $"[스킨] {assetPath} — Humanoid 아바타를 못 만들었다. 이 유닛은 T자로 선다.\n" +
            "뼈 이름이 표준(Hips·Spine·LeftArm…)이 아닐 가능성이 크다. " +
            "Mixamo로 리깅해 오거나, UnitModelPostprocessor.GenericRigUnits에 유닛 이름을 넣어 " +
            "Generic(자기 애니메이션 사용)으로 돌려라.");
    }

    // 폴더 이름이 목록에 있으면 Generic으로 둔다. 경로는 "Assets/Art/Units/<유닛>/<파일>" 꼴이다.
    static bool IsGenericRigUnit(string path)
    {
        // 🔴 macOS에서 유니티가 넘기는 assetPath는 한글이 NFC가 아니다(자모가 풀린 채 온다).
        //    소스의 리터럴은 NFC라서 그냥 StartsWith하면 **조용히 false**다 — 2026-09-08
        //    재규어가 이 목록에 있는데도 Humanoid로 임포트됐던 게 이것이다.
        //    양쪽을 NFC로 맞춰 비교한다. 한글 경로를 리터럴과 견주는 곳은 전부 같은 함정이다.
        string nfc = path.Normalize(System.Text.NormalizationForm.FormC);
        foreach (string unit in GenericRigUnits)
            if (nfc.StartsWith((UnitModelRoot + unit + "/").Normalize(System.Text.NormalizationForm.FormC)))
                return true;

        return false;
    }

    static float ImportScaleFor(string path)
    {
        string nfc = path.Normalize(System.Text.NormalizationForm.FormC);   // IsGenericRigUnit과 같은 NFC 함정
        foreach ((string unit, float scale) in ImportScales)
            if (nfc.StartsWith((UnitModelRoot + unit + "/").Normalize(System.Text.NormalizationForm.FormC)))
                return scale;
        return 1f;
    }

    // 외부 애니메이션(Mixamo 등)을 나중에 붙일 수 있게 뼈를 남긴다.
    // Strip Bones가 켜져 있으면 애니메이션에 안 쓰이는 뼈가 지워져, 나중에 필요한 뼈가 이미 없다.
    static void KeepBones(ModelImporter importer)
    {
        SerializedObject so = new SerializedObject(importer);
        SerializedProperty strip = so.FindProperty("m_StripBones");
        if (strip != null) { strip.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
