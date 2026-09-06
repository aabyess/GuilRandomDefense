# 특성강화 26명 전수 — `Trig_T_Ability_hero_Actions` 원문 그대로

조사: 리서치담당 / 2026-09-06
요청: PM — 「매칭실패 4명 말고 26명 전부. 비용·능력ID·부수변수·효과 종류
빠짐없이. 「능력 레벨을 올린다」로 보여도 실제론 다른 변수를 건드리는
경우가 있으니 부수변수를 놓치지 마라」

## 결론 — **26명 전부 원작에 값이 있다. 지어낼 필요가 없다.**

`Trig_T_Ability_hero_Actions`(캐스터의 유닛타입으로 26가지 분기, 각각
캐스트 즉시 실행) 원문을 전부 열었다. **패턴이 균일하지 않다** — 단순
"능력 레벨+1"이 아니라 최소 **6가지 다른 형태**가 섞여 있다: 능력레벨업
(다수) / 능력 신규부여(다수) / 완전 변신(유닛 자체를 다른 오브젝트로
교체) / 전역효과(전체 플레이어의 다른 건물에 적용) / 반복구매형(1회
제한 없음) / 스탯 직접상승(레벨업 아님).

## 전체 26명 표

| 캐릭터 | 유닛ID | 비용 | 효과 | 부수 변수/특이사항 |
|---|---|---|---|---|
| 검은수염(치치) | `H090` | 2 | `IncUnitAbilityLevelSwapped('A0D0')` | 🔴 `udg_Tichi_TR_AddInt`=**3**(원래 기본6 — 이게 진짜 효과, 능력레벨업은 부수적) |
| 키드 | `H0B4` | 3 | `IncUnitAbilityLevelSwapped('A10F')` | `udg_bool_kid_tr`=true |
| 프랑키 | `H08Y` | 1 | `IncUnitAbilityLevelSwapped('A069')` | `udg_bool_franky_tr`=true |
| 루치 | `H08W` | 2 | `IncUnitAbilityLevelSwapped('A0R2')` | `udg_bool_lucchi_tr`=true |
| 후지토라 | `H08X` | 2 | `IncUnitAbilityLevelSwapped('A0GR')` | 없음(단순 능력레벨업) |
| 로우 | `H096` | 1 | `UnitAddAbilityBJ('A10S')` + `UnitRemoveAbilityBJ('A0I3')`(능력 교체) | `udg_Law_Bool`=true |
| 나미 | `H08V` | 2 | `UnitAddAbilityBJ('A107')` + 더미유닛 `h0AJ` 소환·자신에게 이동 | `udg_NamiT_Bool`=true |
| 시라호시 | `H08U` | 2 | `SetUnitAbilityLevelSwapped('A0EQ',+1)` | 이 구매버튼 자체를 `SetPlayerAbilityAvailableBJ(false,...)`로 재구매 차단(1인1회 별도 잠금) |
| 호킨스 | `H094` | 2 | `UnitAddAbilityBJ('A0WK')` + 더미유닛 `h08L` 소환·자신에게 이동 | 이 구매버튼도 `SetPlayerAbilityAvailableBJ(false,...)`로 재구매 차단 |
| 도플라밍고 | `H09E` | 3 | `SetUnitAbilityLevelSwapped('A0AL',+1)` | 이 구매버튼도 `SetPlayerAbilityAvailableBJ(false,...)` |
| 아오키지 | `H097` | 2 | 🔴 **완전 변신** — `RemoveUnit`+`CreateNUnitsAtLoc('H0B1',...)`, 경험치·STR/AGI/INT 이월, `A0WP`(INT비례,실패시31)·`A11V`(STR비례,실패시35) 스탯비례 능력세팅 | **Eternal_Nika와 완전히 같은 패턴**(별개 조사에서 이미 특수취급한 그 구조) |
| 타시기 | `H05N` | 1 | 🔴 **스킬 아니라 스탯** — `AddHeroXPSwapped(5000)` + STR/AGI/INT 각 **+3** | 능력 레벨업이 전혀 없다, 순수 스탯형 유일 사례 |
| 루피(레일리의 제자) | `H099` | 3 | `UnitRemoveAbilityBJ('A0KD')`+`UnitAddAbilityBJ('A0JU')`(능력교체) | `UnitAddTypeBJ(UNIT_TYPE_SAPPER)` — 유닛타입 자체가 바뀐다 |
| 로빈 | `H098` | 2 | `UnitAddAbilityBJ('A0FL')`+`UnitAddAbilityBJ('A0ZP')`, **영구화**(`UnitMakeAbilityPermanent`) | 🔴 대상이 캐스터 자신이 아니라 `GetSpellTargetUnit()` — **다른 유닛에게 거는 능력**(팀원 버프로 보임) |
| 우솝(G.O.D) | `H09B` | 3 | 🔴 **전역효과** — 루프로 **4명 전원**의 `udg_Manso[]`(도움소 건물)에 `IncUnitAbilityLevelSwapped('A0IE')` | `SetPlayerTechResearchedSwap('R017', 카운트+1,...)` + 캐스터 INT+5 — **자신이 아니라 전체 플레이어 공용 건물에 영향** |
| 초파 | `H091` | 2 | 🔴 **완전 변신** — `RemoveUnit`+`CreateNUnitsAtLoc('H093',...)`, 경험치·스탯 이월, `A0WP`/`A11V` 동일 패턴 | 아오키지와 같은 "Nika형" |
| 아카이누 | `H095` | 1 | 🔴 **반복구매형** — `SetUnitAbilityLevelSwapped('A0HG',+1)`은 항상 실행, **최초 1회만** 구매버튼 제거, 이후엔 매번 `IncUnitAbilityLevelSwapped(GetSpellAbilityId())`+`SetUnitUserData(+1)` | **26명 중 유일하게 "1회성"이 아니라 여러 번 살 수 있는 구조**(UserData가 구매 횟수 카운터) |
| 브룩 | `H09I` | 2 | `UnitAddAbilityBJ('A0R1')` | `ConditionalTriggerExecute(gg_trg_Brook_Skill_3)` — 구매 즉시 스킬3을 강제 발동 |
| 샹크스 | `H08Z` | 2 | `UnitAddAbilityBJ('A136')`+`UnitAddAbilityBJ('A0XO')` | `ConditionalTriggerExecute(gg_trg_Shanks_skill_5)` — 구매 즉시 스킬5 강제 발동 |
| 센고쿠 | `h04E` | 2 | `IncUnitAbilityLevelSwapped('A0D8')` + `UnitAddAbilityBJ('A0WT')` | 없음 |
| 제트(Z) | `h04G` | 1 | `SetUnitAbilityLevelSwapped('A09E',+1)` | 없음 |
| 거프 | `h04C` | 2 | `IncUnitAbilityLevelSwapped('A0GY')` | 없음 |
| 드래곤 | `h04D` | 3 | `IncUnitAbilityLevelSwapped('A0FS')` | 없음 |
| 시키 | `h04B` | 2 | `UnitAddAbilityBJ('A0T4')` | 없음 |
| 레일리 | `h049` | 3 | `IncUnitAbilityLevelSwapped('A0ES')` | 없음 |
| 한콕 | `h05C` | 3 | `IncUnitAbilityLevelSwapped('A0IN')` | 없음 |

## 요약 통계

- **비용 분포**: 1(4명: 프랑키·로우·타시기·아카이누·Z — 5명), 2(13명), 3(8명) — **균일하지 않다, 일괄가정 금지가 맞았다.**
- **"부수변수 있음"이 10/26** — `Tichi_TR_AddInt`처럼 확률식 자체를 바꾸는
  숨은 변수(치치)부터, 단순 부기 bool(키드·프랑키·루치·로우·나미), 다른
  건물 전역효과(우솝), 유닛타입 변경(루피), 재구매잠금(시라호시·호킨스·
  도플라밍고), 반복구매카운터(아카이누)까지 형태가 제각각이다.
- **"완전 변신"형 2건**(아오키지·초파) — 어제 Eternal_Nika 조사에서
  "완전 별종"이라 특수취급했던 게, 사실 최상위 26명 특성강화 안에서는
  드문 패턴이 아니라 **최소 3건(Nika 포함)의 공유 구조**였다.
- **"자신이 아니라 남에게/전체에게" 거는 효과 2건**(로빈=대상유닛,
  우솝=전체 플레이어 도움소) — 캐스터 자기강화로만 가정하면 이 둘이 깨진다.

## 대조 — PM의 "사장님 07번 확정" 재검토용

사장님 07번은 "특성 9종 효과는 우리가 등급에 맞춰 지어내라"였다.
**이 26명은 이미 원작에 정확한 값이 있으므로 지어낼 대상이 아니다** —
`Trait_*.asset` 239개 중 이 26명분은 위 표 그대로 옮기고, **남은 213개**
(최상위 26명 밖, `traits-are-per-character` 메모리가 이미 "239종 배선은
근거 없음, 26명 확정"이라 닫아둔 그 나머지)만 07번의 "지어내라" 허가가
유효하다 — **이 26명분에 대해서는 그 허가가 애초에 필요 없었다.**
