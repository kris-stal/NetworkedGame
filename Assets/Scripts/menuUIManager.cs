using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class menuUIManager : MonoBehaviour
{   
    // Menu Manager script
    private menuManager menuManagerInstance;

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

    // Lobby Screen
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    // [SerializeField] private Transform playerListContent;
    // [SerializeField] private GameObject playerListItemPrefab;

    // Ran when script is created - before Start
    private void Awake() {
        menuManagerInstance = menuManager.Instance;
        
        if (menuManagerInstance == null)
        {
            Debug.LogError("menuManager instance not found!");
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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
        UpdateLobbyUI();
    }

    private void ShowSigninScreen()
    {
        signinUI.SetActive(true);
        mainMenuUI.SetActive(false);
        lobbyUI.SetActive(false);
    }

    private void ShowMainMenuScreen()
    {
        signinUI.SetActive(false);
        mainMenuUI.SetActive(true);
        lobbyUI.SetActive(false);
        
        // // Refresh lobbies when panel is shown
        // _ = RefreshLobbyList();
    }
    
    private void ShowLobbyScreen()
    {
        signinUI.SetActive(false);
        mainMenuUI.SetActive(false);
        lobbyUI.SetActive(true);
        
        UpdateLobbyUI();
    }

    // Event handlers for main menu
    private async void OnSigninButtonClicked()
    {
        if (string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            Debug.LogWarning("Please enter a player name");
            return;
        }
        
        // Set player name in menuManager
        menuManagerInstance.SetPlayerName(playerNameInput.text);
        
        // Authenticate player
        await menuManagerInstance.AuthenticatePlayer();
        
        // Show main menu after authentication
        ShowMainMenuScreen();
    }

    // Event handlers for lobby selection
    private async void OnCreateLobbyButtonClicked()
    {
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
        
        bool joined = await menuManagerInstance.JoinLobbyByCode(code);
        
        if (joined)
        {
            ShowLobbyScreen();
        }
    }

    // Event handlers for lobby
    private void OnStartGameButtonClicked()
    {
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
        bool left = await menuManagerInstance.LeaveLobby();
        
        if (left)
        {
            ShowMainMenuScreen();
        }
    }

    // Update lobby UI with current information
    private void UpdateLobbyUI()
    {
        if (!lobbyUI.activeSelf) return;
        
        // Update lobby info
        lobbyNameText.text = "Lobby: " + menuManagerInstance.LobbyName;
        lobbyCodeText.text = "Code: " + menuManagerInstance.LobbyCode;
        // playerCountText.text = "Players: " + menuManagerInstance.GetPlayerCount() + "/4";
        
        // Show start game button only for host
        startGameButton.gameObject.SetActive(menuManagerInstance.IsHost);
        
        // Update player list
        // UpdatePlayerList();
    }

    // private void UpdatePlayerList()
    // {
    //     // Clear current player list
    //     foreach (Transform child in playerListContent)
    //     {
    //         Destroy(child.gameObject);
    //     }
        
    //     // Get player names
    //     List<string> playerNames = menuManagerInstance.GetPlayerNames();
        
    //     // Populate player list
    //     foreach (string playerName in playerNames)
    //     {
    //         GameObject playerItem = Instantiate(playerListItemPrefab, playerListContent);
    //         playerItem.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
    //     }
    // }

}
