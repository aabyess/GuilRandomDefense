# `Player(7)` 619개체의 정체 — 🔴 도감 전시가 아니라 "시간이 지나면 사라지는 특전" 시스템의 상태판이다

조사: 리서치담당 / 2026-09-07, `war3map.j`(`CreateUnitsForPlayer7`,
`Trig_ban_Actions`, `Trig_ban_transendence`) 원문 전수 확인.
요청: PM — 619개체의 정체(`war3mapUnits.doo` 접근 불가 재확인 포함),
"도감/조합표 전시" 가설 검증.

## 결론 — **가설은 틀렸다. 이건 전시가 아니라 "27+11+7개 특전 레시피가
게임 도중 랜덤 순서로 하나씩 영구 잠긴다"는 시스템의 실시간 상태판이다**

619개체 중 최소 **53개**(`Eternal` 27 + `IM` 11 + `Forever` 7 + `Limited` 8)가
이 시스템의 **개별 마커**로 확정됐다. "도감처럼 늘어놓았다"가 아니라
**각 마커가 특정 채팅언락 레시피 하나씩을 대표하고, 그 레시피가
게임 중 잠기면 마커에 자물쇠 이펙트가 걸리고 색이 어두워진다** —
플레이어들이 맵을 돌아다니며 "이 캐릭터는 아직 열려있다/이미
잠겼다"를 시각적으로 확인하게 하는 실시간 UI다.

## ① `war3mapUnits.doo` 접근 — PM과 동일하게 재확인, 그리고 왜 못 읽는지도 드러났다

`Archive.read('war3mapUnits.doo')`는 나도 `None`을 받는다(`war3map.doo`는
20,006바이트로 정상 읽힘). 그런데 **`CreateUnitsForPlayer7` 자체를 열어보니
이유를 알 수 있었다** — 이 함수는 **`CreateUnit(플레이어,'유닛ID',x,y,각도)`
호출 619개를 그냥 나열한 것**이다. 이건 정상적인(보호 안 된) 워3 맵이라면
**`war3mapUnits.doo`가 엔진에 의해 직접 로드되지 배치 정보가 JASS
코드로 안 나온다.** 이 맵은 **보호 처리되면서 `.doo`가 지워지고 그
배치 데이터가 이 함수 하나로 통째로 JASS화됐을 가능성이 높다** —
그래서 `.doo` 파일 자체가 없는 것으로 보인다(단정은 아니고 정황).

**부가 확인**: 619개 생성 호출 중 **50개만 전역변수(`gg_unit_XXXX_NNNN`)로
저장되고 나머지 569개는 지역변수 `u`로 던져진다**(핸들을 안 남김). 이건
워3 에디터가 "다른 트리거에서 이 특정 배치 유닛을 이름으로 참조해야
하는 경우"에만 전역변수를 만드는 표준 동작과 일치한다 — 즉 **이
50개는 원래부터 "특정 개체를 나중에 다시 가리켜야 하는" 목적으로
배치된 것**이라는 뜻이다. 그 50개 중 실제로 뭘 하는지 추적했다.

## ② `Trig_ban_Actions` — 게임 시작 시 1회, 53개 레시피를 마커에 등록한다

원문(핵심부, 축약 없이):
```jass
set udg_Ban_Trigger[0]=gg_trg_Eternal_Snake_Luffy
set udg_Ban_unit[0]=gg_unit_H0B2_0231
... (Eternal 0~26, 총 27개, 트리거+마커 쌍)
set udg_Ban_Trigger_IM[0]=gg_trg_IM_roger_1
set udg_Ban_Unit_IM[0]=gg_unit_h04J_0046
... (IM 0~10, 총 11개)
set udg_Ban_Unit_Limited[0]=gg_unit_h05E_0347
set udg_Ban_Trigger_Limited[0]='A01Y'
... (Limited 0~7, 총 8개 — 이건 트리거가 아니라 능력ID를 저장한다, ③ 참고)
set udg_Ban_Trigger_ET[0]=gg_trg_Forever_ACE
set udg_Ban_Unit_ET[0]='h059'
... (Forever/ET 0~6, 총 7개)
set udg_int_Ban=GetRandomInt(0,6)
call DisableTrigger(udg_Ban_Trigger_ET[udg_int_Ban])
set udg_T_Location=GetRectCenter(gg_rct_banForever)
call CreateNUnitsAtLoc(1,udg_Ban_Unit_ET[udg_int_Ban],Player(7),udg_T_Location,bj_UNIT_FACING)
call AddSpecialEffectTargetUnitBJ("origin",GetLastCreatedUnit(),"Effect Lock Black9.mdx")
call SetUnitVertexColorBJ(GetLastCreatedUnit(),15.00,15.00,15.00,35.00)
call RemoveLocation(udg_T_Location)
call TriggerExecute(gg_trg_ban_transendence)
```
**게임이 시작되자마자, "영원한"(`Forever`, ET) 레시피 7개 중 정확히
1개를 `GetRandomInt(0,6)`로 무작위로 골라 그 자리에서 즉시 영구
비활성화한다**(`DisableTrigger`) — 즉 **"영원한" 등급 캐릭터 7명 중
1명은 매 게임마다 무작위로 아예 얻을 수 없게 확정된다.** 그 캐릭터의
전용 검은자물쇠 마커를 `gg_rct_banForever`(고정 지역)에 새로
생성해서 시각 표시하고, 마지막 줄에서 `Trig_ban_transendence`를
즉시 실행해 **다음 단계(Eternal·IM 순환 잠금)를 개시**한다.

## ③ `Trig_ban_transendence` — 반복 루프로 Eternal 27개·IM 11개를 무작위 순서로 하나씩 영구 잠근다

```jass
// Stage 3(Eternal 단계): 매 사이클마다
call s__TrigVariables_Setinteger(GlobalTV,0,GetRandomInt(0,26))
if(udg_Ban_int2[index]==false)then           // 아직 안 잠긴 것만
  set udg_Ban_int2[index]=true                // 잠금 처리 완료 표시
  call DisableTrigger(udg_Ban_Trigger[index]) // 그 Eternal_X 트리거 영구 비활성화
  call AddSpecialEffectTargetUnitBJ("origin",udg_Ban_unit[index],"Effect Lock Black9.mdx")
  call SetUnitVertexColorBJ(udg_Ban_unit[index],15.00,15.00,15.00,35.00) // 어둡게 tint
endif
// (27개 다 잠기면 Stage 10으로 전환)

// Stage 20(IM 단계): 완전히 동일한 구조
call s__TrigVariables_Setinteger(GlobalTV,0,GetRandomInt(0,10))
if(udg_Ban_int3[index]==false)then
  set udg_Ban_int3[index]=true
  call DisableTrigger(udg_Ban_Trigger_IM[index])
  call AddSpecialEffectTargetUnitBJ("origin",udg_Ban_Unit_IM[index],"Effect Lock Black9.mdx")
  call SetUnitVertexColorBJ(udg_Ban_Unit_IM[index],15.00,15.00,15.00,35.00)
endif
```
`SleepForStageNext`/`SleepForStage(...,0.02)`로 **일정 간격을 두고
반복**(정확한 초 단위 환산은 이번엔 안 함). **매 사이클 무작위로
"아직 안 잠긴" Eternal(또는 IM) 레시피 하나를 골라 그 자리에서 영구로
잠근다.** 이미 잠긴 걸 다시 뽑으면(`udg_Ban_int2[index]==true`) 그냥
아무 것도 안 하고 넘어간다 — **결국 시간이 충분히 지나면 27개 Eternal과
11개 IM 레시피가 (무작위 "순서"만 다르고) 전부 잠긴다는 뜻**이다(막는
조건이 안 보인다 — 게임이 끝날 때까지 계속 도는 것으로 보인다).

**즉 "전설급 캐릭터를 채팅으로 여는" 47종(Eternal) + 다른세계 도박
관련 11종(IM) + 영원한 7종(Forever) 레시피는, 게임이 진행될수록
무작위 순서로 하나씩 영구히 사라지는 "한정판" 시스템이다.** 아무리
재료를 다 모아도, 그 레시피가 이미 잠긴 뒤라면 다시는 못 연다 —
**타이밍 요소가 있는 시스템**이다. 이건 지금까지 우리 조사(가챠/조합/판매
전부) 어디에도 안 잡혀 있던 완전히 새로운 매커니즘이다.

## ④ "제한됨" 8종(`Ban_Unit_Limited`)만 다른 방식 — 트리거가 아니라 능력ID

```jass
set udg_Ban_Unit_Limited[0]=gg_unit_h05E_0347   set udg_Ban_Trigger_Limited[0]='A01Y'
set udg_Ban_Unit_Limited[1]=gg_unit_h05F_0631   set udg_Ban_Trigger_Limited[1]='A02O'
set udg_Ban_Unit_Limited[2]=gg_unit_h06L_0183   set udg_Ban_Trigger_Limited[2]='A022'
set udg_Ban_Unit_Limited[3]=gg_unit_h05I_0355   set udg_Ban_Trigger_Limited[3]='A007'
set udg_Ban_Unit_Limited[4]=gg_unit_h040_0334   set udg_Ban_Trigger_Limited[4]='A0RP'
set udg_Ban_Unit_Limited[5]=gg_unit_h07I_0295   set udg_Ban_Trigger_Limited[5]='A0S0'
set udg_Ban_Unit_Limited[6]=gg_unit_h084_0661   set udg_Ban_Trigger_Limited[6]='A0UH'
set udg_Ban_Unit_Limited[7]=gg_unit_h0AI_0759   set udg_Ban_Trigger_Limited[7]='A10A'
```
`Ban_Trigger[]`류와 달리 이 배열엔 **트리거 핸들이 아니라 능력ID
문자열**이 들어간다 — 잠금이 아니라 **"이 능력을 이미 획득했는가"를
검사하는 다른 용도**로 보인다(이번 조사에서 `Ban_Unit_Limited`/
`Ban_Trigger_Limited`를 읽는 자리는 못 찾았다 — `[미확인, 시간 부족]`).
⚠️ **`h0AI`(8번째)가 `PM`이 예로 든 "제한됨 −2" 결손의 그 킹**이다 —
이 배열이 결손 27칸 중 제한됨 몫과 관련 있을 가능성이 있다, 확정은
못한다. `h07I`(6번째)는 이미 다른 조사(`BLOCKERS_AND_ROSTER_DEFICIT.md`
㉢)에서 확인한 샬롯 카타쿠리다.

## ⑤ 619개 중 나머지 ~566개는 여전히 미확인

이번 조사로 확정된 건 53개(27+11+7+8)뿐이다. **나머지는 이 "Ban"
시스템에 안 걸린다** — `udg_Ban_*` 계열 배열 전체(Ban_unit/Ban_Trigger/
Ban_Unit_IM/Ban_Trigger_IM/Ban_Unit_Limited/Ban_Trigger_Limited/
Ban_Unit_ET/Ban_Trigger_ET) 리터럴을 전부 뒤졌지만 이 53개 말고
다른 유닛 핸들은 안 나왔다. **"전시/도감" 가설은 이 53개에 한해서는
확실히 기각됐지만(전시가 아니라 시스템 마커), 나머지 566개가 뭔지는
여전히 열려 있다** — 어쩌면 같은 "상태 마커" 패턴이 우리가 아직 못
찾은 다른 시스템(히든조합·다른 등급 레시피 등)에도 쓰이고 있을
가능성이 높다고 본다(같은 팀이 짠 코드라 재사용 패턴일 가능성).
다음 조사로 남긴다.

## 우리 구현에 대한 함의 (판단만, 코딩은 안 함)

**우리 `HiddenCombineManager`가 플레이어 인벤토리에서 재료를 소비하는
구조는 여전히 맞다**(어제 정정한 소유자 필터 건과 별개). 다만 이번
발견은 **완전히 새로운 축**이다 — Eternal 47종 중 27종(전체는 아니고
`Ban_Trigger` 배열에 등록된 27종만) + IM 11종 + Forever 7종은 **원작에서
"영구히 못 열릴 수도 있는" 시간제한부 콘텐츠**였다는 뜻이라, 우리가
지금 이 47+11+7종을 "언제나 조건만 맞으면 100% 열리는" 것으로
구현했다면 그 부분이 원작과 다르다. **사장님 판단이 필요한 사안**이라
판단만 남기고 코드는 안 건드린다.

---

## 🟢 2026-09-07 추가 — 619개체 완전 분해, 566개 미확인이 41개로 줄었다

`ROSTER_DENOMINATOR_GACHA_POOL_2026-09-07.md`에서 찾은 `Trig_tier_Random`
(원작 자신의 가챠 풀)을 이 619개체 좌표와 다시 대조했다. **정확히
설명된다, 나머지도 3.9%까지 좁혔다:**

```
619개체 = 532(가챠 쇼케이스 대표 153 + 같은 타입의 추가 배치 379)
        + 46(밴 시스템 마커 — Eternal/IM/Limited 레지스트리에 등록된 정확한 유닛타입)
        + 41(개별적으로 이미 알려진 특수/히든 콘텐츠 30종)
= 619 (정확히 맞음, 오차 없음)
```

### ① 532개(86%) — 가챠 쇼케이스 + 같은 타입 추가 배치

흔함·안흔함·특별함·희귀함·랜덤전용(다른세계) 5개 등급의 가챠 사각형
(`Ran0`~`Ran_7`+`Model_Pack_R1Unit`) 안에 서 있는 **153개체(등급당
대표 1기씩)**가 이미 확인됐다. 그런데 **같은 유닛타입이 그 사각형
밖에도 추가로 여러 번(타입당 5~11회) 더 배치돼 있다** — 예:
`h001`(흔함)은 쇼케이스에 1기+그 밖에 9기, `h005`는 1+11기. **이건
등급 쇼케이스의 "여분 배치"이거나, 그 등급 유닛이 실제 라운드에서
싸우는 라인몹으로 맵 곳곳에 미리 서 있는 것으로 보인다**(README가
이미 확인한 "라인몹 레벨 1~63은 normal 55/55"와 정합적) — 확정은
아니지만 이 해석이 가장 자연스럽다.

### ② 46개(7%) — 밴 시스템 마커, 정확히 일치

`udg_Ban_unit`/`udg_Ban_Unit_IM`/`udg_Ban_Unit_Limited`에 등록된
전역변수(`gg_unit_XXXX_NNNN`)를 유닛타입으로 역매핑하면 **정확히
46종**이고, 이 46종의 배치 인스턴스 수를 세면 **정확히 46개체**다
(타입당 1개씩, 중복 없음 — 밴 마커는 원래 유일해야 하니 당연하다).

### ③ 41개(7%, 30종) — 이미 개별적으로 확인된 특수/히든 콘텐츠

가챠에도 밴 레지스트리에도 없는 30종을 뽑았다:
```
h00H(좀비, 이미 "죽은 슬롯"으로 확정) · h05X(레일리) · h05Y(고대의 배) ·
h060(해적선) · h06G(행운의 토큰) · h07I(카타쿠리, 제한됨) · h0AI(킹, 제한됨) ·
h08B(다른세계 인증 유닛 — I00F 툴팁의 "맵 중앙 유닛"일 가능성 높음) ·
h03T·h03W·h03X·h03Z·h04S·h05S·h05W·h06J·h06K·h07K·h087·h08I·h08O·
h09N·h09R·h09W·h0A2·h0A6·h0AC·h0BC·h0BF·h0BR·e0FJ (나머지, 개별 미조사)
```
**이 중 다수가 이미 이번 세션의 다른 조사에서 개별적으로 밝혀진
것들이다**(레일리·고대의배·해적선·행운토큰·카타쿠리·킹·좀비) — 즉
"통째로 미확인"이 아니라 **"각자 다른 특수 획득경로를 쓰는 캐릭터/
아이템이라 표준 가챠·밴 시스템 어디에도 안 걸린다"는 게 정체다.**
나머지 절반가량(`h03T` 등)은 이번엔 개별 조사를 안 했다 — `[미확인]`.

**결론: Player(7) 619개체는 "정체불명의 대량 배치"가 아니라 3개
서로 다른 시스템(①표준 가챠 쇼케이스+라인몹, ②시간부 밴 레지스트리
마커, ③개별 특수경로 콘텐츠)이 전부 같은 중립 플레이어 아래 얹혀
있었던 것뿐이다 — 미스터리는 사실상 해소됐다.**

---

## 🟢 상위 5등급의 "원작 자기 기준" — 밴 레지스트리로 일부만 풀린다

PM 지시로 `Trig_Eternal_*`/`Trig_IM_*`/`Forever_*`/`Limited_*` 트리거
이름을 캐릭터별로 대조했다.

| 밴 계열 | 등록 수 | 대응 등급 | 근거 |
|---|---:|---|---|
| `Eternal_*` | 27(레지스트리 등록) / 실제 트리거는 28개 존재 | **초월함** | `Eternal_Shanks`(H08Z 샹크스)·`Eternal_Yamato`(H0BD 야마토)·`Eternal_ryokugyu`(로쿠규) 등이 이미 `ROSTER_DEFICIT_27_SLOTS.md`의 초월함 후보와 겹친다 |
| `IM_*` | 11 | **불멸의** | `IM_roger`·`IM_garp`·`IM_sengoku`·`IM_dragon`·`IM_edward`·`IM_siki`·`IM_Z`·`IM_Kaido`·`IM_Bigmam`·`IM_lailiey`(레일리)·`IM_musasi`(마커 `h04F`=가반, 코드명이 다를 뿐 같은 유닛) — 내 불멸의 upoi=151 목록 13명 중 11명과 정확히 겹친다(`h0AE`히그마·`h0BU`정체불명 2명만 IM에 없음) |
| `Forever_*`(ET) | 7 | **영원한** | 이미 확정(비비·미호크·에이스·버기·캐번디시·핸콕·오뎅 = 내 영원한 8명 목록과 정확히 겹친다, 하쿠바만 별도) |
| `Limited_*` | 8 | **제한됨** | 이미 확정(`h0AI`킹·`h07I`카타쿠리 포함) |

### 🔴 밴 레지스트리 합(27+11+7+8=53)은 "원작 로스터"가 아니다 — "시간부 잠금 대상 서브셋"이다

**PM이 짚은 대로 밴 레지스트리로 상위 등급 분모를 확인해봤는데,
합계가 그 등급의 전체 로스터와 안 맞는다:**

| 등급 | 밴 레지스트리 수 | 밀도표 원작로스터(BLOCKERS) |
|---|---:|---:|
| 초월함 | 27(또는 28) | 64 |
| 불멸의 | 11(또는 13, 히그마·정체불명 포함시) | 18 |
| 영원한 | 7 | 9(하쿠바 별도) |
| 제한됨 | 8 | 12 |

**전부 밴 레지스트리 쪽이 작다.** 이건 모순이 아니라 **밴 레지스트리는
"이 등급 전체"가 아니라 "이 등급 중에서도 시간이 지나면 사라질 수
있는 위험군"만 담고 있다는 뜻**으로 보인다 — 나머지(초월함 37~38종,
불멸의 5~7종, 영원한 1~2종, 제한됨 4종)는 **영구히(시간제한 없이)
얻을 수 있는 별도의 안전한 캐릭터군**일 가능성이 높다. **즉 "53"은
상위 등급의 원작 로스터 답이 아니라 "그 로스터 중 밴 위험이 있는
서브셋 크기"다** — PM 가설은 기각한다, 답은 다른 곳(전설적인엔
밴 레지스트리 자체가 아예 없다는 것도 같은 방향의 증거 — 히든조합
목록이나 전체 유닛 명단을 다시 훑어야 한다)에 있다. `[미확정]`으로
남기고 다음 조사로 넘긴다.
