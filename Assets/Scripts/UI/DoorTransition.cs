using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 선술집 문 전환(사장님 10-06 「혼자 하기·방 만들기·시작 때 선술집 문이 열리듯, 되돌아올 때는 반대로 닫히게」).
/// 닫힘(쿵) → 할 일(씬 전환·방 만들기) → 준비되면 열림(삐걱) 순서. 문은 화면을 반씩 덮는 두 짝(blender 그림 Resources/UI/Door,
/// 효과음은 Kenney CC0 Resources/Sfx/door). 열릴 때는 경첩(화면 바깥쪽 끝)을 축으로 안쪽으로 젖혀지는 것을 가로 축소+어두워짐으로 흉내 낸다.
/// 씬을 넘어 산다(DontDestroyOnLoad). 로딩 화면(정렬 32000)이 문(31000) 위에 뜨므로, 로딩이 걷힌 뒤에 닫힌 문이 보이고 그때 열린다.
/// </summary>
public class DoorTransition : MonoBehaviour
{
    const float CloseSeconds = 0.45f, OpenSeconds = 0.95f, HoldAfterReady = 0.2f, NoSceneWait = 0.35f;
    const int SortingOrder = 31000;

    static DoorTransition active;
    public static bool Busy => active != null;

    RectTransform left, right;
    RawImage leftImage, rightImage, gap;
    AudioSource audioSource;
    enum Phase { Closing, Waiting, Opening }
    Phase phase;
    float t;
    Action after;
    bool expectScene, sceneArrived;
    float readyAt = -1f;

    /// <summary>문을 닫고 다 닫히면 after를 부른다. expectScene이면 다음 씬이 올라오고(로딩 화면까지 걷힌 뒤) 열린다, 아니면 잠깐 뒤 열린다.</summary>
    public static void CloseThen(Action after, bool expectScene)
    {
        if (active != null) { after?.Invoke(); return; }   // 이미 전환 중이면 문은 그대로 두고 일만
        active = Create();
        active.after = after;
        active.expectScene = expectScene;
        active.Begin(CloseSeconds);
    }

    /// <summary>바깥(네트 씬 전환 등)이 이미 씬을 바꾸는 중일 때 — 문을 빨리 닫아 두고 씬이 오면 연다.</summary>
    public static void SlamForSceneChange()
    {
        if (active != null) { active.expectScene = true; return; }
        active = Create();
        active.expectScene = true;
        active.Begin(0.18f);
    }

    static DoorTransition Create()
    {
        var go = new GameObject("[DoorTransition]");
        DontDestroyOnLoad(go);
        return go.AddComponent<DoorTransition>();
    }

    float closeDuration;
    void Begin(float duration)
    {
        closeDuration = duration;
        phase = Phase.Closing;
        t = 0f;
        Play("Sfx/door/close_doorClose_1", 0.55f);
    }

    void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;   // 세로 기준 — 두 짝이 늘 화면 높이를 다 덮는다
        gameObject.AddComponent<GraphicRaycaster>();   // 전환 중 클릭 막기

        var block = new GameObject("Block", typeof(RectTransform), typeof(Image));
        block.transform.SetParent(transform, false);
        var br = (RectTransform)block.transform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
        block.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        leftImage = Leaf("Left", "UI/Door/door_left", new Vector2(0f, 0.5f), out left);
        rightImage = Leaf("Right", "UI/Door/door_right", new Vector2(1f, 0.5f), out right);

        var g = new GameObject("GapLight", typeof(RectTransform), typeof(RawImage));
        g.transform.SetParent(transform, false);
        var gr = (RectTransform)g.transform; gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one; gr.offsetMin = gr.offsetMax = Vector2.zero;
        gap = g.GetComponent<RawImage>();
        gap.texture = Resources.Load<Texture2D>("UI/Door/door_gap_light");
        gap.raycastTarget = false;
        gap.color = new Color(1f, 0.85f, 0.6f, 0f);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (active == this) active = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => sceneArrived = true;

    // 한 짝: 화면 절반(바깥 끝이 경첩). 그림 가로세로비(1280×1440 ≈ 0.889)로 반 화면(960×1080)을 덮게 높이 기준으로 맞춘다.
    RawImage Leaf(string name, string path, Vector2 hinge, out RectTransform rect)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = hinge;
        rect.pivot = hinge;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 1080f);
        var img = go.GetComponent<RawImage>();
        img.texture = Resources.Load<Texture2D>(path);
        img.raycastTarget = false;
        return img;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // 씬 로드 중 긴 프레임에 애니가 건너뛰지 않게
        t += dt;
        // 화면 가로 절반(캔버스 단위) — 세로 1080 기준이라 넓은 화면이면 절반이 960보다 크다
        float half = ((RectTransform)transform).rect.width * 0.5f + 2f;

        switch (phase)
        {
            case Phase.Closing:
            {
                float k = Mathf.Clamp01(t / closeDuration);
                float e = k * k;                                    // 가속하며 닫힌다(쾅)
                SetOpenAmount(1f - e, half);
                if (k >= 1f)
                {
                    Play("Sfx/door/close_thud_impactWood_heavy_001", 0.6f);
                    phase = Phase.Waiting;
                    t = 0f;
                    Action a = after; after = null;
                    try { a?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
                }
                break;
            }
            case Phase.Waiting:
            {
                SetOpenAmount(0f, half);
                bool ready = expectScene ? sceneArrived && !LoadingScreen.IsShowing : t >= NoSceneWait;
                if (ready && readyAt < 0f) readyAt = t;
                if (readyAt >= 0f && t - readyAt >= HoldAfterReady)
                {
                    phase = Phase.Opening;
                    t = 0f;
                    Play("Sfx/door/open_doorOpen_1", 0.6f);
                }
                if (t > 30f) Destroy(gameObject);   // 안전장치 — 무엇이 막혀도 문이 영영 남지 않게
                break;
            }
            case Phase.Opening:
            {
                float k = Mathf.Clamp01(t / OpenSeconds);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);   // 확 열리다 천천히 멈춤
                SetOpenAmount(e, half);
                gap.color = new Color(1f, 0.85f, 0.6f, Mathf.Sin(Mathf.Clamp01(k * 2.2f) * Mathf.PI) * 0.9f);   // 틈이 벌어질 때 불빛
                if (k >= 1f) Destroy(gameObject);
                break;
            }
        }
    }

    // 0 = 닫힘(두 짝이 가운데서 만남), 1 = 다 열림(경첩 쪽으로 젖혀져 가로 0). 젖혀질수록 어둡게·살짝 커지게(안쪽으로 다가오는 원근).
    void SetOpenAmount(float open, float half)
    {
        float width = half * Mathf.Cos(open * Mathf.PI * 0.5f);
        float shade = Mathf.Lerp(1f, 0.35f, open);
        float grow = 1f + 0.08f * Mathf.Sin(open * Mathf.PI);
        foreach ((RectTransform r, RawImage img) in new[] { (left, leftImage), (right, rightImage) })
        {
            r.sizeDelta = new Vector2(width, 1080f * grow);
            img.color = new Color(shade, shade, shade, 1f);
            r.gameObject.SetActive(width > 0.5f);
        }
    }

    void Play(string path, float volume)
    {
        if (!GameSound.Enabled) return;
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip != null) audioSource.PlayOneShot(clip, volume * AudioPrefs.SfxVolume);
    }
}
