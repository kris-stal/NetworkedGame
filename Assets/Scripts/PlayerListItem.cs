using TMPro;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UI;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerPingText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Image readyStatusImage;

    private CoreManager coreManagerInstance;
    private LobbyManager lobbyManagerInstance;

    private string playerId;
    private ulong clientId;
    private bool isReady = false;
    private float updateTimer = 0f;
    private const float UPDATE_INTERVAL = 1f; // Update ping display every second

    public bool IsReady => isReady;

    public void Initialize(string playerName, string playerId, ulong clientId = 0)
    {
        this.playerNameText.text = playerName;
        this.playerId = playerId;
        this.clientId = clientId;

        // Check if this is the local player for button interactivity
        bool isLocalPlayer = playerId == AuthenticationService.Instance.PlayerId;

        // Always show the ready button, but only make it interactive for the local player
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(true);
            readyButton.interactable = isLocalPlayer;

            // Only add the click listener for the local player
            if (isLocalPlayer)
            {
                readyButton.onClick.AddListener(ToggleReady);
            }
        }

        // Set initial ready status
        if (readyStatusImage != null)
        {
            readyStatusImage.color = Color.red; // Default to not ready
        }

        // Set initial ping
        UpdatePingDisplay();
    }

    void Awake()
    {
        // Assign manager instances
        coreManagerInstance = CoreManager.Instance;
        lobbyManagerInstance = coreManagerInstance.lobbyManagerInstance;
    }

    // Update is called once per frame
    void Update()
    {
        // Update ping display periodically
        updateTimer += Time.deltaTime;
        if (updateTimer >= UPDATE_INTERVAL)
        {
            updateTimer = 0f;
            UpdatePingDisplay();
        }
    }

    public void UpdatePingDisplay()
    {
        // Early return if playerId or required references are not set
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogWarning("UpdatePingDisplay: playerId is null or empty!");
            return;
        }

        if (lobbyManagerInstance == null)
        {
            Debug.LogWarning("UpdatePingDisplay: lobbyManagerInstance is null!");
            return;
        }

        if (playerPingText == null)
        {
            Debug.LogWarning("UpdatePingDisplay: playerPingText is null!");
            return;
        }

        // Check if this is the host and handle separately
        if (lobbyManagerInstance.IsHost && playerId == AuthenticationService.Instance?.PlayerId)
        {
            // For host, display "Host" instead of ping
            playerPingText.text = "Host";
            return;
        }

        // Otherwise, get the ping for the player from the lobby manager
        float ping = lobbyManagerInstance.GetPlayerPing(playerId);

        // Format ping display (with color based on ping value)
        string pingText;
        if (ping < 50)
            pingText = $"<color=green>{ping:F0} ms</color>";
        else if (ping < 100)
            pingText = $"<color=yellow>{ping:F0} ms</color>";
        else
            pingText = $"<color=red>{ping:F0} ms</color>";

        playerPingText.text = pingText;
    }

    private void ToggleReady()
    {
        isReady = !isReady;

        // Update visual state
        if (readyStatusImage != null)
        {
            readyStatusImage.color = isReady ? Color.green : Color.red;
        }

        // Here you would notify other players of ready status change
        // This depends on your network implementation
        // Example: GameManager.Instance.SetPlayerReady(isReady);
    }
}
