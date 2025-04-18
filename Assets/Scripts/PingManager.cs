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


    
    // Mapping from player id to ping (in seconds or milliseconds)
    public Dictionary<string, float> playerPing = new Dictionary<string, float>();

    private void Awake() {
        if (Instance != null && Instance != this) {
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
            float sendTimestamp = Time.realtimeSinceStartup;
            string playerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            // Send our ping request to the server
            SendPingServerRpc(sendTimestamp, playerId);

            // Wait three seconds before sending the next ping request
            yield return new WaitForSeconds(2f);
        }
    }

    // Called periodically from the client to send a ping request.
    [ServerRpc(RequireOwnership = false)]
    public void SendPingServerRpc(float sendTime, string playerId)
    {
        // Immediately reply; this method runs on the server.
        RespondPingClientRpc(sendTime, playerId);
    }
    
    // This RPC goes to all clients, or can be targeted to just the sender.
    [ClientRpc]
    public void RespondPingClientRpc(float sendTime, string playerId)
    {
        // Calculate ping if this is the local client that initiated the ping.
        if (AuthenticationService.Instance.PlayerId == playerId) {
            float ping = Time.realtimeSinceStartup - sendTime;
            // Optionally, show the ping to the local UI immediately.
            // Also, send the value back to the server to update the global mapping.
            SendPingValueServerRpc(ping, playerId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendPingValueServerRpc(float ping, string playerId)
    {
        // Update the server’s dictionary for this player's ping value.
        playerPing[playerId] = ping;
        // Broadcast the updated ping to all clients (so all UIs can update)
        UpdatePlayerPingClientRpc(playerId, ping);
    }
    
    [ClientRpc]
    public void UpdatePlayerPingClientRpc(string playerId, float ping)
    {
        // For example, if you manage your player list UI centrally, notify it to update
        // the ping text for the player whose ID is "playerId".
        menuUIManagerInstance?.UpdatePlayerPing(playerId, ping);
    }
}
