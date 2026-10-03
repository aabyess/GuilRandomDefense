# 모리아(h00B) 자리 = 특별함_임채준 — 2026-10-02 (사장님 결정 → PM 지시, 구현담당1)

- 대응표(`MASTER_UID_ROSTER_MAP.csv`): `h015 → 특별함_임채준` 행을 `h00B → 특별함_임채준`으로 바꿨다. 버기(h015) 자리는 비움(로스터에 대응 유닛 없음).
- 원작 h00B(w3u): ua1b 400 · ua1c 1.0 · ua1r 600 · ua1t normal · uabi A0JE·A07Y·A0BA(판매)·A028·A016(조합)·Avul·**A113**.
  전투 능력은 A113뿐. 스탯은 특별함 33행 수열의 (400/1.0/600) 행 = 사거리 112.12, 평타 타입 Normal로 옮김(박기찬·노태현과 같은 수열 행).
- 옛 임채준 값 525/1.3158/134.39는 버기 값이 아니라 원작 h01A(크로커다일 525/0.76/600) 행의 순위 배정값이었다 → 이 행은 지금 holder가 없다(그대로 둠, 보고).
- 버기 스킬 SkillData_원작능력_특별함_임채준(A05E 마기탄) 에셋과 SkillVfxTable 항목 삭제(고아 검사 ④).
- A113(ANba 블랙애로, Nbau=h00H 좀비): `UnitData.raiseOnKill*` + `UnitAttacker.TryRaiseOnKill` — 임채준이 **평타로 죽인** 적이 주인 몫의 안흔함_좀비로 부활.
  · 대상: 원작 atar `air,nonancient,nonsapper,nonhero,enemies,ground`. 원작에서 보스·퀘스트 보스·PV 200 몹은 전부 `ancient,sapper`(utyp) → **PointValue ≥ 200 제외**(보스 불가).
  · 발동: ANba는 오브 자동시전, amcs 0(마나 0), ahdu/adur 0.05(버프 0.05초 = 그 화살로 죽은 적만).
  · 🔴 [추정] Nba3 = 10.0: 맵에 ANba가 이것 하나뿐이라 w3a만으론 확정 불가. 기본은 「부활 확률 10%·영구」(A029 좀비×3 조합이 성립하려면 쌓여야 함).
    「소환 지속시간 10초」가 맞다면 에셋 `raiseOnKillChancePercent: 100`, `raiseOnKillLifetimeSeconds: 10`으로만 바꾸면 된다(TimedLife).
- 좀비(h00H) 원작 uabi = A029,Avul — 판매·창고 능력 없음(우리 판매 보상 0으로만 반영, 유닛 UI 버튼 제한은 안 함).
- 탐침: `gameshot … call:MoriaProbe.Setup wait:45 call:MoriaProbe.Report` (확률 100%로 시험 후 되돌림) — 평타 처치 19건 → 좀비 19기(모델 에밀리아) → 좀비×3 + 흔함_강재규 → 압살롬 조합 성공(좀비 19→16·강재규 1→0·압살롬 0→1).


## 10-03 확정(PM)
Nba3 = **소환 지속시간**(WC3 ANba 필드: Nba1 추가 피해 · Nba2 소환 수 · Nba3 소환 유닛 지속시간 — 스톡 블랙애로 Dark Minion 80초). 원작 = 화살로 죽은 적마다 100% 좀비 1기·10초.
→ 에셋 `raiseOnKillChancePercent: 100` · `raiseOnKillLifetimeSeconds: 10`. 좀비×3 조합은 10초 안에 셋을 모아야 한다(원작 그대로).
