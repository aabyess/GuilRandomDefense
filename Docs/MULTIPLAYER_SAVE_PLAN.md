# 멀티 세이브 설계 (초안 — 3단계, 코드 대기)

> 작성: 구현담당2, 2026-09-26 · 브랜치 `mp` · PM 지시: 사장님 두 창 결과 전까지 문서만.
> 근거 코드: `PersistentSave.cs` · `PlayerSaveData.cs` · 호출부(RewardDistributor·RoundManager·CombineSystem·ChatUnlockManager).

## 1. 지금 구조 (싱글 기준, 실측)

| 항목 | 내용 |
|---|---|
| 파일 | `Application.persistentDataPath/Save/player_{슬롯}.json` — **그 게임을 돌리는 기계에** 슬롯별로 |
| 내용 | `PlayerSaveData`: 누적 플레이포인트 · 누적 클리어 횟수 · 한 판 최고점 · 레벨(항상 0) — int 4개 |
| 읽기 | `PersistentSave.Awake`에서 그 슬롯 파일을 읽는다 |
| 쓰는 곳 1 | 게임 시작 `RewardDistributor.Start` → `ApplyLoadThresholdRewards`: 누적 10/100/300/600/900 문턱이면 골드·목재·특성포인트 개시 보너스 |
| 쓰는 곳 2 | 조합식·채팅 해금 조건 `cumulativeClearCount >= requiredSaveCount`(CombineSystem:335, ChatUnlockManager:177) |
| 쓰는 곳 3 | 판 끝 `RoundManager.FinishPersistentSave` → `FinishRun`(죽은 사람 제외) → **파일에 씀** |
| 싱글에서 내 파일 | 싱글은 항상 슬롯 0 → **`player_0.json`이 곧 「내 세이브」** |

## 2. 멀티에서 틀어지는 것

- 호스트 PC에서 슬롯 1~3의 `PersistentSave`가 **호스트 PC의** `player_1~3.json`을 읽는다 — 친구 것이 아니라 호스트 PC에 있는 아무 파일(대개 없음 = 0).
  → 친구는 **자기 누적치의 개시 보너스·해금 조건을 못 받는다**.
- 판이 끝나면 친구의 결과가 **호스트 PC의** `player_1.json`에 쓰인다. 친구 PC엔 아무것도 안 남는다.
  → 친구가 다음에 방장을 하거나 혼자 하면 진행이 없다. 방장을 바꿀 때마다 진행이 갈린다.
- 클라 PC의 `PersistentSave`들도 Awake에서 자기 PC 파일을 읽지만 **쓰는 일은 없다**(판정·FinishRun은 호스트). 지금은 무해.

## 3. 제안 — 「세이브는 각자 PC가 주인, 판 동안만 호스트에 빌려준다」 (권장 A안)

1. **내 세이브 = 내 PC의 `player_0.json`**(싱글과 같은 파일). 사장님 기존 싱글 진행이 그대로 이어진다. 파일 위치·형식은 안 바꾼다.
2. **대기실에서 제출**: 클라가 NetPlayer를 받는 순간(Spawned, 입력권한) 자기 `player_0.json`을 읽어 `RPC_SubmitSave(point, clear, best, level)`로 호스트에 보낸다.
   호스트는 NetPlayer의 [Networked] 4칸에 받아 둔다(대기실 화면에 「누적 클리어 N」 표시도 가능).
3. **판 시작(호스트)**: 게임 씬 `PersistentSave.Awake`에서 — 멀티이고 이 슬롯이 호스트 자신이 아니면 — 파일 대신 **받아 둔 값으로 Data를 채우고 `remote` 표시**.
   (`// MP:` PersistentSave 한 곳. `MatchConfig`에 슬롯별 제출값을 두면 Awake가 읽을 수 있다 — 좌석과 같은 방식.)
   → `ApplyLoadThresholdRewards`·조합·채팅 해금 조건이 **친구 자기 누적치**로 돈다(코드 수정 없음).
4. **판 끝(호스트)**: `FinishRun`이 remote 슬롯이면 **파일에 쓰지 않고** 결과 Data를 그 클라에 `RPC_SaveResult(point, clear, best, level)`로 보낸다.
   클라는 받아서 **자기 `player_0.json`에 쓴다.** 호스트 자신(슬롯 0)은 지금처럼 자기 파일에 쓴다.
5. **클라 쪽 PersistentSave**: 멀티 클라에서는 파일을 읽지도 쓰지도 않는다(`// MP:` 가드) — 헷갈림 방지.

### 경계 사례
| 경우 | 처리 |
|---|---|
| 판 도중 나감(퇴장) | 원작 Gone = 사망 표식 → FinishRun 대상 아님 → 저장 없음. 원작 「패배한 상태에선 세이브 불가」와 같다 |
| 호스트가 먼저 나감/끊김 | 그 판 결과는 아무에게도 안 남는다(원작도 방이 깨지면 세이브코드를 못 받음). 받아 둔 제출값은 친구 PC 파일 그대로라 손실 없음 |
| 결과 RPC가 안 닿음(판 끝 직후 끊김) | 친구는 그 판 몫만 잃는다. 재시도·확인 응답은 3단계 후반 선택지 |
| 세이브 조작 | 각자 PC 파일이라 고치면 고친 대로 — 친구끼리 베타라 막지 않는다(싱글과 같은 수준). 값은 받을 때 0 이상으로만 자른다 |
| 같은 PC 두 창 테스트 | 두 창이 같은 `player_0.json`을 제출·기록한다 — 테스트용이라 무해하지만 결과가 겹쳐 쓰인다(알고 있을 것) |

## 4. 다른 안 (안 권함)

- **B. 호스트가 친구 세이브를 보관**(닉네임별 파일을 호스트 PC에): 방장이 바뀌면 진행이 갈린다. 친구 PC엔 아무것도 없다. ✗
- **C. 서버/클라우드 저장**(Photon 사용자 데이터·별도 서버): 계정·비용·개인정보가 생긴다. 친구 베타에 과하다. ✗

## 5. 작업량 (A안)

- NetPlayer: [Networked] 4칸 + `RPC_SubmitSave`(클라→호스트) + `RPC_SaveResult`(호스트→그 클라).
- MatchConfig: 슬롯별 제출값.
- `// MP:` PersistentSave: Awake(remote면 제출값) · FinishRun(remote면 쓰지 말고 결과 RPC) · 멀티 클라 가드. 20줄 안팎.
- 검증: 빌드 두 개 + 서로 다른 persistentDataPath가 필요하다(같은 PC면 같은 파일) → 테스트용 `-mpSaveDir` 옵션으로 저장 폴더를 가른다.

## 6. PM·사장님 결정이 필요한 것

1. **A안으로 가도 되는가**(「내 세이브 = 내 PC의 player_0.json」).
2. 대기실에 각자 「누적 클리어 N」을 보여 줄지(원작엔 세이브코드 로드 메시지가 채팅으로 뜬다).
3. 결과 RPC 확인 응답(재전송)까지 할지, 끊기면 그 판 몫은 잃는 것으로 둘지.

## 7. 결정 (PM, 2026-09-26) — 코드는 사장님 두 창 결과 뒤에 시작

1. **A안 승인.** 내 세이브 = 내 PC의 `player_0.json`, 대기실에서 제출 → 판 끝에 돌려받는다(원작 세이브코드도 각자 자기 것 — 같은 구조).
2. 대기실 「누적 클리어 N」 표시 **안 함**(나중에 사장님이 원하시면).
3. 확인 응답·재전송 **안 함** — 연결이 살아 있으면 Fusion 신뢰 RPC가 도착을 보장하고, 끊겼으면 받을 곳이 없다.
   대신 클라가 결과를 받아 **파일에 쓴 순간** 로그 한 줄 + 화면 알림 **「이번 판 기록을 저장했습니다」**(사장님 테스트에서 눈으로 확인용).

## 8. 구현·검증 (2026-09-26, 구현담당2)

- `NetSaves`(새) · `NetPlayer.RPC_SubmitSave`/`RPC_SaveResult` · `MatchConfig` 슬롯별 제출값 · `// MP:` `PersistentSave`(Awake·WriteToDisk·경로 함수).
- 검증(빌드 두 개, `-mpSaveDir`로 세이브 폴더를 갈라서): 클라 `player_0.json` 150점·클리어 3 →
  호스트 「슬롯 1은 제출값 사용 — 누적 150점 · 클리어 3회」 → 클라 개시 보너스 **골드 40·목재 2**(30+10, 1+1) →
  테스트 판 끝(+5점) → 클라 파일 **155점·클리어 3·최고 9**, 화면 「이번 판 기록을 저장했습니다.」 →
  호스트 폴더엔 **자기 player_0.json(5점)만**(player_1.json 안 생김).
- 실제 판 끝(RoundManager.FinishPersistentSave, 완주 때만)은 같은 FinishRun을 부르므로 같은 길이다 — 완주까지 돌린 판으로는 미확인.
