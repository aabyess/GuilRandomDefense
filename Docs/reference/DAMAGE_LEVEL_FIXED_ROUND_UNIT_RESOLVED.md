# `Round_Unit` 정체 + `Damage_level_Fixed`의 실제 방향 — 🔴 프레이밍 정정

조사: 리서치담당 / 2026-09-06
요청: PM ④⑤ — 구현담당2가 여기서 멈춰 있음
전제: [[navigation-routes-full]](NAVIGATION_ROUTES_FULL.md) ⑤에서 "`Damage_level_Fixed`가
`Round_Unit`의 `A11S` 레벨을 올린다"까지는 확인했으나 `Round_Unit`이 뭔지,
"올린다"가 플레이어에게 유리한지 불리한지는 안 팠던 상태.

## 🔴 먼저 정정 — 이전 보고의 프레이밍이 틀렸다

이전 보고에서 "전역 피해 스케일 변수"라고 썼는데 **틀렸다.** `Round_Unit`은
플레이어의 유닛이 아니라 **매 라운드 스폰되는 적(몬스터) 그 자체**다.
`Damage_level_Fixed`는 "내가 세게 때린다"가 아니라 **"내 라인의 적이 스킬
피해를 더 많이 받는다"(=적이 더 잘 죽는다) 축이다** — 방향 자체는 플레이어
유리가 맞지만, 성격이 "공격력 버프"가 아니라 "적 취약도 디버프"다.

## ① `Round_Unit`의 정체 — **매 라운드, 플레이어별로 1마리씩 스폰되는 그 라운드의 적**

`Trig_Round_10ver_Actions`(라운드 진행 시스템 자체 — "10ver"는 라운드10과
무관, 이 트리거 스크립트의 개발 버전번호로 보인다, 라운드 번호와 무관하게
매 라운드 실행됨)의 실제 스폰 코드:

```jass
set udg_C_int=1
loop
  exitwhen udg_C_int>4
  if(...) then
    set udg_Round_Unit[udg_C_int]=CreateUnitAtLoc(Player(6),
        udg_Round_UnitType[udg_Level], udg_Start_Location[udg_C_int], 270.00)
    call SetUnitUserData(udg_Round_Unit[udg_C_int], udg_C_int)
    call SetUnitAbilityLevelSwapped('Aegr', udg_Round_Unit[udg_C_int], udg_magicDP_Rine[udg_C_int])
    call SetUnitAbilityLevelSwapped('A11S', udg_Round_Unit[udg_C_int], (udg_Damage_level_Fixed[udg_C_int]+14))
  endif
  set udg_C_int=udg_C_int+1
endloop
```

- **`Player(6)`** = 적 진영(플레이어 유닛이 아니다).
- **`udg_Round_UnitType[udg_Level]`** — `udg_Level`은 **현재 라운드 번호**(같은
  트리거 위쪽에서 `set udg_Level=...(라운드 진행에 따라 갱신)`). `Round_UnitType`
  배열은 `Trig_MobBase_Actions`가 채운, **라운드 1~105까지 전체를 커버하는
  적 유닛타입 테이블**이다(`o00I`,`o004`...`o02H`, 원작 89종 로스터와 같은
  코드 계열). **즉 이건 "라운드10 전용 특수유닛"이 아니라 매 라운드 그
  라운드에 지정된 일반 적 그 자체다.**
- **`udg_C_int` 1~4 루프** — **플레이어(라인)별로 1마리씩**, 4명 전부에게
  각자 스폰된다. 이 분기는 "보스전"이 아닌 일반 라운드일 때만 탄다(바로
  옆 분기가 `gg_trg_Enemy_Boss_create` 호출 + "보스전입니다" 텍스트로 보스
  라운드를 따로 처리하는 게 보인다 — `Round_Unit`은 **일반 라운드 몬스터**
  경로다).
- **`SetUnitAbilityLevelSwapped('Aegr', ..., udg_magicDP_Rine[C_int])`** —
  마법피해 관련 스택축(`Aegr`, 이전 조사에서 이미 확인)도 **같은 자리에서
  플레이어별로** 걸린다 — `Damage_level_Fixed`와 나란히 가는 "플레이어별
  라운드 몬스터 개별 스케일링" 시스템의 절반이다.
- **신세계 사이드보스도 같은 배열을 재사용한다** — `Trig_sin_boss_skill1~4_Actions`
  가 똑같이 `udg_Round_UnitType[udg_Level]`로 스폰하고 `A11S`엔 고정 `1`을
  준다(이쪽은 `Damage_level_Fixed`를 안 먹는다 — 별개 경로).

**결론**: `Round_Unit`은 특정 캐릭터·특정 라운드 전용 존재가 아니라
**"이번 라운드에 이 플레이어 라인에 뜨는 일반 적"의 대명사**다. 우리
로스터로 옮기면 "적 89종 전체 각각"에 해당한다 — 특정 하나의 EnemyData
에셋에 매핑할 대상이 아니라, **라운드가 진행되는 동안 그때그때 스폰되는
모든 일반 몬스터 각각에 개별로 걸리는 축**이다.

## ② `A11S` 레벨과 `percentDamageTaken`의 방향 — **높을수록 적이 더 잘 죽는다**

`EnemyData.cs`에 이미 기록된 원작 공식(리서치담당 2026-09-05 조사, 이번에
재대조 — 값 일치, 정정 없음):

```
피해 = 대상 현재체력 × 0.10 × (0.20 + 0.05 × A11S레벨)
일반 적 레벨14 → 0.90   보스 레벨16 → 1.00(기준값)
```

`Damage_level_Fixed[플레이어]+14`가 그대로 이 레벨에 들어간다. **레벨이
오를수록 계수가 커진다 → 더 많은 %피해를 받는다 → 그 라운드의 적이 더
잘 죽는다(플레이어 유리).** 항법 "패왕의길"(+2)만 골라도 레벨14→16, 즉
**그 플레이어 라인의 일반 적이 원래 "일반 적" 계수(0.90)가 아니라 "보스"
계수(1.00)를 받는다** — 라운드 내내 모든 일반 몹이 보스급 스킬-피해
감수성을 갖게 된다는 뜻이다. 히든 이벤트 3개(Hidden_Aokiji+2·Eternal_Lucci+2·
IM_dragon+4)까지 다 쌓이면(패왕의길과 합산 가능한지는 아래 ③, 별도
확인 필요) 레벨14~24, 계수 0.90~1.40까지 갈 수 있다.

**`percentDamageTaken` 필드 자체의 기존 매핑(14→0.90, 16→1.00)은 이번
재확인으로 정정 없음 — 값으로 대조 완료, 그대로 맞다.**

## ③ 남은 것 — PM 요청 1~3은 여기 이어서 (다음 턴)

- 히든 이벤트 3개(`Hidden_Aokiji`·`Eternal_Lucci`·`IM_dragon`) 각각의 달성
  조건, 한 판에 셋 다 가능한지(`Tech_Onedill`류 상호배제 게이트 여부).
- "패왕의길" 항법(+2)과 히든 이벤트가 같은 판에서 합산되는지.
- **구현 관점**: 지금 이 축은 "player가 스킬(A0LZ류가 아니라 일반 스킬
  대미지)로 적을 때릴 때, 그 라운드의 모든 일반 적에게 걸리는 개별
  취약도"다 — 이미 `EnemyData.percentDamageTaken`이 이 정확한 자리를
  프로퍼티로 감싸놨다는 걸 확인했다(`EnemyDummy.PercentDamageTakenMultiplier`,
  기존 주석이 이미 "Trig_Hidden9 +2 등"이라고 이 정확한 메커니즘을 추측해
  뒀었다 — 방향이 맞았다). **구현 방식은 새 필드가 아니라 기존
  `percentDamageTaken` 계산에 `(Damage_level_Fixed÷20)`을 더하는 런타임
  누적으로 붙이면 된다**(정확한 계수는 위 공식대로 `0.05×증가레벨`).
