using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;

public class MenuUIManager : MonoBehaviour
{   
    public static MenuUIManager Instance { get; private set; } // Ensures that the menuManager can only be read by other scripts, but only this script can modify it.
    
    
    // Reference other scripts via CoreManager
    private CoreManager coreManagerInstance;
    private MenuManager menuManagerInstance;
    private LobbyManager lobbyManagerInstance;


    // Reference UI Elements
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
    private List<GameObject> instantiatedLobbyItems = new List<GameObject>();



    [SerializeField] private GameObject reconnectionPanel;
    [SerializeField] private TextMeshProUGUI reconnectionLobbyText;
    [SerializeField] private TextMeshProUGUI reconnectionErrorText;
    [SerializeField] private Button reconnectionButton;

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
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private async void Start()
    {
        // Assign manager instances
        coreManagerInstance = CoreManager.Instance;
        lobbyManagerInstance = coreManagerInstance.lobbyManagerInstance;
        menuManagerInstance = coreManagerInstance.menuManagerInstance;

        bool isUnityServiceInitialized = await coreManagerInstance.InitializeUnityServices();
        if (isUnityServiceInitialized)
        {
        // Check if player is already signed in
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log($"Already signed in as {AuthenticationService.Instance.PlayerId}");

            // 🔹 Retrieve stored player name
            string savedName = await AuthenticationService.Instance.GetPlayerNameAsync();
            if (!string.IsNullOrEmpty(savedName))
            {
                Debug.Log($"Restoring player name: {savedName}");
                lobbyManagerInstance.SetPlayerName(savedName);
            }
            else
            {
                Debug.LogWarning("No player name found, using default name.");
                lobbyManagerInstance.SetPlayerName("Player " + Random.Range(1, 99)); // Fallback name
            }

            ShowMainMenuScreen();
        }

        else
            {
                ShowSigninScreen();
            }
        }

        else 
        {
            Debug.LogError("Unity Services not initialized");
        }



        // Set up button listeners
        signInButton.onClick.AddListener(OnSigninButtonClicked);
        
        createLobbyButton.onClick.AddListener(OnCreateLobbyButtonClicked);
        joinLobbyWithCodeButton.onClick.AddListener(OnJoinByCodeButtonClicked);
        searchLobbiesButton.onClick.AddListener(async () => await SearchAndDisplayLobbies());

        startGameButton.onClick.AddListener(OnStartGameButtonClicked);
        leaveLobbyButton.onClick.AddListener(OnLeaveLobbyButtonClicked);
        startNetworkStressButton.onClick.AddListener(OnNetworkStressButtonClicked);
    }

    // Update is called once per frame
    void Update()
    {
        if (lobbyUI.activeSelf)
        {
            UpdateLobbyUI();
        }
    }

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
    
    private void ClearLobbyList()
    {
        foreach (GameObject item in instantiatedLobbyItems)
        {
            Destroy(item);
        }
        instantiatedLobbyItems.Clear();
    }
    
    private async void OnJoinLobbyClicked(string lobbyId)
    {
        // Call join lobby method from your lobby manager
        bool success = await lobbyManagerInstance.JoinLobbyByIdWithRelay(lobbyId, playerName);
        
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
    }

    // Event handlers for main menu
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
            Debug.Log($"Player name updated to {playerName}");

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


    // Event handlers for lobby selection
    private async void OnCreateLobbyButtonClicked()
    {
        if (menuManagerInstance == null)
        {
            Debug.LogError("Cannot create lobby: menuManager not found!");
            return;
        }

        bool created = await lobbyManagerInstance.CreateLobbyWithRelay();
        
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

    // Event handlers for lobby
    private void OnStartGameButtonClicked()
    {
        if (menuManagerInstance == null)
        {
            Debug.LogError("Cannot start game: menuManager not found!");
            return;
        }

        if (lobbyManagerInstance.IsHost)
        {
            menuManagerInstance.StartGame();
        }
        else
        {
            Debug.LogError("Only the host can start the game!");
        }
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

    // Update lobby UI with current information
    private void UpdateLobbyUI()
    {
        if (!lobbyUI.activeSelf || menuManagerInstance == null || lobbyManagerInstance == null) return;

        // Update lobby info
        lobbyNameText.text = "Lobby: " + lobbyManagerInstance.LobbyName;
        lobbyCodeText.text = "Code: " + lobbyManagerInstance.LobbyCode;
        playerCountText.text = "Players: " + lobbyManagerInstance.GetPlayerCount() + "/4";
        
        // Show start game button only for host
        startGameButton.gameObject.SetActive(lobbyManagerInstance.IsHost);
        startNetworkStressButton.gameObject.SetActive(lobbyManagerInstance.IsHost);
        
        // Update player list
        UpdatePlayerList();
    }

    private void UpdatePlayerList()
    {
        if (menuManagerInstance == null || lobbyManagerInstance == null ) return;

        // Clear current player list
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }
        
        // Get player names
        List<string> playerNames = lobbyManagerInstance.GetPlayerNames();
        
        // Populate player list
        foreach (string playerName in playerNames)
        {
            GameObject playerItem = Instantiate(playerListItemPrefab, playerListContent);
            playerItem.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
        }
    }
}
