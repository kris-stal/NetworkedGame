using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

public class PingManager : NetworkBehaviour
{
    // REFERENCES
    public static PingManager Instance { get; private set; }

    // Reference other scripts
    private CoreManager coreManagerInstance;
    private MenuUIManager menuUIManagerInstance;
    private GameUIManager gameUIManagerInstance;

    // Mapping from player id to ping (in milliseconds)
    public Dictionary<string, float> playerPing = new Dictionary<string, float>();

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
        // Assign script references
        coreManagerInstance = CoreManager.Instance;
        menuUIManagerInstance = coreManagerInstance.menuUIManagerInstance;
        gameUIManagerInstance = coreManagerInstance.gameUIManagerInstance;
    }

    public override void OnNetworkSpawn()
    {
        // Only send pings if this instance is running as a client (even host acts as a client)
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

                // Send the ping request to the server
                RequestPingServerRpc(sendTime, playerId);
            }

            // Wait for 2 seconds before sending the next ping
            yield return new WaitForSeconds(2f);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPingServerRpc(float sendTime, string playerId, ServerRpcParams rpcParams = default)
    {
        // Calculate the round-trip time (RTT) on the server
        float currentTime = Time.realtimeSinceStartup;
        float ping = (currentTime - sendTime) * 1000f; // Convert to milliseconds

        // Update the server's ping dictionary
        if (playerPing.ContainsKey(playerId))
        {
            playerPing[playerId] = ping;
        }
        else
        {
            playerPing.Add(playerId, ping);
        }

        Debug.Log($"[Server] Calculated ping for player {playerId}: {ping} ms");

        // Broadcast the updated ping to all clients
        UpdatePlayerPingClientRpc(playerId, ping);
    }

    [ClientRpc]
    public void UpdatePlayerPingClientRpc(string playerId, float ping)
    {
        Debug.Log($"[ClientRpc] Updating ping for player {playerId}: {ping} ms");

        // Update the ping dictionary on the client
        if (playerPing.ContainsKey(playerId))
        {
            playerPing[playerId] = ping;
        }
        else
        {
            playerPing.Add(playerId, ping);
        }

        // Update the ping UI
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
