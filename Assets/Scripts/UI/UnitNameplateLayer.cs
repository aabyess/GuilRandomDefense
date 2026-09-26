using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 필드에 나와 있는 플레이어 유닛 머리 위 이름표(PM 지시 2026-09-23, 원작/원랜디 스타일).
// 조합표 인형이 아니라 UnitIdentity.Active(필드 등록부)를 돈다 — HealthBarLayer·
// SideBossBarLayer와 같은 자기완결형 Screen Space Overlay 캔버스 + 풀링 구조.
//
// 색은 UnitGrade.Color()(Assets/Scripts/Data/UnitData.cs)를 그대로 쓴다. 이 표는 이미
// Assets/Scripts 쪽 공용 코드다 — Assets/Editor/MapGenerator.cs의 GradeColor(grade)도
// grade.Color()를 그대로 위임할 뿐이라(에디터 전용 코드에 따로 정의돼 있지 않다) 여기서
// 새로 옮기거나 중복 정의할 게 없다.
public class UnitNameplateLayer : MonoBehaviour
{
    // HealthBarLayer(-100)보다는 위, GameHud(0)보다는 아래 — 플레이어 유닛은 체력바가 없어서
    // (UnitData.hp 주석 참고) 겹칠 대상이 없지만 관례를 맞춘다.
    const int SortingOrder = -95;

    [SerializeField] int maxLabels = 48;
    [SerializeField] float headHeightMargin = 6f;   // 머리 위로 띄우는 여유

    // 🔴 (09-24) 13 → 24. 사장님 「유닛 위에 이름이 안 보여」의 남은 몫이 **크기**였다.
    //    1920×1080 실측으로 글자 높이가 **11~12px**이었다 — 겨우 읽히는 수준이고,
    //    HUD 본문(팀 패널 20 · 보유 아이템 18)보다 작아서 「HUD는 읽히는데 이름표만 안 읽힌다」가 됐다.
    //    24면 같은 비율로 약 21~22px이 되어 HUD와 같은 급이 된다.
    //    ⚠️ 이건 캔버스 기준 크기라 창이 작아지면 HUD와 **같이** 줄어든다 — 화면 비율이 유지된다.
    //    고정 px로 박으면 창 크기가 바뀔 때 어긋난다(09-23·24에 같은 병을 여덟 번 봤다).
    [SerializeField] float fontSize = 24f;
    [SerializeField] Vector2 labelSize = new Vector2(220f, 34f);

    // 거리 컬링 — 화면이 유닛으로 뒤덮이는 걸 막는다(PM 지시: "멀면 작아지거나 사라지게").
    //
    // 🔴 (09-23) 세계 거리 고정값(35·60)이었다. **그래서 이 기능은 한 번도 화면에 보인 적이 없다.**
    //    옛 맵에서도 카메라 기본 거리가 68이라 60을 이미 넘었고, 맵이 4.167배가 된 뒤로는
    //    기본 거리가 283이라 아예 말이 안 됐다. 실제 게임 화면에서 확인한 적이 없어 몰랐다.
    //
    //    고정 거리는 맵 배율·유닛 크기가 바뀌면 조용히 어긋난다(09-23에만 같은 병을 일곱 번 봤다).
    //    라벨이 쓸모 있는 건 「유닛이 화면에서 알아볼 만할 때」이고, 그건 절대 거리가 아니라
    //    **카메라 높이 대비 거리**로 정해진다. 그래서 배수로 바꿨다 — 줌을 당기면 뜨고,
    //    빼면 사라진다. 맵을 또 키워도 따라온다.
    //    (기본값은 카메라 기본 높이 216·거리 283 기준으로 잡았다: 283 ÷ 216 = 1.31이므로
    //     1.6에서 흐려지기 시작해 2.6에서 사라진다 = 기본 줌에서는 또렷하게 보인다.)
    [SerializeField] float fadeStartHeights = 1.6f;
    [SerializeField] float maxHeights = 2.6f;

    // 유닛이 서는 섬 윗면 높이. MapLayout.IslandTop(에디터 전용 상수)과 같은 값이다 —
    // 런타임에서 그 상수를 못 보므로 여기 적는다. 섬 높이를 바꾸면 여기도 같이 바꿀 것.
    const float GroundY = 8f;

    class Label
    {
        public RectTransform root;
        public TMP_Text text;
    }

    Camera cam;
    RectTransform canvasRect;
    readonly List<Label> pool = new List<Label>();

    // 겹쳐 선 같은 유닛 묶음(2026-09-26 사장님 「완전 겹치게 하니까 흔함 유닛들이 몇 개 있는지 모르겠음」 → 「둘 다 ㄱㄱ」).
    //    흔함은 자기 칸 한 점에 포개 서서(원작도 충돌 0) 이름표 N장이 한 자리에 겹쳤다 — 같은 종류·같은 주인·같은 자리(StackCell 칸)면
    //    이름표를 하나만 그리고 「강주혁 ×3」으로 개수를 붙인다. 원작엔 없는 표시다(원작은 더블클릭 선택으로 셌다 — SelectionManager).
    const float StackCell = 6f;
    readonly Dictionary<(UnitData, int, int, int), int> stackIndex = new Dictionary<(UnitData, int, int, int), int>();
    readonly List<(UnitIdentity rep, int count)> stacks = new List<(UnitIdentity, int)>();

    void Awake()
    {
        cam = Camera.main;
        BuildCanvas();
    }

    void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("UnitNameplateCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        canvasRect = (RectTransform)canvasObject.transform;
    }

    // 화면에 실제로 보이는 만큼만, 필요해질 때 만든다(HealthBarLayer와 같은 이유).
    Label GetOrCreateLabel(int index)
    {
        while (pool.Count <= index)
            pool.Add(CreateLabel());

        return pool[index];
    }

    Label CreateLabel()
    {
        GameObject root = new GameObject("Nameplate", typeof(RectTransform));
        root.transform.SetParent(canvasRect, false);

        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.sizeDelta = labelSize;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rootRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        // 3D 위에 뜨는 글자라 TMP(SDF) 효과가 특히 크다 — 카메라 줌에 따라 크기가 계속 바뀌는데
        // 레거시 Text는 구워둔 비트맵을 늘려서 번졌다(2026-09-23 TMP 전환).
        if (GameHud.UiFontAsset != null) text.font = GameHud.UiFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Bottom;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        // 맨눈에 잘 읽히도록 검정 외곽선. TMP는 머티리얼이 외곽선을 직접 지원해서
        // 레거시 Outline 컴포넌트(정점을 네 벌 더 그리던 방식)보다 싸고 깨끗하다.
        //
        // 🔴 (09-24) 0.2 → 0.28, 알파 220 → 255. **대비가 크기만큼 중요하다.**
        //    등급 색은 중간 명도(흔함=초록 등)인데 바닥이 풀밭(올리브)·우리 모래(베이지)·
        //    바다로 계속 바뀌어서, 글자를 키우기만 하면 밝은 바닥에서 또 묻힌다.
        //    외곽선이 배경과 글자 사이에 어두운 테를 만들어 어떤 바닥에서도 떨어져 보이게 한다.
        text.outlineWidth = 0.28f;
        text.outlineColor = new Color32(0, 0, 0, 255);

        root.SetActive(false);
        return new Label { root = rootRect, text = text };
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].root.gameObject.activeSelf)
                pool[i].root.gameObject.SetActive(false);
        }

        int used = 0;
        Vector3 camPos = cam.transform.position;

        // 문턱을 **카메라 높이에 비례**해서 매 프레임 다시 잡는다(위 🔴 주석 참고).
        // 유닛이 서는 땅 높이(MapLayout.IslandTop = 8)를 빼서 「지면에서 얼마나 떠 있나」로 센다 —
        // 섬 높이가 또 바뀌어도 값이 안 어긋난다. 바닥에 붙어도 1 밑으로는 안 내려가게 막는다.
        float camAboveGround = Mathf.Max(1f, camPos.y - GroundY);
        float maxDistance = camAboveGround * maxHeights;
        float fadeStartDistance = camAboveGround * fadeStartHeights;

        stackIndex.Clear();
        stacks.Clear();
        foreach (UnitIdentity identity in UnitIdentity.Active)
        {
            if (identity == null || identity.Data == null) continue;
            Vector3 p = identity.transform.position;
            int owner = identity.TryGetComponent(out OwnedByPlayer ownedBy) ? ownedBy.OwnerId : -1;
            var key = (identity.Data, owner, Mathf.RoundToInt(p.x / StackCell), Mathf.RoundToInt(p.z / StackCell));
            if (stackIndex.TryGetValue(key, out int at)) stacks[at] = (stacks[at].rep, stacks[at].count + 1);
            else { stackIndex[key] = stacks.Count; stacks.Add((identity, 1)); }
        }

        foreach ((UnitIdentity identity, int count) in stacks)
        {
            if (used >= maxLabels) break;
            UnitData data = identity.Data;

            float distance = Vector3.Distance(camPos, identity.transform.position);
            if (distance >= maxDistance) continue;

            Vector3 worldPos = identity.transform.position + Vector3.up * (HeadHeight(identity) + headHeightMargin);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0f) continue;
            if (screenPos.x < 0f || screenPos.x > Screen.width || screenPos.y < 0f || screenPos.y > Screen.height) continue;

            Label label = GetOrCreateLabel(used);
            label.root.gameObject.SetActive(true);
            label.root.position = new Vector3(screenPos.x, screenPos.y, 0f);

            string caption = count > 1 ? $"{data.unitName} ×{count}" : data.unitName;
            if (label.text.text != caption) label.text.text = caption;

            Color gradeColor = data.grade.Color();
            float alpha = distance <= fadeStartDistance
                ? 1f
                : Mathf.Clamp01(1f - (distance - fadeStartDistance) / Mathf.Max(0.01f, maxDistance - fadeStartDistance));
            gradeColor.a = alpha;
            label.text.color = gradeColor;

            used++;
        }
    }

    // 🔴 (09-24) **그리는 것(Renderer)을 먼저 본다.** HealthBarLayer·SideBossBarLayer는
    //    콜라이더를 먼저 보는데, 이름표에서는 그게 틀린 답을 낸다:
    //    `ArtBinder.FitToHeight`가 캡슐 콜라이더를 **유닛 키(30)로 통일**해 세우기 때문에,
    //    네발짐승처럼 실제로 낮은 모델은 콜라이더가 몸보다 한참 높다.
    //    안흔함_강재규(재규어)는 몸이 13.8인데 콜라이더가 30이라 이름표가 **머리 위 16쯤에
    //    떠 있었다**(09-24 플레이 캡처에서 눈에 띄게 어긋났다).
    //    그리는 것의 경계를 쓰면 키가 어떻든 늘 머리 바로 위에 붙는다.
    //    ⚠️ 체력바는 안 건드린다 — 적은 자리표시 큐브라 콜라이더와 경계가 같고,
    //       거기서는 지금 계산이 맞게 동작한다. 고칠 이유가 없는 것을 같이 고치지 않는다.
    static float HeadHeight(UnitIdentity identity)
    {
        Renderer tallest = null;
        float top = float.NegativeInfinity;
        foreach (Renderer renderer in identity.GetComponentsInChildren<Renderer>())
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
            if (!renderer.enabled) continue;
            if (renderer.bounds.max.y <= top) continue;
            top = renderer.bounds.max.y;
            tallest = renderer;
        }

        if (tallest != null) return top - identity.transform.position.y;

        if (identity.TryGetComponent(out Collider body))
            return body.bounds.max.y - identity.transform.position.y;

        return 2f;
    }
}
