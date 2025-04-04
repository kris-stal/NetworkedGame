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

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    // Lobby-related variables
    private Lobby hostLobby;
    private Lobby joinedLobby;
    private float heartbeatTimer;
    private float lobbyUpdateTimer;
    private string playerName;
    
    // Constants
    private const float lobbyUpdateTimerMax = 2f;
    private float pingMeasurementTimer = 0f;
    private const float PING_MEASUREMENT_INTERVAL = 2f;
    
    // Public variables
    public Lobby HostLobby => hostLobby;
    public Lobby JoinedLobby => joinedLobby;
    public string LobbyName => (hostLobby != null) ? hostLobby.Name : (joinedLobby != null) ? joinedLobby.Name : "";
    public string LobbyCode => (hostLobby != null) ? hostLobby.LobbyCode : (joinedLobby != null) ? joinedLobby.LobbyCode : "";
    public bool IsHost => hostLobby != null;

    // Dictionary
    public Dictionary<string, float> playerPings = new Dictionary<string, float>();
    public Dictionary<string, ulong> playerClientIds = new Dictionary<string, ulong>(); 

    // Events
    public event EventHandler<LobbyEventArgs> OnLeftLobby;
    public event EventHandler<LobbyEventArgs> OnJoinedLobby;
    public event EventHandler<LobbyEventArgs> OnJoinedLobbyUpdate;
    public event EventHandler<LobbyEventArgs> OnKickedFromLobby;
    public class LobbyEventArgs : EventArgs {
        public Lobby lobby;
        public string PlayerId {get; set;}
        public string PlayerName {get; set;}
    }

    public event EventHandler<OnLobbyListChangedEventArgs> OnLobbyListChanged;
    public class OnLobbyListChangedEventArgs : EventArgs {
        public List<Lobby> lobbyList;
    }

    private bool hasRegisteredWithHost = false;



    // Awake is called first, before start
    private void Awake()
    {
        // If theres an instance already which is not this one
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject); // destroy this one to prevent duplicates
            return;
        }

        // Else, there is no other instance so set this as the instance
        Instance = this;
        DontDestroyOnLoad(gameObject);


        // Initialize timers
        heartbeatTimer = 15f;
        lobbyUpdateTimer = 3f;
    }

    private void Start()
    {
        // Subscribe to events
        OnJoinedLobby += HandleJoinedLobby;

        NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
        {
            Debug.Log($"Successfully connected to host as Client-{clientId}");

            // For local client connection
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                string localPlayerId = AuthenticationService.Instance.PlayerId;

                // Register player with both IDs
                RegisterPlayer(localPlayerId, clientId);

                // If client (not host), inform the host about our mapping
                if (!IsHost && !hasRegisteredWithHost)
                {
                    hasRegisteredWithHost = true;
                    Debug.Log($"Client informing host about mapping: {localPlayerId} -> {clientId}");
                    NotifyHostAboutPlayerMapping_ServerRpc(localPlayerId);
                }
            }
            else if (IsHost)
            {
                // Remote client connected to host - we'll register them once they identify themselves via RPC
                Debug.Log($"Remote client {clientId} connected to host, awaiting player identification");
            }

            // 🔍 Debug: Print full mapping on connect
            Debug.Log("Current playerClientIds mapping:");
            foreach (var kvp in playerClientIds)
            {
                Debug.Log($"PlayerID: {kvp.Key} -> ClientID: {kvp.Value}");
            }

            // 🔍 Debug: Print all tracked pings too
            Debug.Log("Current playerPings:");
            foreach (var kvp in playerPings)
            {
                Debug.Log($"PlayerID: {kvp.Key} -> Ping: {kvp.Value}ms");
            }
        };

        NetworkManager.Singleton.OnClientDisconnectCallback += (clientId) =>
        {
            Debug.LogError($"Disconnected from host. Client ID: {clientId}");

            UnRegisterPlayer(clientId);
        };
    }

    private void Update()
    {
        // Handle lobby heartbeat and updates
        HandleLobbyHeartbeat();
        HandleLobbyPollForUpdates(); 

        // Add ping measurement
        // Check if the instance is the host or a client
        if (NetworkManager.Singleton.IsHost)
        {
            // If we are the host, run the host ping measurement
            MeasureAndUpdatePingsForHost();
        }
        else if (NetworkManager.Singleton.IsConnectedClient)
        {
            // If we are a client, run the client ping measurement
            MeasureAndUpdatePingForClient();
        }
    }

    // Setter for player name
    public void SetPlayerName(string name)
    {
        playerName = name;
    }
    private void MeasureAndUpdatePingsForHost()
    {
        // Only measure ping if we're connected as a host
        if (!NetworkManager.Singleton.IsHost)
            return;

        pingMeasurementTimer += Time.deltaTime;

        if (pingMeasurementTimer < PING_MEASUREMENT_INTERVAL)
            return;

        pingMeasurementTimer = 0f;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
            return;

        // Measure ping for all connected clients
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // Skip self (host)
            if (clientId == NetworkManager.Singleton.LocalClientId)
                continue;

            float pingMs = transport.GetCurrentRtt(clientId) * 1000f;
            string playerId = GetPlayerIdByClientId(clientId);

            if (!string.IsNullOrEmpty(playerId))
                UpdatePlayerPing(playerId, pingMs);
        }

        // Optional: Set the host's own ping to 0
        string hostPlayerId = GetPlayerIdByClientId(NetworkManager.Singleton.LocalClientId);
        if (!string.IsNullOrEmpty(hostPlayerId))
            UpdatePlayerPing(hostPlayerId, 0f);
    }

    private void MeasureAndUpdatePingForClient()
    {
        if (!NetworkManager.Singleton.IsConnectedClient)
            return;

        pingMeasurementTimer += Time.deltaTime;

        if (pingMeasurementTimer < PING_MEASUREMENT_INTERVAL)
            return;

        pingMeasurementTimer = 0f;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
            return;

        // Measure RTT to the host
        float pingMs = transport.GetCurrentRtt(NetworkManager.ServerClientId) * 1000f;

        // Update *client’s* own ping
        string localPlayerId = GetPlayerIdByClientId(NetworkManager.Singleton.LocalClientId);
        if (!string.IsNullOrEmpty(localPlayerId))
            UpdatePlayerPing(localPlayerId, pingMs);

        // Update *host’s* ping (from client's perspective) — same RTT
        string hostPlayerId = GetPlayerIdByClientId(NetworkManager.ServerClientId);
        if (!string.IsNullOrEmpty(hostPlayerId))
            UpdatePlayerPing(hostPlayerId, pingMs);
    }





    // Heartbeat function to keep server alive
    private async void HandleLobbyHeartbeat()
    {
        if (hostLobby != null)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer < 0f)
            {
                float heartbeatTimerMax = 15f;
                heartbeatTimer = heartbeatTimerMax;

                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
                Debug.Log("Sent heartbeat ping");
            }
        }
    }

    // Update function for clients
    private async void HandleLobbyPollForUpdates()
    {
        if (joinedLobby != null)
        {
            lobbyUpdateTimer -= Time.deltaTime;
            if (lobbyUpdateTimer < 0f)
            {
                lobbyUpdateTimer = lobbyUpdateTimerMax;

                joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

                OnJoinedLobbyUpdate?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby });

                if (!IsPlayerInLobby()) {
                    // Player was kicked out of this lobby
                    Debug.Log("Kicked from Lobby!");

                    OnKickedFromLobby?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby });

                    joinedLobby = null;
                }
            }
        }
    }

    
    // Check if player in lobby (true)
    private bool IsPlayerInLobby() {
        if (joinedLobby != null && joinedLobby.Players != null) {
            foreach (Player player in joinedLobby.Players) {
                if (player.Id == AuthenticationService.Instance.PlayerId) {
                    // This player is in this lobby
                    return true;
                }
            }
        }
        return false;
    }

    public async Task<bool> CreateLobbyWithRelay()
    {
        try
        {
            string lobbyName = playerName + "'s Lobby";
            int maxPlayers = 4;

            // 1. Create a Relay allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            
            // 2. Get the join code for clients to use
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            // 3. Get the Relay server's endpoint for display purposes
            var relayServerEndpoint = allocation.ServerEndpoints[0];
            string hostIP = relayServerEndpoint.Host;
            int hostPort = relayServerEndpoint.Port;
            Debug.Log($"Relay server info - IP: {hostIP}, Port: {hostPort}, JoinCode: {joinCode}");

            // 4. Configure the transport with the Relay data
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("Transport component not found on NetworkManager!");
                return false;
            }
            
            // This is the critical part - setting up the relay on the transport
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);
            
            // 5. Create Lobby with relay join code
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

            // Create the lobby
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
            hostLobby = lobby;
            joinedLobby = lobby;

            // 6. Start as host
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

            // Notify UI or other listeners
            OnLobbyListChanged?.Invoke(this, new OnLobbyListChangedEventArgs { lobbyList = queryResponse.Results });

            return queryResponse.Results;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to search lobbies: {e.Message}");
            return new List<Lobby>();
        }
    }

    public async Task<bool> JoinLobbyByIdWithRelay(string lobbyId, string playerName)
    {
        try
        {
            // Join the lobby
            var joinOptions = new JoinLobbyByIdOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) }
                    }
                }
            };

            // Join the lobby
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, joinOptions);
            this.joinedLobby = joinedLobby;

            Debug.Log($"Successfully joined lobby as {playerName}");

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

    // Joining lobby via code input
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
            Debug.Log("Joined lobby with code" + lobbyCode);

            // Output player count
            PrintPlayers(joinedLobby);

            // save this lobby as last joined lobby for
            PlayerPrefs.SetString("LastJoinedLobby", lobby.Id);
            PlayerPrefs.Save();
            
            // Retrieve host connection details from lobby metadata
            if (joinedLobby.Data.TryGetValue("HostIP", out DataObject hostIPData) &&
                joinedLobby.Data.TryGetValue("HostPort", out DataObject hostPortData))
            {
                string hostIP = hostIPData.Value;
                int hostPort = int.Parse(hostPortData.Value);

                Debug.Log($"Joining lobby with Host IP: {hostIP}, Port: {hostPort}");

                // Configure NetworkManager to connect to host IP and port
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.SetConnectionData(hostIP, (ushort)hostPort);  // ✅ Use hostIP from lobby!
                    Debug.Log($"Client connecting to: {hostIP}:{hostPort}");
                }
            }

            // Start as client
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool success = NetworkManager.Singleton.StartClient();
                if (!success)
                {
                    Debug.LogError("Failed to start as client!");
                    return false;
                }
                Debug.Log("Started as network client");
            }
            
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return false;
        }
    }

    private void HandleJoinedLobby(object sender, LobbyEventArgs e)
    {
        Debug.Log($"Player joined lobby: {e.PlayerName}");
        
        // Register the player at lobby level (ping tracking only, no client ID yet)
        RegisterPlayer(e.PlayerId);
        
        // Note: We deliberately don't map client IDs here, as the network
        // connection might not be established yet
    }

    // Get player data
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

    // Leaving lobby
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

                playerPings.Remove(playerId);
                playerClientIds.Remove(playerId);
                Debug.Log($"Removed player {playerId} from mappings");

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

    // Get list of player names from current lobby
    public List<string> GetPlayerNames()
    {
        List<string> names = new List<string>();
        Lobby currentLobby = hostLobby ?? joinedLobby;

        if (currentLobby == null)
        {
            Debug.LogWarning("GetPlayerNames: No active lobby found.");
            return names;
        }

        if (currentLobby.Players == null)
        {
            Debug.LogWarning("GetPlayerNames: Lobby exists but Players list is null.");
            return names;
        }

    foreach (Player player in currentLobby.Players)
    {
        if (player == null)
        {
            Debug.LogWarning("GetPlayerNames: Found a null player in the lobby.");
            continue;
        }

        if (player.Data == null)
        {
            Debug.LogWarning($"GetPlayerNames: Player {player.Id} has null Data.");
            continue;
        }

        if (!player.Data.ContainsKey("PlayerName"))
        {
            Debug.LogWarning($"GetPlayerNames: Player {player.Id} is missing PlayerName data.");
            continue;
        }

        names.Add(player.Data["PlayerName"].Value);
    }

        return names;
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
    // Update ping for a player
    public void UpdatePlayerPing(string playerId, float ping)
    {
        playerPings[playerId] = ping;
    }

    // Get ping for a player by player ID
    public float GetPlayerPing(string playerId)
    {
        if (playerPings.TryGetValue(playerId, out float ping))
        {
            return ping;
        }
        return 0f;
    }

    public void MapPlayerIdToClientId(string playerId, ulong clientId)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError($"Attempted to map null/empty PlayerID to ClientID: {clientId}");
            return;
        }

        // Check if this player ID is already mapped to this client ID
        if (playerClientIds.TryGetValue(playerId, out ulong existingClientId) && existingClientId == clientId)
        {
            Debug.Log($"PlayerID: {playerId} is already mapped to ClientID: {clientId}. Skipping duplicate mapping.");
            return;
        }
        
        // Remove any existing mappings for this client ID if it's a different player
        string existingPlayerId = GetPlayerIdByClientId(clientId);
        if (existingPlayerId != null && existingPlayerId != playerId)
        {
            playerClientIds.Remove(existingPlayerId);
            Debug.Log($"Removed existing mapping for ClientID {clientId} (was mapped to {existingPlayerId}) due to new mapping for PlayerID: {playerId}");
        }
        
        // Add or update the new mapping
        playerClientIds[playerId] = clientId;
        Debug.Log($"Mapped PlayerID: {playerId} -> ClientID: {clientId}");
    }


    public string GetPlayerIdByClientId(ulong clientId)
    {
        foreach (var kvp in playerClientIds)
        {
            if (kvp.Value == clientId)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    // Get ping by client ID (used in game)
    public string GetPlayerPingByClientId(ulong clientId)
    {
        foreach (var kvp in playerClientIds)
        {
            if (kvp.Value == clientId)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    // Call this when players join lobby
    public void RegisterPlayer(string playerId = null, ulong clientId = 0)
    {
        // Default to local player if no ID provided
        if (string.IsNullOrEmpty(playerId))
        {
            playerId = AuthenticationService.Instance.PlayerId;
        }

        Debug.Log($"RegisterPlayer called for PlayerID: {playerId}, ClientID: {clientId}");

        
        // Track ping for this player if not already tracked
        if (!playerPings.ContainsKey(playerId))
        {
            playerPings[playerId] = 0;
            Debug.Log($"Added player {playerId} to ping tracking");
        }
        
        // Map client ID if provided and valid
        if (clientId != 0)
        {
            MapPlayerIdToClientId(playerId, clientId);
        }
    }

    public void UnRegisterPlayer(ulong clientId)
    {
        string playerId = GetPlayerIdByClientId(clientId);
        if (playerId != null)
        {
            playerPings.Remove(playerId);
            playerClientIds.Remove(playerId);
            Debug.Log($"Unregistered player {playerId} with ClientId {clientId}");
        }
    }



    // RPCS //
    [ServerRpc(RequireOwnership = false)]
    private void NotifyHostAboutPlayerMapping_ServerRpc(string playerId, ServerRpcParams rpcParams = default)
    {
        // Get the sender's client ID from the RPC parameters
        ulong sendingClientId = rpcParams.Receive.SenderClientId;
        
        Debug.Log($"Server RPC received from client {sendingClientId} for player {playerId}");
        
        // Map the player ID to the client ID
        MapPlayerIdToClientId(playerId, sendingClientId);
        Debug.Log($"Host received player mapping: PlayerID: {playerId} -> ClientID: {sendingClientId}");
        
        // Broadcast the mapping to all clients
        NotifyAllClientsAboutMapping_ClientRpc(playerId, sendingClientId);
    }

    // Update this method to avoid resetting clientId unnecessarily
    [ClientRpc]
    private void NotifyAllClientsAboutMapping_ClientRpc(string playerId, ulong clientId)
    {
        // Skip if we're the host since we already have this mapping
        if (IsHost) return;
        
        // Add the mapping on all clients
        MapPlayerIdToClientId(playerId, clientId);
        Debug.Log($"Client received player mapping: PlayerID: {playerId} -> ClientID: {clientId}");
    }
}