# GuilRandomDefense 프로젝트 브리프

## 1. 프로젝트 정체
- 장르: 워크래프트3 유즈맵 "원랜디(원피스 랜덤 디펜스)" 스타일의 3D 게임
- 핵심 룰: 랜덤 뽑기로 나온 유닛을 조합 → 라인으로 오는 몹 웨이브 방어
- 없는 것: 기지 건설·자원 채집·포그오브워 (풀 RTS 프레임워크는 오버킬)
- 필요한 4가지 시스템
  1. WC3식 유닛 조작 (드래그 박스 선택 · 우클릭 이동 · 스킬 단축키)
  2. 웨이브 스포너 + 레인 경로
  3. 랜덤 뽑기 / 조합
  4. 데이터 드리븐 유닛·스킬 정의 (ScriptableObject)
- 원피스 캐릭터는 IP → 사용 불가, **오리지널 캐릭터**로
- 솔로 개발 (Claude 세션들이 팀원 역할)

## 2. 환경 (실측 확인됨)
- Unity 6.0 LTS **6000.0.82f1**, Apple Silicon, Universal 3D (URP) 템플릿
- 경로: `/Users/sang/Documents/GitHub/GuilRandomDefense`
- git: `main`, remote `https://github.com/aabyess/GuilRandomDefense` (PRIVATE), 공식 Unity .gitignore 적용
- `com.unity.ai.navigation` 2.0.14 설치됨 → NavMesh 바로 사용
- **Input System** (`com.unity.inputsystem` 1.19.0) 기준 — 구 Input Manager 사용 금지
- Rosetta 2 설치됨, Unity Personal 라이선스 활성화됨

## 3. 확정된 방향
- 엔진: Unity (Godot은 3D 대량 유닛 성능 한계, Unreal은 솔로에 과함)
- 인게임 맵 에디터 없음 — 맵 1개 고정, Unity Terrain/씬 에디터로 제작
- "직접 만드는" 가치는 **데이터 레이어** (유닛 스탯/스킬/웨이브를 ScriptableObject 또는 JSON으로 밸런싱)
- 에셋스토어 풀 RTS 프레임워크(RTS Engine 등) 구매 안 함 — 디펜스엔 직접 짜는 게 더 작음

## 4. 에셋 후보 (라이선스 확인됨, 아직 미반입)
- Quaternius "Ultimate Fantasy RTS" — CC0, 128모델(건물/자연), ⚠️ 애니메이션·텍스처 없음
- Quaternius Animated Character Pack — 유닛용, 애니 포함 (받기 전 개별 라이선스 재확인)
- Kenney "Tower Defense Kit" — 3D, CC0, 160파일, 중세 성 테마
- 애니메이션: Mixamo(무료) 리타게팅 / 모델 수정: Blender

## 5. 진행 순서 (현재 위치: 2단계 직전)
1. ✅ 프로젝트 생성 + git + GitHub push
2. NavMesh 바닥 — Plane(Scale 5,1,5) → NavMesh Surface → Bake / Capsule + NavMesh Agent (에디터 UI 작업, **사용자가 직접**)
3. 클릭 이동 스크립트 (첫 실제 코드) → 드래그 박스 선택 → 공격
4. 레인 1개 + 웨이브 스포너 + 랜덤 뽑기 → **여기서 재미 검증**
5. 재미 확인 후 에셋 입히기

## 6. 작업 규칙
- 커밋은 자주. **push는 PM이 판단해서 한다** — 사장님 확정(2026-09-03): *"좀 많이 변경됐다 싶으면 계속 푸시하셈"*
- 씬/프리팹 등 Unity 바이너리성 파일은 충돌 시 되돌리기 어려움 → **씬 수정 전 커밋**
- 코드는 `Assets/Scripts/` 아래 기능별 폴더: `Units/` `Waves/` `Data/` `UI/`
