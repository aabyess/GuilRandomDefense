using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상위 등급(초월·불멸·영원) 획득 컷인 — 2.6초 전체화면 오버레이(사장님 C안, PM 10-08 「레이어 PNG + UI 애니」).
///
/// 영상(mp4)이 아니라 blender가 뽑은 1920×1080 투명 레이어 PNG를 겹쳐 UI 애니로 움직인다.
/// 이름·칭호·캐릭터는 레이어 PNG 안에 구워져 있다(유닛마다 char·shadow·title·band_name 정도가 다르고, 나머지는 같은 모양).
/// 시간표(blender C_*_timing.txt): 등장 1.0s · 유지 1.2s · 퇴장 0.4s = 2.6s @30fps · 효과음 슉(캐릭터 미끄러짐) 0.25s · 칭(흰 띠) 0.83s · 퇴장 2.20s.
///
/// 파일 자리(Tools/cutin/import_cutin.py가 만든다): Assets/Resources/Cutin/c_&lt;이름 SHA1 앞 8자리&gt;/{char,shadow,title,band_name,band_gray}.png + layout.json,
///   Cutin/_common/&lt;transcend|immortal|eternal&gt;/{halftone,ring,streak,stripes,band}.png + layout.json(flat 색 포함). 각 PNG는 투명 여백을 잘라 낸 작은 그림이고 layout.json의 x,y,w,h(1920×1080 화면 픽셀)에 놓는다.
///   텍스처는 압축 임포트(Crunch 권장)로 둘 것 — 용량 계산은 import_cutin.py 출력 참고.
/// 레이어를 하나도 못 찾으면(아직 산출 반입 전) 조용히 건너뛴다. 켜기: <see cref="Enabled"/>.
///
/// 순서(아래→위, 시안 12컷 시트 기준 추정): flat(등급색 단색) · halftone(점무늬) · ring(동심원) · streak(속도선) · stripes(왼쪽 아래 줄무늬)
///   · shadow(캐릭터 그림자) · char(캐릭터) · band_gray(띠 그림자) · band(흰 띠) · band_name(띠 위 이름) · title(칭호). 움직임 식은 Evaluate(blender gen_cutin_comp.py frame_C 이식).
/// 알려진 한계(스켈레톤): ①시안과 눈으로 대조 전 ②MP — 지금은 이 PC에서 OnAcquired를 받은 쪽(싱글·호스트)만. 친구 화면은 SummonVoice처럼
///   호스트가 유닛 번호를 RPC로 넘기는 길이 더 필요하다(NetGameState.RouteSummonVoice 참고) ③획득이 몰리면 큐로 한 편씩.
/// </summary>
public class CutinOverlay : MonoBehaviour
{
    /// <summary>켬(사장님 승인 10-08 「획득 순간 2.6초 전체화면」). 산출물 폴더가 없는 유닛은 건너뛴다. 효과음 파일은 사장님이 고를 때까지 없어 무음.</summary>
    public static bool Enabled = true;
    /// <summary>촬영 전용 배속(1 = 실시간). 프레임 사진을 촘촘히 찍으려고 탐침이 낮춘다.</summary>
    public static float DebugSpeed = 1f;

    public const float TotalSeconds = 2.6f, InSeconds = 1.0f, HoldSeconds = 1.2f, OutSeconds = 0.4f;
    public const float WhooshAt = 0.25f, TingAt = 0.83f, ExitSoundAt = 2.20f;
    const string Folder = "Cutin/";

    // 움직임 정본 = blender Tools/blender/gen_cutin_comp.py frame_C(12fps 프레임 f = t×12). 아래 Evaluate가 그 식을 옮긴 것이다.
    static readonly string[] Order = { "halftone", "ring", "stripes", "shadow", "char", "streak", "band_gray", "band_name", "title" };   // flat은 따로(전체 덮개)
    static readonly Vector2 FrameCenter = new Vector2(960f, 540f), RingCenter = new Vector2(1420f, 420f);   // 1920×1080 기준

    static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t * t; }

    [System.Serializable] class LayoutEntry { public string name, file; public int x, y, w, h; }
    [System.Serializable] class LayoutFile { public string grade; public int[] flat; public LayoutEntry[] layers; }

    class Layer { public RawImage image; public string name; public Vector2 pivot; }   // pivot = 이 레이어가 회전·확대하는 화면 점(1920×1080, 왼쪽 위 원점)

    static CutinOverlay instance;
    readonly List<Layer> layers = new List<Layer>();
    readonly List<GameObject> spawned = new List<GameObject>();
    readonly Queue<string> queue = new Queue<string>();
    GameObject root;
    Image flatImage;
    float clock = -1f;   // -1 = 쉬는 중
    bool whooshed, tinged, exited;

    /// <summary>유닛 에셋 이름(UnitData.name)으로 컷인 한 편을 건다. 이미 돌고 있으면 줄을 선다. 산출물이 없으면 false.</summary>
    public static bool Play(string unitAssetName)
    {
        if (!Enabled || string.IsNullOrEmpty(unitAssetName)) return false;
        if (Resources.Load<TextAsset>(Folder + UnitKey(unitAssetName) + "/layout") == null) return false;
        Ensure().queue.Enqueue(unitAssetName);
        return true;
    }

    // 폴더 이름은 ASCII 키 — 한글 폴더는 맥(NFD)·윈도(NFC)에서 Resources.Load 결과가 갈린다(10-08 실측). Tools/cutin/import_cutin.py unit_key와 같은 식.
    static string UnitKey(string unitAssetName)
    {
        byte[] hash;
        using (var sha1 = System.Security.Cryptography.SHA1.Create()) hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(unitAssetName.Normalize(System.Text.NormalizationForm.FormC)));
        var sb = new System.Text.StringBuilder("c_");
        for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }

    static string GradeKey(string grade)
    {
        switch (grade) { case "초월": return "transcend"; case "불멸": return "immortal"; case "영원": return "eternal"; default: return grade; }
    }

    static CutinOverlay Ensure()
    {
        if (instance != null) return instance;
        GameObject host = new GameObject("[CutinOverlay]");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<CutinOverlay>();
        instance.Build();
        return instance;
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;   // HUD·메뉴보다 위
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        GameObject flat = new GameObject("flat", typeof(RectTransform), typeof(Image));
        flat.transform.SetParent(root.transform, false);
        RectTransform fr = (RectTransform)flat.transform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
        flatImage = flat.GetComponent<Image>();
        flatImage.raycastTarget = false;
        root.SetActive(false);
    }

    void Update()
    {
        if (clock < 0f)
        {
            if (queue.Count == 0) { if (GamePause.CutinHold) GamePause.SetCutinHold(false); return; }   // 줄이 비어야 게임 시간을 푼다(큐로 이어지면 정지도 이어진다)
            Begin(queue.Dequeue());
            return;
        }
        clock += Time.unscaledDeltaTime * DebugSpeed;
        if (clock >= TotalSeconds) { End(); return; }

        if (!whooshed && clock >= WhooshAt) { whooshed = true; GameSound.Play(GameSoundId.CutinWhoosh); }
        if (!tinged && clock >= TingAt) { tinged = true; GameSound.Play(GameSoundId.CutinTing); }
        if (!exited && clock >= ExitSoundAt) { exited = true; GameSound.Play(GameSoundId.CutinWhoosh); }

        Evaluate(clock);
    }

    // 한 레이어를 놓는다: dx,dy = 화면 픽셀 이동(오른쪽·아래 +), s = pivot 기준 확대, rot = pivot 기준 반시계 도, a = 알파.
    void Put(Layer layer, float dx, float dy, float s, float rot, float a)
    {
        bool show = a > 0.001f;
        if (layer.image.gameObject.activeSelf != show) layer.image.gameObject.SetActive(show);
        if (!show) return;
        RectTransform r = layer.image.rectTransform;
        r.anchoredPosition = new Vector2(layer.pivot.x + dx, -(layer.pivot.y + dy));
        r.localScale = new Vector3(s, s, 1f);
        r.localRotation = Quaternion.Euler(0f, 0f, rot);
        Color c = Color.white; c.a = Mathf.Clamp01(a);
        layer.image.color = c;
    }

    void Evaluate(float t)
    {
        float f = t * 12f;
        float tOut = t - (InSeconds + HoldSeconds);
        float cover = tOut < 0f ? Mathf.Clamp01((f - 0.6f) / 1.4f) : 1f - EaseIn((tOut - 0.22f) / 0.18f);
        Color fc = flatImage.color; fc.a = Mathf.Clamp01(cover); flatImage.color = fc;

        float shP = EaseOut((f - 5f) / 2.5f);
        float outFade = tOut < 0f ? 1f : 1f - EaseIn(tOut / 0.3f);
        float rp = EaseOut((f - 6.5f) / 2f);
        float sp = EaseOut((f - 7f) / 2f);
        float so = tOut < 0f ? 0f : EaseIn((tOut - 0.08f) / 0.2f);
        float rise = EaseOut((f - 1.6f) / 1.2f);
        float slide = EaseOut((f - 3f) / 1.6f);
        float cxOff = -560f * slide, cyOff = 260f * (1f - rise);
        float chA = Mathf.Min(1f, rise * 1.5f) * (tOut < 0f ? 1f : 1f - EaseIn((tOut - 0.25f) / 0.15f));
        float bp = EaseOut((f - 9.6f) / 1f);
        float bo = tOut < 0f ? 0f : EaseIn(tOut / 0.22f);
        float bx = 500f * (1f - bp) + 1500f * bo;
        bool bandOn = bp > 0f && bo < 1f;
        string bandKey = tOut < 0f ? (f < 11f ? "band_gray" : "band_name") : (tOut > 0.05f ? "band_gray" : "band_name");

        foreach (Layer layer in layers)
        {
            switch (layer.name)
            {
                case "halftone": Put(layer, 0f, 0f, 1f, 0f, shP > 0f ? shP * outFade : 0f); break;
                case "ring": Put(layer, 0f, 0f, 0.55f + 0.45f * rp, 35f * (1f - rp) + 2f * t, rp > 0f ? rp * outFade : 0f); break;
                case "stripes": Put(layer, -1100f * (1f - sp) - 1100f * so, 0f, 1f, 0f, sp > 0f && so < 1f ? 1f : 0f); break;
                case "shadow": Put(layer, cxOff + 10f, cyOff + 10f, 1f + 0.25f * shP, 0f, shP > 0f ? shP * chA : 0f); break;
                case "char": Put(layer, cxOff, cyOff, 1f, 0f, chA); break;
                case "streak": Put(layer, (f - 8.6f) * 600f - 500f, 0f, 1f, 0f, f >= 8.6f && f < 10.2f ? 1f : 0f); break;
                case "band_gray": Put(layer, bx, 0f, 1f, 0f, bandOn && bandKey == "band_gray" ? 1f : 0f); break;
                case "band_name": Put(layer, bx, 0f, 1f, 0f, bandOn && bandKey == "band_name" ? 1f : 0f); break;
                case "title": Put(layer, bx, 0f, 1f, 0f, bandOn && f >= 11f && tOut < 0.05f ? 1f : 0f); break;
                case "band": Put(layer, 0f, 0f, 1f, 0f, 0f); break;   // blender frame_C는 band 대신 band_gray·band_name(띠 모양 포함)을 쓴다
            }
        }
    }

    // 반입 형식: Resources/Cutin/<유닛>/layout.json(유닛별 레이어) + Resources/Cutin/_common/<등급>/layout.json(등급 공통 레이어 + flat 색). Tools/cutin/import_cutin.py가 만든다.
    void Begin(string unitAssetName)
    {
        foreach (GameObject go in spawned) if (go != null) Destroy(go);
        spawned.Clear(); layers.Clear();
        string grade = unitAssetName.Split('_')[0];
        LayoutFile common = LoadLayout(Folder + "_common/" + GradeKey(grade));
        LayoutFile unit = LoadLayout(Folder + UnitKey(unitAssetName));
        if (unit == null) return;
        Color flat = common != null && common.flat != null && common.flat.Length >= 3 ? new Color32((byte)common.flat[0], (byte)common.flat[1], (byte)common.flat[2], 255) : new Color(0.2f, 0.8f, 0.75f);
        flatImage.color = new Color(flat.r, flat.g, flat.b, 0f);
        var entries = new List<(string folder, LayoutEntry entry)>();
        if (common != null && common.layers != null) foreach (LayoutEntry e in common.layers) entries.Add((Folder + "_common/" + GradeKey(grade), e));
        if (unit.layers != null) foreach (LayoutEntry e in unit.layers) entries.Add((Folder + UnitKey(unitAssetName), e));
        entries.Sort((a, b) => System.Array.IndexOf(Order, a.entry.name).CompareTo(System.Array.IndexOf(Order, b.entry.name)));
        foreach ((string folder, LayoutEntry entry) in entries)
        {
            Texture2D texture = Resources.Load<Texture2D>(folder + "/" + entry.file);
            if (texture == null) continue;
            GameObject go = new GameObject(entry.name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(root.transform, false);
            RectTransform r = (RectTransform)go.transform;
            // 앵커 = 화면 왼쪽 위, 피벗 = 이 레이어가 도는 점(그림 사각형 안 비율). anchoredPosition은 피벗의 화면 좌표(Put이 채운다).
            Vector2 pivotPoint = entry.name == "ring" ? RingCenter : FrameCenter;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(entry.w, entry.h);
            r.pivot = new Vector2((pivotPoint.x - entry.x) / entry.w, 1f - (pivotPoint.y - entry.y) / entry.h);
            RawImage image = go.GetComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            go.SetActive(false);
            spawned.Add(go);
            layers.Add(new Layer { image = image, name = entry.name, pivot = pivotPoint });
        }
        if (layers.Count == 0) return;
        clock = 0f; whooshed = tinged = exited = false;
        root.SetActive(true);
        GamePause.SetCutinHold(true);   // 컷인 동안 게임 시간 정지(혼자 하기만) — 시계는 unscaledDeltaTime이라 계속 돈다. 소리는 안 멈춘다
    }

    static LayoutFile LoadLayout(string path)
    {
        TextAsset asset = Resources.Load<TextAsset>(path + "/layout");
        return asset != null ? JsonUtility.FromJson<LayoutFile>(asset.text) : null;
    }

    void End()
    {
        clock = -1f;   // 정지는 여기서 안 푼다 — Update가 줄이 비었을 때 푼다
        root.SetActive(false);
        foreach (GameObject go in spawned) if (go != null) Destroy(go);
        spawned.Clear(); layers.Clear();
        Resources.UnloadUnusedAssets();   // 이번 편 텍스처를 쥐고 있지 않게
    }

    // ── 획득 연결: 상위 등급 유닛이 처음 주인을 얻을 때(UnitIdentity.OnAcquired — 포탈·도박·조합·보상 모든 획득 길) ──
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        UnitIdentity.OnAcquired -= HandleAcquired;
        UnitIdentity.OnAcquired += HandleAcquired;
    }

    static void HandleAcquired(UnitIdentity unit, UnitInventory inventory)
    {
        if (unit == null || unit.Data == null) return;
        UnitGrade grade = unit.Data.grade;
        if (grade != UnitGrade.Transcendent && grade != UnitGrade.Immortal && grade != UnitGrade.Eternal) return;
        string name = unit.Data.name.Normalize(System.Text.NormalizationForm.FormC);
        // 같이 하기(사장님 10-08 확정: 누가 얻어도 모든 플레이어에게 컷인 + 맵 정지): 획득은 호스트만 알아챈다 → 호스트가 Play하고 전원에게 이름을 보낸다.
        if (GamePause.IsNetworked)
        {
            if (!GameAuthority.IsServer) return;
            if (Play(name)) NetGameState.Instance?.BroadcastCutin(name);
            return;
        }
        // 혼자 하기: 내 유닛만.
        if (unit.TryGetComponent(out OwnedByPlayer owner) && owner.OwnerId != LocalPlayer.LocalPlayerId) return;
        Play(name);
    }
}
