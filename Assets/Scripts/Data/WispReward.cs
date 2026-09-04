// StoryData.wispRewards가 쓴다. StoryRewardData(죽은 ScriptableObject, 참조 0건)가 원래
// 이 파일에 같이 있었으나 지웠다 — 이 클래스만 옮기면 6개 파일의 정의 위치가 바뀌는
// 위험 대비 이득이 안 맞아서, 파일을 통째로 StoryRewardData.cs → WispReward.cs로 개명했다
// (GUID 검색 0건 확인 후라 개명해도 깨질 참조가 없었다).
[System.Serializable]
public class WispReward
{
    public WispData wisp;
    public int count = 1;
}
