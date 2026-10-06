using UnityEditor;
using UnityEngine;

/// <summary>
/// 유물 「지배자의싸인」(사장님 10-06, 초월 엄태웅) — 스토리 6(1/29)의 우타의 헤드셋(I00H, 아군 공격속도 +12%) 자리를 대신한다. 나머지 5종은 원작 그대로.
/// 사장님: 「능력 따로 없어서 원작 아무 유물이랑 능력 맞춰주고 대체해」. 고른 이유: 엄태웅은 물딜·공격력/공속 오라 결이고, 헤드셋은 효과값이 확정·구현된 범용 공속 버프라 의미가 있다
/// (명검-흑도슈스이는 효과 필드가 비어 있어 능력을 이을 게 없고, 불사조의 깃털·약주는 체력회복 결).
/// 능력은 헤드셋의 것을 그대로 물려받는다(복사). 헤드셋 에셋은 지우지 않는다 — 스토리 6 보상에서만 뺀다(아이템 도박 풀은 계속 가진다). BrokenMirrorApply와 같은 방식.
/// 호출: call RulerSignApply.Apply (다시 불러도 안전). 끝나면 NetCatalog(멀티 아이템 목록)도 다시 만든다.
/// </summary>
static class RulerSignApply
{
    const string Source = "Assets/Data/Items/ItemData_I00H_우타의헤드셋.asset";
    const string Target = "Assets/Data/Items/ItemData_R003_지배자의싸인.asset";
    const string StoryPath = "Assets/Data/Stories/Story06_구일고등학교.asset";

    static string Apply()
    {
        ItemData source = AssetDatabase.LoadAssetAtPath<ItemData>(Source);
        if (source == null) return "❌ 우타의 헤드셋 에셋 없음";
        ItemData sign = AssetDatabase.LoadAssetAtPath<ItemData>(Target);
        if (sign == null)
        {
            if (!AssetDatabase.CopyAsset(Source, Target)) return "❌ 에셋 복사 실패";
            sign = AssetDatabase.LoadAssetAtPath<ItemData>(Target);
        }
        sign.itemName = "지배자의싸인";
        sign.isRelic = true;
        sign.icon = null;   // 비슷한 아이콘이 없다 — 글자만
        sign.tooltipText = "유물. " + source.tooltipText;
        sign.designNote = "사장님 10-06 유물 「지배자의싸인」(초월 엄태웅): 스토리 6(1/29) 우타의 헤드셋 자리를 대신한다. 능력은 헤드셋(아군 공격속도 +12%)을 그대로 물려받는다(effects 복사). 엄태웅 전용 추가 효과는 사장님이 따로 주실 때까지 없음.";
        EditorUtility.SetDirty(sign);

        StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(StoryPath);
        if (story == null || story.itemDrops == null || story.itemDrops.Count == 0) return "❌ 스토리 6 itemDrops 없음";
        int replaced = 0;
        foreach (EnemyItemDrop drop in story.itemDrops)
            if (drop.item == source || drop.item == sign) { drop.item = sign; drop.message = "★아이템:\n지배자의싸인 - 초월함 획득!"; replaced++; }
        EditorUtility.SetDirty(story);
        AssetDatabase.SaveAssets();
        NetSetup.BuildCatalog();
        return $"지배자의싸인 적용: 아이템 에셋 {Target} · 스토리 6 보상 {replaced}칸 교체(확률 {story.itemDropChance:F4}) · 우타의 헤드셋 에셋 유지";
    }
}
