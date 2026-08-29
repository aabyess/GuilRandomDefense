# 지형 · 이동 규칙 (사용자 제공, 2026-08-29)

> 근거: 스토리존 상세 이미지 `Docs/reference/map/01_스토리존.png`, 미니맵 `00_minimap.png`

## 핵심 전제: 맵 전체가 바다이고, 육지는 섬이다

미니맵의 어두운 영역이 전부 **바다**. 메인 필드·스토리존·창고·조합식 표 등은 전부 **섬**이다.

## 이동 규칙

| 유닛 종류 | 바다 통과 |
|---|---|
| 일반 지상 유닛 | ❌ 불가 |
| **비행 유닛** | ✅ 가능 |
| **수상보행(물 위를 걷는) 능력 보유 유닛** | ✅ 가능 |
| 텔레포트 능력 보유 유닛 | 별도 — 경로가 아니라 순간이동 |

→ 즉 **같은 맵인데 유닛마다 갈 수 있는 곳이 다르다.**

## 구현 방침 (Unity NavMesh)

Unity NavMesh의 **Area + AreaMask** 기능이 정확히 이 문제를 위한 것이다.

1. **NavMesh Area 분리**
   - `Walkable` — 섬(육지)
   - `Sea` — 바다 (커스텀 Area 추가)
   - 바다도 NavMesh를 굽되 별도 Area로 표시한다. 그래야 비행 유닛이 그 위로 경로를 찾을 수 있다.

2. **유닛별 AreaMask**
   - 지상 유닛: `Walkable`만
   - 비행/수상보행 유닛: `Walkable + Sea`
   - `NavMeshAgent.areaMask`로 설정

3. **텔레포트**
   - 경로 탐색이 아니라 위치 순간 이동. `NavMeshAgent.Warp()` 사용
   - 또는 고정 경로면 **OffMeshLink**

4. **"라인 쪽으로 돌아가기" 포탈** (스토리존 이미지 상단)
   - 스토리존 → 자기 레인으로 복귀시키는 장치
   - 트리거 진입 시 `Warp` 또는 OffMeshLink

## ⚠️ 현재 코드의 문제

`Assets/Scripts/Units/UnitMover.cs`가 목적지를 찾을 때 **`NavMesh.AllAreas`** 를 쓰고 있다.

```csharp
NavMesh.SamplePosition(hit.point, out navHit, destinationSampleRadius, NavMesh.AllAreas)
```

이러면 **지상 유닛도 바다를 목적지로 잡을 수 있다.** 에이전트의 `areaMask`를 무시하기 때문.
→ `agent.areaMask`를 넘기도록 고쳐야 한다.

## 스토리존 동작 (이미지 기준)

- 섬 **가운데에 스토리 오브젝트**가 있다
- 플레이어가 **자기 레인에서 스토리존으로 유닛을 보내면 자동으로 공격**한다
  (별도 명령 불필요 — 기존 `UnitAttacker`의 자동 공격과 같은 방식)
- 섬 상단에 **"라인 쪽으로 돌아가기"** 포탈
- 오른쪽에 다리(선착장)

## 물범 섬

스토리존 **아래에 작은 섬 4개**. 물범을 잡는 곳. (4개 = 플레이어 4인분으로 추정 — 확인 필요)
