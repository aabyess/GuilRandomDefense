using UnityEditor;
using UnityEngine;

/// <summary>
/// 유물 「부서진손거울」(사장님 10-06) — 스토리 확률 아이템 6종 중 스토리 4(1/22) 자리의 폐함대선(I004)을 대신한다. 나머지 5종은 원작 그대로.
/// 능력은 폐함대선의 것을 그대로 물려받는다(복사). 폐함대선 에셋은 지우지 않는다 — 스토리 4 보상에서만 뺀다(아이템 도박 풀 H0BS는 계속 가진다).
/// 호출: call BrokenMirrorApply.Apply (다시 불러도 안전). 끝나면 NetCatalog(멀티 아이템 목록)도 다시 만든다.
/// </summary>
static class BrokenMirrorApply
{
    const string Source = "Assets/Data/Items/ItemData_I004_폐함대선.asset";
    const string Target = "Assets/Data/Items/ItemData_R001_부서진손거울.asset";
    const string StoryPath = "Assets/Data/Stories/Story04_구일초등학교.asset";

    static string Apply()
    {
        ItemData source = AssetDatabase.LoadAssetAtPath<ItemData>(Source);
        if (source == null) return "❌ 폐함대선 에셋 없음";
        ItemData mirror = AssetDatabase.LoadAssetAtPath<ItemData>(Target);
        if (mirror == null)
        {
            if (!AssetDatabase.CopyAsset(Source, Target)) return "❌ 에셋 복사 실패";
            mirror = AssetDatabase.LoadAssetAtPath<ItemData>(Target);
        }
        mirror.itemName = "부서진손거울";
        mirror.isRelic = true;
        mirror.icon = null;   // 비슷한 아이콘이 없다 — 글자만(사장님 지시)
        mirror.tooltipText = "유물. " + source.tooltipText;
        mirror.designNote = "사장님 10-06 유물 「부서진손거울」: 스토리 4(1/22) 폐함대선 자리를 대신한다. 기본 능력은 폐함대선(I004)을 그대로 물려받는다(linkedAbilityId·효과 필드 복사). 노태현 전용 추가 효과는 사장님이 따로 주실 때까지 없음.";
        EditorUtility.SetDirty(mirror);

        StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(StoryPath);
        if (story == null || story.itemDrops == null || story.itemDrops.Count == 0) return "❌ 스토리 4 itemDrops 없음";
        int replaced = 0;
        foreach (EnemyItemDrop drop in story.itemDrops)
            if (drop.item == source || drop.item == mirror) { drop.item = mirror; drop.message = "★아이템:\n부서진손거울 - 전설적인 획득!"; replaced++; }
        EditorUtility.SetDirty(story);
        AssetDatabase.SaveAssets();
        NetSetup.BuildCatalog();
        return $"부서진손거울 적용: 아이템 에셋 {Target} · 스토리 4 보상 {replaced}칸 교체(확률 {story.itemDropChance:F4}) · 폐함대선 에셋 유지";
    }
}
