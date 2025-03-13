using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Authentication;

public class MenuUIManager : MonoBehaviour
{   
    public static MenuUIManager Instance { get; private set; } // Ensures that the menuManager can only be read by other scripts, but only this script can modify it.
    
    
    // Menu Manager script
    [SerializeField] private MenuManager menuManagerInstance;
    [SerializeField] private LobbyManager lobbyManagerInstance;

    // Reference UI Elements
    // Sign In Sceen
    [SerializeField] private GameObject signinUI;
    [SerializeField] private TMPro.TMP_InputField playerNameInput;
    [SerializeField] private Button signInButton;

    // Main Menu Screen
    [SerializeField] private GameObject mainMenuUI;
    [SerializeField] private TMPro.TMP_InputField codeInputBox;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyWithCodeButton;
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


    // Ran when script is created - before Start
    private void Awake() {
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
        ShowSigninScreen();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private async void Start()
    {
        // Ensure Unity services are initialized
        await Unity.Services.Core.UnityServices.InitializeAsync();
        
        // Ensure Menu Manager exists
        if (menuManagerInstance == null)
        {
            menuManagerInstance = MenuManager.Instance;

            if (menuManagerInstance == null)
            {
                Debug.LogError("menuManager instance not found! Attempting to find in scene.");
                menuManagerInstance = FindFirstObjectByType<MenuManager>();
                
                if (menuManagerInstance == null)
                {
                    Debug.LogError("menuManager not found in scene either!");
                }
            }
        }


        // Check if player is already signed in
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log($"Already signed in as {AuthenticationService.Instance.PlayerId}");

            // 🔹 Retrieve stored player name
            string savedName = await AuthenticationService.Instance.GetPlayerNameAsync();
            if (!string.IsNullOrEmpty(savedName))
            {
                Debug.Log($"Restoring player name: {savedName}");
                menuManagerInstance.SetPlayerName(savedName);
            }
            else
            {
                Debug.LogWarning("No player name found, using default name.");
                menuManagerInstance.SetPlayerName("Player"); // Fallback name
            }

            ShowMainMenuScreen();
        }
        else
        {
            ShowSigninScreen();
        }
            
        // Set up button listeners
        signInButton.onClick.AddListener(OnSigninButtonClicked);
        
        createLobbyButton.onClick.AddListener(OnCreateLobbyButtonClicked);
        joinLobbyWithCodeButton.onClick.AddListener(OnJoinByCodeButtonClicked);
        
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
            menuManagerInstance = MenuManager.Instance;
            if (menuManagerInstance == null)
            {
                Debug.LogError("Cannot sign in: menuManager not found!");
                return;
            }
        }

        // Ensure player enters a name
        string playerName = playerNameInput.text.Trim();
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
                await menuManagerInstance.AuthenticatePlayer();
            }
            else
            {
                Debug.Log("Player was already authenticated.");
            }

            // 🔹 Store the player's name in Unity's Authentication system
            await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
            Debug.Log($"Player name updated to {playerName}");

            // Store locally
            menuManagerInstance.SetPlayerName(playerName);

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

        bool created = await menuManagerInstance.CreateLobby();
        
        if (created)
        {
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

        bool joined = await menuManagerInstance.JoinLobbyByCode(code);
        
        if (joined)
        {
            ShowLobbyScreen();
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
            ShowMainMenuScreen();
            return;
        }

        bool left = await menuManagerInstance.LeaveLobby();
        
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

        Debug.Log("Is Host: " + lobbyManagerInstance.IsHost);  // Debug to check if host status is correct

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
        if (menuManagerInstance == null) return;

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
