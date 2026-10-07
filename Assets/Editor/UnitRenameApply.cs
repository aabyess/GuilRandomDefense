using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 사장님 유닛 이름 바꾸기 18건(10-07, 웹 이름표). 에셋(파일) 이름은 그대로 — GUID·대응표·세이브 키가 그걸 쓴다.
/// 규칙(사장님 확정): 새 이름이 한 단어 = 앞 인물 이름만 바꾸고 칭호 유지 · 두 단어 = 인물+칭호 전체 교체.
/// 인물 이름은 UnitData.personOverride(맨 뒤 필드), 칭호는 unitName. 히든 5명은 이름 한 덩어리라 unitName=새 이름 + personOverride=새 이름(둘이 같으면 한 번만 보인다).
/// 호출: call UnitRenameApply.Apply (다시 불러도 안전 — 같은 값을 쓴다) · call UnitRenameApply.Report (옛→새 표 로그).
/// </summary>
static class UnitRenameApply
{
    // 로스터 파일 이름 · 새 인물 이름 · 새 칭호(null = 칭호 유지)
    static readonly (string asset, string person, string title)[] Table =
    {
        ("특별함_이지원", "김지원", null),
        ("희귀함_배현진", "박현진", "퀸카"),
        ("희귀함_선효진", "선효진", "보디빌더"),
        ("희귀함_정내연", "장내연", "피카소"),
        ("전설적인_양문호", "양문호", "트위터"),
        ("전설적인_이유선", "김유선", "페미니스트"),
        ("전설적인_이현주", "진현주", "그분의여자"),
        ("전설적인_임건웅", "노건웅", null),
        ("전설적인_정윤식", "정윤식", "CM설립자"),
        ("전설적인_진연서", "이연서", null),
        ("히든_석성례", "성성례", "성성례"),
        ("히든_여은서", "푸은서", "푸은서"),
        ("히든_전유라", "이유라", "이유라"),
        ("히든_전주연", "광주연", "광주연"),
        ("히든_최윤서", "노윤서", "노윤서"),
        ("전설적인_신지우", "최지우", null),
        ("불멸_신지우", "최지우", null),
        ("불멸_이이삭", "김이삭", null),
        ("영원_이지원", "김지원", null),
    };

    static string Apply()
    {
        var sb = new StringBuilder();
        int changed = 0;
        foreach (var row in Table)
        {
            string path = $"Assets/Data/Units/Roster/{row.asset}.asset";
            var unit = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (unit == null) { sb.AppendLine($"  ❌ 없음 {path}"); continue; }
            var so = new SerializedObject(unit);
            string beforeName = unit.DisplayName;
            so.FindProperty("personOverride").stringValue = row.person;
            if (row.title != null) so.FindProperty("unitName").stringValue = row.title;
            if (so.ApplyModifiedPropertiesWithoutUndo()) { EditorUtility.SetDirty(unit); changed++; }
            sb.AppendLine($"  {row.asset}: 「{beforeName}」 → 「{unit.DisplayName}」");
        }
        AssetDatabase.SaveAssets();
        return $"유닛 이름 {Table.Length}건 (바뀐 에셋 {changed})\n{sb}";
    }
}
