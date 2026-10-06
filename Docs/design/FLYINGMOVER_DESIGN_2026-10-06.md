# FlyingMover 설계 (B안, PM 결정) — 구현담당2, 2026-10-06

목적: 비행(이재윤·황준석)·바다이동(불멸 박은석)·전지역이동(불멸 김용태) 유닛이 **섬 밖 바다로 못 나가는** 문제를 푼다.
뿌리: 바다 NavMesh(y 0)와 섬 NavMesh(y 8)가 이어져 있지 않다(`UnitData.cs` MovementAbility 주석). `areaMask`에 Sea를 넣어도 에이전트는 섬을 못 떠난다. 그래서 이 유닛은 NavMesh를 버리고 직선으로 난다.
(코드 읽기만으로 정리한 설계 — 구현·실측 전. 아래 줄 번호는 10-06 HEAD 기준.)

## 1. 구조 — 컴포넌트 하나, 호출부 5곳

- **`FlyingMover`**(새 MonoBehaviour, `Assets/Scripts/Units/`): 목적지 하나를 들고 매 프레임 `transform.position`을 직선으로 `speed`만큼 옮긴다. 도착(거리 ≤ 임계)하면 멈춘다. 회전은 이동 방향으로 즉시(`UnitSpawner`가 NavMeshAgent에 주는 1080°/s와 같게).
- **속도는 `NavMeshAgent.speed` 한 곳에서 읽는다.** `UnitSpawner.cs:46`이 `agent.speed = data.moveSpeed`를 넣고, `UnitAttacker.cs:2049~2053`의 아군 이속 감소(`AllyMoveSpeedDebuff`)도 `agent.speed`를 쓴다 — 끈 에이전트도 속도 속성은 쓰고 읽을 수 있으므로(`TryGetComponent`만 하고 `enabled`는 안 본다) **FlyingMover가 `agent.speed`를 읽으면 이속 감소·슬로우 코드를 한 줄도 안 고친다.** 속도 값을 따로 들면 이감이 조용히 안 먹는다.
- **높이는 스폰 때 y 고정**(섬 윗면 y 8 근처). 바다 위에서도 같은 높이로 난다(바다 윗면 0이라 잠기지 않음). 적(`EnemyDummy`)도 레인 높이라 3D 거리 판정(`UnitCombat.CurrentTarget`의 `sqrMagnitude`)은 그대로 맞다.
- **`NavMeshAgent`는 지우지 않고 `enabled=false`.** `UnitMover`·`UnitCombat`이 `RequireComponent(NavMeshAgent)`라 지우면 안 된다. `NetReplicaBuilder.cs:150`이 이미 같은 방식(끄기만)이다.

## 2. 붙이는 자리 — `UnitSpawner.Spawn`의 `NavMeshAgent` 블록(`UnitSpawner.cs:44` 근처)

```
if ((data.movementAbility & MovementAbility.Flying) != 0)   // 일반화: 아래 §6
{
    NavPlacement.Place(agent, position);                    // 스폰 자리는 그대로 NavMesh에 올려 y를 얻는다(먼저)
    agent.enabled = false;
    instance.AddComponent<FlyingMover>();
}
```
`agent.areaMask`·`acceleration` 설정은 그대로 둔다(꺼진 에이전트라 무해).
🔴 순서: `Place`(Warp) → `enabled=false` → `AddComponent`. 반대로 하면 스폰 자리가 NavMesh 밖이라 조용히 굳는다(`NavPlacement` 주석의 그 함정).
`PirateQuestManager.cs:131`·`UnitPortal`·`CombineSystem.cs:354~364`의 `ComputeAreaMask(movementAbility)`는 **스폰 자리 판정용**이라 바꾸지 않는다(비행도 섬 위에서 태어난다).

## 3. 호출부 전수 — 에이전트 API를 직접 부르는 곳과 처리

| 위치 | 지금 | 처리 |
|---|---|---|
| `UnitCombat.SetHold` (82~86) | `agent.isActiveAndEnabled && isOnNavMesh`일 때만 `ResetPath`·`velocity=0` | 꺼진 에이전트라 조건이 거짓 → **`flying.Stop()` 분기 추가** |
| `UnitCombat.Stop` (128~132) | 같음 | 같음. `S`키가 비행 유닛에 안 먹는 사고가 여기서 난다 |
| `UnitCombat.SnapTo` (113) | `NavPlacement.Place(agent…)`(실패 시 안 옮김) | 비행이면 `transform.position = (x, y고정, z)` + `Stop()`. NavMesh 검사 없음(모으기 V·정렬 C) |
| `UnitCombat.HasArrived` (351~352) | `agent.pathPending`·`remainingDistance`(꺼진 에이전트에선 오류) | 비행이면 `flying.HasArrived`. **이걸 안 고치면 PlayerMoving·Returning·AttackMoving이 영영 안 끝난다** |
| `UnitCombat.SetDestination` (355~362) | `agent.SetDestination` | 비행이면 `flying.SetDestination` |
| `UnitMover.MoveToGroundPoint` (126~148) | `isOnNavMesh`·`SamplePosition`으로 걸을 자리 검사 후 `combat.IssueMoveCommand` | 비행이면 **검사 생략**, 점을 맵 범위로만 자르고(§5) `combat.IssueMoveCommand` |
| `UnitCommands.AttackMove` (209~210) | `SamplePosition(… agent.areaMask)` 못 찾으면 `continue` | 비행이면 맵 범위로 자른 점으로 `combat.AttackMove` |

호출부가 적어서(UnitCombat 5 · UnitMover 1 · UnitCommands 1) 인터페이스는 안 만든다. UnitCombat에 `FlyingMover flying;`(Awake에서 `TryGetComponent`) 하나 두고 `if (flying != null)`로 가른다. **상태 기계(`CombatState`)는 한 줄도 안 바뀐다** — 에이전트 대신 부르는 대상만 바뀐다.

이미 알아서 되는 곳(고치지 않음, 실측으로만 확인): `CharacterAnimator.CurrentSpeed`(꺼진 에이전트면 `transform` 변화량으로 잼) · `Warehouse.Teleport`(`isOnNavMesh` 거짓이면 `transform.position` 대입) · `NetReplicaBuilder`.

## 4. 정지·공격·복귀 명령과의 관계

- **A 이동·우클릭·공격 이동·추적·복귀** 전부 `UnitCombat.SetDestination` 한 입구를 거치므로 FlyingMover는 「목적지 한 점」만 알면 된다. 적을 쫓을 때(`Chasing`) 목적지가 매 0.25초 적 위치로 갱신돼도(`SetDestination`이 같은 점이면 무시) 직선 추적이 된다.
- **공격 사거리 안에선 `SetDestination(transform.position)`**(`UnitCombat` 기존 규칙)이라 FlyingMover는 목적지=자기 위치 → 도착 → 정지. 그대로 둔다.
- **홀드(H)·정지(S)**: `Stop()`이 목적지를 지운다. 홀드는 목적지를 안 받게 `IsHolding`이 막는 기존 경로 그대로.
- **`UnitFacing`**(공격 중 표적을 바라봄): `agent.enabled` 거짓이라 항상 「멈춘 유닛」 경로로 떨어져 **이동 중에도 표적 쪽으로 돌려 버린다.** → FlyingMover가 이동 중이면 `UnitFacing`이 물러나게 `FlyingMover.IsMoving`(속도² > 임계)을 읽도록 한 줄(`UnitFacing.cs:25` 옆).

## 5. 맵 범위 — 어디까지 날 수 있나

NavMesh가 막아 주지 않으므로 **FlyingMover가 직접 제한**한다. 후보: 맵 전체 바깥 경계(`MapLayout`/`MapGenerator`의 월드 최대 반경) 안쪽 +여유. 이재윤 「섬 가장자리 26」 문제는 바다 NavMesh가 없어서가 아니라 **섬 NavMesh 바깥은 에이전트가 못 가서**였으니 이쪽은 풀린다. 구현 때 `MapLayout`의 실제 월드 경계 상수를 찾아 유도식으로(박지 않는다 — `thresholds-must-be-proportional`).
막을 것: 클릭이 맵 밖이면 가장자리로 자르기(조용한 실패 금지, 로그).

## 6. 일반화 — movementAbility 값으로

| 능력 | 사양 | 이동 |
|---|---|---|
| Flying(이재윤 · 황준석 「어디든지달려갑니다」) | 어디든 | FlyingMover(전지역) |
| 바다이동(불멸 박은석) | 육지+바다 | **섬↔바다 NavMesh가 안 이어져서 NavMesh로는 불가** → FlyingMover 같은 틀, 단 목적지 필터(바다만/육지만이 아니라 「바다 포함」) — 사장님 원문(`SPEC_IMMORTAL…`)의 정확한 뜻을 구현 때 다시 읽는다 |
| 전지역이동(불멸 김용태) | 맵 전체 | FlyingMover |

`MovementAbility` enum은 **맨 뒤에만 추가**(직렬화 규칙). 새 값이 필요하면(`SeaMove`·`AllMap`) 맨 뒤에. 지금 쓸 수 있는 값은 `Flying`·`WaterWalk`뿐이다. `ComputeAreaMask`가 이 둘을 같은 「바다 통과」로 묶으므로, FlyingMover 붙이는 조건은 **`Flying`만**(WaterWalk는 지금 쓰는 유닛 0기, 그대로 둔다).

## 7. 멀티(MP) — 위치 복제

- 실물(호스트)의 `transform`이 `NetEntity`(`NetworkTransform`)로 복제된다(`NetEntity.cs:103~106`이 거울을 실물 위치로 맞춘다). FlyingMover가 **호스트 실물의 transform을 직접 옮기므로 복제에는 추가 작업이 없다.**
- 클라 거울의 겉모습(`NetReplicaBuilder`)은 `KeepTypes` 밖의 MonoBehaviour를 지운다 → FlyingMover는 거울에서 자동 제거(좋음). 에이전트는 이미 꺼진다.
- **이동 명령은 이미 호스트 권한**: 클라의 우클릭은 `NetCommands.RequestMove`→호스트 `MoveToGroundPoint`(`UnitMover.cs:114`, `if (!GameAuthority.IsServer)`). FlyingMover도 `GameAuthority.IsServer`일 때만 `Update`가 움직이게(거울이 만들어지기 전에도 안전하게 이중 가드).
- 위험: 클라 거울이 NavMesh 검사를 하지 않으니(실물만 검사) 문제 없음. 점은 맵 범위로 자른 뒤 보낸다.

## 8. 실측 계획(구현 뒤, gameshot — `JaeyunProbe.MoveSea`/`MoveReport`를 FlyingMover용으로 고쳐 재사용)

1. 이재윤에게 바다 점 이동 명령 → `MoveReport`에서 **남은 거리 0**(지금은 섬 가장자리 약 26에서 멈춤). 같은 명령을 지상 아군(강재규)에겐 안 닿는 걸 대조로.
2. 이동 중 `S` → 즉시 정지 · `H` 홀드 · 공격 이동 → 적을 만나면 치고 다 치면 다시 목적지(비행 유닛).
3. 적 추적(`Chasing`) 직선 추적 확인 · 사거리 안 정지.
4. 아군 이속 감소 오라를 걸어 `agent.speed` 경로로 느려지는지(한 줄도 안 고쳤다는 가정의 검증).
5. 애니메이션: `CharacterAnimator`가 이동 클립을 타는지(사진·`speed` 파라미터).
6. MP: 호스트 비행 유닛이 바다로 나갈 때 클라 거울이 따라가는지(멀티 탐침이 있으면; 없으면 로컬 호스트 단독에서 `NetEntity` 경로만 확인하고 **미실측으로 적는다**).

## 9. 안 하는 것 / 미해결

- 순간이동(`TeleportToPoint`)은 구현담당3 배성령 틀을 공유한다(별건).
- 바다 위 유닛을 적이 어떻게 보나(적의 표적 선택은 `EnemyDummy.Active` 거리 기반 — 비행 유닛이 레인 밖에 있으면 안 맞는다는 것 자체는 의도로 본다).
- 비행 유닛끼리 겹침·회피 없음(현재 지상 유닛도 회피가 사실상 꺼져 있다 — `UnitMover.Awake` 주석).
- 막대한 호출부 변경이 아니라서 **한 덩어리로 가능**: 소스 3~4개(UnitCombat·UnitMover·UnitCommands·UnitFacing·UnitSpawner + 새 FlyingMover) + 이재윤 이동 탐침.
