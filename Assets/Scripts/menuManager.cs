using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public class menuManager : MonoBehaviour
{

    public static menuManager Instance { get; private set; } // Ensures that the menuManager can only be read by other scripts, but only this script can modify it.

    // Reference other scripts
    [SerializeField] private NetworkManager networkManagerPrefab;
    [SerializeField] private menuUIManager menuUIManagerInstance;

    // Private Variables only this script accesses
    private Lobby hostLobby;
    private Lobby joinedLobby;
    private float heartbeatTimer;
    private float lobbyUpdateTimer;
    private string playerName;
    private bool isNetworkInitialized = false;
    private string lastGameLobbyId;
    private bool wasDisconnected = false;

    // Public Variables for UI script to get/read
    public Lobby HostLobby => hostLobby; // return values of the private variables
    public Lobby JoinedLobby => joinedLobby;
    public string LobbyName => (hostLobby != null) ? hostLobby.Name : (joinedLobby != null) ? joinedLobby.Name : ""; // return hostlobby name if host, joinedlobby name if client, or empty if neither
    public string LobbyCode => (hostLobby != null) ? hostLobby.LobbyCode : (joinedLobby != null) ? joinedLobby.LobbyCode : ""; // return hostlobby code if host, joinedlobby code if client, or empty if neither
    public bool IsHost => hostLobby != null; // is host if hostlobby is not null


    // Ran when script is created - before Start
    private void Awake()
    {
        // Set Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize timers
        heartbeatTimer = 15f;
        lobbyUpdateTimer = 1.1f;
    }

    // Start is called before first frame update, after Awake
    private async void Start()
    {   
    // Initialize Unity Services first
    try
    {
        await UnityServices.InitializeAsync();
        Debug.Log("Unity Services initialized successfully");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
        return;
    }

    // Find the UI manager if not assigned through Inspector
    if (menuUIManagerInstance == null)
        {
            menuUIManagerInstance = menuUIManager.Instance;
            
            if (menuUIManagerInstance == null)
            {
                Debug.LogError("menuUIManager instance not found! Attempting to find in scene.");
                menuUIManagerInstance = FindFirstObjectByType<menuUIManager>();
                
                if (menuUIManagerInstance == null)
                {
                    Debug.LogError("menuUIManager not found in scene either!");
                    return;
                }
            }
        }
        // IMPORTANT: Always show sign-in screen first
        menuUIManagerInstance.ShowSigninScreen();
        
        // No automatic authentication attempts
        // Let the player click the sign-in button explicitly
        Debug.Log("Showing sign-in screen. User must sign in manually.");
    }

    // Update is ran every frame
    private void Update()
    {
        HandleLobbyHeartbeat(); 
        HandleLobbyPollForUpdates();
    }

    // Initialize NetworkManager if not already initialized
    private void InitializeNetworkManager()
    {
        if (isNetworkInitialized) return;
        
        if (NetworkManager.Singleton == null)
        {
            if (networkManagerPrefab != null)
            {
                NetworkManager networkManagerInstance = Instantiate(networkManagerPrefab);
                DontDestroyOnLoad(networkManagerInstance.gameObject);
                Debug.Log("NetworkManager instantiated");
            }
            else
            {
                Debug.LogError("NetworkManager prefab not assigned in inspector!");
                return;
            }
        }
        else
        {
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
        }
        
        isNetworkInitialized = true;
        Debug.Log("NetworkManager initialized");
    }


    // Setter for player name
    public void SetPlayerName(string name)
    {
        playerName = name;
    }

    public async Task AuthenticatePlayer() 
    {
        try 
        {
            // Initialize Unity services if not already done
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                Debug.LogWarning("Unity Services are not initialized yet, intializing.");
                await UnityServices.InitializeAsync();
            }
            
            // Register event callback
            AuthenticationService.Instance.SignedIn += () => 
            {
                Debug.Log("Signed in: " + AuthenticationService.Instance.PlayerId);
            };

            // Sign in anonymously
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Authentication complete for player: " + playerName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Authentication failed: {e.Message}");
            throw;
        }
    }

    // Heartbeat function to keep server alive - by default the lobby service automatically shuts down a lobby for 30 seconds of inactivity
    private async void HandleLobbyHeartbeat() 
    {
        if (hostLobby != null) // If there is a lobby
        {
            heartbeatTimer -= Time.deltaTime; // timer goes down in time
            if (heartbeatTimer < 0f) // when timer reaches 0
            {   
                float heartbeatTimerMax = 15f; // set max time for timer
                heartbeatTimer  = heartbeatTimerMax; // reset timer

                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id); // send heartbeat to lobbyservice
                Debug.Log("Sent heartbeat ping");
            }
        }
    }


    // Update function for clients
    private async void HandleLobbyPollForUpdates() 
    {
        if (joinedLobby != null) // If there is a lobby that client is connected to
        {
            lobbyUpdateTimer -= Time.deltaTime; // timer goes down in time
            if (lobbyUpdateTimer < 0f) // when timer reaches 0
            {   
                float lobbyUpdateTimerMax = 3f; // set max time for timer
                lobbyUpdateTimer  = lobbyUpdateTimerMax; // reset timer

                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id); // update the lobby connected to
                joinedLobby = lobby; // set joined lobby as this currently connected lobby
            }
        }
    }

    
    // Creating the lobby
    public async Task<bool> CreateLobby() 
    {
        try 
        {
            string lobbyName = playerName + "'s Lobby";
            int maxPlayers = 4;

            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions // Create new options for lobby
            {
                IsPrivate = false, // Public game, no code required
                Player = GetPlayer() // Get host player
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions); // Create the lobby instance with the lobby options
            hostLobby = lobby;
            joinedLobby = lobby; // Host has also joined this lobby


            // Initialize the Network Manager
            InitializeNetworkManager();

            // Then start Hosting through the Network Manager
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

            // Output lobby details
            Debug.Log("Created lobby! " + lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);

            // Output players
            PrintPlayers(hostLobby);

            return true;

         } catch (LobbyServiceException e) 
         {
            Debug.Log(e);
            return false;
         }
    }

    // // Listing lobbies
    // private async void ListLobbies()
    // {
    //     try {  
    //         QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions // Create new options for search
    //         {
    //             Count = 25, // Show 25 lobbies
    //             Filters = new List<QueryFilter> // New filters
    //             {
    //                 new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT) // query filter to see lobbies will available slots > 0 
    //             },
    //             Order = new List<QueryOrder> // New order
    //             {
    //                 new QueryOrder(false, QueryOrder.FieldOptions.Created) // ascending, by time created
    //             }
    //         };
            
    //         QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryLobbiesOptions); // create the search


    //         // Output results of search
    //         Debug.Log("Lobbies found :" + queryResponse.Results.Count);
    //         foreach (Lobby lobby in queryResponse.Results)
    //             {
    //                 Debug.Log(lobby.Name + " " + lobby.MaxPlayers);
    //             }
    //     } catch (LobbyServiceException e)
    //     {
    //         Debug.Log(e);
    //     }
    // }

    // Joining lobby via code input
    public async Task<bool> JoinLobbyByCode(string lobbyCode) 
    {   
        try {  
            JoinLobbyByCodeOptions joinLobbyByCodeOptions = new JoinLobbyByCodeOptions { // Create new options for joining
                Player = GetPlayer() // Get player
            };

            // QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();
            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinLobbyByCodeOptions); // Joined lobby
            joinedLobby = lobby;

            // Initialize the Network Manager
            InitializeNetworkManager();

            // Start as client through the Network Manager
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                // Use host's relay connection info here if you're using Relay
                // For now, use localhost for testing
                bool success = NetworkManager.Singleton.StartClient();
                if (!success)
                {
                    Debug.LogError("Failed to start as client!");
                    return false;
                }
                Debug.Log("Started as network client");
            }

            // Output that joined lobby
            Debug.Log ("Joined lobby with code" + lobbyCode);

            // Output player count
            PrintPlayers(joinedLobby);

            return true;

        } catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return false;
        }
    }

    // // Joining lobby via quick join and the filters set
    // private async void JoinLobbyByQuickJoin() 
    // {
    //     try {  
    //         await LobbyService.Instance.QuickJoinLobbyAsync(); // Start quickjoin
        
    //     } catch (LobbyServiceException e)
    //     {
    //         Debug.Log(e);
    //     }
    // }

    // Get player name
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
    private void PrintPlayers()
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
        Debug.Log ("Players in lobby " + lobby.Name);
        foreach (Player player in lobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data["PlayerName"].Value);
        }
    }


    // Leaving lobby
    // Unity handles host migration automatically
    public async Task<bool> LeaveLobby()
    {
    {
        try
        {
            if (joinedLobby != null)
            {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                Debug.Log("Left lobby: " + joinedLobby.Name);

                // Shut down Network Manager connection
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    if (hostLobby != null)
                    {
                        NetworkManager.Singleton.Shutdown();
                        Debug.Log("Host network shutdown");
                    }
                    else
                    {
                        NetworkManager.Singleton.Shutdown();
                        Debug.Log("Client network shutdown");
                    }
                }
                
                if (hostLobby != null)
                {
                    hostLobby = null;
                }
                
                joinedLobby = null;
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
    }

    // Start game as host
    public void StartGame()
    {
        if (hostLobby != null)
        {
            Debug.Log("Starting game as host");
            
            // Make sure NetworkManager is already set up
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                // Load the game scene
                NetworkManager.Singleton.SceneManager.LoadScene("BallArena", LoadSceneMode.Single);
                Debug.Log("Loading scene: BallArena");
            }
            else
            {
                Debug.LogError("Network not initialized as host! Cannot start game.");
            }
        }
        else
        {
            Debug.LogError("Cannot start game: not a host");
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

    // Method to check if player was in a game and disconnected
    public async Task<bool> CheckForReconnection()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("Player not signed in, cannot check for reconnection");
            return false;
        }
        
        try
        {
            // Query for lobbies where this player is still listed
            // This only works if the lobby hasn't removed the player yet
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.MaxPlayers, AuthenticationService.Instance.PlayerId, QueryFilter.OpOptions.EQ)
                }
            };
            
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            
            if (response.Results.Count > 0)
            {
                // Found a lobby with this player
                Lobby reconnectionLobby = response.Results[0];
                lastGameLobbyId = reconnectionLobby.Id;
                wasDisconnected = true;
                
                // Show reconnection UI
                menuUIManagerInstance.ShowReconnectionOption(reconnectionLobby.Name);
                return true;
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Error checking for reconnection: {e.Message}");
        }
        
        return false;
    }

    // Method to rejoin the game
    public async Task<bool> ReconnectToGame()
    {
        if (!wasDisconnected || string.IsNullOrEmpty(lastGameLobbyId))
        {
            Debug.LogError("No game to reconnect to");
            return false;
        }
        
        try
        {
            // Join the lobby
            Lobby lobby = await LobbyService.Instance.JoinLobbyByIdAsync(lastGameLobbyId, new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            });
            
            joinedLobby = lobby;
            
            // Initialize the Network Manager
            InitializeNetworkManager();
            
            // Start as client
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool success = NetworkManager.Singleton.StartClient();
                if (!success)
                {
                    Debug.LogError("Failed to start as client for reconnection!");
                    return false;
                }
                
                // Set a flag to indicate this is a reconnection
                PlayerPrefs.SetInt("IsReconnecting", 1);
                PlayerPrefs.SetString("ReconnectAuthId", AuthenticationService.Instance.PlayerId);
                
                Debug.Log("Started as network client for reconnection");
            }
            
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to reconnect to game: {e.Message}");
            return false;
        }
    }
}
