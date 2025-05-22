using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Manager for individual player items in UI list
public class PlayerListItem : MonoBehaviour
{   
    // REFERENCES //
    private CoreManager coreManagerInstance;
    private LobbyManager lobbyManagerInstance;



    // UI ELEMENTS //
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerPingText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Image readyStatusImage;



    // VARIABLES
    private string playerId;
    private bool isReady;
    public string PlayerId => playerId;



    // INITIALIZATION
    public void Initialize(string playerName, string playerId, bool isLocalPlayer)
    {
        // Assign references
        coreManagerInstance = CoreManager.Instance;
        lobbyManagerInstance = coreManagerInstance.lobbyManagerInstance;

        // Initial UI and variables
        this.playerId = playerId;
        this.playerNameText.text = playerName;
        this.isReady = false;
        this.playerPingText.text = "";

        // Enable or disable the ready button based on whether this is the local player
        this.readyButton.interactable = isLocalPlayer;

            // Hide ready button if in game scene
        if (SceneManager.GetActiveScene().name == "BallArena")
        {
            HideReadyButton();
        }
        else
        {
            this.readyButton.gameObject.SetActive(true);
            this.readyButton.interactable = isLocalPlayer;
        }
    }



    void Awake()
    {
        this.readyButton.onClick.AddListener(toggleReady);
    }


    // UI //
    public void ShowReadyButton()
    {
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(true);
        }
    }
    
    public void HideReadyButton()
    {
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(false);
        }
    }



    // READY //
    public void toggleReady()
    {
        Debug.Log($"Player {playerId} pressed ready button!");

        // Ensure this is the local player
        if (playerId != AuthenticationService.Instance.PlayerId)
        {
            Debug.LogError($"[toggleReady] Player {playerId} tried to toggle ready status, but this is not the local player!");
            return;
        }

        // Swap ready status
        isReady = !isReady;
        Debug.Log($"Player {playerId} ready status: {isReady}");

        // Notify LobbyManager about the change
        if (NetworkManager.Singleton.IsServer)
        {
            // Call the ServerRpc directly if this is the server
            lobbyManagerInstance?.UpdateReadyStatusServerRpc(playerId, isReady);
        }
        else
        {
            // Call the new ServerRpc to notify the server
            lobbyManagerInstance?.RequestUpdateReadyStatusServerRpc(playerId, isReady);
        }
    }

    public void SetReadyStatus(bool isReady)
    {
        this.isReady = isReady;

        // Update the ready status UI (e.g., change button color or status icon)
        readyStatusImage.color = isReady ? Color.green : Color.red;
    }



    // PING //
    public void UpdatePing(float ping)
    {
        // Check if this player is the host (using LobbyManager's HostPlayerId)
        if (lobbyManagerInstance != null &&
            !string.IsNullOrEmpty(lobbyManagerInstance.HostPlayerId) &&
            this.PlayerId == lobbyManagerInstance.HostPlayerId)
        {
            playerPingText.text = "HOST";
            playerPingText.color = Color.cyan;
            return;
        }

        // Otherwise, update and display the ping value
        if (ping < 1f) ping = 1f; // Ensure a minimum display value

        playerPingText.text = $"{ping:F0} ms";

        // Adjust the color based on the ping value
        if (ping > 100f)
        {
            playerPingText.color = Color.red;
        }
        else if (ping > 50f)
        {
            playerPingText.color = Color.yellow;
        }
        else
        {
            playerPingText.color = Color.green;
        }
    }
}
