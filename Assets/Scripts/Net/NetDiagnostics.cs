using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// 러너에 붙는 진단 기록. 연결이 끊긴 **이유**를 로그에 남긴다 — Development 빌드는 스택이 길어
/// 예외 한 줄만으로는 원인이 안 보였다(09-26, 판 중간 80초쯤 호스트가 Photon Cloud 연결을 잃고
/// 재접속 ConnectToCloud가 ExceptionOnConnect로 실패, 클라가 같이 끊김 — 두 번 재현).
/// 10초마다 FPS·왕복 지연(RTT)도 찍는다 — 한 기계에서 두 판을 돌릴 때 프레임이 멈춰 끊기는지 가르려고.
/// </summary>
public class NetDiagnostics : MonoBehaviour, INetworkRunnerCallbacks
{
    NetworkRunner runner;
    float nextReport;
    float worstFrame;

    public void Attach(NetworkRunner networkRunner)
    {
        runner = networkRunner;
        runner.AddCallbacks(this);
        NetworkRunner.CloudConnectionLost += OnCloudConnectionLost;
    }

    void OnDestroy()
    {
        NetworkRunner.CloudConnectionLost -= OnCloudConnectionLost;
    }

    void Update()
    {
        worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
        if (runner == null || !runner.IsRunning || Time.realtimeSinceStartup < nextReport) return;
        nextReport = Time.realtimeSinceStartup + 10f;

        string rtt = "";
        if (!runner.IsServer)
            rtt = $", RTT {runner.GetPlayerRtt(runner.LocalPlayer) * 1000.0:F0}ms";
        Debug.Log($"[MP] 진단: FPS {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):F0}, 최악 프레임 {worstFrame * 1000f:F0}ms{rtt}, 접속 {System.Linq.Enumerable.Count(runner.ActivePlayers)}명");
        worstFrame = 0f;
    }

    static void OnCloudConnectionLost(NetworkRunner r, ShutdownReason reason, bool reconnecting)
    {
        Debug.LogWarning($"[MP] 진단: Photon Cloud 연결 끊김 — 이유 {reason}, 재접속 시도 {reconnecting}, IsServer={r.IsServer}");
    }

    public void OnShutdown(NetworkRunner r, ShutdownReason shutdownReason) =>
        Debug.LogWarning($"[MP] 진단: 러너 종료 — 이유 {shutdownReason}");

    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) =>
        Debug.LogWarning($"[MP] 진단: 서버와 끊김 — 이유 {reason}");

    public void OnConnectFailed(NetworkRunner r, NetAddress remoteAddress, NetConnectFailedReason reason) =>
        Debug.LogWarning($"[MP] 진단: 접속 실패 — 이유 {reason}");

    public void OnConnectedToServer(NetworkRunner r) => Debug.Log("[MP] 진단: 서버에 접속");

    public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerJoined(NetworkRunner r, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner r, PlayerRef player) { }
    public void OnInput(NetworkRunner r, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner r) { }
    public void OnSceneLoadStart(NetworkRunner r) { }
}
