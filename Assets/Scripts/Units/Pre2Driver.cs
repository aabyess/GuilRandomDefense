using System;
using UnityEngine;

/// <summary>
/// 원작 입자(PRE2) 한 묶음을 Unity ParticleSystem에 연결하는 구동기(10-09). 방출률 키(KP2E)·가시 키(KP2V)는 MDX 전체 시간(ms) 키라
/// ParticleSystem이 직접 못 받아 여기서 재생 시각마다 보간해 rateOverTime에 넣는다. 중력은 모델 스케일(lossyScale)을 곱해 월드 가속으로 환산한다.
/// 모델 로컬 단위는 미터(워3 1=0.01m)로 만들어 두고(PS 크기·속도 Hierarchy 스케일), 모델 루트 스케일이 월드 크기를 정한다.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class Pre2Driver : MonoBehaviour
{
    [Serializable] public class Key { public float ms; public float value; }

    public float baseRate = 10f;          // 키가 없을 때의 방출률(초당)
    public float war3Gravity;             // 워3 중력(양수 = 아래로, 워3 단위/s²)
    public float sequenceStartMs;         // 재생 중인 시퀀스의 MDX 전체 시간 start
    public Key[] rateKeys = new Key[0];   // KP2E
    public Key[] visKeys = new Key[0];    // KP2V
    public bool rateKeysLinear, visKeysLinear;
    public bool burstOnly;                // squirt: 방출률 키가 있는 구간에서만

    ParticleSystem ps;
    float time;
    bool active;

    void Awake() { ps = GetComponent<ParticleSystem>(); }

    /// <summary>재생 시작(연출이 모델을 켠 직후 부른다). 시간 0에서 시작, 입자를 비운다.</summary>
    public void Begin(float seqStartMs)
    {
        if (ps == null) ps = GetComponent<ParticleSystem>();
        sequenceStartMs = seqStartMs;
        time = 0f; active = true;
        float scale = Mathf.Max(0.0001f, transform.lossyScale.y);
        var main = ps.main;
        main.gravityModifier = war3Gravity * 0.01f * scale / 9.81f;   // 워3 단위→m→월드(lossyScale)
        ps.Clear(true);
        ApplyRate(0f);
        ps.Play(true);
    }

    /// <summary>방출을 멈춘다(남은 입자는 수명대로 사라진다).</summary>
    public void Halt() { active = false; if (ps != null) { var e = ps.emission; e.rateOverTime = 0f; } }

    void Update()
    {
        if (!active || ps == null) return;
        time += Time.deltaTime;
        ApplyRate(time);
    }

    void ApplyRate(float t)
    {
        float ms = sequenceStartMs + t * 1000f;
        float rate = rateKeys.Length > 0 ? Eval(rateKeys, ms, rateKeysLinear, 0f) : (burstOnly ? 0f : baseRate);
        if (visKeys.Length > 0) rate *= Eval(visKeys, ms, visKeysLinear, 1f);
        var e = ps.emission; e.rateOverTime = rate;
    }

    static float Eval(Key[] keys, float ms, bool linear, float before)
    {
        if (ms < keys[0].ms) return before == 1f ? keys[0].value : 0f;
        for (int i = 1; i < keys.Length; i++)
            if (ms < keys[i].ms)
                return linear ? Mathf.Lerp(keys[i - 1].value, keys[i].value, (ms - keys[i - 1].ms) / Mathf.Max(1e-3f, keys[i].ms - keys[i - 1].ms)) : keys[i - 1].value;
        return keys[keys.Length - 1].value;
    }
}
