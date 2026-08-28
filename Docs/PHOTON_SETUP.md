# Photon Fusion 2 도입 절차 (2026-08-28, 리서치담당 조사)

> ⚠️ Photon 공식 문서가 봇 차단으로 직접 열람이 안 돼, 검색 캐시·커뮤니티 자료로 재구성했습니다.
> **실제 진행 시 https://doc.photonengine.com/fusion/current/getting-started 에서 최종 확인 권장.**

## 1. 계정 / App ID 발급
1. [Photon Dashboard](https://dashboard.photonengine.com) 접속 → 로그인/가입
2. **YOUR APPS → Development → CREATE A NEW APP**
3. Select Photon SDK: **Fusion** / Select SDK Version: **Fusion 2**
4. 생성하면 **App ID** 발급 (Unity에 입력할 값)

## 2. SDK 설치 (Unity 6000.0.82f1)
- 배포 형식: **.unitypackage** 가 표준
  - UPM(git URL) 지원 여부는 **미확인** → 설치 직전 공식 페이지 확인
- 절차: Photon "Getting Started → SDK & Release Notes"에서 다운로드
  → Unity에서 `Assets → Import Package → Custom Package`
- 최소 Unity 버전: 2020.3 LTS 이상 → 우리 버전 충족 ✅
- 의존성: Mono Cecil이 필요할 수 있음
  (`Window → Package Manager → + → Add package from git URL` → `com.unity.nuget.mono-cecil@1.10.2`)
  최신 SDK는 자동 처리될 수도 있음 → 설치 시 확인

### App ID 입력 위치
- 임포트 완료 시 **Fusion Hub 위저드가 자동 팝업** → Welcome 탭에 입력란
- 놓쳤으면 `Tools → Fusion → Fusion Hub → Fusion Setup`
- 또는 `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` 직접 선택해 입력

## 3. 동작 확인
- 공식 샘플 **Fusion Starter** 사용 (Host Mode / Shared Mode 두 버전 → **우리는 Host Mode**)
- 씬의 **Connection Manager** 오브젝트에서 `Auto Host or Client` / `Host` / `Client` 역할 지정 가능
- 확인 절차: SDK 임포트 → App ID 입력 → Fusion Starter 씬 열기
  → 에디터를 Host로 실행 + 빌드본(또는 2번째 에디터)을 Client로 접속 → 움직임 동기화되면 정상

## 4. ⚠️ Unity 6 관련 함정 (중요)
Unity Discussions에 **Unity 6.0.27 + Fusion 2.0.3에서 데모 임포트 시 컴파일 에러 19개**
(`The type or namespace name 'Menu' does not exist in the namespace 'Fusion'` 등) 보고 사례 있음.

**Photon 팀 공식 답변**: SDK가 깨진 게 아니라 **임포트 문제**.
렌더파이프라인 설정 변경 후 **Unity 재시작 없이** 임포트하면 발생.

→ **컴파일 에러가 나도 SDK 재설치부터 하지 말 것.**
   먼저 Unity 에디터를 완전히 재시작한 뒤 데모 패키지를 재임포트할 것.

또한 커뮤니티 정보(2025-06): "Fusion 2는 Unity 6 지원, 단 **6.1은 미검증이라 6.0.x 권장**"
→ 우리는 6000.0.82f1 (6.0.x 라인)이라 권장 범위 안 ✅

## 5. 멀티플레이 테스트 방법
- **Unity Multiplayer Play Mode (MPM)** 패키지 권장
  - 최대 4개(메인 에디터 1 + 추가 인스턴스 3) 로컬 동시 실행, **빌드 없이** 반복 테스트
  - ⚠️ **버전 주의**: MPM 1.6.0은 최소 6000.0.22f1 요구 → 우리 충족.
    **MPM 2.0은 6000.3.0b4 이상 요구라 우리는 못 씀 → 1.6.x 라인 설치할 것**
- 대안: 에디터(Host) + 빌드 실행파일 1~3개(Client) 동시 실행

## 미확인 항목
- Fusion 2 최신 SDK 정확한 버전 번호
- UPM(git URL) 설치 지원 여부
- 최신 SDK에도 Mono Cecil 의존성이 여전히 필요한지

출처: [App ID 발급](https://doc.photonengine.com/fusion/v2/getting-started/appid-instructions),
[SDK 다운로드](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download),
[Host Mode 튜토리얼](https://doc.photonengine.com/fusion/v2/tutorials/host-mode-basics/1-getting-started),
[Fusion Starter](https://doc.photonengine.com/fusion/current/game-samples/fusion-starter),
[Unity 6 호환성 스레드](https://discussions.unity.com/t/is-photon-fusion-2-0-in-unity-6-broken/1556429),
[Multiplayer Play Mode](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@1.6/manual/index.html)
