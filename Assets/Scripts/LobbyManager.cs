using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

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


    public event EventHandler OnLeftLobby;

    public event EventHandler<LobbyEventArgs> OnJoinedLobby;
    public event EventHandler<LobbyEventArgs> OnJoinedLobbyUpdate;
    public event EventHandler<LobbyEventArgs> OnKickedFromLobby;
    public class LobbyEventArgs : EventArgs {
        public Lobby lobby;
    }

    public event EventHandler<OnLobbyListChangedEventArgs> OnLobbyListChanged;
    public class OnLobbyListChangedEventArgs : EventArgs {
        public List<Lobby> lobbyList;
    }


    private void Awake()
    {
        // Set Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize timers
        heartbeatTimer = 15f;
        lobbyUpdateTimer = 3f;
    }

    private void Update()
    {
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

    // Creating the lobby
    public async Task<bool> CreateLobby()
    {
        try
        {
            string lobbyName = playerName + "'s Lobby";
            int maxPlayers = 4;

            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetPlayer()
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
            hostLobby = lobby;
            joinedLobby = lobby;

            // Output lobby details
            Debug.Log("Created lobby! " + lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);

            // Output players
            PrintPlayers(hostLobby);
    
            // save this lobby as last joined lobby
            PlayerPrefs.SetString("LastJoinedLobby", lobby.Id);
            PlayerPrefs.Save();

            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return false;
        }
    }

    // Add this method to your LobbyManager class
    public async Task<List<Lobby>> SearchLobbies()
    {
        try
        {
            // Set up query options
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Count = 25, // Maximum number of results to return
                Filters = new List<QueryFilter>
                {
                    // Only show lobbies that aren't full
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.AvailableSlots,
                        op: QueryFilter.OpOptions.GT,
                        value: "0")
                },
                Order = new List<QueryOrder>
                {
                    // Sort by creation time (newest first)
                    new QueryOrder(
                        asc: false,
                        field: QueryOrder.FieldOptions.Created)
                }
            };

            // Execute the query
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            
            Debug.Log($"Found {queryResponse.Results.Count} lobbies");
            
            return queryResponse.Results;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to search lobbies: {e.Message}");
            return new List<Lobby>();
        }
    }

    public async void RefreshLobbyList() {
        try {
            QueryLobbiesOptions options = new QueryLobbiesOptions();
            options.Count = 25;

            // Filter for open lobbies only
            options.Filters = new List<QueryFilter> {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.AvailableSlots,
                    op: QueryFilter.OpOptions.GT,
                    value: "0")
            };

            // Order by newest lobbies first
            options.Order = new List<QueryOrder> {
                new QueryOrder(
                    asc: false,
                    field: QueryOrder.FieldOptions.Created)
            };

            QueryResponse lobbyListQueryResponse = await LobbyService.Instance.QueryLobbiesAsync();

            OnLobbyListChanged?.Invoke(this, new OnLobbyListChangedEventArgs { lobbyList = lobbyListQueryResponse.Results });
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }
    }

        // Method to join a lobby by ID
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
            
            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            Debug.Log($"Joined lobby: {currentLobby.Name}");
            
            // Here you would typically start heartbeats and handle lobby events
            
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

            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return false;
        }
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
}