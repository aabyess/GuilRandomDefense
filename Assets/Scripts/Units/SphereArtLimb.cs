using UnityEngine;

/// <summary>
/// 팔에 씌우는 부가 이펙트 메시(마그마 소매 등)의 맞춤 정보 — SphereArtBuilder가 프리팹 루트에 단다(2026-10-01).
/// 원작 소매는 원작 몸의 팔(팔 벌린 자세)에 맞춘 것이라 우리 스킨 팔과 방향·길이가 다르다. 그래서 UnitSphereArt가 붙일 때
/// 「어깨 끝 → 손 끝」 선분(프리팹 로컬)을 우리 팔의 어깨 뼈 → 손 뼈 선분에 회전·크기로 맞춘 뒤 위팔 뼈에 월드 자리를 지킨 채 붙인다.
/// </summary>
public class SphereArtLimb : MonoBehaviour
{
    public Vector3 start;    // 소매의 어깨 쪽 끝(프리팹 로컬, 게임 단위)
    public Vector3 end;      // 소매의 손 쪽 끝
    public bool left;        // 왼팔이면 true(유니티 +X가 몸의 오른쪽 — 방향은 하이어라키 뼈로 정하므로 참고용)
    public float lengthBoost = 2.2f;   // 우리 팔 길이에 맞춘 배율에 더 곱한다 — 소매는 원래 팔보다 부풀어 있다
}
