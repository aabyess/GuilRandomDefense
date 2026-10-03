# GuilRandomDefense

Unity 6 URP 프로젝트 (6000.0.82f1).

## 팀 세션 운영 규칙

이 프로젝트는 여러 Claude Code 세션이 팀으로 협업한다. 세션 간 통신은 `ListAgents` / `SendMessage`를 사용한다.
각 세션은 자기 역할을 지키고, 상세 규칙은 `.claude/TEAM_RULES.md`를 따른다.

| 세션 이름 | 직급 | 역할 | 모델 |
|---|---|---|---|
| pm | 리더 | 사장님 지시 수신, 작업 분배, 코딩, 코드리뷰, push · 사장님이 직접 띄움 | 사장님이 고름 |
| 구현담당1 | 팀원 | 게임 규칙·스킬·밸런스 측정 도구 구현 | Sonnet 5.5 |
| 구현담당2 | 팀원 | UI·표시류·세이브·이펙트 구현 | Sonnet 5.5 |
| blender | 팀원 | Blender 모델·스킨·동작 (Assets엔 안 씀) | Sonnet 5.5 |

## 이어받기

사장님은 토큰 비용 때문에 세션을 하루에 한 번 갈아 끼운다. 사장님은 **PM 세션만** 띄우고 「next.md 파일 읽고 진행해」라고 지시한다.
그 지시를 받은 새 PM은 이 순서대로 한다(사장님 확정 2026-10-03):

1. **`.claude/NEXT_SESSION.md`를 읽는다.** 직전 세션이 어디까지 했고, 무엇이 막혀 있고, 사장님 답을 기다리는 게 무엇인지가 거기 있다.
2. **`.claude/team/spawn_team.sh --fresh`로 팀원 셋(구현담당1·구현담당2·blender)을 Orca 탭에 새로 띄운다.**
   어제 같은 이름 탭이 남아 있으면 스크립트가 닫고 새로 띄운다(하루치 교체 = 토큰 절약 · 같은 이름이 둘이면 SendMessage가 헷갈린다).
   각 탭은 `claude --dangerously-skip-permissions -n <역할> --model claude-sonnet-5-5 <역할 지시문>`이다.
   역할 지시문은 `.claude/team/<역할>.md`다.
   닫기 전에 할 일: ListAgents로 옛 팀원이 **일하는 중(busy)**인지 본다. 일하는 중이면 사장님께 먼저 묻는다.
   측정 체인(nohup)과 에디터 판은 탭을 닫아도 안 죽는다.
   하루 중간에 하나만 다시 띄울 때는 `--fresh 구현담당1`처럼 역할을 준다. `--fresh`가 없으면 떠 있는 역할은 건너뛴다.
3. 팀원은 문서를 읽은 뒤 PM에게 「[역할 → PM] 준비 완료」와 자기 몫의 남은 일을 보낸다. 셋이 다 보고하면 첫 지시를 준다.
   첫 지시에는 배경과 에디터 규칙을 같이 준다(팀원은 어제 기억이 없다).
4. 세션을 마칠 때 PM이 `NEXT_SESSION.md`를 갱신한다.

## 프로젝트 브리프

게임 정체·환경·확정 방향·진행 순서·작업 규칙은 `.claude/PROJECT_BRIEF.md`를 따른다. 작업 전 반드시 읽을 것.

핵심 요약: 원랜디 스타일 3D 랜덤 디펜스 / Input System 패키지 사용(구 Input Manager 금지) / NavMesh(ai.navigation 설치됨) /
코드는 `Assets/Scripts/{Units,Waves,Data,UI}/` / **push는 PM이 판단해서 (아래)** / 씬 수정 전 커밋.
