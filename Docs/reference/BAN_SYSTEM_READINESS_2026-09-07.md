# 밴 시스템 사전점검 — 명세 도착 전, 지금 확인 가능한 것만

조사: 구현담당2 / 2026-09-07
요청: PM — `Trig_ban_Actions`+`Trig_ban_transendence`(채팅언락 레시피를 무작위로
영구 잠그는 12번째 시스템) 구현 전, 우리 쪽이 이걸 받을 준비가 됐는지 코드
레벨로 점검. **코드는 안 고쳤다 — 점검과 계획만.**

## 결론 한 줄

`ChatUnlockData` 자산 자체가 **0개**다(사장님 유닛 배정 대기, 스키마만
있음). 밴 시스템이 "잠글 대상"조차 아직 실재하지 않는다 — 명세가 와도
**자산이 먼저 채워져야** 밴 로직을 실제로 시험할 수 있다. 그 전제 위에서
①~⑤ 답한다.

---

## ① 레시피 단위 잠금 상태 — 없다, 새 저장소가 필요하다

`ChatUnlockManager`의 잠금 상태는 딱 하나, `hasClaimedShared`
(`Dictionary<int, bool>`, playerId 키)뿐이다 — **"47개 중 아무거나 하나
받았는가"라는 플레이어 단위 bool 하나**지, 레시피(`ChatUnlockData`)별
개별 플래그가 아니다. `CanUnlock`가 이걸 `HasClaimedSharedSlot(playerId)`
로 조회해서 게이트 하나로 47개를 다 잠근다(`ChatUnlockManager.cs:136`).

**레시피별 잠금을 넣을 자리**: `ChatUnlockManager` 안에 새 저장소를
추가하면 된다(아래 ②의 범위에 따라 모양만 갈린다) — 클래스 자체를
바꿀 필요는 없다. `CanUnlock`에 새 검사 한 줄
(`if (banned.Contains(data)) { reason = "..."; return false; }`)을
`HasClaimedSharedSlot` 검사 옆에 추가하는 정도로 끝난다.

## ② 범위(플레이어별 vs 게임 전역) — 지금 코드는 플레이어별로 짜여 있다

`ChatUnlockManager`는 씬에 **단일 인스턴스**로 존재한다(`GameChatBox`가
`[SerializeField]` 하나로 참조, `GameChatBox` 자신도 `PlayerContext.Local`
하나만 보는 로컬 플레이어 전용 UI). 이 매니저 자체는 "누구 것"이 아니라
`TryUnlock(playerId, data)`처럼 **playerId를 인자로 받아 그때그때 대상을
찾는 공용 창구**다. 기존 게이트(`hasClaimedShared`)는 이 공용 창구 안에서
**playerId로 키를 나눠** 플레이어별 상태를 흉내낸다.

**두 경우 각각 무엇을 고쳐야 하는가**(원작 확정 전 미리 적어둠):

```
원작이 플레이어별이면(4명 각자 자기 47개가 따로 밴됨)
  → banned: Dictionary<int, HashSet<ChatUnlockData>>
  → hasClaimedShared와 정확히 같은 모양, 옆에 필드 하나 추가로 끝난다.
  → 구조 변경 없음.

원작이 게임 전역이면(밴 하나가 4명 전원에게 동시 적용)
  → banned: HashSet<ChatUnlockData> (playerId 키 없음)
  → 이것도 클래스 안에 필드 하나 추가로 끝난다 — CanUnlock이
    playerId를 안 보고 그냥 banned.Contains(data)만 확인하면 된다.
  → 구조 변경 없음. "구조를 바꿔야 한다"는 PM 우려는 과할 수 있다 —
    ChatUnlockManager가 이미 단일 공용 인스턴스라 어느 쪽이든
    필드 모양만 다르지 클래스·소유 관계는 안 바뀐다.
```

⚠️ 다만 **`hasClaimedShared`와 밴 목록이 상호작용할 수도 있다** — 예를
들어 "이미 받은 것도 나중에 밴 대상이 될 수 있는가"(원작이 재획득 방지용
락과 밴을 같은 풀에서 굴리는지)는 명세 없이는 모른다. `Trig_ban_
transendence`가 "반복 루프"라고 하셨으니 매 라운드/매 킬마다 하나씩
추가로 잠그는 것으로 보이는데, **이미 hasClaimedShared로 받은 47개
자체를 대상 풀에서 빼는지**는 확인이 필요하다 — 안 빼면 "이미 받은 걸
또 밴"하는 무의미한 굴림이 섞일 수 있다.

## ③ 대상 집합(Eternal/Forever/Immortal/Nika) — **셀 자산 자체가 0개**

`ChatUnlockData.cs` 자체 주석에 이미 못박혀 있다: "사장님이 유닛별 배정은
나중에 직접 준다고 하셔서 에셋은 만들지 않았다." `Assets/Data/`를
`ChatUnlock`으로 전수 검색해도 **자산 인스턴스가 0개**다(스크립트와
`ChatUnlockCategory` enum만 존재 — `Eternal`·`Forever`·`Immortal`·`Nika`
4개 값).

**개수 대조 자체가 불가능하다** — 비교할 우리 쪽 실물이 없다. 대신
**두 문서 사이에서 숫자 불일치를 하나 발견**했다:

```
ChatUnlockManager.cs 기존 주석    "Eternal·Forever·IM 47종 + Nika 1종" = 48
이번 PM 전달(리서치 원문)         "Eternal 27 · IM 11 · Forever 7"    = 45 (+Nika?)
```
27+11+7=45, 여기에 Nika 1을 더해도 46 — 기존 주석의 47/48과 안 맞는다.
**어느 쪽이 최신이거나, 집계 기준(예: 원작 CSV 70행 중 몇 종이 실제
"채팅 코드"이고 몇 종이 다른 방식인지)이 다를 수 있다** — `[미확인]`.
리서치 스펙 문서가 오면 이 숫자부터 재대조해야 한다.

**category enum 자체(4종)는 원작 4계열과 이름이 맞다** — Eternal/Forever/
Immortal/Nika. 이건 문제 없다.

### 🔴 2026-09-07 갱신 — 명세 도착, 세 번째 숫자까지 나왔다. 셋 다 다르다

```
ChatUnlockManager.cs 기존 주석    "Eternal·Forever·IM 47종 + Nika 1종" = 48
PM 1차 전달(리서치 초안)          "Eternal 27 · IM 11 · Forever 7"    = 45
BAN_SYSTEM_SPEC(리서치 확정)      "Eternal 27 · 불멸 11 · 제한 8 · 영원 7" = 53
```
세 소스가 전부 다른 수를 대고 있다 — **47도 45도 53과 안 맞는다.**
`category` enum이 Eternal/Forever/Immortal/Nika 4종인데, 확정 스펙은
"초월(Eternal?)·불멸·제한·영원" 4계열로 **이름 자체도 다르게 부른다**
(Forever↔영원, Immortal↔불멸이 같은 대상의 다른 이름일 가능성, 또는
Nika가 이 53에서 아예 빠졌을 가능성) — enum 이름과 스펙 계열명이
1:1로 맞물리는지부터 다시 봐야 한다.

**⚠️ 자산이 생길 때(사장님 유닛 배정 시) 이 개수(원작 계열별 정확한
수)를 맞추는 게 검수 항목이다** — 지금 세 숫자 중 어느 것도 확정으로
믿지 말고, 자산을 다 채운 뒤 계열별로 직접 세어 스펙 문서(최신본)와
대조할 것. 지어내지 않는다 원칙상 지금 이 셋 중 하나를 고르지 않는다.

**범위(②)는 명세로 확정됐다 — 게임 전역 공유다.** `DisableTrigger`가
전역 핸들이고, 잠금 자체는 `Player(0)~(3)` 4명에 개별 반복 호출되는
구조라는 게 결정적 근거(리서치 확인). ②에서 적어둔 "두 경우 각각
무엇을 고칠지" 표는 **게임 전역 쪽(`HashSet<ChatUnlockData>`, playerId
키 없음)으로 확정**됐다 — 명세 구현 시 그 갈래를 쓰면 된다.

## ④ UI — 잠금 표시 자리 자체가 없다

`GameChatBox`는 순수 텍스트 입력창(OnGUI, `TextField` 하나)뿐이다 —
평소엔 아무것도 안 뜨고, 코드를 직접 쳐야만 성공/실패 메시지가 4초간
뜬다(`ShowStatus`). **"지금 어느 코드가 열려있고 어느 게 잠겼는지"를
보여주는 상시 UI 자체가 없다** — 플레이어가 코드를 몰라서 못 치는
것과 몰라서 잠긴 걸 모르는 것을 화면만 보고 구분할 방법이 없다.

`자물쇠`/lock 아이콘·마커 관련 코드도 전수 검색 0건이다. 원작처럼
"마커에 자물쇠 이펙트"를 보여주려면 **새 UI가 필요하다** — 최소한
"이 47개(또는 대상 수) 중 N개가 밴됨"류 요약 텍스트라도 `GameHud`나
`GameChatBox`에 새로 붙여야 한다. 지금은 완전한 빈 자리다.

## ⑤ 저장(PersistentSave) — 안 붙어 있고, 붙일 자리도 없다

`PlayerSaveData`(파일로 저장되는 유일한 구조체)는 필드 4개뿐이다
(`cumulativePlayPoint`·`cumulativeClearCount`·`bestRunPoint`·
`playerLevel`) — 밴 목록이나 잠금 상태를 담을 필드가 없다.

`ChatUnlockData`가 `PersistentSave`와 상호작용하는 유일한 지점은
`requiredSaveCount`(Forever 전용, "누적 클리어 N회 이상"조건, `ChatUnlockManager.
CanUnlock`)뿐이고, 이건 밴과 무관한 기존 게이트다.

기존 `hasClaimedShared`는 **의도적으로 저장 안 함**("한 판 한정", 주석에
명시) — 원작 `udg_Tech_Onedill`도 매판 리셋이 확인돼 있다. **같은 선례를
따르면 밴 목록도 매판 새로 뽑는 게 자연스럽다**(저장할 게 없다)는 게
지금 근거로 미룰 수 있는 최선의 추정이다 — 다만 이건 추정이지 확인이
아니다. `Trig_ban_Actions`가 "시작 1회"라는 표현이 "게임 시작 시
1회"(매판 리셋과 일치)인지 "세이브 슬롯 최초 1회"(영속)인지는 명세
문서가 결정할 몫이다.

---

## 요약 — 명세 도착 즉시 착수 가능한 순서

```
1. ChatUnlockData 자산이 아직 없으므로, 밴 로직 자체는 자산 없이도
   구조만 먼저 넣을 수 있다(스키마 필드는 append-only로 추가).
2. ChatUnlockManager에 밴 저장소 필드 하나 추가(②의 두 모양 중 명세가
   정하는 쪽) + CanUnlock에 검사 한 줄.
3. ③의 숫자 불일치(45/47/48)를 리서치 스펙 문서로 재확인.
4. UI(④)는 최소 요약 텍스트부터 — GameChatBox 상태 메시지 자리를
   재사용하면 새 패널 없이도 시작할 수 있다.
5. 저장 여부(⑤)는 명세의 "시작 1회"가 어느 스코프인지에 달렸다 —
   영속이면 PlayerSaveData에 필드 추가(append-only), 아니면 손 안 댐.
```

**클래스·소유 구조를 바꿔야 하는 항목은 없었다** — 전부 기존 클래스에
필드·검사 한 줄을 더하는 수준이다. 가장 큰 공백은 ③(자산 0개)과
④(UI 전무)다.
