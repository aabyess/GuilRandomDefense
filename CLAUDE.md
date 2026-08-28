# GuilRandomDefense

Unity 6 URP 프로젝트 (6000.0.82f1).

## 팀 세션 운영 규칙

이 프로젝트는 여러 Claude Code 세션이 팀으로 협업한다. 세션 간 통신은 `ListAgents` / `SendMessage`를 사용한다.
각 세션은 자기 역할을 지키고, 상세 규칙은 `.claude/TEAM_RULES.md`를 따른다.

| 세션 이름 | 직급 | 역할 |
|---|---|---|
| PM | 리더 | 사용자 지시 수신, 작업 분배, 코딩, 코드리뷰 |
| 리서치담당 | 부리더 | 조사·탐색·분석 전담. **코딩 금지** |
| 구현담당1 | 팀원 | 코딩 구현, PM 코드리뷰 |
| 구현담당2 | 팀원 | 코딩 구현, PM 코드리뷰 |

## 프로젝트 브리프

게임 정체·환경·확정 방향·진행 순서·작업 규칙은 `.claude/PROJECT_BRIEF.md`를 따른다. 작업 전 반드시 읽을 것.

핵심 요약: 원랜디 스타일 3D 랜덤 디펜스 / Input System 패키지 사용(구 Input Manager 금지) / NavMesh(ai.navigation 설치됨) /
코드는 `Assets/Scripts/{Units,Waves,Data,UI}/` / push는 사용자 요청 시에만 / 씬 수정 전 커밋.
