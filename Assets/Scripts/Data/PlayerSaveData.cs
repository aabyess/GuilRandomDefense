using System;

// 원작 세이브 코드(VJSE)가 담던 네 값의 우리 버전이다. 원작은 워크3에 파일 저장이 없어서
// 이 넷을 문자열로 압축해 채팅창에 코드로 뽑아냈다(MySampleSavecodeConfig__DecodeVersion_1,
// VJSE_Decode) — 사장님이 "진짜 영속 저장"으로 정하셔서(2026-09-05, 11번) 그 인코더/디코더는
// 옮기지 않았고 이 클래스를 그대로 파일에 쓴다. 원작 상한값(누적포인트 100,000 / 누적횟수 300 /
// 베스트포인트 1,000 / 레벨 30)도 원작 세이브 코드 "길이" 제약에서 나온 것이라 우리 상한으로
// 삼을 근거가 아니다(PM 지시 2026-09-05) — 그래서 여기엔 상한을 두지 않았다. 필요해지면
// 그때 정하고 이유를 이 주석에 적을 것.
//
// 원문 근거 — war3map.j, SavePlayer(자동 저장 경로):
//   TEMPDATA_PLAYER_PLAYTIME  = Load_PlayCount + Save_playcount        → cumulativeClearCount
//   TEMPDATA_PLAYER_PLAYPOINT = Load_PlayPoint + present_Save_playpoint → cumulativePlayPoint
//   TEMPDATA_PLAYER_LEVEL     = Save_playerlevel                        → playerLevel
//   TEMPDATA_PLAYER_BESTPOINT = present_Save_playpoint, Load_PlayBestPoint보다 클 때만 갱신 → bestRunPoint
//
// ⚠️ 원작에 저장 경로가 둘 있고(자동 SavePlayer / 수동 채팅 "-save" OnChatSave) 같은 칸에
// 서로 다른 식을 쓴다 — PM 지시로 원문을 직접 대조해 확인된 진짜 모순이다:
//   OnChatSave는 PLAYPOINT를 Load_PlayPoint에 더하지 않고 그 판의 udg_Save_playpoint로
//   덮어쓰고(누적 소실), 신세계 클리어 보너스(yuca_bonus)도 안 들어간다. 저장 직후
//   udg_Save_playpoint를 0으로 리셋하기까지 한다 — "누적 진행 저장"이 아니라 "이번 접속
//   스냅샷을 코드로 뽑아 보여주기" 용으로 보인다.
//   우리는 SavePlayer(자동, 누적 + 보너스 포함) 쪽 식만 채택했다 — "영속 저장"의 취지(진행이
//   끊기지 않고 쌓인다)에 맞는 건 이쪽이고, OnChatSave는 애초에 채팅 명령으로 세이브 코드
//   문자열을 뽑아내려던 임시 우회로라서 코드 문자열 자체를 안 만들기로 한 우리에게는
//   대응할 대상이 없다.
[Serializable]
public class PlayerSaveData
{
    public int cumulativePlayPoint;    // 누적 플레이 포인트 (원작 TEMPDATA_PLAYER_PLAYPOINT)
    public int cumulativeClearCount;   // 누적 클리어 횟수   (원작 TEMPDATA_PLAYER_PLAYTIME)
    public int bestRunPoint;           // 한 판 최고 점수    (원작 TEMPDATA_PLAYER_BESTPOINT)

    // 그릇만 준비— 원작 TEMPDATA_PLAYER_LEVEL(Save_playerlevel) 대응. 우리 쪽엔 아직
    // "플레이어 레벨" 개념 자체가 없어서 항상 0이다. 나중에 채울 때 이 필드를 그대로 쓰면 된다.
    public int playerLevel;
}
