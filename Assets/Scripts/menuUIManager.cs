using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;

// UI Manager for menu
// Handles only the UI functionality
public class MenuUIManager : MonoBehaviour
{   
    // REFERENCES //
    // Singleton public reference
    public static MenuUIManager Instance { get; private set; }
    
    // Other script references
    private CoreManager coreManagerInstance;
    private MenuManager menuManagerInstance;
    private LobbyManager lobbyManagerInstance;
    private ReconnectManager reconnectManagerInstance;



    // UI ELEMENT REFERENCES //
    // Sign In Sceen
    [SerializeField] private GameObject signinUI;
    [SerializeField] private TMPro.TMP_InputField playerNameInput;
    [SerializeField] private Button signInButton;

    // Main Menu Screen
    [SerializeField] private GameObject mainMenuUI;
    [SerializeField] private TextMeshProUGUI usernameText;
    [SerializeField] private TMPro.TMP_InputField codeInputBox;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyWithCodeButton;
    [SerializeField] private Button searchLobbiesButton;
    [SerializeField] private GameObject lobbyListPanel;
    [SerializeField] private GameObject lobbyListItem;
    [SerializeField] private Transform lobbyListContent;
    
    // Lobby Screen 
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private Button startNetworkStressButton;
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;

    [SerializeField] private GameObject reconnectionPanel;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private TextMeshProUGUI reconnectLobbyText;
    


    // VARIABLES //
    // List of displaye lobbies
    private List<GameObject> instantiatedLobbyItems = new List<GameObject>();
    // List of displayer players
    private List<GameObject> instantiatedPlayerItems = new List<GameObject>();
    private string playerName;



    // Awake is ran when script is created - before Start
    private void Awake() {
        // Set Singleton Pattern
        // If theres an instance already which is not this one 
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject); // destroy this one to prevent duplicates
            return;
        }

        // Else, there is no other instance so set this as the instance
        Instance = this;


        // Show sign in screen by default
        ShowSigninScreen();
        reconnectionPanel.SetActive(false);
        reconnectButton.onClick.AddListener(OnReconnectButtonClicked);
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private async void Start()
    {
        // Assign script instances
        coreManagerInstance = CoreManager.Instance;
        lobbyManagerInstance = coreManagerInstance.lobbyManagerInstance;
        menuManagerInstance = coreManagerInstance.menuManagerInstance;
        reconnectManagerInstance = coreManagerInstance.reconnectManagerInstance;

        // Check if unity services initialized (should be true from CoreManager)
        bool isUnityServiceInitialized = await coreManagerInstance.InitializeUnityServices();
        if (isUnityServiceInitialized)
        {
        // Check if player is already signed in
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log($"Already signed in as {AuthenticationService.Instance.PlayerId}");

            // Retrieve stored player name
            string savedName = await AuthenticationService.Instance.GetPlayerNameAsync();
            if (!string.IsNullOrEmpty(savedName))
            {
                Debug.Log($"Restoring player name: {savedName}");
                lobbyManagerInstance.SetPlayerName(savedName);
            }
            else
            {
                // If no name found, preset a name with a random number
                Debug.LogWarning("No player name found, using default name.");
                lobbyManagerInstance.SetPlayerName("Player " + Random.Range(1, 99));
            }
            
            // Signed in, proceed to main menu
            ShowMainMenuScreen();
        }
        else
            {
                // Not signed in, ensure sign in screen shown
                ShowSigninScreen();
            }
        }
        else 
        {
            Debug.LogError("Unity Services not initialized");
        }

        // If we detect that we were disconnected and now in the lobby,
        // update the reconnection UI.
        if (lobbyManagerInstance.WasDisconnected)
        {
            // Here, lobbyManagerInstance should already contain the lobby information.
            if (lobbyManagerInstance != null)
            {
                // Update the reconnect panel text immediately, before any button click.
                UpdateReconnectionUI(lobbyManagerInstance.LobbyName);
            }
        }


        // Set up button listeners
        signInButton.onClick.AddListener(OnSigninButtonClicked);
        
        createLobbyButton.onClick.AddListener(OnCreateLobbyButtonClicked);
        joinLobbyWithCodeButton.onClick.AddListener(OnJoinByCodeButtonClicked);
        searchLobbiesButton.onClick.AddListener(async () => await SearchAndDisplayLobbies());

        startGameButton.onClick.AddListener(OnStartGameButtonClicked);
        leaveLobbyButton.onClick.AddListener(OnLeaveLobbyButtonClicked);
        startNetworkStressButton.onClick.AddListener(OnNetworkStressButtonClicked);


        // Set up event handling
        if (lobbyManagerInstance != null)
        {
            lobbyManagerInstance.OnJoinedLobby += (sender, args) => {
                Debug.Log("Player joined lobby - updating player list");
                ClearPlayerList();  
                UpdatePlayerList();
            };
            
            lobbyManagerInstance.OnLeftLobby += (sender, args) =>
            {
                Debug.Log("Player left lobby - updating player list");
                ClearPlayerList();
                UpdatePlayerList();
            };

            lobbyManagerInstance.OnJoinedLobbyUpdate += (sender, args) => {
                Debug.Log("Lobby updated - updating player list");
                ClearPlayerList();
                UpdatePlayerList();
            };
        }
    }


    // Update is called once per frame
    void Update()
    {
        // When in a lobby, keep updating lobby info (playercount)
        if (lobbyUI.activeSelf)
        {
            UpdateLobbyUI(); 
        }
    }

    // Update lobby UI with current information
    private void UpdateLobbyUI()
    {
        if (!lobbyUI.activeSelf || menuManagerInstance == null || lobbyManagerInstance == null) return;

        // Update lobby info
        lobbyNameText.text = lobbyManagerInstance.LobbyName;
        lobbyCodeText.text = "Code: " + lobbyManagerInstance.LobbyCode;
        playerCountText.text = "Players: " + lobbyManagerInstance.GetPlayerCount() + "/4";
        
        // Show start game button only for host
        startGameButton.gameObject.SetActive(lobbyManagerInstance.IsHost);
        startNetworkStressButton.gameObject.SetActive(lobbyManagerInstance.IsHost);
    }



    // LOBBIES LIST UI //
    // Search for lobbies via Lobby Service, then display in UI
    public async Task SearchAndDisplayLobbies()
    {
        // Clear previous lobby items
        ClearLobbyList();
        
        // Search for lobbies
        List<Lobby> lobbies = await lobbyManagerInstance.SearchAndRefreshLobbies();
        
        // Show the lobby panel
        lobbyListPanel.SetActive(true);

        // Display lobbies
        DisplayLobbies(lobbies);
    }

    // Display lobbies in UI
    private void DisplayLobbies(List<Lobby> lobbies)
    {
        if (lobbies.Count == 0)
        {
            // You might want to display a "No lobbies found" message
            Debug.Log("No lobbies found");
            return;
        }
        
        foreach (Lobby lobby in lobbies)
        {
            // Instantiate a lobby list item
            GameObject lobbyItem = Instantiate(lobbyListItem, lobbyListContent);
            
            // Set up the lobby item data
            LobbyListItem item = lobbyItem.GetComponent<LobbyListItem>();
            if (item != null)
            {
                item.Initialize(lobby);
                
                // Add join button functionality
                item.JoinButton.onClick.AddListener(() => OnJoinLobbyClicked(lobby.Id));
            }
            
            // Add to our list for cleanup later
            instantiatedLobbyItems.Add(lobbyItem);
        }
    }

    // Delete all displayed lobbies in UI
    private void ClearLobbyList()
    {
        foreach (GameObject item in instantiatedLobbyItems)
        {
            Destroy(item);
        }
        instantiatedLobbyItems.Clear();
    }



    // PLAYER LIST UI //
    // Create a UI for a player in lobby
    private GameObject CreatePlayerListItem(Player player)
    {
        Debug.Log($"Creating player item for {player.Id} with prefab: {playerListItemPrefab != null}");
        if (playerListItemPrefab == null)
        {
            Debug.LogError("playerListItemPrefab is not assigned!");
            return null;
        }
        
        if (playerListContent == null)
        {
            Debug.LogError("playerListContent is not assigned!");
            return null;
        }

        // Instantiate the player list item prefab
        GameObject playerItem = Instantiate(playerListItemPrefab, playerListContent);
        
        // Get the player name from data
        string playerName = "Unknown";
        if (player.Data.ContainsKey("PlayerName") && player.Data["PlayerName"] != null)
        {
            playerName = player.Data["PlayerName"].Value;
        }
        
        // Get the player's ID
        string playerId = player.Id;

        // Check if this is the local player
        bool isLocalPlayer = playerId == AuthenticationService.Instance.PlayerId;

        // Initialize the player list item component
        PlayerListItem item = playerItem.GetComponent<PlayerListItem>();
        if (item != null)
        {
            item.Initialize(playerName, playerId, isLocalPlayer);

            // Initialize ready status in LobbyManager
            if (!lobbyManagerInstance.playerReadyStatus.ContainsKey(playerId))
            {
                lobbyManagerInstance.playerReadyStatus[playerId] = false; // Default to not ready
            }
        }
        
        // Add to our list for cleanup later
        instantiatedPlayerItems.Add(playerItem);


          // Force UI refresh after adding items
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(playerListContent.GetComponent<RectTransform>());
        
        return playerItem;
    }

    // Delete all displayed players in UI
    public void ClearPlayerList()
    {
        foreach (GameObject item in instantiatedPlayerItems)
        {
            Destroy(item);
        }
        instantiatedPlayerItems.Clear();
    }

    // Update entire player list
    public void UpdatePlayerList()
    {
        Debug.Log("Updating player list...");

        if (menuManagerInstance == null || lobbyManagerInstance == null)
        {
            Debug.LogWarning("UpdatePlayerList: Missing manager instances");
            return;
        }

        Lobby currentLobby = lobbyManagerInstance.CurrentLobby;
        if (currentLobby == null || currentLobby.Players == null)
        {
            Debug.LogWarning("UpdatePlayerList: No active lobby or players found");
            return;
        }

        // Clear the current player list
        ClearPlayerList();

        // Rebuild the player list
        foreach (Player player in currentLobby.Players)
        {
            CreatePlayerListItem(player);
        }

        Debug.Log("Player list updated.");
    }

    public void UpdatePlayerReadyStatus(string playerId, bool isReady)
    {
        foreach (GameObject playerItemObject in instantiatedPlayerItems)
        {
            PlayerListItem item = playerItemObject.GetComponent<PlayerListItem>();
            if (item != null && item.PlayerId == playerId)
            {
                item.SetReadyStatus(isReady);
            }
        }
    }

    public void RemovePlayerFromUI(string playerId)
    {
        Debug.Log($"Removing player {playerId} from UI...");

        // Find the player item in the list
        GameObject playerItemToRemove = null;
        foreach (var playerItemObject in instantiatedPlayerItems)
        {
            PlayerListItem item = playerItemObject.GetComponent<PlayerListItem>();
            if (item != null && item.PlayerId == playerId)
            {
                playerItemToRemove = playerItemObject;
                break;
            }
        }

        // If found, remove it from the list and destroy the GameObject
        if (playerItemToRemove != null)
        {
            instantiatedPlayerItems.Remove(playerItemToRemove);
            Destroy(playerItemToRemove);
            Debug.Log($"Player {playerId} removed from UI.");
        }
        else
        {
            Debug.LogWarning($"Player {playerId} not found in UI.");
        }
    }

    public void ShowAllReadyButtons()
    {
        foreach (var playerItemObject in instantiatedPlayerItems)
        {
            PlayerListItem item = playerItemObject.GetComponent<PlayerListItem>();
            if (item != null)
            {
                item.ShowReadyButton();
            }
        }
    }



    // RECONNECTION UI //
    // Call this method when you detect a disconnect
    public void ShowReconnectionPanel()
    {
        reconnectionPanel.SetActive(true);
    }

    private void OnReconnectButtonClicked()
    {
        // Hide the reconnection panel.
        reconnectionPanel.SetActive(false);

        // Trigger the RPC call to attempt a reconnection.
        // This uses the persistent ReconnectManager so the logic will run even if other scene-specific managers (like GameManager) are not present
        reconnectManagerInstance.ReconnectToGameServerRpc(AuthenticationService.Instance.PlayerId);
    }

    public void UpdateReconnectionUI(string lobbyName)
    {
        if (reconnectLobbyText != null)
        {
            reconnectLobbyText.text = $"Reconnect to {lobbyName}";
        }
        
        // Optionally update other lobby details here (like lobby code, player count, etc.)
        
        // Show the reconnection panel.
        reconnectionPanel.SetActive(true);
    }



    // PING MANAGEMENT //
    public void UpdatePlayerPing(string playerId, float ping)
    {
        foreach (var playerItemObject in instantiatedPlayerItems)
        {
            PlayerListItem item = playerItemObject.GetComponent<PlayerListItem>();
            if (item != null && item.PlayerId == playerId)
            {
                item.UpdatePing(ping);
            }
        }
    }



    // UI SCREEN MANAGEMENT //
    public void ShowSigninScreen()
    {
        signinUI.SetActive(true);
        mainMenuUI.SetActive(false);
        lobbyUI.SetActive(false);
    }

    public void ShowMainMenuScreen()
    {
        signinUI.SetActive(false);
        mainMenuUI.SetActive(true);
        lobbyUI.SetActive(false);

        usernameText.text = playerName;
    }
        
    public void ShowLobbyScreen()
    {
        signinUI.SetActive(false);
        mainMenuUI.SetActive(false);
        lobbyUI.SetActive(true);

        UpdateLobbyUI();
        UpdatePlayerList();

        // Show all ready buttons
        ShowAllReadyButtons();
    }



    // BUTTON EVENTS //
    // Sign in screen handlers
    private async void OnSigninButtonClicked()
    {
        if (menuManagerInstance == null)
        {
                Debug.LogError("Cannot sign in: menuManager not found!");
                return;
        }

        // Ensure player enters a name
        playerName = playerNameInput.text.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning("Please enter a player name.");
            return;
        }

        try
        {
            // Authenticate if not already signed in
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await menuManagerInstance.Authenticate(playerName);
            }
            else
            {
                Debug.Log("Player was already authenticated.");
            }

            // 🔹 Store the player's name in Unity's Authentication system
            await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
            Debug.Log($"Player name: {playerName}");

            // Store locally
            lobbyManagerInstance.SetPlayerName(playerName);

            // Proceed to main menu
            ShowMainMenuScreen();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Authentication error: {e.Message}");
        }
    }

    // Main menu screen handlers
    private async void OnCreateLobbyButtonClicked()
    {
        if (menuManagerInstance == null)
        {
            Debug.LogError("Cannot create lobby: menuManager not found!");
            return;
        }

        bool created = await lobbyManagerInstance.CreateLobby();
        
        if (created)
        {
            lobbyListPanel.SetActive(false);
            ShowLobbyScreen();
        }
    }

    private async void OnJoinByCodeButtonClicked()
    {
        string code = codeInputBox.text.Trim();
        
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("Please enter a lobby code");
            return;
        }

        if (menuManagerInstance == null)
        {
            Debug.LogError("Cannot join lobby: menuManager not found!");
            return;
        }

        bool joined = await lobbyManagerInstance.JoinLobbyByCode(code);
        
        if (joined)
        {
            lobbyListPanel.SetActive(false);
            ShowLobbyScreen();
            UpdateLobbyUI();
        }
    }

    private async void OnJoinLobbyClicked(string lobbyId)
    {
        // Call join lobby method from your lobby manager
        bool success = await lobbyManagerInstance.JoinLobbyById(lobbyId);
        
        if (!success)
        {
            Debug.LogError("Failed to join lobby");
            return;
        }

        // show and clear lobby list UI
        lobbyListPanel.SetActive(false);
        ClearLobbyList();

        // Show and update lobby UI
        ShowLobbyScreen();
        UpdateLobbyUI();
    }

    // Lobby screen handlers
    private void OnStartGameButtonClicked()
    {
        if (menuManagerInstance == null)
        {
            Debug.LogError("Cannot start game: menuManager not found!");
            return;
        }

        if (!lobbyManagerInstance.IsHost)
        {
            Debug.LogError("Only the host can start the game!");
            return;
        }

        // Check if all players are ready
        if (!lobbyManagerInstance.AreAllPlayersReady())
        {
            Debug.LogWarning("Cannot start game: Not all players are ready!");
            return;
        }

        // Start the game
        menuManagerInstance.StartGame();
    }

    private async void OnLeaveLobbyButtonClicked()
    {
        if (menuManagerInstance == null)
        {
            Debug.LogError("Cannot leave lobby: menuManager not found!");
            return;
        }

        bool left = await lobbyManagerInstance.LeaveLobby();
        
        if (left)
        {
            ShowMainMenuScreen();
        }
    }

    private void OnNetworkStressButtonClicked()
    {
        if (menuManagerInstance == null)
        {
            Debug.LogError("Cannot start stress test: menuManager not found!");
            ShowMainMenuScreen();
            return;
        }

        if (lobbyManagerInstance.IsHost)
        {
            menuManagerInstance.StartStressTest();
        }
        else
        {
            Debug.LogError("Only the host can stress test the network!");
        }
    }
}
