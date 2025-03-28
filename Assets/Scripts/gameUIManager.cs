using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
using System;
using Unity.Multiplayer.Tools.NetStatsMonitor;
public class GameUIManager : NetworkBehaviour
{   
    // Singleton Pattern
    public static GameUIManager Instance { get; private set; }

    // Reference other scripts via CoreManager
    private CoreManager coreManagerInstance;
    private GameManager gameManagerInstance;

    private float lastResolutionPing = -5;


    // Variables
    // In Game UI
    [SerializeField] private GameObject gameUI;
    [SerializeField] private TextMeshProUGUI player1ScoreText;
    [SerializeField] private TextMeshProUGUI player2ScoreText;
    [SerializeField] private TextMeshProUGUI highPingWarningText;

    // UI elements for countdown and winner display
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private GameObject winnerPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Button newGameButton;

    // Tab Menu UI
    [SerializeField] private GameObject tabMenuUI;
    [SerializeField] private TextMeshProUGUI[] pingTexts;
    [SerializeField] private RuntimeNetStatsMonitor networkMonitor;

    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;

    // Menu UI
    [SerializeField] private GameObject menuUI;
    [SerializeField] private Button resumeGameButton;
    [SerializeField] private Button leaveGameButton;



    // Awake is called when the script instance is loaded, before Start
    private void Awake()
    {
        // Set Singleton Pattern
        // If theres an instance already which is not this one 
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject); // destroy this one to prevent duplicates
            return;
        }

        // Else, there is no other instance so set this as the instance
        Instance = this;

        // Hide network monitor by default
        networkMonitor.Visible = false; 


        // Button listeners
        resumeGameButton.onClick.AddListener(() => { // On resume game button click

            menuUI.gameObject.SetActive(false);
        });

        leaveGameButton.onClick.AddListener(() => { // On resume game button click

            gameManagerInstance.LeaveGame();
        });
    }

    private void Start()
    {
        // Hide countdown and winner panels initially
        ShowCountdown(false);
        ShowWinnerScreen(false);

        // Assign manager instances
        coreManagerInstance = CoreManager.Instance;
        gameManagerInstance = coreManagerInstance.gameManagerInstance;
        
        // Add listener to new game button
        if (newGameButton != null)
        {
            newGameButton.onClick.AddListener(OnNewGameButtonClicked);
        }
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created, after Awake
    private void Update()
    {
        if (gameManagerInstance == null || NetworkManager.Singleton == null) return;

        // Get all player objects
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        // Update ping display for each player
        for (int i = 0; i < players.Length && i < pingTexts.Length; i++)
        {
            ulong clientId = gameManagerInstance.GetPlayerClientId(players[i]);
            float ping = gameManagerInstance.GetPlayerPing(clientId);
            
            // Update the UI text
            pingTexts[i].text = $"Player {i+1} Ping: {ping:0}ms";
        }

        
        if ((!menuUI.gameObject.activeSelf) && Input.GetKeyDown(KeyCode.Escape)) // If menu ui isnt already up and player presses escape
        {
            menuUI.gameObject.SetActive(true); // show menu ui
        }
        else if (menuUI.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) // if menu ui is already up and player presses escape
        {
            menuUI.gameObject.SetActive(false); // hide menu ui 
        }

        // Show Tab Menu while Tab is held
        if (Input.GetKey(KeyCode.Tab))
        {
            tabMenuUI.gameObject.SetActive(true); // show tab menu while holding Tab
            networkMonitor.Visible = true;
        }
        else
        {
            tabMenuUI.gameObject.SetActive(false); // hide tab menu when Tab is released
            networkMonitor.Visible = false;
        }

        // Handle Score UI
        player1ScoreText.text = gameManagerInstance.GetPlayer1Score().ToString();
        player2ScoreText.text = gameManagerInstance.GetPlayer2Score().ToString();
    }

    // Toggle display of the high ping warning message
    public void toggleHighPingWarning(bool isActive)
    {
        if (highPingWarningText.gameObject.activeSelf != isActive)
        {
            highPingWarningText.gameObject.SetActive(isActive);
        }
    }

    public void changeResolution(float ping)
    {
        if (Mathf.Abs(ping - lastResolutionPing) < 10) return; // Avoid small fluctuations
        lastResolutionPing = ping;

        if (ping > 100)
        {
            Screen.SetResolution(640, 360, FullScreenMode.FullScreenWindow);
        }
        else if (ping > 50)
        {
            Screen.SetResolution(1280, 720, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
        }
        Debug.Log($"Resolution changed to: {Screen.width} x {Screen.height}");
    }

        // Methods for countdown display
    public void ShowCountdown(bool show)
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(show);
        }
    }
    
    public void UpdateCountdownText(string text)
    {
        if (countdownText != null)
        {
            countdownText.text = text;
        }
    }
    
    // Methods for winner display
    public void ShowWinnerScreen(bool show)
    {
        if (winnerPanel != null)
        {
            winnerPanel.SetActive(show);
        }
    }
    
    public void ShowWinnerScreen(string winner)
    {
        Debug.Log($"Showing winner screen: {winner}");
        if (winnerPanel != null && winnerText != null)
        {
            winnerText.text = winner;
            winnerPanel.SetActive(true);
        }
    }
    
    // Button click handler for new game
    private void OnNewGameButtonClicked()
    {
        if (gameManagerInstance != null)
        {
            gameManagerInstance.StartNewGame();
            ShowWinnerScreen(false);
        }
    }
    
}