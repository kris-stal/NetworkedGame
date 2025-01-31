using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class lobbyUI : NetworkBehaviour
{

    [SerializeField] private Button startGameButton;
    [SerializeField] private Button playerReadyButton;
    [SerializeField] private Transform playerListParent;  // Parent object for player list items
    [SerializeField] private GameObject playerListItemPrefab;  // Player item prefab
    [SerializeField] private TextMeshProUGUI playerCountText;

    // Network Variables
    private NetworkVariable<int> playerCount = new NetworkVariable<int>(0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        // Only host should see start button
        startGameButton.gameObject.SetActive(IsHost);
        
        // Subscribe to player join/leave events
        NetworkManager.Singleton.OnClientConnectedCallback += HandlePlayerJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandlePlayerLeft;

        // Update UI for host (first to join as they start the lobby)
        UpdatePlayerList();
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
        // Clear existing list

        foreach (Transform child in playerListParent)
        {
            Destroy(child.gameObject);
        }

        // Get all players and create list items
        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            // Instatiate new list item
            GameObject newListItem = Instantiate(playerListItemPrefab, playerListParent);

            // Get NetworkObjectId
            ulong playerId = client.Key;

            if (client.Key == NetworkManager.Singleton.LocalClientId && IsHost)
            {
                // Update list to add host
                newListItem.GetComponentInChildren<TextMeshProUGUI>().text = $"Host {playerId}";
            }
            else 
            {
                // Update list to add player
                newListItem.GetComponentInChildren<TextMeshProUGUI>().text = $"Player {playerId}";
            }
        }
    }

    private void StartGame()
    {   
        SceneManager.LoadScene("BallArena");
    }

    private void UpdateUI()
    {
        UpdatePlayerList();
        
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
