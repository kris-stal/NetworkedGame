using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

public class PingManager : NetworkBehaviour
{
    public static PingManager Instance { get; private set; }

    public Dictionary<string, float> playerPing = new Dictionary<string, float>();

    private CoreManager coreManagerInstance;
    private MenuUIManager menuUIManagerInstance;
    private GameUIManager gameUIManagerInstance;

    private Dictionary<string, float> pendingPingSendTimes = new Dictionary<string, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        coreManagerInstance = CoreManager.Instance;
        menuUIManagerInstance = coreManagerInstance.menuUIManagerInstance;
        gameUIManagerInstance = coreManagerInstance.gameUIManagerInstance;
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            StartCoroutine(PingLoop());
        }
    }

    private IEnumerator PingLoop()
    {
        while (true)
        {
            if (NetworkManager.Singleton.IsClient)
            {
                float sendTime = Time.realtimeSinceStartup;
                string playerId = AuthenticationService.Instance.PlayerId;

                // Store the send time so we can calculate RTT when the echo comes back
                pendingPingSendTimes[playerId] = sendTime;

                // Send the ping request to the server (just echo back)
                RequestPingEchoServerRpc(sendTime, playerId);
            }
            yield return new WaitForSeconds(2f);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPingEchoServerRpc(float sendTime, string playerId, ServerRpcParams rpcParams = default)
    {
        // Just echo back the sendTime to the client
        RespondPingEchoClientRpc(sendTime, playerId);
    }

    [ClientRpc]
    public void RespondPingEchoClientRpc(float sendTime, string playerId)
    {
        // Only the client who sent the ping should process this
        if (playerId != AuthenticationService.Instance.PlayerId)
            return;

        float ping = (Time.realtimeSinceStartup - sendTime) * 1000f;
        playerPing[playerId] = ping;

        // Send the measured ping to the server so it can update its dictionary and broadcast to all clients
        ReportPingToServerRpc(playerId, ping);

        // Update local UI
        UpdatePingUI(playerId, ping);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportPingToServerRpc(string playerId, float ping)
    {
        // Store the ping in the server's dictionary
        playerPing[playerId] = ping;

        // Broadcast to all clients (including host)
        UpdatePlayerPingClientRpc(playerId, ping);
    }

    [ClientRpc]
    public void UpdatePlayerPingClientRpc(string playerId, float ping)
    {
        playerPing[playerId] = ping;
        UpdatePingUI(playerId, ping);
    }

    private void UpdatePingUI(string playerId, float ping)
    {
        if (menuUIManagerInstance != null && menuUIManagerInstance.gameObject.activeSelf)
        {
            menuUIManagerInstance.UpdatePlayerPing(playerId, ping);
        }
        else if (gameUIManagerInstance != null && gameUIManagerInstance.gameObject.activeSelf)
        {
            gameUIManagerInstance.UpdatePlayerPing(playerId, ping);
        }
    }
}
