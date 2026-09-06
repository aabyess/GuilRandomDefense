# 공격타입×등급 업그레이드 — `war3map.w3q` 원문 전수 (`R00G`~`R01W`)

조사: 리서치담당 / 2026-09-06
요청: PM — 「`SkillEffectBasis.ResearchLevel`의 `basis=4` 9건이 참조하는 공격타입×등급
연구 시스템 전체를 원문에서 덤프하라, 지을지 말지는 이 결과로 결정한다」

## 결론 한 줄

**PM이 말한 "범위(`R00G`~`R01W`, 53개)"는 한 시스템이 아니라 최소 4개의 서로 다른
시스템이 우연히 같은 ID 대역을 나눠 쓰고 있다.** 진짜 "공격타입×등급" 시스템은 그중
**16개**뿐이고(마스터 버튼 4 + 자동부여 12), 나머지는 (a) 이미 구현된 등급트랙과 겹치는
4개, (b) 이름 있는 캐릭터 전용 고유 보너스 5개(트리거 참조 0건), (c) `race=creeps`인
완전히 별개의 시스템(적 스케일링으로 추정, 25개, **이번 조사 범위 밖**)이다.

## ① 실재하는 ID 목록 — 진짜 "공격타입×등급" 시스템은 16개

건물 **"강화소 3"**(유닛 `unam`="강화소 3", `ures`="R01V,R00H,R00G,R00I") 하나가 이 넷을
전부 연구 목록으로 갖고 있다 — 우리가 이미 만든 "강화소"(등급트랙, `UnitUpgradeTrackData`)와
**이름은 같지만 다른 건물**이다. 실제로 원작엔 강화소가 최소 3개 있다:

| 건물 유닛 이름 | `ures`(연구 목록) | 정체 |
|---|---|---|
| 강화소 1 | `R00F,R008,R019,R002,R00E,R003,R004,R000` | **이미 구현된 등급트랙**(우리 `UnitUpgradeTrackData` 9종이 이것) |
| 강화소(번호 없음) | `R032,R02O,R015,R00X,R013,R007,R014,R016` | **이름 있는 "영원한" 캐릭터 전용 고유 보너스**(아래 ③) — 별개 시스템 |
| **강화소 3** | `R01V,R00H,R00G,R00I` | **이번에 조사한 "공격타입×등급" 시스템** — 미구현 |

`R00G`/`R00H`/`R00I`/`R01V`를 연구하면(마스터 버튼), `war3map.j`의
`Trig_upgrade_______u`(이름이 특수문자 제거로 깨진 트리거, `EVENT_PLAYER_UNIT_RESEARCH_START`에
등록)가 그 즉시 하위 3개 기술을 **한꺼번에 공짜로**(`SetPlayerTechResearchedSwap(...,+1,...)`)
부여한다. 즉 **플레이어가 직접 사는 건 4개뿐이고, 나머지 12개는 자동으로 딸려온다.**

```jass
// Trig_upgrade_______u_Actions (원문 그대로, 4개 분기만 발췌)
if(Trig_upgrade_______u_Func006C())then   // GetResearched()=='R00G' (일반)
call SetPlayerTechResearchedSwap('R01K',(GetPlayerTechCountSimple('R01K',GetOwningPlayer(GetTriggerUnit()))+1),GetOwningPlayer(GetTriggerUnit()))
call SetPlayerTechResearchedSwap('R01L',(GetPlayerTechCountSimple('R01L',GetOwningPlayer(GetTriggerUnit()))+1),GetOwningPlayer(GetTriggerUnit()))
call SetPlayerTechResearchedSwap('R01M',(GetPlayerTechCountSimple('R01M',GetOwningPlayer(GetTriggerUnit()))+1),GetOwningPlayer(GetTriggerUnit()))
else
endif
if(Trig_upgrade_______u_Func007C())then   // GetResearched()=='R00H' (공성)
call SetPlayerTechResearchedSwap('R01N',...+1...) call SetPlayerTechResearchedSwap('R01O',...+1...) call SetPlayerTechResearchedSwap('R01P',...+1...)
else endif
if(Trig_upgrade_______u_Func008C())then   // GetResearched()=='R00I' (관통)
call SetPlayerTechResearchedSwap('R01Q',...+1...) call SetPlayerTechResearchedSwap('R01R',...+1...) call SetPlayerTechResearchedSwap('R01S',...+1...)
else endif
if(Trig_upgrade_______u_Func009C())then   // GetResearched()=='R01V' (패기)
call SetPlayerTechResearchedSwap('R01W',...+1...) call SetPlayerTechResearchedSwap('R01U',...+1...) call SetPlayerTechResearchedSwap('R01T',...+1...)
else endif
```

같은 트리거가 등급트랙 쪽(`R004`→`R022`+`R006`+`R02Q` 등, `R000`→8개 동시 등)도 똑같은
"연구 하나로 여러 개 공짜 부여" 패턴을 쓴다 — **이 맵의 연구 시스템 전체가 이 방식이다**,
공격타입만 특별한 게 아니다.

## ② 각 ID가 무엇인가 — 공격타입 × 버킷 3단(4단은 패기만)

각 자식 ID는 **레벨이 없는 단일값**이다(`gba1`=`gmo1`, `gba2`=`gmo2`— 값이 완전히
같다. 등급트랙과 같은 "레벨1값=레벨당증분" 관례). `glvl`(최대레벨) 필드는 마스터
버튼(`R00G` 등)에 3으로 박혀 있지만, 하위 12개(`R01K` 등)엔 이 필드가 상속된 스톡
템플릿 값(레벨 1~30 지꺼기, `Aegr` 플래토와 같은 종류의 잔재)이라 **실제로 의미
있는 값인지 확인 못 했다** — 실측(`gba2` 등)이 레벨 구분 없이 딱 하나뿐이라 사실상
"연구하면 즉시 최종값"으로 작동하는 것으로 보인다. `[미확인 — 재확인 필요]`.

| 공격타입 | 마스터 버튼 | 공속(`gba1`=`gmo1`) | 자식ID(버킷1) | 자식ID(버킷2) | 자식ID(버킷3) |
|---|---|---:|---|---|---|
| 일반 | `R00G` | +3% | `R01K`=1500 | `R01L`=2000 | `R01M`=3000 |
| 공성 | `R00H` | +3% | `R01O`=1500 | `R01P`=2000 | `R01N`=3000 |
| 관통 | `R00I` | +3% | `R01S`=1500 | `R01R`=2000 | `R01Q`=3000 |
| 패기 | `R01V` | +4% | `R01W`=2000 | `R01U`=3000 | `R01T`=4000 |

⚠️ 각 자식ID의 툴팁(`gub1`)은 전부 "히든/전설/랜덤유닛:1500·제한됨/특수함:2000·초월/불멸/영원:4000"이라는
**똑같은 복사-붙여넣기 문구**를 갖고 있다 — 어느 자식ID를 열어도 문구가 동일하다. 즉
**툴팁 숫자는 원문이 아니라 잔재**이고, **그 ID 자신의 `gba2` 필드 값이 진짜**다(위 표는
전부 필드값 기준, 툴팁 기준 아님). 패기만 1500 버킷이 없고 대신 4000이 하나 더 있어
비대칭이다 — 원작이 그렇게 만든 건지 데이터 누락인지는 `[미확인]`.

`R01T`/`R01M`/`R01O`/`R01V`(PM이 지목한 4개)의 정체: **`R01T`=패기 4000버킷,
`R01M`=일반 3000버킷, `R01O`=공성 1500버킷, `R01V`=패기 계열 마스터 버튼**(자식 ID가
아니라 구매 그 자체다 — PM의 원래 가정과 달리 `R01V`는 "패기 타입"의 자식이 아니라
부모다).

## ③ 「히든/전설/랜덤유닛」·「제한됨/특수함」·「초월/불멸/영원」 버킷이 등급과 맞는가

**아니다, 툴팁 문구는 잔재라 신뢰 불가.** 대신 유닛별 `upgr`(그 유닛에 실제로 적용되는
연구 목록) 필드를 직접 뒤져 실제 적용 사례를 확인했다:

- `h05C`(핸콕, 영원한): `upgr`=`R01T,R016` — **패기 4000버킷**을 받는다(+ 아래 ③의 고유
  보너스 `R016`도 별도로 받는다).
- 그 외 다수 유닛의 `upgr`에서 `R01K,R000` / `R01O,R001` / `R01Q,R003` / `R01T,R004`
  같은 **"자식ID + 등급트랙ID" 쌍**이 반복 확인됐다 — 즉 **버킷은 등급 그 자체가 아니라
  "그 유닛이 등급트랙 몇 번을 쓰는가"에 매여 있는 것으로 보인다**(예: 등급트랙 `R000`을
  쓰는 유닛들이 대체로 `R01K`/`R01O`/`R01Q`/`R01T` 중 하나를 받는다). 정확히 "등급트랙
  N번 = 버킷 M번" 1:1 대응표까지는 이번 조사에서 못 뽑았다 — 필요하면 `upgr` 전량을
  등급트랙ID로 묶어 교차표를 만들 수 있다(안 했음, 시간 배분상 후순위로 미룸).

## ④ 🔴 구매 비용과 구매 방법

- **비용**: 마스터 버튼(`R00G`/`R00H`/`R00I`/`R01V`) 각각 **골드 3000 + 목재 500**
  (`gglb`=3000, `gglm`=500 필드, int로 정확히 디코드 — 등급트랙 `UnitUpgradeTrackData`가
  이미 이 두 필드를 "골드/성장비용"으로 검증해 쓰고 있어 같은 의미로 신뢰할 수 있다).
  **자식 12개(`R01K` 등)는 비용 필드가 30/20으로 남아있지만 실제로 청구되지 않는다** —
  플레이어가 직접 사는 게 아니라 트리거로 공짜 부여되기 때문이다. 30/20은 죽은 값.
- **어디서 사는가**: **"강화소 3"**이라는 별도 건물(우리가 이미 만든 "강화소"=강화소 1과
  다른 유닛). `연구소(h05U)`가 아니다 — PM이 말한 후보 중 "별도 상점(건물)" 쪽이 맞다.
  강화소 3 자신의 건설 비용(골드/목재)은 이번 조사에서 못 찾았다 — `[미확인]`, 유닛
  레코드에 `ugol`류 필드가 이 창에 없었다(추가로 더 찾아볼 수 있다, 후속 필요시 요청).
- **시작 시 이미 레벨이 올라가 있는가**: 반증(자동 연구완료 트리거 등)을 못 찾았다 —
  등급트랙처럼 "시작은 레벨0"으로 추정하지만 **확정 아님**, `[미확인]`.

## ⑤ `h05C`(핸콕)가 실제로 참조하는 ID

`war3map.w3u`의 h05C 레코드 `upgr` 필드 원문: **`R01T,R016`**.
- `R01T` = 위 ②의 패기타입 4000버킷(이 시스템의 일부, 미구현 상태).
- `R016` = "강화소(번호없음)"이 파는 **핸콕 전용 고유 보너스**("|c00ff0080[영원한]|r
  핸콕의 공격력4500, 공격속도를 15% 증가시킵니다.", `gglb`=110/`gglm`=80) — 공격타입
  시스템과 무관한 **완전히 별개의 캐릭터 전용 강화**다. `SkillEffectBasis.ResearchLevel`
  9건 중 h05C 몫이 어느 쪽을 가리키는지는 파일 설명문에 없어 코드 쪽(구현담당) 확인이
  필요하다 — 원문 근거로는 **패기타입 버킷(`R01T`)일 가능성이 높다**(고유 보너스
  `R016`은 절대 가산치라 basis=ResearchLevel의 "레벨 비례" 서술과 안 맞는다).

## 범위 밖 — 조사는 했지만 이번 결정에 안 쓴 것

- **`R00X`,`R013`~`R016`(에이스·비비·버기·카벤딧슈·핸콕 전용 고유 보너스, 5개)**:
  `war3map.j`에 리터럴 참조가 **0건**이다 — 트리거가 안 건드린다는 뜻은 엔진이
  네이티브로 적용한다는 뜻일 수도 있다(`Aegr`/`AIsr`과 같은 부류). "강화소(번호없음)"가
  실제로 파는 목록이라 죽은 데이터는 아니다 — 다만 공격타입 시스템과 무관하니 이번
  보고에서는 존재만 기록한다.
- **`race=creeps`인 25개 그룹**(`R00J`~`R00W`, `R00Y`~`R012`, `R01A`~`R01I` 중 위 16개를
  뺀 나머지): 적(크립) 유닛의 `upgr`에서만 발견됐다 — 난이도/라운드별 스탯 스케일링
  시스템으로 추정되나 **이번 조사 범위(공격타입×등급) 밖이라 더 파지 않았다.** 필요하면
  별도 요청 주시면 그때 판다.

## 추출 스크립트 (재사용 가능 — `.w3q`용, `.w3a`용과 헤더 포맷이 다름)

```python
"""
war3map.w3q(업그레이드) 레코드 파서 — 2026-09-06, 리서치담당
w3q 레코드 헤더는 [oldId(4)][newId(4)][modCount(4)]이고, 그 뒤 필드가
[tag(4)][type(4)][level(4)][dataId(4)][value] 순으로 이어진다(type 0=int,3=string,그외=float).
w3a와 달리 필드마다 objectId를 반복하지 않는다 — 순서대로 그냥 읽으면 된다.
"""
import struct

def parse_upgrade_record(data, start_pos, end_pos):
    region = data[start_pos+8:end_pos][4:]  # oldId+newId+modCount 건너뜀
    fields = {}
    pos = 0
    while pos + 16 <= len(region):
        tag = region[pos:pos+4]
        if not all(32 <= b < 127 for b in tag):
            pos += 1
            continue
        typ, level, dataId = struct.unpack('<iii', region[pos+4:pos+16])
        if typ == 3:
            send = region.find(b'\x00', pos+16)
            if send == -1:
                break
            val = region[pos+16:send].decode('utf-8', 'replace')
            pos = send + 1
        elif typ == 0:
            val = struct.unpack('<i', region[pos+16:pos+20])[0]
            pos += 20
        else:
            val = struct.unpack('<f', region[pos+16:pos+20])[0]
            pos += 20
        fields.setdefault((tag.decode(), level), val)
    return fields
```
