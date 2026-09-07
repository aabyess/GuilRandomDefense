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
    //   안흔함_김수빈 — 나나치(메이드 인 어비스). Hips가 아예 없고(루트가 spine) 다리에
    //                   무릎도 없다(thigh→foot 직결). 짐승 형태라 사람 골격이 아니다.
    // Mixamo로 리깅해 오면 이 목록에서 빼야 한다(그때 mixamorig: 접두어가 붙는다).
    static readonly string[] GenericRigUnits =
    {
        "안흔함_김수빈",
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

        // 모델에 딸려 온 애니메이션은 안 가져온다.
        //
        // 왜 — Humanoid 유닛은 공용 컨트롤러(Character.controller)의 Idle/Walk/Attack을
        // 리타게팅해서 쓴다. 모델 자체 클립까지 들어오면 그게 이기고, 부스·조합판 인형이
        // 엉뚱한 자세로 선다(2026-09-07 김경현이 Mixamo의 FreeRunning 발차기 자세로 섰다).
        // Generic 쪽은 정반대로 자기 애니메이션이 유일한 동작이라 위에서 건드리지 않는다.
        importer.importAnimation = false;

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
