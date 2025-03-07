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

    // Countdown and game state variables
    private NetworkVariable<float> countdownTimer = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    private NetworkVariable<bool> gameInProgress = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    private NetworkVariable<bool> gameOver = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    private NetworkVariable<ulong> winnerClientId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    // Other Network Variables
    [SerializeField] private gameUIManager gameUIManagerInstance;  // Get gameUIManager script
    
    [System.Serializable] // Class to store player state when disconnected
    public class GamePlayerState
    {
        public string authId;
        public Vector3 position;
        public int score;
        public float disconnectTime;
        public ulong clientId;
    }

    private float pingUpdateInterval = 3f; // How often ping update is carried out
    private float timeSinceLastPingUpdate = 0f; // Timer for ping update
    private Dictionary<ulong, float> playerPings = new Dictionary<ulong, float>(); // Player pings dictionary

    private Dictionary<ulong, string> playerIds = new Dictionary<ulong, string>(); // Maps NetworkObject IDs to Auth IDs
    private Dictionary<string, GamePlayerState> disconnectedPlayerStates = new Dictionary<string, GamePlayerState>(); // Stores state of disconnected players
    private NetworkVariable<bool> waitingForReconnection = new NetworkVariable<bool>(false);
    private float reconnectionWaitTime = 30f; // Time to wait for reconnection before ending game
    private float reconnectionTimer = 0f;

    // Game Variables
    GameObject theBall; // Get ball Game Object
    private List<GameObject> playerObjects = new List<GameObject>(); // List of player Game Objects
    private List<Vector3> playerSpawnPos = new List<Vector3>(); // List of spawn positions for players
    private bool winnerScreenShown; // is winner screen shown

    // Constants
    private const int WIN_SCORE = 10;
    private const float INITIAL_COUNTDOWN = 5f;
    private const float RESET_COUNTDOWN = 3f;


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
        // Subscribe to client dis/connect event
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnect;

        // Find the ball Game Object
        theBall = GameObject.FindGameObjectWithTag("Ball");
        
        // Spawn Positions
        playerSpawnPos.Add(new Vector3(-5, 1, 0)); // spawn pos 1
        playerSpawnPos.Add(new Vector3(5, 1, 0)); // spawn pos 2
        Debug.Log(playerSpawnPos.Count); // Output num of spawn pos for testing

        winnerScreenShown = false;
        
        // Find all players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Debug.Log($"Found {players.Length} players");

        // Add all players to list of Player Objects
        foreach (GameObject player in players)
        {
            playerObjects.Add(player);
            Debug.Log($"Added player with NetworkObjectId: {player.GetComponent<NetworkObject>().NetworkObjectId}");
        }

        // Start initial countdown when server
        if (IsServer)
        {
            StartInitialCountdown();
        }

        gameInProgress.OnValueChanged += OnGameInProgressChanged;
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
        
        // Check for unstable connection
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

        // Handle countdown timer on server
        if (IsServer && countdownTimer.Value > 0)
        {
            countdownTimer.Value -= Time.deltaTime;
            
            // When countdown reaches zero, start the game
            if (countdownTimer.Value <= 0)
            {
                countdownTimer.Value = 0;
                
                if (!gameOver.Value)
                {
                    StartGameServerRpc();
                }
            }
        }

        // Update UI with countdown information
        if (IsClient)
        {
            // Only show countdown when timer is active
            if (countdownTimer.Value > 0)
            {
                int countdownSeconds = Mathf.CeilToInt(countdownTimer.Value);
                gameUIManagerInstance.UpdateCountdownText(countdownSeconds.ToString());
                gameUIManagerInstance.ShowCountdown(true);
            }
            else
            {
                gameUIManagerInstance.ShowCountdown(false);
            }
            
            // Show winner when game is over
            if (gameOver.Value && !winnerScreenShown)
            {
                string winnerText = "Player " + (winnerClientId.Value == 1 ? "1" : "2") + " Wins!";
                gameUIManagerInstance.ShowWinnerScreen(winnerText);
                winnerScreenShown = true;
            }
        }
        
        // Freeze players and ball during countdown
        if (!gameInProgress.Value)
        {
            FreezeBallAndPlayers();
        }

        // Handle Reconnection Timer
        if (IsServer && waitingForReconnection.Value)
        {
        reconnectionTimer -= Time.deltaTime;
    
        // Update clients with the timer
        UpdateReconnectionTimerClientRpc(Mathf.CeilToInt(reconnectionTimer));
        
        if (reconnectionTimer <= 0)
        {
            // Player didn't reconnect in time
            waitingForReconnection.Value = false;
            
            // End game or handle as appropriate
            if (disconnectedPlayerStates.Count > 0)
            {
                // For simplicity, award win to the remaining player
                ulong remainingClientId = playerObjects.Find(p => p != null && 
                                                        !disconnectedPlayerStates.ContainsKey(
                                                            playerIds[p.GetComponent<NetworkObject>().OwnerClientId]))
                                        ?.GetComponent<NetworkObject>().OwnerClientId ?? 0;
                
                if (remainingClientId != 0)
                {
                    GameOver(remainingClientId);
                }
                
                // Clear disconnected states
                disconnectedPlayerStates.Clear();
            }
        }
    }
    }


    // Start initial countdown
    private void StartInitialCountdown()
    {
        if (!IsServer) return;
        
        // Reset game state
        gameInProgress.Value = false;
        gameOver.Value = false;
        countdownTimer.Value = INITIAL_COUNTDOWN;
        winnerScreenShown = false;
        
        // Reset player positions
        ResetPlayersPositionServerRpc();
        
        // Reset ball position
        ResetBallPositionServerRpc();
        
        Debug.Log("Starting initial countdown: " + INITIAL_COUNTDOWN + " seconds");
    }

    // Freeze ball and players during countdown
    private void FreezeBallAndPlayers()
    {
        // Freeze ball
        if (theBall != null && theBall.TryGetComponent<Rigidbody>(out Rigidbody ballRb))
        {
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
            ballRb.isKinematic = true;
        }
        
        // Freeze players - now using playerNetwork instead of playerController
        foreach (GameObject player in playerObjects)
        {
            if (player != null && player.TryGetComponent<playerNetwork>(out playerNetwork controller))
            {
                controller.SetMovementEnabled(gameInProgress.Value);
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

    // public List<string> GetPlayerNames()
    // {
    //     List<string> names = new List<string>();
    //     Lobby currentLobby = hostLobby != null ? hostLobby : joinedLobby;
        
    //     if (currentLobby != null)
    //     {
    //         foreach (Player player in currentLobby.Players)
    //         {
    //             names.Add(player.Data["PlayerName"].Value);
    //         }
    //     }
        
    //     return names;
    // }

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

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer) return; // only server handles this!

        // // Find player's auth ID
        // string authId = null;
        // foreach (var pair in playerIds)
        // {
        //     if (pair.Key == clientId)
        //     {
        //         authId = pair.Value;
        //         break;
        //     }
        // }

        // // If not player auth ID
        // if (authId == null)
        // {
        //     Debug.LogError($"Could not find authId for disconnected client {clientId}");
        //     return;
        // }

        // Ignore host (server) disconnects
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Host (server) is disconnecting. Skipping disconnect handling.");
            return;
        }

        // Find player's auth ID
        if (!playerIds.TryGetValue(clientId, out string authId))
        {
            Debug.LogError($"Could not find authId for disconnected client {clientId}");
            return;
        }


        // Save state for disconnected player
        GamePlayerState playerState = new GamePlayerState
        {
            authId = authId,
            clientId = clientId,
            disconnectTime = Time.time,
            score = clientId == 1 ? playerScore1.Value : playerScore2.Value,
            position = FindPlayerByClientId(clientId)?.transform.position ?? Vector3.zero
        };
        
        disconnectedPlayerStates[authId] = playerState;
        
        
        // Start reconnection timer if not already waiting
        if (!waitingForReconnection.Value)
        {
            waitingForReconnection.Value = true;
            reconnectionTimer = reconnectionWaitTime;
            
            // Pause the game
            if (gameInProgress.Value)
            {
                gameInProgress.Value = false;
                // Show waiting for player message
                ShowWaitingForPlayerClientRpc();
            }
        }
        
        Debug.Log($"Player {clientId} disconnected. Stored state and waiting for reconnection.");   
    }

    private void OnClientConnect(ulong clientId)
    {
        if (!IsServer) return;
        
        // For new connections, we need a way to identify returning players
        // We'll need to extend the connection process to include authentication ID

        // We'll handle this in a new method that should be called when a player connects and authenticates
    }

    // This should be called after authentication when a player connects to the game
    public void RegisterPlayer(ulong clientId, string authId)
    {
        if (!IsServer) return;
        
        playerIds[clientId] = authId;
        
        // Check if this is a reconnecting player
        if (disconnectedPlayerStates.TryGetValue(authId, out GamePlayerState state))
        {
            // Handle reconnection
            HandlePlayerReconnection(clientId, authId, state);
        }
    }

    private void HandlePlayerReconnection(ulong clientId, string authId, GamePlayerState state)
    {
        if (!IsServer) return;
        
        Debug.Log($"Player {authId} reconnected with client ID {clientId}");
        
        // Remove from disconnected list
        disconnectedPlayerStates.Remove(authId);
        
        // If no more disconnected players, continue game
        if (disconnectedPlayerStates.Count == 0 && waitingForReconnection.Value)
        {
            waitingForReconnection.Value = false;
            
            // Resume game after countdown
            StartReconnectionCountdown();
        }
    }

    private void StartReconnectionCountdown()
    {
        if (!IsServer) return;
        
        // Reset countdown timer
        countdownTimer.Value = 5f;
        
        // Hide waiting message
        HideWaitingForPlayerClientRpc();
        
        // Show countdown instead
        // The existing countdown logic in Update will handle the rest
    }

    // Find player's object via a client ID
    private GameObject FindPlayerByClientId(ulong clientId)
    {
        foreach (GameObject player in playerObjects)
        {
            if (player != null && player.GetComponent<NetworkObject>().OwnerClientId == clientId)
            {
                return player;
            }
        }
        return null;
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

        if (isPlayer1Scored) // if player 1 scored
        {
            playerScore1.Value += 1; // update player 1 score
            
            // Check for win condition
            if (playerScore1.Value >= WIN_SCORE)
            {
                GameOver(1);
                return;
            }
        }
        else // else player 2 scored
        {
            playerScore2.Value += 1; // update player 2 score
            
            // Check for win condition
            if (playerScore2.Value >= WIN_SCORE)
            {
                GameOver(2);
                return;
            }
        }

        // Reset and start countdown for next point
        StartResetCountdown();
    }

    // Start reset countdown between points
    private void StartResetCountdown()
    {
        if (!IsServer) return;
        
        // Pause game and start countdown
        gameInProgress.Value = false;
        countdownTimer.Value = RESET_COUNTDOWN;
        
        // Reset positions
        ResetPlayersPositionServerRpc();
        ResetBallPositionServerRpc();
        
        Debug.Log("Starting reset countdown: " + RESET_COUNTDOWN + " seconds");
    }
    
    // Handle game over
    private void GameOver(ulong winningPlayer)
    {
        if (!IsServer) return;
        
        gameOver.Value = true;
        gameInProgress.Value = false;
        winnerClientId.Value = winningPlayer;
        
        Debug.Log("Game Over! Player " + winningPlayer + " wins!");
    }

        // Start a new game after someone has won
    public void StartNewGame()
    {
        if (!IsServer) return;
        
        // Reset scores
        playerScore1.Value = 0;
        playerScore2.Value = 0;
        
        // Reset game state
        gameOver.Value = false;
        
        // Start initial countdown
        StartInitialCountdown();
    }

    private void OnGameInProgressChanged(bool previous, bool current)
    {
        // When game starts, enable movement for all players
        if (current == true)
        {
            foreach (GameObject player in playerObjects)
            {
                if (player != null && player.TryGetComponent<playerNetwork>(out playerNetwork controller))
                {
                    controller.SetMovementEnabled(true);
                }
            }
        }
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

    [ClientRpc]
    private void ShowWaitingForPlayerClientRpc()
    {
        // Call your UI to display a waiting message
        gameUIManagerInstance.ShowWaitingForReconnection(true);
    }

    [ClientRpc]
    private void UpdateReconnectionTimerClientRpc(int seconds)
    {
        // Update UI with reconnection timer
        gameUIManagerInstance.UpdateReconnectionTimerText(seconds.ToString());
    }
    
    [ClientRpc]
    private void HideWaitingForPlayerClientRpc()
    {
        gameUIManagerInstance.ShowWaitingForReconnection(false);
    }

    // Game Handling Calls
    // Start the game when countdown is complete
    [ServerRpc]
    private void StartGameServerRpc()
    {
        if (!IsServer) return;
        
        gameInProgress.Value = true;
        
        // Launch the ball in a random direction
        if (theBall != null && theBall.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.AddForce(new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * 10f, ForceMode.Impulse);
        }

        // Enable player controllers - now using playerNetwork
        foreach (GameObject player in playerObjects)
        {
            if (player != null && player.TryGetComponent<playerNetwork>(out playerNetwork controller))
            {
                controller.SetMovementEnabled(true);
            }
        }
        // Notify clients that the game has started
        StartGameClientRpc();
    }

    
    [ClientRpc]
    private void StartGameClientRpc()
    {
        Debug.Log("Game started!");
        
        // Enable player controllers - now using playerNetwork
        foreach (GameObject player in playerObjects)
        {
            if (player != null && player.TryGetComponent<playerNetwork>(out playerNetwork controller))
            {
                controller.SetMovementEnabled(true);
            }
        }
    }


    // Reset Ball Position
    [ServerRpc]
    private void ResetBallPositionServerRpc()
    {
        if (!IsServer) return;
        
        if (theBall != null)
        {
            theBall.transform.position = new Vector3(0, 1, 0);
            if (theBall.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        
        // Notify clients
        ResetBallPositionClientRpc();
    }
    
    [ClientRpc]
    private void ResetBallPositionClientRpc()
    {
        // Any client-side logic needed when ball is reset
        Debug.Log("Ball position reset");
    }

        // Reset Player Positions
    [ServerRpc]
    private void ResetPlayersPositionServerRpc()
    {
        if (!IsServer) return;

        int playerCount = playerObjects.Count;
        Debug.Log($"Resetting {playerCount} players");

        for (int i = 0; i < playerCount && i < playerSpawnPos.Count; i++)
        {
            if (playerObjects[i] != null)
            {
                // Reset position and velocity
                playerObjects[i].transform.position = playerSpawnPos[i];
                if (playerObjects[i].TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                Debug.Log($"Reset player {i} to position {playerSpawnPos[i]}");
            }
        }
        
        // Notify clients
        ResetPlayersPositionClientRpc();
    }
    
    [ClientRpc]
    private void ResetPlayersPositionClientRpc()
    {
        // Any client-side logic needed when players are reset
        Debug.Log("Player positions reset");
    }


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
