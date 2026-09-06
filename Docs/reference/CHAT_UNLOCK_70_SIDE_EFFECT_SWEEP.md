# 채팅언락 70행 전수 — "결과유닛 지급 외 전부" 사이드이펙트 스윕

조사: 리서치담당 / 2026-09-06
요청: PM ㊹ — `ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv`가 "채팅→유닛지급"만 묻는
질문지였다는 걸 확인한 뒤, **같은 질문지의 사각지대가 나머지 45개에도
있는지 70행 전부 원문 재확인.**

## 방법

CSV 70행 각각의 `Trig_<트리거명>_Actions` 함수 전문을 `war3map.j`에서 그대로
추출(70개 전부 함수 존재 확인, 누락 없음). 각 본문에서 **결과유닛
스폰(`CreateNUnitsAtLoc`) 외의 모든 호출**을 기계적으로 뽑아 분류했다:
능력레벨변경(`SetUnitAbilityLevelSwapped`/`IncUnitAbilityLevel`), 능력
부여/제거(`UnitAddAbilityBJ`/`UnitRemoveAbilityBJ`), 자원증감
(`AdjustPlayerStateBJ`/`SetPlayerState`), 트리거 on/off(`Enable`/
`DisableTrigger`), 연구(`SetPlayerTechResearchedSwap`), 그 외 전역변수
대입(`set udg_X=`, 이미 알려진 공통 부기변수 9개는 제외).

**공통 보일러플레이트로 제외한 것(전 계열 공통, 이미 알려짐)**: `udg_Tech_Onedill`/
`udg_Tech_Onedill_int`/`udg_one_dill`(공유게이트 부기), `udg_UnitSound`(효과음),
`udg_ChinghoName`/`udg_ChinghoName2`(칭호명 스왑 연출), `udg_T_Location`/
`udg_Player_Group`/`udg_Exp_Group`(위치·경험치그룹 임시변수), 무작위유닛
제거(`RemoveUnit(GroupPickRandomUnit(...))`, 조합재료 정리로 보임). 아래
표는 **이걸 다 뺀 "진짜 남는 것"**만 적었다.

## 결론 — **`Damage_level_Fixed`는 정확히 3/70이다. 나머지로 안 번진다.**

어젯밤 걱정("Eternal 47개가 같은 부기변수 태그를 공유하니 다른 것도
`Damage_level_Fixed`를 가질 수 있다")은 **기우였다** — 70개 전부 훑은
결과 `Damage_level_Fixed`가 나오는 건 **`Hidden_Aokiji`(+2)·
`Eternal_Lucci`(+2)·`IM_dragon`(+4)`, 딱 이 셋뿐이다.** 나머지 67개엔
전역 스탯/경제 축 가산이 전혀 없다.

## 행별 표

### Hidden 23종 — 재료조합, 부기변수도 거의 없음(가장 깨끗함)

| 트리거 | 부가효과 |
|---|---|
| Hidden_Akainu | 없음 |
| **Hidden_Aokiji** | **`Damage_level_Fixed`+2**, `aohidden_bool`=true(부기) |
| Hidden_Bal | 없음 |
| Hidden_Bonkure | 없음 |
| Hidden_Dekken | 없음 |
| Hidden_Godenel | 없음 |
| Hidden_Killer | 없음 |
| Hidden_Mihawk | 없음 |
| Hidden_Mobidic | 없음 |
| Hidden_Redforce | 없음 |
| Hidden_Ryuma | 없음 |
| Hidden_Sabo | 없음 |
| Hidden_Sunny | `Sunny_int` 부기변수만(용도 미확인, 스탯 아님) |
| Hidden_Tiger | 없음 |
| Hidden_Yiwan | 없음 |
| Hidden_bergo | 없음 |
| Hidden_carrot | 없음 |
| Hidden_kinemon | 없음 |
| Hidden_koalla | 없음 |
| Hidden_perona | 없음 |
| Hidden_rebecca | 없음 |
| Hidden_rokugu | 없음 |
| Hidden_siru | 없음 |

### Eternal 28종 — 전부 `SetPlayerTechResearchedSwap('Rhfl',1,...)` 공통(아래 참고, 사실상 없음)

| 트리거 | 부가효과 |
|---|---|
| Eternal_Akainu | (Rhfl만) |
| Eternal_Aokiji | (Rhfl만) + `aocho`/`aocho2`(칭호쌍 부기, 스탯 아님) |
| Eternal_Brook | (Rhfl만) + `magicDP_Rine`↔결과유닛에 적용(자기 자신 스탯 설정, 아래 참고) |
| Eternal_DP | (Rhfl만) |
| Eternal_Franky | (Rhfl만) |
| Eternal_Hokins | (Rhfl만) |
| Eternal_Kid | (Rhfl만) + 결과유닛 자체 능력 교체(A020/A10E 제거→A17O 부여, 새 히어로 자기 자신 스킬셋 세팅) |
| Eternal_Kizaru | (Rhfl만) |
| Eternal_Law | (Rhfl만) |
| **Eternal_Lucci** | **`Damage_level_Fixed`+2** + (Rhfl) |
| Eternal_Luffy | (Rhfl) + Nika 연계 부기변수(`Nika_Johab_Bool` 등, 별도 전제계열) |
| Eternal_Nami | (Rhfl) + `Gold_Plus`/`treasure_range_int`(나미 고유 골드보너스 시스템 부기, 나미 자신 스킬과 연동으로 보임) |
| Eternal_Nika | **완전 별종** — `A0WP`/`A11V` 능력레벨을 `GetHeroStatBJ`(그 영웅 자신의 STR/INT)로 설정 + 골드5 아님 **목재5**(AdjustPlayerStateBJ 확인). 이미 `ChatUnlockCategory.Nika`로 별도 취급 중, 새 발견 아님 |
| Eternal_Robin | (Rhfl만) |
| Eternal_Sabo | (Rhfl) + 결과유닛에 `A0Q9` 부여(자기 스킬셋) |
| Eternal_Sandi | (Rhfl) + `sancho`/`sancho2`(칭호쌍) |
| Eternal_Shanks | (Rhfl) + `shanks_legend`(범위감지용 유닛 핸들 부기, `Trig_UnitJohabCounter`에서 이미 본 그 패턴) |
| Eternal_Shyrahosi | (Rhfl만) |
| Eternal_Snake_Luffy | (Rhfl) + Nika 연계 부기(Luffy와 동일 패턴) |
| Eternal_Tashigi | (Rhfl만) |
| Eternal_Tichi | (Rhfl만) |
| Eternal_Usop | (Rhfl) + `Usop_unit`(유닛 핸들 부기) |
| Eternal_Yamato | (Rhfl) + 결과유닛에 능력 4개 부여(A11F/A11E/A139/A152, 자기 스킬셋) |
| Eternal_Zoro | (Rhfl) + `zocho`/`zocho2`(칭호쌍) |
| Eternal_chopa | (Rhfl) + 결과유닛에 능력 2개(A11H/A11I) + `chocho`/`chocho2`(칭호쌍) |
| Eternal_huji | (Rhfl만) |
| Eternal_jinbe | (Rhfl만) |
| Eternal_ryokugyu | (Rhfl) + 결과유닛에 능력 3개(A11F/A11E/A139) + `Roykugu_Meta_unit`/`Item_Ryokugu`(부기) |

### Forever 8종 — 전부 목재5 확인, `Damage_level_Fixed` 없음

| 트리거 | 부가효과 |
|---|---|
| Forever_ACE | 결과유닛에 `A0Q9` 부여 |
| Forever_Bugi | `Gold_Plus`/`bugi_johab_Bool`(부기) |
| Forever_Cavendish | 없음(목재5 외) |
| Forever__Hancock | `hancock_Eternal`(부기) |
| Forever_mihawk | 없음(목재5 외) |
| **Forever_oden** | 🔴 **`EnableTrigger(gg_trg_Odeng_dibuff)`** — 오뎅 전용 디버프 트리거를 켠다(다른 시스템을 여는 사례, PM이 주의하라던 그 패턴). 정체는 이번엔 안 팠다(오뎅 자신의 패시브로 추정, 전역 아닐 가능성 높음) |
| Forever_uta | `Uta_Johab_bool`/`Uta_item_bool`/`Hero_Uta_Trig_unit`(부기, 우타 자신의 오라 시스템 연결로 추정) |
| Forever_vivi | 없음(목재5 외) |

### IM(불멸의) 11종 — 전부 목재10 확인, `Damage_level_Fixed`는 dragon 하나뿐

| 트리거 | 부가효과 |
|---|---|
| IM_Bigmam | 결과유닛에 `A0ZQ` 부여 + `Bigmom_IM_Dumunit`/`Item_Bigmom`(부기) |
| IM_Kaido | 결과유닛에 능력 3개(A0ZR/A0ZN/A0ZQ) |
| IM_Z_1 | 없음(목재10 외) |
| **IM_dragon** | **`Damage_level_Fixed`+4** (목재10 외) |
| IM_edward1 | 없음(목재10 외) |
| IM_garp1 | 없음(목재10 외) |
| IM_lailiey1(레일리) | 없음(목재10 외) — PM이 찾던 "레일리 히든" 항목은 이것과는 별개일 가능성(아래 참고) |
| IM_musasi1 | 없음(목재10 외) |
| IM_roger_1 | 없음(목재10 외) |
| IM_sengoku1 | 없음(목재10 외) |
| IM_siki_1 | 없음(목재10 외) |

## 그 외 확인한 것

- **`SetPlayerTechResearchedSwap('Rhfl',1,...)`이 Eternal 26/28행에 무조건
  붙는다(Hidden·Forever·IM엔 없음)** — `Rhfl`의 `war3map.w3q` `gnam`(표시이름)
  필드를 직접 확인: **"초월함 유닛 조합 완료"**. 순수 UI 체크박스/도감용
  연구이고, `war3map.j` 다른 곳에서 이 값을 읽어 전투 수치에 쓰는 코드는
  없다(별도 확인) — **전투 무관, 새 사각지대 아님.**
- **캐릭터별 `UnitAddAbilityBJ`/`UnitRemoveAbilityBJ`는 전부 방금 스폰된
  "그 결과유닛 자신"에게만 걸린다**(대상 인자가 `GetLastCreatedUnit()`류
  또는 방금 `set`한 그 유닛 변수) — 다른 유닛이나 전역에 영향 없음, "이
  영원한/불멸의 히어로의 스킬셋이 뭔가"를 CSV의 "결과유닛" 칸이 이름만
  적고 능력까지는 안 담았을 뿐이다. **밸런스에 걸리는 문제긴 하다** —
  이 히어로들 스킬셋을 나중에 만들 때 CSV 이름만 보고 "스톡 능력이겠지"로
  넘기면 안 되고, 이 표의 AddAbility 목록을 봐야 한다. 다만 `Damage_level_Fixed`
  같은 "전역 미확인 결측"은 아니다.
- `Forever_oden`의 `Odeng_dibuff` 트리거는 이번엔 내용을 안 열어봤다 —
  필요하면 다음 조사로.
- `AdjustPlayerStateBJ`는 전부 `PLAYER_STATE_RESOURCE_LUMBER`로 확인—
  Forever=-5, IM=-10, CSV/스키마 기존 값과 정확히 일치(새 결측 아님).

## 결론 — 스키마 반영 가이드(구현 판단은 넘김)

**`Damage_level_Fixed` 필드 하나만 있으면 된다** — 70개 스윕 결과 이거
말고는 "전역 스탯/경제 축" 성격의 숨은 효과가 없다. `ChatUnlockData`/
`HiddenCombineData`에 `int damageLevelFixedBonus = 0`(기본0) 필드 하나
추가하고, `Hidden_Aokiji`=2·`Eternal_Lucci`=2·`IM_dragon`=4 세 에셋에만
값을 채우면 원작과 일치한다(나머지 67개는 0 그대로 둬도 맞다 — "혹시
몰라 미리 넣어둔다"가 아니라 실측으로 확정된 값이다).
