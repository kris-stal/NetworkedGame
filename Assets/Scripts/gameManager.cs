using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;

// Manager for game functionality
// Handles game start, countdown timers, player pings, scoring
public class GameManager : NetworkBehaviour
{
    // REFERENCES
    // Singleton Pattern Reference
    public static GameManager Instance { get; private set; }
        
    // Reference other scripts 
    private CoreManager coreManagerInstance;
    private GameUIManager gameUIManagerInstance;
    private LobbyManager lobbyManagerInstance;



    // VARIABLES //
    // Network variables 
    // Player scores
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

    // Game state variables
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

    
    // Game and player state variables
    [System.Serializable] // Class to store state of game
    public class GameState
    {
        // Game State
        public float gameTime;
        public bool gameInProgress;
        public bool gameOver;
        public ulong winnerClientId;
        public float countdownValue;

        // Player States
        public List<PlayerState> players = new List<PlayerState>();

        // Ball State
        public Vector3 ballPosition;
        public Vector3 ballVelocity;
        public Vector3 ballAngularVelocity;

        // Timestamp
        public System.DateTime timestamp;
    }

    // Player States
    [System.Serializable]
    public class PlayerState
    {
        public string authId;
        public ulong clientId;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public int score;
        public bool isConnected;
        public float disconnectTime;
    }

    // Other network variables

    private Dictionary<ulong, string> playerIds = new Dictionary<ulong, string>(); // Maps NetworkObject IDs to Auth IDs
    

    // Game Variables
    GameObject theBall; // Get ball Game Object
    private List<GameObject> playerObjects = new List<GameObject>(); // List of player Game Objects
    private List<Vector3> playerSpawnPos = new List<Vector3>(); // List of spawn positions for players
    private bool winnerScreenShown; // is winner screen shown

    // Constants
    private const int WIN_SCORE = 10;
    private const float INITIAL_COUNTDOWN = 5f;
    private const float RESET_COUNTDOWN = 3f;
    private const string TEMP_SAVE_PATH = "gameState.json"; // Added constant for local save path



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
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created, after Awake
    void Start()
    {
        // Make sure NetworkManager is ready
        if (NetworkManager.Singleton != null)
        {
            // Subscribe to client disconnect/connect events
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnect;
        }
        else
        {
            Debug.LogError("NetworkManager is not available.");
            return;
        }

        // Assign manager instances
        coreManagerInstance = CoreManager.Instance;
        lobbyManagerInstance = coreManagerInstance.lobbyManagerInstance;
        gameUIManagerInstance = coreManagerInstance.gameUIManagerInstance;

        // Set Spawn Positions
        playerSpawnPos.Add(new Vector3(-5, 1, 0)); // spawn pos 1
        playerSpawnPos.Add(new Vector3(5, 1, 0)); // spawn pos 2
        Debug.Log(playerSpawnPos.Count); // Output num of spawn pos for testing

        onLoadArena();

        // Call RPC only if the object is spawned and we are the server
        if (IsServer && IsSpawned)
        {
            StartInitialCountdown();
        }
    }

    // Called every frame
    private void Update()
    {
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
    }

    // GAME FUNCTIONALITY //
    // For above use in starting the game.
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

        // Enable player controllers - now using PlayerNetwork
        foreach (GameObject player in playerObjects)
        {
            if (player != null && player.TryGetComponent<PlayerNetwork>(out PlayerNetwork controller))
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

        // Hide all ready buttons
        gameUIManagerInstance?.HideAllReadyButtons();

        // Enable player controllers - now using PlayerNetwork
        foreach (GameObject player in playerObjects)
        {
            if (player != null && player.TryGetComponent<PlayerNetwork>(out PlayerNetwork controller))
            {
                controller.SetMovementEnabled(true);
            }
        }
    }

    // Start game upon loading the arena scene
    // Find ball, players and start initial countdown
    public void onLoadArena()
    {
        // Find the ball Game Object
        theBall = GameObject.FindGameObjectWithTag("Ball");

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

        winnerScreenShown = false;
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


        FreezeBallAndPlayers();

        // Reset player positions
        ResetPlayersServerRpc();
        
        // Reset ball position
        ResetBallServerRpc();
        
        Debug.Log("Starting initial countdown: " + INITIAL_COUNTDOWN + " seconds");
    }

    // Reset Players RPC (Position + Velocity)
    [ServerRpc]
    private void ResetPlayersServerRpc()
    {
        if (!IsServer) return;

        int playerCount = playerObjects.Count;
        Debug.Log($"Resetting {playerCount} players");

        // Reset positions and velocities of all players
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
        ResetPlayersClientRpc();
    }

    // Notify Players
    [ClientRpc]
    private void ResetPlayersClientRpc()
    {
        // Any client-side logic needed when players are reset
        Debug.Log("Player positions reset on client");
    }

    // Freeze ball and players during countdown
    private void FreezeBallAndPlayers()
    {
        // Freeze ball
        if (theBall != null && theBall.TryGetComponent<Rigidbody>(out Rigidbody ballRb))
        {
            ballRb.isKinematic = true;
        }
        
        // Freeze players - now using PlayerNetwork instead of playerController
        foreach (GameObject player in playerObjects)
        {
            if (player != null && player.TryGetComponent<PlayerNetwork>(out PlayerNetwork controller))
            {
                controller.SetMovementEnabled(gameInProgress.Value);
            }
        }
    }

    // Reset Ball RPC
    [ServerRpc]
    private void ResetBallServerRpc()
    {
        if (theBall != null)
        {
            // Reset position
            theBall.transform.position = new Vector3(0, 1, 0);
            
            // Reset velocity
            if (theBall.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                
                // Apply random force
                rb.AddForce(new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * 10f, ForceMode.Impulse);
            }
            
            // Notify clients
            ResetBallClientRpc();
        }
    }

    // Notify players
    [ClientRpc]
    private void ResetBallClientRpc()
    {
        // Any client-side logic needed when ball is reset
        Debug.Log("Ball position reset on client");
    }

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
                        RequestDespawnServerRpc(playerObject); // Despawn on network
                    }

                    // Disconnect
                    NetworkManager.Singleton.Shutdown();

                    // Clear the player list
                    GameUIManager.Instance?.ClearPlayerList();

                    // Load lobby scene again
                    SceneManager.LoadScene("LobbyMenu");
                }
            }
        }
    }


    // Despawn player object
    [ServerRpc(RequireOwnership = false)]
    public void RequestDespawnServerRpc(NetworkObjectReference objectRef)
    {
        if (objectRef.TryGet(out NetworkObject networkObject))
        {
            networkObject.Despawn();
        }
    }

    // Start reset countdown between points
    private void StartResetCountdown()
    {
        if (!IsServer) return;
        
        // Pause game and start countdown
        gameInProgress.Value = false;
        countdownTimer.Value = RESET_COUNTDOWN;
        
        // Reset positions
        ResetPlayersServerRpc();
        ResetBallServerRpc();
        
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
                if (player != null && player.TryGetComponent<PlayerNetwork>(out PlayerNetwork controller))
                {
                    controller.SetMovementEnabled(true);
                }
            }
        }
    }



    // SCORE HANDLING //
    // Getting player scores
    public int GetPlayer1Score()
    {
        return playerScore1.Value;
    }
    public int GetPlayer2Score()
    {
        return playerScore2.Value;
    }

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
    


    // PING FUNCTIONALITY //
    // Helper method to get the client ID of a player GameObject
    public ulong GetPlayerClientId(GameObject playerObject)
    {
        if (playerObject != null && playerObject.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            return netObj.OwnerClientId;
        }
        return 0;
    }



    // GAME STATE //
    private void SaveGameStateLocally(GameState state)
    {
        string json = JsonUtility.ToJson(state);
        File.WriteAllText(TEMP_SAVE_PATH, json);
        Debug.Log("Game state saved locally");
    }

    private GameState LoadGameStateLocally()
    {
        if(File.Exists(TEMP_SAVE_PATH))
        {
            string json = File.ReadAllText(TEMP_SAVE_PATH);
            return JsonUtility.FromJson<GameState>(json); // Load from file
        }
        return null;
    }


    private GameState CaptureGameState()
    {
        // Capture game state
        GameState state = new GameState
        {
            gameTime = Time.time,
            gameInProgress = gameInProgress.Value,
            gameOver = gameOver.Value,
            winnerClientId = winnerClientId.Value,
            countdownValue = countdownTimer.Value,
            timestamp = System.DateTime.Now
        };

        // Capture player states
        foreach (GameObject playerObj in playerObjects)
        {
            if (playerObj != null)
            {
                ulong clientId = playerObj.GetComponent<NetworkObject>().OwnerClientId;
                
                PlayerState playerState = new PlayerState
                {
                    clientId = clientId,
                    authId = playerIds.TryGetValue(clientId, out string authId) ? authId : "unknown",
                    position = playerObj.transform.position,
                    isConnected = true,
                    score = clientId == 1 ? playerScore1.Value : playerScore2.Value
                };
                
                // Get velocity if available
                if (playerObj.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    playerState.velocity = rb.linearVelocity;
                    playerState.angularVelocity = rb.angularVelocity;
                }
                
                state.players.Add(playerState);
            }
        }

        // Capture ball state
        if (theBall != null)
        {
            state.ballPosition = theBall.transform.position;
            
            if (theBall.TryGetComponent<Rigidbody>(out Rigidbody ballRb))
            {
                state.ballVelocity = ballRb.linearVelocity;
                state.ballAngularVelocity = ballRb.angularVelocity;
            }
        }
        
        return state; // Return the captured state
    }



    // EVENT HANDLING //
    private async void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer) return; // Only server handles this

        // Ignore host (server) disconnects
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Host (server) is disconnecting, ending game");
            await lobbyManagerInstance.LeaveLobby(); // delete lobby
            NetworkManager.Singleton.Shutdown(); // end network connection
            return;
        }

        // Find player's auth ID
        if (!playerIds.TryGetValue(clientId, out string authId))
        {
            Debug.LogError($"Could not find authId for disconnected client {clientId}");
            return;
        }

        Debug.Log($"Player {clientId} with authId {authId} disconnected");

        // Capture the full game state
        GameState gameState = CaptureGameState();
        SaveGameStateLocally(gameState);
    }

    private void OnClientConnect(ulong clientId)
    {
        if (!IsServer) return;
        
        Debug.Log($"Client {clientId} connected to the game");
        
        // We won't handle authentication directly in this method
        // Instead, we'll expect the client to send their authId via a ServerRPC
        // This allows for proper authentication flow from the client
        
        // Notify the client that they need to authenticate
    }
    
}
