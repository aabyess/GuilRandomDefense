using UnityEngine;

// 장작불 빛의 세기를 불규칙하게 흔든다. 두 사인파를 엇갈려 겹쳐 규칙이 눈에 안 띄게.
public class FireLightFlicker : MonoBehaviour
{
    [SerializeField] float baseIntensity = 6f;
    [SerializeField] float amplitude = 1.6f;

    Light target;
    float seed;

    void Awake()
    {
        target = GetComponent<Light>();
        seed = Random.value * 100f;
    }

    void Update()
    {
        if (target == null) return;
        float t = Time.time + seed;
        float wobble = Mathf.Sin(t * 7.3f) * 0.5f + Mathf.Sin(t * 13.1f + 1.7f) * 0.3f + (Mathf.PerlinNoise(t * 3f, seed) - 0.5f);
        target.intensity = baseIntensity + wobble * amplitude;
    }
}
