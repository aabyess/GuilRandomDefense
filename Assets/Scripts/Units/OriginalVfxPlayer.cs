using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 원작(워크3 MDX) 이펙트 모델 재생기(10-08, blender 변환 FBX): 뼈 애니 클립을 재생하고, 층(레이어)마다
/// 알파 곡선(선형 보간)과 UV 오프셋(계단)을 머티리얼 속성 블록으로 입힌다. 입자(PRE2)는 자식 ParticleSystem(Pre2Driver)이 맡는다.
/// 데이터(Layer)는 에디터 빌더(OriginalVfxBuilder)가 json에서 직렬화해 둔다. SkillVfx.PlayPrefab이 Restart()로 다시 쓴다.
/// 연출(SkillCinematic)은 시퀀스를 이름으로 고른다(Play("birth"/"stand"/"death", speed, loop)) — 알파 곡선은 MDX 전체 시간(글로벌) 곡선을 시퀀스 start만큼 밀어 읽는다.
/// </summary>
public class OriginalVfxPlayer : MonoBehaviour
{
    [Serializable]
    public class Layer
    {
        public Renderer renderer;
        public float staticAlpha = 1f;
        public float[] alphaTimes = new float[0];     // 첫 클립 기준 초
        public float[] alphaValues = new float[0];
        public float[] uvTimes = new float[0];        // 계단: 키 시각 이후 그 값을 유지
        public Vector2[] uvOffsets = new Vector2[0];  // (u, Unity offsetV = -v)
        // 연출용(10-09): MDX 전체 시간 알파 곡선(시퀀스 start를 더해 읽는다). 첫 키보다 앞 시각은 정적 알파(blender 규칙).
        public float[] globalAlphaTimes = new float[0];
        public float[] globalAlphaValues = new float[0];
    }

    [Serializable]
    public class ClipEntry
    {
        public string name;          // s0_Birth 같은 시퀀스 이름
        public AnimationClip clip;
        public float startSec;       // MDX 전체 시간에서 이 시퀀스 시작(초)
        public float lengthSec;
        public bool looping;
    }

    public Animator animator;
    public AnimationClip clip;
    public float duration = 1f;
    public bool loop;
    public Layer[] layers = new Layer[0];
    public ClipEntry[] clips = new ClipEntry[0];   // 비어 있으면 clip 하나(기존 반입 이펙트)

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    PlayableGraph graph;
    AnimationClipPlayable playable;
    AnimationPlayableOutput output;
    MaterialPropertyBlock block;
    Color[] baseColors;
    float time;
    bool playing;
    int clipIndex = -1;
    float speed = 1f;
    float runDuration;
    bool runLoop;

    public bool IsPlaying => playing;
    /// <summary>지금 재생 중인 시퀀스의 MDX 전체 시간 start(초). 입자 키 시각 기준.</summary>
    public float CurrentStartSec => clipIndex >= 0 && clips.Length > 0 ? clips[clipIndex].startSec : 0f;
    public bool HasClip(string kind)
    {
        for (int i = 0; i < clips.Length; i++) if (clips[i].name != null && clips[i].name.IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
    public float Speed { get => speed; set => speed = value; }
    /// <summary>재생이 끝나 스스로 꺼졌는가(연출이 반환 여부를 본다).</summary>
    public bool Finished { get; private set; }

    void Awake() => Init();

    void Init()
    {
        if (block != null) return;
        block = new MaterialPropertyBlock();
        baseColors = new Color[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            Renderer r = layers[i].renderer;
            if (r == null) continue;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;   // 뼈가 움직이면 경계가 안 맞아 잘린다
            Color c = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId) ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
            baseColors[i] = c;
        }
        if (animator != null && (clip != null || clips.Length > 0))
        {
            graph = PlayableGraph.Create("OriginalVfx");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "out", animator);
            SelectClip(clips.Length > 0 ? 0 : -1);
        }
    }

    void SelectClip(int index)
    {
        AnimationClip c = index >= 0 ? clips[index].clip : clip;
        if (c == null || !graph.IsValid()) { clipIndex = index; return; }
        if (playable.IsValid()) playable.Destroy();
        playable = AnimationClipPlayable.Create(graph, c);
        playable.SetApplyFootIK(false);
        output.SetSourcePlayable(playable);
        clipIndex = index;
    }

    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

    /// <summary>처음부터 다시 재생(첫 시퀀스, 한 번). 풀에서 꺼낸 사본이 부른다.</summary>
    public void Restart()
    {
        Init();
        if (clips.Length > 0) SelectClip(0);
        time = 0f; speed = 1f;
        runDuration = clips.Length > 0 ? clips[0].lengthSec : duration;
        runLoop = loop;
        playing = true; Finished = false;
        gameObject.SetActive(true);
        Apply(0f);
    }

    /// <summary>시퀀스를 이름 조각으로 골라 재생(연출). kind: birth·stand·death(대소문자 무시, 없으면 첫 시퀀스). stand는 보통 반복.</summary>
    public void Play(string kind, float speedMultiplier = 1f, bool? forceLoop = null)
    {
        Init();
        int index = 0;
        if (clips.Length > 0 && !string.IsNullOrEmpty(kind))
            for (int i = 0; i < clips.Length; i++)
                if (clips[i].name != null && clips[i].name.IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0) { index = i; break; }
        if (clips.Length > 0) SelectClip(index);
        time = 0f; speed = speedMultiplier;
        runDuration = clips.Length > 0 ? clips[index].lengthSec : duration;
        runLoop = forceLoop ?? (clips.Length > 0 ? clips[index].looping : loop);
        playing = true; Finished = false;
        gameObject.SetActive(true);
        Apply(0f);
    }

    /// <summary>에디터 크기 재기용: 그 시각의 모습으로 한 번 그린다(재생 상태는 안 바꾼다).</summary>
    public void SampleAt(float t)
    {
        Init();
        float d = clips.Length > 0 ? clips[Mathf.Max(0, clipIndex)].lengthSec : duration;
        Apply(Mathf.Clamp(t, 0f, d));
    }

    public void Stop() { playing = false; foreach (Layer l in layers) if (l.renderer != null) l.renderer.SetPropertyBlock(null); }

    void Update()
    {
        if (!playing) return;
        time += Time.deltaTime * speed;
        float d = runDuration > 0f ? runDuration : duration;
        if (time >= d)
        {
            if (runLoop) time %= Mathf.Max(d, 0.01f);
            else { playing = false; Finished = true; gameObject.SetActive(false); return; }
        }
        Apply(time);
    }

    void Apply(float t)
    {
        if (graph.IsValid())
        {
            AnimationClip c = clipIndex >= 0 && clips.Length > 0 ? clips[clipIndex].clip : clip;
            if (c != null)
            {
                playable.SetTime(Mathf.Min(t, (float)c.length));
                graph.Evaluate(0f);
            }
        }
        float seqStart = clipIndex >= 0 && clips.Length > 0 ? clips[clipIndex].startSec : 0f;
        for (int i = 0; i < layers.Length; i++)
        {
            Layer l = layers[i];
            if (l.renderer == null) continue;
            l.renderer.GetPropertyBlock(block);
            Color c = baseColors[i];
            float a;
            if (l.globalAlphaTimes.Length > 0) a = Mathf.Clamp01(SampleStatic(l.globalAlphaTimes, l.globalAlphaValues, seqStart + t, l.staticAlpha));
            else if (l.alphaTimes.Length > 0 && clipIndex <= 0) a = Mathf.Clamp01(Sample(l.alphaTimes, l.alphaValues, t));   // 곡선이 있으면 곡선이 알파(MDX 층 알파는 정적 또는 키 — staticAlpha 0은 「키가 있다」는 자리 표시일 수 있다)
            else a = l.staticAlpha;
            c.a = baseColors[i].a * a;
            block.SetColor(BaseColorId, c);
            if (l.uvTimes.Length > 0 && clipIndex <= 0)
            {
                Vector2 o = l.uvOffsets[StepIndex(l.uvTimes, t)];
                block.SetVector(BaseMapStId, new Vector4(1f, 1f, o.x, o.y));
            }
            l.renderer.SetPropertyBlock(block);
        }
    }

    static float Sample(float[] times, float[] values, float t)
    {
        int n = times.Length;
        if (n == 0) return 1f;
        if (t <= times[0]) return values[0];
        for (int i = 1; i < n; i++)
            if (t <= times[i])
            {
                float span = times[i] - times[i - 1];
                return span <= 1e-5f ? values[i] : Mathf.Lerp(values[i - 1], values[i], (t - times[i - 1]) / span);
            }
        return values[n - 1];
    }

    /// <summary>첫 키보다 앞 시각은 정적 알파(blender 규칙 — 키가 뒤 시퀀스에서 시작하는 모델이 있다).</summary>
    static float SampleStatic(float[] times, float[] values, float t, float staticValue)
    {
        if (times.Length == 0 || t < times[0]) return staticValue;
        return Sample(times, values, t);
    }

    static int StepIndex(float[] times, float t)
    {
        int index = 0;
        for (int i = 0; i < times.Length; i++) { if (times[i] <= t) index = i; else break; }
        return index;
    }
}
