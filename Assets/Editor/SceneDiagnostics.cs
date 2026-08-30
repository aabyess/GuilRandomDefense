using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 배선을 텍스트로 덤프한다.
/// 씬이 바이너리로 저장되면 파일을 직접 읽어 배선을 검증할 수 없는데,
/// 오늘까지 잡은 버그(레시피 미연결, 삭제된 웨이브 참조, 더미를 가리키던 가챠 테이블)가
/// 전부 그 방식으로 찾은 것들이라 대체 경로가 필요하다.
/// </summary>
public static class SceneDiagnostics
{
    const string OutputPath = "Docs/scene-dump.txt";

    [MenuItem("Tools/진단/씬 배선 덤프")]
    static void Dump()
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder report = new StringBuilder();

        report.AppendLine($"씬: {scene.name} ({scene.path})");
        report.AppendLine($"덤프 시각: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();

        report.AppendLine("== 루트 오브젝트 ==");
        foreach (GameObject root in scene.GetRootGameObjects())
            report.AppendLine($"  {(root.activeSelf ? "●" : "○")} {root.name}  (자식 {root.transform.childCount})" +
                              $"  pos={root.transform.position}  rot={root.transform.eulerAngles}");
        report.AppendLine();

        report.AppendLine("== 카메라 ==");
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            report.AppendLine($"  {cam.name}: pos={cam.transform.position} rot={cam.transform.eulerAngles} " +
                              $"ortho={cam.orthographic} size={cam.orthographicSize} fov={cam.fieldOfView} " +
                              $"target={(cam.targetTexture != null ? cam.targetTexture.name : "화면")}");
        report.AppendLine();

        report.AppendLine("== 주요 컴포넌트 배선 ==");
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .OrderBy(b => b.GetType().Name))
        {
            if (behaviour == null) continue;
            System.Type type = behaviour.GetType();
            if (type.Namespace != null && type.Namespace.StartsWith("Unity")) continue;

            report.AppendLine($"\n[{type.Name}] on '{behaviour.gameObject.name}'" +
                              $"{(behaviour.gameObject.activeInHierarchy ? "" : "  (비활성)")}");

            SerializedObject so = new SerializedObject(behaviour);
            SerializedProperty property = so.GetIterator();
            property.NextVisible(true);   // m_Script

            while (property.NextVisible(false))
                report.AppendLine("    " + Describe(property));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, report.ToString());
        AssetDatabase.Refresh();

        Debug.Log($"[진단] 씬 배선을 {OutputPath}에 썼습니다.");
        EditorUtility.DisplayDialog("씬 배선 덤프", $"{OutputPath} 에 저장했습니다.", "확인");
    }

    static string Describe(SerializedProperty property)
    {
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            List<string> items = new List<string>();
            for (int i = 0; i < Mathf.Min(property.arraySize, 4); i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                items.Add(Describe(element).Replace(element.name + ": ", ""));
            }

            string more = property.arraySize > 4 ? $" 외 {property.arraySize - 4}개" : "";
            return $"{property.name}: [{property.arraySize}] {string.Join(", ", items)}{more}";
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                return $"{property.name}: {Name(property.objectReferenceValue)}";
            case SerializedPropertyType.Integer:
                return $"{property.name}: {property.intValue}";
            case SerializedPropertyType.Float:
                return $"{property.name}: {property.floatValue}";
            case SerializedPropertyType.Boolean:
                return $"{property.name}: {property.boolValue}";
            case SerializedPropertyType.String:
                return $"{property.name}: {property.stringValue}";
            case SerializedPropertyType.Enum:
                return $"{property.name}: {property.enumValueIndex}";
            case SerializedPropertyType.Vector3:
                return $"{property.name}: {property.vector3Value}";
            default:
                return $"{property.name}: ({property.propertyType})";
        }
    }

    // 비어 있는 참조와 '깨진' 참조는 원인이 전혀 다르므로 구분해서 적는다.
    static string Name(Object value)
    {
        if (value == null) return "<비어있음>";
        return $"{value.name} ({value.GetType().Name})";
    }

    [MenuItem("Tools/진단/씬을 텍스트로 다시 저장")]
    static void ForceTextReserialize()
    {
        if (EditorSettings.serializationMode != SerializationMode.ForceText)
        {
            EditorUtility.DisplayDialog("재직렬화",
                $"Asset Serialization이 {EditorSettings.serializationMode}입니다.\n" +
                "Project Settings > Editor 에서 Force Text로 바꾼 뒤 다시 실행하세요.", "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        AssetDatabase.ForceReserializeAssets(new[] { scene.path });
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("재직렬화",
            $"{scene.path} 를 다시 저장했습니다.\n형식이 바뀌었는지 확인해 주세요.", "확인");
    }
}
