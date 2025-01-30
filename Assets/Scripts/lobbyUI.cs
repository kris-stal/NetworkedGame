using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class lobbyUI : NetworkBehaviour
{

    [SerializeField] private Button startGameButton;
    [SerializeField] private Button playerReadyButton;
    [SerializeField] private Transform playerListParent;  // Parent object for player list items
    [SerializeField] private TextMesh playerCountText;

    // Network Variables
    private NetworkVariable<int> playerCount = new NetworkVariable<int>(0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Only host should see start button
        startGameButton.gameObject.SetActive(IsHost);
        
        // Subscribe to player join/leave events
        NetworkManager.Singleton.OnClientConnectedCallback += HandlePlayerJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandlePlayerLeft;
    }


    // Update is called once per frame
    void Update()
    {
        
    }

    private void HandlePlayerJoined(ulong clientId)
    {
        Debug.Log($"Player {clientId} has joined");
        // Update player count
        if (IsServer)  // Only server updates NetworkVariables
        {
            playerCount.Value++;
        }
        // Update UI
        UpdatePlayerList();
    }

    private void HandlePlayerLeft(ulong clientId)
    {
        Debug.Log($"Player {clientId} has left");
        // Update player count
        if (IsServer)
        {
            playerCount.Value--;
        }
        // Update UI
        UpdatePlayerList();
    }

    
    private void UpdatePlayerList()
    {

    }


    private void StartGame()
    {

    }

    private void UpdateUI()
    {

    }


    // OnDestroy is called when this script is destroyed for cleanup
    // Overriding NetworkBehaviour's OnDestroy, must be public as base method is public and cannot restrict it more when overriding.
    public override void OnDestroy()
    {   
        // Check if NetworkManager still exists
        if (NetworkManager.Singleton != null)

            // Unsubscribe from the event to prevent calling methods on destroyed objects
            // -= removes this method from the lists of methods to call when this event occurs
            NetworkManager.Singleton.OnClientConnectedCallback -= HandlePlayerJoined;
            NetworkManager.Singleton.OnClientConnectedCallback -= HandlePlayerLeft;
    }
}
