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
    };

    // 이 숫자를 올리면 유니티가 Assets/Art/Units 아래 모델을 **전부 다시 임포트**한다.
    // 위의 규칙을 고쳤는데 이미 임포트된 모델에 반영이 안 될 때 올린다.
    //
    // 1 → 2 (2026-09-07): 조기 반환을 없애 교체된 스킨의 아바타를 다시 만들게 했다.
    //                     기존 .meta에 남아 있던 옛 뼈 매핑을 씻어내야 T자가 풀린다.
    // 2 → 3 (2026-09-07): 안흔함_강재규(재규어)를 Generic으로 뺐다.
    // 3 → 4 (2026-09-07): 흔함_노태현을 Generic에서 되돌렸다(재임포트하니 잘 매핑됐다).
    public override uint GetVersion() => 4;

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(UnitModelRoot)) return;

        ModelImporter importer = (ModelImporter)assetImporter;

        if (IsGenericRigUnit(assetPath))
        {
            // Generic이어도 뼈는 남긴다 — 자기 애니메이션이 그 뼈를 쓴다.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.optimizeGameObjects = false;
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
        foreach (string unit in GenericRigUnits)
            if (path.StartsWith(UnitModelRoot + unit + "/")) return true;

        return false;
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
