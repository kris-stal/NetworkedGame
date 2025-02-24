using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

// Main Game Manager script for handling game logic
public class gameManager : NetworkBehaviour
{

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

    GameObject theBall; // Get ball Game Object
    private List<GameObject> playerObjects = new List<GameObject>(); // List of player Game Objects
    private List<Vector3> playerSpawnPos = new List<Vector3>(); // List of spawn positions for players


    // Singleton Pattern for easy access to the gameManager from other scripts and to prevent duplicates
    public static gameManager Instance { get; private set; }

    // Awake is called when the script instance is loaded
    private void Awake()
    {
        // Check if an instance already exists
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
        // find the ball Game Object
        theBall = GameObject.FindGameObjectWithTag("Ball");

        playerSpawnPos.Add(new Vector3(-5, 1, 0)); // spawn pos 1
        playerSpawnPos.Add(new Vector3(5, 1, 0)); // spawn pos 2
        Debug.Log(playerSpawnPos.Count); // Output num of spawn pos for testing
        
        // find all players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Debug.Log($"Found {players.Length} players");

        // add all players to list of Player Objects
        foreach (GameObject player in players)
        {
            playerObjects.Add(player);
            Debug.Log($"Added player with NetworkObjectId: {player.GetComponent<NetworkObject>().NetworkObjectId}");
        }
    }

    public void StartAsHost()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void StartAsClient()
    {
        NetworkManager.Singleton.StartClient();
    }

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
                rb.AddForce(new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * 10f, 
                    ForceMode.Impulse);
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
