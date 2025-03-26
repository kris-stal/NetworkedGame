using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Unity.Netcode;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode.Transports.UTP;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    // Lobby-related variables
    private Lobby hostLobby;
    private Lobby joinedLobby;
    private float heartbeatTimer;
    private float lobbyUpdateTimer;
    private string playerName;
    private Lobby currentLobby;


    // Constants
    private const float lobbyUpdateTimerMax = 2f;
    
    // Public properties
    public Lobby HostLobby => hostLobby;
    public Lobby JoinedLobby => joinedLobby;
    public string LobbyName => (hostLobby != null) ? hostLobby.Name : (joinedLobby != null) ? joinedLobby.Name : "";
    public string LobbyCode => (hostLobby != null) ? hostLobby.LobbyCode : (joinedLobby != null) ? joinedLobby.LobbyCode : "";
    public bool IsHost => hostLobby != null;

    public Dictionary<string, float> playerPings = new Dictionary<string, float>();
    public Dictionary<string, ulong> playerClientIds = new Dictionary<string, ulong>(); 


    public event EventHandler OnLeftLobby;
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
    }

    private void Update()
    {
        // Handle lobby heartbeat and updates
        HandleLobbyHeartbeat();
        HandleLobbyPollForUpdates();
    }

    // Setter for player name
    public void SetPlayerName(string name)
    {
        playerName = name;
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

    // Get local IP address
    private string GetLocalIPAddress()
    {
        try 
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "127.0.0.1"; // Fallback to localhost
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    // Get current port from NetworkManager
    private int GetCurrentPort()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        return transport != null ? transport.ConnectionData.Port : 7777; // Default port
    }

    // Creating the lobby
    public async Task<bool> CreateLobby()
    {
        try
        {
            string lobbyName = playerName + "'s Lobby";
            int maxPlayers = 4;

            // Get host's IP and Port
            string hostIP = GetLocalIPAddress();
            int hostPort = GetCurrentPort();

            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    // Store host connection details in lobby metadata
                    { "HostIP", new DataObject(DataObject.VisibilityOptions.Public, hostIP) },
                    { "HostPort", new DataObject(DataObject.VisibilityOptions.Public, hostPort.ToString()) }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
            hostLobby = lobby;
            joinedLobby = lobby;

            // Output lobby details
            Debug.Log("Created lobby! " + lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);
            Debug.Log($"Lobby Host IP: {hostIP}, Port: {hostPort} ");

            // Output players
            PrintPlayers(hostLobby);
    
            // save this lobby as last joined lobby
            PlayerPrefs.SetString("LastJoinedLobby", lobby.Id);
            PlayerPrefs.Save();

            // Start hosting through NetworkManager
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool success = NetworkManager.Singleton.StartHost();
                if (!success)
                {
                    Debug.LogError("Failed to start as host!");
                    return false;
                }
                Debug.Log("Started as network host");
            }
            
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
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

    // Method to join a lobby by ID (from searching lobbies)
    public async Task<bool> JoinLobbyById(string lobbyId)
    {
        try
        {
            // Make sure player is authenticated
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogError("Player is not signed in");
                return false;
            }
            
            // Join the lobby
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            };
            
            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);

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
                    transport.SetConnectionData(hostIP, (ushort)hostPort);
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
            Debug.LogError($"Failed to join lobby: {e.Message}");
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
                    transport.SetConnectionData(hostIP, (ushort)hostPort);
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

        RegisterPlayer(e.PlayerId);
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

    // Output players for currently joined lobby
    public void PrintPlayers()  
    {
        if (hostLobby != null)
        {
            PrintPlayers(hostLobby);
        }
        else if (joinedLobby != null)
        {
            PrintPlayers(joinedLobby);
        }
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

                // try 
                // {
                //     AuthenticationService.Instance.SignOut();
                // }
                // catch (System.Exception e)
                // {
                //     Debug.LogError($"Error signing out: {e.Message}");
                // }
                
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
        Lobby currentLobby = hostLobby != null ? hostLobby : joinedLobby;
        
        if (currentLobby != null)
        {
            foreach (Player player in currentLobby.Players)
            {
                names.Add(player.Data["PlayerName"].Value);
            }
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

    // After game starts, map player IDs to client IDs
    public void MapPlayerIdToClientId(string playerId, ulong clientId)
    {
        playerClientIds[playerId] = clientId;
    }

    // Get ping by client ID (used in game)
    public float GetPlayerPingByClientId(ulong clientId)
    {
        foreach (var kvp in playerClientIds)
        {
            if (kvp.Value == clientId)
            {
                return GetPlayerPing(kvp.Key);
            }
        }
        return 0f;
    }

    // Call this when players join lobby
    public void RegisterPlayer(string playerId, ulong clientId = 0)
    {
        playerPings[playerId] = 0;
        if (clientId != 0)
        {
            playerClientIds[playerId] = clientId;
        }
    }
}