using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
using System;
public class gameUIManager : NetworkBehaviour
{   
    // Get Game Manager
    public static gameManager Instance { get; private set; }

    // Variables
    // In Game UI
    [SerializeField] private GameObject gameUI;
    [SerializeField] private TextMeshProUGUI player1ScoreText;
    [SerializeField] private TextMeshProUGUI player2ScoreText;
    [SerializeField] private TextMeshProUGUI[] pingTexts;
    [SerializeField] private TextMeshProUGUI highPingWarningText;

    // In Game Menu UI
    [SerializeField] private GameObject menuUI;
    [SerializeField] private Button resumeGameButton;
    [SerializeField] private Button leaveGameButton;

    
    // Awake is called when the script instance is loaded, before Start
    private void Awake()
    {
        resumeGameButton.onClick.AddListener(() => { // On resume game button click

            menuUI.gameObject.SetActive(false);
        });


        leaveGameButton.onClick.AddListener(() => { // On resume game button click

            gameManager.Instance.LeaveGame();
        });
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created, after Awake
    private void Update()
    {
        if (gameManager.Instance == null || NetworkManager.Singleton == null) return;

        // Get all player objects
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        // Update ping display for each player
        for (int i = 0; i < players.Length && i < pingTexts.Length; i++)
        {
            ulong clientId = gameManager.Instance.GetPlayerClientId(players[i]);
            float ping = gameManager.Instance.GetPlayerPing(clientId);
            
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

        // Handle Score UI
        player1ScoreText.text = gameManager.Instance.GetPlayer1Score().ToString();
        player2ScoreText.text = gameManager.Instance.GetPlayer2Score().ToString();
    }

    // Toggle display of the high ping warning message
    public void toggleHighPingWarning(Boolean boolean)
    {
        highPingWarningText.gameObject.SetActive(boolean);
    }
}