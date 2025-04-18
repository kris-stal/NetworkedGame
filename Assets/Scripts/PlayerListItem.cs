using TMPro;
using Unity.Services.Authentication;
using UnityEngine;
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
        this.playerPingText.text = "0";

        // Enable or disable the ready button based on whether this is the local player
        this.readyButton.interactable = isLocalPlayer;
    }



    void Awake()
    {
        this.readyButton.onClick.AddListener(toggleReady);
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
        lobbyManagerInstance?.UpdateReadyStatusServerRpc(playerId, isReady);
    }

    public void SetReadyStatus(bool isReady)
    {
        this.isReady = isReady;

        // Update the ready status UI (e.g., change button color or status icon)
        readyStatusImage.color = isReady ? Color.green : Color.red;
    }
}
