using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기록지침(미션판) 창 — 원랜디 S2 2.323의 미션 아이콘판 2×5 + 개인 퀘스트 3칸 + 리롤(사장님 10-18 「S2 아이디어 넣자」).
/// 상단 「퀘스트 (F9)」 단추와 F9 키가 연다(GameHud). 내용은 S2Missions.View(각자 — 멀티 클라는 복제값).
/// 칸에 마우스를 올리면 아래에 「미션 :: 이름 / 내용 / 보상 / 공유·개인」(S2 Bfs, j:9667~9681). 깬 칸은 검게 덮고 「클리어」.
/// </summary>
public class MissionBoardPanel : MonoBehaviour
{
    static MissionBoardPanel instance;
    GameObject window;
    TMP_Text berryText, detailText, hiddenText;
    readonly Image[] cellBg = new Image[S2Missions.MissionCount];
    readonly GameObject[] cellClear = new GameObject[S2Missions.MissionCount];
    readonly RectTransform[] cellRect = new RectTransform[S2Missions.MissionCount];
    readonly TMP_Text[] questName = new TMP_Text[3], questDesc = new TMP_Text[3], questProg = new TMP_Text[3], questReward = new TMP_Text[3];
    readonly GameObject[] rerollButton = new GameObject[3], questClear = new GameObject[3];
    GameObject poneButton;
    float nextRefresh;
    int hovered = -1;

    public static bool IsOpen => instance != null && instance.window != null && instance.window.activeSelf;

    public static void Install(Transform hudRoot)
    {
        if (!S2Features.Enabled) return;   // S2Features 꺼짐
        if (instance != null) return;
        GameObject go = new GameObject("MissionBoardPanel", typeof(RectTransform));
        go.transform.SetParent(hudRoot, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        instance = go.AddComponent<MissionBoardPanel>();
        instance.Build();
    }

    public static void Toggle()
    {
        if (instance == null) return;
        bool open = !instance.window.activeSelf;
        instance.window.SetActive(open);
        if (open) { instance.window.transform.SetAsLastSibling(); instance.Refresh(); }
    }

    public static void Open() { if (instance != null && !IsOpen) Toggle(); }

    static TMP_Text Label(Transform parent, string text, float size, TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text t = go.GetComponent<TextMeshProUGUI>();
        if (GameHud.UiFontAsset != null) t.font = GameHud.UiFontAsset;
        t.text = text; t.fontSize = size; t.alignment = align; t.color = color; t.raycastTarget = false;
        return t;
    }

    static RectTransform Box(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return (RectTransform)go.transform;
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
    { r.anchorMin = r.anchorMax = r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size; }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
    static readonly Color SharedColor = new Color(0.16f, 0.22f, 0.36f, 1f), PersonalColor = new Color(0.3f, 0.16f, 0.3f, 1f);

    void Build()
    {
        RectTransform wr = Box(transform, "Window", new Color(0.07f, 0.08f, 0.12f, 0.96f));
        window = wr.gameObject;
        Place(wr, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1000f, 700f));
        Outline ol = window.AddComponent<Outline>(); ol.effectColor = new Color(0.85f, 0.68f, 0.25f, 1f); ol.effectDistance = new Vector2(2f, -2f);

        TMP_Text title = Label(wr, "기록지침", 32, TextAlignmentOptions.Center, Gold);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(600f, 44f));
        berryText = Label(wr, "", 24, TextAlignmentOptions.Right, new Color(0f, 0.85f, 1f));
        Place(berryText.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(300f, 34f));

        TMP_Text missionHead = Label(wr, "미션  <size=18><color=#9AA4B5>(마우스를 올리면 내용)</color></size>", 24, TextAlignmentOptions.Left, Color.white);
        Place(missionHead.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -58f), new Vector2(600f, 32f));

        for (int i = 0; i < S2Missions.MissionCount; i++)
        {
            S2Missions.MissionInfo info = S2Missions.Missions[i];
            int row = i / 5, col = i % 5;
            RectTransform cell = Box(wr, "Mission" + i, info.shared ? SharedColor : PersonalColor);
            Place(cell, new Vector2(0f, 1f), new Vector2(24f + col * 192f, -96f - row * 96f), new Vector2(180f, 86f));
            cellRect[i] = cell; cellBg[i] = cell.GetComponent<Image>();
            cell.gameObject.AddComponent<Outline>().effectColor = new Color(0.55f, 0.48f, 0.3f, 0.9f);
            TMP_Text nm = Label(cell, info.shortName, 22, TextAlignmentOptions.Center, Color.white);
            Stretch(nm.rectTransform); nm.rectTransform.offsetMin = new Vector2(4f, 22f);
            TMP_Text tag = Label(cell, info.shared ? "공유" : "개인", 16, TextAlignmentOptions.Center, info.shared ? new Color(0.6f, 0.8f, 1f) : new Color(1f, 0.6f, 0.9f));
            Stretch(tag.rectTransform); tag.rectTransform.offsetMax = new Vector2(0f, -56f);
            RectTransform clear = Box(cell, "Clear", new Color(0f, 0f, 0f, 0.78f));
            Stretch(clear);
            clear.GetComponent<Image>().raycastTarget = false;
            TMP_Text ct = Label(clear, "클리어", 26, TextAlignmentOptions.Center, Gold);
            ct.fontStyle = FontStyles.Bold; Stretch(ct.rectTransform);
            cellClear[i] = clear.gameObject;
            clear.gameObject.SetActive(false);
        }

        RectTransform detail = Box(wr, "Detail", new Color(0f, 0f, 0f, 0.45f));
        Place(detail, new Vector2(0f, 1f), new Vector2(24f, -294f), new Vector2(952f, 104f));
        detailText = Label(detail, "", 19, TextAlignmentOptions.TopLeft, new Color(0.9f, 0.92f, 0.96f));
        Stretch(detailText.rectTransform); detailText.rectTransform.offsetMin = new Vector2(12f, 6f); detailText.rectTransform.offsetMax = new Vector2(-12f, -6f);

        TMP_Text questHead = Label(wr, "개인 퀘스트  <size=18><color=#9AA4B5>(셋 다 깨고 클리어하면 1 베리 · 10라운드 시작 전까지 미완료 하나를 1회 변경)</color></size>", 24, TextAlignmentOptions.Left, new Color(0.95f, 0.38f, 0.65f));
        Place(questHead.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -410f), new Vector2(952f, 32f));

        for (int k = 0; k < 3; k++)
        {
            RectTransform row = Box(wr, "Quest" + k, new Color(0.12f, 0.1f, 0.16f, 1f));
            Place(row, new Vector2(0f, 1f), new Vector2(24f, -448f - k * 66f), new Vector2(952f, 58f));
            questName[k] = Label(row, "", 22, TextAlignmentOptions.MidlineLeft, Color.white);
            Place(questName[k].rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(190f, 50f));
            questDesc[k] = Label(row, "", 17, TextAlignmentOptions.MidlineLeft, new Color(0.8f, 0.82f, 0.88f));
            Place(questDesc[k].rectTransform, new Vector2(0f, 0.5f), new Vector2(206f, 0f), new Vector2(330f, 54f));
            questProg[k] = Label(row, "", 22, TextAlignmentOptions.Center, new Color(0.98f, 0.93f, 0.49f));
            Place(questProg[k].rectTransform, new Vector2(0f, 0.5f), new Vector2(540f, 0f), new Vector2(90f, 50f));
            questReward[k] = Label(row, "", 16, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.6f, 0.3f));
            Place(questReward[k].rectTransform, new Vector2(0f, 0.5f), new Vector2(636f, 0f), new Vector2(200f, 54f));

            int slot = k;
            RectTransform rb = Box(row, "Reroll", new Color(0.22f, 0.3f, 0.5f, 1f));
            Place(rb, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(96f, 40f));
            rb.gameObject.AddComponent<Button>().onClick.AddListener(() => S2Missions.RequestReroll(slot));
            TMP_Text rbt = Label(rb, "리롤", 20, TextAlignmentOptions.Center, Color.white); Stretch(rbt.rectTransform);
            rerollButton[k] = rb.gameObject;

            TMP_Text done = Label(row, "완료", 24, TextAlignmentOptions.Center, Gold);
            done.fontStyle = FontStyles.Bold;
            Place(done.rectTransform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(96f, 40f));
            questClear[k] = done.gameObject;
        }

        hiddenText = Label(wr, "", 20, TextAlignmentOptions.Left, new Color(1f, 0.84f, 0f));
        Place(hiddenText.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 16f), new Vector2(760f, 40f));

        RectTransform pb = Box(wr, "PoneStart", new Color(0.45f, 0.32f, 0.08f, 1f));
        Place(pb, new Vector2(1f, 0f), new Vector2(-176f, 12f), new Vector2(200f, 40f));
        pb.gameObject.AddComponent<Button>().onClick.AddListener(StartPone);
        TMP_Text pbt = Label(pb, "해독 시작", 22, TextAlignmentOptions.Center, Color.white); Stretch(pbt.rectTransform);
        poneButton = pb.gameObject;

        RectTransform close = Box(wr, "Close", new Color(0.25f, 0.2f, 0.12f, 1f));
        Place(close, new Vector2(1f, 0f), new Vector2(-20f, 12f), new Vector2(140f, 40f));
        close.gameObject.AddComponent<Button>().onClick.AddListener(Toggle);
        TMP_Text cl = Label(close, "닫기", 22, TextAlignmentOptions.Center, Gold); Stretch(cl.rectTransform);

        window.SetActive(false);
    }

    void Update()
    {
        if (!IsOpen) return;
        int h = -1;
        Vector2 mouse = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
        for (int i = 0; i < S2Missions.MissionCount; i++)
            if (cellRect[i] != null && RectTransformUtility.RectangleContainsScreenPoint(cellRect[i], mouse, null)) { h = i; break; }
        if (h != hovered) { hovered = h; ShowDetail(); }
        if (Time.unscaledTime >= nextRefresh) Refresh();
    }

    static void StartPone()
    {
        SelectionManager sm = FindFirstObjectByType<SelectionManager>();
        UnitIdentity unit = null;
        if (sm != null) foreach (Selectable s in sm.Selected) if (s != null && s.TryGetComponent(out unit)) break;
        S2Missions.RequestPone(unit);
    }

    void ShowDetail()
    {
        if (hovered < 0) { detailText.text = "<color=#9AA4B5>미션 칸에 마우스를 올리면 내용이 보입니다. 공유 미션 8개를 모두 깨면 히든 미션이 열립니다.</color>"; return; }
        S2Missions.MissionInfo m = S2Missions.Missions[hovered];
        detailText.text = $"<color=#FFD040><b>미션 :: {m.name}</b></color>\n{m.desc}\n<color=#FF8200>{m.reward}</color>\n<color=#9AA4B5>{(m.shared ? "모든 플레이어가 공유하는 미션입니다." : "개인이 달성하는 미션입니다.")}</color>";
    }

    void Refresh()
    {
        nextRefresh = Time.unscaledTime + 0.3f;
        int me = LocalPlayer.LocalPlayerId;
        S2Missions.ViewData v = S2Missions.View(me);
        berryText.text = $"베리 {Berry.Get(me)}";
        int shared = 0;
        for (int i = 0; i < S2Missions.MissionCount; i++)
        {
            bool done = S2Missions.Missions[i].shared ? (v.sharedMask & (1 << i)) != 0 : (v.personalMask & (1 << i)) != 0;
            if (S2Missions.Missions[i].shared && done) shared++;
            cellClear[i].SetActive(done);
        }
        bool canReroll = !v.rerollUsed && v.round <= S2Missions.RerollBeforeRound - 1;
        for (int k = 0; k < 3; k++)
        {
            sbyte id = v.questIds[k];
            if (id < 0 || id >= S2Missions.QuestCount)
            {
                questName[k].text = "<color=#7A7F8C>—</color>";
                questDesc[k].text = "<color=#7A7F8C>1라운드가 시작되면 배정됩니다.</color>";
                questProg[k].text = questReward[k].text = "";
                rerollButton[k].SetActive(false); questClear[k].SetActive(false);
                continue;
            }
            S2Missions.QuestInfo q = S2Missions.Quests[id];
            questName[k].text = q.name;
            questDesc[k].text = q.desc + (q.atRoundEnd ? "\n<size=14><color=#9AA4B5>라운드 종료후 조건 충족시 완료</color></size>" : "");
            questProg[k].text = v.questDone[k] ? "" : $"{Mathf.Min(v.questProgress[k], q.goal)} / {q.goal}";
            questReward[k].text = q.reward;
            questClear[k].SetActive(v.questDone[k]);
            rerollButton[k].SetActive(!v.questDone[k] && canReroll);
        }
        bool poneOpen = (v.flags & 1) != 0 && (v.flags & 2) == 0;
        poneButton.SetActive(poneOpen && v.poneLeft <= 0f);
        hiddenText.text = (v.flags & 2) != 0 ? "[히든미션] 포네그리프 해독 — 완료"
            : v.poneLeft > 0f ? $"[히든미션] 포네그리프 해독 중… {Mathf.CeilToInt(v.poneLeft)}초"
            : poneOpen ? $"[히든미션] 포네그리프 해독 — 내 전설적인 이상 유닛을 고르고 「해독 시작」({((v.flags & 4) != 0 ? 50 : 150)}초)"
            : $"<color=#9AA4B5>공유 미션 {shared} / {S2Missions.SharedTotal}</color>";
        if (hovered < 0) ShowDetail();
    }
}
