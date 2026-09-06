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

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(UnitModelRoot)) return;

        ModelImporter importer = (ModelImporter)assetImporter;

        // 이미 사람 손이 닿은 모델은 그대로 둔다 — 이 프로세서는 "처음 들어올 때"만 맞춘다.
        if (importer.importSettingsMissing == false && importer.animationType == ModelImporterAnimationType.Human)
            return;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;

        // 외부 애니메이션(Mixamo 등)을 나중에 붙일 수 있게 뼈를 남긴다.
        SerializedObject so = new SerializedObject(importer);
        SerializedProperty strip = so.FindProperty("m_StripBones");
        if (strip != null) { strip.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
