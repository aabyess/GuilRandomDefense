# 🔴 정정 — 히든조합·채팅언락은 **소유자 필터가 있다** (`Player(7)` 중립풀은 재료가 아니다)

조사: PM / 2026-09-07
대상: `Docs/reference/PLAYER7_NEUTRAL_POOL_CENSUS.md`의 「판정 = `GetPlayableMapRect()` 전체 스캔, **소유자 필터 없음**」
원문: `Tools/w3x/원본/war3map_new.j` (ORD11.089.w3x, sha256 `3f43c682…`)

---

## 0. 한 줄

**정반대였다.** 히든조합 163건 · 채팅언락 423건의 맵 스캔이 **한 건도 빠짐없이**
`GetOwningPlayer(GetFilterUnit())==GetTriggerPlayer()`를 걸고 있다.
재료는 **반드시 그 플레이어 소유**여야 한다.

→ **우리 `HiddenCombineManager`가 플레이어 인벤토리에서 소비하는 건 원작과 맞다.**
「중립 풀이 없어 영영 통과 못 한다」던 차단 우려는 **해소**됐다. 재료 자산만 채워지면 살아난다.

---

## 1. 전수 실측

| 계열 | 맵 스캔 건수 | 소유자 필터 **있음** | **없음** |
|---|---:|---:|---:|
| 채팅언락 (`Trig_Eternal*`·`Trig_Forever*`·`Trig_IM*`) | 423 | **423** | **0** |
| 히든조합 (`Trig_Hidden_*`) | 163 | **163** | **0** |
| 그 밖 (라운드·보스·데스카운트·`Gone*` 등) | 56 | 14 | 42 |

「소유자 필터 없음」이 실제로 존재하는 곳은 **그 밖 42건뿐**이고, 그건 전부
전역 처리(라운드 진행·보스 생성·패배 판정)다 — **재료 판정이 아니다.**

## 2. 조건 함수의 실제 모양

`Trig_Hidden_perona`의 재료 5종 판정과 소비가 전부 같은 꼴이다:

```jass
function Trig_Hidden_perona_Func003Func001001001002001 takes nothing returns boolean
    return(GetUnitTypeId(GetFilterUnit())=='h02N')

function Trig_Hidden_perona_Func003Func001001001002002 takes nothing returns boolean
    return(GetOwningPlayer(GetFilterUnit())==GetTriggerPlayer())   // ← 이것이 있다

function Trig_Hidden_perona_Func003Func001001001002 takes nothing returns boolean
    return GetBooleanAnd( …001(), …002() )
```

판정(`CountUnitsInGroup(…)>=1`)과 소비(`RemoveUnit(GroupPickRandomUnit(…))`)가
**같은 조건 함수 쌍을 공유**한다. 즉 「센 것」과 「없앤 것」이 같은 집합이고,
그 집합은 트리거를 발동시킨 플레이어의 소유 유닛으로 한정된다.

## 3. 왜 반대로 읽혔나

`GetPlayableMapRect()`라는 **모양이 같아서**다. 전역 처리 42건에도 같은 함수가 쓰이는데,
그쪽은 소유자 필터가 없는 게 맞다. **그 42건을 본 결과를 재료 계열 586건에 옮긴 것**으로 보인다.

- 뿌리 ㊹ — 「전수했다」는 **그 조사가 던진 질문에 대해 전수**라는 뜻이다.
  「맵 전체 스캔인가?」의 전수는 「재료 판정에 소유권이 필요한가?」의 전수가 아니다.
- 「모양이 같아도 대응은 아니다」 — 같은 BJ 함수라도 계열이 다르면 다른 것이다.

## 4. 다시 열린 질문 — `Player(7)` 619개체는 그럼 무엇인가

재료가 아니라면 그 619개체(229종)의 용도가 **다시 미확인**이다. 지어내지 말 것.
지금 아는 것은 「재료 판정에는 안 쓰인다」뿐이다.

⚠️ **예외 확인 필요**: 위 census는 `GetPlayableMapRect()` 스캔만 셌다.
다른 경로(`GetUnitsOfPlayerAll`, 미리 만든 unit group 변수, `udg_` 캐시 등)로
중립 유닛을 집는 자리가 있는지는 **이 조사가 아무 말도 안 한다.** 그건 별도 질문이다.

## 5. 재현

```python
import re, collections
src = open('Tools/w3x/원본/war3map_new.j', encoding='utf-8', errors='replace').read()
bodies = {m.group(1): m.group(4) for m in
          re.finditer(r'function (\w+) takes ([^\n]*?) returns (\w+)(.*?)endfunction', src, re.S)}

def expand(name, depth=0, seen=None):            # 조건 함수 체인을 잎까지 펼친다
    seen = seen or set()
    if name in seen or depth > 6 or name not in bodies: return []
    seen.add(name); b = bodies[name]
    called = re.findall(r'(Trig_\w+)\(\)', b)
    return [x for c in called for x in expand(c, depth+1, seen)] if called else [b.strip()]

scan  = re.compile(r'GetUnitsInRectMatching\(GetPlayableMapRect\(\),Condition\(function (\w+)\)\)')
owner = re.compile(r'GetOwningPlayer\(GetFilterUnit\(\)\)==GetTriggerPlayer\(\)')
# fname 접두사로 계열을 가르고, expand 결과에 owner가 걸리는지 센다
```

전체 스크립트는 이 문서의 결과를 낸 것 그대로다 — 계열 판정만
`Trig_Hidden_` / `Trig_(Eternal|Forever|IM|Immortal)` / 그 밖으로 나눴다.

## 6. 반영할 곳

- [ ] `PLAYER7_NEUTRAL_POOL_CENSUS.md` — 「소유자 필터 없음」 문장 정정 (리서치담당)
- [x] `HiddenCombineManager` — 손댈 것 없음. 지금 구조가 맞다
- [x] `ChatUnlockManager` — 손댈 것 없음
- [ ] 메모리 `next-up-2026-09-06`의 📌 확인 대기 항목 — **해소**로 닫기
