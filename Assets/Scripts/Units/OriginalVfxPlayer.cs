using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 원작(워크3 MDX) 이펙트 모델 재생기(10-08, blender 변환 FBX): 뼈 애니 클립 하나를 한 번 재생하고, 층(레이어)마다
/// 알파 곡선(선형 보간)과 UV 오프셋(계단)을 머티리얼 속성 블록으로 입힌다. 파티클(PRE2)은 1차 제외.
/// 데이터(Layer)는 에디터 빌더(OriginalVfxBuilder)가 json에서 직렬화해 둔다. SkillVfx.PlayPrefab이 Restart()로 다시 쓴다.
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
    }

    public Animator animator;
    public AnimationClip clip;
    public float duration = 1f;
    public bool loop;
    public Layer[] layers = new Layer[0];

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    PlayableGraph graph;
    AnimationClipPlayable playable;
    MaterialPropertyBlock block;
    Color[] baseColors;
    float time;
    bool playing;

    public bool IsPlaying => playing;

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
        if (animator != null && clip != null)
        {
            graph = PlayableGraph.Create("OriginalVfx");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "out", animator);
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            output.SetSourcePlayable(playable);
        }
    }

    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

    /// <summary>처음부터 다시 재생. 풀에서 꺼낸 사본이 부른다.</summary>
    public void Restart()
    {
        Init();
        time = 0f;
        playing = true;
        gameObject.SetActive(true);
        Apply(0f);
    }

    /// <summary>에디터 크기 재기용: 그 시각의 모습으로 한 번 그린다(재생 상태는 안 바꾼다).</summary>
    public void SampleAt(float t) { Init(); Apply(Mathf.Clamp(t, 0f, duration)); }

    public void Stop() { playing = false; foreach (Layer l in layers) if (l.renderer != null) l.renderer.SetPropertyBlock(null); }

    void Update()
    {
        if (!playing) return;
        time += Time.deltaTime;
        if (time >= duration)
        {
            if (loop) time %= Mathf.Max(duration, 0.01f);
            else { playing = false; gameObject.SetActive(false); return; }
        }
        Apply(time);
    }

    void Apply(float t)
    {
        if (graph.IsValid())
        {
            playable.SetTime(Mathf.Min(t, (float)clip.length));
            graph.Evaluate(0f);
        }
        for (int i = 0; i < layers.Length; i++)
        {
            Layer l = layers[i];
            if (l.renderer == null) continue;
            l.renderer.GetPropertyBlock(block);
            Color c = baseColors[i];
            c.a = baseColors[i].a * l.staticAlpha * Sample(l.alphaTimes, l.alphaValues, t);
            block.SetColor(BaseColorId, c);
            if (l.uvTimes.Length > 0)
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

    static int StepIndex(float[] times, float t)
    {
        int index = 0;
        for (int i = 0; i < times.Length; i++) { if (times[i] <= t) index = i; else break; }
        return index;
    }
}
