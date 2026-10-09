using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원작 스킨 교체(10-09) — 원작 모델은 시퀀스마다 지오셋 알파가 달라(변신체·소품이 특정 동작에서만 보임) FBX 자체엔 그 알파가 안 실린다.
/// blender clip_map.json의 clips[].hiddenMeshes(그 시퀀스 시작에 알파 0인 메시)를 프리팹에 담아 두고, 지금 재생 중인 클립이 그 클립이면 그 메시를 끈다.
/// 클립 이름은 임포트한 이름(Attack·Spell …). 평소(Idle·Move)에 안 보이는 메시는 hiddenAlways로 둔다.
/// </summary>
public class SkinClipHider : MonoBehaviour
{
    [Serializable] public class Entry { public string clip; public string[] meshes = new string[0]; }

    public Entry[] entries = new Entry[0];
    public string[] hiddenAlways = new string[0];

    Animator animator;
    readonly Dictionary<string, Renderer[]> cache = new Dictionary<string, Renderer[]>();
    string lastClip;
    Renderer[] lastHidden = new Renderer[0];

    void Awake() { animator = GetComponentInChildren<Animator>(); }

    Renderer[] Find(string[] names)
    {
        var list = new List<Renderer>();
        foreach (string n in names)
        {
            if (!cache.TryGetValue(n, out Renderer[] found))
            {
                var tmp = new List<Renderer>();
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject.name == n || (r is SkinnedMeshRenderer smr && smr.sharedMesh != null && smr.sharedMesh.name == n)) tmp.Add(r);
                cache[n] = found = tmp.ToArray();
            }
            list.AddRange(found);
        }
        return list.ToArray();
    }

    void Start()
    {
        foreach (Renderer r in Find(hiddenAlways)) r.enabled = false;
    }

    void LateUpdate()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        string clip = null;
        AnimatorClipInfo[] infos = animator.GetCurrentAnimatorClipInfo(0);
        if (infos.Length > 0) clip = infos[0].clip.name;
        if (clip == lastClip) return;
        lastClip = clip;
        foreach (Renderer r in lastHidden) if (r != null) r.enabled = true;
        foreach (Renderer r in Find(hiddenAlways)) r.enabled = false;
        lastHidden = new Renderer[0];
        if (clip == null) return;
        foreach (Entry e in entries)
            if (e.clip == clip) { lastHidden = Find(e.meshes); foreach (Renderer r in lastHidden) r.enabled = false; break; }
    }
}
