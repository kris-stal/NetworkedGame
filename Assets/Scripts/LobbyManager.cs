using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Collections;

// Manager for Lobby Service
// Handles all lobby functionality (lobbies/players in the lobby)
// Including creating, searching, joining and leaving lobbies.
public class LobbyManager : NetworkBehaviour
{   
    // REFERENCES //
    // Singleton public reference
    public static LobbyManager Instance { get; private set; }

    // Other script references
    private CoreManager coreManagerInstance;
    private MenuUIManager menuUIManagerInstance;

    // VARIABLES //
    // Lobby-related variables
    private Lobby hostLobby;
    private Lobby joinedLobby;
    private float heartbeatTimer;
    private string playerName;


    
    // Constants
    private const float LOBBY_HEARTBEAT_INTERVAL = 15f;
    
    // Public variables
    public Lobby CurrentLobby => hostLobby ?? joinedLobby;
    public string LobbyName => CurrentLobby?.Name ?? "";
    public string LobbyCode => CurrentLobby?.LobbyCode ?? "";
    public Dictionary<string, bool> playerReadyStatus = new Dictionary<string, bool>();

    // Events
    public event EventHandler OnLeftLobby;
    public event EventHandler<LobbyEventArgs> OnJoinedLobby;
    public event EventHandler<LobbyEventArgs> OnJoinedLobbyUpdate;
    public event EventHandler<LobbyEventArgs> OnKickedFromLobby;
    public class LobbyEventArgs : EventArgs {
        public Lobby lobby;
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
    }

    public event EventHandler<OnLobbyListChangedEventArgs> OnLobbyListChanged;
    public class OnLobbyListChangedEventArgs : EventArgs {
        public List<Lobby> lobbyList;
    }


    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize timers
        heartbeatTimer = LOBBY_HEARTBEAT_INTERVAL;
    }

    private void Start()
    {
        // Get script instances
        coreManagerInstance = CoreManager.Instance;
        menuUIManagerInstance = coreManagerInstance.menuUIManagerInstance;
        
        // Subscribe to events
        OnJoinedLobby += HandleJoinedLobby;

        // NetworkManager Events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    private void Update()
    {
        // Handle lobby heartbeat
        HandleLobbyHeartbeat();
    }

    // Heartbeat function to keep server alive
    // Unity's Lobby Service automatically deletes lobbies when inactive for 30 seconds.
    // This heartbeat pings the lobby service every 15 seconds to keep it alive.
    private async void HandleLobbyHeartbeat()
    {
        if (hostLobby != null)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer < 0f)
            {
                heartbeatTimer = LOBBY_HEARTBEAT_INTERVAL; // 15 seconds

                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
                    Debug.Log("Sent heartbeat ping");
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogError($"Heartbeat failed: {e.Message}");
                }
            }
        }
    }



    // EVENT HANDLING //
    // Client Connected / disconnected
    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");

        // If this is the local client connection
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            string localPlayerId = AuthenticationService.Instance.PlayerId;
            Debug.Log($"Local connection from ClientID: {clientId}, PlayerID: {localPlayerId}");
        }
        else if (IsHost)
        {
            // A remote client connected to the host
            Debug.Log($"Remote connection to host from ClientID: {clientId}");

            // So update lobby after a delay to allow for correct lobby players after connection
            StartCoroutine(UpdateLobbyAndNotifyUI());
        }
    }

    private async void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");

        // If this is the local client, check if we were kicked from the lobby
        if (clientId == NetworkManager.Singleton.LocalClientId && joinedLobby != null)
        {
            try
            {
                // Check if we're still in the lobby
                Lobby currentLobbyState = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                
                if (!IsPlayerInLobby(currentLobbyState))
                {
                    Debug.Log("Kicked from Lobby!");
                    OnKickedFromLobby?.Invoke(this, new LobbyEventArgs { lobby = currentLobbyState });
                    joinedLobby = null;
                }
            }
            catch (LobbyServiceException e)
            {
                // If we can't get the lobby, we might have been kicked or the lobby was deleted
                Debug.Log($"Lobby no longer exists - likely kicked or lobby closed, exception: {e}");
                OnKickedFromLobby?.Invoke(this, new LobbyEventArgs { lobby = null });
                joinedLobby = null;
            }
        }
    }

    // Update lobby data with a delay
    private IEnumerator UpdateLobbyAndNotifyUI()
    {
        // Use a coroutine to handle the async operation
        Task<Lobby> updateTask = Task.Run(async () => {
            try {
                // Wait briefly for Unity services to update
                await Task.Delay(500);
                return await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
            } 
            catch (Exception e) {
                Debug.LogError($"Error updating lobby: {e.Message}");
                return joinedLobby;
            }
        });
        
        // Wait for the task to complete
        while (!updateTask.IsCompleted)
        {
            yield return null;
        }
        
        // Update the lobby reference with fresh data
        if (updateTask.IsCompletedSuccessfully)
        {
            joinedLobby = updateTask.Result;
            
            // Now invoke the event with updated data
            OnJoinedLobbyUpdate?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby });
        }
    }

    // Handle joining lobby event - for lobby service not actual connection
    private void HandleJoinedLobby(object sender, LobbyEventArgs e)
    {
        Debug.Log($"Player joined lobby: {e.PlayerName}");

        // Log current lobby players
        if (joinedLobby != null)
        {
            PrintPlayers(joinedLobby);
        }
    }



    // LOBBY MANAGEMENT //
    public async Task<bool> CreateLobby()
    {
        try
        {
            string lobbyName = playerName + "'s Lobby";
            int maxPlayers = 4;

            // (RELAY) Create a Relay allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            
            // (RELAY) Get the join code for clients to use
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            // (RELAY) Get the Relay server's endpoint
            var relayServerEndpoint = allocation.ServerEndpoints[0];
            string hostIP = relayServerEndpoint.Host;
            int hostPort = relayServerEndpoint.Port;
            Debug.Log($"Relay server info - IP: {hostIP}, Port: {hostPort}, JoinCode: {joinCode}");

            // (NGO) Configure the transport with the Relay data
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("Transport component not found on NetworkManager!");
                return false;
            }
            
            // (NGO) Setting up the relay on the transport
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);
            
            // Create Lobby with relay join code
            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    // Store the join code instead of direct IP/port
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            };

            // (LOBBY SERVICE) Create the lobby
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
            hostLobby = lobby;
            joinedLobby = lobby;

            // (NGO) Start as host
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool success = NetworkManager.Singleton.StartHost();
                if (!success)
                {
                    Debug.LogError("Failed to start as host!");
                    return false;
                }
                Debug.Log("Started as network host through Relay");
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creating lobby with relay: {e.Message}");
            return false;
        }
    }

    public async Task<List<Lobby>> SearchAndRefreshLobbies()
    {
        try
        {
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Count = 25, // Max results
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.AvailableSlots,
                        op: QueryFilter.OpOptions.GT,
                        value: "0") // Only open lobbies
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(
                        asc: false,
                        field: QueryOrder.FieldOptions.Created) // Newest first
                }
            };

            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            Debug.Log($"Found {queryResponse.Results.Count} lobbies");

            // Notify UI
            OnLobbyListChanged?.Invoke(this, new OnLobbyListChangedEventArgs { lobbyList = queryResponse.Results });

            return queryResponse.Results;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to search lobbies: {e.Message}");
            return new List<Lobby>();
        }
    }
    
    public async Task<bool> JoinLobbyById(string lobbyId)
    {
        try
        {
            // Join the lobby
            JoinLobbyByIdOptions joinLobbyByIdOptions = new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            };

            // (LOBBY SERVICE) Join the lobby
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, joinLobbyByIdOptions);
            this.joinedLobby = joinedLobby;

            Debug.Log($"Successfully joined lobby as {playerName}");

            // (RELAY) Get the relay join code from the lobby data
            if (joinedLobby.Data.TryGetValue("RelayJoinCode", out DataObject relayJoinCodeData))
            {
                string joinCode = relayJoinCodeData.Value;
                Debug.Log($"Retrieved relay join code: {joinCode}");

                // (RELAY) Join the relay with the join code
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                
                // (NGO) Set up the transport with the relay data
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
                    transport.SetRelayServerData(relayServerData);
                    
                    Debug.Log("Configured transport with relay data, connecting as client...");
                }
                else
                {
                    Debug.LogError("Transport component not found on NetworkManager!");
                    return false;
                }

                // (NGO) Start the client
                if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
                {
                    bool success = NetworkManager.Singleton.StartClient();
                    if (!success)
                    {
                        Debug.LogError("Failed to start as client!");
                        return false;
                    }
                    Debug.Log("Started as network client through Relay");
                }

                 // After successfully joining a lobby, notify UI
                OnJoinedLobby?.Invoke(this, new LobbyEventArgs { 
                    lobby = joinedLobby,
                    PlayerId = AuthenticationService.Instance.PlayerId,
                    PlayerName = playerName
                });

                return true;
            }
            else
            {
                Debug.LogError("Failed to find relay join code in lobby data");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby with relay: {e.Message}");
            return false;
        }
    }

   public async Task<bool> JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            JoinLobbyByCodeOptions joinLobbyByCodeOptions = new JoinLobbyByCodeOptions
            {
                Player = GetPlayer()
            };

            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinLobbyByCodeOptions);
            joinedLobby = lobby;

            // Output that joined lobby
            Debug.Log("Joined lobby with code " + lobbyCode);

            // Output player count
            PrintPlayers(joinedLobby);

            // Save this lobby as last joined lobby
            PlayerPrefs.SetString("LastJoinedLobby", lobby.Id);
            PlayerPrefs.Save();
            
            // Get the relay join code from the lobby data
            if (joinedLobby.Data.TryGetValue("RelayJoinCode", out DataObject relayJoinCodeData))
            {
                string joinCode = relayJoinCodeData.Value;
                Debug.Log($"Retrieved relay join code: {joinCode}");

                // Join the relay with the join code
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                
                // Set up the transport with the relay data
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
                    transport.SetRelayServerData(relayServerData);
                    
                    Debug.Log("Configured transport with relay data, connecting as client...");
                }
                else
                {
                    Debug.LogError("Transport component not found on NetworkManager!");
                    return false;
                }

                // Start the client
                if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
                {
                    bool success = NetworkManager.Singleton.StartClient();
                    if (!success)
                    {
                        Debug.LogError("Failed to start as client!");
                        return false;
                    }
                    Debug.Log("Started as network client through Relay");
                }
                
                // After successfully joining a lobby
                OnJoinedLobby?.Invoke(this, new LobbyEventArgs { 
                    lobby = joinedLobby,
                    PlayerId = AuthenticationService.Instance.PlayerId,
                    PlayerName = playerName
                });
                
                return true;
            }
            else
            {
                Debug.LogError("Failed to find relay join code in lobby data");
                return false;
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return false;
        }
    }

    // Leaving lobby (as client)
    public async Task<bool> LeaveLobby()
    {
        try
        {
            if (joinedLobby != null)
            {
                string playerId = AuthenticationService.Instance.PlayerId;

                if (IsHost) 
                {
                    await DeleteLobby(); // If host, delete lobby before leaving
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                    Debug.Log("Left lobby: " + joinedLobby.Name);
                }

                joinedLobby = null;
                hostLobby = null;

                // Shut down Network Manager connection
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                    Debug.Log("Network shutdown");
                }
                
                return true;
            }
            return false;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Failed to leave lobby: " + e.Message);
            return false;
        }
    }

    // Method to delete the lobby (only host can call this)
    public async Task<bool> DeleteLobby()
    {
        if (hostLobby == null)
        {
            Debug.LogError("Only the host can delete the lobby.");
            return false;
        }

        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(hostLobby.Id);
            Debug.Log("Lobby deleted: " + hostLobby.Name);

            hostLobby = null;
            joinedLobby = null;
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Failed to delete lobby: " + e.Message);
            return false;
        }
    }



    // PLAYER MANAGEMENT //
    // Local setter for the player name
    public void SetPlayerName(string name)
    {
        playerName = name;
    }

    // Check if player in lobby (true)
    private bool IsPlayerInLobby(Lobby lobby = null) {
        lobby = lobby ?? joinedLobby; // Use provided lobby or default to joinedLobby
        
        if (lobby != null && lobby.Players != null) {
            foreach (Player player in lobby.Players) {
                if (player.Id == AuthenticationService.Instance.PlayerId) {
                    return true;
                }
            }
        }
        return false;
    }

    public string HostPlayerId
    {
        get
        {
            if (CurrentLobby != null && CurrentLobby.Players != null && CurrentLobby.Players.Count > 0)
            {
                return CurrentLobby.Players[0].Id; // Assumes the host is the first player
            }
            return string.Empty;
        }
    }

    // Get new player data
    private Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName)}
            }
        };
    }

    // Output players in a specific lobby
    private void PrintPlayers(Lobby lobby)
    {
        Debug.Log("Players in lobby " + lobby.Name);
        foreach (Player player in lobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data["PlayerName"].Value);
        }
    }

    // Get player count in current lobby
    public int GetPlayerCount()
    {
        if (hostLobby != null)
        {
            return hostLobby.Players.Count;
        }
        else if (joinedLobby != null)
        {
            return joinedLobby.Players.Count;
        }
        
        return 0;
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateReadyStatusServerRpc(string playerId, bool isReady)
    {
        // Ensure this is only executed on the server
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("[ServerRpc] UpdateReadyStatusServerRpc called on a non-server instance!");
            return;
        }

        // Update the server's authoritative ready status
        if (playerReadyStatus.ContainsKey(playerId))
        {
            playerReadyStatus[playerId] = isReady;
        }
        else
        {
            playerReadyStatus.Add(playerId, isReady);
        }

        Debug.Log($"[Server] Updated ready status for player {playerId}: {isReady}");

        // Notify all clients about the updated ready status
        UpdateReadyStatusClientRpc(playerId, isReady);
    }

    [ClientRpc]
    private void UpdateReadyStatusClientRpc(string playerId, bool isReady)
    {
        // Instead of checking just IsServer, check that this instance is NOT a pure server.
        if (NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            Debug.Log("[ClientRpc] UpdateReadyStatusClientRpc called on server-only instance. Ignoring...");
            return;
        }

        // Update the ready status on all clients
        if (playerReadyStatus.ContainsKey(playerId))
        {
            playerReadyStatus[playerId] = isReady;
        }
        else
        {
            playerReadyStatus.Add(playerId, isReady);
        }

        Debug.Log($"[Client] Updated ready status for player {playerId}: {isReady}");

        // Update the UI for the player list
        menuUIManagerInstance.UpdatePlayerReadyStatus(playerId, isReady);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUpdateReadyStatusServerRpc(string playerId, bool isReady)
    {
        // Ensure this is only executed on the server
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("[ServerRpc] RequestUpdateReadyStatusServerRpc called on a non-server instance!");
            return;
        }

        // Call the existing method to update the ready status
        UpdateReadyStatusServerRpc(playerId, isReady);
    }


    // Check if all players are ready
    public bool AreAllPlayersReady()
    {
        if (CurrentLobby == null || CurrentLobby.Players == null)
        {
            Debug.LogWarning("No lobby or players found to check ready status.");
            return false;
        }

        Debug.Log("Checking if all players are ready...");
        Debug.Log("Current player ready statuses:");
        foreach (var kvp in playerReadyStatus)
        {
            Debug.Log($"Player {kvp.Key}: Ready = {kvp.Value}");
        }

        foreach (var player in CurrentLobby.Players)
        {
            Debug.Log($"Checking player {player.Id}...");
            if (!playerReadyStatus.TryGetValue(player.Id, out bool isReady) || !isReady)
            {
                Debug.Log($"Player {player.Id} is not ready.");
                return false; // If any player is not ready, return false
            }
        }

        Debug.Log("All players are ready!");
        return true; // All players are ready
    }



    // CLEANUP //
    public override void OnDestroy()
    {
        base.OnDestroy();

        // Unsubscribe from events
        OnJoinedLobby -= HandleJoinedLobby;
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }
}