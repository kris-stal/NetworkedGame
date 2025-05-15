using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using Unity.Services.Authentication;

public class ReconnectManager : NetworkBehaviour
{
    // Singleton instance ensuring persistence across scenes
    public static ReconnectManager Instance { get; private set; }

    // Dictionary recording disconnected players (keyed by authId) with their disconnect time.
    private Dictionary<string, DateTime> disconnectedPlayers = new Dictionary<string, DateTime>();

    // Constant used for saving the game state locally
    private const string TEMP_SAVE_PATH = "gameState.json";

    // -----------------------------
    //   State container definitions
    // -----------------------------
    
    [Serializable]
    public class GameState
    {
        public float gameTime;
        public bool gameInProgress;
        public bool gameOver;
        public ulong winnerClientId;
        public float countdownValue;
        public List<PlayerState> players = new List<PlayerState>();
        public Vector3 ballPosition;
        public Vector3 ballVelocity;
        public Vector3 ballAngularVelocity;
        public DateTime timestamp;
    }

    [Serializable]
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

    // -----------------------------
    //   Unity lifecycle methods
    // -----------------------------

    private void Awake()
    {
        // Singleton pattern: if another instance exists, destroy this one.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persist across scene loads
    }

    private void Start()
    {
        // Start periodic cleanup of stale disconnect records.
        StartCoroutine(CleanupDisconnectedPlayersCoroutine());
    }

    // -----------------------------
    //       Reconnection API
    // -----------------------------

    /// <summary>
    /// Call this when a disconnect is detected to record the disconnect time.
    /// </summary>
    public void RecordDisconnect(string authId)
    {
        disconnectedPlayers[authId] = DateTime.Now;
        Debug.Log($"Recorded disconnect for player {authId} at {DateTime.Now}");
    }

    /// <summary>
    /// ServerRPC called by a client attempting to reconnect.
    /// This method checks whether the player’s disconnect record is still valid,
    /// loads the saved game state (from disk or memory), and then triggers the ClientRPC to restore state.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ReconnectToGameServerRpc(string authId, ServerRpcParams rpcParams = default)
    {
        // Check if a disconnect record exists for this authId.
        if (!disconnectedPlayers.ContainsKey(authId))
        {
            Debug.Log($"Reconnect attempt: No record found for player {authId}.");
            return;
        }

        DateTime disconnectTime = disconnectedPlayers[authId];
        // If the allowed grace period has expired, reject the reconnect.
        if ((DateTime.Now - disconnectTime).TotalSeconds > 60)
        {
            Debug.Log($"Reconnect attempt: The grace period for {authId} has expired.");
            disconnectedPlayers.Remove(authId);
            return;
        }

        // Load the saved game state from disk.
        GameState savedState = LoadGameStateLocally();
        if (savedState == null)
        {
            Debug.Log("Reconnect attempt: No saved game state available.");
            return;
        }

        // Attempt to find the saved state for the reconnecting player.
        PlayerState restoredState = null;
        foreach (var playerState in savedState.players)
        {
            if (playerState.authId == authId)
            {
                restoredState = playerState;
                break;
            }
        }
        if (restoredState == null)
        {
            Debug.Log($"Reconnect attempt: No saved state found for player {authId}.");
            return;
        }

        Debug.Log($"Restoring game state for player {authId}.");

        // Trigger ClientRPC to restore the player's state.
        RestorePlayerStateClientRpc(authId, restoredState.position, restoredState.velocity, restoredState.angularVelocity, restoredState.score);

        // Remove the disconnect record on successful reconnection.
        disconnectedPlayers.Remove(authId);

        // If you maintain global disconnect flags (for instance in your lobby manager), reset them here.
        // Example: LobbyManager.Instance.WasDisconnected = false;
    }

    /// <summary>
    /// ClientRPC to restore a reconnecting client’s state.
    /// The reconnecting client (matching authId) will update its player object accordingly.
    /// </summary>
    [ClientRpc]
    private void RestorePlayerStateClientRpc(string authId, Vector3 pos, Vector3 vel, Vector3 angVel, int score)
    {
        // Only the reconnecting client should process this.
        if (AuthenticationService.Instance.PlayerId != authId)
            return;

        Debug.Log($"[ClientRpc] Restoring state for player {authId}.");

        // Retrieve the local player's NetworkObject.
        GameObject localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
        if (localPlayerObject != null)
        {
            localPlayerObject.transform.position = pos;
            if (localPlayerObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.linearVelocity = vel;
                rb.angularVelocity = angVel;
            }
        }

        // Optionally, update your UI with the restored score or other state details.
        // For example: GameUIManager.Instance.UpdateScore(score);
    }

    // -----------------------------
    //      Game State Helpers
    // -----------------------------

    /// <summary>
    /// Saves the provided game state to local storage.
    /// </summary>
    private void SaveGameStateLocally(GameState state)
    {
        try
        {
            string json = JsonUtility.ToJson(state);
            File.WriteAllText(TEMP_SAVE_PATH, json);
            Debug.Log("Game state saved locally.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save game state: " + e.Message);
        }
    }

    /// <summary>
    /// Loads the game state from local storage.
    /// </summary>
    private GameState LoadGameStateLocally()
    {
        try
        {
            if (File.Exists(TEMP_SAVE_PATH))
            {
                string json = File.ReadAllText(TEMP_SAVE_PATH);
                GameState state = JsonUtility.FromJson<GameState>(json);
                return state;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load game state: " + e.Message);
        }
        return null;
    }

    /// <summary>
    /// Captures the current game state.
    /// You can call this method (from your GameManager) before a disconnect occurs
    /// so that the state can be restored on reconnection.
    /// </summary>
    public GameState CaptureGameState()
    {
        GameState state = new GameState();
        state.gameTime = Time.time;
        // Set additional flags or values here as needed.
        // For example, capture ball position, player states, etc.
        Debug.Log("Capturing game state for reconnection.");
        // You might want to call further helper methods or retrieve data from your GameManager.
        return state;
    }

    // -----------------------------
    //         Cleanup
    // -----------------------------

    /// <summary>
    /// Periodically checks the disconnectedPlayers dictionary and removes entries
    /// that have exceeded the allowed reconnection grace period.
    /// </summary>
    public IEnumerator CleanupDisconnectedPlayersCoroutine()
    {
        while (true)
        {
            List<string> keys = new List<string>(disconnectedPlayers.Keys);
            foreach (string key in keys)
            {
                if ((DateTime.Now - disconnectedPlayers[key]).TotalSeconds > 60)
                {
                    disconnectedPlayers.Remove(key);
                    Debug.Log($"Removed record for player {key} due to timeout.");
                }
            }
            yield return new WaitForSeconds(5f);
        }
    }
}
