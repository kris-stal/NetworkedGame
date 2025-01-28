using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

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

    GameObject theBall;


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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        theBall = GameObject.FindGameObjectWithTag("Ball");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

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
    }


    public int GetPlayer1Score()
    {
        return playerScore1.Value;
    }

    public int GetPlayer2Score()
    {
        return playerScore2.Value;
    }

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
}
