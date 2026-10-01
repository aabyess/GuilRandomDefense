using UnityEngine;

/// <summary>
/// 손에 쥐는 부가 이펙트(얼음 칼날 등)의 「쥐는 점」 — SphereArtBuilder가 손 뼈 붙임 프리팹 루트에 단다(2026-10-01).
/// 원작 pivot은 원작 몸의 손(팔 벌린 자세)이라 우리 스킨 손과 자리가 다르다. UnitSphereArt가 붙일 때 이 점을 우리 손 뼈 자리로 옮긴다.
/// </summary>
public class SphereArtHold : MonoBehaviour
{
    public Vector3 pivot;   // 프리팹 로컬(게임 단위) — 원작 손 뼈 pivot
}
