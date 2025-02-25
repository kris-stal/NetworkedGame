using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

// Main Game Manager script for handling game and networking logic
public class gameManager : NetworkBehaviour
{
    // Singleton Pattern for easy access to the gameManager from other scripts and to prevent duplicates
    public static gameManager Instance { get; private set; }
    

    // Variables
    // Network variables for player scores
    private NetworkVariable<int> playerScore1 = new NetworkVariable<int>(
        0,  // Initial value
        NetworkVariableReadPermission.Everyone,  // Everyone can read the score
        NetworkVariableWritePermission.Server    // Only server can modify
    );

    private NetworkVariable<int> playerScore2 = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server 
    );

    // Other Network Variables
    [SerializeField] private gameUIManager gameUIManagerInstance;  // Get gameUIManager script
    private float pingUpdateInterval = 3f; // How often ping update is carried out
    private float timeSinceLastPingUpdate = 0f; // Timer for ping update
    private Dictionary<ulong, float> playerPings = new Dictionary<ulong, float>(); // Player pings dictionary

    // Game Variables
    GameObject theBall; // Get ball Game Object
    private List<GameObject> playerObjects = new List<GameObject>(); // List of player Game Objects
    private List<Vector3> playerSpawnPos = new List<Vector3>(); // List of spawn positions for players


    // Awake is called when the script instance is loaded, before Start
    private void Awake()
    {
        // Ensuring Singleton Pattern
        // Check this script already exists
        if (Instance == null)
        {
            // If not, set this instance as the singleton
            Instance = this;
        }
        else
        {
            // If an instance already exists, destroy the duplicate
            Destroy(gameObject);
        }
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created, after Awake
    void Start()
    {
        // Find the ball Game Object
        theBall = GameObject.FindGameObjectWithTag("Ball");
        
        // Spawn Positions
        playerSpawnPos.Add(new Vector3(-5, 1, 0)); // spawn pos 1
        playerSpawnPos.Add(new Vector3(5, 1, 0)); // spawn pos 2
        Debug.Log(playerSpawnPos.Count); // Output num of spawn pos for testing
        
        // Find all players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Debug.Log($"Found {players.Length} players");

        // Add all players to list of Player Objects
        foreach (GameObject player in players)
        {
            playerObjects.Add(player);
            Debug.Log($"Added player with NetworkObjectId: {player.GetComponent<NetworkObject>().NetworkObjectId}");
        }
    }


    // Called every frame
    private void Update()
    {
        // Update ping values periodically
        timeSinceLastPingUpdate += Time.deltaTime;
        if (timeSinceLastPingUpdate >= pingUpdateInterval)
        {
            timeSinceLastPingUpdate = 0f;
            UpdatePingValues();
        }
        
        // Check for unstable connection (example threshold)
        if (IsClient && !IsServer)
        {
            float myPing = GetPlayerPing(NetworkManager.Singleton.LocalClientId); // Get player's ping
            if (myPing > 100f) // 100ms threshold for unstable connection
            {
                // Display unstable connection message
                Debug.Log("Connection unstable: High ping detected (" + myPing + "ms)");
                gameUIManagerInstance.toggleHighPingWarning(true); // Show warning
            }
            else
            {
                gameUIManagerInstance.toggleHighPingWarning(false); // Hide warning
            }
        }
    }


    // Updating player's ping
    private void UpdatePingValues()
    {
        if (!NetworkManager.Singleton.IsListening) return;
        
        // For the server: get RTT for all connected clients
        if (IsServer)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId != NetworkManager.Singleton.LocalClientId) // Skip server itself
                {
                    try
                    {
                        // Access RTT through NetworkManager's transport layer
                        float rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId);
                        playerPings[clientId] = rtt;
                        
                        // Broadcast updated ping to all clients
                        UpdatePingClientRpc(clientId, rtt);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Error getting RTT for client {clientId}: {e.Message}");
                    }
                }
            }
        }

        // For clients: request their ping from the server
        if (IsClient && !IsServer)
        {
            RequestPingServerRpc();
        }
    }


    // Helper method to get the client ID of a player GameObject
    public ulong GetPlayerClientId(GameObject playerObject)
    {
        if (playerObject != null && playerObject.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            return netObj.OwnerClientId;
        }
        return 0;
    }


    // Get player ping
    public float GetPlayerPing(ulong clientId)
    {
        if (playerPings.TryGetValue(clientId, out float ping))
        {
            return ping;
        }
        return 0f;
    }


    // Leave game
    public void LeaveGame()
    {
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsClient)
            {
                // Destroy player object before disconnecting
                if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
                {
                    NetworkObject playerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
                    if (playerObject.IsOwner)
                    {
                        playerObject.Despawn(true); // Despawn on network
                    }

                    // Discconet
                    NetworkManager.Singleton.Shutdown();

                    // Load lobby scene again
                    SceneManager.LoadScene("LobbyMenu");
                }
            }
        }
    }


    // Find player count
    public void findPlayerCount()
    {
        int playerCount = playerObjects.Count; // int of num players in list of Player Objects
    }

    // Score Handling
    public void Score(bool isPlayer1Scored) 
    {
        if (!IsServer) return; // Only server can update score
        Debug.Log("Score called for " + (isPlayer1Scored ? "Player 1" : "Player 2"));

        if (isPlayer1Scored) // if player 1 scored
        {
            playerScore1.Value += 1; // update player 1 score
            Debug.Log("Player 1 score is now: " + playerScore1.Value);
        }
        else // else player 2 scored
        {
            playerScore2.Value += 1; // update player 2 score
            Debug.Log("Player 2 score is now: " + playerScore2.Value);
        }

        // reset ball pos
        ResetBallServerRpc();
        ResetPlayerServerRpc();
    }

    // Getting player scores
    public int GetPlayer1Score()
    {
        return playerScore1.Value;
    }

    public int GetPlayer2Score()
    {
        return playerScore2.Value;
    }

    // Remote Procedure Calls
    // Network Handling Calls

    // Client calls this to update their ping on the server
    [ServerRpc(RequireOwnership = false)]
    private void UpdatePingServerRpc(int pingValue, ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        playerPings[clientId] = pingValue;
        
        // Broadcast the updated ping to all clients
        UpdatePingClientRpc(clientId, pingValue);
        
        // Debug log to verify the ping is being received
        // Debug.Log($"Server received ping {pingValue}ms from client {clientId}");
    }


    // Client calls this to request their ping from server
    [ServerRpc(RequireOwnership = false)]
    private void RequestPingServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        try
        {
            float rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId);
            playerPings[clientId] = rtt;
            
            // Send ping value directly back to the requesting client
            UpdatePingClientRpc(clientId, rtt, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error getting RTT for client {clientId}: {e.Message}");
        }
    }


    // Client calls this to update their ping display
    [ClientRpc]
    private void UpdatePingClientRpc(ulong clientId, float pingValue, ClientRpcParams clientRpcParams = default)
    {
        playerPings[clientId] = pingValue;
    }
    

    // Game Handling Calls

    // Reset Ball
    [ServerRpc]
    private void ResetBallServerRpc()
    {
        if (theBall != null)
        {
            theBall.transform.position = new Vector3( 0, 1, 0);
            if (theBall.TryGetComponent<Rigidbody> (out Rigidbody rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.AddForce(new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * 10f, ForceMode.Impulse);
            }
        }
    }

    // Reset Player
    [ServerRpc]
    private void ResetPlayerServerRpc()
    {
        if (!IsServer) return;

        int playerCount = playerObjects.Count;
        Debug.Log($"Resetting {playerCount} players");

        for (int i = 0; i < playerCount; i++)
        {
            if (playerObjects[i] != null)
            {
                // Reset position and velocity
                playerObjects[i].transform.position = playerSpawnPos[i];
                if (playerObjects[i].TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.linearVelocity = Vector3.zero;
                }
                Debug.Log($"Reset player {i} to position {playerSpawnPos[i]}");
            }
        }
    }
}
