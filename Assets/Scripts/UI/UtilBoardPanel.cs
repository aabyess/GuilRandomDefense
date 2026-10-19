using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유틸보드(S2 2.323, 사양 GRD_s2_ideas/spec_ui.md 3절) — 왼쪽 위 「유틸」 단추로 켜고 끈다(원작 기본 OFF).
///   ① 방깎·이감 합계 4줄(원작 BjK): 「유틸 갱신」을 눌렀을 때만 다시 센다(원작도 수동, 대기 5초 j:1339).
///      공식은 사양 3.4 제안 그대로 — 내 유닛(흔함·소환물 제외)의 지금 스킬 레벨 효과 중 적 대상만, 버프키(buffId, 없으면 스킬 에셋 이름)마다 최댓값을 더한다.
///   ② 진행 중인 스토리가 있으면 그 위에 플레이어별 기여도 %(0.25초마다, 원작 CNq). 기준은 우리 보상 기준 30%(RewardDistributor), 4번째 스토리부터 보인다.
/// 둘 다 호스트에만 있는 값이다(특성 구매·스킬 레벨은 UnitUpgrades, 기여 피해는 EnemyDummy.ContributionDamage). 멀티 클라는 「호스트에서만 표시」만 보인다(복제는 나중에).
/// 스스로 붙는다(QuestTimerPanel과 같은 방식) — GameHud는 건드리지 않는다. GameHud가 있을 때만 보인다.
/// </summary>
public class UtilBoardPanel : MonoBehaviour
{
    static UtilBoardPanel instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!S2Features.Enabled) return;   // S2Features 꺼짐
        if (instance != null) return;
        var host = new GameObject("UtilBoardPanel");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<UtilBoardPanel>();
    }

    const float RefreshCooldown = 5f;     // 원작 jc=5.
    const float StoryTick = 0.25f;        // 원작 CNq 주기
    const float ContributionGoal = 30f;   // 우리 기여 보상 기준(RewardDistributor 「기여도 30% 이상」)
    const float PanelWidth = 360f;

    GameObject canvasRoot;
    GameObject panel;
    TMP_Text body;
    readonly List<TMP_Text> labels = new List<TMP_Text>();
    bool open;
    bool refreshedOnce;
    float refreshReadyAt;
    float nextTick;
    float[] totals = new float[4];   // C1 방깎 합계 · C4 발동 방깎 · C2 오라 이감% · C3 발동 이감%
    int totalsFor = -1;

    void Awake()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvasRoot = canvasGo;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 켜고 끄는 단추 — 윗줄(약 48px) 바로 아래 왼쪽
        MakeButton(canvasGo.transform, "유틸", new Vector2(10f, -60f), new Vector2(70f, 30f), Toggle);

        panel = new GameObject("Board", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);   // 오른쪽 점수판 아래(왼쪽은 알림 글자 자리라 겹친다) — 의뢰 타이머 창(-232)과도 안 겹치게
        rt.anchoredPosition = new Vector2(-24f, -330f);
        rt.sizeDelta = new Vector2(PanelWidth, 200f);
        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.07f, 0.78f);
        bg.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        var tr = (RectTransform)textGo.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(10f, 44f); tr.offsetMax = new Vector2(-10f, -6f);
        body = textGo.AddComponent<TextMeshProUGUI>();
        body.fontSize = 18f; body.color = Color.white; body.alignment = TextAlignmentOptions.TopLeft;
        body.richText = true; body.raycastTarget = false;
        labels.Add(body);

        Button refresh = MakeButton(panel.transform, "유틸 갱신", Vector2.zero, new Vector2(110f, 30f), Refresh);
        var br = (RectTransform)refresh.transform;
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 0f);
        br.anchoredPosition = new Vector2(10f, 8f);

        panel.SetActive(false);
        canvasRoot.SetActive(false);
    }

    Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, System.Action onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.13f, 0.20f, 0.92f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(() => onClick());
        var tGo = new GameObject("Label", typeof(RectTransform));
        tGo.transform.SetParent(go.transform, false);
        var tr = (RectTransform)tGo.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        var t = tGo.AddComponent<TextMeshProUGUI>();
        t.text = label; t.fontSize = 17f; t.color = new Color(1f, 0.86f, 0.35f); t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        labels.Add(t);
        return btn;
    }

    void Toggle()
    {
        open = !open;
        panel.SetActive(open);
        nextTick = 0f;
        PlayerNotification.Show(LocalPlayer.LocalPlayerId, $"<color=#F15F5F>[시스템]</color> 유틸보드를 {(open ? "표시합니다." : "숨깁니다.")}", 3f);
    }

    void Refresh()
    {
        int me = LocalPlayer.LocalPlayerId;
        float left = refreshReadyAt - Time.unscaledTime;
        if (left > 0f)
        {
            PlayerNotification.Show(me, $"<color=#F15F5F>[시스템]</color> 유틸 갱신 대기시간이 <color=#ffcc00>{left:0.0}초</color> 남았습니다.", 3f);
            return;
        }
        refreshReadyAt = Time.unscaledTime + RefreshCooldown;
        if (GameAuthority.IsServer) { totals = ComputeTotals(me); totalsFor = me; }
        refreshedOnce = true;
        nextTick = 0f;
        PlayerNotification.Show(me, "<color=#F15F5F>[시스템]</color> 유틸리티 정보를 갱신했습니다.", 3f);
    }

    void Update()
    {
        bool hud = FindFirstObjectByType<GameHud>() != null;
        if (canvasRoot.activeSelf != hud) canvasRoot.SetActive(hud);
        if (!hud) return;
        if (GameHud.UiFontAsset != null)
            foreach (TMP_Text t in labels) if (t != null && t.font != GameHud.UiFontAsset) t.font = GameHud.UiFontAsset;
        if (!open || Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + StoryTick;
        string text = BuildText(out int lines);
        if (body.text != text) body.text = text;
        ((RectTransform)panel.transform).sizeDelta = new Vector2(PanelWidth, 56f + lines * 23f);
    }

    string BuildText(out int lines)
    {
        var sb = new StringBuilder();
        int count = 0;
        void Line(string s) { sb.Append(s).Append('\n'); count++; }
        BuildBody(Line);
        lines = count;
        return sb.ToString().TrimEnd('\n');
    }

    void BuildBody(System.Action<string> Line)
    {

        StoryManager sm = StoryManager.Instance;
        bool server = GameAuthority.IsServer;
        EnemyDummy story = server && sm != null ? sm.ActiveStoryEnemy : null;
        if (story != null && story.ContributionDamage != null && sm.Running != null)
        {
            Line($"<b>유틸보드 - {StoryManager.DisplayName(sm.Running)}</b>");
            Line("<color=#ffcc00>[스토리 기여도]</color>");
            float maxHp = Mathf.Max(1f, story.MaxHp);
            bool showGoal = sm.FinishedCount >= 3;   // 원작: 처음 세 스토리는 기준 없이 %만
            foreach (PlayerContext c in PlayerContext.Occupied)
            {
                int p = c.PlayerId;
                if (p < 0 || p >= story.ContributionDamage.Length) continue;
                float pct = 100f * story.ContributionDamage[p] / maxHp;
                string col = StatsChatCommands.PlayerHex(p);
                string name = $"<color=#{col}>{StatsChatCommands.PlainName(p)}</color>";
                if (!showGoal) Line($"{name} [<color=#00ff22>{pct:0.00}%</color>]");
                else
                {
                    string pctText = pct >= ContributionGoal ? $"<color=#00ff22>{pct:0.00}%</color>" : $"{pct:0.00}%";
                    Line($"{name} [{pctText} / <color=#00ced1>{ContributionGoal:0}%</color>]");
                }
            }
        }
        else
        {
            Line("<b>유틸보드</b>");
            Line(server ? "<color=#aaaaaa>현재 진행 중인 스토리 기여도 없음</color>" : "<color=#aaaaaa>스토리 기여도 — 호스트에서만 표시</color>");
        }

        if (!server)
        {
            Line("<color=#aaaaaa>방깎·이감 합계 — 호스트에서만 표시</color>");
            return;
        }
        if (totalsFor != LocalPlayer.LocalPlayerId) { totals = new float[4]; totalsFor = LocalPlayer.LocalPlayerId; }
        Line(refreshedOnce ? "<color=#ffcc00>[유틸리티 정보 - 수동 갱신]</color>" : "<color=#ffcc00>[유틸 갱신 버튼을 눌러주세요]</color>");
        Line($"방어력 감소 합계 : <color=#ffcc00>{totals[0]:0.0}</color>");
        Line($"발동형 방어력 감소 : <color=#ffcc00>{totals[1]:0.#}</color>");
        Line($"오라형 이동속도 감소 : <color=#ffcc00>{totals[2]:0.#}%</color>");
        Line($"발동형 이동속도 감소 : <color=#ffcc00>{totals[3]:0.#}%</color>");
    }

    static bool HitsEnemies(SkillTargetKind t) =>
        t == SkillTargetKind.Enemies || t == SkillTargetKind.SingleTarget || t == SkillTargetKind.RandomEnemyInRange || t == SkillTargetKind.ChainEnemies;

    /// <summary>
    /// 사양 3.4 공식(호스트) — 반환 [C1 방깎 합계, C4 발동 방깎, C2 오라 이감%, C3 발동 이감%].
    /// 원작 S2처럼 버프키마다 최댓값 하나만 세고 키끼리 더한다(우리 엔진 실제 반영량 — 이감 최강 하나·AId1 상한 75 — 과는 다를 수 있다, 사양 모호점 6).
    /// </summary>
    public static float[] ComputeTotals(int playerId)
    {
        var cand = new Dictionary<string, float>[4];
        for (int i = 0; i < 4; i++) cand[i] = new Dictionary<string, float>();
        void Put(int cat, string key, float v)
        {
            if (v <= 0f) return;
            if (!cand[cat].TryGetValue(key, out float old) || v > old) cand[cat][key] = v;
        }

        PlayerContext context = PlayerContext.Get(playerId);
        if (context == null || context.UnitInventory == null) return new float[4];
        UnitUpgrades upgrades = context.UnitUpgrades;
        foreach (UnitIdentity u in context.UnitInventory.Members)
        {
            if (u == null || u.IsSummon || u.Data == null) continue;
            UnitData d = u.Data;
            if (d.grade == UnitGrade.Common) continue;   // S2 BIG: 흔함 목록 제외(사양 모호점 5)
            int levelIndex = upgrades != null ? upgrades.SkillLevelIndexFor(d) : 0;
            for (int si = 0; si < d.SkillCount; si++)
            {
                SkillData s = d.SkillAt(si);
                if (s == null || s.levels == null || s.levels.Count == 0) continue;
                SkillLevel lv = s.levels[Mathf.Clamp(levelIndex, 0, s.levels.Count - 1)];
                if (lv == null || lv.effects == null) continue;
                bool aura = s.triggerType == SkillTriggerType.Aura;
                foreach (SkillEffect e in lv.effects)
                {
                    if (e == null || !HitsEnemies(e.target)) continue;
                    string key = string.IsNullOrEmpty(e.buffId) ? s.name : e.buffId;
                    switch (e.kind)
                    {
                        case SkillEffectKind.ArmorBonus:
                            if (e.multiplier < 0f) Put(0, key, -e.multiplier);
                            break;
                        case SkillEffectKind.ArmorBreak:
                            Put(1, key, e.multiplier);
                            break;
                        case SkillEffectKind.Slow:
                            float pct = e.multiplier <= 0f ? 100f : (1f - e.multiplier) * 100f;   // 0 = 「최저 이속까지」
                            if (aura) Put(2, key, pct);
                            else if (e.duration > 0f) Put(3, key, pct);
                            break;
                    }
                }
            }
            if (upgrades != null)
            {
                Put(1, "trait:" + d.name, upgrades.EffectSum(d, TraitEffectKind.ArmorShred));
                Put(3, "trait:" + d.name, upgrades.EffectSum(d, TraitEffectKind.SlowOnHit) * 100f);
            }
        }

        // 표시 순서는 판 줄 순서: [C1, C4, C2, C3]
        var result = new float[4];
        for (int i = 0; i < 4; i++) foreach (float v in cand[i].Values) result[i] += v;
        return result;
    }
}
