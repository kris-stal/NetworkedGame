using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public class menuUIManager : MonoBehaviour
{   
    public static menuUIManager Instance { get; private set; } // Ensures that the menuManager can only be read by other scripts, but only this script can modify it.
    
    
    // Menu Manager script
    [SerializeField] private menuManager menuManagerInstance;
    [SerializeField] private playerProfileManager playerProfileManagerInstance;

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
            menuManagerInstance = menuManager.Instance;

            if (menuManagerInstance == null)
            {
                Debug.LogError("menuManager instance not found! Attempting to find in scene.");
                menuManagerInstance = FindFirstObjectByType<menuManager>();
                
                if (menuManagerInstance == null)
                {
                    Debug.LogError("menuManager not found in scene either!");
                }
            }
        }

        // Ensure player profile manager exists
        if (playerProfileManagerInstance == null)
        {
            playerProfileManagerInstance = playerProfileManager.Instance;

            if (playerProfileManagerInstance == null)
            {
                Debug.LogError("playerProfileManager instance not found! Attempting to find in scene.");
                playerProfileManagerInstance = FindFirstObjectByType<playerProfileManager>();
                
                if (playerProfileManagerInstance == null)
                {
                    Debug.LogError("playerProfileManager not found in scene either!");
                }
            }
        }

        // Check if player is signed in
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log($"Already signed in as {AuthenticationService.Instance.PlayerId}");
            ShowMainMenuScreen();

            // Retrieve player name from Cloud Save
            try
            {
                // Check if CloudSaveService.Instance is initialized
                if (CloudSaveService.Instance != null)
                {
                    string currentName = await playerProfileManagerInstance.GetPlayerName(); // Get the name from Cloud Save

                    if (!string.IsNullOrEmpty(currentName))
                    {
                        playerNameInput.text = currentName; // Pre-fill the input field
                    }
                    else
                    {
                        Debug.LogWarning("Player name not found in Cloud Save.");
                    }
                }
                else
                {
                    Debug.LogError("CloudSaveService.Instance is null. Make sure the service is initialized.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to get player name from Cloud Save: {ex.Message}");
            }
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
        
        // Start with the main menu panel active
        ShowSigninScreen();
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
        
        // // Refresh lobbies when panel is shown
        // _ = RefreshLobbyList();
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
        // Check if Menu Manager exists
        if (menuManagerInstance == null)
        {
            menuManagerInstance = menuManager.Instance;
            if (menuManagerInstance == null)
            {
                Debug.LogError("Cannot sign in: menuManager not found!");
                return;
            }
        }

        // Then get name
        if (string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            Debug.LogWarning("Please enter a player name");
            return;
        }

        // Set player name in menuManager
        menuManagerInstance.SetPlayerName(playerNameInput.text);
        
        try
        {
            // Check if already signed in
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                // Authenticate player only if not already signed in
                await menuManagerInstance.AuthenticatePlayer();
            }
            else
            {
                Debug.Log("Player was already authenticated");
            }

            // Update player name on backend, even if already signed in
            await AuthenticationService.Instance.UpdatePlayerNameAsync(playerNameInput.text);
            Debug.Log($"Player name updated to {playerNameInput.text}");

            // Show main menu after authentication confirmed
            ShowMainMenuScreen();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Authentication error: {e.Message}");
            // Stay on sign-in screen if authentication fails
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

        if (menuManagerInstance.IsHost)
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

    // Update lobby UI with current information
    private void UpdateLobbyUI()
    {
        if (!lobbyUI.activeSelf || menuManagerInstance == null) return;
        
        // Update lobby info
        lobbyNameText.text = "Lobby: " + menuManagerInstance.LobbyName;
        lobbyCodeText.text = "Code: " + menuManagerInstance.LobbyCode;
        playerCountText.text = "Players: " + menuManagerInstance.GetPlayerCount() + "/4";
        
        // Show start game button only for host
        startGameButton.gameObject.SetActive(menuManagerInstance.IsHost);
        
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
        List<string> playerNames = menuManagerInstance.GetPlayerNames();
        
        // Populate player list
        foreach (string playerName in playerNames)
        {
            GameObject playerItem = Instantiate(playerListItemPrefab, playerListContent);
            playerItem.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
        }
    }

    public void ShowReconnectionOption(string lobbyName)
    {
        if (reconnectionPanel != null)
        {
            reconnectionPanel.SetActive(true); // Show reconnection UI
        }

        if (reconnectionLobbyText != null)
        {
            reconnectionLobbyText.text = "Disconnected from: " + lobbyName + "\nWould you like to reconnect?";
        }

        Debug.Log("Reconnection option displayed for lobby: " + lobbyName);
    }

    public async void OnReconnectButtonClicked()
    {
        if (menuManager.Instance == null)
        {
            Debug.LogError("menuManager instance not found!");
            return;
        }

        reconnectionButton.interactable = false; // Disable button to prevent spam clicking
        bool success = await menuManager.Instance.ReconnectToGame();

        if (success)
        {
            Debug.Log("Reconnected successfully!");
            if (reconnectionPanel != null)
            {
                reconnectionPanel.SetActive(false); // Hide reconnection UI
            }
        }
        else
        {
            Debug.LogError("Failed to reconnect!");
            if (reconnectionErrorText != null)
            {
                reconnectionErrorText.text = "Failed to reconnect. The game may have ended.";
            }
            reconnectionButton.interactable = true; // Re-enable button if failed
        }
    }
}
