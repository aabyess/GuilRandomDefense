using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원작 스킬 연출 「대본」(10-09, blender ~/GRD_scenes/scripts/*.json → Editor CinematicImporter가 만든다): 시간표(timeline)와 쓰는 모델 목록.
/// 재생은 SkillCinematic이 한다. 단위: 거리는 워크3 단위(월드 = ÷WorldScale), 시간은 초.
/// </summary>
public class CinematicScript : ScriptableObject
{
    public enum Op { Spawn, Set, Ramp, Kill, CameraShake }
    public enum Anchor { Caster, Target, Ship }

    [Serializable]
    public class Ev
    {
        public float t;
        public Op op;
        public string id;
        public string model;             // models[].name
        public float baseScale = 1f;     // usca
        public float scalePercent = 100f;
        public float flyHeight;          // 워3 단위
        public float lifeSec = -1f;      // 음수 = 시퀀스 길이를 따른다
        public float deathSec = 0.1f;
        public Anchor anchor;
        public bool hasPolar;
        public float polarRadius, polarAngleDeg;   // 워3 단위·도
        public string anim;              // birth / death / "" (= stand)
        public float timescale = 1f;
        public bool facingRandom;
        public bool hasMove;
        public Anchor moveAnchor;
        public float moveSpeed;          // 워3 단위/초
        public float rampTo, rampRate;   // flyHeight 변화
        public float shakeDuration, shakeMagnitude;
        public float vertexAlpha = 1f;
        public bool hasScaleSet, hasTimescaleSet;
    }

    [Serializable]
    public class ModelRef
    {
        public string name;
        public GameObject prefab;        // null이면 대체 처리(substitute)
        public string substitute;        // 「ship」「lightning」 같은 기존 소품 이름(대체)
        public float substituteLengthWc3;    // >0이면 대체 모델: 이 길이(워3 단위)에 맞춰 키운다
        public float substituteNativeLength; // 대체 프리팹의 원래 길이(월드/미터 — 프리팹 단위 그대로)
    }

    public string scriptId;
    public string title;
    public float duration;
    public List<Ev> events = new List<Ev>();
    public List<ModelRef> models = new List<ModelRef>();

    public ModelRef Model(string name)
    {
        foreach (ModelRef m in models) if (m.name == name) return m;
        return null;
    }
}
