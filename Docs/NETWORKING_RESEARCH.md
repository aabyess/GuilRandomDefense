# 네트워킹 프레임워크 조사 (2026-08-28, 리서치담당)

> 배경: GuilRandomDefense는 1~4인 협동 멀티플레이 확정 (GDD 9장). 프레임워크 선택 필요.

## 1. 프레임워크 비교

| | Unity NGO | Mirror | Photon Fusion 2 | Photon PUN2 |
|---|---|---|---|---|
| 비용 | 엔진 내장 무료, 릴레이는 UGS 별도 과금 | 완전 무료·오픈소스(MIT), CCU 과금 없음 | 무료 100 CCU, 이후 500=$125/월 | 유지보수 모드(레거시) |
| Unity 6 호환 | 공식 내장 (2.x) | 지원 확인됨 | 지원, 신규 권장 | 지원되나 Photon이 Fusion 권장 |
| 학습 난이도 | 중간 | 중간 (릴레이·매치메이킹 직접 구성) | 낮음~중간 (룸 코드 내장) | 낮음, 레거시 |
| 유지보수 | 활발 (커뮤니티 평 호불호) | 활발, 2014~ 장수 | 활발, Photon 주력 | 유지보수만 |

## 2. 우리 케이스 (4인 + 몹 수십~100)

- 이 규모는 **4개 프레임워크 전부 스펙상 여유**. 원래 훨씬 큰 동접을 타겟팅함.
- 단, **몹을 서버/호스트 권위로 두고 클라이언트는 보간만** 해야 대역폭 안정 —
  이건 프레임워크 무관하게 우리가 직접 설계할 부분.
- **호스트-클라이언트 권장**: 전용 서버는 24/7 비용이라 솔로 개발 전제와 안 맞음.
- **릴레이 필수**: 서로 다른 집 네트워크면 순수 P2P는 대부분 실패.
  - NGO → Unity Relay (UGS)
  - Mirror → 자체 릴레이 없음, 외부(Edgegap) 또는 직접 구축 → **추가 작업**
  - Photon → Photon Cloud가 릴레이+매치메이킹 기본 내장

## 3. 마이그레이션 비용

프레임워크 무관하게 패턴 동일: MonoBehaviour → NetworkBehaviour, 상태값 → NetworkVariable,
입력·요청 → ServerRpc/ClientRpc, 스폰을 서버 전용으로 격리.

→ **"어느 게 마이그레이션이 싼가"보다 "어느 게 앞으로 짤 때 삽질이 적은가"가 실질 기준.**

## 4. 추천

**1순위: Photon Fusion 2 (Host Mode)**
- 무료 100 CCU = 4인 게임 기준 약 25룸 동시 운영, 사실상 당분간 무료
- 릴레이+매치메이킹 내장 → 인프라 직접 안 짜도 됨 (솔로 개발 시간 절감 큼)
- Photon이 신규 프로젝트 표준으로 미는 중
- 리스크: 서드파티 종속, CCU 초과 시 과금, Unity 신기능(DOTS 등) 결합은 NGO보다 불리

**2순위: Unity NGO + Unity Relay**
- 공식·엔진 내장이라 종속성 최소화 우선이면 이쪽
- 릴레이 직접 붙여야 함, 커뮤니티 평 호불호
- Relay 무료 CCU 한도는 2023년 자료 기준이라 **최신 재확인 필요 (추정)**

## 주의

이번 조사는 웹 검색 요약 기준. **Unity Relay / Photon 최신 가격·무료 한도는 공식 페이지에서 재확인 권장.**

출처: [Unity NGO Manual](https://docs.unity3d.com/6000.5/Documentation/Manual/com.unity.netcode.gameobjects.html),
[Mirror GitHub](https://github.com/MirrorNetworking/Mirror),
[Photon Fusion Pricing](https://www.photonengine.com/fusion/pricing),
[Photon blog - PUN2](https://blog.photonengine.com/tag/pun-2/)
