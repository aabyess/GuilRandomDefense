// GameChatBox가 열려 있는 동안 게임 단축키를 죽이는 스위치. SelectionManager(V/H/C, 드래그
// 선택)·RtsCameraController(WASD, Space/Home)가 각자 Update() 맨 위에서 이 값을 본다 —
// Input System은 텍스트 필드에 포커스가 있어도 Keyboard.current를 그대로 읽으므로, 채팅에
// "w"를 치면 카메라가 움직이고 "v"를 치면 유닛이 모인다(사장님 지시 2026-09-05로 발견).
// 매니저를 서로 참조시키지 않으려고 정적 값 하나로 뺐다 — GameChatBox 쪽만 켜고 끄면 된다.
public static class ChatInputGate
{
    public static bool IsOpen { get; set; }
}
