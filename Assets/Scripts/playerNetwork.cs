using System.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

// Manager for individual player ball objects
// Handles movement, collision
public class PlayerNetwork : NetworkBehaviour
{

    private CoreManager coreManagerInstance;
    private GameManager gameManagerInstance;
    // VARIABLES //
    [SerializeField] private float acceleration = 4f;
    [SerializeField] private float deceleration = 2f;
    [SerializeField] private float maxSpeed = 10f;

        [SerializeField] private Renderer playerRenderer; // Assign in inspector or get in Awake/Start
            [SerializeField] private Material playerBlue;
    [SerializeField] private Material playerRed;


    public Rigidbody rb; // player rigidbody
    public Vector3 InputKey; // input keys
    private bool movementEnabled = false; // control if movement is enabled
    private Vector3 lastReceivedPosition;
    private Vector3 targetPosition;
    private float interpolationTime = 0f;
    private bool isCorrectingPosition = false;
    private Vector3 lastSentPosition;
    private Vector3 lastSentVelocity;

    // Constants
    private const float POSITION_THRESHOLD = 0.1f;
    private const float VELOCITY_THRESHOLD = 0.1f;

    // Reconciliation variables
    private float reconciliationTimer = 0f;
    private float RECONCILIATION_INTERVAL = 0.15f; 
    private float positionErrorThreshold = 5f;
    

    // Test for syncing variable value, randomly generated integer:
    private NetworkVariable<int> randomNumber = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    // New NetworkVariable int, (starting value, read permissions, write permissions)
    // Everyone is able to read the value, only owners of this specific network variable (every player has one) can alter the value of their owned variable

    // New custom data struct for testing
    private NetworkVariable<MyCustomData> customIntBool = new NetworkVariable<MyCustomData>(
        new MyCustomData {
            _int = 56,
            _bool = true,
            message = "parkinglotscarecrow",
        }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


    // Data serialization (which is handled for set data types like the int in random num) is the process of converting
    // data objects in a data structure into a byte stream for storage, transfer and distribution. This is used in sending data
    // over objects.
    // For MyCustomData, the data within must be serialized:
    public struct MyCustomData : INetworkSerializable {
        public int _int;
        public bool _bool;
        public string message;

        // Importing base serializer:
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Altering the serialize method for MyCustomData:
            serializer.SerializeValue(ref _int);
            serializer.SerializeValue(ref _bool);
            serializer.SerializeValue(ref message);
        }
    }

    // Altering OnNetworkSpawn method to see values of variables and data structs:
    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

            coreManagerInstance = CoreManager.Instance;
    gameManagerInstance = coreManagerInstance.gameManagerInstance;

        if (IsOwner && gameManagerInstance != null && !gameManagerInstance.playerObjects.Contains(gameObject))
        {
            gameManagerInstance.playerObjects.Add(gameObject);

            // If the game is already in progress, enable movement
            if (gameManagerInstance.IsSpawned && gameManagerInstance.IsServer && gameManagerInstance.gameInProgress.Value)
            {
                SetMovementEnabled(true);
            }
        
            if (IsOwner && gameManagerInstance != null)
    {
        gameManagerInstance.RegisterPlayer(gameObject);
    }
    }

        // When randomNumber value changes,
        randomNumber.OnValueChanged += (int previousValue, int newValue) => {
        // Output OnwerClientID + that owner's value of the randomNumber
        Debug.Log(OwnerClientId + "; randomNumber: " + randomNumber.Value);
        };
        // This is better than using Debug.Log in the update method as we only need to if the value of randomNumber changes correctly between players when it actually does change value
        // so this way there are less repeated messages.

        // Custom Data struct version:
        customIntBool.OnValueChanged += (MyCustomData previousValue, MyCustomData newValue) => {
        // Output OnwerClientID + that owner's values of the custom data struct
        Debug.Log(OwnerClientId + "; randomNumber: " + newValue._int + "; " + newValue._bool + "; " + newValue.message);
        };
    }



    // Awake is called when script is first made
    private void Awake()
    {
        if (!IsOwner) return; // if not owner of player object return
        rb = GetComponent<Rigidbody>(); // get rigidbody of this player object
        
// In Start() or Awake(), ensure this gets the correct renderer:
if (playerRenderer == null)
    playerRenderer = GetComponent<MeshRenderer>(); // Try explicit MeshRenderer
    }


    


    private void Start()
    {
        // Assign script instances
        coreManagerInstance = CoreManager.Instance;
        gameManagerInstance = coreManagerInstance.gameManagerInstance;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
            gameManagerInstance = coreManagerInstance.gameManagerInstance;
        if (IsServer && SceneManager.GetActiveScene().name == "BallArena")
        {
            StartCoroutine(WaitAndAssignColor());
        }
    }


    private IEnumerator WaitAndAssignColor()
    {
        // Wait until GameManager exists and playerObjects is populated with 2 players
        while (gameManagerInstance == null)
        {
            yield return null;
        }

        AssignPlayerColorServerRpc();
    }

[ServerRpc(RequireOwnership = false)]
private void AssignPlayerColorServerRpc()
{
    // Get all connected client IDs in sorted order
    var clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
    clientIds.Sort(); // Host will always be first

    int colorIndex = clientIds.IndexOf(OwnerClientId); // 0 = blue, 1 = red

    Debug.Log($"Assigning color: OwnerClientId={OwnerClientId}, colorIndex={colorIndex}");
    AssignPlayerColorClientRpc(colorIndex);
}


    [ClientRpc]
    private void AssignPlayerColorClientRpc(int colorIndex)
    {
        // Get renderer directly from the player object, not children
        if (playerRenderer == null)
            playerRenderer = GetComponent<Renderer>();

        Debug.Log($"AssignPlayerColorClientRpc called with colorIndex={colorIndex}, playerRenderer={playerRenderer != null}, materials: blue={playerBlue != null}, red={playerRed != null}");

        if (colorIndex == 0 && playerBlue != null)
        {
            // Create a new material instance instead of direct assignment
            playerRenderer.material = new Material(playerBlue);
            Debug.Log($"Set player {OwnerClientId} to BLUE");
        }
        else if (colorIndex == 1 && playerRed != null)
        {
            playerRenderer.material = new Material(playerRed);
            Debug.Log($"Set player {OwnerClientId} to RED");
        }
        else
        {
            // Fallback color assignment using direct color property
            if (colorIndex == 0)
                playerRenderer.material.color = Color.blue;
            else
                playerRenderer.material.color = Color.red;
                
            Debug.Log($"Used fallback color for player {OwnerClientId} with colorIndex {colorIndex}");
        }
    }









    
    // Update is called once per frame
    private void Update()
    {
        // if NOT the local owner of this player object, return
        if (!IsOwner) return;

        // Only process input if movement is enabled
        if (movementEnabled)
        {
            InputKey = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        }
        else
        {
            // Clear input when movement is disabled
            InputKey = Vector3.zero;
        }

        // Press 'I' to generate new random number so can test sync between host and client:
        if (Input.GetKeyDown(KeyCode.I))
        {
            randomNumber.Value = UnityEngine.Random.Range(0, 100);
        }

        // Press 'C' to set value of MyCustomData to specified variable values:
        if (Input.GetKeyDown(KeyCode.C))
        {
            customIntBool.Value = new MyCustomData
            {
                _int = 10,
                _bool = false,
                message = "MESSAGE REDACTED",
            };
        }

        // Press R 
        if (Input.GetKeyDown(KeyCode.R))
        {
            TestServerRpc();
        }

        // Only for the owner clients
        if (IsOwner)
        {
            reconciliationTimer += Time.deltaTime;
            if (reconciliationTimer >= RECONCILIATION_INTERVAL)
            {
                reconciliationTimer = 0f;
                RequestServerPositionServerRpc();
            }
        }

        if (!IsOwner)
        {
            interpolationTime += Time.deltaTime * 5f;
            transform.position = Vector3.Lerp(lastReceivedPosition, targetPosition, interpolationTime);
        }
    }

    private void FixedUpdate() 
    {
        if (!IsOwner) return;

        if (!movementEnabled)
        {
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                rb.linearVelocity = Vector3.zero;
                RequestStopMovementServerRpc();
            }
            return;
        }

        if (InputKey != Vector3.zero)
        {
            // Local prediction
            Vector3 force = InputKey * acceleration;
            rb.AddForce(force, ForceMode.Force);

            if (rb.linearVelocity.magnitude > maxSpeed) 
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }

            // Always send to server, every tick
            RequestAccelerateServerRpc(InputKey);
        }
        else 
        {
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                rb.AddForce(rb.linearVelocity * -deceleration, ForceMode.Force);
            }
            RequestDecelerateServerRpc();
        }
    }




    // COLLISION 
    private void OnCollisionEnter(Collision collision)
    {
        if (!movementEnabled) return; // skip if movement disabled


        // Allow client-side collision response for immediate feedback
        if (!IsServer && IsOwner)
        {
            HandleCollisionLocally(collision);
        }
        
        if (!IsServer) return;
        
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Ball"))
        {
            // Get the other player's NetworkBehaviour component
            PlayerNetwork otherPlayer = collision.gameObject.GetComponent<PlayerNetwork>();
            
            // Only apply force if we're colliding with another player
            if (otherPlayer != null)
            {
                // Calculate relative velocity
                Vector3 relativeVelocity = collision.relativeVelocity;
                float forceMagnitude = relativeVelocity.magnitude;
                Vector3 forceDirection = collision.contacts[0].normal;
                
                // Apply force to both objects in opposite directions
                float forceMultiplier = 0.2f; // Adjust this value to control bounce strength
                Vector3 force = forceDirection * forceMagnitude * forceMultiplier;
                
                // Apply opposite forces to each player
                rb.AddForce(force, ForceMode.Impulse);
                otherPlayer.rb.AddForce(-force, ForceMode.Impulse);
            }
        }
    }

    // Client-side collision prediction
    private void HandleCollisionLocally(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Ball"))
        {
            Vector3 relativeVelocity = collision.relativeVelocity;
            float forceMagnitude = relativeVelocity.magnitude;
            Vector3 forceDirection = collision.contacts[0].normal;
            
            float forceMultiplier = 0.2f;
            Vector3 force = forceDirection * forceMagnitude * forceMultiplier;
            
            // Apply local force for immediate feedback
            rb.AddForce(force, ForceMode.Impulse);
        }
    }

    // Method for gameManager to enable/disable movement
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        
        // If disabling movement, immediately stop velocity
        if (!enabled && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // Tell server to stop movement for this player
            if (IsOwner)
            {
                RequestStopMovementServerRpc();
            }
        }
    }

    // Routine for smooth correction
    private IEnumerator SmoothCorrection(Vector3 targetPos, Vector3 targetVel)
    {
        Vector3 startPos = transform.position;
        Vector3 startVel = rb.linearVelocity;
        float elapsed = 0f;
        float duration = 0.1f; // Correction time in seconds
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Lerp position and velocity
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            rb.linearVelocity = Vector3.Lerp(startVel, targetVel, t);
            

            yield return null;
        }
        
        // Ensure we end exactly at target values
        transform.position = targetPos;
        rb.linearVelocity = targetVel;

            // Set flag when done
        isCorrectingPosition = false;
        yield break;
    }


    // Server Rpc
    // When client calls ServerRpc function, the server runs the function NOT the client
    // Clients can use Server Remote Procedure Calls to ask for movement / actions.

    // Testing ServerRpc
    [ServerRpc]
    private void  TestServerRpc() 
    {
        Debug.Log("TestServerRpc " + OwnerClientId);
    }



    // MOVEMENT //
    // Asking for acceleration in movement
[ServerRpc]
private void RequestAccelerateServerRpc(Vector3 InputKey, ServerRpcParams rpcParams = default)
{
    if (!movementEnabled) return;
    
    ulong senderId = rpcParams.Receive.SenderClientId;
    bool isHost = NetworkManager.Singleton.LocalClientId == NetworkManager.ServerClientId;
    
    // CRITICAL FIX: Different handling for host vs client players
    if (senderId == OwnerClientId && !isHost)
    {
        // For client-owned objects, apply minimal force on server
        // to avoid double-physics problem
        Vector3 force = InputKey * acceleration * 0.2f;
        rb.AddForce(force, ForceMode.Force);
        
        // Let client speed exceed normal max slightly 
        float adjustedMaxSpeed = maxSpeed * 1.5f;
        if (rb.linearVelocity.magnitude > adjustedMaxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * adjustedMaxSpeed;
        }
    }
    else
    {
        // Normal force application for host players
        Vector3 force = InputKey * acceleration;
        rb.AddForce(force, ForceMode.Force);
        
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }
    
    // Send updates to other clients
    List<ulong> targetClients = new List<ulong>();
    foreach (ulong id in NetworkManager.ConnectedClientsIds)
    {
        if (id != OwnerClientId)
        {
            targetClients.Add(id);
        }
    }
    
    var clientRpcParams = new ClientRpcParams
    {
        Send = new ClientRpcSendParams
        {
            TargetClientIds = targetClients.ToArray()
        }
    };
    
    SyncMovementClientRpc(transform.position, rb.linearVelocity, clientRpcParams);
}


    // Asking for deceleration in movement
    [ServerRpc]
    private void RequestDecelerateServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!movementEnabled) return; // skip if movement disabled

        // Only apply deceleration if we're not in a collision
        if (rb.linearVelocity.magnitude > 0.1f)  // Small threshold to prevent jitter
        {
            rb.AddForce(rb.linearVelocity * -deceleration, ForceMode.Force);
        }
        
        // Create a list to store client IDs except the owner
        List<ulong> targetClients = new List<ulong>();
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
        {
            if (id != OwnerClientId)
            {
                targetClients.Add(id);
            }
        }

        // Send to all clients EXCEPT the owner
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = targetClients.ToArray()
            }
        };
        
        SyncMovementClientRpc(transform.position, rb.linearVelocity, clientRpcParams);
    }

    // Completely stop movement
    [ServerRpc]
    private void RequestStopMovementServerRpc(ServerRpcParams rpcParams = default)
    {
        // Force stop all movement
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

            // Sync to all clients
    // Build the target client list as you do elsewhere:
    List<ulong> targetClients = new List<ulong>();
    foreach (ulong id in NetworkManager.ConnectedClientsIds)
    {
        if (id != OwnerClientId)
        {
            targetClients.Add(id);
        }
    }
    var clientRpcParams = new ClientRpcParams
    {
        Send = new ClientRpcSendParams
        {
            TargetClientIds = targetClients.ToArray()
        }
    };
        
        // Sync to all clients
        SyncMovementClientRpc(transform.position, rb.linearVelocity, clientRpcParams);
    }

    // Server RPC to request authoritative position
    [ServerRpc]
    private void RequestServerPositionServerRpc(ServerRpcParams rpcParams = default)
    {
        // Server sends its authoritative position/velocity to the requesting client
        ReconcilePositionClientRpc(transform.position, rb.linearVelocity, 
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            });
    }

    // Client Rpc
    // Used to be run from server to send messages to clients.
    // Clients cannot run this Rpc.

    // Testing ClientRpc
    [ClientRpc]
    private void TestClientRpc()
    {
        Debug.Log("TestClientRpc " + OwnerClientId);
    }



    // MOVEMENT
    [ClientRpc]
    private void SyncMovementClientRpc(Vector3 newPos, Vector3 newVelocity, ClientRpcParams rpcParams = default)
    {
        if (IsOwner) return; // Owner predicts locally

        // For remote clients, interpolate
        lastReceivedPosition = transform.position;
        targetPosition = newPos;
        interpolationTime = 0f;
        rb.linearVelocity = newVelocity;
    }


    // Client RPC specifically for owner reconciliation
    [ClientRpc]
    private void ReconcilePositionClientRpc(Vector3 serverPos, Vector3 serverVel, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        Vector3 posDifference = serverPos - transform.position;

        // Only make MAJOR corrections when significantly off
        // Increase this threshold to allow more client prediction
        if (posDifference.magnitude > positionErrorThreshold * 1.5f && !isCorrectingPosition)
        {
            isCorrectingPosition = true;
            StartCoroutine(SmoothCorrection(serverPos, serverVel));
        }
        // For smaller differences, make minimal adjustments
        else if (posDifference.magnitude > 0.5f)
        {
            // Slight position nudging (10% toward server position)
            transform.position = Vector3.Lerp(transform.position, serverPos, 0.1f);
        }
    }


}