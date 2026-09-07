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
    //   안흔함_박준희 — 블렌더 기본 이름(Armature·Bone_001…). 사람 골격 매핑 불가
    //   안흔함_황정기 — 뼈가 Scene_Root뿐. 사실상 오브젝트 애니메이션
    // 둘 다 Mixamo로 리깅해 오면 이 목록에서 빼야 한다(그때 mixamorig: 접두어가 붙는다).
    static readonly string[] GenericRigUnits =
    {
        "안흔함_박준희",
        "안흔함_황정기",
    };

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

        // 이미 사람 손이 닿은 모델은 그대로 둔다 — 이 프로세서는 "처음 들어올 때"만 맞춘다.
        if (importer.importSettingsMissing == false && importer.animationType == ModelImporterAnimationType.Human)
            return;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;

        KeepBones(importer);
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
